# 项目架构优化方案

> AI 模型：GPT-5 Codex
> 创建日期：2026-04-29
> 依据范围：`README.md`、`docs/` 非 review 文档、`docs/06-ModuleDefinitions/`、当前源码结构抽样
> 约束：本文档只规划架构优化，不要求一次性重写，不扩大到游戏商店或游戏库存功能

---

## 1. 优化目标

本项目的文档架构目标是清晰的：Windows 10/11 原生 WinUI 启动器，采用分层架构和模块化纵向切片，重点解决官方启动器常见的冷启动慢、页面卡死、下载刷新阻塞 UI、资产列表滚动掉帧等问题。

本轮优化以当前分析结论为基准，目标是让代码实现逐步回到文档承诺的架构边界上：

1. **解耦**：模块之间只依赖 Contracts，不引用对方内部实现，也不把领域实体直接泄漏给 UI。
2. **模块化**：每个模块有清楚的入口、用例、持久化端口、基础设施实现和 UI 投影。
3. **高扩展性**：新增模块或替换某个模块内部实现时，不牵动无关模块。
4. **高性能**：启动、下载、缩略图、列表渲染、后台任务都必须避免打爆 UI 线程。
5. **数据驱动**：API 端点、并发、缓存 TTL、预热策略、更新源、OAuth 行为尽量通过配置或 Options 管理，非必要不硬编码。
6. **AI 可持续协作**：任务必须可局部理解、可局部验证、可从 `SessionContextRecord.md` 恢复。

---

## 2. 当前架构评价

### 2.1 已具备的基础

项目已经具备良好的架构骨架：

- 解决方案拆为 `Launcher.App`、`Launcher.Presentation`、`Launcher.Application`、`Launcher.Domain`、`Launcher.Infrastructure`、`Launcher.Background`、`Launcher.Shared`。
- 文档定义了明确的层间依赖和模块间 Contracts 规则。
- Downloads、Installations 等核心领域已有 Domain 状态机。
- FabLibrary 已经围绕真实性能问题做了热恢复、缩略图缓存、列表 probe 止血等优化。
- SQLite、Dapper、Serilog、Polly、CommunityToolkit.Mvvm 的技术选型适合 Windows 桌面启动器。

### 2.2 主要偏差

当前源码和文档目标之间存在以下偏差：

| 编号 | 偏差 | 影响 |
|------|------|------|
| P-01 | `Launcher.Application` 基本空心化，业务编排大量落在 `Infrastructure` | 用例边界不清，基础设施层变成事实业务层 |
| P-02 | Contracts 中泄漏 Domain 类型，如 `DownloadTask`、`Installation`、`DownloadState` | UI 和跨模块调用方容易被内部状态机牵连 |
| P-03 | `Background` 没有统一任务宿主，App 直接 `Start()` Worker 且存在多处 `Task.Run` | 生命周期、取消、异常隔离和退出流程难统一 |
| P-04 | Downloads 调度器和真实执行链之间未完全闭环 | 下载 MVP 的运行行为可能和文档管线不一致 |
| P-05 | `Infrastructure.DependencyInjection` 过重，注册了几乎所有模块实现和硬编码端点 | 组合根难维护，数据驱动不足 |
| P-06 | `EpicOwnedFabCatalogClient`、`FabLibraryViewModel`、`DialogService` 等文件过大 | 单文件理解成本高，AI 修改风险高 |
| P-07 | Presentation 层部分直接引用 Domain 类型 | UI 投影和领域状态分离不彻底 |
| P-08 | README、技术栈文档、项目文件存在 .NET 8 / .NET 9、自包含发布等描述差异 | 后续协作容易误判当前真实基线 |

---

## 3. 目标架构

### 3.1 目标分层

优化后的推荐职责如下：

```text
Launcher.App
  只做 WinUI 宿主、组合根、启动阶段编排、单实例、托盘、全局异常兜底

Launcher.Presentation
  Page / ViewModel / UI service / UI-only bridge
  只消费 Application Contracts 中的 Request / Summary / Event / DTO

Launcher.Application
  模块用例、命令、查询、应用级编排、端口接口、跨模块契约
  不包含 HTTP、SQLite、WebView2、文件系统直接实现

Launcher.Domain
  领域实体、值对象、状态机、领域规则
  不被 UI 直接消费

Launcher.Infrastructure
  HTTP、SQLite、文件系统、系统进程、Token 存储、缓存、Options 实现
  实现 Application 定义的端口，不承载用户用例主流程

Launcher.Background
  统一后台任务宿主、Worker 生命周期、调度、周期任务、事件订阅

Launcher.Shared
  Result / Error / PagedResult / 日志上下文 / 最小稳定基础类型
```

### 3.2 目标模块通信

模块之间只保留四种通信方式：

1. **Query**：读取 Summary，不暴露内部实体。
2. **Command**：发起动作，返回 Result。
3. **Event**：通知已发生事实，不替代方法调用。
4. **Projection**：面向 UI 或其他模块的稳定投影。

Repository、HTTP client、WebView probe、SQLite migration、文件修复器等都属于模块内部实现或应用端口实现，不应作为跨模块契约暴露。

---

## 4. 优化路线

### Phase A：边界护栏和文档同步

目标：先建立保护网，防止后续重构越改越乱。

- 同步 README 和技术栈文档中的真实运行时基线。
- 新增架构边界检查测试，至少覆盖项目引用方向和禁用 namespace。
- 明确 `SessionContextRecord.md` 的上下文恢复协议。
- 梳理当前 “public Contracts” 和 “内部端口” 的边界清单。

### Phase B：Application 层补实

目标：让业务流程从 Infrastructure 回到 Application。

- 按模块引入用例类或应用服务。
- 将 `DownloadCommandService`、`InstallCommandService`、`FabAssetCommandService` 等逐步迁为 Application 编排。
- Infrastructure 只保留技术实现，例如 HTTP client、SQLite repository、文件系统、进程启动。
- 每迁移一个模块，就补对应单元测试。

### Phase C：Contracts 去 Domain 泄漏

目标：让 Presentation 和跨模块调用方只看稳定 DTO。

- 将 UI 需要的状态和标识移动到 Application Contracts。
- Repository 端口与跨模块 Contracts 分离。
- Presentation 不再引用 `Launcher.Domain.*`。
- Download / Install / Engine / Plugin 的 UI 状态统一通过 Summary 呈现。

### Phase D：后台任务统一宿主

目标：消除分散的 `Task.Run` 和手动 `Start()`。

- 引入 `IBackgroundTaskHost`、`IBackgroundWorker`、`WorkerStatus`。
- 将 TokenRefresh、AutoInstall、AppUpdate、NetworkMonitor、FabWarmup 纳入统一宿主。
- App 只启动宿主，不直接启动每个 Worker。
- 退出时由宿主统一取消、等待、记录错误。

### Phase E：Downloads 管线闭环

目标：让下载从 Start 到 Chunk 执行、Checkpoint、RuntimeStore、完成事件形成可运行闭环。

- 明确 Scheduler 到 Worker 的连接方式。
- 将并发、chunk 大小、重试、checkpoint 周期从配置读取。
- 补崩溃恢复、暂停、恢复、取消的端到端测试。
- 保证 UI 只接收 500ms 级别聚合快照。

### Phase F：配置和 Options 数据驱动

目标：减少硬编码。

- 建立 `EpicApiOptions`、`FabApiOptions`、`DownloadOptions`、`CacheOptions`、`UpdateOptions`、`AuthOptions`。
- API BaseAddress、GitHub owner/repo、缓存 TTL、curl fallback、WebView probe 参数、后台检查周期全部从配置进入。
- 对配置加验证，启动时记录配置来源和有效值摘要，敏感字段脱敏。

### Phase G：大类拆分和性能收敛

目标：降低单文件复杂度，继续提升 Fab 和启动性能。

- 拆分 `EpicOwnedFabCatalogClient`。
- 拆分 `FabLibraryViewModel` 中的搜索状态、快照恢复、缩略图卡片逻辑。
- 拆分 `DialogService` 中普通对话框和 Epic WebView2 登录窗口。
- 用性能日志固化冷启动、Fab 首屏、详情返回、下载刷新等指标。

---

## 5. 优先级建议

| 优先级 | 工作 | 原因 |
|--------|------|------|
| P0 | `SessionContextRecord.md` 协议、架构边界测试、文档基线同步 | 防止后续 AI 会话失控 |
| P0 | Background 统一宿主 | 直接影响启动、退出、后台错误和长任务安全 |
| P1 | Application 层补实 | 这是当前最大架构偏差 |
| P1 | Contracts 去 Domain 泄漏 | 影响解耦和长期演化 |
| P1 | Downloads 管线闭环 | 启动器核心能力，必须可信 |
| P2 | Options 数据驱动 | 提升可配置性和可部署性 |
| P2 | 大类拆分 | 降低维护成本，提升 AI 协作稳定性 |

---

## 6. 验收指标

### 6.1 架构指标

- `Launcher.Presentation` 不引用 `Launcher.Domain.*` 命名空间。
- `Launcher.Application` 不引用 `Launcher.Infrastructure`、`Launcher.Presentation`。
- 跨模块公共 Contracts 不返回领域实体。
- `Launcher.App` 不直接启动具体 Worker，只启动后台宿主。
- `Infrastructure.DependencyInjection` 中的硬编码端点显著减少，端点来自 Options。

### 6.2 性能指标

- 冷启动到窗口可见继续保持接近当前水平，不因架构重构退化。
- Fab 列表 100+ 资产虚拟化不卡顿。
- 下载进度 UI 更新不超过 500ms 级别频率。
- 后台 Worker 启动不阻塞首帧。

### 6.3 协作指标

- 每个原子任务都能独立构建或独立测试。
- 每个阶段完成后更新 `SessionContextRecord.md`。
- 上下文压缩后，必须先读取 `docs/SessionContextRecord.md` 再继续执行。

---

## 7. 风险控制

1. **不做一次性大重构**：所有工作必须按实现文档拆成小任务。
2. **保持可运行主干**：每个原子任务结束后至少运行聚焦 build/test。
3. **先加护栏再迁移**：边界测试先于大规模移动代码。
4. **先保留兼容层**：迁移 Contracts 时可短期保留旧成员，但必须记录移除计划。
5. **不碰 review 文档作为架构依据**：review 目录只作为历史记录，不作为本方案的设计来源。
6. **遇到上下文风险先记录**：详细状态写入 `docs/SessionContextRecord.md` 后再等待压缩或继续。
