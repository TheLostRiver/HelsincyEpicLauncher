// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Application.Modules.Downloads.UseCases;
using Launcher.Domain.Downloads;
using Launcher.Shared;

namespace Launcher.Tests.Unit.Downloads;

public class StartDownloadUseCaseTests
{
    private readonly IDownloadOrchestrator _orchestrator = Substitute.For<IDownloadOrchestrator>();
    private readonly StartDownloadUseCase _sut;

    public StartDownloadUseCaseTests()
    {
        _sut = new StartDownloadUseCase(_orchestrator);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_DelegatesToOrchestratorAndReturnsTaskId()
    {
        var request = CreateRequest();
        var expectedTaskId = DownloadTaskId.New();
        _orchestrator.EnqueueAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(expectedTaskId));

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedTaskId);
        await _orchestrator.Received(1).EnqueueAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_BlankAssetId_FailsWithoutDelegating()
    {
        var request = CreateRequest(assetId: " ");

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("DOWNLOAD_START_INVALID_REQUEST");
        await _orchestrator.DidNotReceive().EnqueueAsync(Arg.Any<StartDownloadRequest>(), Arg.Any<CancellationToken>());
    }

    private static StartDownloadRequest CreateRequest(string assetId = "asset-1") => new()
    {
        AssetId = assetId,
        AssetName = "Test Asset",
        DownloadUrl = "https://cdn.example.com/file.zip",
        DestinationPath = @"C:\Downloads\file.zip",
        TotalBytes = 1024,
        Priority = 0
    };
}
