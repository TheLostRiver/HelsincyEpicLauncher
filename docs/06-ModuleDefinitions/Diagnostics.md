# Diagnostics 模块

---

## 架构定义

### 职责

- 日志查看面板（实时日志流 + 历史日志搜索）
- 日志分级显示（Debug / Info / Warning / Error / Fatal）
- 网络连通性诊断
- 磁盘空间监控
- 缓存管理（查看缓存大小、清理缓存）
- CPU / 内存使用概览
- 导出诊断报告

### 不负责

- 日志写入（由 Infrastructure.Logging 处理）
- 网络请求（由各模块的 API 客户端处理）
- 文件修复（由 Installations 处理）

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `Settings.Contracts` | 读取日志路径、缓存路径 |
| `Launcher.Shared` | Result 模型 |

### 谁可以依赖 Diagnostics

无。Diagnostics 是末端工具模块。

---

## API 定义

```csharp
namespace Launcher.Application.Modules.Diagnostics.Contracts;

public interface IDiagnosticsReadService
{
    /// <summary>获取最近日志条目</summary>
    Task<IReadOnlyList<LogEntry>> GetRecentLogsAsync(int count, LogLevel? minLevel, CancellationToken ct);

    /// <summary>搜索日志</summary>
    Task<IReadOnlyList<LogEntry>> SearchLogsAsync(string query, DateTime? from, DateTime? to, CancellationToken ct);

    /// <summary>获取系统诊断摘要</summary>
    Task<SystemDiagnosticsSummary> GetSystemSummaryAsync(CancellationToken ct);

    /// <summary>获取缓存使用统计</summary>
    Task<CacheStatistics> GetCacheStatisticsAsync(CancellationToken ct);
}

public interface IDiagnosticsCommandService
{
    /// <summary>清理缩略图缓存</summary>
    Task<Result> ClearThumbnailCacheAsync(CancellationToken ct);

    /// <summary>清理 Manifest 缓存</summary>
    Task<Result> ClearManifestCacheAsync(CancellationToken ct);

    /// <summary>清理全部缓存</summary>
    Task<Result> ClearAllCacheAsync(CancellationToken ct);

    /// <summary>导出诊断报告到文件</summary>
    Task<Result<string>> ExportDiagnosticsReportAsync(string outputPath, CancellationToken ct);

    /// <summary>测试网络连通性</summary>
    Task<Result<NetworkTestResult>> TestNetworkAsync(CancellationToken ct);
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Source { get; init; } = default!;
    public string Message { get; init; } = default!;
    public string? Exception { get; init; }
}

public sealed class SystemDiagnosticsSummary
{
    public long AvailableDiskSpaceMb { get; init; }
    public long UsedMemoryMb { get; init; }
    public long TotalMemoryMb { get; init; }
    public double CpuUsagePercent { get; init; }
    public string OsVersion { get; init; } = default!;
    public string AppVersion { get; init; } = default!;
    public DateTime AppStartedAt { get; init; }
}

public sealed class CacheStatistics
{
    public long ThumbnailCacheSizeMb { get; init; }
    public long ManifestCacheSizeMb { get; init; }
    public long TotalCacheSizeMb { get; init; }
    public int CachedThumbnailCount { get; init; }
    public int CachedManifestCount { get; init; }
}

public sealed class NetworkTestResult
{
    public bool EpicApiReachable { get; init; }
    public bool FabApiReachable { get; init; }
    public bool CdnReachable { get; init; }
    public int LatencyMs { get; init; }
    public string? ErrorDetail { get; init; }
}
```

---

## UI 结构

```
DiagnosticsPage
├─ TabControl
│  ├─ 日志 Tab
│  │  ├─ 日志级别筛选器
│  │  ├─ 搜索框
│  │  ├─ 虚拟化日志列表
│  │  └─ 导出按钮
│  ├─ 系统 Tab
│  │  ├─ 磁盘空间仪表盘
│  │  ├─ 内存使用图表
│  │  └─ 应用版本/运行时间
│  ├─ 缓存 Tab
│  │  ├─ 各类缓存大小显示
│  │  └─ 清理按钮（缩略图/Manifest/全部）
│  └─ 网络 Tab
│     ├─ 连通性测试按钮
│     └─ 测试结果显示
└─ 导出诊断报告按钮
```
