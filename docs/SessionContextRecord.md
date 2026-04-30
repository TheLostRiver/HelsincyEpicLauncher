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
| 当前基线提交 | `5e3312a`（Task 3.1 完成上下文提交） |
| 当前阶段 | Phase 3：后台任务统一宿主 |
| 当前任务 | Task 3.2：新增 BackgroundTaskHost |
| 当前状态 | 验证完成，待提交 |
| 下一步 | 执行 `git diff --check`、暂存并提交 Task 3.2 |
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

---

## 7. 未完成事项

- Task 3.2 已完成验证，待提交：新增 BackgroundTaskHost。
- 主工作区 `Q:\MyEpicLauncher` 存在既有未提交改动，不属于本轮实现 worktree。

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
