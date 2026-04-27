# Fab 列表页性能止血执行记录

## 使用说明

这个文件不是设计文档，而是“上下文快满前必须先写”的本地执行记录。

固定规则：

1. 只记录事实，不写长篇推测。
2. 一旦当前子切片完成，同时同步 `CHANGELOG.md` 与 `SESSION_HANDOFF.md`。
3. 若只是做到一半，还没完成原子任务，就只更新本文，不提前污染其它交接文件。
4. 压缩上下文后，第一步先读 [20-FabLibraryPerformanceReliefImplementationSlices.md](20-FabLibraryPerformanceReliefImplementationSlices.md) 与本文，再继续。

## 当前状态

- 当前 Slice：`未开始`
- 当前子切片：`S1-A`
- 当前任务状态：`未开始`

## 当前假设

- Fab 列表当前的主要性能热点不是“整页 WebView”，而是缺图卡片进入视口后触发隐藏 `FabPreviewProbe` WebView2 读取 listing HTML。
- 第一阶段最小止血应当是：列表卡片停用 probe，缺图直接占位，详情主 Hero 保留真实预览恢复。

## 已完成项

- 已完成性能问题代码定位。
- 已完成性能止血实施拆解文档落地。
- 已完成本地执行记录模板落地。

## 进行中项

- 无。

## 最近修改文件

- `docs/review/20-FabLibraryPerformanceReliefImplementationSlices.md`
- `docs/review/21-FabLibraryPerformanceReliefExecutionLog.md`

## 最近验证

- 文档类变更，无编译验证要求。

## 下一步

- 从 `S1-A` 开始：在列表卡片 ViewModel 中引入显式 preview probe 策略位。

## 若被迫压缩上下文，压缩前至少补齐以下五项

1. 当前正在做的 `子切片`
2. 已经改过的文件列表
3. 已完成的验证动作
4. 当前阻塞点或未决问题
5. 压缩后恢复时的第一步