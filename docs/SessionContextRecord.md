# SessionContextRecord

> AI 模型：GPT-5 Codex
> 用途：记录架构优化任务的关键上下文，防止会话压缩或上下文爆满后丢失状态。
> 铁律：上下文压缩后，必须先读取本文件，再继续执行任何任务。

---

## 1. 恢复协议

新会话或压缩恢复后，严格按以下顺序执行：

1. 读取本文件。
2. 读取 `docs/17-ArchitectureOptimizationPlan.md`。
3. 读取 `docs/18-ArchitectureOptimizationImplementation.md`。
4. 检查 `git status --short`。
5. 只继续“当前任务”中记录的任务。
6. 若当前任务为空，等待用户指定，或从实现文档的下一个未完成任务开始前先确认。

---

## 2. 固定约束

- 本项目是 WinUI 制作的 Epic 启动器 Win10/Win11 版。
- 本轮架构优化以此前分析结果为基准。
- 不包含游戏商店和游戏库存模块。
- `docs/review/` 目录不是本次架构方案依据，除非用户明确要求读取。
- 任何代码实现前都必须先读对应模块文档。
- 不删除任何文件，除非用户明确要求。
- 不做一次性大重构。
- 每个原子任务必须小、可验证、可恢复。
- 上下文将要爆满时，必须先更新本文件。

---

## 3. 当前基线摘要

### 3.1 已完成的本轮工作

- 已完成项目整体架构分析。
- 已创建架构优化方案：`docs/17-ArchitectureOptimizationPlan.md`。
- 已创建架构优化实现拆解：`docs/18-ArchitectureOptimizationImplementation.md`。
- 已创建本上下文记录文件：`docs/SessionContextRecord.md`。

### 3.2 关键分析结论

- 文档层面的架构目标成熟：分层架构 + 模块化纵向切片。
- 当前最大实现偏差是 `Launcher.Application` 过薄，`Launcher.Infrastructure` 过重。
- Background 层没有统一后台任务宿主，App 直接启动多个 Worker。
- Contracts 中存在 Domain 类型泄漏，Presentation 也有直接引用 Domain 的情况。
- Downloads 调度和真实执行链需要进一步确认并闭环。
- 硬编码端点、缓存时间、OAuth 默认值、WebView probe 参数需要逐步 Options 化。
- 大类风险集中在 `EpicOwnedFabCatalogClient`、`FabLibraryViewModel`、`DialogService`。

---

## 4. 当前任务状态

| 字段 | 内容 |
|------|------|
| 当前阶段 | 文档规划阶段 |
| 当前任务 | 创建架构优化方案、实现文档、SessionContextRecord |
| 当前状态 | 已完成 |
| 下一步 | 等待用户确认文档结构；若进入执行阶段，从 `docs/18-ArchitectureOptimizationImplementation.md` 的 Task 0.1 开始 |
| 阻塞项 | 无 |

---

## 5. 最近触碰文件

| 文件 | 操作 | 说明 |
|------|------|------|
| `docs/17-ArchitectureOptimizationPlan.md` | 新增 | 项目架构优化总方案 |
| `docs/18-ArchitectureOptimizationImplementation.md` | 新增 | 原子任务实现拆解 |
| `docs/SessionContextRecord.md` | 新增 | 上下文恢复记录 |

---

## 6. 最近验证

当前只新增文档，未运行 build/test。已执行以下只读检查：

```powershell
git status --short
Get-Content .\docs\SessionContextRecord.md -Encoding UTF8
Select-String -Path .\docs\17-ArchitectureOptimizationPlan.md,.\docs\18-ArchitectureOptimizationImplementation.md,.\docs\SessionContextRecord.md -Pattern '上下文压缩|SessionContextRecord|原子任务|铁律'
```

检查结果摘要：

- 三份文档已创建。
- 未发现未完成占位词。
- `SessionContextRecord.md` 铁律已写入方案文档和实现文档。
- `git status --short` 显示本轮新增 3 个文档；仓库中还存在此前未由本轮创建的既有未提交改动。

---

## 7. 未完成事项

- 用户确认是否认可文档结构和执行顺序。
- 若进入执行阶段，从 `docs/18-ArchitectureOptimizationImplementation.md` 的 Task 0.1 开始。
- 执行任何任务前，先更新本文件的“当前任务状态”。

---

## 8. 压缩前必须补充的信息

如果上下文将要爆满，请在压缩前补齐以下内容：

1. 当前正在执行的 Task 编号。
2. 已修改但未验证的文件。
3. 最近一次成功命令。
4. 最近一次失败命令和错误摘要。
5. 下一步只能做什么。
6. 哪些文件绝对不能碰。
7. 用户新增的最新约束。
