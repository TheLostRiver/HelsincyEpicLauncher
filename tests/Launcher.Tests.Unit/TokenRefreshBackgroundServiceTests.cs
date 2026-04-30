// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Background;
using Launcher.Background.Auth;
using Launcher.Background.Hosting;
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
        services.AddSingleton(Substitute.For<IAuthService>());

        services.AddBackground();

        using var provider = services.BuildServiceProvider();
        var workers = provider.GetServices<IBackgroundWorker>();

        workers.Should().ContainSingle(worker => worker is TokenRefreshBackgroundService);
    }
}
