# 架构原则

> 本文档定义 HelsincyEpicLauncher 项目必须遵守的架构原则。  
> 这些原则同时约束人类开发者和 AI 辅助编码。

---

## 1. 六大核心原则

### 原则 1：UI 只负责显示和交互

Page / UserControl / Window 中 **禁止** 出现：

- 下载逻辑
- 文件系统扫描
- 哈希校验
- HTTP 调用
- SQL 操作
- 登录刷新
- 大段 if/else 业务判断

UI 层只做三件事：

1. **绑定** ViewModel
2. **响应** 视觉事件（点击、滚动、拖拽）
3. **调用** Command

> 依据：WinUI 3 的 MVVM 模式核心就是把 UI 和非 UI 代码解耦，提高可维护性和可测试性。

---

### 原则 2：ViewModel 只负责页面状态

ViewModel 的职责边界：

**应该做的：**
- 持有当前页面状态（筛选条件、选中项、加载状态）
- 提供 Command（暂停、恢复、取消等用户操作）
- 把领域模型转换成 UI 可展示数据
- 调用 Application 层用例

**不应该做的：**
- CDN 切换逻辑
- Patch 清单合并
- Chunk 校验
- 断点续传状态机运行
- 数据库操作
- 直接 HttpClient 调用

ViewModel 不是业务层。很多项目烂在 ViewModel 巨石化上。

---

### 原则 3：长任务全部后台化

以下操作 **必须** 离开 UI 线程：

- 文件下载
- 文件解压
- 哈希校验
- 磁盘扫描
- 缩略图生成
- Manifest 解析
- 大量目录遍历
- Patch 合并
- 安装/卸载文件操作

通过后台 Worker 执行，以事件/状态流回传摘要状态到 UI。

> 这是启动器的生死线。官方 Epic Launcher 卡死的主要原因就是重活跑在 UI 线程。

---

### 原则 4：领域规则集中化

下载状态机、安装状态机、修复规则 **必须** 集中在 Domain 层。

**禁止：**
- 状态转换逻辑散落在页面 code-behind 中
- ViewModel 里直接 `task.State = xxx`
- Service 里各自维护一套状态判断

**要求：**
- 所有状态变更走统一方法：`TransitionTo(targetState)`
- 状态机定义在 Domain 层对应模块中

---

### 原则 5：状态必须分层

项目中的状态分为四类，**不允许混存**：

| 分类 | 存储位置 | 举例 |
|------|----------|------|
| 持久化状态 | SQLite / JSON 文件 | 已安装资产、下载断点、用户配置、登录会话 |
| 运行时业务状态 | 进程内内存 Store | 当前下载进度、当前安装任务、网络可用性 |
| 页面状态 | ViewModel 私有字段 | 当前筛选、排序、选中项、面板折叠 |
| 全局 UI 状态 | Shell 级 State | 当前主题、Toast 队列、活动下载数 |

> 详见 [08-StateManagement.md](08-StateManagement.md)

---

### 原则 6：先搭骨架，再填功能

开发顺序是：

1. 先定目录、接口、模块边界、通信方式
2. 再做 Shell 壳层
3. 最后按模块填充功能

不要跳过骨架直接开始写页面。

---

## 2. 项目铁律

以下 10 条规则是不可违反的项目级约束：

| 编号 | 铁律 |
|------|------|
| **R-01** | 模块是系统的最小边界单位 |
| **R-02** | 模块之间只通过 Contracts 通信 |
| **R-03** | 默认 `internal`，公开即承诺 |
| **R-04** | UI 只消费 ViewModel 和 Summary，不消费领域实体 |
| **R-05** | 事件只表达"已发生的事实"，不替代正常方法调用 |
| **R-06** | 任何新功能必须先归属到某个模块 |
| **R-07** | 任何改动若波及无关模块，优先怀疑边界设计有问题 |
| **R-08** | 禁止 `CommonService` / `Manager` / `Helper` 式万能类 |
| **R-09** | 跨模块共享数据必须用窄 DTO，不得共享可变内部对象 |
| **R-10** | AI 只能在模块边界内工作，除非明确进行契约升级 |

---

## 3. 可局部理解原则

架构的终极目标不仅是"解耦"，而是：

> **把系统设计成可被局部理解、局部替换、局部验证的模块集合。**

这意味着：

### 3.1 高内聚

一个模块内部把自己的事情做完整。Downloads 模块自己解决创建任务、调度、checkpoint、retry、校验、进度聚合，不到处借别人的 service。

### 3.2 低耦合

模块之间的联系满足：
- 数量少
- 方向固定（单向）
- 只通过契约
- 不泄漏内部状态机
- 不共享可变内部对象

### 3.3 可局部替换

重写下载模块，只要 Contracts 不变，不应逼着改 FabLibrary 核心逻辑。

### 3.4 可局部理解

AI 或开发者看 Downloads 模块时，不需要先读完整个项目。每个模块的 README_ARCH.md 就是入口。

### 3.5 可局部测试

改下载逻辑，只测下载相关用例和少量集成边界。

---

## 4. 依赖方向规则

依赖方向 **必须始终单向**：

```
Presentation → Application → Domain
                    ↓
              Contracts (接口)
                    ↑
             Infrastructure (实现)
```

### 允许的方向

```
Presentation  →  Application
Application   →  Contracts / Domain
Infrastructure →  Contracts（实现接口）
Background    →  Application / Contracts / Domain
ModuleA       →  ModuleB.Contracts
```

### 禁止的方向

```
Infrastructure  →  Presentation     ❌
Domain          →  Infrastructure   ❌
Application     →  Presentation     ❌
Downloads       →  FabLibrary.内部   ❌
任何模块         →  另一模块内部实现  ❌
```

---

## 5. 命名守则

为降低理解成本（人类和 AI 共用），命名必须表达职责。

### 好名字

```
DownloadOrchestrator
PatchPlanBuilder
FabAssetSearchUseCase
InstallationVerifier
ManifestCacheRepository
```

### 坏名字

```
CommonService        ← 职责不明
GameManager          ← 管什么？
DataHelper           ← 帮什么忙？
LauncherUtils        ← 万能垃圾桶
```

---

## 6. 文件粒度守则

- 一个 UseCase 一个文件
- 一个 Facade 一个文件
- 一个状态机一个文件
- 一个 Event 一个文件
- ViewModel 不超过 400 行，超过就拆子 VM

**禁止：**
- 一个 ViewModel 1200 行
- 一个 Service 同时做下载、安装、修复、通知
- 一个 DTO 文件塞 30 个不相关模型
