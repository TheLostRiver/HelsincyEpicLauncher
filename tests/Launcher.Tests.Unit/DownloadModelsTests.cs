// Copyright (c) Helsincy. All rights reserved.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Application.Modules.Downloads.Contracts;
using Launcher.Domain.Downloads;

namespace Launcher.Tests.Unit;

public class DownloadModelsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void DownloadStatusKind_Default_ShouldBeQueued()
    {
        default(DownloadStatusKind).Should().Be(DownloadStatusKind.Queued);
    }

    [Fact]
    public void DownloadStatusSummary_ShouldExposeContractOwnedStatusAlongsideLegacyUiState()
    {
        var summary = CreateSummary();

        summary.Status.Should().Be(DownloadStatusKind.Downloading);
        summary.UiState.Should().Be(DownloadUiState.Downloading);
        typeof(DownloadStatusSummary).GetProperty(nameof(DownloadStatusSummary.Status))!
            .PropertyType.Should().Be<DownloadStatusKind>();
    }

    [Fact]
    public void DownloadStatusSummary_Status_ShouldSerializeAsStringEnum()
    {
        var json = JsonSerializer.Serialize(CreateSummary(), JsonOptions);

        json.Should().Contain("\"status\":\"Downloading\"");
    }

    [Fact]
    public void DownloadStatusSummary_ShouldExposeContractOwnedTaskKey()
    {
        var taskId = DownloadTaskId.New();
        var summary = CreateSummary(taskId);

        summary.TaskKey.Value.Should().Be(taskId.Value);
        summary.TaskKey.ToLegacyTaskId().Should().Be(taskId);
    }

    [Fact]
    public void DownloadProgressSnapshot_ShouldExposeContractOwnedTaskKeyAndStatus()
    {
        var taskId = DownloadTaskId.New();
        var snapshot = new DownloadProgressSnapshot(
            taskId,
            DownloadUiState.Verifying,
            ProgressPercent: 75,
            DownloadedBytes: 768,
            TotalBytes: 1024,
            SpeedBytesPerSecond: 128);

        snapshot.TaskKey.Value.Should().Be(taskId.Value);
        snapshot.Status.Should().Be(DownloadStatusKind.Verifying);
    }

    [Fact]
    public void DownloadStatusSummary_Status_ShouldBeInitOnlyProjectionProperty()
    {
        var setter = typeof(DownloadStatusSummary)
            .GetProperty(nameof(DownloadStatusSummary.Status), BindingFlags.Public | BindingFlags.Instance)!
            .SetMethod;

        setter.Should().NotBeNull();
        setter!.ReturnParameter.GetRequiredCustomModifiers()
            .Should().Contain(typeof(IsExternalInit));
    }

    private static DownloadStatusSummary CreateSummary()
    {
        return CreateSummary(DownloadTaskId.New());
    }

    private static DownloadStatusSummary CreateSummary(DownloadTaskId taskId) => new()
    {
        TaskId = taskId,
        AssetId = "asset-1",
        AssetName = "Test Asset",
        UiState = DownloadUiState.Downloading,
        Status = DownloadStatusKind.Downloading,
        Progress = 0.5,
        DownloadedBytes = 512,
        TotalBytes = 1024,
        BytesPerSecond = 128,
        CanPause = true,
        CanResume = false,
        CanCancel = true,
    };
}
