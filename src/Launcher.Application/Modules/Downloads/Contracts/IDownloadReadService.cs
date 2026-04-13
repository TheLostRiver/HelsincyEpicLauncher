// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载只读查询服务。对外暴露的查询入口。
/// </summary>
public interface IDownloadReadService
{
    Task<DownloadStatusSummary?> GetStatusAsync(string assetId, CancellationToken ct = default);
    Task<IReadOnlyList<DownloadStatusSummary>> GetActiveDownloadsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DownloadStatusSummary>> GetHistoryAsync(int limit = 50, CancellationToken ct = default);
    int ActiveCount { get; }
}
