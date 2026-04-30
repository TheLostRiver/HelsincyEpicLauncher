// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Launcher.Infrastructure.Downloads;

namespace Launcher.Tests.Unit;

public sealed class DownloadWorkerContractTests : IDisposable
{
    private readonly DownloadScheduler _scheduler = new();

    public void Dispose()
    {
        _scheduler.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DownloadTaskExecutorPort_ShouldExposeSingleTaskExecutionContract()
    {
        var method = typeof(IDownloadTaskExecutor).GetMethod(
            nameof(IDownloadTaskExecutor.ExecuteAsync),
            [typeof(DownloadTaskId), typeof(CancellationToken)]);

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<Task>();
    }

    [Fact]
    public async Task SchedulerTaskReady_ShouldDispatchToDownloadTaskExecutorOnce()
    {
        var executor = Substitute.For<IDownloadTaskExecutor>();
        executor
            .ExecuteAsync(Arg.Any<DownloadTaskId>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var taskId = DownloadTaskId.New();
        _scheduler.TaskReady += executor.ExecuteAsync;

        await _scheduler.QueueAsync(taskId, 0, CancellationToken.None);
        await Task.Delay(50);

        await executor.Received(1).ExecuteAsync(taskId, Arg.Any<CancellationToken>());
        _scheduler.ActiveCount.Should().Be(1);
    }

    [Fact]
    public void Scheduler_ShouldNotInstantiateConcreteDownloadWorker()
    {
        var source = File.ReadAllText(Path.Combine(
            FindSolutionRoot(),
            "src",
            "Launcher.Infrastructure",
            "Downloads",
            "DownloadScheduler.cs"));

        source.Should().NotContain("new DownloadWorker");
        source.Should().NotContain("new ChunkDownloadClient");
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
}
