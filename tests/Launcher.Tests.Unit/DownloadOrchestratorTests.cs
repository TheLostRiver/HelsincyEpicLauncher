// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;
using Launcher.Infrastructure.Downloads;
using Launcher.Shared;

namespace Launcher.Tests.Unit;

public class DownloadOrchestratorTests
{
    private readonly IDownloadTaskRepository _repository = Substitute.For<IDownloadTaskRepository>();
    private readonly IDownloadScheduler _scheduler = Substitute.For<IDownloadScheduler>();
    private readonly IDownloadTaskExecutor _executor = Substitute.For<IDownloadTaskExecutor>();
    private readonly DownloadOrchestrator _sut;

    public DownloadOrchestratorTests()
    {
        _sut = new DownloadOrchestrator(_repository, _scheduler, _executor);
    }

    private static StartDownloadRequest CreateRequest(string assetId = "asset-1", long totalBytes = 0) => new()
    {
        AssetId = assetId,
        AssetName = "Test Asset",
        DownloadUrl = "https://cdn.example.com/file.zip",
        DestinationPath = @"C:\Downloads\file.zip",
        TotalBytes = totalBytes,
        Priority = 0
    };

    // ── EnqueueAsync ──

    [Fact]
    public async Task Constructor_SchedulerTaskReady_DelegatesToDownloadTaskExecutor()
    {
        var scheduler = Substitute.For<IDownloadScheduler>();
        var executor = Substitute.For<IDownloadTaskExecutor>();
        Func<DownloadTaskId, CancellationToken, Task>? taskReadyHandler = null;
        scheduler
            .When(x => x.TaskReady += Arg.Any<Func<DownloadTaskId, CancellationToken, Task>>())
            .Do(call => taskReadyHandler = call.Arg<Func<DownloadTaskId, CancellationToken, Task>>());
        executor
            .ExecuteAsync(Arg.Any<DownloadTaskId>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _ = new DownloadOrchestrator(_repository, scheduler, executor);
        var taskId = DownloadTaskId.New();

        taskReadyHandler.Should().NotBeNull();
        await taskReadyHandler!(taskId, CancellationToken.None);

        await executor.Received(1).ExecuteAsync(taskId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueAsync_NewAsset_ReturnsTaskId()
    {
        _repository.GetByAssetIdAsync("asset-1", Arg.Any<CancellationToken>())
            .Returns((DownloadTask?)null);

        var result = await _sut.EnqueueAsync(CreateRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).InsertAsync(Arg.Any<DownloadTask>(), Arg.Any<CancellationToken>());
        await _scheduler.Received(1).QueueAsync(result.Value, 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueAsync_DuplicateActiveTask_Fails()
    {
        var existing = new DownloadTask(
            DownloadTaskId.New(), "asset-1", "Dup", "https://x.com/f", @"C:\f", 100);
        _repository.GetByAssetIdAsync("asset-1", Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _sut.EnqueueAsync(CreateRequest(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _repository.DidNotReceive().InsertAsync(Arg.Any<DownloadTask>(), Arg.Any<CancellationToken>());
    }

    // ── PauseAsync ──

    [Fact]
    public async Task PauseAsync_NotFound_Fails()
    {
        _repository.GetByIdAsync(Arg.Any<DownloadTaskId>(), Arg.Any<CancellationToken>())
            .Returns((DownloadTask?)null);

        var result = await _sut.PauseAsync(DownloadTaskId.New(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PauseAsync_QueuedTask_DequeuedFromScheduler()
    {
        var taskId = DownloadTaskId.New();
        var task = new DownloadTask(taskId, "a", "A", "https://x.com/f", @"C:\f", 100);
        // DownloadTask starts in Queued state
        _repository.GetByIdAsync(taskId, Arg.Any<CancellationToken>()).Returns(task);

        var result = await _sut.PauseAsync(taskId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _scheduler.Received(1).Dequeue(taskId);
    }

    // ── ResumeAsync ──

    [Fact]
    public async Task ResumeAsync_NotFound_Fails()
    {
        _repository.GetByIdAsync(Arg.Any<DownloadTaskId>(), Arg.Any<CancellationToken>())
            .Returns((DownloadTask?)null);

        var result = await _sut.ResumeAsync(DownloadTaskId.New(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // ── CancelAsync ──

    [Fact]
    public async Task CancelAsync_QueuedTask_Cancelled()
    {
        var taskId = DownloadTaskId.New();
        var task = new DownloadTask(taskId, "a", "A", "https://x.com/f", @"C:\f", 100);
        _repository.GetByIdAsync(taskId, Arg.Any<CancellationToken>()).Returns(task);

        var result = await _sut.CancelAsync(taskId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _scheduler.Received(1).Dequeue(taskId);
        await _repository.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_NotFound_Fails()
    {
        _repository.GetByIdAsync(Arg.Any<DownloadTaskId>(), Arg.Any<CancellationToken>())
            .Returns((DownloadTask?)null);

        var result = await _sut.CancelAsync(DownloadTaskId.New(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // ── RecoverAsync ──

    [Fact]
    public async Task RecoverAsync_QueuedTask_ReenqueuesInScheduler()
    {
        var taskId = DownloadTaskId.New();
        var task = new DownloadTask(taskId, "a", "A", "https://x.com/f", @"C:\f", 100);
        _repository.GetActiveTasksAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DownloadTask> { task });

        await _sut.RecoverAsync(CancellationToken.None);

        await _scheduler.Received(1).QueueAsync(taskId, 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverAsync_PausedTask_DoesNotReenqueue()
    {
        var taskId = DownloadTaskId.New();
        // Create a task in Paused state using the recovery constructor
        var task = new DownloadTask(
            taskId, "a", "A", "https://x.com/f", @"C:\f",
            totalBytes: 100, downloadedBytes: 50,
            state: DownloadState.Paused, priority: 0, retryCount: 0,
            lastError: null,
            createdAt: DateTimeOffset.UtcNow, updatedAt: DateTimeOffset.UtcNow);
        _repository.GetActiveTasksAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DownloadTask> { task });

        await _sut.RecoverAsync(CancellationToken.None);

        await _scheduler.DidNotReceive().QueueAsync(Arg.Any<DownloadTaskId>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // ── SetPriorityAsync ──

    [Fact]
    public async Task SetPriorityAsync_ExistingTask_UpdatesBoth()
    {
        var taskId = DownloadTaskId.New();
        var task = new DownloadTask(taskId, "a", "A", "https://x.com/f", @"C:\f", 100);
        _repository.GetByIdAsync(taskId, Arg.Any<CancellationToken>()).Returns(task);

        var result = await _sut.SetPriorityAsync(taskId, 5, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        task.Priority.Should().Be(5);
        await _repository.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).ReprioritizeAsync(taskId, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPriorityAsync_NotFound_Fails()
    {
        _repository.GetByIdAsync(Arg.Any<DownloadTaskId>(), Arg.Any<CancellationToken>())
            .Returns((DownloadTask?)null);

        var result = await _sut.SetPriorityAsync(DownloadTaskId.New(), 5, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // ── DownloadWorker ──

    [Fact]
    public async Task DownloadWorker_SuccessfulChunk_UpdatesRepositoryRuntimeStoreAndReleasesScheduler()
    {
        var taskId = DownloadTaskId.New();
        var task = new DownloadTask(taskId, "asset-1", "Asset", "https://cdn.example.com/a.zip", @"C:\Downloads\a.zip", 100);
        var runtimeStore = Substitute.For<IDownloadRuntimeStore>();
        var chunkDownloader = Substitute.For<IChunkDownloader>();
        _repository.GetByIdAsync(taskId, Arg.Any<CancellationToken>()).Returns(task);
        chunkDownloader
            .DownloadChunkAsync(Arg.Any<ChunkDownloadRequest>(), Arg.Any<IProgress<long>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new ChunkDownloadResult { BytesDownloaded = 100, HashMatch = true }));
        var worker = new DownloadWorker(_repository, _scheduler, runtimeStore, chunkDownloader);

        await worker.ExecuteAsync(taskId, CancellationToken.None);

        task.State.Should().Be(DownloadState.Completed);
        await _repository.Received().UpdateAsync(
            Arg.Is<DownloadTask>(candidate => candidate.Id == taskId && candidate.State == DownloadState.Completed),
            Arg.Any<CancellationToken>());
        runtimeStore.Received(1).NotifyCompleted(taskId, "asset-1", @"C:\Downloads\a.zip");
        _scheduler.Received(1).NotifyCompleted(taskId);
    }

    [Fact]
    public async Task DownloadWorker_FailedChunk_UpdatesRepositoryRuntimeStoreAndReleasesScheduler()
    {
        var taskId = DownloadTaskId.New();
        var task = new DownloadTask(taskId, "asset-2", "Asset", "https://cdn.example.com/b.zip", @"C:\Downloads\b.zip", 100);
        var runtimeStore = Substitute.For<IDownloadRuntimeStore>();
        var chunkDownloader = Substitute.For<IChunkDownloader>();
        var error = new Error
        {
            Code = "DL_TEST",
            UserMessage = "测试下载失败",
            TechnicalMessage = "chunk failed",
            CanRetry = true,
        };
        _repository.GetByIdAsync(taskId, Arg.Any<CancellationToken>()).Returns(task);
        chunkDownloader
            .DownloadChunkAsync(Arg.Any<ChunkDownloadRequest>(), Arg.Any<IProgress<long>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ChunkDownloadResult>(error));
        var worker = new DownloadWorker(_repository, _scheduler, runtimeStore, chunkDownloader);

        await worker.ExecuteAsync(taskId, CancellationToken.None);

        task.State.Should().Be(DownloadState.Failed);
        task.LastError.Should().Contain("chunk failed");
        await _repository.Received().UpdateAsync(
            Arg.Is<DownloadTask>(candidate => candidate.Id == taskId && candidate.State == DownloadState.Failed),
            Arg.Any<CancellationToken>());
        runtimeStore.Received(1).NotifyFailed(taskId, "asset-2", Arg.Is<string>(message => message.Contains("chunk failed")), true);
        _scheduler.Received(1).NotifyCompleted(taskId);
    }
}
