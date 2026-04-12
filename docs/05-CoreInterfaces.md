# 核心接口设计

> 本文档定义跨模块的关键接口。这些接口是模块之间的"通信管道"，也是架构边界的物理体现。
> 所有接口均位于对应模块的 `Contracts/` 目录中。

---

## 1. 导航服务

```csharp
namespace Launcher.Presentation.Shell.Navigation;

/// <summary>
/// 页面导航服务。Shell 壳层和各模块通过此接口进行页面跳转。
/// </summary>
public interface INavigationService
{
    /// <summary>导航到指定路由</summary>
    Task NavigateAsync(string route, object? parameter = null);

    /// <summary>返回上一页</summary>
    Task GoBackAsync();

    /// <summary>是否可以返回</summary>
    bool CanGoBack { get; }

    /// <summary>当前路由</summary>
    string CurrentRoute { get; }
}
```

---

## 2. 对话框服务

```csharp
namespace Launcher.Presentation.Shell;

/// <summary>
/// 统一对话框服务。模块不直接弹窗，而是通过此接口请求对话框。
/// </summary>
public interface IDialogService
{
    /// <summary>显示确认对话框</summary>
    Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "确认", string cancelText = "取消");

    /// <summary>显示信息对话框</summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>显示错误对话框</summary>
    Task ShowErrorAsync(string title, string message, bool canRetry = false);

    /// <summary>显示自定义内容对话框</summary>
    Task<TResult?> ShowCustomAsync<TResult>(object dialogViewModel);
}
```

---

## 3. 通知服务

```csharp
namespace Launcher.Presentation.Shell;

/// <summary>
/// 全局 Toast 通知服务。
/// </summary>
public interface INotificationService
{
    /// <summary>显示成功通知</summary>
    void ShowSuccess(string message, TimeSpan? duration = null);

    /// <summary>显示警告通知</summary>
    void ShowWarning(string message, TimeSpan? duration = null);

    /// <summary>显示错误通知</summary>
    void ShowError(string message, TimeSpan? duration = null);

    /// <summary>显示信息通知</summary>
    void ShowInfo(string message, TimeSpan? duration = null);
}
```

---

## 4. 认证服务

```csharp
namespace Launcher.Application.Modules.Auth.Contracts;

/// <summary>
/// Epic Games 认证服务。处理 OAuth 2.0 登录流程和 Token 管理。
/// </summary>
public interface IAuthService
{
    /// <summary>当前是否已认证</summary>
    bool IsAuthenticated { get; }

    /// <summary>当前登录用户信息</summary>
    AuthUserInfo? CurrentUser { get; }

    /// <summary>启动 OAuth 登录流程</summary>
    Task<Result<AuthUserInfo>> LoginAsync(CancellationToken ct);

    /// <summary>登出</summary>
    Task<Result> LogoutAsync(CancellationToken ct);

    /// <summary>获取有效的 Access Token（自动刷新过期 Token）</summary>
    Task<Result<string>> GetAccessTokenAsync(CancellationToken ct);

    /// <summary>尝试从缓存恢复会话（启动时调用）</summary>
    Task<Result<AuthUserInfo>> TryRestoreSessionAsync(CancellationToken ct);
}

/// <summary>认证用户信息</summary>
public sealed class AuthUserInfo
{
    public string AccountId { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string Email { get; init; } = default!;
}
```

---

## 5. 下载服务（对外 Contracts）

### 5.1 只读查询

```csharp
namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载状态查询服务。其他模块通过此接口查询下载状态，不暴露下载器内部细节。
/// </summary>
public interface IDownloadReadService
{
    /// <summary>查询单个资产的下载状态</summary>
    Task<DownloadStatusSummary?> GetStatusAsync(string assetId, CancellationToken ct);

    /// <summary>查询所有活跃下载</summary>
    Task<IReadOnlyList<DownloadStatusSummary>> GetActiveDownloadsAsync(CancellationToken ct);

    /// <summary>查询下载历史</summary>
    Task<IReadOnlyList<DownloadStatusSummary>> GetHistoryAsync(int limit, CancellationToken ct);

    /// <summary>当前活跃下载数</summary>
    int ActiveCount { get; }
}
```

### 5.2 命令操作

```csharp
namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载命令服务。其他模块通过此接口发起/控制下载操作。
/// </summary>
public interface IDownloadCommandService
{
    /// <summary>将资产加入下载队列</summary>
    Task<Result<DownloadTaskId>> StartAsync(StartDownloadRequest request, CancellationToken ct);

    /// <summary>暂停下载</summary>
    Task<Result> PauseAsync(DownloadTaskId taskId, CancellationToken ct);

    /// <summary>恢复下载</summary>
    Task<Result> ResumeAsync(DownloadTaskId taskId, CancellationToken ct);

    /// <summary>取消下载</summary>
    Task<Result> CancelAsync(DownloadTaskId taskId, CancellationToken ct);

    /// <summary>调整优先级</summary>
    Task<Result> SetPriorityAsync(DownloadTaskId taskId, int priority, CancellationToken ct);
}
```

### 5.3 下载编排器（内部使用）

```csharp
namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载编排器。负责下载的完整生命周期管理。
/// 由 Application 层 Handler 调用，不直接暴露给 UI。
/// </summary>
public interface IDownloadOrchestrator
{
    /// <summary>编排一个完整的下载任务</summary>
    Task<Result<DownloadTaskId>> EnqueueAsync(StartDownloadRequest request, CancellationToken ct);

    /// <summary>暂停任务</summary>
    Task<Result> PauseAsync(DownloadTaskId taskId, CancellationToken ct);

    /// <summary>恢复任务</summary>
    Task<Result> ResumeAsync(DownloadTaskId taskId, CancellationToken ct);

    /// <summary>取消任务</summary>
    Task<Result> CancelAsync(DownloadTaskId taskId, CancellationToken ct);

    /// <summary>崩溃恢复：从持久化状态恢复所有中断的任务</summary>
    Task RecoverAsync(CancellationToken ct);
}
```

### 5.4 下载 DTO

```csharp
namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>下载任务 ID 值对象</summary>
public readonly record struct DownloadTaskId(Guid Value);

/// <summary>开始下载请求</summary>
public sealed class StartDownloadRequest
{
    public string AssetId { get; init; } = default!;
    public string AssetName { get; init; } = default!;
    public string DownloadUrl { get; init; } = default!;
    public string DestinationPath { get; init; } = default!;
    public long TotalBytes { get; init; }
    public int Priority { get; init; }
}

/// <summary>
/// 下载状态摘要 — 对外投影。
/// 这是其他模块和 UI 消费的唯一下载状态模型。
/// </summary>
public sealed class DownloadStatusSummary
{
    public DownloadTaskId TaskId { get; init; }
    public string AssetId { get; init; } = default!;
    public string AssetName { get; init; } = default!;
    public DownloadUiState UiState { get; init; }
    public double Progress { get; init; }           // 0.0 ~ 1.0
    public long DownloadedBytes { get; init; }
    public long TotalBytes { get; init; }
    public long BytesPerSecond { get; init; }       // 当前速度
    public TimeSpan? EstimatedRemaining { get; init; }
    public bool CanPause { get; init; }
    public bool CanResume { get; init; }
    public bool CanCancel { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>对外 UI 状态枚举（收敛后）</summary>
public enum DownloadUiState
{
    Queued,
    Downloading,
    Paused,
    Verifying,
    Installing,
    Completed,
    Failed,
    Cancelled
}
```

---

## 6. 下载调度器

```csharp
namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载调度器。管理下载队列和并发限制。
/// </summary>
public interface IDownloadScheduler
{
    /// <summary>将任务加入调度队列</summary>
    Task QueueAsync(DownloadTaskId taskId, int priority, CancellationToken ct);

    /// <summary>调整任务优先级</summary>
    Task ReprioritizeAsync(DownloadTaskId taskId, int priority, CancellationToken ct);

    /// <summary>获取当前活跃任务列表</summary>
    Task<IReadOnlyList<DownloadTaskId>> GetActiveTaskIdsAsync(CancellationToken ct);

    /// <summary>当前最大并发数</summary>
    int MaxConcurrency { get; set; }
}
```

---

## 7. Chunk 下载器

```csharp
namespace Launcher.Infrastructure.Network.Download;

/// <summary>
/// 分块下载器。负责单个 Chunk 的 HTTP 下载。
/// </summary>
public interface IChunkDownloader
{
    /// <summary>下载单个分块</summary>
    Task<Result<ChunkDownloadResult>> DownloadChunkAsync(
        ChunkDownloadRequest request,
        IProgress<long>? progress,
        CancellationToken ct);
}

public sealed class ChunkDownloadRequest
{
    public string Url { get; init; } = default!;
    public string DestinationPath { get; init; } = default!;
    public long RangeStart { get; init; }
    public long RangeEnd { get; init; }
    public string? ExpectedHash { get; init; }
}

public sealed class ChunkDownloadResult
{
    public long BytesDownloaded { get; init; }
    public string ActualHash { get; init; } = default!;
    public bool HashMatch { get; init; }
}
```

---

## 8. 完整性校验器

```csharp
namespace Launcher.Application.Modules.Installations.Contracts;

/// <summary>
/// 文件完整性校验服务。
/// </summary>
public interface IIntegrityVerifier
{
    /// <summary>校验单个文件哈希</summary>
    Task<Result<bool>> VerifyFileAsync(string filePath, string expectedHash, CancellationToken ct);

    /// <summary>校验整个安装目录</summary>
    Task<Result<VerificationReport>> VerifyInstallationAsync(
        string installPath, 
        InstallManifest manifest, 
        IProgress<VerificationProgress>? progress,
        CancellationToken ct);
}

public sealed class VerificationReport
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> MissingFiles { get; init; } = [];
    public IReadOnlyList<string> CorruptedFiles { get; init; } = [];
    public long TotalFilesChecked { get; init; }
}

public sealed class VerificationProgress
{
    public long CheckedFiles { get; init; }
    public long TotalFiles { get; init; }
    public string CurrentFile { get; init; } = default!;
}
```

---

## 9. 安装服务

```csharp
namespace Launcher.Application.Modules.Installations.Contracts;

/// <summary>
/// 安装命令服务。
/// </summary>
public interface IInstallCommandService
{
    /// <summary>安装已下载的资产</summary>
    Task<Result> InstallAsync(InstallRequest request, CancellationToken ct);

    /// <summary>卸载已安装的资产</summary>
    Task<Result> UninstallAsync(string assetId, CancellationToken ct);

    /// <summary>修复损坏的安装</summary>
    Task<Result> RepairAsync(string assetId, CancellationToken ct);
}

/// <summary>
/// 安装状态查询服务。
/// </summary>
public interface IInstallReadService
{
    /// <summary>获取资产安装状态</summary>
    Task<InstallStatusSummary?> GetStatusAsync(string assetId, CancellationToken ct);

    /// <summary>获取所有已安装资产</summary>
    Task<IReadOnlyList<InstallStatusSummary>> GetInstalledAsync(CancellationToken ct);
}

public sealed class InstallRequest
{
    public string AssetId { get; init; } = default!;
    public string SourcePath { get; init; } = default!;      // 下载完成的文件路径
    public string InstallPath { get; init; } = default!;     // 安装目标路径
}

public sealed class InstallStatusSummary
{
    public string AssetId { get; init; } = default!;
    public string AssetName { get; init; } = default!;
    public string InstallPath { get; init; } = default!;
    public string Version { get; init; } = default!;
    public long SizeOnDisk { get; init; }
    public DateTime InstalledAt { get; init; }
    public bool NeedsRepair { get; init; }
}
```

---

## 10. Fab 资产库服务

```csharp
namespace Launcher.Application.Modules.FabLibrary.Contracts;

/// <summary>
/// Fab 资产目录查询服务。
/// </summary>
public interface IFabCatalogReadService
{
    /// <summary>搜索 Fab 资产</summary>
    Task<Result<PagedResult<FabAssetSummary>>> SearchAsync(
        FabSearchQuery query, CancellationToken ct);

    /// <summary>获取资产详情</summary>
    Task<Result<FabAssetDetail>> GetDetailAsync(string assetId, CancellationToken ct);

    /// <summary>获取已拥有的资产列表</summary>
    Task<Result<IReadOnlyList<FabAssetSummary>>> GetOwnedAssetsAsync(CancellationToken ct);

    /// <summary>获取资产分类列表</summary>
    Task<Result<IReadOnlyList<AssetCategoryInfo>>> GetCategoriesAsync(CancellationToken ct);
}

/// <summary>
/// Fab 资产操作服务。
/// </summary>
public interface IFabAssetCommandService
{
    /// <summary>发起资产下载（会调用 Downloads 模块）</summary>
    Task<Result<DownloadTaskId>> DownloadAssetAsync(string assetId, string installPath, CancellationToken ct);

    /// <summary>刷新本地资产缓存</summary>
    Task<Result> RefreshCacheAsync(CancellationToken ct);
}

public sealed class FabSearchQuery
{
    public string? Keyword { get; init; }
    public string? Category { get; init; }
    public string? EngineVersion { get; init; }       // 按引擎版本兼容性过滤
    public FabSortOrder SortOrder { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public enum FabSortOrder
{
    Relevance,
    Newest,
    PriceLowToHigh,
    PriceHighToLow,
    Rating
}

public sealed class FabAssetSummary
{
    public string AssetId { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string ThumbnailUrl { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string Author { get; init; } = default!;
    public decimal Price { get; init; }
    public double Rating { get; init; }
    public bool IsOwned { get; init; }
    public bool IsInstalled { get; init; }
    public IReadOnlyList<string> SupportedEngineVersions { get; init; } = [];
}

public sealed class FabAssetDetail
{
    public string AssetId { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Author { get; init; } = default!;
    public decimal Price { get; init; }
    public double Rating { get; init; }
    public int RatingCount { get; init; }
    public long DownloadSize { get; init; }
    public string LatestVersion { get; init; } = default!;
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<string> Screenshots { get; init; } = [];
    public IReadOnlyList<string> SupportedEngineVersions { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? TechnicalDetails { get; init; }
    public bool IsOwned { get; init; }
    public bool IsInstalled { get; init; }
}

public sealed class AssetCategoryInfo
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public int AssetCount { get; init; }
}
```

---

## 11. 文件系统服务

```csharp
namespace Launcher.Infrastructure.FileSystem;

/// <summary>
/// 文件系统抽象服务。UI 和业务层不直接操作磁盘。
/// </summary>
public interface IFileSystemService
{
    Task<bool> DirectoryExistsAsync(string path);
    Task EnsureDirectoryAsync(string path);
    Task<long> GetAvailableSpaceAsync(string drivePath);
    Task DeleteFileAsync(string path, CancellationToken ct);
    Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct);
    Task<long> GetDirectorySizeAsync(string path, CancellationToken ct);
    Task<IReadOnlyList<string>> ScanFilesAsync(string path, string pattern, CancellationToken ct);
}
```

---

## 12. 哈希服务

```csharp
namespace Launcher.Infrastructure.FileSystem;

/// <summary>
/// 文件哈希计算服务。
/// </summary>
public interface IHashingService
{
    /// <summary>计算文件 SHA-1 哈希</summary>
    Task<string> ComputeSha1Async(string filePath, CancellationToken ct);

    /// <summary>计算文件 SHA-256 哈希</summary>
    Task<string> ComputeSha256Async(string filePath, CancellationToken ct);

    /// <summary>计算流的哈希（用于分块校验）</summary>
    Task<string> ComputeSha1Async(Stream stream, CancellationToken ct);
}
```

---

## 13. 运行时下载状态存储

```csharp
namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 运行时下载状态存储。
/// 把"运行时下载进度"和"数据库持久化状态"分离。
/// UI 通过此接口获取实时下载进度快照。
/// </summary>
public interface IDownloadRuntimeStore
{
    /// <summary>当前所有下载快照</summary>
    IReadOnlyCollection<DownloadRuntimeSnapshot> Current { get; }

    /// <summary>更新或插入快照</summary>
    void Upsert(DownloadRuntimeSnapshot snapshot);

    /// <summary>移除快照</summary>
    void Remove(DownloadTaskId taskId);

    /// <summary>状态变更事件（UI 订阅用于刷新）</summary>
    event EventHandler<DownloadRuntimeSnapshot>? SnapshotChanged;
}

public sealed class DownloadRuntimeSnapshot
{
    public DownloadTaskId TaskId { get; init; }
    public string AssetId { get; init; } = default!;
    public DownloadUiState UiState { get; init; }
    public double Progress { get; init; }
    public long DownloadedBytes { get; init; }
    public long TotalBytes { get; init; }
    public long BytesPerSecond { get; init; }
    public DateTime UpdatedAt { get; init; }
}
```

---

## 14. 仓储接口

### 14.1 下载任务仓储

```csharp
namespace Launcher.Application.Modules.Downloads.Contracts;

/// <summary>
/// 下载任务持久化仓储。
/// </summary>
public interface IDownloadTaskRepository
{
    Task<DownloadTask?> GetByIdAsync(DownloadTaskId taskId, CancellationToken ct);
    Task<IReadOnlyList<DownloadTask>> GetPendingAsync(CancellationToken ct);
    Task SaveAsync(DownloadTask task, CancellationToken ct);
    Task SaveCheckpointAsync(DownloadCheckpoint checkpoint, CancellationToken ct);
    Task<DownloadCheckpoint?> GetCheckpointAsync(DownloadTaskId taskId, CancellationToken ct);
}
```

### 14.2 Fab 资产仓储

```csharp
namespace Launcher.Application.Modules.FabLibrary.Contracts;

/// <summary>
/// Fab 资产本地缓存仓储。
/// </summary>
public interface IFabAssetRepository
{
    Task<FabAssetSummary?> GetByIdAsync(string assetId, CancellationToken ct);
    Task<IReadOnlyList<FabAssetSummary>> GetOwnedAsync(CancellationToken ct);
    Task SaveAsync(FabAssetSummary asset, CancellationToken ct);
    Task SaveBatchAsync(IReadOnlyList<FabAssetSummary> assets, CancellationToken ct);
    Task<DateTime?> GetLastSyncTimeAsync(CancellationToken ct);
}
```

### 14.3 安装记录仓储

```csharp
namespace Launcher.Application.Modules.Installations.Contracts;

/// <summary>
/// 安装记录仓储。
/// </summary>
public interface IInstallationRepository
{
    Task<InstallStatusSummary?> GetByAssetIdAsync(string assetId, CancellationToken ct);
    Task<IReadOnlyList<InstallStatusSummary>> GetAllInstalledAsync(CancellationToken ct);
    Task SaveAsync(InstallStatusSummary record, CancellationToken ct);
    Task DeleteAsync(string assetId, CancellationToken ct);
}
```

---

## 15. 后台任务宿主

```csharp
namespace Launcher.Background.Hosting;

/// <summary>
/// 统一后台任务宿主。所有 Worker 通过此宿主注册和管理生命周期。
/// 禁止模块自行 Task.Run。
/// </summary>
public interface IBackgroundTaskHost
{
    /// <summary>注册一个后台 Worker</summary>
    void Register(IBackgroundWorker worker);

    /// <summary>启动所有已注册的 Worker</summary>
    Task StartAllAsync(CancellationToken ct);

    /// <summary>优雅停止所有 Worker</summary>
    Task StopAllAsync(CancellationToken ct);

    /// <summary>获取 Worker 运行状态</summary>
    IReadOnlyList<WorkerStatus> GetStatuses();
}

/// <summary>
/// 后台 Worker 抽象。
/// </summary>
public interface IBackgroundWorker
{
    string Name { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    WorkerState State { get; }
}

public enum WorkerState
{
    Idle,
    Running,
    Stopping,
    Stopped,
    Faulted
}

public sealed class WorkerStatus
{
    public string Name { get; init; } = default!;
    public WorkerState State { get; init; }
    public DateTime? LastRunAt { get; init; }
    public string? LastError { get; init; }
}
```

---

## 16. 配置接口

```csharp
namespace Launcher.Application.Modules.Settings.Contracts;

/// <summary>
/// 应用配置读取服务。
/// </summary>
public interface IAppConfigProvider
{
    DownloadConfig Download { get; }
    AppearanceConfig Appearance { get; }
    PathConfig Paths { get; }
    NetworkConfig Network { get; }
}

public sealed class DownloadConfig
{
    public int MaxConcurrentDownloads { get; set; } = 3;
    public int MaxRetryCount { get; set; } = 5;
    public int ChunkSizeMb { get; set; } = 10;
    public bool AutoInstallAfterDownload { get; set; } = true;
}

public sealed class AppearanceConfig
{
    public string Theme { get; set; } = "System";   // System / Light / Dark
    public string Language { get; set; } = "zh-CN";
}

public sealed class PathConfig
{
    public string DefaultInstallPath { get; set; } = @"C:\EpicAssets";
    public string CachePath { get; set; } = "";      // 默认 %LocalAppData%\HelsincyEpicLauncher\Cache
    public string LogPath { get; set; } = "";         // 默认 %LocalAppData%\HelsincyEpicLauncher\Logs
}

public sealed class NetworkConfig
{
    public string? ProxyUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public bool UseCdnFallback { get; set; } = true;
}
```
