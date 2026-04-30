# Downloads 模块

---

## 架构定义

### 职责

- 下载队列管理（入队、优先级调整、移除）
- 分块并行下载（chunked download）
- 断点续传（checkpoint 持久化）
- 下载进度聚合与节流推送
- CDN 节点切换 / 回退
- 下载速度计算与预估剩余时间
- 失败重试（指数退避）
- 崩溃恢复（从 checkpoint 恢复中断任务）
- 对外提供收敛后的 UI 状态投影

### 不负责

- 文件安装/解压（由 Installations 模块处理）
- 文件完整性校验（由 Installations 的 Verifier 处理）
- UI 渲染（由 Presentation 处理）
- 网络认证（通过 Auth.Contracts 获取 Token）

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| `Auth.Contracts` | 获取 Download Token |
| `IDownloadOptionsProvider` | 读取任务并发、chunk 并发、chunk size、重试次数、checkpoint 周期 |
| `Launcher.Shared` | Result 模型 |

### 谁可以依赖 Downloads

| 模块 | 依赖的 Contracts |
|------|----------------|
| FabLibrary | `IDownloadReadService`, `IDownloadCommandService` |
| Installations | `IDownloadReadService` |
| EngineVersions | `IDownloadCommandService`, `IDownloadReadService` |
| Shell | `IDownloadReadService`（活跃下载数） |

---

## 内部架构

> 详见 [07-DownloadSubsystem.md](../07-DownloadSubsystem.md) 获取完整管线设计和时序图。

### 内部组件

```
[IDownloadOrchestrator]     ← 编排入口
       ↓
[IDownloadScheduler]        ← 队列 + 并发控制
       ↓
[IDownloadTaskExecutor]
       ↓
[DownloadWorker]            ← 单任务执行（当前 Infrastructure 实现）
       ↓
[IChunkDownloader]          ← 分块 HTTP 下载
       ↓
[IDownloadCheckpointRepo]   ← 断点持久化
       ↓
[IDownloadRuntimeStore]     ← 运行时状态（→ UI）
```

### 状态机

**内部细粒度状态**（`DownloadState`，仅模块内部使用）：

```
Queued
Preparing
FetchingManifest
AllocatingDisk
DownloadingChunks
RetryingChunk
PausingChunks
Paused
VerifyingDownload
Finalizing
Completed
Failed
Cancelled
```

**对外 UI 状态**（`DownloadStatusKind`，由 Application Contracts 拥有，对其他模块和 UI 暴露）：

```
Queued
Downloading
Paused
Verifying
Installing
Completed
Failed
Cancelled
```

> 映射规则：`Preparing/FetchingManifest/AllocatingDisk/DownloadingChunks/RetryingChunk` → `Downloading`  
> 其他一一对应。

当前 DTO 中仍保留 `DownloadUiState` 和 `DownloadTaskId` 作为兼容字段；Presentation 和新增跨模块消费应优先使用 `DownloadStatusKind` 与 `DownloadTaskKey`。

---

## API 定义

> 详见 [05-CoreInterfaces.md](../05-CoreInterfaces.md) 第 5~6 节

### 对外事件

```csharp
// 下载完成（Installations/FabLibrary 会订阅）
public sealed record DownloadCompletedEvent(
    DownloadTaskId TaskId,
    string AssetId,
    string DownloadedFilePath);

// 下载失败
public sealed record DownloadFailedEvent(
    DownloadTaskId TaskId,
    string AssetId,
    string ErrorMessage,
    bool CanRetry);

// 下载进度变化（节流后推送，不每个 chunk 都发）
public sealed record DownloadProgressChangedEvent(
    DownloadTaskId TaskId,
    double Progress,
    long BytesPerSecond);
```

---

## 关键流程

### 开始下载

```
1. FabLibrary 调用 IDownloadCommandService.StartAsync(request)
2. Application DownloadCommandService：
   a. 调用 StartDownloadUseCase 做请求前置校验
   b. 委托 IDownloadOrchestrator.EnqueueAsync()
3. Infrastructure DownloadOrchestrator：
   a. 验证路径、磁盘空间和重复活跃任务
   b. 创建 DownloadTask 领域实体
   c. 持久化到 IDownloadTaskRepository
   d. 推入 IDownloadScheduler 队列
   e. 返回 DownloadTaskId
4. DownloadScheduler：
   a. 检查当前并发数
   b. 如果有空位 → 立即调度
   c. 如果已满 → 等待，按优先级排队
5. DownloadOrchestrator 已订阅 TaskReady：
   a. 将任务 ID 委托给 IDownloadTaskExecutor.ExecuteAsync()
6. DownloadWorker 被调度执行：
   a. 获取 manifest（文件列表、chunk 信息）
   b. 检查已有 checkpoint（断点恢复）
   c. 并行下载各 chunk（受限于 chunk 并发数）
   d. 每个 chunk 完成 → 更新 checkpoint
   e. 定期聚合进度 → 更新 IDownloadRuntimeStore
7. 全部 chunk 完成：
   a. 状态转为 Finalizing
   b. 发布 DownloadCompletedEvent
   c. 状态转为 Completed
   d. 调用 IDownloadScheduler.NotifyCompleted() 释放调度位
```

### 暂停/恢复

```
暂停：
1. IDownloadCommandService.PauseAsync(taskId)
2. DownloadOrchestrator → DownloadWorker 发送 CancellationToken
3. Worker 保存当前 checkpoint
4. 状态转为 Paused
5. IDownloadRuntimeStore 更新

恢复：
1. IDownloadCommandService.ResumeAsync(taskId)
2. 从 checkpoint 读取断点位置
3. 重新推入 Scheduler 队列
4. Worker 从断点位置继续下载
```

### 崩溃恢复

```
1. App 启动 Phase 2
2. IDownloadOrchestrator.RecoverAsync()
3. 从 IDownloadTaskRepository 加载所有 State != Completed/Cancelled 的任务
4. 对每个任务：
   a. 加载 checkpoint
   b. 状态重置为 Queued
   c. 推入 Scheduler 队列
5. Scheduler 按优先级重新调度
```

---

## 当前已落地的架构现实

- `DownloadCommandService` 已位于 Application 层，并委托 `StartDownloadUseCase` 与 `IDownloadOrchestrator`。
- `StartDownloadUseCase` 当前只做开始下载请求的参数前置校验，不触碰 HTTP、SQLite 或文件系统。
- `DownloadOrchestrator`、`DownloadScheduler`、`DownloadRuntimeStore`、`DownloadTaskRepository` 和 `DownloadWorker` 当前仍在 Infrastructure。
- `DownloadOrchestrator` 已在构造时订阅 `IDownloadScheduler.TaskReady`，并委托 `IDownloadTaskExecutor.ExecuteAsync()`；调度器到执行器的生产断点已闭合。
- `IChunkDownloader` 由 `ChunkDownloadClient` 实现；它仍属于下载内部执行能力，不是 UI/跨模块公共 Contract。
- 下载配置已通过 `DownloadOptions` / `IDownloadOptionsProvider` 数据驱动：任务并发、chunk 并发、chunk size、重试次数和 checkpoint 周期均来自配置或默认值。
- 目录命名仍存在迁移期债务：Repository、Scheduler、RuntimeStore、Executor 等内部端口还位于 `Contracts` 目录中，但语义上应视作内部端口。
