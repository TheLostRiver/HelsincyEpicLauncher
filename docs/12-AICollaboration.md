# AI 协作规则

> 本文档定义 AI 辅助编码时必须遵守的约束和工作流程。  
> 当代码量到达数万行以上时，AI 的上下文限制会成为主要风险源。  
> 这套规则的目标是：让 AI 在局部模块内安全工作，避免因信息丢失产生幻觉性 bug。

---

## 1. AI 编码五条铁律

### AI-01：单模块原则

每次任务只允许 AI 操作 **一个模块**，除非明确声明是"跨模块契约调整"。

```
✅ "修改 Downloads 模块的重试策略"
✅ "重构 Installations 模块的校验逻辑"
❌ "同时改 Downloads 和 FabLibrary 的代码"（除非确认是契约变更）
```

### AI-02：先读文档再写代码

AI 改模块前，必须先读：

1. 该模块的 `README_ARCH.md`（架构定义文档）
2. 该模块的 `Contracts/` 目录（对外接口）
3. 当前任务相关的接口和类
4. 相关测试文件

### AI-03：不跨模块加依赖

AI 不得新增对其他模块 **内部实现** 的依赖。

```
✅ AI 在 FabLibrary 中调用 IDownloadReadService（Contracts）
❌ AI 在 FabLibrary 中 import Downloads.Infrastructure.ChunkDownloadClient
```

### AI-04：同步更新文档

AI 新增/修改公共接口时，必须同步更新对应的 API 文档。

### AI-05：声明影响面

AI 改动跨模块契约时，必须在提交说明中列出：

- 哪些模块受影响
- 哪些测试需要更新
- 哪些 UI 投影会变化

---

## 2. AI 友好的工程结构

### 2.1 模块自描述

每个模块附带三个文档，方便 AI 快速理解上下文：

```
Modules/Downloads/
├─ README_ARCH.md    → 模块职责、依赖、禁止项、状态机
├─ README_API.md     → 对外公共接口清单
├─ README_FLOW.md    → 关键流程（开始下载、暂停、恢复、崩溃恢复）
├─ Contracts/
├─ Application/
├─ Domain/
├─ Infrastructure/
└─ Tests/
```

AI 读这三个文档 + 少量代码，就能在局部闭环里工作。

### 2.2 可见性控制

- 模块内部实现默认 `internal`
- 只有对外 Contracts 中的类是 `public`
- 这不仅防人，也防 AI 误用内部类

### 2.3 窄接口

不给 AI 看到"一个接口 30 个方法"的诱惑。

```
✅ IDownloadReadService（4 个方法）
✅ IDownloadCommandService（5 个方法）
❌ IDownloadService（30 个方法的万能接口）
```

### 2.4 文件粒度

一个文件只做一件事。AI 读小文件比读大文件准确率高得多。

```
✅ StartDownloadHandler.cs（一个用例）
✅ DownloadStateMachine.cs（一个状态机）
❌ DownloadService.cs（1500 行，什么都做）
```

---

## 3. AI 工作流程模板

### 3.1 典型任务流程

```
1. 接收任务描述
2. 确定任务归属的模块
3. 读取模块文档（ARCH、API、FLOW）
4. 读取相关代码文件
5. 确认改动范围：
   a. 仅模块内部 → 直接修改
   b. 涉及 Contracts → 声明影响面
6. 编写/修改代码
7. 编写/更新测试
8. 更新模块文档（如果接口变了）
9. 列出受影响的其他模块
```

### 3.2 跨模块契约变更流程

```
1. 明确声明"这是契约变更"
2. 列出变更的接口/DTO
3. 列出所有依赖方模块
4. 逐模块修改适配
5. 逐模块更新测试
6. 更新相关模块的 README_API.md
```

---

## 4. AI 禁止操作清单

| 编号 | 禁止操作 | 原因 |
|------|---------|------|
| X-01 | 跨模块引用 `internal` 类 | 违反可见性边界 |
| X-02 | 新增 `CommonService` / `HelperService` 类 | 万能类是架构退化 |
| X-03 | 在 UI 层写业务逻辑 | 违反 MVVM 原则 |
| X-04 | ViewModel 直接调 HttpClient | 越层访问 |
| X-05 | 直接 `task.State = xxx` | 绕过状态机 |
| X-06 | 在事件处理器中做长时间阻塞 | 阻塞事件总线 |
| X-07 | 自己偷开 `Task.Run` 跑后台任务 | 绕过 BackgroundTaskHost |
| X-08 | 把领域实体直接绑到 UI | 违反投影规则 |
| X-09 | 省略 CancellationToken 参数 | 后台任务不可取消 |
| X-10 | 忽略 Result 的 Error 不处理 | 静默吞错误 |

---

## 5. AI 代码审查清单

AI 提交代码前自查：

| 检查项 | 通过标准 |
|--------|---------|
| 是否只改了一个模块？ | 是（或已声明跨模块） |
| 是否引用了其他模块内部实现？ | 否 |
| 新增的类是 `internal` 还是 `public`？ | 内部类 internal，Contracts 里的 public |
| 是否有万能类命名？ | 否 |
| 长任务是否在后台执行？ | 是 |
| 状态变更是否走状态机？ | 是 |
| 结果是否用 Result<T> 返回？ | 是 |
| 接口方法是否包含 CancellationToken？ | 是 |
| 是否更新了相关测试？ | 是 |
| 是否更新了模块文档？ | 是（如果公共接口变了） |

---

## 6. 给 AI 的提示模板

当向 AI 下达编码任务时，建议使用以下模板：

```
模块：Downloads
任务：修改分块下载的重试策略，从固定间隔改为指数退避
范围：仅模块内部修改，Contracts 无变化

请先阅读：
- docs/06-ModuleDefinitions/Downloads.md
- docs/07-DownloadSubsystem.md 第 5.3 节
- src/Launcher.Background/Downloads/DownloadWorker.cs
- src/Launcher.Infrastructure/Network/Download/ChunkDownloadClient.cs

要求：
- 最大重试次数保持 5 次
- 退避间隔：1s → 2s → 4s → 8s → 16s + 随机抖动 ±20%
- 添加对应的单元测试
```

---

## 7. 代码注释规范

为了帮助 AI 理解代码意图，关键位置必须有中文注释：

```csharp
/// <summary>
/// 下载状态机。所有下载任务的状态转换都必须通过此类。
/// 禁止任何地方直接修改 DownloadTask.State。
/// </summary>
internal sealed class DownloadStateMachine
{
    // 状态转换表：Key = 当前状态，Value = 允许转换到的目标状态集合
    private static readonly Dictionary<DownloadState, HashSet<DownloadState>> _transitions = ...
}
```

```csharp
// 每 500ms 聚合一次进度，防止高频更新打爆 UI
private readonly Debouncer _progressDebouncer = new(TimeSpan.FromMilliseconds(500));
```

---

## 8. 会话交接协议

> AI 上下文不是无限的。当上下文接近上限时，信息会丢失。  
> 会话交接协议确保 AI 在新对话中能快速恢复上下文。

### 8.1 SESSION_HANDOFF.md

每次 AI 会话结束前（或完成一个原子任务后），更新项目根目录的 `SESSION_HANDOFF.md`：

```markdown
# 会话交接文档

## 最后更新
- 时间：2024-12-15 14:30 UTC
- 完成任务：Task 1.2（ShellPage + NavigationView）

## 当前项目状态
- 最后成功编译：是
- 最后测试结果：全部通过（12/12）
- 当前 Phase：Phase 1
- 下一个任务：Task 1.3（Toast 通知服务）

## 本次会话完成的工作
1. 创建了 ShellPage.xaml 和 ShellViewModel
2. 实现了 NavigationService（替换了 StubNavigationService）
3. 创建了 8 个模块占位页面
4. 修复了 Frame 导航缓存问题

## 遗留问题
- NavigationView 的 Header 区域在 Dark 主题下对比度不够（非阻塞，Phase 8 处理）

## 下一个任务的输入
- 读取文档：docs/05-CoreInterfaces.md § INotificationService
- 相关代码：src/Launcher.Presentation/Services/（目录结构）
- 注意事项：Toast 控件需要在 ShellPage.xaml 中预留容器位置
```

### 8.2 新会话启动流程

AI 在新对话开始时必须执行：

```
1. 读取 SESSION_HANDOFF.md              → 了解项目进度和下一步
2. 读取下一个任务对应的文档（输入）      → 了解接口定义和需求
3. 读取相关模块的 README_ARCH.md         → 了解模块上下文
4. 运行 dotnet build                     → 确认项目当前可编译
5. 开始正式编码
```

### 8.3 交接触发时机

| 时机 | 操作 |
|------|------|
| 完成一个原子任务 | 更新 SESSION_HANDOFF.md |
| 对话即将结束（提示即将达到上下文上限） | 立即更新 SESSION_HANDOFF.md |
| 遇到阻塞问题需要新对话解决 | 在 SESSION_HANDOFF.md 记录问题详情 |

---

## 9. CHANGELOG.md 变更日志

### 9.1 格式

项目根目录维护 `CHANGELOG.md`，每完成一个原子任务记录一条：

```markdown
# Changelog

## [Unreleased]

### Task 1.3 - Toast 通知服务 (2024-12-15)
- 新增 INotificationService 接口及实现
- 新增 ToastNotificationControl.xaml 控件
- 在 ShellPage.xaml 中集成 Toast 容器
- 新增 3 个单元测试

### Task 1.2 - ShellPage + NavigationView (2024-12-15)
- 新增 ShellPage.xaml 和 ShellViewModel
- 实现 NavigationService（替换 StubNavigationService）
- 创建 8 个模块占位页面
- 修复 Frame 导航缓存问题

### Task 1.1 - MainWindow 自定义标题栏 (2024-12-14)
- 新增自定义标题栏（应用图标 + 标题 + 窗口按钮）
- 启用 Mica 背景材质
- 设置最小窗口尺寸 1024x640
```

### 9.2 用途

- AI 在新会话中读取 CHANGELOG 可快速了解项目已完成哪些工作
- 发现潜在冲突（某个 Task 修改了公共接口，后续 Task 需要注意）
- 人类开发者也能一目了然地看到项目进展

---

## 10. 模块 README 规范

### 10.1 创建时机

当一个模块的第一个 Task 开始时，先在模块目录创建三个 README：

```
src/Launcher.Background/Downloads/
├─ README_ARCH.md    → 模块架构
├─ README_API.md     → 对外接口
├─ README_FLOW.md    → 关键流程
```

### 10.2 README_ARCH.md 模板

```markdown
# Downloads 模块架构

## 职责
分块下载、暂停/恢复、断点续传、崩溃恢复

## 依赖
- Launcher.Shared（Result、Error）
- Launcher.Infrastructure（IFileSystemService、IHashingService）

## 禁止
- 不得直接访问 UI 层
- 不得引用其他业务模块的内部实现

## 关键类
- DownloadOrchestrator：任务编排入口
- DownloadScheduler：并发调度
- DownloadWorker：单任务执行
- ChunkDownloader：分块 HTTP 下载
- DownloadStateMachine：状态管理

## 状态机
Idle → Queued → Downloading → Paused → Verifying → Completed / Failed

## 数据流
用户请求 → Orchestrator → Scheduler(排队) → Worker(执行) → ChunkDownloader(HTTP) → Checkpoint(持久化)
```

### 10.3 更新规则

| 变更类型 | README 更新 |
|---------|-------------|
| 新增公共接口 | 更新 README_API.md |
| 修改架构/职责 | 更新 README_ARCH.md |
| 新增/修改流程 | 更新 README_FLOW.md |
| 仅内部实现变更 | 不需要更新 |

---

## 11. AI 上下文管理最佳实践

### 11.1 问题：AI 上下文有限

- AI 的上下文窗口约 100K~200K tokens
- 当代码量达到数万行时，不可能把所有代码加载到上下文中
- 上下文溢出会导致：幻觉、遗忘之前的约定、产生与已有代码不一致的代码

### 11.2 解决方案矩阵

| 策略 | 解决什么问题 | 实现方式 |
|------|-------------|---------|
| **原子任务拆分** | 单次对话工作量可控 | [13-DevelopmentPhases.md](13-DevelopmentPhases.md) |
| **SESSION_HANDOFF.md** | 跨对话上下文恢复 | 本文 § 8 |
| **CHANGELOG.md** | 项目进度追溯 | 本文 § 9 |
| **模块 README** | 局部上下文自足 | 本文 § 10 |
| **结构化日志** | Bug 追踪和状态恢复 | [15-LoggingStrategy.md](15-LoggingStrategy.md) |
| **窄接口 + 可见性** | 减少 AI 需读取的代码量 | 本文 § 2.2 ~ 2.4 |
| **单模块原则** | 限制 AI 操作范围 | 本文 § 1 AI-01 |

### 11.3 上下文预算分配建议

每次 AI 对话的上下文预算大致分配：

```
SESSION_HANDOFF.md          ≈  1K tokens    （恢复上下文）
目标任务文档                ≈  2K tokens    （需求定义）
模块 README_ARCH/API/FLOW   ≈  3K tokens    （模块上下文）
相关代码文件                ≈ 10K tokens    （实际代码）
AI 思考 + 生成代码          ≈ 80K tokens    （主要工作）
                            ─────────────
                            ≈ 96K tokens
```

留出足够的空间给 AI 思考和生成代码，不要把上下文塞满文档。
