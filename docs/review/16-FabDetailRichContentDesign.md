# Fab 详情富内容设计

## 1. 背景

> 若需要按“小任务、可断点恢复”的方式推进实现，请同时查看 [17-FabDetailImplementationSlices.md](17-FabDetailImplementationSlices.md)。

当前 Fab 详情页已经有基础壳层：

- Hero 图
- 描述
- 截图画廊
- 技术细节
- 兼容引擎版本
- 标签

但运行态仍存在两个关键缺口：

1. 部分资产详情没有介绍图 / 画廊图，页面只剩空白 Hero 区。
2. 页面能展示的详情字段仍明显少于 Fab 网站详情页，尤其缺少“包含格式”“更完整的详情元数据”等富内容。

根因不是单纯 XAML 没有布局，而是当前 `FabAssetDetail` 契约和 detail 数据恢复链路仍是收缩版：

- Fab 网站 API 详情模型只暴露了窄字段。
- Epic owned fallback 详情只映射了 `KeyImages`、`Description`、`ReleaseNote` 等基础信息。
- 当前缺少一个专门面向“详情富内容”的 enrichment 链路。

## 2. 目标

本轮设计只解决 Fab 详情页的“富内容恢复”，不扩展到交易能力。

### 2.1 UI 目标

详情页第一阶段应稳定展示：

- Hero 主图
- 横向媒体画廊
- 标题 / 作者 / 分类语义
- 描述
- 标签
- 技术细节
- 兼容引擎版本
- 详情元数据：最近更新、首次发布、版本、大小
- 包含格式（例如 Unreal Engine、FBX、OBJ 等）
- 底部“更多内容”区，优先展示同作者资产摘要卡片

### 2.2 非目标

以下内容明确不在本轮范围内：

- 购买 / 许可选择 / 购物车
- Fab 网页端完整商业侧栏复刻
- 在 Page / Code-Behind 中直接抓网页或解析 HTML
- 绕过 Contracts 让 Presentation 直接依赖 Infrastructure 实现

这与 `docs/14-AntiPatterns.md`、`docs/02-ArchitecturePrinciples.md`、`docs/04-ModuleDependencyRules.md` 保持一致。

## 3. UI 方案

详情页继续保留“单页纵向滚动”结构，但信息层级调整为：

1. 顶部 Hero + 基础摘要区
2. 左侧主内容：描述、媒体画廊、技术详情、兼容性、标签
3. 右侧摘要栏：标题、作者、评分、操作按钮、详情元数据、包含格式
4. 底部“更多内容”区

### 3.1 Hero 与媒体画廊

- Hero 始终优先使用第一张媒体图。
- 若当前详情源没有 `KeyImages`，则允许通过 listing enrichment 恢复媒体图。
- Hero 与画廊共用同一组稳定媒体 URL，不维护两套来源。

### 3.2 详情元数据

第一阶段保持“窄而稳定”的只读投影，不做自由表单式渲染。建议固定字段：

- 最近更新
- 首次发布
- 当前版本
- 下载大小

### 3.3 包含格式

- 独立于标签展示。
- 表现为 chip / badge 列表。
- 语义上属于“资产可交付格式”，不是分类标签。

### 3.4 更多内容

- 参考网页详情页底部卡片区，但第一阶段只做只读摘要卡片，不复刻网页端轮播与商业入口。
- 数据优先复用现有 `FabAssetSummary`，避免引入新的 UI 专用 DTO。
- 主策略为“同作者精确匹配”；若在线搜索不可用，则退化为已拥有资产中的同作者项。

## 4. 架构方案

### 4.1 分层责任

#### Presentation

- `FabAssetDetailPage.xaml` 只负责布局和绑定。
- `FabAssetDetailViewModel` 只负责页面状态转换和 UI 投影，不解析 HTML，不做 HTTP。

#### Application Contracts

- 继续由 `IFabCatalogReadService.GetDetailAsync(...)` 作为唯一详情读取入口。
- 对外升级 `FabAssetDetail`，补足详情页所需的稳定字段。

#### Infrastructure

- `FabCatalogReadService` 继续做主编排。
- 详情读取采用“主源 + enrichment”策略：
  1. 优先使用当前详情源（Fab API 或 Epic owned fallback）。
  2. 若媒体图或富元数据缺失，再进入内部 enrichment。
- enrichment 只在 Infrastructure 内部发生，不泄漏实现细节到 Presentation。

### 4.2 推荐内部组件

建议新增内部抽象：

- `IFabDetailEnrichmentResolver`

职责：

- 基于 listing page HTML 恢复详情富内容
- 只返回窄 DTO / projection
- 不直接触碰 UI

建议返回类型：

- `FabDetailEnrichmentResult`

内容建议包含：

- `MediaUrls`
- `Formats`
- `PublishedAt`

必要时可追加少量稳定字段，但禁止把整份网页 JSON blob 往上冒。

### 4.3 浏览器上下文复用

当前仓库已经有：

- `IFabListingPageReadService`
- `FabListingPageReadService`

因此详情 enrichment 应复用现有 browser-context listing 读取能力，而不是重新引入第二套 WebView2 抓页机制。

这满足以下约束：

- 不在 Page / ViewModel 里写业务抓取逻辑
- 不让 Infrastructure 反向依赖 Presentation 具体类型
- 继续通过 Contracts 注入 browser-context 能力

## 5. 契约升级建议

`FabAssetDetail` 第一阶段建议新增：

- `PublishedAt`
- `Formats`

保留现有：

- `Screenshots`
- `Description`
- `TechnicalDetails`
- `Tags`
- `SupportedEngineVersions`

这样 UI 仍消费稳定投影，而不是网页模型原样透传。

## 6. 数据恢复策略

### 6.1 主源优先

若当前详情源已有完整信息：

- 直接使用
- 不额外触发 enrichment

### 6.2 稀疏数据补全

当任一条件成立时进入 enrichment：

- `Screenshots.Count == 0`
- `Formats.Count == 0`
- `PublishedAt` 缺失

### 6.3 合并规则

- 媒体 URL 去重后合并
- `Formats` 去重后合并
- 主源已存在的稳定字段优先，不被 enrichment 低质量结果覆盖

## 7. 反模式规避说明

本设计显式规避以下反模式：

- AP-02：不在 Page Code-Behind 写抓取/解析业务
- AP-03：不把详情 enrichment 全塞进 `FabAssetDetailViewModel`
- AP-07：不让 Repository / ApiClient 直接返回 UI 模型
- P-01：不跨模块依赖内部实现
- P-05：不让 Infrastructure 反向依赖 Presentation 具体类

## 8. 实施顺序

第一阶段：

1. 升级 `FabAssetDetail` 契约
2. 增加内部 detail enrichment resolver
3. 在 `FabCatalogReadService.GetDetailAsync(...)` 中做稀疏补全
4. 更新详情页 UI，新增“包含格式”、更完整的元数据区和底部“更多内容”区
5. 添加针对 fallback/detail enrichment 的单元测试

第二阶段（可选）：

1. 若 listing HTML 中可恢复更稳定的“详情分组”，再考虑引入分组化 detail sections
2. 再评估是否需要 richer markdown / html 描述渲染

## 9. 验收标准

满足以下条件即可认为第一阶段达标：

- owned fallback 详情页不再频繁出现空白 Hero
- 详情页能显示“包含格式”
- 详情页能显示“最近更新 / 首次发布 / 版本 / 大小”中的稳定字段
- 详情 enrichment 不把业务逻辑塞进 Presentation
- 变更可通过单元测试和宿主构建验证