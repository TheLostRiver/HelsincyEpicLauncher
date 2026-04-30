# Downloads Application Boundary

> AI 模型：GPT-5 Codex
> 创建日期：2026-04-30
> 依据：`docs/06-ModuleDefinitions/Downloads.md`、`docs/07-DownloadSubsystem.md`、当前 Downloads 代码结构

---

## 1. 目标边界

Downloads 模块的 Application 层应承载下载用例、命令、查询、模块内部端口和跨模块稳定契约。HTTP、SQLite、文件系统、Polly、具体调度实现和分块下载实现应留在 Infrastructure 或 Background。

当前代码已经有 Downloads Contracts 目录，但其中混放了两类接口：

1. 对外公共 Contracts：供 Presentation、FabLibrary、Installations、EngineVersions、Shell 等模块调用。
2. 模块内部端口：供 Downloads 用例编排依赖，由 Infrastructure 实现。

后续优化应先明确边界，再逐步迁移实现，不一次性重写。

---

## 2. 对外公共 Contracts

以下接口是跨模块和 UI 可见的公共入口：

| Contract | 当前职责 | 边界要求 |
|----------|----------|----------|
| `IDownloadCommandService` | Start/Pause/Resume/Cancel/SetPriority/PauseAll/ResumeAll | 命令入口，只接受请求 DTO 或稳定 ID，返回 `Result` 或任务 ID |
| `IDownloadReadService` | 查询单个资产下载状态、活跃下载、历史、活跃数量 | 查询入口，只返回 UI/跨模块稳定 Summary |

以下模型当前也属于公共可见模型：

| Model | 当前用途 | 边界要求 |
|-------|----------|----------|
| `StartDownloadRequest` | 创建下载任务请求 | 可以作为公共请求 DTO |
| `DownloadStatusSummary` | UI 和跨模块状态摘要 | 应保持为稳定投影，不返回领域实体 |
| `DownloadProgressSnapshot` | 运行时进度快照 | 应保持节流后的投影模型 |
| `DownloadCompletedEvent` | 完成事件 | 事件模型应只描述已发生事实 |
| `DownloadFailedEvent` | 失败事件 | 事件模型应只描述已发生事实 |
| `DownloadProgressChangedEvent` | 进度事件 | 不应替代查询接口，不应高频推送原始 chunk 进度 |

公共 Contracts 的长期目标：

- 不返回 `DownloadTask`。
- 不暴露内部 `DownloadState`。
- 不要求 Presentation 引用 `Launcher.Domain.*`。
- `DownloadTaskId`、`DownloadUiState` 当前来自 Domain，属于已知短期兼容债务；后续 Task 2.x 应引入 Contract-owned ID/UI 状态或稳定投影，逐步移除 Domain 泄漏。

---

## 3. 模块内部端口

以下接口当前位于 `Contracts` 目录，但从架构语义上应视作 Downloads 内部端口：

| Port | 当前实现 | 可否使用 Domain | 说明 |
|------|----------|----------------|------|
| `IDownloadTaskRepository` | `Infrastructure.Downloads.DownloadTaskRepository` | 可以 | 持久化端口，可返回 `DownloadTask`、`DownloadCheckpoint`、`DownloadState`、`DownloadTaskId` |
| `IDownloadScheduler` | `Infrastructure.Downloads.DownloadScheduler` | 可以短期使用 | 队列和并发控制端口，当前使用 `DownloadTaskId` 并通过 `TaskReady` 事件驱动执行 |
| `IDownloadRuntimeStore` | `Infrastructure.Downloads.DownloadRuntimeStore` | 可以短期使用 | 内存运行时状态端口，当前同时承载快照和完成/失败事件 |
| `IDownloadOrchestrator` | `Infrastructure.Downloads.DownloadOrchestrator` | 可以短期使用 | 当前事实上的用例编排入口，后续应迁移到 Application 用例或应用服务 |

建议的目录演进：

```text
src/Launcher.Application/Modules/Downloads/
  Contracts/   # 跨模块和 UI 可见接口、请求、Summary、Event
  Ports/       # Repository、Scheduler、RuntimeStore、Chunk downloader 等内部端口
  UseCases/    # Start/Pause/Resume/Cancel/Recover 等应用用例
```

---

## 4. Chunk 下载边界

当前分块下载由 `Infrastructure.Downloads.ChunkDownloadClient` 和 `ChunkDownloadModels` 承担，尚没有 Application 层端口。

后续建议新增内部端口：

```csharp
public interface IChunkDownloader
{
    Task<Result<ChunkDownloadResult>> DownloadChunkAsync(
        ChunkDownloadRequest request,
        IProgress<long>? progress,
        CancellationToken ct);
}
```

边界要求：

- `IChunkDownloader` 属于内部端口，不是公共 Contract。
- HTTP Range、CDN 回退、Polly 重试/断路器、临时文件写入都属于 Infrastructure 实现细节。
- Chunk 请求和结果可以是 Application 内部模型；不要让 Presentation 或跨模块调用方看到 chunk 级细节。

---

## 5. 当前 Scheduler 到 Worker 断点

Task 4.1 搜索结果显示，当前生产代码已经能把下载请求送入 `DownloadScheduler.QueueAsync`，但调度器事件还没有生产订阅者，因此尚未连接到真实下载执行器。

当前启动链路：

```text
FabAssetCommandService / EngineVersionCommandService
  -> IDownloadCommandService.StartAsync(StartDownloadRequest)
  -> Application DownloadCommandService.StartAsync
  -> StartDownloadUseCase.ExecuteAsync
  -> IDownloadOrchestrator.EnqueueAsync
  -> Infrastructure DownloadOrchestrator.EnqueueAsync
  -> IDownloadTaskRepository.InsertAsync
  -> IDownloadScheduler.QueueAsync
  -> Infrastructure DownloadScheduler.TryScheduleNextAsync
  -> TaskReady?.Invoke(taskId, cancellationToken)
```

当前事实：

- `TaskReady +=` 只出现在 `tests/Launcher.Tests.Unit/DownloadSchedulerTests.cs`，生产代码中没有订阅者。
- `DownloadScheduler.QueueAsync` 会把任务加入队列，并异步调用 `TryScheduleNextAsync`。
- `TryScheduleNextAsync` 会把任务移动到 `_activeTasks`，随后仅在 `TaskReady is not null` 时触发执行。
- 当 `TaskReady` 没有订阅者时，任务会占用活跃调度位，但不会启动 `ChunkDownloadClient`，也不会写入进度、完成或失败事件。
- `IDownloadScheduler.NotifyCompleted` 目前只有调度器测试调用，生产代码没有下载 Worker 调用它释放活跃位。
- `DownloadRuntimeStore.NotifyCompleted` / `NotifyFailed` 目前没有生产侧下载执行器调用，现有订阅者主要是 UI、Shell 和 `AutoInstallWorker`。
- `ChunkDownloadClient` 当前仅在 DI 中注册，没有被 Orchestrator、Scheduler 或 Worker 消费。

后续闭环方向：

1. 引入明确的下载执行端口或 Worker，例如 `IDownloadTaskExecutor` / `DownloadWorker`。
2. 由 Orchestrator 或专门后台 Worker 订阅 `IDownloadScheduler.TaskReady`。
3. 执行器按任务 ID 读取 `DownloadTask` 和 checkpoint，调用 chunk 下载实现。
4. 执行成功后同时更新 Repository、RuntimeStore，并调用 `IDownloadScheduler.NotifyCompleted` 释放调度位。
5. 执行失败或取消时更新 Repository、RuntimeStore，并释放或保留调度位，语义需在 Task 4.2/4.3 中测试固化。

---

## 6. Checkpoint 边界

设计文档中有独立的 `IDownloadCheckpointRepository`，当前实现将 checkpoint 操作合并在 `IDownloadTaskRepository` 中。

短期可以保留合并实现，条件是：

- 该接口仍被视为模块内部端口。
- 公共 Contracts 不暴露 checkpoint、chunk、manifest JSON。
- 后续当 checkpoint 逻辑继续变复杂时，再拆出 `IDownloadCheckpointRepository`。

---

## 7. 当前主要架构债务

| 债务 | 影响 | 后续任务 |
|------|------|----------|
| `DownloadCommandService` 在 Infrastructure | 用例入口和技术实现混在一起 | Task 1.2 / 1.3 |
| `DownloadReadService` 在 Infrastructure | 查询投影依赖 Repository 和 RuntimeStore 的编排落在 Infrastructure | 后续 Read UseCase 迁移 |
| `DownloadOrchestrator` 在 Infrastructure | 磁盘空间检查、重复任务校验、状态流转编排属于应用流程 | Task 1.x |
| Repository/Scheduler/RuntimeStore 位于 `Contracts` 目录 | 公共契约和内部端口命名混淆 | Task 2.1 |
| 公共 DTO 使用 `DownloadTaskId`、`DownloadUiState` Domain 类型 | Presentation 和跨模块调用方被 Domain 牵连 | Task 2.2 / 2.3 |
| Chunk 下载无 Application 端口 | 用例无法依赖抽象 chunk 下载能力 | Downloads 管线闭环阶段 |
| `DownloadScheduler.TaskReady` 无生产订阅者 | 任务能入队但不会被真实下载执行器消费，调度位也不会释放 | Task 4.2 / 4.3 |
| `DownloadScheduler` 当前默认并发硬编码为 3 | 数据驱动不足 | Options 数据驱动阶段 |

---

## 8. 迁移顺序建议

1. 新增小的 Application 用例壳，例如 `StartDownloadUseCase`，先只做请求校验和委托。
2. 将命令服务从 Infrastructure 迁到 Application，Infrastructure 保留技术端口实现。
3. 将 Repository、Scheduler、RuntimeStore 语义上归类为 `Ports`，先文档化，再移动目录。
4. 为公共 Contracts 引入不依赖 Domain 的 Summary/ID/UI 状态投影，保留兼容层过渡。
5. 再处理 chunk downloader、checkpoint repository 和下载管线闭环。

---

## 9. 验收约束

- 新增公共 Downloads Contract 时，不能返回 `DownloadTask` 或 `DownloadState`。
- 新增内部端口时，必须明确由 Application 定义、Infrastructure 实现。
- Presentation 不应新增 `Launcher.Domain.Downloads` 引用。
- Downloads 用例测试应优先验证应用编排，而不是 HTTP、SQLite 或文件系统细节。
