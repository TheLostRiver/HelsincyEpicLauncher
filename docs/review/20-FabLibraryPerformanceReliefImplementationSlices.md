# Fab 列表页性能止血细粒度实施拆解

## 1. 目的

这不是策略稿，而是一份用于“防上下文爆满”和“避免性能修复漂移”的实施拆解文档。

适用场景：

- 用户已经明确反馈 Fab 列表页“比网页版还卡、加载太慢”
- 当前已定位到隐藏 `FabPreviewProbe` WebView2 探针窗口参与了列表缺图卡片的补图链路
- 需要先做一轮低风险止血，而不是直接重构整条 Fab 数据链或继续做页面缓存实验

本文默认以以下文档为强约束来源：

- [19-FabLibraryWarmResumeImplementationSlices.md](19-FabLibraryWarmResumeImplementationSlices.md)
- [18-FabLibraryWarmResumeStrategy.md](18-FabLibraryWarmResumeStrategy.md)
- [17-FabDetailImplementationSlices.md](17-FabDetailImplementationSlices.md)
- [../06-ModuleDefinitions/FabLibrary.md](../06-ModuleDefinitions/FabLibrary.md)
- [../14-AntiPatterns.md](../14-AntiPatterns.md)
- [../02-ArchitecturePrinciples.md](../02-ArchitecturePrinciples.md)

若本文与以上文档冲突，以架构规约、模块规约和既有已落地边界为准。

## 2. 使用规则

每次会话只做一个子切片，严格遵守以下规则：

1. 一次只选择一个 `子切片` 开工，不跨到下一个子切片。
2. 单个子切片原则上不超过 `3` 个生产文件；若超过，必须继续细拆。
3. 第一优先级是“列表页止血”，不是“恢复所有真实预览图”。
4. 不得为了性能止血顺手重构 `NavigationService`、`SessionStateStore`、`FabCatalogReadService` 或整个启动管线。
5. 不得把 `FabLibraryViewModel` 升级为全局静态单例，也不得重新打开 `NavigationCacheMode.Required` 实验。
6. 首期不改 `IFabPreviewUrlReadService` 的整体架构，不做“单长期存活 probe 复用”大改；那属于后续可选实验。
7. 首期允许列表卡片在存在 preview 锚点时依旧显示占位图；这是有意的性能换取，不算功能回退 bug。
8. 详情页主 Hero 的真实预览恢复链路保持可用，不应被列表页止血顺手拆掉。
9. 每完成一个原子任务，必须同步更新 `CHANGELOG.md` 与 `SESSION_HANDOFF.md`。

### 2.1 上下文快满时的固定动作

本轮专用本地执行记录文件：

- [21-FabLibraryPerformanceReliefExecutionLog.md](21-FabLibraryPerformanceReliefExecutionLog.md)

当上下文接近上限时，必须先做以下动作，再压缩上下文：

1. 更新执行记录文件中的“当前子切片”“已完成项”“进行中项”“最后验证”“下一步”。
2. 若当前原子任务已经完成，同时更新 `CHANGELOG.md` 与 `SESSION_HANDOFF.md`。
3. 压缩上下文。
4. 压缩完成后的第一步，不是继续猜，而是先读本文和执行记录文件，再继续当前子切片。

## 3. 当前代码现状快照

以下现状已经在代码中确认，后续切片必须建立在这些事实之上：

1. [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs) 当前在 `ItemsRepeater.ElementPrepared` 中对每个进入视口的卡片调用 `card.LoadThumbnailAsync()`。
2. [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs) 中的 `FabAssetCardViewModel.LoadThumbnailAsync()` 当前在 `ThumbnailUrl` 为空、但存在 `PreviewListingId / PreviewProductId` 时，会调用 `IFabPreviewUrlReadService.TryResolveThumbnailUrlAsync(...)`。
3. [../../src/Launcher.Presentation/Modules/FabLibrary/FabListingPageReadService.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabListingPageReadService.cs) 当前每次读取 listing HTML 时，都会新建一个隐藏离屏 `Window`，标题为 `FabPreviewProbe`，内部承载 `WebView2`；并为每次读取创建独立临时目录，带 `12s` 超时与串行 `SemaphoreSlim`。
4. [../../src/Launcher.Infrastructure/FabLibrary/FabApiClient.cs](../../src/Launcher.Infrastructure/FabLibrary/FabApiClient.cs) 当前直连 `www.fab.com/api` 时仍可能命中站点 challenge；[../../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs](../../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs) 会在该情况下回退到 `EpicOwnedFabCatalogClient`。
5. [../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs) 当前详情页主 Hero 与“更多内容”相关卡片仍可能调用 `IFabPreviewUrlReadService` 做真实预览补图。
6. [19-FabLibraryWarmResumeImplementationSlices.md](19-FabLibraryWarmResumeImplementationSlices.md) 当前 `S0-S9` 已全部完成，且 `S9-B` 已明确回退页面缓存实验，恢复到“快照恢复为主、页面缓存不保留”的基线。

## 4. 本轮实施的边界决策

为避免性能止血再次扩散，本轮实现明确采用以下边界：

### 4.1 首期主路径先做“列表页停 probe”

当前第一优先级不是让列表页补齐所有真实预览，而是立刻减少：

1. 首屏等待感
2. 滚动进入视口时的卡顿
3. 隐藏 `FabPreviewProbe` 窗口频繁创建的成本

因此首期主路径采用：

1. Fab 列表页卡片不再为缺图资产拉起真实预览 probe。
2. 缺图卡片直接进入稳定占位态。
3. 用户打开详情页后，再允许详情主图继续尝试真实预览恢复。

### 4.2 首期不做“长期存活单 probe 复用”

虽然复用单个隐藏 WebView2 可能进一步提速，但这已经超出“止血”范围。

首期不做：

1. `FabListingPageReadService` 生命周期重构
2. 单窗口 probe 池化
3. 跨页面共享浏览器上下文

这些只保留到后续可选实验切片。

### 4.3 首期不改详情页主 Hero 的真实预览恢复

列表页停 probe 的目的，是先把资产库入口体验拉回可接受水平。

因此本轮：

1. 详情页主 Hero 继续允许补真实预览。
2. 详情页“更多内容”卡片是否也停 probe，单独作为后续可选切片评估。
3. 不把列表页止血顺手扩大成“全站禁用真实预览解析”。

### 4.4 首期不新增用户设置开关

当前用户反馈已经足够明确：现状太卡。

因此首期直接调整默认行为，不先增加“列表页是否启用真实预览补图”的设置项。

若后续出现明确分歧，再把它升级为设置开关。

## 5. 切片总览

| Slice | 名称 | 状态 | 目标 |
|------|------|------|------|
| S0 | 文档与执行记录入口 | 已完成 | 建立性能止血切片文档与本地执行记录模板 |
| S1 | 列表卡片停用 probe | 未开始 | 让 Fab 列表卡片不再触发真实预览解析 |
| S2 | 占位态与文案校正 | 未开始 | 让停 probe 后的列表卡片立即稳定呈现占位态，而不是长时间 loading |
| S3 | 详情主图回归保护 | 未开始 | 确认详情页主 Hero 仍能保留真实预览恢复路径 |
| S4 | 日志与定向验证 | 未开始 | 为“列表页跳过 probe”补最小可观测日志与聚焦验证 |
| S5 | 详情页更多内容评估（可选） | 未开始 | 若详情页相关卡片仍明显卡顿，再单独评估是否停用其 probe |
| S6 | 单 probe 复用实验（可选） | 未开始 | 只有在 S1-S4 完成且仍证据充足时，再评估复用单个隐藏 probe |

### 5.1 子切片总览

| 子切片 | 所属 Slice | 状态 | 单次目标 |
|------|------|------|------|
| S0-A | S0 | 已完成 | 新建性能止血实施拆解文档 |
| S0-B | S0 | 已完成 | 新建本地执行记录模板 |
| S1-A | S1 | 未开始 | 在列表卡片 ViewModel 中引入“是否允许真实预览 probe”的显式策略位 |
| S1-B | S1 | 未开始 | Fab 列表页构造卡片时显式关闭 probe |
| S1-C | S1 | 未开始 | 缺图列表卡片在 probe 关闭时直接短路，不再调用 `IFabPreviewUrlReadService` |
| S1-D | S1 | 未开始 | 为“列表卡片关闭 probe 后不再读 listing HTML”补聚焦单测 |
| S2-A | S2 | 未开始 | probe 关闭的缺图卡片立即结束 loading，转入稳定占位态 |
| S2-B | S2 | 未开始 | 占位文案区分“平台无预览”与“列表页当前不补图” |
| S2-C | S2 | 未开始 | 若有必要，微调占位 UI 文案或提示，不让用户误解为网络故障 |
| S3-A | S3 | 未开始 | 确认详情页主 Hero 预览恢复链路不受 S1/S2 影响 |
| S3-B | S3 | 未开始 | 若触及详情页代码，则补一条对应的回归验证 |
| S4-A | S4 | 未开始 | 为列表卡片跳过 probe 补最小结构化日志 |
| S4-B | S4 | 未开始 | 运行定向 build / test 验证止血改动 |
| S4-C | S4 | 未开始 | 固化最小手工冒烟步骤与回退判定 |
| S5-A | S5 | 未开始 | 评估详情页“更多内容”卡片是否仍然触发过多 probe |
| S5-B | S5 | 未开始 | 若证据成立，仅对相关卡片停用 probe |
| S6-A | S6 | 未开始 | 设计单长期存活 probe 复用方案 |
| S6-B | S6 | 未开始 | 对比复用前后内存、滚动与等待收益，再决定保留或回退 |

## 6. 细粒度切片定义

### S0 文档与执行记录入口

- 状态：`已完成`
- 目标：在真正动性能止血代码前，先把切片入口与本地执行记录落好。
- 已落地产物：
  - [20-FabLibraryPerformanceReliefImplementationSlices.md](20-FabLibraryPerformanceReliefImplementationSlices.md)
  - [21-FabLibraryPerformanceReliefExecutionLog.md](21-FabLibraryPerformanceReliefExecutionLog.md)

### S1 列表卡片停用 probe

- 状态：`未开始`
- 目标：先切断 Fab 列表卡片缺图时对隐藏 WebView2 probe 的依赖。

#### S1-A 引入列表卡片的 preview probe 策略位

- 状态：`未开始`
- 目标：不要再让“是否走真实预览解析”依赖隐式调用环境，而是显式建模。
- 本轮只做：
  - 在列表卡片 ViewModel 中新增一个最小策略位，例如 `allowPreviewProbe` 或等价命名
  - 默认行为保持兼容，避免一口气影响详情页相关卡片
- 本轮不做：
  - 改 `IFabPreviewUrlReadService`
  - 改详情页 Hero 行为
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 卡片 ViewModel 本身已具备“允许 / 禁止真实预览解析”的显式状态位
- 验证动作：
  - 编译通过

#### S1-B Fab 列表页构造卡片时显式关闭 probe

- 状态：`未开始`
- 目标：只改 Fab 列表页卡片创建路径，不波及详情页。
- 本轮只做：
  - 在 `FabLibraryViewModel` 构造 `FabAssetCardViewModel` 时传入“关闭 probe”策略
  - 保持详情页主 Hero 与详情页相关卡片的现有构造路径不变
- 本轮不做：
  - 改详情页“更多内容”卡片
  - 改基础缓存层
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - Fab 列表卡片与详情页相关卡片已经能走不同的 probe 策略
- 验证动作：
  - 编译通过

#### S1-C probe 关闭时直接短路，不再触发 listing HTML 读取

- 状态：`未开始`
- 目标：把收益落到真正耗时点上，而不是只加一个字段不生效。
- 本轮只做：
  - 在 `LoadThumbnailAsync()` 中，当 `ThumbnailUrl` 为空且 probe 被禁用时直接进入占位态
  - 不再调用 `IFabPreviewUrlReadService.TryResolveThumbnailUrlAsync(...)`
- 本轮不做：
  - 调整详情页 hero 逻辑
  - 重写缩略图缓存
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 列表缺图卡片不会再拉起 `FabPreviewProbe`
  - 卡片仍然能稳定显示占位态，不出现异常或长期 loading
- 验证动作：
  - 编译通过
  - 本地运行态确认进入 Fab 列表页时，不再因为首屏缺图卡片触发隐藏 probe 窗口洪泛

#### S1-D 为列表页停 probe 补聚焦单测

- 状态：`未开始`
- 目标：把“停 probe”这个关键止血点锁进测试，避免后续回归。
- 本轮只做：
  - 新增或扩展单测，验证：当 `ThumbnailUrl` 为空、存在 preview 锚点、且 probe 被禁用时，不会调用 preview resolver
  - 同时验证卡片最终进入占位态
- 建议目标文件：
  - `tests/Launcher.Tests.Unit/FabAssetCardViewModelTests.cs`
  - 如不便新增文件，也可扩展现有 `FabLibraryViewModel` 相关测试文件
- 完成标准：
  - “列表卡片关闭 probe”具备回归保护
- 验证动作：
  - 定向 `dotnet test`

### S2 占位态与文案校正

- 状态：`未开始`
- 目标：列表页停 probe 后，用户看到的不是“坏了”，而是“当前列表页不补真实预览”。

#### S2-A probe 关闭的缺图卡片立即结束 loading

- 状态：`未开始`
- 目标：避免用户看到大量长期转圈的卡片。
- 本轮只做：
  - 确保 probe 禁用分支会同步或尽快把 `IsThumbnailLoading` 置为 false
  - `ShowThumbnailPlaceholder` 及时生效
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - Fab 首屏缺图卡片不会长时间保留 loading 骨架
- 验证动作：
  - 编译通过
  - 本地运行态观察首屏卡片状态切换

#### S2-B 占位文案语义改正

- 状态：`未开始`
- 目标：停 probe 以后，原来的“平台未返回预览”文案可能误导用户，需要重新校正。
- 本轮只做：
  - 为“平台真的没给图”和“列表页当前主动不补图”区分文案
  - 保持文案简洁，不加大段说明
- 本轮不做：
  - 新增复杂 Tooltip 面板
  - 增加设置项说明区
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 用户不会把“止血策略”误判成“网络/接口异常”
- 验证动作：
  - 编译通过

#### S2-C 必要时微调列表占位 UI 提示

- 状态：`未开始`
- 目标：只有在 S2-B 文案仍不足以解释状态时，才轻微触碰 XAML。
- 本轮只做：
  - 若确认需要，给占位态补充极小提示或 Tooltip
  - 保持卡片布局稳定，不改整体网格结构
- 本轮不做：
  - 大规模重排卡片 UI
  - 新增复杂状态面板
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml)
  - 如必要，再配合同文件 code-behind 或 ViewModel 的最小改动
- 完成标准：
  - 占位态语义清楚，但不让列表 UI 变吵
- 验证动作：
  - 编译通过

### S3 详情主图回归保护

- 状态：`未开始`
- 目标：确认列表页止血不误伤详情页主 Hero 的真实预览恢复。

#### S3-A 确认详情主 Hero 行为不变

- 状态：`未开始`
- 目标：把本轮边界钉死，避免“列表停 probe”被误做成“详情也停 probe”。
- 本轮只做：
  - 检查 `FabAssetDetailViewModel.TryResolveHeroPreviewUrlAsync()` 仍保留现有路径
  - 若 S1/S2 未触及详情页代码，本切片以验证为主，不强制生产改动
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
- 完成标准：
  - 列表页止血完成后，详情主图仍能继续尝试真实预览恢复
- 验证动作：
  - 编译通过
  - 最小手工冒烟：选一个列表缺图但有 preview 锚点的资产，确认详情 Hero 仍可尝试恢复图像

#### S3-B 若触及详情页代码，再补定向回归验证

- 状态：`未开始`
- 目标：只有在前面切片不得不调整详情相关代码时，才补这一层自动化保护。
- 建议目标文件：
  - `tests/Launcher.Tests.Unit/*FabAssetDetail*Tests*.cs`
  - 或扩展现有 Fab 详情相关测试文件
- 完成标准：
  - 若详情页代码被修改，则必须有至少一条回归验证保护 Hero 预览恢复行为
- 验证动作：
  - 定向 `dotnet test`

### S4 日志与定向验证

- 状态：`未开始`
- 目标：让“列表页确实停掉了 probe”具备最小可观测性与最小验收闭环。

#### S4-A 为列表跳过 probe 补最小日志

- 状态：`未开始`
- 目标：后续如果用户仍反馈慢，能快速区分到底是列表卡顿，还是详情/其他路径。
- 本轮只做：
  - 为“列表卡片因策略跳过 preview probe”补一条低噪音结构化日志
  - 避免每张卡片都打高频日志；可考虑采样或仅在关键路径打点
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 运行态可从日志确认列表卡片是否已经停止真实预览解析
- 验证动作：
  - 编译通过

#### S4-B 定向 build / test 验证

- 状态：`未开始`
- 目标：把止血改动控制在局部验证，不重跑无关大面。
- 建议命令：
  1. `dotnet build src/Launcher.App/Launcher.App.csproj --no-restore`
  2. `dotnet test tests/Launcher.Tests.Unit/Launcher.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~FabAssetCardViewModelTests|FullyQualifiedName~FabLibraryViewModel"`
- 完成标准：
  - 相关构建通过
  - 新增或受影响的聚焦测试通过

#### S4-C 固化最小手工冒烟与回退判定

- 状态：`未开始`
- 目标：避免改完以后只凭感觉判断。
- 本轮必须确认以下步骤：
  1. 首次进入 Fab 列表页，首屏可见速度是否明显改善。
  2. 首屏缺图卡片是否直接落到占位态，而不是长时间转圈。
  3. 观察后台是否仍频繁出现 `FabPreviewProbe` 窗口。
  4. 打开一个缺图资产的详情页，确认详情 Hero 是否仍能恢复图像或继续尝试恢复。
- 回退判定：
  1. 若列表仍明显卡顿，且确认已不再触发列表级 probe，则瓶颈不在这条链，进入 `S5/S6` 或重新排查主数据链。
  2. 若详情 Hero 回归丢失，则优先修正 `S3`，不继续推进后续可选优化。

### S5 详情页更多内容评估（可选）

- 状态：`未开始`
- 目标：只有在列表止血完成后，才评估详情页“更多内容”卡片是否也需要同样处理。

#### S5-A 评估相关卡片是否仍触发过多 probe

- 状态：`未开始`
- 目标：先看证据，不预设答案。
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
- 完成标准：
  - 明确“更多内容”卡片是否构成新的主要卡顿来源

#### S5-B 若证据成立，仅对相关卡片停用 probe

- 状态：`未开始`
- 目标：把详情页二级卡片与 Hero 分开处理，只降二级卡片，不误伤主图。
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
- 完成标准：
  - 相关卡片不再拖慢详情页，但 Hero 仍保留真实预览恢复

### S6 单 probe 复用实验（可选）

- 状态：`未开始`
- 目标：只有当 `S1-S4` 已完成、且仍有明确性能证据时，才评估更大改造。

#### S6-A 设计单长期存活 probe 复用方案

- 状态：`未开始`
- 目标：把“每次新建隐藏窗口 + 新建 WebView2 环境”的成本，收敛为可复用的内部服务。
- 注意：
  - 这是架构级实验，不应与首期止血混做
  - 一次会话不应跨过 `3` 个生产文件

#### S6-B 对比收益后决定保留或回退

- 状态：`未开始`
- 目标：避免再次走到“复杂度上去了，但收益不确定”的路径。
- 完成标准：
  - 至少能比较返回耗时、首屏等待感、内存常驻成本，再决定是否留用

## 7. 推荐执行顺序

严格按以下顺序推进：

1. `S0-A → S0-B`
2. `S1-A → S1-B → S1-C → S1-D`
3. `S2-A → S2-B → S2-C`
4. `S3-A → S3-B`
5. `S4-A → S4-B → S4-C`
6. `S5-A → S5-B` 仅在列表止血后仍有详情卡顿证据时再做
7. `S6-A → S6-B` 仅在前面全部完成且仍需更深性能收益时再做

## 8. 第一批建议开工点

如果下一轮开始实现，默认从以下最小闭环启动：

1. `S1-A`，为列表卡片引入显式 probe 策略位
2. `S1-B`，Fab 列表页构造卡片时关闭 probe
3. `S1-C`，缺图列表卡片 probe 关闭时直接短路到占位态
4. `S1-D`，补一条“列表卡片不再读 listing HTML”的聚焦单测

原因：

1. 当前最重的用户感知问题是列表页慢和卡，而不是详情页主 Hero 偶尔补图慢。
2. `S1` 只需聚焦一个主要生产文件即可完成大部分止血收益，适合在上下文受限时优先推进。
3. `S1` 做完后，再决定是否需要 `S2` 的文案/UI 校正与 `S5/S6` 的更深实验。