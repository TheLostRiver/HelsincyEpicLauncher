# 会话交接文档

## 最后更新
- 时间：2026-04-16
- 完成任务：Task 8.2（网络韧性增强）

## 当前项目状态
- 最后成功编译：是（dotnet build 9 个项目零错误零警告）
- 最后测试结果：全部通过（176/176）
- 当前 Phase：Phase 8 进行中（Task 8.1 + 8.2 完成）
- 下一个任务：Task 8.3（性能优化）

## 本次会话完成的工作

### Task 8.2 — 网络韧性增强
- Application 契约：INetworkMonitor（IsNetworkAvailable + NetworkStatusChanged 事件），IDownloadCommandService 新增 PauseAllAsync/ResumeAllAsync
- Infrastructure：NetworkMonitor（NetworkChange.NetworkAvailabilityChanged 驱动，Singleton + IDisposable），DI 注册
- DownloadOrchestrator：GetActiveTaskIdsAsync（非 Paused 活跃任务 ID）+ GetPausedTaskIdsAsync（Paused 状态任务 ID）
- DownloadCommandService：PauseAllAsync（批量暂停）+ ResumeAllAsync（批量恢复），逐任务记录失败日志
- Background：NetworkMonitorWorker（订阅 NetworkStatusChanged → 断联暂停/恢复续传，async void 内部 try/catch），DI 注册
- App.xaml.cs：NetworkMonitorWorker.Start() 在启动阶段
- ShellViewModel：注入 INetworkMonitor，构造时同步初始化 IsNetworkAvailable，订阅 NetworkStatusChanged（DispatcherQueue 切换线程）
- 遵循 AI-03：Background 仅依赖 Application 契约，不引用 Infrastructure

### Task 8.1 — 自动更新
- Application 层契约：UpdateInfo DTO、UpdateAvailableEvent、IAppUpdateService、IInternalUpdateNotifier（避免 Background→Infrastructure 跨层耦合）
- AppUpdateService：GitHub Releases API、版本比较、跳过版本持久化、流式下载+进度、PS 更新脚本、Environment.Exit(0)
- AppUpdateWorker：24h 定时检查、5min 启动延迟、IInternalUpdateNotifier 触发事件
- ShellViewModel：订阅 UpdateAvailable、HasPendingUpdate/IsNotDownloadingUpdate/CanSkipUpdate 状态
- ShellPage.xaml：InfoBar 更新通知条

### Task 7.2 — 引擎启动 + 插件管理
- Application 层：PluginSummary / CompatibilityReport DTO、IPluginReadService、IPluginCommandService
- PluginReadService：扫描已安装资产（排除 UE_ 前缀）、.uplugin JSON 元数据解析、兼容性检查
- PluginCommandService：.uproject JSON 编辑（添加/移除 Plugins 数组条目）
- PluginsViewModel：插件列表加载、兼容性检查命令、状态管理
- PluginsPage.xaml：插件卡片列表、加载/错误/空状态
- NavigationRoute + NavigationService + ShellPage 添加"插件管理"导航项
- ShellViewModel 添加 NavigateToPlugins 命令
- Infrastructure DI：PluginReadService + PluginCommandService（Singleton）
- Presentation DI：PluginsViewModel（Transient）
- 引擎启动功能已在 Task 7.1 LaunchEditorAsync 中实现

### Task 7.1 — 引擎版本管理
- Application 层：EngineVersionSummary / InstalledEngineSummary DTO、IEngineVersionReadService、IEngineVersionCommandService
- EngineVersionApiClient：Polly 3 次指数退避 + 30s 超时、Bearer 认证、snake_case JSON
- EngineVersionReadService：5 分钟缓存、远程+本地合并（UE_ 前缀过滤）
- EngineVersionCommandService：下载委托（IDownloadCommandService 高优先级 10）、卸载委托（IInstallCommandService）、编辑器启动（Process.Start）
- EngineVersionsViewModel：并发加载可用+已安装、下载/卸载/启动命令、HasError/IsEmpty/IsNotLoading 状态
- EngineVersionItemViewModel：CanDownload/HasStatus 计算属性联动
- EngineVersionsPage.xaml：标题栏+刷新、加载/错误/空状态、已安装列表（启动/卸载）、可用版本列表（已安装标签/安装按钮）
- Infrastructure DI：HttpClient("EngineVersionApi") + 3 个服务
- Presentation DI：EngineVersionsViewModel（Transient）

### Task 6.1 — Fab API 客户端
- PagedResult<T> 泛型分页容器（Shared 层）
- FabModels：FabAssetType/AssetOwnershipState/FabSortOrder 枚举、FabSearchQuery、5 个 DTO
- IFabCatalogReadService / IFabAssetCommandService 应用层契约
- FabApiClient：HTTP + Polly（3次重试+30s超时）、Bearer 认证、snake_case JSON、内部 API DTO
- FabCatalogReadService：ConcurrentDictionary 5分钟缓存、IsInstalled 丰富、DTO 映射
- FabAssetCommandService：下载信息获取 → StartDownloadRequest → 委托 IDownloadCommandService
- Infrastructure DI 注册（HttpClient + FabApiClient + 两个服务）

### Task 6.2 — Fab 资产浏览页
- IThumbnailCacheService 接口 + ThumbnailCacheService 实现（SHA-256 哈希、LRU 2000 条 + 7 天过期）
- FabLibraryViewModel：分页/无限滚动、搜索防抖 300ms、分类/排序切换、骨架屏状态
- FabAssetCardViewModel：缩略图懒加载（ElementPrepared 触发）、价格/评分格式化
- FabLibraryPage.xaml：UniformGridLayout 虚拟化网格、搜索栏+分类+排序筛选、骨架屏、空状态、无限滚动
- DI 注册：HttpClient("ThumbnailDownload") + IThumbnailCacheService + FabLibraryViewModel

### Task 6.3 — 搜索/筛选 + 详情页
- FabAssetDetailViewModel：详情加载、截图懒加载、下载按钮、返回导航
- FabAssetDetailPage.xaml：Hero 图 + 信息面板、描述、截图画廊、引擎版本、标签、技术细节
- NavigationRoute + NavigationService 注册 FabAssetDetail
- FabLibraryPage 卡片 Tapped 导航到详情页
- DI 注册 FabAssetDetailViewModel

### Task 4.1 — DownloadTask 领域实体 + 状态机
- DownloadState（13 状态）、DownloadStateMachine（17 转换）、DownloadTask 实体、ChunkInfo/DownloadCheckpoint 值对象
- 46 个单元测试

### Task 4.2 — Download Orchestrator + Scheduler + 服务层
- DownloadScheduler（并发+优先级）、DownloadOrchestrator（全流程编排）、Command/Read Service、Repository

### Task 4.3 — ChunkDownloader + HTTP Range + Polly 韧性
- ChunkDownloadClient、Polly ResiliencePipeline（重试+超时+断路器）、分块策略、原子写入

### Task 4.4 — Checkpoint 持久化 + 崩溃恢复
- Migration_005_DownloadCheckpoints、检查点 CRUD、DownloadOrchestrator 崩溃恢复逻辑

### Task 4.5 — DownloadRuntimeStore + 进度聚合
- ConcurrentDictionary 快照管理、SpeedCalculator（5s 滑动窗口、500ms 节流）、ETA 计算

### Task 4.6 — Downloads UI 页面
- IDownloadRuntimeStore 应用层接口、DownloadsViewModel/Page、ShellViewModel 下载速度状态栏

### Task 5.1 — Install Worker + Manifest
- InstallState（8 状态）、InstallStateMachine（16 转换）、InstallManifest/ManifestFileEntry
- Installation 实体、全套契约、InstallationRepository（SQLite+Manifest JSON）、InstallWorker（ZIP+Zip Slip 防护）
- InstallCommandService/InstallReadService

### Task 5.2 — Integrity Verifier + Repair
- IHashingService/HashingService（SHA-256、并行多文件）
- IIntegrityVerifier/IntegrityVerifier（两遍扫描：缺失+哈希校验）
- InstallCommandService.RepairAsync 完整实现（Manifest 加载→校验→报告→状态转换）

### Task 5.3 — Uninstaller + Installations UI
- InstallationsViewModel：Load/Verify/Repair/Uninstall 命令、InstallItemViewModel 列表项
- InstallationsPage.xaml：资产卡片列表（名称/版本/大小/路径/安装时间）、校验/修复/卸载按钮、空状态
- NavigationRoute + NavigationService + ShellPage 添加"已安装"导航项
- ShellViewModel 添加 NavigateToInstallations 命令
- Presentation DI 注册 InstallationsViewModel

## 遗留问题
- RepairAsync 目前仅检测损坏文件并记录日志，实际重新下载损坏文件需要 Downloads 模块配合（后续任务）
- "下载完自动安装"开关功能需要 DownloadCompletedEvent → InstallAsync 联动，留待后续整合
- Fab API 客户端目前无单元测试（HTTP 客户端需 mock HttpClient，留待集成测试或后续补充）

## 下一个任务的输入
- Phase 8：自动更新 + 打磨
- Task 8.1：自动更新（IAppUpdateService、4h 后台检查、下载+校验、退出时替换）
- Task 8.2：网络韧性增强（在线/离线检测、自动暂停/恢复、Retry-After、CDN 回退）
- Task 8.3：性能优化（冷启动 <2s、内存优化）
- Phase 7 引擎版本+插件管理模块已完成，可作为后续整合基础

## 关键约束提醒
- 文件名英文，内容中文（代码除外，注释中文）
- 仅在 Q:\MyEpicLauncher 目录内操作
- 项目名称为 HelsincyEpicLauncher，解决方案文件 HelsincyEpicLauncher.slnx
- 每个代码文件顶部必须有版权声明：// Copyright (c) Helsincy. All rights reserved.
- 不含游戏商店、游戏库存模块；包含 Fab 资产库模块
- Result API: Result.Ok() / Result.Fail(Error) / Result.Ok<T>(value) / Result.Fail<T>(Error)
- Error 使用 required init 属性：new Error { Code, UserMessage, TechnicalMessage, CanRetry, Severity }
- Serilog: Log.ForContext<T>() 模式，不用 ILogger
- Entity<TId>: 无构造参数，Id 用 protected setter 赋值
- ViewModelLocator.Resolve<T>() 模式
- PowerShell Set-Content 会破坏中文编码，禁止使用
- **每个原子任务完成后必须同步更新 CHANGELOG.md 和 SESSION_HANDOFF.md**
