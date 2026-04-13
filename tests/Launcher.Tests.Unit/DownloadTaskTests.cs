// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Downloads;

namespace Launcher.Tests.Unit;

public class DownloadTaskTests
{
    private static readonly DownloadTaskId DefaultId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private static DownloadTask CreateDefaultTask(DownloadTaskId? id = null)
        => new(id ?? DefaultId, "asset-001", "Fortnite", "https://example.com/download", @"C:\Games\Fortnite", 10_000_000_000L);

    [Fact]
    public void Constructor_NewTask_InitializesCorrectly()
    {
        var task = CreateDefaultTask();

        task.Id.Should().Be(DefaultId);
        task.AssetId.Should().Be("asset-001");
        task.DisplayName.Should().Be("Fortnite");
        task.DownloadUrl.Should().Be("https://example.com/download");
        task.InstallPath.Should().Be(@"C:\Games\Fortnite");
        task.TotalBytes.Should().Be(10_000_000_000L);
        task.DownloadedBytes.Should().Be(0);
        task.SpeedBytesPerSecond.Should().Be(0);
        task.Priority.Should().Be(0);
        task.State.Should().Be(DownloadState.Queued);
        task.UiState.Should().Be(DownloadUiState.Queued);
        task.ProgressPercent.Should().Be(0);
        task.LastError.Should().BeNull();
        task.RetryCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_FromPersistence_RestoresState()
    {
        var id = new DownloadTaskId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var created = DateTimeOffset.UtcNow.AddHours(-1);
        var updated = DateTimeOffset.UtcNow;

        var task = new DownloadTask(
            id, "asset-002", "Rocket League", "https://example.com/rl", @"D:\Games\RL",
            5_000_000_000L, 2_500_000_000L,
            DownloadState.Paused, priority: 1, retryCount: 2,
            lastError: "网络超时", createdAt: created, updatedAt: updated);

        task.Id.Should().Be(id);
        task.State.Should().Be(DownloadState.Paused);
        task.DownloadedBytes.Should().Be(2_500_000_000L);
        task.ProgressPercent.Should().Be(50);
        task.RetryCount.Should().Be(2);
        task.LastError.Should().Be("网络超时");
        task.UiState.Should().Be(DownloadUiState.Paused);
    }

    [Fact]
    public void TransitionTo_ValidTransition_UpdatesStateAndTime()
    {
        var task = CreateDefaultTask();
        var beforeTransition = task.UpdatedAt;

        var result = task.TransitionTo(DownloadState.Preparing);

        result.IsSuccess.Should().BeTrue();
        task.State.Should().Be(DownloadState.Preparing);
        task.UpdatedAt.Should().BeOnOrAfter(beforeTransition);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_FailsAndKeepsState()
    {
        var task = CreateDefaultTask();

        var result = task.TransitionTo(DownloadState.Completed);

        result.IsFailure.Should().BeTrue();
        task.State.Should().Be(DownloadState.Queued);
    }

    [Fact]
    public void CanTransitionTo_DelegatesToStateMachine()
    {
        var task = CreateDefaultTask();

        task.CanTransitionTo(DownloadState.Preparing).Should().BeTrue();
        task.CanTransitionTo(DownloadState.Completed).Should().BeFalse();
    }

    [Fact]
    public void UpdateProgress_UpdatesBytesAndSpeed()
    {
        var task = CreateDefaultTask();

        task.UpdateProgress(5_000_000_000L, 100_000_000L);

        task.DownloadedBytes.Should().Be(5_000_000_000L);
        task.SpeedBytesPerSecond.Should().Be(100_000_000L);
        task.ProgressPercent.Should().Be(50);
    }

    [Fact]
    public void ProgressPercent_ZeroTotalBytes_ReturnsZero()
    {
        var task = new DownloadTask(DownloadTaskId.New(), "a1", "Test", "https://example.com/test", @"C:\Test", 0);

        task.ProgressPercent.Should().Be(0);
    }

    [Fact]
    public void SetTotalBytes_UpdatesTotalAndTime()
    {
        var task = CreateDefaultTask();

        task.SetTotalBytes(20_000_000_000L);

        task.TotalBytes.Should().Be(20_000_000_000L);
    }

    [Fact]
    public void SetError_RecordsErrorAndIncrementsRetry()
    {
        var task = CreateDefaultTask();

        task.SetError("连接超时");
        task.SetError("磁盘已满");

        task.LastError.Should().Be("磁盘已满");
        task.RetryCount.Should().Be(2);
    }

    [Fact]
    public void ClearError_RemovesLastError()
    {
        var task = CreateDefaultTask();
        task.SetError("连接超时");

        task.ClearError();

        task.LastError.Should().BeNull();
        task.RetryCount.Should().Be(1); // 不重置重试计数
    }

    [Fact]
    public void Priority_CanBeSet()
    {
        var task = CreateDefaultTask();

        task.Priority = 5;

        task.Priority.Should().Be(5);
    }
}
