// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Launcher.Application.Modules.Updates.Contracts;
using Launcher.Background;
using Launcher.Background.Auth;
using Launcher.Background.Hosting;
using Launcher.Shared.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Launcher.Tests.Unit;

public sealed class TokenRefreshBackgroundServiceTests
{
    [Fact]
    public async Task TokenRefreshBackgroundService_ShouldExposeBackgroundWorkerLifecycle()
    {
        var authService = Substitute.For<IAuthService>();
        using var service = new TokenRefreshBackgroundService(authService);

        var worker = service.Should().BeAssignableTo<IBackgroundWorker>().Subject;

        worker.Name.Should().Be(nameof(TokenRefreshBackgroundService));
        worker.State.Should().Be(WorkerStatus.Idle);

        await worker.StartAsync(CancellationToken.None);
        worker.State.Should().Be(WorkerStatus.Running);

        await worker.StopAsync(CancellationToken.None);
        worker.State.Should().Be(WorkerStatus.Stopped);
    }

    [Fact]
    public void AddBackground_ShouldRegisterTokenRefreshAsBackgroundWorker()
    {
        var services = new ServiceCollection();
        RegisterBackgroundWorkerDependencies(services);

        services.AddBackground();

        using var provider = services.BuildServiceProvider();
        var workers = provider.GetServices<IBackgroundWorker>();

        workers.Should().ContainSingle(worker => worker is TokenRefreshBackgroundService);
    }

    private static void RegisterBackgroundWorkerDependencies(IServiceCollection services)
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
}
