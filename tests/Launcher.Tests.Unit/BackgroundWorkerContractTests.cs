// Copyright (c) Helsincy. All rights reserved.

using Launcher.Background.Hosting;

namespace Launcher.Tests.Unit;

public sealed class BackgroundWorkerContractTests
{
    [Fact]
    public void WorkerStatus_ShouldCoverExpectedLifecycleStates()
    {
        var statuses = Enum.GetNames<WorkerStatus>();

        statuses.Should().Equal(
            nameof(WorkerStatus.Idle),
            nameof(WorkerStatus.Running),
            nameof(WorkerStatus.Stopping),
            nameof(WorkerStatus.Stopped),
            nameof(WorkerStatus.Faulted));
    }

    [Fact]
    public void IBackgroundWorker_ShouldExposeNameStateStartAndStop()
    {
        var contract = typeof(IBackgroundWorker);

        contract.GetProperty(nameof(IBackgroundWorker.Name))!
            .PropertyType.Should().Be<string>();
        contract.GetProperty(nameof(IBackgroundWorker.State))!
            .PropertyType.Should().Be<WorkerStatus>();

        var start = contract.GetMethod(nameof(IBackgroundWorker.StartAsync))!;
        start.ReturnType.Should().Be<Task>();
        start.GetParameters().Should().ContainSingle()
            .Which.ParameterType.Should().Be<CancellationToken>();

        var stop = contract.GetMethod(nameof(IBackgroundWorker.StopAsync))!;
        stop.ReturnType.Should().Be<Task>();
        stop.GetParameters().Should().ContainSingle()
            .Which.ParameterType.Should().Be<CancellationToken>();
    }
}
