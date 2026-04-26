# Fab 列表页热恢复与启动预热细粒度实施拆解

## 1. 目的

这不是策略稿，而是一份用于“防上下文爆满”的实施拆解文档。

适用场景：

- 单次会话只能完成一个很小的实现切片
- 需要把任务拆到足够原子，避免 AI 一次跨太多文件导致漂移
- 需要在多轮会话之间快速恢复上下文，并知道下一步该改什么文件

本文默认以以下文档为强约束来源：

- [18-FabLibraryWarmResumeStrategy.md](18-FabLibraryWarmResumeStrategy.md)
- [../14-AntiPatterns.md](../14-AntiPatterns.md)
- [../02-ArchitecturePrinciples.md](../02-ArchitecturePrinciples.md)
- [../04-ModuleDependencyRules.md](../04-ModuleDependencyRules.md)
- [../06-ModuleDefinitions/FabLibrary.md](../06-ModuleDefinitions/FabLibrary.md)
- [../10-StartupPipeline.md](../10-StartupPipeline.md)

若本文与以上文档冲突，以架构规约、模块规约、策略文档为准。

## 2. 使用规则

每次会话只做一个子切片，严格遵守以下规则：

1. 一次只选择一个 `子切片` 开工，不跨到下一个子切片。
2. 单个子切片原则上不超过 `3` 个生产文件；若超过，必须继续拆。
3. 先改“目标文件”，再做“验证动作”；验证失败只修当前子切片。
4. 不得为了热恢复顺手重构 `NavigationService`、Settings 架构或整个启动管线。
5. 不得为了省事把 `FabLibraryViewModel` 改成全局静态单例。
6. 不得在 Page Code-Behind 中写 HTTP、缓存解析、鉴权或复杂业务编排。
7. 启动预热必须放在 Phase 3，不得阻塞 Phase 0/1 首帧与可交互时间。
8. 若某一子切片仍然偏大，优先继续细拆，而不是硬做完。

推荐状态值：

- `未开始`
- `进行中`
- `已完成`
- `已阻塞`

## 3. 当前代码现状快照

以下现状已经在代码中确认，后续切片必须建立在这些事实之上：

1. [../../src/Launcher.Presentation/DependencyInjection.cs](../../src/Launcher.Presentation/DependencyInjection.cs) 当前把 `FabLibraryViewModel` 注册为 `Transient`。
2. [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs) 当前在 `Page_Loaded` 中无条件执行 `LoadCommand`，并在 `Page_Unloaded` 中调用 `ViewModel.Dispose()`。
3. [../../src/Launcher.Presentation/Shell/Navigation/NavigationService.cs](../../src/Launcher.Presentation/Shell/Navigation/NavigationService.cs) 当前没有 Fab 专属的热恢复语义，只有普通 `_frame.Navigate(...)`。
4. [../../src/Launcher.Application/Modules/Settings/Contracts/ConfigModels.cs](../../src/Launcher.Application/Modules/Settings/Contracts/ConfigModels.cs) 当前只有 `Download / Appearance / Path / Network` 四组配置，没有 Fab 热恢复预热配置。
5. [../../src/Launcher.Infrastructure/Settings/SettingsService.cs](../../src/Launcher.Infrastructure/Settings/SettingsService.cs) 与 [../../src/Launcher.Infrastructure/Settings/UserSettings.cs](../../src/Launcher.Infrastructure/Settings/UserSettings.cs) 当前也没有 Fab 配置持久化位。
6. [../../src/Launcher.App/App.xaml.cs](../../src/Launcher.App/App.xaml.cs) 当前 Phase 3 只启动后台 Worker，Fab 首屏预热仍是 TODO。
7. [../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailNavigationPayload.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabAssetDetailNavigationPayload.cs) 已经存在，因此首期实现不应为了“返回列表”凭空发明新的跨模块契约。

## 4. 本轮实施的边界决策

为避免过度设计，本轮实现明确采用以下边界：

### 4.1 主路径先做“快照恢复”，不先赌页面实例常驻

首期主路径不依赖 `NavigationCacheMode.Required`，也不要求 `FabLibraryPage` 常驻内存。

优先级是：

1. 页面离开前把“可恢复快照”存进 Presentation 内的会话 Store。
2. 页面回来时先恢复快照，再按快照年龄决定是否后台刷新。

这样做的好处是：

1. 不需要把 VM 提升成全局单例。
2. 不需要改造通用导航层。
3. 即便页面实例被销毁，也能稳定恢复可见内容。

### 4.2 首期只做“单槽位”列表会话，不引入全局 resume token 体系

当前应用只有一个 Fab 列表主路由，没有多标签、多并发列表上下文。

因此首期只做单槽位 `SessionStateStore`：

1. 保存最近一次 Fab 列表会话快照。
2. 返回列表时直接恢复该快照。
3. 只有当未来出现多上下文并发需求时，才升级为带 `SnapshotId` 的方案。

### 4.3 启动预热必须通过小型协调器接入，不能把 Fab 逻辑直接堆进 App

`App.xaml.cs` 只能负责启动阶段编排，不负责 Fab 查询逻辑本身。

因此启动预热要通过一个小型协调器服务实现，`App` 只负责在 Phase 3 调它一次。

### 4.4 缓存层级保持三层，不新增跨层共享可变对象

缓存职责保持如下：

1. L1：Presentation `SessionStateStore` 保存可恢复快照。
2. L2：Infrastructure `FabCatalogReadService` 继续负责目录数据缓存。
3. L3：缩略图缓存继续由 `IThumbnailCacheService` 负责。

禁止把 Card VM、`BitmapImage`、可变领域对象直接塞进 L1。

## 5. 切片总览

| Slice | 名称 | 状态 | 目标 |
|------|------|------|------|
| S0 | 文档与入口对齐 | 已完成 | 建立策略文档与实现拆解文档的双入口 |
| S1 | 会话快照骨架 | 已完成 | 定义快照 DTO、Store 接口、内存实现、DI 注册 |
| S2 | 列表恢复与写回 | 已完成 | `FabLibraryViewModel` 具备 Restore/Save 主路径 |
| S3 | 视口与返回体验 | 已完成 | 返回列表后恢复滚动位置，进入详情前先保存快照 |
| S4 | SWR 刷新策略 | 未开始 | 按快照年龄区分 Fresh / Warm / Stale |
| S5 | 失效与容量控制 | 未开始 | 防止快照无限增长、跨账号串态、长期脏数据 |
| S6 | 设置开关接入 | 未开始 | 增加 `FabLibrary.AutoWarmOnStartup` 设置项 |
| S7 | 启动预热协调器 | 未开始 | 在 Phase 3 背景预热 Fab 首屏但不导航 |
| S8 | 验证与提交流程 | 未开始 | 固化单测、冒烟、日志点、提交前检查 |
| S9 | 页面缓存试验（可选） | 未开始 | 仅在主路径完成后，再评估是否启用 `NavigationCacheMode.Required` |

### 5.1 子切片总览

| 子切片 | 所属 Slice | 状态 | 单次目标 |
|------|------|------|------|
| S1-A | S1 | 已完成 | 定义会话快照 DTO |
| S1-B | S1 | 已完成 | 定义会话 Store 接口 |
| S1-C | S1 | 已完成 | 实现内存版会话 Store |
| S1-D | S1 | 已完成 | 在 Presentation DI 注册会话 Store |
| S2-A | S2 | 已完成 | `FabLibraryViewModel` 注入 Store 与恢复守卫字段 |
| S2-B | S2 | 已完成 | 把快照 `FabAssetSummary` 恢复成卡片列表 |
| S2-C | S2 | 已完成 | 首次加载先尝试恢复快照 |
| S2-D | S2 | 已完成 | 搜索/翻页成功后写回快照 |
| S3-A | S3 | 已完成 | 页面离开前保存滚动位置 |
| S3-B | S3 | 已完成 | 页面恢复后回滚到上次位置 |
| S3-C | S3 | 已完成 | 点击详情前先固化最新快照 |
| S4-A | S4 | 未开始 | 定义快照年龄分类辅助逻辑 |
| S4-B | S4 | 未开始 | Fresh 快照直接展示，不发刷新 |
| S4-C | S4 | 未开始 | Warm 快照先展示，再静默刷新 |
| S4-D | S4 | 未开始 | Warm 刷新失败不覆盖当前可见列表 |
| S4-E | S4 | 未开始 | Stale 快照走完整加载 |
| S5-A | S5 | 未开始 | 限制快照页数和卡片数量 |
| S5-B | S5 | 未开始 | 为快照增加账号作用域并做失效判定 |
| S5-C | S5 | 未开始 | 增加 Clear/Trim 的日志与显式清理路径 |
| S6-A | S6 | 未开始 | 增加 `FabLibraryConfig` 配置模型与读写契约 |
| S6-B | S6 | 未开始 | 完成 Settings 持久化与默认值 |
| S6-C | S6 | 未开始 | SettingsViewModel 暴露预热开关 |
| S6-D | S6 | 未开始 | SettingsPage 增加预热开关 UI |
| S7-A | S7 | 未开始 | 新增 Fab 预热协调器服务 |
| S7-B | S7 | 未开始 | Phase 3 调用预热协调器 |
| S7-C | S7 | 未开始 | 预热跳过条件与静默日志收口 |
| S8-A | S8 | 未开始 | 为 Session Store / 年龄策略补单测 |
| S8-B | S8 | 未开始 | 为 ViewModel Restore/SWR 补单测 |
| S8-C | S8 | 未开始 | 为设置持久化与预热协调器补单测 |
| S8-D | S8 | 未开始 | 形成手工冒烟清单与提交前检查 |
| S9-A | S9 | 未开始 | 单独评估 `NavigationCacheMode.Required` |
| S9-B | S9 | 未开始 | 对比内存与返回耗时数据后决定保留或回退 |

## 6. 细粒度切片定义

### S0 文档与入口对齐

- 状态：`已完成`
- 目标：把策略文档、模块文档、实施拆解文档挂成统一入口。
- 已落地产物：
  - [18-FabLibraryWarmResumeStrategy.md](18-FabLibraryWarmResumeStrategy.md)
  - [19-FabLibraryWarmResumeImplementationSlices.md](19-FabLibraryWarmResumeImplementationSlices.md)
  - [../06-ModuleDefinitions/FabLibrary.md](../06-ModuleDefinitions/FabLibrary.md)

### S1 会话快照骨架

- 状态：`未开始`
- 目标：先把“可保存的东西”和“保存到哪里”固定下来，不急着接页面行为。

#### S1-A 定义会话快照 DTO

- 状态：`已完成`
- 目标：新增一个只承载可恢复 UI 状态的窄快照模型。
- 本轮只做：
  - 新增 `FabLibrarySessionSnapshot`
  - 包含查询条件、分页信息、摘要结果、滚动位置、时间戳、账号作用域
  - 结果集合类型只允许 `IReadOnlyList<FabAssetSummary>` 或等价窄 DTO
- 本轮不做：
  - 保存 `FabAssetCardViewModel`
  - 保存 `BitmapImage`
  - 保存任何服务实例或委托
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs)
- 完成标准：
  - 快照字段足以恢复“首屏可见内容 + 查询态 + 滚动位置”
  - 快照对象本身不持有可变 UI 对象
- 验证动作：
  - 编译通过

- 已完成结果：
  - 已新增 `FabLibrarySessionSnapshot`
  - 当前字段已覆盖 `Keyword / Category / SortOrder / CurrentPage / TotalPages / HasNextPage / TotalCount / VerticalOffset / SnapshotAtUtc / AccountScopeKey / AssetSummaries`
  - 结果集合复用现有 `FabAssetSummary`，未新增重复 DTO

#### S1-B 定义会话 Store 接口

- 状态：`已完成`
- 目标：把 Session Store 的职责压缩成最小接口。
- 本轮只做：
  - 新增 `IFabLibrarySessionStateStore`
  - 只暴露 `Save / TryGet / Clear / Trim` 这类最小方法
- 本轮不做：
  - 数据库持久化
  - 跨模块公开 Contracts
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/IFabLibrarySessionStateStore.cs](../../src/Launcher.Presentation/Modules/FabLibrary/IFabLibrarySessionStateStore.cs)
- 完成标准：
  - 接口足够支撑列表恢复，但没有演化成万能缓存中心
- 验证动作：
  - 编译通过

- 已完成结果：
  - 已新增 `IFabLibrarySessionStateStore`
  - 当前接口只暴露 `Save / TryGet / Clear / Trim`
  - 接口保持在 Presentation 内部，未升级为跨模块 Contracts

#### S1-C 实现内存版会话 Store

- 状态：`已完成`
- 目标：先做进程内单槽位实现，不做磁盘持久化。
- 本轮只做：
  - 实现 `InMemoryFabLibrarySessionStateStore`
  - 原子替换当前快照
  - 预留 `Trim` 与容量控制入口
- 本轮不做：
  - 多快照 LRU
  - 持久化到 JSON/SQLite
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/InMemoryFabLibrarySessionStateStore.cs](../../src/Launcher.Presentation/Modules/FabLibrary/InMemoryFabLibrarySessionStateStore.cs)
- 完成标准：
  - 单槽位保存/读取/清理闭环存在
  - 不引入静态全局字段
- 验证动作：
  - 编译通过

- 已完成结果：
  - 已新增 `InMemoryFabLibrarySessionStateStore`
  - 当前实现采用进程内单槽位快照 + `lock` 互斥保护
  - `Trim()` 当前保留为扩展点，不提前引入多快照或容量策略

#### S1-D 在 Presentation DI 注册 Store

- 状态：`已完成`
- 目标：把 Session Store 注册成 `Singleton`，但不改动 `FabLibraryViewModel` 的 `Transient` 生命周期。
- 目标文件：
  - [../../src/Launcher.Presentation/DependencyInjection.cs](../../src/Launcher.Presentation/DependencyInjection.cs)
- 完成标准：
  - `IFabLibrarySessionStateStore` 已注册
  - `FabLibraryViewModel` 仍保持 `Transient`
- 验证动作：
  - 编译通过

- 已完成结果：
  - `IFabLibrarySessionStateStore` 已注册为 `Singleton`
  - `InMemoryFabLibrarySessionStateStore` 已作为默认实现接入 Presentation DI
  - `FabLibraryViewModel` 仍保持 `Transient`

### S2 列表恢复与写回

- 状态：`未开始`
- 目标：让 `FabLibraryViewModel` 具备“先恢复、后查询、成功即写回”的主路径。

#### S2-A 注入 Store 与恢复守卫字段

- 状态：`已完成`
- 目标：先把 ViewModel 内部的最小控制位补齐。
- 本轮只做：
  - 注入 `IFabLibrarySessionStateStore`
  - 增加 `IsRestoredFromSnapshot`、`_forceNetworkReload` 之类的内部守卫字段
- 本轮不做：
  - SWR 后台刷新
  - 滚动位置恢复
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - ViewModel 已能访问 Session Store
  - 后续恢复逻辑有明确插入点
- 验证动作：
  - 编译通过

- 已完成结果：
  - `FabLibraryViewModel` 已注入 `IFabLibrarySessionStateStore`
  - 已补充 `_isRestoredFromSnapshot`、`_forceNetworkReload` 两个恢复守卫字段
  - 已补充内部只读属性，作为后续切片与测试的稳定观察点
  - 为兼容公开构造函数依赖链，`FabLibrarySessionSnapshot` 与 `IFabLibrarySessionStateStore` 已调整为 `public`，但仍然留在 Presentation 层，不升级到 Application `Contracts`

#### S2-B 快照摘要恢复成卡片列表

- 状态：`已完成`
- 目标：从快照中的 `FabAssetSummary` 重新构造卡片 VM。
- 本轮只做：
  - 新增 `RestoreAssetsFromSnapshot(...)` 一类辅助方法
  - 恢复 `Assets / HasAssets / IsEmpty / CurrentPage / TotalCount / HasNextPage`
- 本轮不做：
  - 缩略图预热重写
  - 详情页逻辑改造
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 给定快照即可恢复一屏卡片可见内容
- 验证动作：
  - 编译通过

- 已完成结果：
  - `FabLibraryViewModel` 已新增从 `FabLibrarySessionSnapshot` 恢复卡片列表和分页状态的辅助方法
  - 当前恢复逻辑已覆盖 `Assets / CurrentPage / TotalPages / HasNextPage / TotalCount / HasAssets / IsEmpty`
  - 本轮仍未把恢复入口接入 `LoadAsync`，调用时机继续留给 `S2-C`

#### S2-C 首次加载先尝试恢复快照

- 状态：`已完成`
- 目标：把快照恢复插到 `LoadAsync` 的最前面。
- 本轮只做：
  - `LoadAsync` 先调用 `TryRestore...`
  - 恢复成功时先让用户看到内容，再决定是否刷新
- 本轮不做：
  - 年龄策略分类
  - 启动预热
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 回到 Fab 页面时，存在快照则不先落回空骨架
- 验证动作：
  - 编译通过

- 已完成结果：
  - `LoadAsync` 已在首页查询前优先尝试恢复会话快照
  - 快照恢复成功时，本轮会先展示恢复内容，并跳过首页资产查询
  - 当前仍只保留分类加载；后续刷新策略继续留给 `S4` 收口

#### S2-D 搜索/翻页成功后写回快照

- 状态：`已完成`
- 目标：把快照写回放到成功路径上，而不是散落在页面事件里。
- 本轮只做：
  - 在 `SearchInternalAsync(...)` 成功后统一写回
  - 手动刷新时允许跳过旧快照并在成功后覆盖新快照
- 本轮不做：
  - 失败重试策略扩展
  - 容量裁剪复杂规则
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 首屏、翻页、刷新三条成功路径都会更新快照
- 验证动作：
  - 编译通过

- 已完成结果：
  - 成功查询路径在 `UpdatePageState(...)` 之后统一写回会话快照
  - `FabAssetCardViewModel` 已支持导出 `FabAssetSummary`，保证快照可完整重建当前列表
  - 当前快照已覆盖首屏、翻页、手动刷新三类成功路径

### S3 视口与返回体验

- 状态：`未开始`
- 目标：列表内容恢复后，还要尽量回到用户离开前的视口位置。

#### S3-A 页面离开前保存滚动位置

- 状态：`已完成`
- 目标：在最小改动下把当前 `VerticalOffset` 写回 Session Store。
- 本轮只做：
  - 在 `FabLibraryPage` 离开路径中采集 `AssetScrollViewer.VerticalOffset`
  - 通过 ViewModel 小型方法写回快照
- 本轮不做：
  - 在 Code-Behind 拼装业务对象
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs)
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 页面离开前能更新快照中的 `VerticalOffset`
- 验证动作：
  - 编译通过

- 已完成结果：
  - `FabLibraryViewModel` 已支持带 `VerticalOffset` 写回当前会话快照
  - `FabLibraryPage` 在 `Page_Unloaded` 中会先保存 `AssetScrollViewer.VerticalOffset`，再执行 `Dispose()`
  - 当前离开页面时，快照已不再固定写回 `0`

#### S3-B 页面恢复后回滚到上次位置

- 状态：`已完成`
- 目标：在内容恢复并布局完成后，把 ScrollViewer 拉回旧位置。
- 本轮只做：
  - 在页面加载完成后的安全时机执行 `ChangeView`
  - 只在确有快照偏移量时恢复
- 本轮不做：
  - 滚动动画优化
  - 复杂的锚点对齐算法
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs)
- 完成标准：
  - 从详情页或其他页面回到列表时，视口能回到上次位置附近
- 验证动作：
  - 编译通过

- 已完成结果：
  - `FabLibraryViewModel` 已支持暂存一次性消费的恢复滚动偏移量
  - `FabLibraryPage` 在 `LoadAsync` 完成后会尝试消费该偏移量，并调用 `AssetScrollViewer.ChangeView(...)`
  - 当前只有在偏移量大于 `0` 时才触发回滚，避免对顶部位置做无意义恢复

#### S3-C 点击详情前先固化最新快照

- 状态：`已完成`
- 目标：避免用户点进详情时，最后一次滚动位置还没写入快照。
- 本轮只做：
  - 在 `AssetCard_Tapped` 导航前，先保存当前滚动位置和快照时间戳
  - 继续复用现有 `FabAssetDetailNavigationPayload`
- 本轮不做：
  - 发明新的 resume token 契约
  - 改造 `NavigationService`
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs)
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 点击卡片进入详情前，列表快照已是最新态
- 验证动作：
  - 编译通过

- 已完成结果：
  - `FabLibraryPage.AssetCard_Tapped(...)` 现在会在导航前先写回当前滚动位置和最新快照
  - 导航到详情页前，列表快照时间戳与 `VerticalOffset` 都会以当前时刻为准
  - 至此 `S3` 视口与返回体验切片已经完整闭环

### S4 SWR 刷新策略

- 状态：`未开始`
- 目标：把“有快照”再拆成 `Fresh / Warm / Stale` 三种行为，而不是一刀切。

#### S4-A 定义快照年龄分类辅助逻辑

- 状态：`未开始`
- 目标：先把年龄判断收敛成一个小型 helper，不散落到 ViewModel 各分支。
- 本轮只做：
  - 定义 `Fresh / Warm / Stale` 三种分类
  - 固定阈值：`30s / 5m`
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySnapshotAgePolicy.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySnapshotAgePolicy.cs)
- 完成标准：
  - 年龄分类逻辑集中在单一文件中
- 验证动作：
  - 编译通过

#### S4-B Fresh 快照直接展示，不发刷新

- 状态：`未开始`
- 目标：年龄足够新时，完全避免额外网络请求。
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - `age <= 30s` 时恢复后不再触发首轮刷新
- 验证动作：
  - 编译通过

#### S4-C Warm 快照先展示，再静默刷新

- 状态：`未开始`
- 目标：用户先看到旧快照，随后后台刷新第一页。
- 本轮只做：
  - 恢复快照后后台执行第一页刷新
  - 刷新时不清空当前 `Assets`
- 本轮不做：
  - Toast 文案打磨
  - 多页并行刷新
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - `30s < age <= 5m` 时能看到“秒开 + 静默更新”
- 验证动作：
  - 编译通过

#### S4-D Warm 刷新失败不覆盖当前可见列表

- 状态：`未开始`
- 目标：后台刷新失败时，保留原快照并给轻提示。
- 本轮只做：
  - 复用已有 `INotificationService`
  - 后台刷新失败时不切回整页错误态
- 本轮不做：
  - 新增页面级 Error Banner 样式
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - Warm 刷新失败后已有卡片仍保留可见
- 验证动作：
  - 编译通过

#### S4-E Stale 快照走完整加载

- 状态：`未开始`
- 目标：快照过旧时，不再假装热恢复成功。
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - `age > 5m` 时走完整加载路径
- 验证动作：
  - 编译通过

### S5 失效与容量控制

- 状态：`未开始`
- 目标：防止会话快照无限增长、跨账号串态、长期脏数据常驻。

#### S5-A 限制快照页数与卡片数量

- 状态：`未开始`
- 目标：把会话快照限制在“首屏可恢复”而不是“永久列表镜像”。
- 本轮只做：
  - 最多保留前 `3` 页
  - 最多保留 `60` 条卡片摘要
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/InMemoryFabLibrarySessionStateStore.cs](../../src/Launcher.Presentation/Modules/FabLibrary/InMemoryFabLibrarySessionStateStore.cs)
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs)
- 完成标准：
  - 快照数据量有硬上限
- 验证动作：
  - 编译通过

#### S5-B 账号作用域失效判定

- 状态：`未开始`
- 目标：避免 A 账号的快照被 B 账号直接复用。
- 本轮只做：
  - 给快照加 `AccountScopeKey`
  - `FabLibraryViewModel` 恢复前校验当前账号作用域是否匹配
- 本轮不做：
  - 登录模块重构
  - 复杂跨模块事件总线
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibrarySessionSnapshot.cs)
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 账号切换后旧快照不会直接复用
- 验证动作：
  - 编译通过

#### S5-C Clear/Trim 日志与显式清理路径

- 状态：`未开始`
- 目标：Store 的清理行为要可观测，而不是 silent state change。
- 本轮只做：
  - `Save / Clear / Trim / ScopeMismatch` 打日志
  - 明确手动刷新、登出或重置配置时哪些路径应触发清理
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/InMemoryFabLibrarySessionStateStore.cs](../../src/Launcher.Presentation/Modules/FabLibrary/InMemoryFabLibrarySessionStateStore.cs)
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryViewModel.cs)
- 完成标准：
  - 快照保存/清理/裁剪都有结构化日志
- 验证动作：
  - 编译通过

### S6 设置开关接入

- 状态：`未开始`
- 目标：让“启动后自动预热 Fab 列表”成为显式配置，而不是硬编码策略。

#### S6-A 增加 FabLibrary 配置模型与读写契约

- 状态：`未开始`
- 目标：先把配置契约补齐。
- 本轮只做：
  - 新增 `FabLibraryConfig`
  - `ISettingsReadService` 增加 `GetFabLibraryConfig()`
  - `ISettingsCommandService` 增加 `UpdateFabLibraryConfigAsync(...)`
- 目标文件：
  - [../../src/Launcher.Application/Modules/Settings/Contracts/ConfigModels.cs](../../src/Launcher.Application/Modules/Settings/Contracts/ConfigModels.cs)
  - [../../src/Launcher.Application/Modules/Settings/Contracts/ISettingsReadService.cs](../../src/Launcher.Application/Modules/Settings/Contracts/ISettingsReadService.cs)
  - [../../src/Launcher.Application/Modules/Settings/Contracts/ISettingsCommandService.cs](../../src/Launcher.Application/Modules/Settings/Contracts/ISettingsCommandService.cs)
- 完成标准：
  - Settings Contracts 已正式承载 Fab 预热配置
- 验证动作：
  - 编译通过

#### S6-B 完成 Settings 持久化与默认值

- 状态：`未开始`
- 目标：把 Fab 配置接入现有 `user.settings.json` 管线。
- 本轮只做：
  - `UserSettings` 增加 `FabLibrary`
  - `SettingsService` 支持读写该配置段
  - 默认值为 `AutoWarmOnStartup = false`
- 目标文件：
  - [../../src/Launcher.Infrastructure/Settings/UserSettings.cs](../../src/Launcher.Infrastructure/Settings/UserSettings.cs)
  - [../../src/Launcher.Infrastructure/Settings/SettingsService.cs](../../src/Launcher.Infrastructure/Settings/SettingsService.cs)
- 完成标准：
  - 配置能持久化并在重启后保留
- 验证动作：
  - 编译通过

#### S6-C SettingsViewModel 暴露预热开关

- 状态：`未开始`
- 目标：在设置页 VM 中暴露一个简单布尔开关。
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/Settings/SettingsViewModel.cs](../../src/Launcher.Presentation/Modules/Settings/SettingsViewModel.cs)
- 完成标准：
  - 可以读取/保存 `AutoWarmOnStartup`
- 验证动作：
  - 编译通过

#### S6-D SettingsPage 增加预热开关 UI

- 状态：`未开始`
- 目标：在现有设置页中提供最小 UI 入口。
- 本轮只做：
  - 在 Settings 页面增加一个 ToggleSwitch
  - 文案明确为“启动后自动预热 Fab 列表”
- 本轮不做：
  - 二级高级设置页
  - 复杂说明面板
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/Settings/SettingsPage.xaml](../../src/Launcher.Presentation/Modules/Settings/SettingsPage.xaml)
  - [../../src/Launcher.Presentation/Modules/Settings/SettingsPage.xaml.cs](../../src/Launcher.Presentation/Modules/Settings/SettingsPage.xaml.cs)
- 完成标准：
  - 用户可以在设置页显式控制该开关
- 验证动作：
  - 编译通过

### S7 启动预热协调器

- 状态：`未开始`
- 目标：在不污染 `App.xaml.cs` 的前提下，把 Phase 3 背景预热接进来。

#### S7-A 新增 Fab 预热协调器服务

- 状态：`未开始`
- 目标：新增一个小型协调器，负责“读取设置 -> 判断条件 -> 拉首屏 -> 写快照”。
- 本轮只做：
  - 新增 `FabLibraryWarmupCoordinator`
  - 依赖 `ISettingsReadService`、`IFabCatalogReadService`、`IFabLibrarySessionStateStore`、`IAuthService`
  - 只预热默认首屏查询，不做路由切换
- 本轮不做：
  - 预热多分类、多排序、多页
  - UI 控件直接操作
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryWarmupCoordinator.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryWarmupCoordinator.cs)
  - [../../src/Launcher.Presentation/DependencyInjection.cs](../../src/Launcher.Presentation/DependencyInjection.cs)
- 完成标准：
  - 预热逻辑不直接堆在 `App.xaml.cs`
- 验证动作：
  - 编译通过

#### S7-B Phase 3 调用预热协调器

- 状态：`未开始`
- 目标：把预热调用放到启动流程正确位置。
- 本轮只做：
  - `StartBackgroundServicesAsync(...)` 在现有 Worker 启动后调用协调器
  - 保持调用为后台、非阻塞
- 本轮不做：
  - 改写 Phase 0/1
  - 提前到主窗口显示前执行
- 目标文件：
  - [../../src/Launcher.App/App.xaml.cs](../../src/Launcher.App/App.xaml.cs)
- 完成标准：
  - 预热只发生在 Phase 3
- 验证动作：
  - 编译通过

#### S7-C 预热跳过条件与静默日志收口

- 状态：`未开始`
- 目标：把“不开启 / 未登录 / 离线 / 已有新鲜快照”都统一收敛成静默跳过。
- 本轮只做：
  - 明确跳过条件
  - 只记录日志，不弹错误
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryWarmupCoordinator.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryWarmupCoordinator.cs)
- 完成标准：
  - 预热失败或跳过都不影响启动体验
- 验证动作：
  - 编译通过

### S8 验证与提交流程

- 状态：`未开始`
- 目标：在实现前就把验证方式和提交边界写死，防止后面边做边飘。

#### S8-A Session Store / 年龄策略单测

- 状态：`未开始`
- 目标：给最纯的逻辑先补单测。
- 建议目标文件：
  - `tests/Launcher.Tests.Unit/FabLibrarySessionStateStoreTests.cs`
  - `tests/Launcher.Tests.Unit/FabLibrarySnapshotAgePolicyTests.cs`
- 完成标准：
  - 覆盖 `Save / TryGet / Clear / Trim`
  - 覆盖 `Fresh / Warm / Stale` 边界
- 验证动作：
  - 定向 `dotnet test`

#### S8-B ViewModel Restore/SWR 单测

- 状态：`未开始`
- 目标：把“先恢复、后刷新”的主路径压进测试。
- 建议目标文件：
  - `tests/Launcher.Tests.Unit/FabLibraryViewModelWarmResumeTests.cs`
- 完成标准：
  - 覆盖无快照 / Fresh / Warm / Stale 四条路径
  - 覆盖“Warm 刷新失败不清空已有列表”
- 验证动作：
  - 定向 `dotnet test`

#### S8-C 设置持久化与预热协调器单测

- 状态：`未开始`
- 目标：把“配置保存”和“Phase 3 预热条件分支”补进测试。
- 建议目标文件：
  - `tests/Launcher.Tests.Unit/SettingsServiceFabLibraryConfigTests.cs`
  - `tests/Launcher.Tests.Unit/FabLibraryWarmupCoordinatorTests.cs`
- 完成标准：
  - 覆盖开关开启/关闭
  - 覆盖未登录、离线、已有新鲜快照三种跳过条件
- 验证动作：
  - 定向 `dotnet test`

#### S8-D 手工冒烟清单与提交前检查

- 状态：`未开始`
- 目标：把运行态验收与提交边界固化，避免后续忘记。
- 本轮必须形成以下手工检查步骤：
  1. 首次进入 Fab：无快照时正常骨架加载。
  2. 进入详情再返回：列表秒开且滚动位置恢复。
  3. 切到其他模块再回 Fab：Fresh/Warm/Stale 三种年龄行为符合预期。
  4. Warm 刷新失败：已有列表不被清空，只显示轻提示。
  5. 关闭并重开应用：若开启预热，首次进入 Fab 首屏明显更快。
  6. 退出登录或切换账号：旧快照不串到新账号。
- 提交前检查：
  1. 更新 [../../CHANGELOG.md](../../CHANGELOG.md)
  2. 更新 [../../SESSION_HANDOFF.md](../../SESSION_HANDOFF.md)
  3. 定向 `dotnet build`
  4. 定向 `dotnet test`
  5. 再做一次最小手工冒烟

### S9 页面缓存试验（可选）

- 状态：`未开始`
- 目标：只有在 S1-S8 已完成后，才评估是否要为 `FabLibraryPage` 单独开启 `NavigationCacheMode.Required`。

#### S9-A 单独评估 `NavigationCacheMode.Required`

- 状态：`未开始`
- 目标：把页面实例缓存当成增量优化，而不是主路径前提。
- 目标文件：
  - [../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs](../../src/Launcher.Presentation/Modules/FabLibrary/FabLibraryPage.xaml.cs)
  - 如有必要，再补 1 个相关辅助文件
- 完成标准：
  - 只影响 `FabLibraryPage`，不波及全站导航
- 验证动作：
  - 手工冒烟 + 内存观察

#### S9-B 以数据决定保留或回退

- 状态：`未开始`
- 目标：记录缓存页前后“返回耗时”和“内存占用”的差异，再决定要不要留。
- 完成标准：
  - 若收益不明显或引入新泄漏，立即回退

## 7. 推荐执行顺序

严格按以下顺序推进：

1. `S1-A → S1-B → S1-C → S1-D`
2. `S2-A → S2-B → S2-C → S2-D`
3. `S3-A → S3-B → S3-C`
4. `S4-A → S4-B → S4-C → S4-D → S4-E`
5. `S5-A → S5-B → S5-C`
6. `S6-A → S6-B → S6-C → S6-D`
7. `S7-A → S7-B → S7-C`
8. `S8-A → S8-B → S8-C → S8-D`
9. `S9-A → S9-B` 仅在前面全部完成且有证据需求时再做

## 8. 第一批建议开工点

如果下一轮开始实现，默认从以下最小闭环启动：

1. `S4-A`，定义快照年龄分类辅助逻辑
2. `S4-B`，Fresh 快照直接展示
3. `S4-C`，Warm 快照先展示再静默刷新
4. `S4-D`，Warm 刷新失败不覆盖当前列表

原因：

1. `S3` 已经完成，当前返回列表的内容与滚动位置恢复链已经打通。
2. 接下来可以专注 `S4` 的年龄分类和后台刷新，不必再回头补返回上下文。
3. `S4-A` 到 `S4-D` 仍然可以保持很小的单步推进。
