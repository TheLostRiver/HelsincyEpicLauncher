# Fab 列表页热恢复与缓存策略（替代无脑懒加载）

> 本文只定义方案边界、行为策略与约束。若要按“一个很小的原子任务”推进实现，请配套阅读 [19-FabLibraryWarmResumeImplementationSlices.md](19-FabLibraryWarmResumeImplementationSlices.md)。

## 1. 问题定义

当前体验问题集中在两条路径：

1. 从其他栏目切回 Fab 列表页后，列表重新加载，等待明显。
2. 进入 Fab 详情页再返回列表页后，列表重新加载，等待明显。

这说明当前链路更接近“每次进入都重新初始化”，而不是“可恢复的页面会话”。

## 2. 现状根因（基于当前代码）

### 2.1 Presentation 生命周期导致列表态丢失

- `FabLibraryViewModel` 当前注册为 `Transient`。
- `FabLibraryPage` 在 `Unloaded` 时调用 `ViewModel.Dispose()`。
- `Page_Loaded` 每次都会执行 `LoadCommand`。

结果：页面状态（资产卡片、筛选条件、分页进度、滚动位置）无法跨路由返回复用。

### 2.2 导航层没有“页面会话复用”语义

`NavigationService` 每次 `NavigateAsync(...)` 都走 `_frame.Navigate(...)`，没有针对 Fab 列表的“热恢复”策略。

### 2.3 仅有数据层缓存，不等于 UI 立即可恢复

`FabCatalogReadService` 虽有 5 分钟内存缓存，但这只能减少网络请求，不会自动保留：

- UI 列表实例
- 卡片 VM 状态
- 滚动位置
- 页面交互上下文

因此用户仍感知“重新加载”。

## 3. 设计目标

1. 返回 Fab 列表页时优先“秒开可见内容”。
2. 不牺牲架构边界：UI 仍只处理状态与绑定，不写网络/解析业务。
3. 在可控内存预算下提供热恢复，而不是永久堆积对象。
4. 支持 `Stale-While-Revalidate`：先展示旧快照，再后台刷新。

## 4. 非目标

1. 不引入跨模块共享可变对象。
2. 不把 Fab 变成全局静态单例 VM。
3. 不把所有页面都改成永久缓存。
4. 不新增后端 API。

## 5. 总体方案：Warm Resume + SWR（分层缓存）

### 5.1 L1：页面会话快照缓存（Presentation 内）

新增 `IFabLibrarySessionStateStore`（Presentation 层接口 + 实现，`Singleton`），保存：

- 查询状态：`Keyword/Category/Sort/Page`。
- 结果快照：`IReadOnlyList<FabAssetSummary>`（建议最多保留前 2-3 页）。
- UI 状态：`TotalCount/HasNextPage`。
- 视口状态：`VerticalOffset`。
- 快照时间戳：`SnapshotAtUtc`。

约束：仅保存“可重建 UI 的窄 DTO”，不保存可变业务对象。

### 5.2 L2：现有目录数据缓存（Infrastructure）

继续使用 `FabCatalogReadService` 现有 5 分钟缓存，作为数据源兜底。

### 5.3 L3：缩略图文件缓存（现有）

继续使用 `IThumbnailCacheService` 的本地缓存，避免回页后二次下载图片。

## 6. 页面行为策略（关键）

### 6.1 首次进入 Fab

- 无会话快照：按当前流程加载第一页。
- 加载成功后写入 `SessionStateStore`。

### 6.2 从详情返回 Fab（热恢复路径）

- 优先从 `SessionStateStore` 立即恢复列表与分页状态。
- 立即恢复滚动位置。
- 若快照年龄 <= `HotResumeTtl`（建议 30 秒）：不阻塞刷新。
- 若快照年龄 > `HotResumeTtl`：后台静默刷新第一页并合并更新。

### 6.3 从其他栏目切回 Fab

- 若快照年龄 <= `WarmTtl`（建议 5 分钟）：先显示快照，再后台刷新。
- 若快照失效：显示骨架并重新加载。

## 7. 导航与生命周期改造建议

### 7.1 首期实现不依赖页面实例常驻

现状在 `Unloaded` 调 `Dispose()` 会结束当前页面实例；但首期热恢复并不要求页面实例常驻。

建议分两阶段：

1. 首期主路径：允许页面实例按当前生命周期结束，但必须在离开前把“可恢复快照”写入 `SessionStateStore`。
2. 可选增强：若主路径完成后仍存在明显抖动，再单独评估 `NavigationCacheMode.Required` 或其他有限页面缓存策略。

这样可以先解决“回页秒开”的主问题，而不把方案绑定到页面缓存或 VM 常驻上。

### 7.2 Fab 列表页启用有限缓存（可选）

可评估 `NavigationCacheMode.Required` 仅用于 `FabLibraryPage`。

注意：

- 这是“有限页面缓存”，不是全站缓存。
- 仍需会话快照作为退路，不能把策略只押注在 Page Cache。

2026-04-27 评估结论：当前基线不保留 `NavigationCacheMode.Required`。

原因：

1. 现有 `SessionStateStore + SWR` 已经覆盖返回列表页的主路径，不依赖页面实例常驻也能恢复可见内容与滚动位置。
2. 页面缓存虽然能减少一次 `LoadCommand` 与卡片重建，但会额外保留 `FabLibraryPage / FabLibraryViewModel / 已加载缩略图 BitmapImage` 等 UI 对象图，生命周期成本明显高于当前快照方案。
3. 在当前自动化环境里无法完成可信的 WinUI 交互式返回耗时与内存观测，因此本轮按保守策略回退缓存页实验；若后续人工数据表明仍有必要，再只对 `FabLibraryPage` 重新开启。

## 8. 刷新策略（SWR）

定义三段策略：

1. `Fresh`：`age <= 30s`，直接用快照，不刷新。
2. `Warm`：`30s < age <= 5m`，先显示快照，再静默刷新。
3. `Stale`：`age > 5m`，走完整加载流程。

后台刷新必须满足：

- 不清空当前列表导致闪烁。
- 刷新失败不覆盖已有快照，仅给轻提示（可复用现有通知机制）。

## 9. 详情往返优化点

当从列表点击详情时，额外保存：

- 当前列表路由状态快照 ID
- 当前滚动位置

返回时优先使用该快照 ID 恢复，而不是重新查第一页。

## 10. 与架构规约的一致性

本方案满足：

1. `02-ArchitecturePrinciples.md`
   - UI 仍只绑定与状态恢复，不新增 HTTP/SQL/解析逻辑。
2. `04-ModuleDependencyRules.md`
   - 不新增跨模块直连；Fab 列表仍通过 Contracts 获取数据。
3. `14-AntiPatterns.md`
   - 避免“无脑懒加载导致反复重建”与“全局静态状态横飞”。
   - 不引入 God Service。

## 10.1 用户可控预热开关（新增建议）

建议在设置页增加开关：

- 名称：启动后自动预热 Fab 列表
- 配置键：`FabLibrary.AutoWarmOnStartup`
- 默认值：`false`

### 行为定义

1. 开启：应用启动后在后台触发一次 Fab 列表首屏预热（不抢焦点，不切路由）。
2. 关闭：保持按需加载，首次进入 Fab 时再加载。
3. 无网络或未登录：跳过预热并记录日志，不弹错误。

### 为什么要做成开关

1. 有些用户追求“打开即秒进 Fab”，愿意换取少量启动后台流量。
2. 有些用户更在意启动时资源占用，应该保留完全按需加载路径。
3. 让策略可配置，避免“一刀切”引发新的性能争议。

### 与热恢复策略的关系

这个开关是“首进体验加速器”，不是热恢复本身：

1. 热恢复解决“切回页面慢”。
2. 启动预热解决“本次会话第一次进入 Fab 慢”。

两者组合可以覆盖首次进入和往返进入两类慢路径。

## 11. 实施切片概览（详细版见 19）

详细的原子任务拆解、目标文件、完成标准与验证动作见 [19-FabLibraryWarmResumeImplementationSlices.md](19-FabLibraryWarmResumeImplementationSlices.md)。

### W1：会话状态接口与内存实现

- 新增 `IFabLibrarySessionStateStore`。
- 支持 `Save/Restore/Clear`。

### W2：FabLibraryViewModel 接入 Restore/Save

- `LoadAsync` 开始前先尝试恢复快照。
- 搜索/分页后更新快照。

### W3：滚动位置恢复

- 页面离开前写 `VerticalOffset`。
- 页面恢复后回滚到上次位置。

### W4：SWR 后台刷新

- 对 `Warm` 快照做静默刷新，不打断可见内容。

### W5：释放策略与内存预算

- 限制快照容量（例如最多 60~80 卡）。
- 记录内存占用与命中率。

### W6：设置开关与启动预热（可选）

- 在 Settings 增加 `FabLibrary.AutoWarmOnStartup`。
- 在启动后台阶段按开关决定是否触发 Fab 首屏预热。
- 预热只写入 SessionState，不触发页面导航。

## 12. 验收指标（必须可测）

1. 返回 Fab 列表首屏可见时间：目标 < 200ms（热路径）。
2. 进入详情再返回列表：目标不出现骨架全量重刷。
3. 连续切换栏目 5 次：列表状态保持稳定，内存不持续线性增长。
4. 网络异常时：保留上次快照可见，不崩溃。

## 13. 为什么“无脑懒加载”不可取

“无脑懒加载”只解决“初次加载时少做事”，但不能解决“页面会话可恢复”。

它的副作用是：

1. 每次回页都重新触发懒加载链，导致体感反复等待。
2. 缺少状态保存，用户上下文（滚动位置/筛选）丢失。
3. UI 响应不稳定，用户会感知为“卡、慢、抖动”。

正确方向是“懒加载 + 热恢复 + 后台刷新”的组合，而不是单点优化。

## 14. 结论

Fab 列表性能问题的最优解不是继续加懒加载，而是建立可恢复的页面会话层。

推荐采用：

- `SessionStateStore` 保存列表会话
- `SWR` 提供可见即返回
- 默认不保留页面缓存；仅在后续有明确人工测量收益时，再重新评估有限页面缓存 + 明确释放策略

这套方案能在不破坏现有模块边界的前提下，显著改善“切页回来就重载”的核心痛点。