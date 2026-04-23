# Fab 详情页细粒度实施拆解

## 1. 目的

这不是设计稿，而是一份用于“防上下文丢失”的实施拆解文档。

适用场景：

- 单次只能完成一个很小的实现切片
- 多轮会话之间需要快速恢复上下文
- 需要明确每一步该改哪些文件、做到什么程度、如何验证

本文默认以前置设计文档 [16-FabDetailRichContentDesign.md](16-FabDetailRichContentDesign.md) 为约束来源；若两者冲突，以架构/反模式约束和设计文档为准。

## 2. 使用规则

每次会话只做一个切片，严格遵守以下规则：

1. 只选择一个 `Slice` 开工。
2. 先完成该切片的“目标文件”修改，再做该切片的“验证动作”。
3. 若验证失败，只修当前切片，不顺手扩到下一个切片。
4. 切片完成后，更新本文中的状态标记。
5. 未明确列入当前切片的需求，一律不在本轮处理。
6. 若当前切片仍然过大，优先执行其子切片，例如 `S3-A`、`S3-B`。
7. 单个子切片原则上不超过 `3` 个生产文件；若超出，继续再拆。

推荐状态值：

- `未开始`
- `进行中`
- `已完成`
- `已阻塞`

## 3. 当前现状快照

截至本文编写时，以下内容已经完成：

- `FabAssetDetail` 已补充 `PublishedAt`、`Formats`
- Epic owned fallback 已补充格式映射、发布时间映射、预览图兜底
- 详情页 UI 已具备：Hero、描述、预览图、技术详情、兼容性、标签、包含格式、详情元数据、底部更多内容
- 定向单元测试已覆盖 fallback 关键路径

这意味着后续工作重点不再是“从零开始搭详情页”，而是继续补齐 enrichment、提升稳定性、压实验证和文档闭环。

### 3.1 文档阶段完成度

当前“实现文档”阶段以以下交付物是否齐备为准：

| 交付物 | 作用 | 当前状态 |
|------|------|------|
| `16-FabDetailRichContentDesign.md` | 高层设计与边界约束 | 已完成 |
| `17-FabDetailImplementationSlices.md` | 细粒度切片与恢复模板 | 已完成 |
| S6 冒烟验证清单 | 实现前后的运行态检查步骤 | 本文内已完成 |
| S7 提交前清单与提交模板 | 防止会话切换后遗漏验证与提交说明 | 本文内已完成 |

按这个标准，文档阶段在当前版本已经满足“可提交、可暂停、可恢复”的要求。

## 4. 切片总览

| Slice | 名称 | 状态 | 目标 |
|------|------|------|------|
| S0 | 文档与现状对齐 | 已完成 | 建立设计文档与实现拆解文档 |
| S1 | 详情基础字段落地 | 已完成 | `PublishedAt` / `Formats` 契约与 fallback 落地 |
| S2 | 详情页结构增强 | 已完成 | 右侧详情栏、格式区、更多内容区落地 |
| S3 | Fab API 主路径 enrichment | 进行中 | 非 fallback 路径也能补媒体图/格式/发布时间 |
| S4 | 列表页到详情页的上下文透传 | 未开始 | 为 detail enrichment 提供更稳定的 listing 锚点 |
| S5 | 更多内容的数据质量提升 | 未开始 | 降低“同作者更多内容”误匹配 |
| S6 | UI 冒烟验证与回归记录 | 已完成 | 把运行态检查流程固化 |
| S7 | 文档闭环与提交前检查 | 已完成 | 补齐模块文档、变更说明、提交检查单 |

### 4.1 子切片总览

以下子切片是后续恢复上下文时的首选入口。若用户说“继续做下一步”，默认从下一个未完成子切片开始，而不是重新理解整个 S3 或 S4。

| 子切片 | 所属 Slice | 状态 | 单次目标 |
|------|------|------|------|
| S3-A | S3 | 已完成 | 定义主路径 detail enrichment 结果模型与合并入口 |
| S3-B | S3 | 已完成 | 主路径补 Hero/截图媒体图 |
| S3-C | S3 | 未开始 | 主路径补 Formats / PublishedAt |
| S3-D | S3 | 未开始 | 为主路径 enrichment 补单测 |
| S4-A | S4 | 未开始 | 新增详情导航 payload |
| S4-B | S4 | 未开始 | 列表页导航切到 payload |
| S4-C | S4 | 未开始 | 详情页兼容 payload 与旧 assetId |
| S4-D | S4 | 未开始 | 为导航上下文透传做编译/冒烟验证 |
| S5-A | S5 | 未开始 | 更多内容去重与排除自身 |
| S5-B | S5 | 未开始 | 更多内容排序稳定化 |
| S5-C | S5 | 未开始 | 更多内容空结果与退化策略整理 |
| S6-A | S6 | 已完成 | 整理测试资产样本矩阵 |
| S6-B | S6 | 已完成 | 编写手工冒烟步骤 |
| S6-C | S6 | 已完成 | 记录失败信号与回退判定 |
| S7-A | S7 | 已完成 | 同步文档状态与入口 |
| S7-B | S7 | 已完成 | 提交前检查清单 |
| S7-C | S7 | 已完成 | 提交说明模板与完成记录 |

## 5. 细粒度切片定义

### S0 文档与现状对齐

- 状态：`已完成`
- 目标：确认高层设计存在，并新增本文档作为细粒度实施入口。
- 已落地产物：
  - [docs/review/16-FabDetailRichContentDesign.md](docs/review/16-FabDetailRichContentDesign.md)
  - [docs/review/17-FabDetailImplementationSlices.md](docs/review/17-FabDetailImplementationSlices.md)

### S1 详情基础字段落地

- 状态：`已完成`
- 目标：把详情页缺失的稳定字段补到 Contracts 和 fallback 数据链路。
- 已修改文件：
  - [src/Launcher.Application/Modules/FabLibrary/Contracts/FabModels.cs](../src/Launcher.Application/Modules/FabLibrary/Contracts/FabModels.cs)
  - [src/Launcher.Infrastructure/FabLibrary/FabApiClient.cs](../src/Launcher.Infrastructure/FabLibrary/FabApiClient.cs)
  - [src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs](../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs)
  - [src/Launcher.Infrastructure/FabLibrary/EpicOwnedFabCatalogClient.cs](../src/Launcher.Infrastructure/FabLibrary/EpicOwnedFabCatalogClient.cs)
- 完成标准：
  - `FabAssetDetail` 拥有 `PublishedAt`
  - `FabAssetDetail` 拥有 `Formats`
  - owned fallback 无图时能恢复首张预览图
  - 对应单测通过

### S2 详情页结构增强

- 状态：`已完成`
- 目标：把详情页改成更接近网页的双栏结构，并接入新的稳定字段。
- 已修改文件：
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml)
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml.cs)
- 完成标准：
  - 右侧详情栏显示版本/更新时间/发布时间/大小
  - 单独显示“包含格式”
  - 底部显示“更多内容”卡片区
  - 编译通过

### S3 Fab API 主路径 enrichment

- 状态：`进行中`
- 目标：当前 fallback 路径已经补强，但 Fab API 直连成功时仍可能只返回窄字段。此切片要补的是“主路径 detail enrichment”。
- 只允许做这些事：
  - 新增内部 enrichment 抽象
  - 在 `FabCatalogReadService.GetDetailAsync(...)` 中加入“主源后补全”编排
  - 只补媒体图、格式、发布时间这三类稳定信息
- 不允许做这些事：
  - 在 Page / ViewModel 里解析 HTML
  - 直接把网页 JSON blob 透传到 Contracts
  - 顺手做描述富文本渲染
- 目标文件：
  - [src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs](../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs)
  - [src/Launcher.Infrastructure/FabLibrary/FabPreviewMetadataResolver.cs](../src/Launcher.Infrastructure/FabLibrary/FabPreviewMetadataResolver.cs)
  - 如确有必要，再新增一个 Infrastructure 内部文件承载 detail enrichment resolver
- 最小完成标准：
  - 当 `Screenshots` 为空时，主路径可尝试补图
  - 当 `Formats` 为空时，主路径可尝试补格式
  - 当 `PublishedAt` 为空时，主路径可尝试补发布时间
  - 合并规则遵守“主源优先”
- 验证动作：
  - 新增或扩展 unit test，覆盖主路径补全逻辑
  - 至少执行一次定向 `dotnet test`

#### S3-A 定义 enrichment 结果模型与合并入口

- 状态：`已完成`
- 目标：先把“主路径补全”所需的最小内部模型立起来，但暂时不追求完整行为落地。
- 本轮只做：
  - 新增或整理内部结果类型，例如 `FabDetailEnrichmentResult`
  - 明确字段只包含 `MediaUrls`、`Formats`、`PublishedAt`
  - 在 `FabCatalogReadService` 中预留合并入口或合并辅助方法
- 本轮不做：
  - 真正读取 listing HTML
  - 修改 Presentation
  - 新增复杂缓存策略
- 目标文件：
  - [src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs](../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs)
  - 如有必要，新建一个 Infrastructure 内部文件承载 enrichment result / resolver
- 完成标准：
  - 代码里已经有稳定的 enrichment 结果模型
  - `FabCatalogReadService` 具备明确的 detail merge 落点
- 验证动作：
  - 编译通过即可

- 已完成结果：
  - 新增 `FabDetailEnrichmentContext`
  - 新增 `FabDetailEnrichmentResult`
  - 新增 `IFabDetailEnrichmentResolver` 与空实现
  - `FabCatalogReadService.GetDetailAsync(...)` 已接入 enrichment 入口与 merge helper
  - 定向 `FabCatalogReadServiceTests` 已通过

#### S3-B 主路径补媒体图

- 状态：`已完成`
- 目标：只解决主路径 `Screenshots` 为空时的 Hero/画廊补图。
- 本轮只做：
  - 复用现有 listing page 读取能力
  - 当主路径 `Screenshots.Count == 0` 时尝试补一张或多张媒体图
  - 合并时保持主源优先
- 本轮不做：
  - Formats
  - PublishedAt
  - 描述富文本
- 目标文件：
  - [src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs](../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs)
  - [src/Launcher.Infrastructure/FabLibrary/FabPreviewMetadataResolver.cs](../src/Launcher.Infrastructure/FabLibrary/FabPreviewMetadataResolver.cs)
- 完成标准：
  - 主路径空图时可以进入补图分支
  - 不覆盖已有主源截图
- 验证动作：
  - 至少 1 个定向单测
  - 执行一次定向 `dotnet test`

- 已完成结果：
  - 主路径详情在 `Screenshots` 为空时会先尝试使用缓存摘要中的 preview 锚点回填媒体图
  - 若当前主路径没有 preview 锚点，则会退回使用缓存摘要中的 `ThumbnailUrl`
  - 回填结果只在主源无截图时生效，不覆盖已有主源媒体图
  - 定向 `FabCatalogReadServiceTests` 已通过新增空图回填测试

#### S3-C 主路径补 Formats / PublishedAt

- 状态：`未开始`
- 目标：在 S3-B 之后，把同一条 enrichment 链继续用于格式和发布时间。
- 本轮只做：
  - 当 `Formats.Count == 0` 时补格式
  - 当 `PublishedAt` 缺失时补发布时间
  - 继续遵守主源优先
- 本轮不做：
  - 更多内容推荐逻辑
  - 新增对外 Contracts 字段
- 目标文件：
  - [src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs](../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs)
  - enrichment 内部实现文件
- 完成标准：
  - 主路径也能输出稳定的 `Formats`
  - 主路径也能输出稳定的 `PublishedAt`
- 验证动作：
  - 至少 1 个定向单测
  - 执行一次定向 `dotnet test`

#### S3-D 主路径 enrichment 单测收口

- 状态：`未开始`
- 目标：把 S3-B / S3-C 的逻辑正式压进测试，而不是只靠运行态观察。
- 本轮只做：
  - 为主路径空图补图增加测试
  - 为主路径空格式/空发布时间补全增加测试
  - 覆盖“不覆盖主源已有值”的合并规则
- 目标文件：
  - 与 `FabCatalogReadService` 对应的 unit test 文件
- 完成标准：
  - 主路径 enrichment 关键规则有测试覆盖
- 验证动作：
  - 执行定向 `dotnet test`

### S4 列表页到详情页的上下文透传

- 状态：`未开始`
- 目标：当前详情页导航主要只传 `assetId`。若要提高 detail enrichment 成功率，需要把列表页已有的 preview 锚点尽量带到详情页。
- 只允许做这些事：
  - 为导航参数增加一个稳定 DTO 或 route payload
  - 透传 `PreviewListingId`、`PreviewProductId`
  - 在详情页 ViewModel 中优先使用导航上下文，而不是重新猜测
- 目标文件：
  - [src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs)
  - [src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml.cs)
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
- 最小完成标准：
  - 详情页不再只依赖 `assetId`
  - 列表页点击进入详情时能带上 preview 锚点
  - 不破坏现有直接按 `assetId` 导航的兼容性
- 验证动作：
  - 编译通过
  - 如有测试切入点则补测试，否则至少做一次本地运行态冒烟

#### S4-A 新增详情导航 payload

- 状态：`未开始`
- 目标：定义一个最小 payload，把详情页需要的导航上下文固定下来。
- 本轮只做：
  - 新建 payload 类型
  - 字段只放 `AssetId`、`PreviewListingId`、`PreviewProductId`
  - 不改现有跳转调用点
- 本轮不做：
  - 修改详情页 ViewModel
  - 修改详情读取逻辑
- 目标文件：
  - Presentation 中与 Fab 详情导航相关的类型定义文件
- 完成标准：
  - payload 类型存在且命名清晰
  - 保留旧的 `assetId` 导航兼容思路
- 验证动作：
  - 编译通过

#### S4-B 列表页导航改用 payload

- 状态：`未开始`
- 目标：只修改列表页点击详情的入口，让它把 preview 锚点带过去。
- 本轮只做：
  - 从卡片 ViewModel 读取 `AssetId`、`PreviewListingId`、`PreviewProductId`
  - 导航时传 payload
- 本轮不做：
  - 详情页消费 payload
  - 更多内容点击跳转改造
- 目标文件：
  - [src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
  - [src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs)
- 完成标准：
  - 列表页到详情页的导航参数不再只有 `assetId`
- 验证动作：
  - 编译通过

#### S4-C 详情页消费 payload 并兼容旧路由

- 状态：`未开始`
- 目标：让详情页理解 payload，同时不破坏旧调用方。
- 本轮只做：
  - `FabAssetDetailPage` 支持接收 payload
  - `FabAssetDetailViewModel` 优先使用 payload 里的 preview 锚点
  - 若传入仍是字符串，继续按原逻辑工作
- 目标文件：
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml.cs)
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
- 完成标准：
  - 详情页具备双入口兼容能力
- 验证动作：
  - 编译通过
  - 至少一次本地冒烟

#### S4-D 导航透传验证收口

- 状态：`未开始`
- 目标：把导航透传这个变更变成可重复验证的结论。
- 本轮只做：
  - 记录列表页进入详情的验证步骤
  - 记录直接按 `assetId` 进入详情的兼容验证步骤
- 完成标准：
  - 两类入口都能进入详情页
  - 详情页不因 payload 改造崩溃
- 验证动作：
  - 实机运行 Launcher.App

### S5 更多内容的数据质量提升

- 状态：`未开始`
- 目标：当前“更多内容”区采用同作者精确匹配，能工作，但质量未必稳定。
- 只允许做这些事：
  - 优先排除当前资产本身
  - 增加更稳的排序策略，例如评分优先、标题去重、空图降级
  - 评估是否需要优先来源于当前分类或标签交集
- 不允许做这些事：
  - 新做推荐算法
  - 引入新的后端接口
  - 把更多内容扩展为复杂业务模块
- 目标文件：
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
- 最小完成标准：
  - “更多内容”列表更稳定，重复项更少
  - 当搜索路径失败时，退化策略仍可工作
- 验证动作：
  - 编译通过
  - 如果逻辑显著复杂化，需要补至少一个 unit test

#### S5-A 去重与排除自身

- 状态：`未开始`
- 目标：先让“更多内容”列表结果干净，不出现当前资产自己或重复项。
- 本轮只做：
  - 排除当前 `AssetId`
  - 对重复 `AssetId` / 标题做去重
- 本轮不做：
  - 改排序规则
  - 引入标签相似度
- 目标文件：
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
- 完成标准：
  - “更多内容”不再包含自己
  - 重复卡片明显减少
- 验证动作：
  - 编译通过

#### S5-B 排序稳定化

- 状态：`未开始`
- 目标：在结果已经干净的基础上，让顺序更接近用户感知上的“像网站”。
- 本轮只做：
  - 明确排序规则，例如评分优先、标题次排序、免费优先或非免费优先二选一
  - 固定排序规则，避免每次结果跳动
- 本轮不做：
  - 新接口
  - 复杂推荐
- 目标文件：
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
- 完成标准：
  - 同样输入得到稳定顺序
- 验证动作：
  - 编译通过
  - 如逻辑分支较多，则补测试

#### S5-C 空结果与退化策略整理

- 状态：`未开始`
- 目标：明确“没有更多内容时”的处理，不让页面显得异常。
- 本轮只做：
  - 明确无结果时隐藏区块还是显示空态
  - 明确在线搜索失败时是否退化到已拥有资产
- 目标文件：
  - [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs)
  - 如有必要，对应 XAML
- 完成标准：
  - 空结果行为稳定且不会报错
- 验证动作：
  - 编译通过
  - 最好做一次运行态验证

### S6 UI 冒烟验证与回归记录

- 状态：`已完成`
- 目标：把当前详情页功能变成可以重复执行的检查清单，防止后续改动回退。
- 只允许做这些事：
  - 编写运行态检查步骤
  - 记录预期现象和失败现象
  - 把测试资产样本写清楚
- 建议文档位置：
  - 追加到本文末尾，或单独新建 review 文档
- 最小完成标准：
  - 至少覆盖 3 类资产：有完整图、有 fallback 图、无更多内容
  - 每类资产记录进入方式、预期 UI、失败信号
- 验证动作：
  - 实机打开 Launcher.App 验证一次

#### S6-A 测试资产样本矩阵

- 状态：`已完成`
- 目标：先把验证对象定义清楚，避免每次都临时找资产。
- 本轮只做：
  - 列出 3 到 5 个代表性资产
  - 标明它们分别用于验证哪种场景
- 建议内容：
  - 完整图资产
  - 依赖 fallback 图资产
  - 无更多内容资产
  - 格式信息丰富资产
- 完成标准：
  - 验证样本可重复使用

- 推荐样本矩阵：

| 场景 | 资产筛选方式 | 重点验证项 | 通过信号 |
|------|------|------|------|
| 完整图资产 | 选择详情页已稳定返回多张截图的公开 Fab 资产 | Hero、画廊、右侧详情栏 | 首屏 Hero 非空，画廊可横向滚动 |
| fallback 图资产 | 选择列表页有缩略图但详情源可能缺图的已拥有资产 | 预览图回填链路 | Hero 不再空白，至少出现 1 张媒体图 |
| 格式丰富资产 | 选择网页侧栏能看到“包含格式”的资产 | `Formats` 显示 | “包含格式”区块出现且至少 1 个 chip |
| 无更多内容资产 | 选择作者资产较少或作者字段缺失的资产 | 底部更多内容退化行为 | 页面不报错，更多内容区块按预期隐藏 |
| 发布时间资产 | 选择网页详情中能看到“已发布”日期的资产 | `PublishedAt` 显示 | 右侧详情栏显示已发布日期 |

#### S6-B 手工冒烟步骤

- 状态：`已完成`
- 目标：把启动、进入详情、观察 UI 的步骤标准化。
- 本轮只做：
  - 列出进入路径
  - 列出每一步预期现象
  - 列出失败截图建议
- 完成标准：
  - 新会话无需重新摸索就能执行验证

- 标准冒烟步骤：

1. 启动 `Launcher.App`，确保当前账号已登录且 Fab 库可进入。
2. 从 Fab 列表页进入“完整图资产”详情页。
3. 检查 Hero、画廊、右侧详情栏、格式区、更多内容区是否按版式出现。
4. 返回列表页，再进入“fallback 图资产”详情页。
5. 检查 Hero 是否仍为空白；若不为空，记录首张图是否来自回填链路。
6. 进入“无更多内容资产”详情页。
7. 检查页面底部是否稳定隐藏更多内容区，或按设计展示空态，但不得抛错。
8. 如本轮涉及主路径 enrichment，再额外对一个非 owned fallback 资产做同样检查。

- 建议截图点位：

1. Hero 区与右侧详情栏同屏截图
2. 包含格式区截图
3. 更多内容区截图
4. 出现异常时的整页截图

#### S6-C 失败信号与回退判定

- 状态：`已完成`
- 目标：让后续会话能快速判断“这是 UI 问题还是数据问题”。
- 本轮只做：
  - 记录常见失败信号，例如空 Hero、格式缺失、更多内容为空、详情页崩溃
  - 记录每种失败优先检查的文件/模块
- 完成标准：
  - 失败定位路径清晰

- 失败信号与优先检查路径：

| 失败信号 | 优先检查位置 | 判断方向 |
|------|------|------|
| Hero 空白但详情页能打开 | [src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs](../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs) / [src/Launcher.Infrastructure/FabLibrary/FabPreviewMetadataResolver.cs](../src/Launcher.Infrastructure/FabLibrary/FabPreviewMetadataResolver.cs) | 先看数据补全链是否没进或没拿到图 |
| 包含格式区缺失 | [src/Launcher.Application/Modules/FabLibrary/Contracts/FabModels.cs](../src/Launcher.Application/Modules/FabLibrary/Contracts/FabModels.cs) / [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs) | 先看 Contracts 是否有值，再看 ViewModel 是否投影 |
| 已发布日期缺失 | [src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs](../src/Launcher.Infrastructure/FabLibrary/FabCatalogReadService.cs) / [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml) | 先区分是数据没拿到还是 UI 没显示 |
| 更多内容为空或重复 | [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs) | 优先检查筛选、去重、退化逻辑 |
| 详情页直接崩溃 | [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailPage.xaml) / [src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs](../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailViewModel.cs) | 先看绑定/空值处理，再看数据来源 |

### S7 文档闭环与提交前检查

- 状态：`已完成`
- 目标：避免实现做完后，文档、模块定义、测试说明和提交信息仍然断裂。
- 只允许做这些事：
  - 更新模块文档链接
  - 更新 review 文档状态
  - 形成提交前检查清单
- 目标文件：
  - [docs/06-ModuleDefinitions/FabLibrary.md](../docs/06-ModuleDefinitions/FabLibrary.md)
  - 本文档
- 最小完成标准：
  - 模块文档能同时指向设计稿和实施拆解稿
  - 本文状态表与实际代码状态一致

#### S7-A 文档状态同步

- 状态：`已完成`
- 目标：把所有文档里的状态和现状对齐。
- 本轮只做：
  - 更新本文中各切片状态
  - 更新模块文档入口说明
- 完成标准：
  - 文档不再出现“代码已完成但文档仍显示未开始”

#### S7-B 提交前检查清单

- 状态：`已完成`
- 目标：形成一个非常短但可执行的提交前核对清单。
- 本轮只做：
  - 列出需要检查的测试命令
  - 列出需要检查的文档
  - 列出需要检查的 UI 行为
- 完成标准：
  - 提交前可逐项勾选

- 提交前检查清单：

1. 运行 `dotnet test tests/Launcher.Tests.Unit/Launcher.Tests.Unit.csproj --filter "FullyQualifiedName~EpicOwnedFabCatalogClientTests|FullyQualifiedName~FabCatalogReadServiceTests"`
2. 若本轮只改主路径入口，至少再运行 `dotnet test tests/Launcher.Tests.Unit/Launcher.Tests.Unit.csproj --filter "FullyQualifiedName~FabCatalogReadServiceTests"`
3. 检查 [docs/review/16-FabDetailRichContentDesign.md](docs/review/16-FabDetailRichContentDesign.md) 与 [docs/review/17-FabDetailImplementationSlices.md](docs/review/17-FabDetailImplementationSlices.md) 是否仍一致
4. 检查 [docs/06-ModuleDefinitions/FabLibrary.md](../06-ModuleDefinitions/FabLibrary.md) 是否保留入口链接
5. 检查当前提交是否只包含 Fab 详情相关文件，没有意外混入无关改动
6. 若本轮改了 UI 绑定，至少做一次本地页面打开验证

#### S7-C 提交说明模板与完成记录

- 状态：`已完成`
- 目标：减少每次提交时重新组织描述的成本。
- 本轮只做：
  - 提供 commit message 模板
  - 提供变更摘要模板
  - 提供“本次完成了哪些子切片”的记录格式
- 完成标准：
  - 后续提交说明可直接套用

- Commit message 模板：

```text
Fab detail: <summary>
```

- 当前阶段推荐 commit message 示例：

```text
Fab detail: add design docs and enrichment scaffolding
```

- 变更摘要模板：

```text
范围：Fab 详情页
完成子切片：S0, S1, S2, S3-A
本次包含：
- 设计文档
- 细粒度实施拆解文档
- fallback 详情增强
- 详情页第一阶段 UI
- 主路径 enrichment 入口骨架
验证：
- <command>
- <result>
```

- 会话完成记录模板：

```text
本轮完成：S?-?
本轮未做：
下一轮建议：S?-?
风险：
```

## 6. 推荐会话粒度

为了防止上下文丢失，建议按以下粒度推进：

- 一次会话最多完成一个 `Slice`
- 若某个 `Slice` 涉及超过 4 个生产文件，进一步拆成 `Slice-A` / `Slice-B`
- 若某个切片已经触碰到 Contracts 和 Presentation，就不要在同一轮顺手补文档闭环

推荐顺序：

1. S3-A
2. S3-B
3. S3-C
4. S3-D
5. S4-A
6. S4-B
7. S4-C
8. S4-D
9. S5-A
10. S5-B
11. S5-C
12. S6-A
13. S6-B
14. S6-C
15. S7-A
16. S7-B
17. S7-C

## 7. 会话恢复模板

后续如果需要快速恢复上下文，可直接复用下面模板：

```text
当前任务：Fab 详情页富内容实现
当前切片：S?-?
切片目标：
已完成：
未完成：
目标文件：
本轮只做：
本轮不做：
验证方式：
```

## 8. 当前建议

如果接下来要继续推进，实现上最合适的下一步不是再改 UI，而是先做 `S3 Fab API 主路径 enrichment`。

更具体地说，下一步建议直接进入 `S3-C`，而不是一口气做完整个 S3。

原因只有一个：

- 现在 fallback 路径已经比主路径更“富”，这会造成不同来源详情数据质量不一致。

先把 S3-C 到 S3-D 做掉，后面的 S4 和 S5 才有稳定基础。

## 9. 文档阶段结论

当前仓库内与 Fab 详情页相关的“实现文档”已经具备以下能力：

1. 能说明为什么做以及不做什么。
2. 能把后续实现拆成单会话子切片。
3. 能在上下文丢失后快速恢复到下一个子切片。
4. 能指导提交前验证与提交说明编写。

因此，文档阶段到此可以认为已经完成，后续动作应切换为：

1. 提交并 push 当前文档与已有基础代码变更。
2. 通过窗口确认是否开始进入下一实现子切片。