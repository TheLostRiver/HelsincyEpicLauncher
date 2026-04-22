# 会话交接文档

## 最后更新
- 时间：2026-04-22
- 完成任务：Epic 两步式登录验证 + Auth 手动 JSON 输入止血 + Auth 自动回调预研/loopback 清单整理 + Auth 宿主自动回调骨架接入 + Auth 第二实例自动回调转发正式修复并完成运行态验收 + Legendary 参考实现分析与下一阶段 Auth 设计文档定稿 + Auth Phase L1 内部 completion 抽象与结构化日志归一 + EGL refresh token 导入预研 + WebView2 exchange code 预研、默认登录实现与风险加固 + Fab 网页端接口误接入修补 + Fab owned 回退统一到流式详情/分页链路 + WebView2 运行态 loader 缺失修正

## 当前项目状态
- 最后成功编译：是（dotnet build 成功）
- 最后测试结果：全部通过（242/242；仍有 1 条既有 `CA1816` 警告未处理）
- 当前重点：**2026-04-22 的真实运行态验收已经把 Epic 默认登录主线的三层阻塞全部收口：默认输出目录 loader 缺失、浏览器兜底 JSON 不兼容、以及 `ContentDialog` 宿主导致的人机验证页面裁切。当前代码已补 `TargetDir` loader 复制、JSON 兼容提取，并把嵌入式登录容器改为独立可调整大小的 WebView2 登录窗口。最新真实运行态已明确记录 `exchange_code_webview`、token exchange succeeded 与嵌入式登录成功，默认主线现可闭环登录。**
- 下一个任务：**如果继续沿 Auth 主线推进，重点不再是登录宿主，而是把这轮最终修复补充提交/推送、同步更新剩余文档，并根据需要继续观察 Epic 页面桥接点是否发生外部漂移。凡是继续做 `src/Launcher.App/*` 运行态验收，仍需显式执行 `dotnet build src/Launcher.App/Launcher.App.csproj`，不能只依赖 `dotnet test`。**

## 审查修复进度
- 已完成：31/71 项（43.7%），6 个 Batch，全部提交推送
- 剩余：39 项，已拆分为 10 个 Phase、40 个原子任务
- 实施计划文档：`docs/review/10-RemainingFixPlan.md`
- 审查日志：`docs/review/99-ReviewLog.md`
- 下一步执行顺序：P1 → P2 → P3|P4|P5|P7 并行 → P6|P8 → P9 → P10

## Phase 8 完整总结

### Task 8.1 — 自动更新
- IAppUpdateService、IInternalUpdateNotifier、AppUpdateService（GitHub Releases API）
- AppUpdateWorker（24h 定时检查，IInternalUpdateNotifier 触发事件）
- ShellViewModel 更新 UI（HasPendingUpdate/CanSkipUpdate）

### Task 8.2 — 网络韧性增强
- INetworkMonitor、NetworkMonitor（NetworkChange 事件）
- NetworkMonitorWorker（断联暂停/恢复续传）
- DownloadOrchestrator GetActiveTaskIdsAsync/GetPausedTaskIdsAsync
- PauseAllAsync/ResumeAllAsync on DownloadCommandService

### Task 8.3 — 性能优化
- Migration_006 DB 索引
- 后台服务异步启动，OperationTimer 计时
- BitmapImage DecodePixelWidth=220
- Page Unloaded → ViewModel.Dispose()

### Task 8.4 — UI 打磨 + 错误闭环
- FabLibraryViewModel：HasError + IsOffline + INetworkMonitor
- FabLibraryPage：离线 InfoBar + 错误 InfoBar（含重试）
- FabAssetDetailPage：错误面板添加重试按钮
- InstallationsViewModel：HasError/ErrorMessage + try/catch
- InstallationsPage：错误 InfoBar（含重试）
- NavigationService：EntranceNavigationTransitionInfo 页面过渡动画

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
- FabLibrary 当前使用的 `https://www.fab.com/api/v1/assets/*` 不是稳定客户端服务接口；运行时会收到 Cloudflare `Just a moment...` 挑战页。当前已修正错误提示，但在线目录本身仍需后续迁移到 Epic 后端服务链路
- EngineVersions 当前 `https://www.unrealengine.com/api/engine/versions` 同样属于网页端入口；已补网站挑战识别。另已验证 `launcher-public-service-prod06.ol.epicgames.com/launcher/api/public/assets/Windows?label=Live` 可被当前 token 正常访问，可作为后续迁移基础
- 当前环境下 `Launcher.App` 窗口未暴露可用的 UIA 顶级窗口/控件树，无法自动化点击详情卡片；已改为使用本机真实登录态直接调用 `IFabCatalogReadService` 完成一次 owned 搜索→详情读取，证明详情回退后端链路可用，但 UI 视觉层仍需人工点开做最后验收
- RepairAsync 目前仅检测损坏文件并记录日志，实际重新下载损坏文件需要 Downloads 模块配合（后续任务）
- "下载完自动安装"开关功能需要 DownloadCompletedEvent → InstallAsync 联动，留待后续整合
- FabApiClient 已有单元测试；本轮新增了 Cloudflare challenge 场景，避免把网站防护拦截误判为普通 403

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
