// Copyright (c) Helsincy. All rights reserved.

using Launcher.Background.Hosting;
using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Application.Modules.Updates.Contracts;
using Launcher.Background;
using Launcher.Background.Auth;
using Launcher.Background.Installations;
using Launcher.Background.Network;
using Launcher.Background.Updates;
using Launcher.Shared.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Launcher.Tests.Unit;

public sealed class BackgroundTaskHostTests
{
    [Fact]
    public async Task StartAllAsync_ShouldStartWorkersInRegistrationOrder()
    {
        var calls = new List<string>();
        var first = new RecordingWorker("first", calls);
        var second = new RecordingWorker("second", calls);
        var host = new BackgroundTaskHost([first, second]);

        await host.StartAllAsync(CancellationToken.None);

        calls.Should().Equal("start:first", "start:second");
        first.State.Should().Be(WorkerStatus.Running);
        second.State.Should().Be(WorkerStatus.Running);
    }

    [Fact]
    public async Task StartAllAsync_ShouldContinueAfterWorkerFailureAndRecordFault()
    {
        var calls = new List<string>();
        var failing = new RecordingWorker("failing", calls, failOnStart: true);
        var second = new RecordingWorker("second", calls);
        var host = new BackgroundTaskHost([failing, second]);

        await host.StartAllAsync(CancellationToken.None);

        calls.Should().Equal("start:failing", "start:second");
        failing.State.Should().Be(WorkerStatus.Faulted);
        second.State.Should().Be(WorkerStatus.Running);
        host.Faults.Should().ContainKey("failing");
    }

    [Fact]
    public async Task StopAllAsync_ShouldStopWorkersInReverseRegistrationOrder()
    {
        var calls = new List<string>();
        var first = new RecordingWorker("first", calls);
        var second = new RecordingWorker("second", calls);
        var host = new BackgroundTaskHost([first, second]);

        await host.StartAllAsync(CancellationToken.None);
        calls.Clear();

        await host.StopAllAsync(CancellationToken.None);

        calls.Should().Equal("stop:second", "stop:first");
        first.State.Should().Be(WorkerStatus.Stopped);
        second.State.Should().Be(WorkerStatus.Stopped);
    }

    [Fact]
    public void AddBackground_ShouldRegisterTaskHostAndWorkers()
    {
        var services = new ServiceCollection();
        RegisterBackgroundDependencies(services);

        services.AddBackground();

        using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<IBackgroundTaskHost>();
        var workerTypes = host.Workers.Select(worker => worker.GetType()).ToArray();

        host.Should().BeOfType<BackgroundTaskHost>();
        workerTypes.Should().BeEquivalentTo(
        [
            typeof(TokenRefreshBackgroundService),
            typeof(AutoInstallWorker),
            typeof(AppUpdateWorker),
            typeof(NetworkMonitorWorker),
        ]);
    }

    [Fact]
    public void AppStartup_ShouldResolveBackgroundTaskHostInsteadOfConcreteWorkers()
    {
        var source = File.ReadAllText(Path.Combine(FindSolutionRoot(), "src", "Launcher.App", "App.xaml.cs"));

        source.Should().Contain("GetRequiredService<IBackgroundTaskHost>()");
        source.Should().NotContain("GetRequiredService<Launcher.Background.Auth.TokenRefreshBackgroundService>");
        source.Should().NotContain("GetRequiredService<Launcher.Background.Installations.AutoInstallWorker>");
        source.Should().NotContain("GetRequiredService<Launcher.Background.Updates.AppUpdateWorker>");
        source.Should().NotContain("GetRequiredService<Launcher.Background.Network.NetworkMonitorWorker>");
        source.Should().NotContain("StartFabLibraryWarmup");
    }

    private static void RegisterBackgroundDependencies(IServiceCollection services)
    {
        services.AddSingleton(Substitute.For<IAuthService>());
        services.AddSingleton(Substitute.For<IDownloadRuntimeStore>());
        services.AddSingleton(Substitute.For<IDownloadReadService>());
        services.AddSingleton(Substitute.For<ISettingsReadService>());
        services.AddSingleton(Substitute.For<IInstallCommandService>());
        services.AddSingleton(Substitute.For<IAppConfigProvider>());
        services.AddSingleton(Substitute.For<IAppUpdateService>());
        services.AddSingleton(Substitute.For<INetworkMonitor>());
        services.AddSingleton(Substitute.For<IDownloadCommandService>());
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HelsincyEpicLauncher.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output directory.");
    }

    private sealed class RecordingWorker : IBackgroundWorker
    {
        private readonly List<string> _calls;
        private readonly bool _failOnStart;

        public RecordingWorker(string name, List<string> calls, bool failOnStart = false)
        {
            Name = name;
            _calls = calls;
            _failOnStart = failOnStart;
        }

        public string Name { get; }

        public WorkerStatus State { get; private set; } = WorkerStatus.Idle;

        public Task StartAsync(CancellationToken ct = default)
        {
            _calls.Add($"start:{Name}");
            if (_failOnStart)
            {
                State = WorkerStatus.Faulted;
                throw new InvalidOperationException($"Failed to start {Name}");
            }

            State = WorkerStatus.Running;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            _calls.Add($"stop:{Name}");
            State = WorkerStatus.Stopped;
            return Task.CompletedTask;
        }
    }
}
