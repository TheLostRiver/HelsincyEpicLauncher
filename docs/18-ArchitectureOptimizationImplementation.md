# Architecture Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` when executing this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将当前项目从“文档架构清晰、实现存在折中”推进到“Application 层有真实用例、Contracts 稳定、后台任务统一、配置数据驱动、核心下载管线闭环”的状态。

**Architecture:** 采用渐进式重构。先建立边界测试和上下文记录协议，再按模块迁移编排逻辑、收紧 Contracts、统一后台宿主，最后拆分大类和数据驱动配置。每个任务都必须小而可验证。

**Tech Stack:** WinUI 3、.NET 9 Windows TFM、CommunityToolkit.Mvvm、Microsoft.Extensions.DependencyInjection、SQLite + Dapper、Serilog、Polly、xUnit、NSubstitute、FluentAssertions。

---

## 0. 铁律：SessionContextRecord

`docs/SessionContextRecord.md` 是后续所有架构优化任务的上下文锚点。

### 0.1 必须更新的时机

执行任何原子任务时，若出现以下任一情况，必须先更新 `docs/SessionContextRecord.md`：

- 上下文将要爆满，或感觉后续回复可能触发压缩。
- 一个原子任务执行到一半，还没有完成验证。
- 即将跨模块修改 Contracts、DI、后台生命周期或启动流程。
- 发现实现和文档不一致，需要后续决策。
- 当前会话准备结束。

### 0.2 压缩后的恢复顺序

上下文压缩后，新的执行者必须按此顺序恢复：

1. 读取 `docs/SessionContextRecord.md`。
2. 读取当前任务涉及的方案文档：`docs/17-ArchitectureOptimizationPlan.md` 和本文档。
3. 读取当前任务列出的源码和测试文件。
4. 检查 `git status --short`，确认是否有前一段未完成改动。
5. 只继续 `SessionContextRecord.md` 中标记的当前任务，不自行跳任务。

### 0.3 每个原子任务的共同结束条件

每个任务结束前必须完成：

- 更新 `docs/SessionContextRecord.md` 的当前任务状态。
- 运行该任务指定的最小验证命令。
- 若验证失败，记录失败命令、错误摘要、下一步建议。
- 不删除任何文件，除非用户明确要求。

---

## 1. 文件责任地图

后续优化预计围绕以下文件和目录展开：

| 区域 | 责任 |
|------|------|
| `docs/17-ArchitectureOptimizationPlan.md` | 架构优化总方案 |
| `docs/18-ArchitectureOptimizationImplementation.md` | 原子任务实施计划 |
| `docs/SessionContextRecord.md` | 上下文压缩前后的恢复记录 |
| `src/Launcher.Application/Modules/*` | 用例、命令、查询、模块公共 Contracts、内部端口 |
| `src/Launcher.Domain/*` | 领域实体、状态机、纯规则 |
| `src/Launcher.Infrastructure/*` | HTTP、SQLite、文件系统、系统集成、Options 实现 |
| `src/Launcher.Background/*` | 后台 Worker 和统一宿主 |
| `src/Launcher.Presentation/*` | Page、ViewModel、UI service、UI-only WebView2 bridge |
| `tests/Launcher.Tests.Unit/*` | 状态机、用例、边界、ViewModel、后台 Worker 单元测试 |
| `tests/Launcher.Tests.Integration/*` | SQLite、迁移、持久化、下载恢复等集成测试 |

---

## Phase 0：护栏和真实基线

### Task 0.1：初始化 SessionContextRecord 基线

**Files:**
- Modify: `docs/SessionContextRecord.md`

- [ ] 读取 `docs/17-ArchitectureOptimizationPlan.md` 和本文档。
- [ ] 在 `SessionContextRecord.md` 中填写当前执行者、当前任务、已知未完成事项。
- [ ] 记录 `git status --short` 的摘要。
- [ ] 不改源码。

**验证：**
- Run: `Get-Content .\docs\SessionContextRecord.md -Encoding UTF8`
- Expected: 能看到当前任务、恢复顺序、最近验证命令。

### Task 0.2：同步 README 与技术栈真实基线

**Files:**
- Modify: `README.md`
- Modify: `docs/01-ProjectOverview.md`
- Modify: `docs/11-TechStack.md`

- [ ] 确认当前真实 TFM：`Directory.Build.props` 中的 `net9.0-windows10.0.19041.0`。
- [ ] 将 README 中 `.NET 8+` 的描述调整为“当前工程为 .NET 9 Windows TFM，文档原则仍兼容后续 LTS 策略”。
- [ ] 检查 `SelfContained` 是否真实存在；若没有，不写“当前已自包含发布”，只写“发布目标建议”。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet build .\HelsincyEpicLauncher.slnx --no-restore`
- Expected: build 成功，且没有因为文档修改影响编译。

### Task 0.3：新增项目引用方向测试

**Files:**
- Create: `tests/Launcher.Tests.Unit/Architecture/ProjectReferenceRulesTests.cs`

- [ ] 编写测试读取 `src/*/*.csproj`。
- [ ] 断言 `Launcher.Application` 不引用 `Launcher.Infrastructure` 或 `Launcher.Presentation`。
- [ ] 断言 `Launcher.Domain` 只引用 `Launcher.Shared`。
- [ ] 断言 `Launcher.Presentation` 不引用 `Launcher.Infrastructure`。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ProjectReferenceRulesTests"`
- Expected: 新测试通过。

### Task 0.4：新增禁用 namespace 扫描测试

**Files:**
- Create: `tests/Launcher.Tests.Unit/Architecture/ForbiddenNamespaceReferenceTests.cs`

- [ ] 编写测试扫描 `src/Launcher.Presentation/**/*.cs`。
- [ ] 先将当前 `Launcher.Domain` 引用列为已知例外清单。
- [ ] 测试必须输出例外文件路径，方便后续逐个消除。
- [ ] 不在本任务修复引用，只建立可观测护栏。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests"`
- Expected: 测试通过，并且例外清单明确。

---

## Phase 1：Application 层补实

### Task 1.1：梳理 Downloads 应用层端口

**Files:**
- Create: `src/Launcher.Application/Modules/Downloads/README_ARCH.md`
- Modify: `docs/SessionContextRecord.md`

- [ ] 读取 `docs/06-ModuleDefinitions/Downloads.md` 和 `docs/07-DownloadSubsystem.md`。
- [ ] 列出 Downloads 对外 Contracts：`IDownloadReadService`、`IDownloadCommandService`。
- [ ] 列出 Downloads 内部端口：Repository、Scheduler、RuntimeStore、Chunk client。
- [ ] 明确哪些端口可以返回 Domain，哪些公共 Contracts 不能返回 Domain。
- [ ] 不移动代码。

**验证：**
- Run: `dotnet build .\src\Launcher.Application\Launcher.Application.csproj --no-restore`
- Expected: build 成功。

### Task 1.2：为 Downloads 新增应用用例壳

**Files:**
- Create: `src/Launcher.Application/Modules/Downloads/UseCases/StartDownloadUseCase.cs`
- Test: `tests/Launcher.Tests.Unit/Downloads/StartDownloadUseCaseTests.cs`

- [ ] 先写测试：给定合法请求，用例调用编排端口并返回 TaskId。
- [ ] 新增 `StartDownloadUseCase`，只做参数前置校验和委托，不接触 HTTP/SQLite。
- [ ] 暂时不替换现有 `DownloadCommandService`，只引入可测试用例壳。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~StartDownloadUseCaseTests"`
- Expected: 新测试通过。

### Task 1.3：迁移 DownloadCommandService 到 Application

**Files:**
- Create: `src/Launcher.Application/Modules/Downloads/DownloadCommandService.cs`
- Modify: `src/Launcher.Infrastructure/DependencyInjection.cs`
- Modify: `src/Launcher.Infrastructure/Downloads/DownloadCommandService.cs`
- Test: `tests/Launcher.Tests.Unit/DownloadCommandServiceTests.cs`

- [ ] 将命令入口实现移动到 Application。
- [ ] Infrastructure 原实现短期保留为兼容包装或删除前先确认无引用。
- [ ] DI 注册改为 Application 实现。
- [ ] 保持公共接口不变。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadCommandServiceTests"`
- Run: `dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`
- Expected: 测试和 App 构建通过。

### Task 1.4：InstallCommandService 编排迁移预备

**Files:**
- Create: `src/Launcher.Application/Modules/Installations/README_ARCH.md`
- Modify: `docs/SessionContextRecord.md`

- [ ] 读取 `docs/06-ModuleDefinitions/Installations.md`。
- [ ] 列出 Installations 中哪些是用例编排，哪些是文件系统实现。
- [ ] 标记 `InstallWorker`、`RepairFileDownloader` 继续留在 Infrastructure。
- [ ] 标记 `RepairAsync` 主流程未来迁到 Application。
- [ ] 不移动代码。

**验证：**
- Run: `dotnet build .\src\Launcher.Application\Launcher.Application.csproj --no-restore`
- Expected: build 成功。

---

## Phase 2：Contracts 去 Domain 泄漏

### Task 2.1：建立公共 Contracts 与内部端口命名规则

**Files:**
- Modify: `docs/04-ModuleDependencyRules.md`
- Modify: `docs/05-CoreInterfaces.md`
- Modify: `docs/SessionContextRecord.md`

- [ ] 明确 `Contracts` 只用于跨模块和 UI 可见 DTO。
- [ ] 明确 Repository 端口属于 Application 内部端口，不能被 Presentation 依赖。
- [ ] 给出命名建议：`Contracts`、`Ports`、`Persistence`、`UseCases`。
- [ ] 不移动代码。

**验证：**
- Run: `dotnet build .\HelsincyEpicLauncher.slnx --no-restore`
- Expected: build 成功。

### Task 2.2：为 Downloads 增加 Contract-owned UI 类型

**Files:**
- Modify: `src/Launcher.Application/Modules/Downloads/Contracts/DownloadModels.cs`
- Test: `tests/Launcher.Tests.Unit/DownloadModelsTests.cs`

- [ ] 新增或确认公共 DTO 中的 `DownloadStatusSummary` 不要求 UI 引用 Domain。
- [ ] 如果需要新类型，先新增兼容字段，不删除旧字段。
- [ ] 测试 DTO 默认值、序列化友好性和不可变投影语义。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadModelsTests"`
- Expected: 新测试通过。

### Task 2.3：移除 Downloads UI 对 Domain 的直接引用

**Files:**
- Modify: `src/Launcher.Presentation/Modules/Downloads/DownloadsViewModel.cs`
- Modify: `src/Launcher.Presentation/Modules/Downloads/DownloadsPage.xaml.cs`
- Test: `tests/Launcher.Tests.Unit/DownloadRuntimeStoreTests.cs`

- [ ] 将 UI 命令参数改为 Application Contracts 暴露的类型。
- [ ] 保持按钮行为不变。
- [ ] 移除 `using Launcher.Domain.Downloads`。
- [ ] 更新 namespace 扫描测试的例外清单。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests|FullyQualifiedName~DownloadRuntimeStoreTests"`
- Run: `dotnet build .\src\Launcher.Presentation\Launcher.Presentation.csproj --no-restore`
- Expected: 测试和 Presentation 构建通过。

### Task 2.4：移除 Installations UI 对 Domain 的直接引用

**Files:**
- Modify: `src/Launcher.Application/Modules/Installations/Contracts/InstallModels.cs`
- Modify: `src/Launcher.Presentation/Modules/Installations/InstallationsViewModel.cs`
- Test: `tests/Launcher.Tests.Unit/InstallationTests.cs`

- [ ] 将 UI 需要的安装状态投影到 `InstallStatusSummary`。
- [ ] ViewModel 不再直接引用 `InstallState`，改用 Contract 状态或显示字段。
- [ ] 更新 namespace 扫描测试的例外清单。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ForbiddenNamespaceReferenceTests|FullyQualifiedName~InstallationTests"`
- Run: `dotnet build .\src\Launcher.Presentation\Launcher.Presentation.csproj --no-restore`
- Expected: 测试和 Presentation 构建通过。

---

## Phase 3：后台任务统一宿主

### Task 3.1：新增后台 Worker 抽象

**Files:**
- Create: `src/Launcher.Background/Hosting/IBackgroundWorker.cs`
- Create: `src/Launcher.Background/Hosting/WorkerStatus.cs`
- Test: `tests/Launcher.Tests.Unit/BackgroundWorkerContractTests.cs`

- [ ] 定义 `Name`、`StartAsync`、`StopAsync`、`State`。
- [ ] `StartAsync` 和 `StopAsync` 必须接受 `CancellationToken`。
- [ ] 测试接口存在且状态枚举覆盖 Idle/Running/Stopping/Stopped/Faulted。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~BackgroundWorkerContractTests"`
- Expected: 新测试通过。

### Task 3.2：新增 BackgroundTaskHost

**Files:**
- Create: `src/Launcher.Background/Hosting/BackgroundTaskHost.cs`
- Create: `src/Launcher.Background/Hosting/IBackgroundTaskHost.cs`
- Test: `tests/Launcher.Tests.Unit/BackgroundTaskHostTests.cs`

- [ ] Host 支持注册多个 Worker。
- [ ] `StartAllAsync` 顺序启动，单个失败不阻断其他 Worker，但记录 Faulted。
- [ ] `StopAllAsync` 逆序停止。
- [ ] 测试启动、停止、失败隔离。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~BackgroundTaskHostTests"`
- Expected: 新测试通过。

### Task 3.3：迁移 TokenRefreshBackgroundService

**Files:**
- Modify: `src/Launcher.Background/Auth/TokenRefreshBackgroundService.cs`
- Modify: `src/Launcher.Background/DependencyInjection.cs`
- Test: `tests/Launcher.Tests.Unit/TokenRefreshBackgroundServiceTests.cs`

- [ ] 实现 `IBackgroundWorker`。
- [ ] 保留当前 2 分钟检查逻辑。
- [ ] `StopAsync` 必须停止 Timer 并释放资源。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~TokenRefreshBackgroundServiceTests"`
- Expected: 现有测试通过或补齐后通过。

### Task 3.4：App 改为只启动后台宿主

**Files:**
- Modify: `src/Launcher.App/App.xaml.cs`
- Modify: `src/Launcher.Background/DependencyInjection.cs`
- Test: `tests/Launcher.Tests.Unit/BackgroundTaskHostTests.cs`

- [ ] `StartBackgroundServicesAsync` 只解析 `IBackgroundTaskHost`。
- [ ] 移除 App 对具体 Worker 的直接 `Start()` 调用。
- [ ] `FabLibraryWarmupCoordinator` 如需后台执行，包装为 Worker。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet build .\src\Launcher.App\Launcher.App.csproj --no-restore`
- Expected: App 构建通过。

---

## Phase 4：Downloads 管线闭环

### Task 4.1：记录当前 Scheduler 到 Worker 断点

**Files:**
- Modify: `src/Launcher.Application/Modules/Downloads/README_ARCH.md`
- Modify: `docs/SessionContextRecord.md`

- [ ] 搜索 `TaskReady +=` 的生产代码引用。
- [ ] 记录当前没有生产订阅者或记录真实订阅位置。
- [ ] 画出当前 StartAsync 到 QueueAsync 的路径。
- [ ] 不改生产代码。

**验证：**
- Run: `rg "TaskReady\\s*\\+=" src tests -g "*.cs"`
- Expected: 结果写入 `SessionContextRecord.md`。

### Task 4.2：新增 DownloadWorker 端口测试

**Files:**
- Create: `tests/Launcher.Tests.Unit/DownloadWorkerContractTests.cs`
- Modify: `src/Launcher.Application/Modules/Downloads/Contracts/IDownloadScheduler.cs`

- [ ] 明确 Scheduler 分发任务后应调用哪个执行端口。
- [ ] 不直接在 Scheduler 中 new 具体 Worker。
- [ ] 测试调度事件或执行端口被调用一次。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadWorkerContractTests|FullyQualifiedName~DownloadSchedulerTests"`
- Expected: 新旧测试通过。

### Task 4.3：连接 Scheduler 与下载执行器

**Files:**
- Modify: `src/Launcher.Infrastructure/Downloads/DownloadOrchestrator.cs`
- Create or Modify: `src/Launcher.Infrastructure/Downloads/DownloadWorker.cs`
- Test: `tests/Launcher.Tests.Unit/DownloadOrchestratorTests.cs`

- [ ] 在 Orchestrator 构造时订阅 Scheduler 分发事件，或引入明确执行端口。
- [ ] Worker 只处理一个任务。
- [ ] Worker 完成后调用 `NotifyCompleted`。
- [ ] 失败时更新 Repository 和 RuntimeStore。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~DownloadOrchestratorTests|FullyQualifiedName~DownloadSchedulerTests"`
- Expected: 下载编排和调度测试通过。

---

## Phase 5：Options 数据驱动

### Task 5.1：新增 DownloadOptions

**Files:**
- Create: `src/Launcher.Application/Modules/Downloads/Contracts/DownloadOptions.cs`
- Modify: `src/Launcher.Infrastructure/Configuration/AppConfigProvider.cs`
- Modify: `src/Launcher.App/appsettings.json`
- Test: `tests/Launcher.Tests.Unit/SettingsServiceFabLibraryConfigTests.cs`

- [ ] 将最大任务并发、最大 chunk 并发、chunk size、retry count、checkpoint interval 建模。
- [ ] 保持现有默认值：任务并发 3，chunk 并发 4。
- [ ] 不在本任务修改下载执行逻辑。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet build .\src\Launcher.Infrastructure\Launcher.Infrastructure.csproj --no-restore`
- Expected: build 成功。

### Task 5.2：新增 API Options

**Files:**
- Create: `src/Launcher.Infrastructure/Configuration/EpicApiOptions.cs`
- Create: `src/Launcher.Infrastructure/Configuration/FabApiOptions.cs`
- Create: `src/Launcher.Infrastructure/Configuration/UpdateOptions.cs`
- Modify: `src/Launcher.Infrastructure/DependencyInjection.cs`
- Modify: `src/Launcher.App/appsettings.json`

- [ ] 将 Fab、Epic library、Epic catalog、EngineVersion、GitHub update 的 BaseAddress 移到配置。
- [ ] 保留 HTTPS 校验。
- [ ] 日志记录端点时不记录 token 或敏感 query。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet build .\src\Launcher.Infrastructure\Launcher.Infrastructure.csproj --no-restore`
- Expected: build 成功。

### Task 5.3：处理 OAuth 配置安全语义

**Files:**
- Modify: `src/Launcher.App/appsettings.json`
- Modify: `src/Launcher.Infrastructure/Auth/EpicOAuthOptions.cs`
- Modify: `docs/06-ModuleDefinitions/Auth.md`

- [ ] 明确 `ClientSecret` 若属于公开桌面客户端凭据，不应被当作真正 secret。
- [ ] 支持从环境变量或 local settings 覆盖 OAuth 配置。
- [ ] 文档写清楚不要把私人凭证提交到仓库。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet build .\src\Launcher.Infrastructure\Launcher.Infrastructure.csproj --no-restore`
- Expected: build 成功。

---

## Phase 6：大类拆分

### Task 6.1：拆分 EpicOwnedFabCatalogClient 的 owned records 加载

**Files:**
- Create: `src/Launcher.Infrastructure/FabLibrary/EpicOwnedRecordsClient.cs`
- Modify: `src/Launcher.Infrastructure/FabLibrary/EpicOwnedFabCatalogClient.cs`
- Test: `tests/Launcher.Tests.Unit/EpicOwnedFabCatalogClientTests.cs`

- [ ] 只迁移 owned records 拉取、分页、cursor、缓存相关代码。
- [ ] 保持 `EpicOwnedFabCatalogClient` 公共行为不变。
- [ ] 不同时迁移 summary mapping。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~EpicOwnedFabCatalogClientTests"`
- Expected: 现有测试通过。

### Task 6.2：拆分 Fab summary mapping

**Files:**
- Create: `src/Launcher.Infrastructure/FabLibrary/EpicFabSummaryMapper.cs`
- Modify: `src/Launcher.Infrastructure/FabLibrary/EpicOwnedFabCatalogClient.cs`
- Test: `tests/Launcher.Tests.Unit/EpicOwnedFabCatalogClientTests.cs`

- [ ] 只迁移 `MapToSummary`、分类规范化、图片选择、格式提取等纯映射逻辑。
- [ ] Mapper 不做 HTTP，不读文件，不启动进程。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~EpicOwnedFabCatalogClientTests"`
- Expected: 现有测试通过。

### Task 6.3：拆分 DialogService 的 Epic 登录窗口

**Files:**
- Create: `src/Launcher.Presentation/Shell/EpicExchangeCodeLoginDialogService.cs`
- Modify: `src/Launcher.Presentation/Shell/DialogService.cs`
- Modify: `src/Launcher.Presentation/Shell/IDialogService.cs`
- Test: `tests/Launcher.Tests.Unit/EpicLoginWebViewBridgeTests.cs`

- [ ] 普通 Confirm/Info/Error/TextInput 仍由 `DialogService` 处理。
- [ ] WebView2 exchange code 登录窗口迁到独立服务。
- [ ] ShellViewModel 通过明确接口调用登录窗口。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~EpicLoginWebViewBridgeTests"`
- Run: `dotnet build .\src\Launcher.Presentation\Launcher.Presentation.csproj --no-restore`
- Expected: 测试和 Presentation 构建通过。

---

## Phase 7：最终一致性收口

### Task 7.1：更新架构文档与模块定义

**Files:**
- Modify: `docs/03-SolutionStructure.md`
- Modify: `docs/04-ModuleDependencyRules.md`
- Modify: `docs/05-CoreInterfaces.md`
- Modify: `docs/06-ModuleDefinitions/Downloads.md`
- Modify: `docs/06-ModuleDefinitions/Installations.md`
- Modify: `docs/06-ModuleDefinitions/FabLibrary.md`

- [ ] 只记录已经完成的代码现实，不预写未完成状态。
- [ ] 更新 Application、Background、Contracts、Options 的最终边界。
- [ ] 更新 `SessionContextRecord.md`。

**验证：**
- Run: `dotnet build .\HelsincyEpicLauncher.slnx --no-restore`
- Expected: build 成功。

### Task 7.2：全量验证

**Files:**
- Modify: `docs/SessionContextRecord.md`

- [ ] 运行全量 build。
- [ ] 运行全量 unit test。
- [ ] 运行 integration test。
- [ ] 记录失败或通过结果。
- [ ] 若失败，停止并记录下一步，不做临时大修。

**验证：**
- Run: `dotnet build .\HelsincyEpicLauncher.slnx --no-restore`
- Run: `dotnet test .\tests\Launcher.Tests.Unit\Launcher.Tests.Unit.csproj --no-restore`
- Run: `dotnet test .\tests\Launcher.Tests.Integration\Launcher.Tests.Integration.csproj --no-restore`
- Expected: 三个命令均通过，或失败详情完整写入 `SessionContextRecord.md`。

---

## 执行建议

推荐执行顺序：

1. Phase 0 完整执行。
2. Phase 3 和 Phase 4 优先处理，因为它们直接影响后台生命周期和下载核心能力。
3. Phase 1 和 Phase 2 交替推进，避免一次性大搬迁。
4. Phase 5 在核心边界稳定后推进。
5. Phase 6 最后拆大类，避免拆分时同时改业务语义。
6. Phase 7 收口文档和验证。

每次只执行一个 Task。不要在同一轮里混做多个 Phase。
