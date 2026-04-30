# SessionContextRecord

> AI 模型：GPT-5 Codex
> 用途：记录架构优化任务的关键上下文，防止会话压缩或上下文爆满后丢失状态。
> 铁律：上下文压缩后，必须先读取本文件，再继续执行任何任务。

---

## 1. 恢复协议

新会话或压缩恢复后，严格按以下顺序执行：

1. 读取本文件。
2. 读取 `docs/17-ArchitectureOptimizationPlan.md`。
3. 读取 `docs/18-ArchitectureOptimizationImplementation.md`。
4. 检查 `git status --short`。
5. 只继续“当前任务”中记录的任务。
6. 若当前任务为空，等待用户指定，或从实现文档的下一个未完成任务开始前先确认。

---

## 2. 固定约束

- 本项目是 WinUI 制作的 Epic 启动器 Win10/Win11 版。
- 本轮架构优化以此前分析结果为基准。
- 不包含游戏商店和游戏库存模块。
- `docs/review/` 目录不是本次架构方案依据，除非用户明确要求读取。
- 任何代码实现前都必须先读对应模块文档。
- 不删除任何文件，除非用户明确要求。
- 不做一次性大重构。
- 每个原子任务必须小、可验证、可恢复。
- AI 无法读取精确额度，因此以保守上下文风险信号代替额度检测。
- 上下文将要爆满或判断可能接近限额时，必须先更新本文件，然后停止执行。
- 架构优化实现任务在隔离 worktree 执行，不触碰 `Q:\MyEpicLauncher` 主工作区中的既有未提交改动。

---

## 3. 当前基线摘要

### 3.1 已完成的本轮工作

- 已完成项目整体架构分析。
- 已创建架构优化方案：`docs/17-ArchitectureOptimizationPlan.md`。
- 已创建架构优化实现拆解：`docs/18-ArchitectureOptimizationImplementation.md`。
- 已创建本上下文记录文件：`docs/SessionContextRecord.md`。

### 3.2 关键分析结论

- 文档层面的架构目标成熟：分层架构 + 模块化纵向切片。
- 当前最大实现偏差是 `Launcher.Application` 过薄，`Launcher.Infrastructure` 过重。
- Background 层没有统一后台任务宿主，App 直接启动多个 Worker。
- Contracts 中存在 Domain 类型泄漏，Presentation 也有直接引用 Domain 的情况。
- Downloads 调度和真实执行链需要进一步确认并闭环。
- 硬编码端点、缓存时间、OAuth 默认值、WebView probe 参数需要逐步 Options 化。
- 大类风险集中在 `EpicOwnedFabCatalogClient`、`FabLibraryViewModel`、`DialogService`。

---

## 4. 当前任务状态

| 字段 | 内容 |
|------|------|
| 当前执行者 | GPT-5 Codex |
| 执行 worktree | `C:\tmp\superpowers\worktrees\MyEpicLauncher\architecture-optimization-implementation` |
| 执行分支 | `codex/architecture-optimization-implementation` |
| 当前基线提交 | `9e16267`（Task 5.2 代码提交） |
| 当前阶段 | Phase 5：Options 数据驱动 |
| 当前任务 | Task 5.2：新增 API Options |
| 当前状态 | 已完成代码实现、验证和代码提交；等待提交本上下文记录 |
| 下一步 | 提交本上下文记录后，若用户继续，从 Task 5.3：处理 OAuth 配置安全语义开始；先读取 `EpicOAuthOptions.cs`、`Auth.md`、`appsettings.json` |
| 阻塞项 | 无 |

---

## 5. 最近触碰文件

| 文件 | 操作 | 说明 |
|------|------|------|
| `docs/17-ArchitectureOptimizationPlan.md` | 新增 | 项目架构优化总方案 |
| `docs/18-ArchitectureOptimizationImplementation.md` | 新增 | 原子任务实现拆解 |
| `docs/SessionContextRecord.md` | 新增 | 上下文恢复记录 |
| `docs/17-ArchitectureOptimizationPlan.md` | 修改 | 补充额度风险触发时先记录并停止 |
| `docs/18-ArchitectureOptimizationImplementation.md` | 修改 | 新增上下文风险信号和触发后动作 |
| `docs/SessionContextRecord.md` | 修改 | 固化额度风险替代检测约束并记录当前任务 |
| `docs/SessionContextRecord.md` | 修改 | Task 0.1：记录执行者、worktree、分支、基线提交和当前任务 |
| `docs/SessionContextRecord.md` | 修改 | Task 0.2：标记真实基线同步任务开始 |
| `README.md` | 修改 | 将运行时描述同步为当前 .NET 9 Windows TFM，并修正解决方案文件名和构建命令 |
| `docs/01-ProjectOverview.md` | 修改 | 将最低系统要求和语言基线同步为当前 .NET 9 Windows TFM |
| `docs/11-TechStack.md` | 修改 | 将运行时基线同步为 `net9.0-windows10.0.19041.0`，将自包含部署改为发布目标建议 |
| `docs/SessionContextRecord.md` | 修改 | Task 0.3：标记项目引用方向测试任务开始 |
| `tests/Launcher.Tests.Unit/Architecture/ProjectReferenceRulesTests.cs` | 新增 | Task 0.3：新增项目引用方向架构测试 |
| `docs/SessionContextRecord.md` | 修改 | Task 0.4：标记禁用 namespace 扫描测试任务开始 |
| `tests/Launcher.Tests.Unit/Architecture/ForbiddenNamespaceReferenceTests.cs` | 新增 | Task 0.4：扫描 Presentation 中禁用的 `Launcher.Domain` 引用 |
| `docs/SessionContextRecord.md` | 修改 | Task 1.1：标记 Downloads 应用层端口梳理任务开始 |
| `src/Launcher.Application/Modules/Downloads/README_ARCH.md` | 新增 | Task 1.1：记录 Downloads 公共 Contracts、内部端口、Domain 泄漏和迁移顺序 |
| `docs/SessionContextRecord.md` | 修改 | Task 1.2：标记 StartDownloadUseCase 用例壳任务开始 |
| `tests/Launcher.Tests.Unit/Downloads/StartDownloadUseCaseTests.cs` | 新增 | Task 1.2：验证合法请求委托 orchestrator，非法请求不委托 |
| `src/Launcher.Application/Modules/Downloads/UseCases/StartDownloadUseCase.cs` | 新增 | Task 1.2：新增开始下载用例壳，做前置校验并委托 orchestrator |
| `docs/SessionContextRecord.md` | 修改 | Task 1.3：标记 DownloadCommandService 迁移任务开始 |
| `tests/Launcher.Tests.Unit/DownloadCommandServiceTests.cs` | 新增 | Task 1.3：验证 Application 命令服务委托 StartDownloadUseCase 并批量暂停/恢复 |
| `src/Launcher.Application/Modules/Downloads/DownloadCommandService.cs` | 新增 | Task 1.3：将命令入口迁到 Application 层 |
| `src/Launcher.Infrastructure/DependencyInjection.cs` | 修改 | Task 1.3：DI 注册切换到 Application 层 `DownloadCommandService` 并注册 `StartDownloadUseCase` |
| `src/Launcher.Infrastructure/Downloads/DownloadCommandService.cs` | 修改 | Task 1.3：保留旧实现并标记为迁移兼容参考 |
| `docs/SessionContextRecord.md` | 修改 | Task 1.4：标记 Installations 编排迁移预备任务开始 |
| `src/Launcher.Application/Modules/Installations/README_ARCH.md` | 新增 | Task 1.4：记录 Installations 编排迁移边界、Infrastructure 保留项和后续顺序 |
| `docs/SessionContextRecord.md` | 修改 | Task 2.1：标记公共 Contracts 与内部端口命名规则任务开始 |
| `docs/04-ModuleDependencyRules.md` | 修改 | Task 2.1：新增 Contracts、Ports、Persistence、UseCases 命名规则 |
| `docs/05-CoreInterfaces.md` | 修改 | Task 2.1：修正“所有接口位于 Contracts”的旧表述，增加公共接口和内部端口区分 |
| `docs/SessionContextRecord.md` | 修改 | Task 2.2：标记 Downloads Contract-owned UI 类型任务开始 |
| `tests/Launcher.Tests.Unit/DownloadModelsTests.cs` | 新增 | Task 2.2：验证 `DownloadStatusSummary` 暴露 Contract-owned 状态、可序列化且为 init-only 投影 |
| `src/Launcher.Application/Modules/Downloads/Contracts/DownloadModels.cs` | 修改 | Task 2.2：新增 `DownloadStatusKind` 和 `DownloadStatusSummary.Status` 兼容字段 |
| `src/Launcher.Infrastructure/Downloads/DownloadReadService.cs` | 修改 | Task 2.2：将旧 `DownloadUiState` 映射到新的 Contract-owned `DownloadStatusKind` |
| `docs/SessionContextRecord.md` | 修改 | Task 2.3：标记 Downloads UI 去 Domain 引用任务开始 |
| `tests/Launcher.Tests.Unit/Architecture/ForbiddenNamespaceReferenceTests.cs` | 修改 | Task 2.3：先移除 Downloads 例外，触发红灯以暴露待迁移 UI 文件 |
| `tests/Launcher.Tests.Unit/DownloadModelsTests.cs` | 修改 | Task 2.3：新增 `DownloadTaskKey` 和快照公共状态红灯测试 |
| `src/Launcher.Application/Modules/Downloads/Contracts/DownloadModels.cs` | 修改 | Task 2.3：新增 `DownloadTaskKey`，并为 Summary、Snapshot、事件提供公共任务标识投影 |
| `src/Launcher.Presentation/Modules/Downloads/DownloadsViewModel.cs` | 修改 | Task 2.3：ViewModel 改用 `DownloadTaskKey` 和 `DownloadStatusKind`，移除 Domain using |
| `src/Launcher.Presentation/Modules/Downloads/DownloadsPage.xaml.cs` | 修改 | Task 2.3：按钮 Tag 类型改为 `DownloadTaskKey`，移除 Domain using |
| `src/Launcher.Presentation/Modules/Downloads/DownloadsPage.xaml` | 修改 | Task 2.3：按钮 Tag 绑定由 `TaskId` 改为 `TaskKey` |
| `docs/SessionContextRecord.md` | 修改 | Task 2.4：标记 Installations UI 去 Domain 引用任务开始 |
| `tests/Launcher.Tests.Unit/Architecture/ForbiddenNamespaceReferenceTests.cs` | 修改 | Task 2.4：移除 Installations 例外，触发 Presentation 禁用 Domain 引用红灯 |
| `tests/Launcher.Tests.Unit/InstallationTests.cs` | 修改 | Task 2.4：新增 `InstallStatusSummary.Status` 公共状态红灯测试 |
| `src/Launcher.Application/Modules/Installations/Contracts/InstallModels.cs` | 修改 | Task 2.4：新增 `InstallStatusKind` 和 `InstallStatusSummary.Status` 兼容字段 |
| `src/Launcher.Infrastructure/Installations/InstallReadService.cs` | 修改 | Task 2.4：将旧 `InstallState` 映射到新的 Contract-owned `InstallStatusKind` |
| `src/Launcher.Presentation/Modules/Installations/InstallationsViewModel.cs` | 修改 | Task 2.4：ViewModel 改用 `InstallStatusKind`，移除 Domain using |
| `docs/SessionContextRecord.md` | 修改 | Task 3.1：标记后台 Worker 抽象任务开始 |
| `tests/Launcher.Tests.Unit/BackgroundWorkerContractTests.cs` | 新增 | Task 3.1：新增后台 Worker 抽象红灯测试 |
| `src/Launcher.Background/Hosting/WorkerStatus.cs` | 新增 | Task 3.1：后台 Worker 生命周期状态枚举 |
| `src/Launcher.Background/Hosting/IBackgroundWorker.cs` | 新增 | Task 3.1：后台 Worker 统一生命周期契约 |
| `docs/SessionContextRecord.md` | 修改 | Task 3.2：标记 BackgroundTaskHost 任务开始 |
| `tests/Launcher.Tests.Unit/BackgroundTaskHostTests.cs` | 新增 | Task 3.2：新增 Host 启动、停止、失败隔离红灯测试 |
| `src/Launcher.Background/Hosting/IBackgroundTaskHost.cs` | 新增 | Task 3.2：后台 Worker 宿主接口 |
| `src/Launcher.Background/Hosting/BackgroundTaskHost.cs` | 新增 | Task 3.2：顺序启动、逆序停止、失败隔离的后台 Worker 宿主 |
| `docs/SessionContextRecord.md` | 修改 | Task 3.3：标记 TokenRefreshBackgroundService 迁移任务开始 |
| `tests/Launcher.Tests.Unit/TokenRefreshBackgroundServiceTests.cs` | 新增 | Task 3.3：新增 TokenRefresh Worker 生命周期和 DI 注册红灯测试 |
| `src/Launcher.Background/Auth/TokenRefreshBackgroundService.cs` | 修改 | Task 3.3：实现 `IBackgroundWorker`，新增 `Name`、`State`、`StartAsync`、`StopAsync`，保留旧 `Start/Stop` |
| `src/Launcher.Background/DependencyInjection.cs` | 修改 | Task 3.3：将 `TokenRefreshBackgroundService` 同时注册为 `IBackgroundWorker` |
| `tests/Launcher.Tests.Unit/BackgroundTaskHostTests.cs` | 修改 | Task 3.4：新增 Background DI 注册和 App 只解析 Host 的红灯测试 |
| `src/Launcher.Background/Installations/AutoInstallWorker.cs` | 修改 | Task 3.4：实现 `IBackgroundWorker`，保留旧 `Start/Stop`，纳入统一宿主 |
| `src/Launcher.Background/Updates/AppUpdateWorker.cs` | 修改 | Task 3.4：实现 `IBackgroundWorker`，保留定时检查和主动检查行为 |
| `src/Launcher.Background/Network/NetworkMonitorWorker.cs` | 修改 | Task 3.4：实现 `IBackgroundWorker`，避免重复订阅网络事件 |
| `src/Launcher.Background/DependencyInjection.cs` | 修改 | Task 3.4：注册 `IBackgroundTaskHost`，并将 AutoInstall/AppUpdate/Network 注册为 `IBackgroundWorker` |
| `src/Launcher.App/App.xaml.cs` | 修改 | Task 3.4：`StartBackgroundServicesAsync` 只解析 `IBackgroundTaskHost`，Fab 预热包装为 App 组合根 Worker |
| `.codex/` | 新增 | 工作区安装 `planning-with-files` Codex skill 与 hooks |
| `src/Launcher.Application/Modules/Downloads/README_ARCH.md` | 修改 | Task 4.1：记录 Scheduler 到 Worker 的当前断点、真实启动链路和后续闭环方向 |
| `tests/Launcher.Tests.Unit/DownloadWorkerContractTests.cs` | 新增 | Task 4.2：下载执行端口契约和 Scheduler 分发测试 |
| `src/Launcher.Application/Modules/Downloads/Contracts/IDownloadScheduler.cs` | 修改 | Task 4.2：新增 `IDownloadTaskExecutor` 内部执行端口契约 |
| `src/Launcher.Infrastructure/Downloads/DownloadOrchestrator.cs` | 修改 | Task 4.3：订阅 Scheduler 分发事件并委托下载执行端口 |
| `src/Launcher.Infrastructure/Downloads/DownloadWorker.cs` | 新增 | Task 4.3：最小下载任务执行器，处理单个任务的成功、失败和取消收口 |
| `tests/Launcher.Tests.Unit/DownloadOrchestratorTests.cs` | 修改 | Task 4.3：验证 Scheduler 分发后执行器被调用、完成/失败语义 |
| `src/Launcher.Application/Modules/Downloads/Contracts/IDownloadRuntimeStore.cs` | 修改 | Task 4.3：补充执行器写入进度、完成、失败和移除快照所需端口 |
| `src/Launcher.Infrastructure/Downloads/ChunkDownloadClient.cs` | 修改 | Task 4.3：新增 `IChunkDownloader` 抽象并由现有客户端实现 |
| `src/Launcher.Infrastructure/DependencyInjection.cs` | 修改 | Task 4.3：注册 `IChunkDownloader` 和 `IDownloadTaskExecutor` |
| `tests/Launcher.Tests.Unit/SettingsServiceFabLibraryConfigTests.cs` | 修改中 | Task 5.1：已新增 DownloadOptions 红灯测试；实现时调整为解析 Application 下载配置端口，避免 Shared 反向依赖 Application |
| `docs/SessionContextRecord.md` | 修改中 | Task 5.1：记录红灯结果和配置端口设计决策 |
| `src/Launcher.Application/Modules/Downloads/Contracts/DownloadOptions.cs` | 新增 | Task 5.1：新增下载运行时 Options 和 `IDownloadOptionsProvider` Application 端口 |
| `src/Launcher.Infrastructure/Configuration/AppConfigProvider.cs` | 修改 | Task 5.1：从配置读取下载 Options，兼容旧 `MaxConcurrent` / `MaxChunksPerTask` 键 |
| `src/Launcher.Infrastructure/DependencyInjection.cs` | 修改 | Task 5.1：以同一 `AppConfigProvider` 实例注册 `IAppConfigProvider` 和 `IDownloadOptionsProvider` |
| `src/Launcher.App/appsettings.json` | 修改 | Task 5.1：下载配置改为数据驱动键：任务并发、chunk 并发、chunk size、重试次数、checkpoint 周期 |
| `tests/Launcher.Tests.Unit/SettingsServiceFabLibraryConfigTests.cs` | 修改 | Task 5.1：验证配置值、文档默认值和旧兼容属性 |
| `docs/SessionContextRecord.md` | 修改中 | Task 5.2：标记 API Options 数据驱动任务开始 |
| `tests/Launcher.Tests.Unit/ApiOptionsTests.cs` | 新增 | Task 5.2：红灯验证命名 HttpClient 使用配置 BaseAddress，并验证非 HTTPS 错误信息不泄漏敏感 query |
| `src/Launcher.Infrastructure/Configuration/FabApiOptions.cs` | 新增 | Task 5.2：Fab API BaseAddress Options，默认保留现有端点 |
| `src/Launcher.Infrastructure/Configuration/EpicApiOptions.cs` | 新增 | Task 5.2：Epic library、catalog、EngineVersion BaseAddress Options，默认保留现有端点 |
| `src/Launcher.Infrastructure/Configuration/UpdateOptions.cs` | 新增 | Task 5.2：GitHub update API BaseAddress Options，默认保留现有端点 |
| `src/Launcher.Infrastructure/DependencyInjection.cs` | 修改 | Task 5.2：命名 HttpClient 从 Options 读取 BaseAddress，并将 HTTPS 校验错误中的 query/userinfo 脱敏 |
| `src/Launcher.App/appsettings.json` | 修改 | Task 5.2：新增 `FabApi`、`EpicApi`、`UpdateApi` 配置段 |

---

## 6. 最近验证

当前只新增文档，未运行 build/test。已执行以下只读检查：

```powershell
git status --short
Get-Content .\docs\SessionContextRecord.md -Encoding UTF8
Select-String -Path .\docs\17-ArchitectureOptimizationPlan.md,.\docs\18-ArchitectureOptimizationImplementation.md,.\docs\SessionContextRecord.md -Pattern '上下文压缩|SessionContextRecord|原子任务|铁律'
```

检查结果摘要：

- 三份文档已创建。
- 未发现未完成占位词。
- `SessionContextRecord.md` 铁律已写入方案文档和实现文档。
- `git status --short` 显示本轮新增 3 个文档；仓库中还存在此前未由本轮创建的既有未提交改动。
- 已补充额度风险替代检测约束：AI 无法读取精确额度，以保守上下文风险信号作为触发条件。
- 已执行占位词检查：无未完成占位词输出。
- 已执行 `git diff --check -- docs/17-ArchitectureOptimizationPlan.md docs/18-ArchitectureOptimizationImplementation.md docs/SessionContextRecord.md`，无空白错误；仅出现 Git 的 LF/CRLF 提示。
- 已确认本次补丁目标文件仅为三份文档。
- 已创建隔离 worktree：`C:\tmp\superpowers\worktrees\MyEpicLauncher\architecture-optimization-implementation`。
- 已执行 `dotnet restore .\HelsincyEpicLauncher.slnx`，成功。
- 已执行 `dotnet build .\HelsincyEpicLauncher.slnx --no-restore`，成功；存在既有 analyzer 警告，0 个错误。
- 已执行 `git status --short`，隔离 worktree 初始状态无未提交文件。
- 已执行 Task 0.1 验证命令 `Get-Content .\docs\SessionContextRecord.md -Encoding UTF8`，输出包含当前任务、恢复顺序和最近验证命令。
- 已提交 Task 0.1：`e91bdd3 docs: 初始化架构优化执行记录`。
- Task 0.2 已确认 `Directory.Build.props` 中真实 TFM 为 `net9.0-windows10.0.19041.0`。
- Task 0.2 已确认当前项目文件未启用 `SelfContained`；`src/Launcher.App/Launcher.App.csproj` 仅设置 `RuntimeIdentifiers=win-x64`。
- 已执行 Task 0.2 验证命令 `dotnet build .\HelsincyEpicLauncher.slnx --no-restore`，构建成功；存在既有 analyzer 警告，0 个错误。
- 已提交 Task 0.2：`aec9f91 docs: 同步项目运行时基线`。
- Task 0.3 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ProjectReferenceRulesTests"`，按预期失败在 `NotImplementedException`。
- Task 0.3 绿灯第一次验证失败：同一命令编译失败，错误为 `CS1061 StringAssertions` 不包含 `BeAnExistingFile`；下一步改用 `File.Exists(...).Should().BeTrue(...)`。
- Task 0.3 绿灯验证已执行：同一过滤测试通过，3 个测试通过，0 个失败；存在既有 analyzer 警告。
- 已提交 Task 0.3：`82de133 test: 添加项目引用方向护栏`。
- Task 0.4 已扫描当前 Presentation 中的 `Launcher.Domain` 引用，已知例外为 `DownloadsPage.xaml.cs`、`DownloadsViewModel.cs`、`InstallationsViewModel.cs`。
- Task 0.4 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests"`，按预期失败在 `NotImplementedException`。
- Task 0.4 绿灯验证已执行：同一过滤测试通过，1 个测试通过，0 个失败；存在既有 analyzer 警告。
- 已执行 Phase 0 架构测试过滤验证：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~Architecture"`，4 个测试通过，0 个失败。
- 已提交 Task 0.4：`798e994 test: 添加 Presentation 禁用命名空间护栏`。
- Task 1.1 已读取 `docs/06-ModuleDefinitions/Downloads.md` 和 `docs/07-DownloadSubsystem.md`。
- Task 1.1 已确认当前公共入口为 `IDownloadCommandService` 和 `IDownloadReadService`。
- Task 1.1 已确认 `IDownloadTaskRepository`、`IDownloadScheduler`、`IDownloadRuntimeStore`、`IDownloadOrchestrator` 当前位于 Contracts 目录，但架构语义上应视作内部端口。
- Task 1.1 已确认当前 `ChunkDownloadClient` 没有 Application 层接口，后续应引入内部 `IChunkDownloader` 端口。
- 已执行 Task 1.1 验证命令 `dotnet build .\src\Launcher.Application\Launcher.Application.csproj --no-restore`，构建成功，0 警告，0 错误。
- 已提交 Task 1.1：`bdee417 docs: 梳理 Downloads 应用层端口`。
- Task 1.2 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~StartDownloadUseCaseTests"`，按预期编译失败，因为 `StartDownloadUseCase` 尚不存在。
- Task 1.2 绿灯验证已执行：同一过滤测试通过，2 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 1.2 额外验证已执行：`dotnet build .\src\Launcher.Application\Launcher.Application.csproj --no-restore`，构建成功，0 警告，0 错误。
- 已提交 Task 1.2：`8d1247b feat: 添加开始下载用例壳`。
- Task 1.3 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadCommandServiceTests"`，按预期编译失败，因为 Application 层 `DownloadCommandService` 尚不存在。
- Task 1.3 绿灯验证已执行：同一过滤测试通过，3 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 1.3 App 构建验证已执行：`dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`，构建成功，0 警告，0 错误。
- 已提交 Task 1.3：`8048253 refactor: 将下载命令服务迁到应用层`。
- Task 1.4 已读取 `docs/06-ModuleDefinitions/Installations.md`。
- Task 1.4 已确认 `InstallCommandService.InstallAsync`、`UninstallAsync`、`RepairAsync` 含有应用编排，应逐步迁到 Application。
- Task 1.4 已确认 `InstallWorker`、`RepairFileDownloader`、`IntegrityVerifier`、`HashingService`、`InstallationRepository` 应继续留在 Infrastructure。
- 已执行 Task 1.4 验证命令 `dotnet build .\src\Launcher.Application\Launcher.Application.csproj --no-restore`，构建成功，0 警告，0 错误。
- 已提交 Task 1.4：`ba39493 docs: 梳理 Installations 编排边界`。
- Task 2.1 已读取 `docs/04-ModuleDependencyRules.md` 和 `docs/05-CoreInterfaces.md`。
- Task 2.1 已明确 Repository 端口属于内部端口，不能作为 Presentation 或跨模块入口。
- 已执行 Task 2.1 验证命令 `dotnet build .\HelsincyEpicLauncher.slnx --no-restore`，构建成功；存在既有 9 个 analyzer 警告，0 个错误。
- 已提交 Task 2.1：`c363968 docs: 明确公共契约与内部端口规则`。
- Task 2.2 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadModelsTests"`，按预期编译失败，因为 `DownloadStatusKind` 和 `DownloadStatusSummary.Status` 尚不存在。
- Task 2.2 绿灯第一次验证失败：同一命令编译失败，原因是当前 Domain `DownloadUiState` 没有 `Installing` 值；下一步按真实枚举修正映射。
- Task 2.2 绿灯验证已执行：同一过滤测试通过，4 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 2.2 App 构建验证已执行：`dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`，构建成功，0 警告，0 错误。
- 已提交 Task 2.2：`70f362d feat: 添加下载状态公共投影`。
- Task 2.3 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests"`，按预期失败；失败输出暴露 `DownloadsPage.xaml.cs`、`DownloadsViewModel.cs` 仍直接引用 `Launcher.Domain`，`InstallationsViewModel.cs` 仍是当前唯一保留例外。
- Task 2.3 Contract 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadModelsTests"`，按预期编译失败；缺少 `DownloadStatusSummary.TaskKey`、`DownloadProgressSnapshot.TaskKey`、`DownloadProgressSnapshot.Status`。
- Task 2.3 Contract 绿灯验证已执行：同一 `DownloadModelsTests` 过滤命令通过，6 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 2.3 namespace 绿灯验证已执行：同一 `ForbiddenNamespaceReferenceTests` 过滤命令通过，1 个测试通过，0 个失败。
- Task 2.3 指定测试验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests|FullyQualifiedName~DownloadRuntimeStoreTests"`，14 个测试通过，0 个失败。
- Task 2.3 Presentation 构建验证已执行：`dotnet build .\src\Launcher.Presentation\Launcher.Presentation.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 2.3 额外源码检查已执行：`rg -n "Launcher\.Domain" src\Launcher.Presentation\Modules\Downloads -g "*.cs"` 无匹配，退出码 1 表示未找到匹配项。
- Task 2.3 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 2.3 代码提交已创建：`56f67aa refactor: 移除 Downloads UI 领域引用`。
- Task 2.3 暂停上下文提交已创建：`1e58ecf docs: 记录 Task 2.3 暂停上下文`。
- Task 2.4 已按恢复协议读取 `docs/SessionContextRecord.md`、`docs/17-ArchitectureOptimizationPlan.md`、`docs/18-ArchitectureOptimizationImplementation.md`。
- Task 2.4 已读取 `docs/06-ModuleDefinitions/Installations.md`、`src/Launcher.Presentation/Modules/Installations/InstallationsViewModel.cs`、`src/Launcher.Application/Modules/Installations/Contracts/InstallModels.cs`。
- Task 2.4 已确认当前唯一剩余 Presentation -> Domain 例外为 `src/Launcher.Presentation/Modules/Installations/InstallationsViewModel.cs`。
- Task 2.4 架构红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests"`，按预期失败；失败输出显示 `InstallationsViewModel.cs` 仍直接引用 `Launcher.Domain`。
- Task 2.4 Contract 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~InstallationTests"`，按预期编译失败；缺少 `InstallStatusKind` 和 `InstallStatusSummary.Status`。
- Task 2.4 Contract 绿灯验证已执行：同一 `InstallationTests` 过滤命令通过，9 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 2.4 namespace 绿灯验证已执行：同一 `ForbiddenNamespaceReferenceTests` 过滤命令通过，1 个测试通过，0 个失败。
- Task 2.4 指定测试验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests|FullyQualifiedName~InstallationTests"`，10 个测试通过，0 个失败。
- Task 2.4 Presentation 构建验证已执行：`dotnet build .\src\Launcher.Presentation\Launcher.Presentation.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 2.4 额外源码检查已执行：`rg -n "Launcher\.Domain" src\Launcher.Presentation -g "*.cs"` 无匹配，退出码 1 表示未找到匹配项。
- Task 2.4 初始化点检查已执行：`rg -n "new InstallStatusSummary" src tests -g "*.cs"` 仅发现 `InstallReadService.cs` 和本任务新增测试两个初始化点，均已设置 `Status`。
- Task 2.4 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 2.4 代码提交已创建：`508ff02 refactor: 移除 Installations UI 领域引用`。
- Task 2.4 完成上下文提交已创建：`4f76b58 docs: 记录 Task 2.4 完成上下文`。
- Task 3.1 已执行 `rg --files src\Launcher.Background tests\Launcher.Tests.Unit | sort`，确认当前 Background 文件为 Auth/TokenRefresh、Installations/AutoInstall、Network/NetworkMonitor、Updates/AppUpdate、DependencyInjection 和项目文件。
- Task 3.1 已读取 `src/Launcher.Background/Launcher.Background.csproj`、`DependencyInjection.cs`、`TokenRefreshBackgroundService.cs`、`AutoInstallWorker.cs`、`NetworkMonitorWorker.cs`、`AppUpdateWorker.cs`。
- Task 3.1 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~BackgroundWorkerContractTests"`，按预期编译失败；`Launcher.Background.Hosting` 命名空间尚不存在。
- Task 3.1 绿灯验证已执行：同一 `BackgroundWorkerContractTests` 过滤命令通过，2 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 3.1 Background 构建验证已执行：`dotnet build .\src\Launcher.Background\Launcher.Background.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 3.1 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 3.1 代码提交已创建：`39423c6 feat: 添加后台 Worker 生命周期契约`。
- Task 3.1 完成上下文提交已创建：`5e3312a docs: 记录 Task 3.1 完成上下文`。
- Task 3.2 已读取 `docs/SessionContextRecord.md`、`src/Launcher.Background/Hosting/IBackgroundWorker.cs`、`src/Launcher.Background/Hosting/WorkerStatus.cs`，并确认 worktree 初始状态干净。
- Task 3.2 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~BackgroundTaskHostTests"`，按预期编译失败；`BackgroundTaskHost` 尚不存在。
- Task 3.2 绿灯验证已执行：同一 `BackgroundTaskHostTests` 过滤命令通过，3 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 3.2 Background 构建验证已执行：`dotnet build .\src\Launcher.Background\Launcher.Background.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 3.2 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 3.2 代码提交已创建：`9fa1623 feat: 添加后台任务宿主`。
- Task 3.2 完成上下文提交已创建：`fb545af docs: 记录 Task 3.2 完成上下文`。
- Task 3.3 已按恢复协议读取 `docs/SessionContextRecord.md`、`docs/17-ArchitectureOptimizationPlan.md`、`docs/18-ArchitectureOptimizationImplementation.md`。
- Task 3.3 已读取 `src/Launcher.Background/Auth/TokenRefreshBackgroundService.cs`、`src/Launcher.Background/DependencyInjection.cs`、`src/Launcher.Application/Modules/Auth/Contracts/IAuthService.cs`。
- Task 3.3 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~TokenRefreshBackgroundServiceTests"`，按预期失败；`TokenRefreshBackgroundService` 尚未实现 `IBackgroundWorker`，且 `AddBackground` 尚未注册 `IBackgroundWorker`。
- Task 3.3 绿灯验证已执行：同一 `TokenRefreshBackgroundServiceTests` 过滤命令通过，2 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 3.3 Background 构建验证已执行：`dotnet build .\src\Launcher.Background\Launcher.Background.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 3.3 App 构建兼容验证已执行：`dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 3.3 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 3.3 代码提交已创建：`03da86c refactor: 迁移 Token 刷新服务到后台 Worker`。
- Task 3.3 完成上下文提交已创建：`55392c1 docs: 记录 Task 3.3 完成上下文`。
- Task 3.4 已读取 `src/Launcher.App/App.xaml.cs`、`src/Launcher.Background/DependencyInjection.cs`、`AutoInstallWorker.cs`、`AppUpdateWorker.cs`、`NetworkMonitorWorker.cs`、`AutoInstallWorkerTests.cs`。
- Task 3.4 红灯第一次验证失败在测试自身类型推断，已修正 `BackgroundTaskHostTests` 中 `BeEquivalentTo` 的期望数组写法。
- Task 3.4 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~BackgroundTaskHostTests"`，按预期失败；`IBackgroundTaskHost` 尚未在 `AddBackground` 注册，且 App 仍直接解析具体 Worker 并调用 `StartFabLibraryWarmup`。
- Task 3.4 绿灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~BackgroundTaskHostTests"`，5 个测试通过，0 个失败。
- Task 3.4 App 构建验证已执行：`dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 3.4 小竞态修正后已重新执行同一组验证：`BackgroundTaskHostTests` 5 个通过，App 构建 0 警告 0 错误。
- Task 3.4 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 3.4 代码提交已创建：`98c0f51 refactor: 通过后台宿主启动 Worker`。
- planning-with-files 工作区安装已提交：`c4df093 chore: 安装 planning-with-files Codex 工作区配置`。
- 全局 Codex hooks 已启用：`C:\Users\14481\.codex\config.toml` 中已写入 `[features] codex_hooks = true`；该文件不属于本项目仓库，未提交到项目 git。
- Task 4.1 已搜索 `TaskReady +=`、`TaskReady`、`QueueAsync`、`StartDownloadUseCase`、`DownloadOrchestrator`、`DownloadScheduler`、`NotifyCompleted`、`ChunkDownloadClient`。
- Task 4.1 当前发现：`TaskReady +=` 只存在于 `DownloadSchedulerTests.cs`；生产代码没有订阅者，`ChunkDownloadClient` 当前仅注册于 DI，没有被调度链路消费。
- Task 4.1 验证命令已执行：`rg "TaskReady\s*\+=" src tests -g "*.cs"`；输出仅包含 `tests\Launcher.Tests.Unit\DownloadSchedulerTests.cs` 中 6 处测试订阅，无生产代码命中。
- Task 4.1 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 4.1 文档提交已创建：`ec557b3 docs: 记录下载调度断点`。
- Task 4.1 完成上下文提交已创建：`d3cdd88 docs: 记录 Task 4.1 完成上下文`。
- Task 4.2 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadWorkerContractTests"`，按预期编译失败；缺少 `IDownloadTaskExecutor`。
- Task 4.2 绿灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadWorkerContractTests|FullyQualifiedName~DownloadSchedulerTests"`，9 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 4.2 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 4.2 代码/测试提交已创建：`171c28b test: 添加下载执行端口契约`。
- Task 4.2 完成上下文提交已创建：`b16ab93 docs: 记录 Task 4.2 完成上下文`。
- Task 4.3 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadOrchestratorTests"`，按预期失败；缺少 3 参数 Orchestrator 构造、`IChunkDownloader`、`DownloadWorker` 和 RuntimeStore 写入端口。
- Task 4.3 第一次绿灯验证失败：同一指定过滤命令中 `IChunkDownloader` 为 internal，NSubstitute 无法代理；随后改为 public 端口。
- Task 4.3 绿灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadOrchestratorTests|FullyQualifiedName~DownloadSchedulerTests"`，20 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 4.3 App 构建验证已执行：`dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 4.3 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 4.3 代码提交已创建：`2efe224 feat: 连接下载调度器与执行器`。
- Task 4.2 完成上下文提交已创建：`b16ab93 docs: 记录 Task 4.2 完成上下文`。
- Task 5.1 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~SettingsServiceFabLibraryConfigTests"`，按预期编译失败；缺少 `DownloadOptions` 和配置读取端口。
- Task 5.1 设计决策：`IAppConfigProvider` 位于 `Launcher.Shared`，不能依赖 `Launcher.Application` 中的 `DownloadOptions`，因此新增 Application 端口 `IDownloadOptionsProvider`，由 Infrastructure `AppConfigProvider` 实现，避免 Shared 反向依赖 Application。
- Task 5.1 绿灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~SettingsServiceFabLibraryConfigTests"`，4 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 5.1 计划验证已执行：`dotnet build .\src\Launcher.Infrastructure\Launcher.Infrastructure.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 5.1 App 构建验证已执行：`dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 5.1 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 5.1 代码提交已创建：`bc42520 feat: 添加下载配置 Options`。
- Task 5.1 完成上下文提交已创建：`00cc581 docs: 记录 Task 5.1 完成上下文`。
- Task 5.2 红灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ApiOptionsTests"`，按预期失败 2 个测试：配置 BaseAddress 未生效；非 HTTPS 配置未触发异常。
- Task 5.2 绿灯验证已执行：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ApiOptionsTests"`，2 个测试通过，0 个失败；存在既有 analyzer 警告。
- Task 5.2 计划验证已执行：`dotnet build .\src\Launcher.Infrastructure\Launcher.Infrastructure.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 5.2 App 构建验证已执行：`dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`，构建成功，0 警告，0 错误。
- Task 5.2 补丁检查已执行：`git diff --check` 无空白错误；仅有 Git 的 LF/CRLF 提示。
- Task 5.2 代码提交已创建：`9e16267 feat: 添加 API 端点配置 Options`。

---

## 7. 未完成事项

- Task 5.2 已完成：新增 API Options，并让 Fab/Epic/EngineVersion/Update 命名 HttpClient 从配置读取 BaseAddress。
- 下一项候选任务为 Phase 5 Task 5.3：处理 OAuth 配置安全语义；开始前必须读取 `src/Launcher.Infrastructure/Auth/EpicOAuthOptions.cs`、`docs/06-ModuleDefinitions/Auth.md`、`src/Launcher.App/appsettings.json`。
- 主工作区 `Q:\MyEpicLauncher` 存在既有未提交改动，不属于本轮实现 worktree。

---

## 10. 本轮暂停记录（2026-04-30）

本轮已从用户“继续推任务”后连续完成并提交：

- `1e58ecf docs: 记录 Task 2.3 暂停上下文`
- `508ff02 refactor: 移除 Installations UI 领域引用`
- `4f76b58 docs: 记录 Task 2.4 完成上下文`
- `39423c6 feat: 添加后台 Worker 生命周期契约`
- `5e3312a docs: 记录 Task 3.1 完成上下文`
- `9fa1623 feat: 添加后台任务宿主`

当前还有本文件的完成上下文记录未提交。记录提交后应暂停，等待用户继续指令，避免在同一上下文中继续跨入 Task 3.3 导致恢复信息过长。

恢复后的唯一正确动作：

1. 读取本文件。
2. 检查 `git status --short`。
3. 若只剩 `docs/SessionContextRecord.md`，提交完成上下文，建议提交信息：`docs: 记录 Task 3.2 完成上下文`。
4. 等待用户继续；若用户继续，从 Task 3.3 开始，不跳任务。

---

## 8. 压缩前必须补充的信息

如果上下文将要爆满，请在压缩前补齐以下内容：

1. 当前正在执行的 Task 编号。
2. 已修改但未验证的文件。
3. 最近一次成功命令。
4. 最近一次失败命令和错误摘要。
5. 下一步只能做什么。
6. 哪些文件绝对不能碰。
7. 用户新增的最新约束。

---

## 9. 额度风险暂停记录（2026-04-30）

用户明确提示“快限额了”，已触发铁律中的额度/上下文风险暂停流程。记录完成后必须停止继续执行，不再修改源码、不再推进下一任务。

### 9.1 当前状态

- 执行 worktree：`C:\tmp\superpowers\worktrees\MyEpicLauncher\architecture-optimization-implementation`
- 执行分支：`codex/architecture-optimization-implementation`
- 最近代码提交：`56f67aa refactor: 移除 Downloads UI 领域引用`
- 当前任务：Task 2.3 已完成代码实现、验证和代码提交。
- 当前未提交内容：`docs/SessionContextRecord.md` 的收口/额度风险记录。
- 下一项候选任务：Task 2.4：移除 Installations UI 对 Domain 的直接引用。
- 现在必须停止：不要继续 Task 2.4，不要再做代码修改，不要删除文件。

### 9.2 Task 2.3 已完成内容

- 新增 `DownloadTaskKey` 作为 Application Contracts 拥有的下载任务公共标识。
- `DownloadStatusSummary` 新增 `TaskKey` 投影，保留旧 `TaskId` 兼容字段。
- `DownloadProgressSnapshot` 新增 `TaskKey` 和 `Status` 投影，保留旧 `TaskId` / `UiState` 兼容字段。
- 下载完成/失败/进度事件新增 `TaskKey` 投影。
- `DownloadsViewModel.cs` 移除 `using Launcher.Domain.Downloads`，命令参数改为 `DownloadTaskKey`，显示状态改为 `DownloadStatusKind`。
- `DownloadsPage.xaml.cs` 移除 `using Launcher.Domain.Downloads`，按钮 Tag 类型改为 `DownloadTaskKey`。
- `DownloadsPage.xaml` 按钮 Tag 绑定由 `TaskId` 改为 `TaskKey`。
- `ForbiddenNamespaceReferenceTests` 中 Downloads 相关例外已移除，仅剩 `InstallationsViewModel.cs`。
- `DownloadModelsTests` 已补充 `DownloadTaskKey` 和快照公共状态测试。

### 9.3 最近验证记录

- 红灯：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests"` 按预期失败，暴露 `DownloadsPage.xaml.cs` 和 `DownloadsViewModel.cs` 的 Domain 引用。
- 红灯：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadModelsTests"` 按预期编译失败，缺少 `TaskKey` / 快照 `Status`。
- 绿灯：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadModelsTests"` 通过，6 个测试通过。
- 绿灯：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests"` 通过，1 个测试通过。
- 指定验证：`dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests|FullyQualifiedName~DownloadRuntimeStoreTests"` 通过，14 个测试通过。
- 构建验证：`dotnet build .\src\Launcher.Presentation\Launcher.Presentation.csproj --no-restore` 成功，0 警告，0 错误。
- 额外检查：`rg -n "Launcher\.Domain" src\Launcher.Presentation\Modules\Downloads -g "*.cs"` 无匹配，退出码 1 表示未找到匹配项。
- 补丁检查：`git diff --check` 无空白错误，仅有 LF/CRLF 提示。

### 9.4 恢复后的唯一正确动作

1. 先读取本文件。
2. 检查 `git status --short`，预期至少会看到 `docs/SessionContextRecord.md` 未提交。
3. 提交本记录文件，建议提交信息：`docs: 记录 Task 2.3 暂停上下文`。
4. 等待用户明确继续后，再开始 Task 2.4。
5. 若继续 Task 2.4，开始前必须读取 `docs/06-ModuleDefinitions/Installations.md`、`src/Launcher.Presentation/Modules/Installations/InstallationsViewModel.cs`、`src/Launcher.Application/Modules/Installations/Contracts/InstallModels.cs`。

### 9.5 禁止事项

- 不触碰 `Q:\MyEpicLauncher` 主工作区中的既有未提交改动。
- 不删除任何文件。
- 不跳过 `SessionContextRecord.md` 恢复协议。
- 不在未得到用户继续确认前执行 Task 2.4 或更后续任务。
