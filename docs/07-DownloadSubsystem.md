# 下载子系统

> 下载子系统是整个启动器最复杂的部分。本文档详细说明其管线架构、调度策略、状态机、断点续传、崩溃恢复和时序流程。

---

## 1. 子系统概览

下载子系统不是一个 Service，而是一套由多个组件协作构成的子系统：

```
┌───────────────────────────────────────────────────────┐
│                   入口层                               │
│  IDownloadCommandService → StartDownloadHandler       │
└──────────────────┬────────────────────────────────────┘
                   ▼
┌───────────────────────────────────────────────────────┐
│                   编排层                               │
│  IDownloadOrchestrator                                 │
│  ├─ 构建下载任务                                       │
│  ├─ 获取 Manifest                                     │
│  ├─ 构建 Chunk 计划                                   │
│  └─ 推入调度器                                         │
└──────────────────┬────────────────────────────────────┘
                   ▼
┌───────────────────────────────────────────────────────┐
│                   调度层                               │
│  IDownloadScheduler                                    │
│  ├─ 优先级队列                                         │
│  ├─ 并发数控制                                         │
│  └─ 任务分配                                           │
└──────────────────┬────────────────────────────────────┘
                   ▼
┌───────────────────────────────────────────────────────┐
│                   执行层                               │
│  DownloadWorker                                        │
│  ├─ 分块并行下载                                       │
│  ├─ 进度收集                                           │
│  └─ 重试控制                                           │
│                                                        │
│  IChunkDownloader                                      │
│  ├─ 单 Chunk HTTP Range 请求                           │
│  └─ CDN 节点回退                                       │
└──────────────────┬────────────────────────────────────┘
                   ▼
┌───────────────────────────────────────────────────────┐
│                   持久化层                              │
│  IDownloadTaskRepository     → 任务元数据               │
│  IDownloadCheckpointRepo     → 断点续传数据             │
│  IDownloadRuntimeStore       → 运行时进度（内存）       │
└───────────────────────────────────────────────────────┘
```

---

## 2. 组件职责

| 组件 | 职责 | 生命周期 |
|------|------|---------|
| `IDownloadCommandService` | 对外命令入口（Start/Pause/Resume/Cancel） | Singleton |
| `IDownloadReadService` | 对外状态查询 | Singleton |
| `IDownloadOrchestrator` | 下载生命周期编排 | Singleton |
| `IDownloadScheduler` | 队列管理 + 并发限流 | Singleton |
| `DownloadWorker` | 单任务执行器（一个任务一个 Worker 实例） | Transient per task |
| `IChunkDownloader` | HTTP 分块下载 | Singleton |
| `IDownloadTaskRepository` | 任务持久化（SQLite） | Singleton |
| `IDownloadCheckpointRepository` | 断点数据持久化（SQLite） | Singleton |
| `IDownloadRuntimeStore` | 运行时进度快照（内存） | Singleton |

---

## 3. 状态机详细定义

### 3.1 内部状态枚举

```csharp
/// <summary>
/// 下载任务内部状态。仅模块内部使用，不对外暴露。
/// </summary>
internal enum DownloadState
{
    Queued,              // 已入队，等待调度
    Preparing,           // 准备中（分配资源）
    FetchingManifest,    // 获取文件清单
    AllocatingDisk,      // 预分配磁盘空间
    DownloadingChunks,   // 正在下载分块
    RetryingChunk,       // 某个分块失败，重试中
    PausingChunks,       // 正在暂停（等待活跃 chunk 完成保存）
    Paused,              // 已暂停
    VerifyingDownload,   // 下载完成后校验
    Finalizing,          // 最终处理（合并、清理临时文件）
    Completed,           // 完成
    Failed,              // 失败
    Cancelled            // 已取消
}
```

### 3.2 状态转换规则

```
Queued → Preparing → FetchingManifest → AllocatingDisk → DownloadingChunks

DownloadingChunks → RetryingChunk → DownloadingChunks（重试成功）
DownloadingChunks → RetryingChunk → Failed（重试耗尽）

DownloadingChunks → PausingChunks → Paused
Paused → Queued（恢复时重新排队）

DownloadingChunks → VerifyingDownload → Finalizing → Completed
VerifyingDownload → Failed（校验失败）

任何活跃状态 → Cancelled
任何活跃状态 → Failed（不可恢复错误）
Failed → Queued（用户重试）
```

### 3.3 状态机实现

```csharp
internal sealed class DownloadStateMachine
{
    private static readonly Dictionary<DownloadState, HashSet<DownloadState>> _transitions = new()
    {
        [DownloadState.Queued]             = [DownloadState.Preparing, DownloadState.Cancelled],
        [DownloadState.Preparing]          = [DownloadState.FetchingManifest, DownloadState.Failed, DownloadState.Cancelled],
        [DownloadState.FetchingManifest]   = [DownloadState.AllocatingDisk, DownloadState.Failed, DownloadState.Cancelled],
        [DownloadState.AllocatingDisk]     = [DownloadState.DownloadingChunks, DownloadState.Failed, DownloadState.Cancelled],
        [DownloadState.DownloadingChunks]  = [DownloadState.RetryingChunk, DownloadState.PausingChunks, DownloadState.VerifyingDownload, DownloadState.Failed, DownloadState.Cancelled],
        [DownloadState.RetryingChunk]      = [DownloadState.DownloadingChunks, DownloadState.Failed, DownloadState.Cancelled],
        [DownloadState.PausingChunks]      = [DownloadState.Paused],
        [DownloadState.Paused]             = [DownloadState.Queued, DownloadState.Cancelled],
        [DownloadState.VerifyingDownload]  = [DownloadState.Finalizing, DownloadState.Failed],
        [DownloadState.Finalizing]         = [DownloadState.Completed, DownloadState.Failed],
        [DownloadState.Failed]             = [DownloadState.Queued],  // 重试
        // Completed 和 Cancelled 是终态
    };

    public DownloadState Current { get; private set; } = DownloadState.Queued;

    public Result TransitionTo(DownloadState target)
    {
        if (!_transitions.TryGetValue(Current, out var allowed) || !allowed.Contains(target))
            return Result.Fail($"非法状态转换: {Current} → {target}");

        Current = target;
        return Result.Ok();
    }
}
```

### 3.4 内部状态 → UI 状态映射

```csharp
internal static DownloadUiState MapToUiState(DownloadState internalState) => internalState switch
{
    DownloadState.Queued             => DownloadUiState.Queued,
    DownloadState.Preparing          => DownloadUiState.Downloading,
    DownloadState.FetchingManifest   => DownloadUiState.Downloading,
    DownloadState.AllocatingDisk     => DownloadUiState.Downloading,
    DownloadState.DownloadingChunks  => DownloadUiState.Downloading,
    DownloadState.RetryingChunk      => DownloadUiState.Downloading,
    DownloadState.PausingChunks      => DownloadUiState.Paused,       // 暂停过渡态映射为 Paused
    DownloadState.Paused             => DownloadUiState.Paused,
    DownloadState.VerifyingDownload  => DownloadUiState.Verifying,
    DownloadState.Finalizing         => DownloadUiState.Downloading,  // 最终处理仍显示下载中
    DownloadState.Completed          => DownloadUiState.Completed,
    DownloadState.Failed             => DownloadUiState.Failed,
    DownloadState.Cancelled          => DownloadUiState.Cancelled,
    _ => DownloadUiState.Failed
};
```

---

## 4. 断点续传设计

### 4.1 Checkpoint 数据结构

```csharp
/// <summary>
/// 下载断点数据。崩溃/暂停后可从此处恢复。
/// </summary>
public sealed class DownloadCheckpoint
{
    public DownloadTaskId TaskId { get; init; }
    public string ManifestJson { get; init; } = default!;     // 缓存的 Manifest（避免重新拉取）
    public IReadOnlyList<ChunkCheckpoint> Chunks { get; init; } = [];
    public DateTime SavedAt { get; init; }
}

public sealed class ChunkCheckpoint
{
    public int ChunkIndex { get; init; }
    public long RangeStart { get; init; }
    public long RangeEnd { get; init; }
    public long DownloadedBytes { get; init; }     // 已下载字节数
    public bool IsCompleted { get; init; }
    public string? PartialFilePath { get; init; }  // 临时文件路径
    public string? Hash { get; init; }             // 已完成 chunk 的哈希
}
```

### 4.2 Checkpoint 保存策略

- 每完成一个 Chunk → 立即保存 checkpoint
- 暂停时 → 保存当前所有 chunk 进度
- 定时保存 → 每 30 秒自动持久化一次（防止崩溃丢失过多进度）
- Checkpoint 保存到 SQLite（和下载任务同一数据库）

### 4.3 恢复流程

```
1. 加载 DownloadCheckpoint
2. 遍历 ChunkCheckpoint：
   a. IsCompleted == true → 跳过
   b. IsCompleted == false && DownloadedBytes > 0 → 从 DownloadedBytes 位置续传
   c. IsCompleted == false && DownloadedBytes == 0 → 重新下载此 chunk
3. 恢复的 Manifest 使用缓存版本（避免重新网络请求）
4. 推入 Scheduler 按正常流程执行
```

---

## 5. 分块下载策略

### 5.1 Chunk 划分

```
默认 Chunk 大小：10 MB（可配置）
文件 100 MB → 10 个 Chunk
文件 1 GB → 100 个 Chunk
每个 Chunk 独立 HTTP Range 请求
```

### 5.2 并发控制

```
任务级并发：默认最多 3 个任务同时下载（可配置）
Chunk 级并发：每个任务内部最多 4 个 Chunk 同时下载
总 HTTP 连接数 = 任务并发数 × Chunk 并发数 = 12
```

### 5.3 重试策略

```csharp
// 单 Chunk 失败重试
// 策略：指数退避 + 抖动
// 最大重试次数：5（可配置）
// 退避间隔：1s → 2s → 4s → 8s → 16s（加随机抖动 ±20%）
// 如果 CDN 节点失败 → 切换备用 CDN → 重试
// 全部重试耗尽 → 标记 Chunk 失败 → 任务状态转为 Failed
```

---

## 6. CDN 回退机制

```
1. 首选 CDN 节点 URL
2. 失败 → 切换到备用节点列表中的下一个
3. 所有节点都失败 → 等待后重试首选节点
4. CDN 节点列表从 Manifest 中获取
5. 节点选择策略可以按延迟/成功率动态调整（后期优化）
```

---

## 7. 进度聚合与推送

### 7.1 防止高频刷新打爆 UI

```
问题：每个 Chunk 每写入一次就更新进度 → 1 秒内可能有几百次更新 → UI 卡死

解决方案：
1. DownloadWorker 内部维护进度累加器
2. 每 500ms 聚合一次（可配置）
3. 聚合后的快照写入 IDownloadRuntimeStore
4. IDownloadRuntimeStore 触发 SnapshotChanged 事件
5. ViewModel 订阅事件，更新 UI 绑定属性
6. UI 绑定更新天然受 WinUI 渲染帧率限制
```

### 7.2 速度计算

```csharp
// 使用滑动窗口平均（最近 5 秒的传输量）
// BytesPerSecond = recentBytesInWindow / windowDuration
// EstimatedRemaining = (TotalBytes - DownloadedBytes) / BytesPerSecond
```

---

## 8. 时序图

### 8.1 完整下载流程

```
FabLibrary        DownloadCmd      Orchestrator     Scheduler      Worker          ChunkDownloader    RuntimeStore
    │                  │                │               │              │                  │               │
    │─StartAsync──────▶│                │               │              │                  │               │
    │                  │─Enqueue──────▶│                │              │                  │               │
    │                  │               │─FetchManifest─▶│              │                  │               │
    │                  │               │◀──manifest─────│              │                  │               │
    │                  │               │─BuildChunks───▶│              │                  │               │
    │                  │               │─Queue─────────▶│              │                  │               │
    │                  │◀──TaskId───────│               │              │                  │               │
    │◀──TaskId─────────│               │               │              │                  │               │
    │                  │               │               │─Dispatch────▶│                  │               │
    │                  │               │               │              │─DownloadChunk───▶│               │
    │                  │               │               │              │◀──bytes──────────│               │
    │                  │               │               │              │─Upsert──────────────────────────▶│
    │                  │               │               │              │  (每500ms聚合)                    │
    │                  │               │               │              │                  │               │
    │                  │               │               │              │─SaveCheckpoint──▶│(SQLite)       │
    │                  │               │               │              │                  │               │
    │                  │               │               │◀──Completed──│                  │               │
    │                  │               │◀──Completed────│              │                  │               │
    │                  │               │                               │                  │               │
    │                  │              [发布 DownloadCompletedEvent]     │                  │               │
```

### 8.2 暂停恢复流程

```
User          DownloadCmd      Orchestrator     Worker          Checkpoint
  │                │                │               │              │
  │─PauseAsync────▶│                │               │              │
  │                │─Pause─────────▶│               │              │
  │                │                │─Cancel Token──▶│              │
  │                │                │               │─SaveAll──────▶│
  │                │                │◀──Paused──────│              │
  │◀──Ok───────────│                │               │              │
  │                │                │               │              │
  │─ResumeAsync───▶│                │               │              │
  │                │─Resume────────▶│               │              │
  │                │                │─LoadCheckpoint──────────────▶│
  │                │                │◀──checkpoint──────────────────│
  │                │                │─Queue to Scheduler           │
  │◀──Ok───────────│                │               │              │
```

---

## 9. 网络韧性设计

### 9.1 网络断联处理

```
1. ChunkDownloader 检测到网络异常
2. 标记当前 chunk 为"中断"
3. 启动网络监视器（轮询检测或 NetworkChange 事件）
4. 网络恢复后：
   a. 自动重试中断的 chunk
   b. 通知 UI 网络已恢复
5. 长时间断联（超过配置的超时时间）→ 任务暂停，保存 checkpoint
```

### 9.2 API 限流处理

```
1. 收到 HTTP 429 Too Many Requests
2. 读取 Retry-After 头
3. 暂停该 CDN 节点的请求
4. 等待 Retry-After 后重试
5. 如果持续限流 → 切换 CDN 节点
```

---

## 10. 磁盘空间管理

```
1. 下载前检查：
   a. 目标路径可用空间 > 文件大小 × 1.2（预留 20% 余量）
   b. 不满足 → 返回 Error，UI 提示用户选择其他路径或清理磁盘
2. 下载中监控：
   a. 定期检查磁盘剩余空间
   b. 低于阈值 → 暂停所有下载任务，通知用户
3. 临时文件清理：
   a. 下载完成后删除 chunk 临时文件
   b. 取消下载时删除所有临时文件
   c. 启动时清理孤立的临时文件（上次崩溃遗留）
```
