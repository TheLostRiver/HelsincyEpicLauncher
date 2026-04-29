// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Downloads.UseCases;
using Launcher.Domain.Downloads;
using Launcher.Shared;

namespace Launcher.Tests.Unit;

public class DownloadCommandServiceTests
{
    private readonly IDownloadOrchestrator _orchestrator = Substitute.For<IDownloadOrchestrator>();
    private readonly DownloadCommandService _sut;

    public DownloadCommandServiceTests()
    {
        _sut = new DownloadCommandService(new StartDownloadUseCase(_orchestrator), _orchestrator);
    }

    [Fact]
    public async Task StartAsync_ValidRequest_DelegatesThroughStartUseCase()
    {
        var request = CreateRequest();
        var taskId = DownloadTaskId.New();
        _orchestrator.EnqueueAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(taskId));

        var result = await _sut.StartAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(taskId);
        await _orchestrator.Received(1).EnqueueAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PauseAllAsync_PausesAllActiveTasks()
    {
        var first = DownloadTaskId.New();
        var second = DownloadTaskId.New();
        _orchestrator.GetActiveTaskIdsAsync(Arg.Any<CancellationToken>())
            .Returns([first, second]);
        _orchestrator.PauseAsync(Arg.Any<DownloadTaskId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var result = await _sut.PauseAllAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _orchestrator.Received(1).PauseAsync(first, Arg.Any<CancellationToken>());
        await _orchestrator.Received(1).PauseAsync(second, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeAllAsync_ResumesAllPausedTasks()
    {
        var first = DownloadTaskId.New();
        var second = DownloadTaskId.New();
        _orchestrator.GetPausedTaskIdsAsync(Arg.Any<CancellationToken>())
            .Returns([first, second]);
        _orchestrator.ResumeAsync(Arg.Any<DownloadTaskId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        var result = await _sut.ResumeAllAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _orchestrator.Received(1).ResumeAsync(first, Arg.Any<CancellationToken>());
        await _orchestrator.Received(1).ResumeAsync(second, Arg.Any<CancellationToken>());
    }

    private static StartDownloadRequest CreateRequest() => new()
    {
        AssetId = "asset-1",
        AssetName = "Test Asset",
        DownloadUrl = "https://cdn.example.com/file.zip",
        DestinationPath = @"C:\Downloads\file.zip",
        TotalBytes = 1024,
        Priority = 0
    };
}
