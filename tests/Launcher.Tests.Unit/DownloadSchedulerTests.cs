// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Downloads;
using Launcher.Infrastructure.Downloads;

namespace Launcher.Tests.Unit;

public class DownloadSchedulerTests : IDisposable
{
    private readonly DownloadScheduler _sut = new();

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task QueueAsync_UnderConcurrencyLimit_TriggersTaskReady()
    {
        var triggered = new List<DownloadTaskId>();
        _sut.TaskReady += (taskId, _) => { triggered.Add(taskId); return Task.CompletedTask; };

        var taskId = DownloadTaskId.New();
        await _sut.QueueAsync(taskId, 0, CancellationToken.None);

        // Allow async scheduling to complete
        await Task.Delay(50);
        triggered.Should().Contain(taskId);
        _sut.ActiveCount.Should().Be(1);
    }

    [Fact]
    public async Task QueueAsync_ExceedsConcurrencyLimit_QueuesWithoutTriggering()
    {
        var triggered = new List<DownloadTaskId>();
        _sut.TaskReady += (taskId, _) => { triggered.Add(taskId); return Task.CompletedTask; };
        _sut.MaxConcurrency = 2;

        // Fill up concurrency slots
        await _sut.QueueAsync(DownloadTaskId.New(), 0, CancellationToken.None);
        await _sut.QueueAsync(DownloadTaskId.New(), 0, CancellationToken.None);
        await Task.Delay(50);

        triggered.Should().HaveCount(2);

        // Third task should be queued but not triggered
        var thirdId = DownloadTaskId.New();
        await _sut.QueueAsync(thirdId, 0, CancellationToken.None);
        await Task.Delay(50);

        triggered.Should().HaveCount(2);
        _sut.ActiveCount.Should().Be(2);
    }

    [Fact]
    public async Task NotifyCompleted_SchedulesNextTask()
    {
        var triggered = new List<DownloadTaskId>();
        _sut.TaskReady += (taskId, _) => { triggered.Add(taskId); return Task.CompletedTask; };
        _sut.MaxConcurrency = 1;

        var first = DownloadTaskId.New();
        var second = DownloadTaskId.New();
        await _sut.QueueAsync(first, 0, CancellationToken.None);
        await Task.Delay(50);
        await _sut.QueueAsync(second, 0, CancellationToken.None);
        await Task.Delay(50);

        triggered.Should().HaveCount(1);

        _sut.NotifyCompleted(first);
        await Task.Delay(50);

        triggered.Should().HaveCount(2);
        triggered.Last().Should().Be(second);
    }

    [Fact]
    public async Task Dequeue_ActiveTask_CancelsAndRemoves()
    {
        CancellationToken receivedCt = default;
        _sut.TaskReady += (_, ct) => { receivedCt = ct; return Task.CompletedTask; };

        var taskId = DownloadTaskId.New();
        await _sut.QueueAsync(taskId, 0, CancellationToken.None);
        await Task.Delay(50);

        _sut.Dequeue(taskId);

        _sut.ActiveCount.Should().Be(0);
        receivedCt.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task RequestPause_CancelsCancellationToken()
    {
        CancellationToken receivedCt = default;
        _sut.TaskReady += (_, ct) => { receivedCt = ct; return Task.CompletedTask; };

        var taskId = DownloadTaskId.New();
        await _sut.QueueAsync(taskId, 0, CancellationToken.None);
        await Task.Delay(50);

        _sut.RequestPause(taskId);

        receivedCt.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task TaskReady_ThrowsException_DoesNotCrashScheduler()
    {
        _sut.TaskReady += (_, _) => throw new InvalidOperationException("boom");

        var taskId = DownloadTaskId.New();
        await _sut.QueueAsync(taskId, 0, CancellationToken.None);
        await Task.Delay(50);

        // Scheduler should still be functional
        _sut.ActiveCount.Should().Be(0); // Failed task removed from active
    }
}
