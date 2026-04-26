# Changelog

## [Unreleased]

### Task 9.8 - Fab 热恢复 S2-D 成功路径写回快照 (2026-04-26)
- 更新 [src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)，在列表查询成功路径后统一写回会话快照
- `FabAssetCardViewModel` 新增导出 `FabAssetSummary` 的能力，保证当前列表可以被稳定保存到 `FabLibrarySessionSnapshot`
- 至此 `S2` 列表恢复与写回切片已完整闭环：依赖接入、卡片恢复辅助方法、`LoadAsync` 恢复入口、成功路径写回快照均已落地
- 已执行 `dotnet build src/Launcher.Presentation/Launcher.Presentation.csproj --no-restore`，构建通过

### Task 9.7 - Fab 热恢复 S2-C 首次加载恢复入口 (2026-04-26)
- 更新 [src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)，在 `LoadAsync` 中优先尝试恢复会话快照
- 当前恢复成功时会先展示快照内容，并跳过本轮首页资产查询；分类加载仍照常执行
- 本轮仍未引入快照写回逻辑与 SWR 后台刷新策略，相关行为继续留给后续 `S2-D` 与 `S4`
- 已执行 `dotnet build src/Launcher.Presentation/Launcher.Presentation.csproj --no-restore`，构建通过

### Task 9.6 - Fab 热恢复 S2-B 快照卡片恢复辅助方法 (2026-04-26)
- 更新 [src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)，新增从 `FabLibrarySessionSnapshot` 恢复卡片列表与分页状态的辅助方法
- 当前恢复逻辑已覆盖 `Assets`、`CurrentPage`、`TotalPages`、`HasNextPage`、`TotalCount`、`HasAssets`、`IsEmpty` 等页面状态字段
- 本轮仍未将恢复入口接入 `LoadAsync`，调用时机继续留给后续 `S2-C`
- 已执行 `dotnet build src/Launcher.Presentation/Launcher.Presentation.csproj --no-restore`，构建通过

### Task 9.5 - Fab 热恢复 S2-A ViewModel 注入 Store (2026-04-26)
- 更新 [src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)，注入 `IFabLibrarySessionStateStore`，并补充 `_isRestoredFromSnapshot`、`_forceNetworkReload` 两个恢复守卫字段及内部只读观察属性
- 为匹配 `FabLibraryViewModel` 的公开构造函数依赖链，更新 [src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs](src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs) 与 [src/Launcher.Presentation/Modules/FabLibrary/IFabLibrarySessionStateStore.cs](src/Launcher.Presentation/Modules/FabLibrary/IFabLibrarySessionStateStore.cs) 为 `public`；它们仍属于 Presentation 层内部实现面，不升级为 Application `Contracts`
- 当前切片只完成依赖接入与守卫位准备，不包含实际的快照恢复或写回行为
- 已执行 `dotnet build src/Launcher.Presentation/Launcher.Presentation.csproj --no-restore`，构建通过

### Task 9.4 - Fab 热恢复 S1-D 会话 Store DI 注册 (2026-04-26)
- 更新 [src/Launcher.Presentation/DependencyInjection.cs](src/Launcher.Presentation/DependencyInjection.cs)，将 `IFabLibrarySessionStateStore` 注册为 `Singleton`，默认实现为 `InMemoryFabLibrarySessionStateStore`
- 保持 `FabLibraryViewModel` 继续为 `Transient`，避免把热恢复方案错误演化为页面级全局单例
- 至此 `S1` 会话快照骨架切片已经完整闭环：快照 DTO、Store 接口、内存实现、DI 注册全部落地
- 已执行 `dotnet build src/Launcher.Presentation/Launcher.Presentation.csproj --no-restore`，构建通过

### Task 9.3 - Fab 热恢复 S1-C 内存会话 Store (2026-04-26)
- 新增 [src/Launcher.Presentation/Modules/FabLibrary/InMemoryFabLibrarySessionStateStore.cs](src/Launcher.Presentation/Modules/FabLibrary/InMemoryFabLibrarySessionStateStore.cs)，为 Fab 列表页热恢复提供进程内单槽位的会话快照存储实现
- 当前实现使用单槽位快照字段加 `lock` 互斥保护，先完成 `Save`、`TryGet`、`Clear`、`Trim` 的最小闭环，不引入多快照、磁盘持久化或容量裁剪策略
- `Trim()` 当前保留为后续扩展点，尚未引入分页上限或账号作用域裁剪逻辑
- 已执行 `dotnet build src/Launcher.Presentation/Launcher.Presentation.csproj --no-restore`，构建通过

### Task 9.2 - Fab 热恢复 S1-B 会话 Store 接口 (2026-04-26)
- 新增 [src/Launcher.Presentation/Modules/FabLibrary/IFabLibrarySessionStateStore.cs](src/Launcher.Presentation/Modules/FabLibrary/IFabLibrarySessionStateStore.cs)，为 Fab 列表页热恢复建立 Presentation 内部的会话快照存储接口
- 当前接口保持最小职责面，只暴露 `Save`、`TryGet`、`Clear`、`Trim` 四个方法，避免演化为通用缓存中心
- 该接口仍停留在 Presentation 层内部，未升级为跨模块 `Contracts`
- 已执行 `dotnet build src/Launcher.Presentation/Launcher.Presentation.csproj --no-restore`，构建通过

### Task 9.1 - Fab 热恢复 S1-A 会话快照 DTO (2026-04-26)
- 新增 [src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs](src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs)，为 Fab 列表页热恢复建立首个内部会话快照 DTO
- 当前快照字段已覆盖查询态、分页态、滚动位置、时间戳、账号作用域，以及用于重建卡片列表的 `FabAssetSummary` 集合
- 该 DTO 仅承载可恢复 UI 的窄状态，不持有 `FabAssetCardViewModel`、`BitmapImage` 或其他可变 UI 对象
- 已执行 `dotnet build src/Launcher.Presentation/Launcher.Presentation.csproj --no-restore`，构建通过

### Fab 列表页热恢复实施文档 (2026-04-26)
- 新增 [docs/review/19-FabLibraryWarmResumeImplementationSlices.md](docs/review/19-FabLibraryWarmResumeImplementationSlices.md)，把 Fab 列表页热恢复、SWR、启动预热方案拆成可逐步执行的细粒度原子任务，并为每个子切片补齐目标文件、完成标准、验证动作和禁止事项
- 更新 [docs/review/18-FabLibraryWarmResumeStrategy.md](docs/review/18-FabLibraryWarmResumeStrategy.md)，明确其定位为策略文档，并把“首期不依赖页面实例常驻”的实现边界写清，避免后续误把页面缓存当成前置条件
- 更新 [docs/06-ModuleDefinitions/FabLibrary.md](docs/06-ModuleDefinitions/FabLibrary.md)，补充 Fab 列表页热恢复文档入口，便于后续从模块文档直接恢复上下文
- 本轮仅完成文档与交接入口整理，未执行 `dotnet build` 或 `dotnet test`

### Fab 预览追踪锚点保留 (2026-04-22)
- `FabAssetSummary` 新增 `PreviewListingId` / `PreviewProductId`，用于为后续二级 preview resolver 保留非 UI 预览追踪元数据，不再要求重新打穿 owned fallback 链路
- `EpicOwnedFabCatalogClient` 现在会从 Epic library owned record 保留 `productId`，并从 catalog `customAttributes` 中大小写不敏感地提取 `ListingIdentifier`；即使 `keyImages=[]`，无图资产也会把后续预览解析所需锚点继续带到列表层
- `Launcher.Infrastructure.FabLibrary` 新增 `IFabPreviewMetadataResolver` 抽象与默认 `NullFabPreviewMetadataResolver`；owned fallback 现在只在“无 thumbnail 且存在 preview 锚点”时尝试二级解析，后续只需替换 resolver 实现即可恢复真实预览图来源
- Fab 列表卡片占位文案细分为“暂无预览”和“平台未返回预览”，避免把“图片下载失败”与“上游当前没给图”混为一谈
- 新增 `EpicOwnedFabCatalogClientTests.SearchOwnedAsync_WhenCatalogOmitsImages_ShouldPreservePreviewTraceMetadata` 与 `SearchOwnedAsync_WhenResolverReturnsPreview_ShouldUseResolvedThumbnailUrl`，回归验证空图 owned fallback 资产既能保留 `ListingIdentifier/productId`，也能在后续接入 resolver 后无缝吃到解析出的预览图
- 真实预览恢复主链已改为“卡片懒加载时按需解析”，不再在 owned 摘要加载阶段阻塞首屏：`IFabPreviewUrlReadService` 负责对可见卡片按需取图，Presentation 新增 `IFabListingPageReadService` 的 WebView2 实现，在受控浏览器上下文中读取 Fab listing HTML；Infrastructure 通过 `FabListingHtmlPreviewMetadataResolver` 从页面内容提取 `media.fab.com/image_previews/...` 并缓存结果
- `EpicOwnedFabCatalogClient` 已撤回摘要阶段的真实预览解析尝试，避免首屏 20 个无图资产逐个走浏览器抓页导致列表初始化显著变慢；真实预览解析现在只在卡片进入视口、且原始 `ThumbnailUrl` 为空时才触发
- 新增 `FabPreviewUrlReadServiceTests`，验证 raw / escaped 两类 listing HTML 都能提取真实 `image_previews` URL，并验证按 listing 级缓存生效
- 真实运行态验收确认：隐藏 WebView2 probe 不能使用 `1x1` 极小视口；`FabListingPageReadService` 现改为离屏但保持 `1366x900` 桌面级视口，否则 Fab listing 会进入过度收缩/延迟分支，导致页面 HTML 长时间拿不到 `image_previews` 预览信号
- `FabListingHtmlPreviewMetadataResolver` 现只缓存成功解析出的 preview URL，不再对 `null/empty` 结果做负缓存；这样 listing 首次未水合时，后续可见卡片再次进入视口仍能重试并命中真实预览，而不会被第一次空结果永久压住
- `FabPreviewUrlReadServiceTests` 追加覆盖 `ListingIdentifier` 缺失时回退 `productId`，以及“首次空结果、再次重试成功”的场景，避免运行态回归成只要第一次没拿到图就长期显示“平台未返回预览”
- `FabLibraryPage` 状态提示区已从资产网格层中拆出：离线提示和错误提示现在统一放进独立的状态区，不再与 `ScrollViewer` / 资产卡片共享同一 `Grid.Row`，修复“读取已拥有 Fab 资产失败”错误条覆盖卡片内容的 UI bug
- `FabLibraryViewModel` 现区分阻断与非阻断搜索失败：首屏空态失败仍显示页面级 Error InfoBar；若列表已有卡片、或仅是翻页追加失败，则改为通过 `INotificationService` 显示非阻断 Warning toast，避免把已有内容错误升级成整页失败态

### Auth 运行态验收修正 (2026-04-22)
- 真实运行态验收确认：默认启动目录 `src/Launcher.App/bin/Debug/net9.0-windows10.0.19041.0` 缺少根层 `WebView2Loader.dll`，导致 WebView2 在 `CoreWebView2Environment.CreateWithOptionsAsync(...)` 阶段抛出“找不到指定的模块”，Shell 随后自动回退到系统浏览器登录兜底
- `src/Launcher.App/Launcher.App.csproj` 现显式引用 `Microsoft.Web.WebView2` 并在 Build / Publish 后把 `runtimes/win-x64/native/WebView2Loader.dll` 复制到真实应用基目录 `TargetDir`，避免默认构建输出只能依赖 `win-x64` 子目录才能初始化 WebView2
- 真实运行态还确认了另一处行为偏差：系统浏览器兜底链路的 Epic 页面实际会返回 JSON 响应，而应用此前只接受纯 `authorizationCode` / 回调 URL，导致用户即使完成网页登录也会因为拿到整段 JSON 而卡住；当前 Auth 已恢复从 JSON 中只提取 `authorizationCode` / `redirectUrl` 的能力，并同步更新高级登录入口提示
- 重新启用嵌入式登录后，运行态又暴露出 WinUI 3 `ContentDialog` 宿主对 Arkose/人机验证页面仍然过于局促；最终修复不再继续挤压 `ContentDialog`，而是把 Epic 登录改为独立可调整大小的窗口，并允许用户直接最大化后完成验证
- 最新真实运行态已确认默认主线真正闭环：独立 WebView2 登录窗口能够完整显示 Epic 登录页和人机验证，`DialogService` 成功捕获 `exchange_code`，随后 `ExchangeCodeGrantExecutor` token exchange succeeded，应用进入已登录状态

### Auth/Fab 运行时接入修正 (2026-04-17)
- 确认 Epic 登录已可建立本地登录态；FabLibrary 失败的根因不是未登录，而是当前 `https://www.fab.com/api/v1/assets/*` 接入命中了 Cloudflare `Just a moment...` 挑战页
- `FabApiClient`：新增网站挑战页识别；命中时返回 `FAB_BROWSER_CHALLENGE_BLOCKED`，用户提示明确为“当前客户端仍在访问网页端受保护入口”，不再误报普通 403
- `EngineVersionApiClient`：对同类网站挑战页做相同识别，返回 `ENGINE_BROWSER_CHALLENGE_BLOCKED`，避免继续把站点拦截误判为普通接口失败
- 错误日志统一改为记录脱敏后的响应体，便于区分业务错误和站点防护拦截
- `FabApiClientTests` 新增 Cloudflare challenge 场景，防止后续回退成普通 403 提示
- `FabCatalogReadService` 接入 `EpicOwnedFabCatalogClient` 已拥有资产回退；Fab 首页在网页端 API 被拦截时，改走 Epic library + catalog 后端
- `EpicOwnedFabCatalogClient` 进一步收敛为同一套 requirement-driven 流式回退：Fab 首页、后续翻页、详情页都共享同一条 owned 记录扩展链路，不再把详情/翻页走独立的全量路径
- 最新运行时日志已验证 Fab 回退分页可按需扩展到 page 2~5；新增单测覆盖“详情 challenge 回退到 owned fallback”与“后续页按需扩展预览窗口”两条回归场景
- 额外完成一次真实会话链路验证：在当前机器已恢复的 Epic 登录态下，直接通过 `IFabCatalogReadService` 成功执行 owned 搜索→详情读取，实测拿到资产 `3e9c264b685f43edabb1bcb000a2330d`（`Modular Scifi Season 2 Starter Bundle`）的详情数据
- Auth Phase A 止血：主登录按钮不再直接要求用户粘贴整段 JSON；Shell 改为先发起浏览器登录，再通过显式“继续登录（导入授权码）”次级动作完成登录
- `EpicOAuthProtocol` 不再接受完整 JSON 作为默认人工输入，只接受纯 `authorizationCode` 或完整回调 URL，减少把额外敏感字段暴露给应用的风险
- Auth 自动回调预研已完成：当前宿主未提供协议激活能力，现成自动回调实现仍只有 loopback；已补一份对外确认用的 loopback 验证清单，后续优先争取 Epic 提供精确可用的 `redirect_uri`
- `Launcher.App` 已补宿主层自动回调骨架：若应用后续从启动参数或第二实例转发中收到完整回调 URL 候选负载，会自动调用现有 Auth 完成登录，为未来 loopback/协议回调接入提前打通应用内部链路
- 已新增一份可直接外发的 Auth loopback 确认模板，覆盖精确 `redirect_uri`、authorize 入口形式和 token exchange 参数矩阵的询问项，减少后续对外沟通往返成本
- 手动授权码导入入口继续降级为低显著度高级链接，并在打开输入框前增加一次确认，进一步避免把兜底路径误当成默认登录方式
- Auth 宿主自动回调链路已完成正式修复并通过运行态验收：单实例判定前移到 `Program.Main`，第二实例转发改为直接使用真实进程入口解析参数，并补上原始命令行兜底；显式构建 `Launcher.App.csproj` 后，已实测主实例能够收到第二实例转发的回调 URL 候选负载并自动进入现有 `CompleteAuthorizationCodeLoginAsync(...)` 链路
- 本轮还确认了一个运行态验收注意点：`dotnet test` 不会刷新 `Launcher.App.exe`，凡是改动 `src/Launcher.App/*` 后要验证真实宿主行为，必须额外执行一次 `dotnet build src/Launcher.App/Launcher.App.csproj`
- Auth Phase L1 已落地：在 `Launcher.Infrastructure.Auth` 内部新增 `EpicLoginResult` 归一化模型、`IEpicLoginGrantExecutor` 与 `AuthorizationCodeGrantExecutor`，把“手工 authorization code / 回调 URL / loopback callback”统一收敛到同一条 `authorization_code` 执行链路，同时保持 `IAuthService` 公共契约不变
- Auth 日志已按“输入类型 / 来源 / grant / 结果”归一：登录结果会先记录 `Kind`、`Source`、`IncludeTokenType` 与是否带 `RedirectUri`，随后在 token exchange 阶段输出结构化的 started / failed / succeeded 事件；新增单测覆盖归一化与 grant 执行器，`dotnet test HelsincyEpicLauncher.slnx --no-restore` 当前为 230/230 通过（仍有 1 条既有 `CA1816` 警告，未在本轮处理）
- EGL refresh token 导入预研已完成：Legendary 的 `--import` 本质上是从 EGL `GameUserSettings.ini` 的 `[RememberMe]` / `Data` 解析 refresh token，再走标准 `grant_type=refresh_token` 建会话；这条路与当前 Auth Phase L1 架构兼容，但必须作为高级入口，并先处理“可能把 EGL 登出”和“不能直接复制 GPL 解密实现”两项风险
- WebView2 exchange code 预研已完成：Legendary 的嵌入式登录只是可选增强能力，本质上通过页面桥接拿 `exchange_code` 后走标准 `grant_type=exchange_code`；当前仓库没有现成 WebView2 基建，因此若要推进，应先做 WinUI 3 最小 POC，而不是直接扩大主线实现面
- 默认 Epic 登录路径已正式切到“嵌入式 WebView2 + exchange_code 自动完成”：Auth 公共契约新增 `StartExchangeCodeLoginAsync()` 与 `CompleteLoginAsync(AuthLoginCompletionInput)`，Infrastructure 新增 `ExchangeCodeGrantExecutor`，Shell 通过嵌入式登录对话框承载 Epic 登录页并在失败时自动回退到系统浏览器兜底；当前 `dotnet test HelsincyEpicLauncher.slnx --no-restore` 为 232/232 通过，`dotnet build src/Launcher.App/Launcher.App.csproj --no-restore` 通过，但真实 Epic 运行态仍待人工验收
- WebView2 默认登录主线已完成第一轮风险加固：嵌入式登录现在只接受受信任 Epic HTTPS 页面发出的桥接消息，拒绝非 Epic 外链，使用独立 WebView2 用户数据目录并尽力清理浏览数据，同时把取消关闭动作调回 UI 线程执行；新增桥接单测后，`dotnet test HelsincyEpicLauncher.slnx --no-restore` 当前为 242/242 通过，`dotnet build src/Launcher.App/Launcher.App.csproj --no-restore` 继续通过

### Task 8.4 - UI 打磨 + 错误闭环 (2026-04-16)
- FabLibraryViewModel：新增 HasError/ErrorMessage（搜索/加载失败时设置）、IsOffline（注入 INetworkMonitor 实时跟踪网络状态）；Dispose 时解除 NetworkStatusChanged 订阅
- FabLibraryPage.xaml：新增离线提示 InfoBar（IsOffline=true 时显示）+ 错误 InfoBar（HasError=true 时显示，含重试按钮）
- FabAssetDetailPage.xaml：错误面板新增「重试」按钮（CommandParameter 绑定 AssetId，触发 LoadCommand 重新加载）
- InstallationsViewModel：新增 HasError/ErrorMessage；LoadAsync 改为 try/catch，失败时设置 HasError
- InstallationsPage.xaml：新增错误 InfoBar（HasError=true 时显示，含重试按钮）
- NavigationService：Frame.Navigate 改用 EntranceNavigationTransitionInfo，页面切换有入场过渡动画
- 错误闭环覆盖：FabLibrary / FabAssetDetail / Installations 全部有 Error→提示+重试 路径
- 离线状态覆盖：FabLibraryPage 在无网络时显示 Warning InfoBar
- dotnet build 9 项目零错误零警告，dotnet test 176/176 通过

### Task 8.3 - 性能优化 (2026-04-16)
- 冷启动优化：后台服务（TokenRefresh/AutoInstall/AppUpdateWorker/NetworkMonitorWorker）移至窗口激活后 Task.Run 异步启动，不再阻塞主窗口显示；App.xaml.cs 添加冷启动全链路耗时日志（OperationTimer）
- DB 索引优化（Migration_006）：新增 `idx_downloads_asset_id`（优化 GetByAssetIdAsync 频繁查询）+ `idx_chunk_checkpoints_task_id`（优化断点续传查询）
- 缩略图内存优化：BitmapImage 设置 `DecodePixelWidth=220 / DecodePixelType=Logical`，GPU 无需缩放原图，内存占用降低约 80%（220px 卡片宽度约束）
- ViewModel 内存泄漏修复：FabLibraryPage / DownloadsPage 添加 `Unloaded` 事件调用 `ViewModel.Dispose()`，解除对 Singleton 服务事件的订阅，防止页面切换时 ViewModel 无法被 GC
- 虚拟化验证：ItemsRepeater + UniformGridLayout 已正确接入 ScrollViewer 视口，虚拟化生效确认
- dotnet build 9 项目零错误零警告，dotnet test 176/176 通过

### Task 8.2 - 网络韧性增强 (2026-04-16)
- Application 层契约：INetworkMonitor（IsNetworkAvailable 属性 + NetworkStatusChanged 事件）、IDownloadCommandService 新增 PauseAllAsync/ResumeAllAsync 方法
- Infrastructure：NetworkMonitor（System.Net.NetworkInformation.NetworkChange 事件驱动，Singleton + IDisposable，DI 注册 NetworkMonitor + INetworkMonitor）
- DownloadOrchestrator 新增 GetActiveTaskIdsAsync（过滤非 Paused 活跃任务）+ GetPausedTaskIdsAsync（仅 Paused 状态任务）
- DownloadCommandService 实现 PauseAllAsync（批量暂停活跃任务）+ ResumeAllAsync（批量恢复已暂停任务），批量失败逐个警告日志
- Background：NetworkMonitorWorker（订阅 INetworkMonitor.NetworkStatusChanged → 断联暂停下载、恢复续传，async void 事件回调内部 try/catch），DI 注册
- App.xaml.cs 启动时调用 NetworkMonitorWorker.Start()
- ShellViewModel：注入 INetworkMonitor，构造时同步初始状态，订阅 NetworkStatusChanged 在 DispatcherQueue 更新 IsNetworkAvailable，状态栏实时显示网络状态
- 遵循 AI-03（Background 不引用 Infrastructure，仅依赖 Application 契约）
- dotnet build 9 项目零错误零警告，dotnet test 176/176 通过

### Task 8.1 - 自动更新 (2026-04-16)
- Application 层契约：UpdateInfo DTO（Version/DownloadUrl/DownloadSize/ReleaseNotes/ReleaseDate/IsMandatory）、UpdateAvailableEvent record、IAppUpdateService（CheckForUpdate/DownloadUpdate/ApplyUpdate/SkipVersion）、IInternalUpdateNotifier（避免 Background→Infrastructure 跨层耦合）
- AppUpdateService：GitHub Releases API 检查最新版本（Bearer + User-Agent + snake_case JSON）、版本比较、跳过版本本地持久化（skipped_versions.json）、下载包流式写入（带进度）、PowerShell 更新脚本生成（等待进程退出+解压+重启）、Environment.Exit(0) 退出（不引用 WinUI API）
- AppUpdateWorker：24h 定时检查、5min 启动延迟、async void 回调内部 try/catch、通过 IInternalUpdateNotifier 触发事件（零跨层耦合）
- ShellViewModel：订阅 IAppUpdateService.UpdateAvailable 事件（仅依赖 Application 契约）、HasPendingUpdate/PendingUpdateVersion/IsNotDownloadingUpdate/CanSkipUpdate 计算属性、DownloadAndApplyUpdateCommand/SkipCurrentUpdateCommand
- ShellPage.xaml：InfoBar 更新通知条（立即更新 + 跳过此版本按钮），强制更新时隐藏跳过按钮
- DI 注册：Infrastructure（UpdateApi HttpClient + AppUpdateService Singleton + IInternalUpdateNotifier）、Background（AppUpdateWorker Singleton）
- App.xaml.cs 启动时调用 AppUpdateWorker.Start()
- 遵循 AI-01（单模块原则）、AI-03（不跨模块加依赖）、AI-05（声明影响面）
- dotnet build 9 项目零错误零警告，dotnet test 176/176 通过

### 遗留问题修复 (2026-04-15)
- RepairAsync 完整实现：IRepairDownloadUrlProvider 获取新鲜 CDN URL → RepairFileDownloader 下载资产包 → 仅解压损坏文件 → SHA-256 校验 → 原子替换 → 二次校验 → 失败回退 NeedsRepair
- RepairDownloadUrlProvider：依赖倒置，接口在 Installations.Contracts，实现在 Infrastructure（调用 FabApiClient），零循环依赖
- RepairFileDownloader：HTTP 下载 + ZIP 局部解压 + Zip Slip 防护 + 哈希校验 + 原子文件替换 + 临时文件清理
- InstallManifest 新增可选 DownloadUrl 字段（参考/诊断用途）
- InstallWorker.ExecuteAsync 接受可选 downloadUrl 参数并保存到 Manifest
- InstallStateMachine 新增 Installed→Repairing 和 Repairing→NeedsRepair 转换
- AutoInstallWorker：监听 DownloadCompletedEvent → 检查 AutoInstall 设置 → 自动调用 InstallAsync，纯事件驱动零耦合
- Background DI 注册 AutoInstallWorker，App.xaml.cs 启动
- FabApiClientTests（8 个测试）：MockHttpMessageHandler + NSubstitute mock IAuthService
- RepairAsyncTests（6 个测试）：覆盖全部修复场景
- AutoInstallWorkerTests（3 个测试）：开关开启/关闭/安装失败不抛异常
- InternalsVisibleTo("Launcher.Tests.Unit") 添加到 Infrastructure
- dotnet build 零错误零警告，dotnet test 169/169 通过

### Task 7.2 - 引擎启动 + 插件管理 (2026-04-13)
- Application 层契约：PluginSummary / CompatibilityReport DTO、IPluginReadService（已安装插件查询/兼容性检查）、IPluginCommandService（添加/移除项目插件）
- PluginReadService：扫描已安装资产（排除 UE_ 前缀）、解析 .uplugin JSON 元数据（FriendlyName/VersionName/CreatedBy/EngineVersion）、兼容性检查（SupportedEngineVersions 匹配）
- PluginCommandService：.uproject JSON 文件编辑（JsonNode/JsonArray API）、添加/移除 Plugins 数组条目、FindUprojectFile/FindPluginEntry 工具方法
- PluginsViewModel：ObservableCollection<PluginItemViewModel> 插件列表、Load/CheckCompatibility 命令、IsLoading/HasPlugins/IsEmpty/HasError 状态管理
- PluginItemViewModel：PluginId/Name/Version/Author/InstallPath/SupportedVersionsText/CompatibilityText/IsCompatible 属性
- PluginsPage.xaml：标题栏+刷新按钮、ProgressRing 加载、InfoBar 错误提示、ItemsRepeater 插件卡片（图标/名称/版本/作者/路径/支持版本/兼容性）、空状态
- NavigationRoute 添加 Plugins 路由常量 + NavigationService RouteMap 注册
- ShellPage.xaml 添加"插件管理"导航项（FontIcon &#xEA86;）
- ShellViewModel 添加 NavigateToPlugins 命令
- Infrastructure DI：IPluginReadService → PluginReadService（Singleton）、IPluginCommandService → PluginCommandService（Singleton）
- Presentation DI：PluginsViewModel（Transient）
- 引擎启动功能已在 Task 7.1 EngineVersionCommandService.LaunchEditorAsync 中实现
- dotnet build 9 项目零错误零警告，dotnet test 158/158 通过

### Task 7.1 - 引擎版本管理 (2026-04-13)
- Application 层契约：EngineVersionSummary / InstalledEngineSummary DTO、IEngineVersionReadService（可用+已安装查询）、IEngineVersionCommandService（下载安装/卸载/启动编辑器）
- EngineVersionApiClient：Polly 重试 3 次指数退避 + 30s 超时、Bearer 认证、snake_case JSON 反序列化、内部 DTO
- EngineVersionReadService：5 分钟简单缓存、远程+本地合并（UE_ 前缀过滤）、IsInstalled 字段增强
- EngineVersionCommandService：下载委托 IDownloadCommandService（高优先级 10）、卸载委托 IInstallCommandService、启动编辑器 Process.Start
- EngineVersionsViewModel：并发加载可用+已安装列表、下载/卸载/启动 RelayCommand、错误显示、空状态
- EngineVersionItemViewModel：HasStatus / CanDownload 计算属性、FormatSize 工具、发布日期格式化
- InstalledEngineItemViewModel：安装路径、磁盘占用、安装时间展示
- EngineVersionsPage.xaml：标题栏+刷新按钮、ProgressRing 加载指示器、InfoBar 错误提示、已安装版本列表（启动/卸载按钮）、可用版本列表（已安装标签/安装按钮）、空状态
- Infrastructure DI：HttpClient("EngineVersionApi") + EngineVersionApiClient + 读写服务
- Presentation DI：EngineVersionsViewModel（Transient）
- dotnet build 9 项目零错误零警告，dotnet test 158/158 通过

### Task 6.3 - 搜索/筛选 + 详情页 (2026-04-13)
- FabAssetDetailViewModel：资产详情加载、截图懒加载、下载按钮、返回导航、FormatSize 工具方法
- ScreenshotItem：截图项 ObservableObject（URL + BitmapImage 懒加载）
- FabAssetDetailPage.xaml：Hero 图 + 右侧信息面板（标题/作者/评分/价格/版本/大小/更新日期）、下载按钮（AccentButton）、描述区、截图横向画廊（ItemsRepeater 水平布局）、兼容引擎版本标签列表、标签胶囊、技术细节区
- FabAssetDetailPage.xaml.cs：OnNavigatedTo 接收 assetId 导航参数、ViewModelLocator 模式
- NavigationRoute 添加 FabAssetDetail 路由常量
- NavigationService RouteMap 注册 FabAssetDetailPage
- FabLibraryPage 卡片 Tapped 事件导航到详情页（通过 Tag 传递 AssetId）
- Presentation DI 注册 FabAssetDetailViewModel（Transient）
- dotnet build 9 个项目零错误零警告，dotnet test 158/158 通过

### Task 6.2 - Fab 资产浏览页 (2026-04-13)
- IThumbnailCacheService 接口（Application 层契约：URL → 本地缓存路径，LRU 淘汰）
- ThumbnailCacheService 实现：SHA-256 URL 哈希文件名、ConcurrentDictionary 内存索引、并发下载锁防重复、7 天过期 + 2000 条 LRU 淘汰、原子临时文件写入
- FabLibraryViewModel：分页/无限滚动、搜索防抖（300ms）、分类/排序切换、骨架屏状态管理
- FabAssetCardViewModel：缩略图懒加载（可视区域触发）、价格/评分格式化、已拥有标记
- FabLibraryPage.xaml：UniformGridLayout 虚拟化网格（ItemsRepeater）、搜索栏 + 分类 ComboBox + 排序 ComboBox、骨架屏加载状态（8 占位卡片）、空状态提示、无限滚动加载更多指示器
- FabLibraryPage.xaml.cs：ViewModelLocator 模式、搜索/筛选/排序事件路由、ScrollViewer 无限滚动检测、ElementPrepared 缩略图懒加载触发
- Infrastructure DI 注册 HttpClient("ThumbnailDownload") + IThumbnailCacheService
- Presentation DI 注册 FabLibraryViewModel（Transient）
- dotnet build 9 个项目零错误零警告，dotnet test 158/158 通过

### Task 6.1 - Fab API Client (2026-04-13)
- PagedResult<T> 泛型分页容器（Shared 层）
- FabModels：FabAssetType/AssetOwnershipState/FabSortOrder 枚举、FabSearchQuery 查询对象、FabAssetSummary/FabAssetDetail/AssetCategoryInfo/FabDownloadInfo DTO
- IFabCatalogReadService 接口（SearchAsync/GetDetailAsync/GetOwnedAssetsAsync/GetCategoriesAsync）
- IFabAssetCommandService 接口（DownloadAssetAsync/RefreshCacheAsync）
- FabApiClient：HTTP 客户端 + Polly ResiliencePipeline（3 次指数退避重试 + 30s 超时）、Bearer 认证注入、snake_case JSON 反序列化、内部 API DTO 映射
- FabCatalogReadService：ConcurrentDictionary 5 分钟内存缓存、IsInstalled 状态丰富、DTO → 领域模型映射
- FabAssetCommandService：获取下载信息 → 构建 StartDownloadRequest → 委托 IDownloadCommandService
- Infrastructure DI 注册：HttpClient("FabApi") + FabApiClient(Singleton) + IFabCatalogReadService + IFabAssetCommandService
- dotnet build 9 个项目零错误零警告，dotnet test 158/158 通过

### Task 5.3 - Uninstaller + Installations UI (2026-04-13)
- InstallationsViewModel：已安装资产列表管理（Load/Verify/Repair/Uninstall 命令）
- InstallItemViewModel：ObservableObject 列表项（State/NeedsRepair/StatusText/IsVerifying 响应式属性）
- InstallationsPage.xaml：已安装资产卡片列表（名称/版本/大小/安装时间/路径）、校验/修复/卸载按钮、空状态提示
- InstallationsPage.xaml.cs：ViewModelLocator 模式 + 按钮事件路由 + x:Bind 辅助方法
- NavigationRoute 添加 Installations 路由常量
- NavigationService RouteMap 注册 InstallationsPage
- ShellPage NavigationView 添加"已安装"导航项（E8B7 图标）
- ShellViewModel 添加 NavigateToInstallations 命令
- Presentation DI 注册 InstallationsViewModel（Transient）
- dotnet build 9 个项目零错误零警告，dotnet test 158/158 通过

### Task 5.2 - Integrity Verifier + Repair (2026-04-13)
- IHashingService 接口（ComputeHashAsync 单文件 + ComputeHashesAsync 并行多文件）
- HashingService 实现：SHA-256、FileStream 81920 缓冲区、SemaphoreSlim 并行控制、IProgress 报告
- IIntegrityVerifier 接口（VerifyFileAsync 单文件 + VerifyInstallationAsync 整体校验）
- IntegrityVerifier 实现：两遍扫描（缺失文件检查 + 并行哈希校验，MaxParallelism=4）
- InstallCommandService.RepairAsync 完整实现：加载 Manifest → 完整性校验 → 报告损坏文件 → 状态转换
- Infrastructure DI 注册 IHashingService → HashingService、IIntegrityVerifier → IntegrityVerifier
- HashingServiceTests（8 个测试）+ IntegrityVerifierTests（8 个测试）
- dotnet build 9 个项目零错误零警告，dotnet test 158/158 通过

### Task 5.1 - Install Worker + Manifest (2026-04-13)
- InstallState 枚举（8 状态：NotInstalled/Installing/Installed/Verifying/NeedsRepair/Repairing/Uninstalling/Failed）
- InstallStateMachine（16 条状态转换规则）
- InstallManifest + ManifestFileEntry（资产清单 + 文件条目：RelativePath/Size/Hash）
- Installation 领域实体（Entity<string>，封装状态机 + 属性更新）
- IInstallCommandService / IInstallReadService / IInstallationRepository / IIntegrityVerifier 应用层契约
- InstallModels：InstallRequest / InstallStatusSummary / VerificationReport / VerificationProgress + 4 事件
- InstallationRepository：SQLite+Dapper CRUD + Manifest JSON 文件持久化（ManifestJsonContext AOT 安全）
- InstallWorker：ZIP 解压（Zip Slip 防护）/ 单文件复制 / SHA-256 哈希 / 磁盘空间检查（×2.5）/ 进度报告
- InstallCommandService：InstallAsync/UninstallAsync/RepairAsync 编排
- InstallReadService：Installation → InstallStatusSummary 映射
- Infrastructure DI 注册 4 个安装模块服务
- InstallStateMachineTests（27 个测试）+ InstallationTests（11 个测试）
- dotnet build 9 个项目零错误零警告，dotnet test 142/142 通过

### Task 4.6 - Downloads UI 页面 (2026-04-13)
- IDownloadRuntimeStore 应用层接口（解耦 Presentation 与 Infrastructure）
- DownloadsViewModel：活跃下载 + 历史记录 ObservableCollection、暂停/恢复/取消命令
- DownloadsPage.xaml：标题速度头、活跃下载 ItemsRepeater（卡片布局+进度条+状态+操作按钮）、空状态提示、历史区域
- BoolNegationVisibilityConverter 值转换器
- ShellViewModel 订阅 IDownloadRuntimeStore 事件：状态栏下载速度实时显示
- ShellPage.xaml 状态栏下载速度 mini 面板
- DownloadRuntimeStore 双接口 DI 注册（具体类型 + IDownloadRuntimeStore）
- Presentation DI 注册 DownloadsViewModel（Transient）
- dotnet build 9 个项目零错误零警告，dotnet test 104/104 通过

### Task 4.5 - DownloadRuntimeStore + 进度聚合 (2026-04-13)
- DownloadRuntimeStore：ConcurrentDictionary 快照管理 + SpeedCalculator 滑动窗口（5s）
- DownloadProgressSnapshot DTO（TaskId/Status/Progress/Speed/ETA/FileName 等）
- 事件：SnapshotChanged / DownloadCompleted / DownloadFailed
- 速度计算：500ms 节流 + 字节差值 / 时间差值
- ETA 计算：剩余字节 / 当前速度
- dotnet build 9 个项目零错误零警告，dotnet test 104/104 通过

### Task 4.4 - Checkpoint 持久化 + 崩溃恢复 (2026-04-13)
- Migration_005_DownloadCheckpoints：检查点表（task_id/chunk_index/downloaded_bytes/updated_at）
- IDownloadTaskRepository 扩展：SaveCheckpointAsync / GetCheckpointsAsync / DeleteCheckpointsAsync
- DownloadTaskRepository 检查点 CRUD 实现（Dapper）
- DownloadOrchestrator 崩溃恢复：启动时加载未完成任务 + 恢复检查点 + 自动续传
- ChunkDownloadClient 断点续传：Range 头 + 检查点偏移
- 崩溃恢复集成测试
- dotnet build 9 个项目零错误零警告，dotnet test 91/91 通过

### Task 4.3 - ChunkDownloader + HTTP Range + Polly 韧性 (2026-04-13)
- ChunkDownloadClient：HTTP Range 分块下载 + IProgress 报告
- Polly ResiliencePipeline：指数退避重试（3 次）+ 超时（30s）+ 断路器
- 分块策略：自动计算分块数（10MB 基准）
- 临时文件 → 合并 → 最终文件原子写入
- dotnet build 9 个项目零错误零警告，dotnet test 78/78 通过

### Task 4.2 - Download Orchestrator + Scheduler + 服务层 (2026-04-13)
- DownloadScheduler：并发控制（SemaphoreSlim）+ 优先级队列 + 暂停/恢复
- DownloadOrchestrator：下载全流程编排（创建→排队→执行→完成/失败）
- IDownloadCommandService / IDownloadReadService 应用层接口
- DownloadCommandService / DownloadReadService 实现
- IDownloadTaskRepository + DownloadTaskRepository（SQLite+Dapper）
- Infrastructure DI 注册全部下载模块服务
- dotnet build 9 个项目零错误零警告，dotnet test 62/62 通过

### Task 4.1 - DownloadTask 领域实体 + 状态机 (2026-04-13)
- DownloadState 枚举（13 状态）+ DownloadTaskId 值对象
- DownloadStateMachine（17 条状态转换规则）
- DownloadTask 领域实体（Entity<DownloadTaskId>，状态机 + 进度更新 + 速度/ETA）
- ChunkInfo / DownloadCheckpoint 值对象
- DownloadStateMachineTests（17 合法 + 8 非法 + 默认 + CanTransitionTo + 4 流程测试）
- DownloadTaskTests（15 个测试：构造/状态转换/进度/AddChunk/ResetProgress）
- dotnet build 9 个项目零错误零警告，dotnet test 55/55 通过
- ShellViewModel 集成 IAuthService：登录/登出/会话恢复命令
- ShellPage NavigationView PaneHeader 用户信息区域（PersonPicture + 显示名 + 登出按钮）
- 未登录状态显示“登录 Epic Games”按钮（AccentButtonStyle）
- IsNotAuthenticated 计算属性供 x:Bind 取反绑定
- ShellPage 加载时自动尝试恢复认证会话
- SessionExpired 事件监听自动清理用户状态
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 3.2 - Token 存储 + 自动刷新 (2026-04-13)
- TokenRefreshBackgroundService：后台定时器每 2 分钟检查 Token 有效性
- 集成到 App.xaml.cs InitializeCoreServices 启动后台刷新
- Background 层 DI 注册 TokenRefreshBackgroundService
- DPAPI 加密存储已在 Task 3.1 完成（FileTokenStore）
- SessionExpiredEvent 已在 Task 3.1 定义并集成
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 3.1 - OAuth 核心流程 (2026-04-13)
- IAuthService 接口（LoginAsync/LogoutAsync/GetAccessTokenAsync/TryRestoreSessionAsync/SessionExpired）
- AuthUserInfo 模型（AccountId/DisplayName/Email）
- TokenPair 模型（AccessToken/RefreshToken/ExpiresAt/AccountId/DisplayName）
- ITokenStore 接口（SaveTokensAsync/LoadTokensAsync/ClearAsync）
- SessionExpiredEvent 事件
- EpicOAuthHandler：本地 HTTP 监听器接收回调、授权码交换、Token 刷新、账户信息获取、Token 撤销
- AuthService：完整 OAuth 流程编排、会话恢复、自动刷新（提前5分钟）、登出
- FileTokenStore：DPAPI 加密文件存储 Token（Task 3.2 将升级为 Credential Locker）
- Infrastructure DI 注册：ITokenStore/EpicOAuthHandler/IAuthService + 命名 HttpClient
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 2.6 - Diagnostics 页面 — 缓存管理 (2026-04-13)
- CacheStatistics 模型（缩略图/Manifest/日志各项字节数+文件数+总计）
- ICacheManager 接口（GetCacheStatisticsAsync / ClearThumbnailCacheAsync / ClearManifestCacheAsync / ClearLogCacheAsync / ClearAllCacheAsync）
- CacheManager 实现（目录扫描统计、分类清理、日志保留最近1天）
- DiagnosticsViewModel 缓存管理状态（刷新/分类清理/全部清理命令）
- DiagnosticsPage.xaml Pivot Tab 3：缓存统计卡片 + 分类清理按钮 + 全部清理
- Infrastructure DI 注册 ICacheManager → CacheManager
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 2.5 - Diagnostics 页面 — 日志查看器 (2026-04-13)
- LogEntryLevel 枚举 + LogEntry 模型（Timestamp/Level/Source/Message/Exception/CorrelationId）
- IDiagnosticsReadService 扩展：GetRecentLogsAsync（数量+级别筛选）/ SearchLogsAsync（关键字+级别）
- DiagnosticsService 实现 CompactJSON 日志文件解析（@t/@l/@m/@mt/SourceContext/@x/CorrelationId）
- 支持 FileShare.ReadWrite 读取 Serilog 正在写入的日志文件
- DiagnosticsViewModel 日志查看状态：搜索关键字、级别筛选、ObservableCollection 绑定、导出
- DiagnosticsPage.xaml Pivot 双 Tab：系统信息 + 日志查看器
- 日志查看器 UI：搜索框、级别 ComboBox、查询/导出按钮、ListView + DataTemplate、底部日志计数
- LogEntryDisplay UI 显示模型（Timestamp/Level/LevelColor/Source/Message/Exception/CorrelationId）
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 2.4 - Diagnostics 页面 — 系统信息 (2026-04-13)
- SystemDiagnosticsSummary 模型（OS/内存/磁盘/版本/进程内存/数据库大小）
- IDiagnosticsReadService 接口（GetSystemSummaryAsync）
- DiagnosticsService 实现（收集 OS/磁盘/内存/数据库信息）
- DiagnosticsViewModel：系统信息/磁盘空间/内存使用展示 + 手动刷新
- DiagnosticsPage.xaml：系统信息、磁盘空间（进度条）、内存、存储四个区域
- 页面加载时自动采集诊断信息
- Infrastructure DI 注册 DiagnosticsService
- Presentation DI 注册 DiagnosticsViewModel
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 2.3 - SQLite 数据层 + Repository 基础 (2026-04-13)
- Migration_002_Downloads：下载任务表（id/asset_id/status/total_bytes/downloaded_bytes/checkpoint 等）
- Migration_003_Installations：已安装资产表（id/asset_id/version/install_path/size_bytes 等）
- Migration_004_SettingsKv：键值设置表（key/value/category，与 user.settings.json 互补）
- RepositoryBase<T> 泛型基类（Dapper CRUD：GetById/GetAll/Query/Insert/Update/Delete/Count/事务）
- Infrastructure InternalsVisibleTo Tests.Integration
- 6 个 CRUD 集成测试（内存 SQLite：插入查询/全量查询/删除/不存在删除/计数/更新）
- Infrastructure DI 注册 3 个新迁移
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 2.2 - Settings 页面 UI (2026-04-13)
- SettingsViewModel（CommunityToolkit.Mvvm）：下载/外观/路径/网络四组配置双向绑定
- SettingsPage.xaml 完整 UI：通用、下载、路径、高级四个设置分组
- 主题切换实时生效（ComboBox 选择后即时通知 ThemeService）
- 各分组独立保存按钮 + 全局重置按钮
- 状态消息提示（保存成功/失败/重置完成）
- ViewModelLocator 静态服务定位器（解决 Frame.Navigate 无法构造器注入的问题）
- Presentation DI 注册 SettingsViewModel（Transient）
- App.xaml.cs 启动时配置 ViewModelLocator
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 2.1 - 配置系统完整实现 (2026-04-13)
- 强类型配置类：DownloadConfig / AppearanceConfig / PathConfig / NetworkConfig（Application 层）
- ConfigChangedEvent 配置变更事件（sealed record，Section + NewConfig）
- ISettingsCommandService 写接口（UpdateDownload/Appearance/Path/NetworkConfigAsync + ResetToDefaultsAsync）
- ISettingsReadService 读接口（GetDownload/Appearance/Path/NetworkConfig 同步返回）
- UserSettings JSON 持久化模型（Infrastructure 层）
- SettingsService 双接口实现（ISettingsCommandService + ISettingsReadService）
- user.settings.json 文件读写（DataPath 目录），异步 I/O
- 线程安全（lock）+ 深拷贝隔离（JSON 序列化/反序列化防止外部修改内部状态）
- ConfigChanged 事件通知订阅者配置变更
- Infrastructure DI 注册 SettingsService（Singleton + 双接口映射）
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 1.6 - 系统托盘 (2026-04-13)
- TrayIconManager（System.Windows.Forms.NotifyIcon via FrameworkReference）
- 系统托盘图标 + 右键菜单（显示主窗口 / 退出）
- 托盘双击 → 显示主窗口
- 关闭按钮 → 最小化到托盘（AppWindow.Closing Cancel + Hide）
- ActivateMainWindow 增强：AppWindow.Show() 恢复隐藏窗口
- Phase 1 Shell 壳层全部完成
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 1.5 - 主题切换 + 状态栏 (2026-04-13)
- ThemeService：Light / Dark / System 三种主题切换，即时生效
- 主题持久化到 theme.json（%LOCALAPPDATA%/HelsincyEpicLauncher/Data/）
- ShellPage.Loaded 初始化 ThemeService 并恢复保存的主题
- 底部状态栏：网络状态图标+文字 + 下载速度占位
- DI 注册 ThemeService
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 1.4 - Dialog 对话框服务 (2026-04-13)
- IDialogService 接口（ShowConfirmAsync / ShowInfoAsync / ShowErrorAsync / ShowCustomAsync）
- DialogService 实现：基于 WinUI 3 ContentDialog + XamlRoot 绑定
- 确认对话框（Primary + Close 按钮）、信息对话框、错误对话框（含可重试按钮）
- ShellPage.Loaded 设置 DialogService.XamlRoot
- 抑制 MVVMTK0045 警告（非 AOT 场景无影响）
- DI 注册 DialogService（具体类型 + 接口）
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 1.3 - Toast 通知服务 (2026-04-13)
- INotificationService 接口（ShowSuccess / ShowWarning / ShowError / ShowInfo，支持自定义持续时间）
- NotificationService 实现：基于 WinUI 3 InfoBar 控件，支持 4 种严重级别
- 自动消失（Info/Success 4s、Warning 6s、Error 8s）+ 手动关闭按钮
- ShellPage 添加 ToastHost 覆盖层（右上角，MaxWidth 400）
- DispatcherQueue 线程安全调度，支持后台线程调用
- DI 注册 NotificationService（具体类型 + 接口）
- dotnet build 9 个项目零错误，dotnet test 15/15 通过

### Task 1.2 - ShellPage + NavigationView (2026-04-13)
- NavigationRoute 路由常量定义（FabLibrary / Downloads / EngineVersions / Settings / Diagnostics）
- NavigationService 完整实现：Frame 导航 + 路由映射 + 历史栈跟踪 + Serilog 日志
- ShellViewModel 基础版：5 个导航 RelayCommand + 全局状态 ObservableProperty 占位
- ShellPage UserControl：NavigationView（Left 模式）5 项导航 + ContentFrame
- 5 个模块占位页：FabLibraryPage / DownloadsPage / EngineVersionsPage / SettingsPage / DiagnosticsPage
- MainWindow 加载 ShellPage 到内容区域（DI 解析 ShellViewModel + NavigationService）
- Presentation DI 更新：NavigationService 替换 StubNavigationService + 注册 ShellViewModel
- dotnet build 9 个项目零错误，dotnet test 15/15 通过

### Task 1.1 - MainWindow 自定义标题栏 + Mica 背景 (2026-04-13)
- 自定义标题栏：应用图标占位（品牌色 H 标识） + 标题文字 + 系统最小化/最大化/关闭按钮
- ExtendsContentIntoTitleBar + SetTitleBar 实现窗口拖拽区域
- MicaBackdrop 背景材质
- Win32 子类化拦截 WM_GETMINMAXINFO 强制最小窗口尺寸 1024x640
- PInvoke 扩展：GetDpiForWindow / SetWindowSubclass / DefSubclassProc / MINMAXINFO 结构体
- DPI 感知：自动将 DIP 最小尺寸转为物理像素
- dotnet build 9 个项目零错误，dotnet test 15/15 通过

### Task 0.7 - WinUI 3 空窗口 + 单实例 (2026-04-13)
- 创建 App.xaml + App.xaml.cs（WinUI 3 Application 子类，管理单实例 + DI + Serilog + 数据库迁移）
- 创建 MainWindow.xaml + MainWindow.xaml.cs（空窗口，1280x800 默认尺寸）
- Mutex 单实例保证（HelsincyEpicLauncher_SingleInstance）
- 命名管道通信（第二实例发送 ACTIVATE → 已有实例激活窗口）
- PInvoke: ShowWindow + SetForegroundWindow
- Program.cs 简化为 WinUI 3 启动入口（DISABLE_XAML_GENERATED_MAIN）
- Launcher.App.csproj: AllowUnsafeBlocks + DISABLE_XAML_GENERATED_MAIN
- Phase 0 全部完成，dotnet build 9 个项目零错误，dotnet test 15/15 通过

### Task 0.6 - INavigationService 空实现 (2026-04-12)
- 创建 INavigationService 接口（Presentation/Shell/Navigation — NavigateAsync / GoBackAsync / CanGoBack / CurrentRoute）
- 创建 StubNavigationService 桩实现（导航历史栈 + Serilog 日志记录）
- Presentation DI 注册 INavigationService → StubNavigationService
- 3 个 NSubstitute 导航单元测试（NavigateAsync / GoBackAsync / 默认状态）
- Tests.Unit 添加 Presentation 项目引用
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 0.5 - SQLite 数据库基础 (2026-04-12)
- 创建 IDbConnectionFactory / IDatabaseInitializer 接口（Application 层）
- 创建 SqliteConnectionFactory（WAL 模式，路径 %LOCALAPPDATA%/HelsincyEpicLauncher/Data/launcher.db）
- 创建 Migration 框架：IMigration 接口 + MigrationRunner（版本化 SQL 脚本执行，__migration_history 表跟踪）
- 创建初始迁移 Migration_001_AppSettings（app_settings 键值表）
- Program.cs 启动时通过 IDatabaseInitializer 执行数据库迁移
- Infrastructure DI 注册 SqliteConnectionFactory、MigrationRunner
- dotnet build 9 个项目零错误零警告，dotnet test 12/12 通过

### Task 0.4 - Shared 层基础类型 (2026-04-12)
- 创建 Error 结构化错误模型（Code / UserMessage / TechnicalMessage / CanRetry / Severity / InnerException）
- 创建 ErrorSeverity 枚举（Warning / Error / Critical / Fatal）
- 创建 Result / Result&lt;T&gt; 统一操作结果（含 Map/Bind 链式操作、IsFailure 便捷属性）
- 创建 Entity&lt;TId&gt; 实体基类（按 Id 判等 + 运算符重载）
- 创建 ValueObject 值对象基类（按属性值判等 + GetEqualityComponents 模式）
- 创建 StateMachine&lt;TState&gt; 泛型状态机基类（DefineTransition + TransitionTo + CanTransitionTo + OnTransitioned 回调）
- 10 个 Result/Error 单元测试（成功/失败/Map/Bind/属性验证）
- dotnet build 9 个项目零错误零警告，dotnet test 12/12 通过

### Task 0.3 - Serilog 日志系统 (2026-04-12)
- 创建 OperationContext（Shared/Logging — CorrelationId + Module + Operation 推入 LogContext）
- 创建 OperationTimer（Shared/Logging — using 模式自动记录操作开始/完成及耗时）
- 创建 LogSanitizer（Shared/Logging — Token 脱敏、URL 敏感参数清理、GeneratedRegex）
- Program.cs 初始化 Serilog：主日志(Info+, 30天)、错误日志(Error+, 90天)、下载日志(Debug, 14天)
- DEBUG 模式启用控制台 Sink（含 Module/Operation 模板）
- 全局 Enricher：ThreadId / AppVersion / LogContext
- Shared 层添加 Serilog 4.2.0 NuGet 引用
- dotnet build 9 个项目零错误零警告，dotnet test 2/2 通过

### Task 0.2 - DI 容器 + 配置系统 (2026-04-12)
- 创建 IAppConfigProvider 接口（Shared 层 — AppVersion / 各路径 / 下载参数）
- 创建 AppConfigProvider 实现（Infrastructure 层 — 读取 IConfiguration，默认 %LOCALAPPDATA%）
- 更新 appsettings.json 添加 Paths 和 Downloads 配置节
- 实现 Program.cs 完整 DI 容器构建流程（ConfigurationBuilder → ServiceCollection → BuildServiceProvider）
- 实现各层 AddXxx() 扩展方法（Domain / Application / Infrastructure / Presentation / Background）
- 为各层项目添加 Microsoft.Extensions.DependencyInjection.Abstractions 包引用
- 为 Infrastructure 添加 Microsoft.Extensions.Configuration.Abstractions 包引用
- dotnet build 9 个项目零错误，dotnet test 2/2 通过

### Task 0.1 - 创建 Solution 和项目文件 (2026-04-12)
- 创建 HelsincyEpicLauncher.slnx 解决方案
- 创建 7 个源码项目：App / Presentation / Application / Domain / Infrastructure / Background / Shared
- 创建 2 个测试项目：Tests.Unit / Tests.Integration
- 配置项目引用关系（按架构依赖图）
- 引入全部 NuGet 包（WindowsAppSDK, CommunityToolkit.Mvvm, Serilog, SQLite, Polly, xUnit 等）
- 创建 Directory.Build.props（统一 TFM/版本/版权 + dotnet CLI 的 AppxPackage 路径修复）
- 创建 global.json（固定 .NET 9.0.309 SDK）
- 创建 .editorconfig 代码风格规范
- 创建 app.manifest（DPI 感知）
- 各层 DependencyInjection.cs 占位
- 2 个 Sanity 测试通过
- dotnet build 零错误零警告
- 技术变更：从 .NET 8 改为 .NET 9（系统无 .NET 8 SDK）

### 文档阶段 (2024-12-15)
- 创建 20+ 架构设计文档（docs/ 目录）
- 覆盖：项目总览、架构原则、解决方案结构、模块依赖、核心接口、10 个模块定义、下载子系统、状态管理、错误处理、启动流程、技术栈、AI 协作、开发阶段、反模式
- 新增日志策略文档（15-LoggingStrategy.md）
- 新增 AI 会话交接协议（12-AICollaboration.md § 8~11）
- 将开发计划细化为 41 个原子任务（13-DevelopmentPhases.md）
