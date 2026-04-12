# 开发阶段规划

> 分阶段交付，每个 Phase 拆解为**原子任务**。  
> 每个原子任务可在单次 AI 对话内完成，有明确的输入、输出和验收条件。  
> 这样即使 AI 上下文丢失，也只需从最近的原子任务检查点恢复。

---

## 阶段依赖图

```
Phase 0 ──▶ Phase 1 ──▶ Phase 2 ──┐
                                    ├──▶ Phase 3 ──▶ Phase 4 ──▶ Phase 5 ──▶ Phase 6 ──▶ Phase 7
                                    │
                                    └──▶ Phase 8（Phase 6 之后持续进行）
```

---

## 原则

1. **每个原子任务结束时必须可编译、可运行**
2. **不跳任务** — 前置任务没完成就不做下一个
3. **每个任务完成后**：更新 `CHANGELOG.md` + 运行 `dotnet build` + `dotnet test`
4. **每个任务开头**：AI 先读 `SESSION_HANDOFF.md` + 相关模块文档恢复上下文
5. **单次对话只做一个原子任务**（除非任务很小可以合并）

---

## Phase 0：工程骨架

**目标**：搭建空跑的项目结构，所有层可编译、可测试。

### Task 0.1：创建 Solution 和项目文件

**输入**：[03-SolutionStructure.md](03-SolutionStructure.md)、[11-TechStack.md](11-TechStack.md)  
**产出**：
- `MyEpicLauncher.sln`
- 所有 .csproj 文件：App / Presentation / Application / Domain / Infrastructure / Background / Shared
- 测试项目：Tests.Unit / Tests.Integration
- 项目引用关系（按 [04-ModuleDependencyRules.md](04-ModuleDependencyRules.md)）
- NuGet 包引用（按 [11-TechStack.md](11-TechStack.md)）
- `Directory.Build.props` 统一版本管理
- `.editorconfig` 代码风格规范

**验收**：`dotnet build` 零错误零警告

---

### Task 0.2：DI 容器 + 配置系统

**输入**：[03-SolutionStructure.md](03-SolutionStructure.md) § DI 注册  
**产出**：
- `Launcher.App/Program.cs` — Host 启动 + DI 容器
- `Launcher.App/appsettings.json` — 基础配置文件
- `IAppConfigProvider` 接口 + 实现
- 各层 `DependencyInjection.cs` 注册扩展方法（空注册）
- 配置强类型绑定基础

**验收**：App 启动时 DI 容器正确构建，日志输出「DI 容器初始化完成」

---

### Task 0.3：Serilog 日志系统

**输入**：[15-LoggingStrategy.md](15-LoggingStrategy.md)  
**产出**：
- Serilog 配置（文件 Sink + 控制台 Sink）
- `OperationContext` 类
- `OperationTimer` 类
- `LogSanitizer` 工具类
- 日志文件轮转配置
- 下载模块独立日志 Sink

**验收**：App 启动后在 `%LOCALAPPDATA%/MyEpicLauncher/Logs/` 生成日志文件

---

### Task 0.4：Shared 层基础类型

**输入**：[05-CoreInterfaces.md](05-CoreInterfaces.md)、[09-ErrorHandling.md](09-ErrorHandling.md)  
**产出**：
- `Result` / `Result<T>` 类
- `Error` 模型（Code, UserMessage, TechnicalMessage, CanRetry, Severity）
- `ErrorSeverity` 枚举
- `Entity` 基类
- `ValueObject` 基类
- `StateMachine<TState>` 基类

**验收**：单元测试通过 — Result 的成功/失败/链式调用

---

### Task 0.5：SQLite 数据库基础

**输入**：[08-StateManagement.md](08-StateManagement.md)  
**产出**：
- SQLite 数据库文件创建（`%LOCALAPPDATA%/MyEpicLauncher/Data/launcher.db`）
- Dapper 基础配置
- `IDbConnectionFactory` 接口 + 实现
- 基础 Migration 框架（版本化 SQL 脚本执行）
- 初始 Migration（创建版本跟踪表）

**验收**：App 启动时数据库文件创建成功，Migration 执行日志可见

---

### Task 0.6：INavigationService 空实现 + 测试项目

**输入**：[05-CoreInterfaces.md](05-CoreInterfaces.md) § INavigationService  
**产出**：
- `INavigationService` 接口
- 空实现 `StubNavigationService`（仅日志记录导航请求）
- xUnit 测试项目配置
- NSubstitute + FluentAssertions 引入
- 一个示例单元测试

**验收**：`dotnet test` 通过，测试输出可见

---

### Task 0.7：WinUI 3 空窗口 + 单实例

**输入**：[03-SolutionStructure.md](03-SolutionStructure.md) § 单实例、[10-StartupPipeline.md](10-StartupPipeline.md)  
**产出**：
- `MainWindow.xaml` — 空窗口
- Mutex 单实例保证
- 命名管道通信（第二实例 → 已有实例）
- 启动参数解析（`--minimized`）
- App 生命周期管理

**验收**：App 启动显示空窗口；第二次启动时激活已有窗口而非开新窗口

---

## Phase 1：Shell 壳层

**目标**：主窗口骨架完成，导航可用，全局 UI 基础设施就绪。

### Task 1.1：MainWindow 自定义标题栏

**输入**：[06-ModuleDefinitions/Shell.md](06-ModuleDefinitions/Shell.md)  
**产出**：
- 自定义标题栏（应用图标 + 标题 + 最小化/最大化/关闭按钮）
- Mica 背景材质
- 窗口拖拽区域
- 最小窗口尺寸限制

**验收**：窗口可拖拽、可缩放、标题栏按钮正常工作、Mica 背景生效

---

### Task 1.2：ShellPage + NavigationView

**输入**：[06-ModuleDefinitions/Shell.md](06-ModuleDefinitions/Shell.md)  
**产出**：
- `ShellPage.xaml` — 左侧 NavigationRail + 右侧内容 Frame
- `ShellViewModel` 基础版
- `NavigationService` 完整实现（替换 Stub）
- 各模块占位页面（显示模块名）

**验收**：导航栏点击切换页面，Frame 正确加载对应占位页

---

### Task 1.3：Toast 通知服务

**输入**：[05-CoreInterfaces.md](05-CoreInterfaces.md) § INotificationService  
**产出**：
- `INotificationService` 接口
- `NotificationService` 实现
- Toast UI 控件（从窗口右上角弹出）
- 支持 Info / Warning / Error 三种样式
- 自动消失 + 手动关闭

**验收**：调用 `ShowAsync("测试消息", NotificationLevel.Info)` 后弹出 Toast

---

### Task 1.4：Dialog 对话框服务

**输入**：[05-CoreInterfaces.md](05-CoreInterfaces.md) § IDialogService  
**产出**：
- `IDialogService` 接口
- `DialogService` 实现
- ContentDialog 通用宿主
- 确认对话框（标题 + 内容 + 确定/取消）

**验收**：调用 `ConfirmAsync("确定删除？")` 后弹出对话框并返回用户选择

---

### Task 1.5：主题切换 + 状态栏

**输入**：[06-ModuleDefinitions/Shell.md](06-ModuleDefinitions/Shell.md)  
**产出**：
- 主题切换（Light / Dark / System）
- 主题持久化（保存到配置）
- 底部状态栏基本框架
- 状态栏：下载速度占位 + 网络状态占位

**验收**：切换主题即时生效，重启后保持选择

---

### Task 1.6：系统托盘

**输入**：[03-SolutionStructure.md](03-SolutionStructure.md) § 系统托盘  
**产出**：
- 系统托盘图标
- 右键菜单（显示主窗口 / 退出）
- 关闭按钮 → 最小化到托盘（可配置）
- 托盘双击 → 显示主窗口

**验收**：关闭窗口后图标出现在托盘，右键菜单可用

---

## Phase 2：基础模块

**目标**：Settings、Diagnostics 功能可用，本地数据持久化就绪。

### Task 2.1：配置系统完整实现

**输入**：[06-ModuleDefinitions/Settings.md](06-ModuleDefinitions/Settings.md)  
**产出**：
- `ISettingsCommandService` 实现
- 配置分层加载：`appsettings.json` → `Local/` → `user.settings`
- 强类型配置类（下载路径、并发数、主题等）
- `ConfigChangedEvent` 事件
- 配置读写 + JSON 序列化

**验收**：修改配置后保存到 `user.settings`，重启后恢复

---

### Task 2.2：Settings 页面 UI

**输入**：[06-ModuleDefinitions/Settings.md](06-ModuleDefinitions/Settings.md)  
**产出**：
- `SettingsPage.xaml` + `SettingsViewModel`
- 设置分组（通用、下载、主题、高级）
- 各设置项 UI 控件（文本框、开关、下拉等）
- 实时生效 + 保存/重置

**验收**：修改设置后实时生效，重启后持久化

---

### Task 2.3：SQLite 数据层 + Repository 基础

**输入**：[08-StateManagement.md](08-StateManagement.md)  
**产出**：
- 完善 Migration 框架（可增量执行）
- 基础表结构（downloads、installations、settings_kv）
- `RepositoryBase<T>` 基类（通用 CRUD）
- 连接池管理

**验收**：Migration 执行后表结构正确，CRUD 单元测试通过

---

### Task 2.4：Diagnostics 页面 — 系统信息

**输入**：[06-ModuleDefinitions/Diagnostics.md](06-ModuleDefinitions/Diagnostics.md)  
**产出**：
- `DiagnosticsPage.xaml` + `DiagnosticsViewModel`
- Tab 1：系统信息面板（OS 版本、.NET 版本、磁盘空间、内存）
- `IDiagnosticsReadService` 部分实现
- 磁盘空间监控

**验收**：诊断页面显示正确的系统信息

---

### Task 2.5：Diagnostics 页面 — 日志查看器

**输入**：[06-ModuleDefinitions/Diagnostics.md](06-ModuleDefinitions/Diagnostics.md)、[15-LoggingStrategy.md](15-LoggingStrategy.md) § 10  
**产出**：
- Tab 2：日志查看器（实时流 + 历史查看）
- 级别筛选（Debug / Info / Warning / Error）
- 模块筛选
- 关键字搜索
- CorrelationId 追踪（输入 ID 显示完整链路）
- 日志导出功能

**验收**：日志实时刷新，筛选正确，导出文件可读

---

### Task 2.6：Diagnostics 页面 — 缓存管理

**输入**：[06-ModuleDefinitions/Diagnostics.md](06-ModuleDefinitions/Diagnostics.md)  
**产出**：
- Tab 3：缓存统计（缩略图缓存、搜索缓存、日志文件大小）
- 清理按钮（分类清理 / 全部清理）
- `ICacheManager` 接口 + 实现

**验收**：缓存大小显示正确，清理后释放磁盘空间

---

## Phase 3：认证模块

**目标**：Epic Games OAuth 登录 + 会话管理完整可用。

### Task 3.1：OAuth 核心流程

**输入**：[06-ModuleDefinitions/Auth.md](06-ModuleDefinitions/Auth.md)  
**产出**：
- `IAuthService` 接口实现
- OAuth 2.0 Authorization Code 流程
- 本地 HTTP 监听器接收回调
- Token 兑换（code → access_token + refresh_token）
- `TokenPair` DTO

**验收**：能完成 OAuth 流程获取 Token（可用测试端点模拟）

---

### Task 3.2：Token 存储 + 自动刷新

**输入**：[06-ModuleDefinitions/Auth.md](06-ModuleDefinitions/Auth.md)  
**产出**：
- `ITokenStore` 接口 + Windows Credential Locker 实现
- Token 加密存储
- Token 自动刷新（过期前 5 分钟主动刷新）
- 后台定时器检查 Token 有效性
- `SessionExpiredEvent` 事件

**验收**：Token 存储安全，自动刷新成功，过期后触发事件

---

### Task 3.3：登录 UI + Shell 集成

**输入**：[06-ModuleDefinitions/Auth.md](06-ModuleDefinitions/Auth.md)  
**产出**：
- 登录页面（或弹窗）UI
- Shell 头部用户信息显示（头像、用户名）
- 登出按钮 + 确认
- 启动时会话恢复流程
- 未登录 → 限制功能（Fab 浏览需要登录）

**验收**：登录状态在 Shell 正确显示，重启后自动恢复会话

---

## Phase 4：下载 MVP

**目标**：下载子系统核心可用。

### Task 4.1：DownloadTask 领域实体 + 状态机

**输入**：[06-ModuleDefinitions/Downloads.md](06-ModuleDefinitions/Downloads.md)、[07-DownloadSubsystem.md](07-DownloadSubsystem.md)  
**产出**：
- `DownloadTask` 领域实体
- 下载状态机（13 个内部状态 + 转换表）
- 状态转换验证
- 内部状态 → UI 状态映射
- 相关 DTO（DownloadTaskSummary、DownloadProgressSnapshot）
- 状态机单元测试

**验收**：所有合法状态转换通过，非法转换抛异常，测试 100% 覆盖

---

### Task 4.2：Download Orchestrator + Scheduler

**输入**：[07-DownloadSubsystem.md](07-DownloadSubsystem.md) § 入口层 + 编排层 + 调度层  
**产出**：
- `IDownloadCommandService` / `IDownloadReadService` 实现
- `DownloadOrchestrator` — 任务编排
- `DownloadScheduler` — 队列 + 并发控制（最大 3 个并行任务）
- 优先级队列
- 任务取消支持（CancellationToken）

**验收**：创建多个下载任务后按优先级排队，不超过并发限制

---

### Task 4.3：ChunkDownloader + HTTP Range

**输入**：[07-DownloadSubsystem.md](07-DownloadSubsystem.md) § 执行层  
**产出**：
- `ChunkDownloader` — 分块下载核心
- HTTP Range 请求实现
- 分块逻辑（默认 10MB/块，每任务最大 4 并行块）
- 重试策略（指数退避 + 随机抖动）
- Polly 韧性策略（重试 + 断路器）
- 进度回报接口

**验收**：能从支持 Range 的 URL 分块下载文件，重试正确触发

---

### Task 4.4：Checkpoint 持久化 + 崩溃恢复

**输入**：[07-DownloadSubsystem.md](07-DownloadSubsystem.md) § Checkpoint  
**产出**：
- `DownloadCheckpoint` / `ChunkCheckpoint` 数据结构
- SQLite Checkpoint 表 + Repository
- Checkpoint 保存策略（每块完成 + 定时 30s）
- 启动时崩溃恢复流程（扫描未完成任务 → 验证文件 → 恢复）
- 临时文件管理

**验收**：杀进程后重启，中断的任务从断点恢复，进度不丢失

---

### Task 4.5：Runtime Store + 进度聚合

**输入**：[07-DownloadSubsystem.md](07-DownloadSubsystem.md) § 运行时状态 + 进度聚合  
**产出**：
- `IDownloadRuntimeStore` 实现（内存进度快照）
- 进度聚合节流（500ms）
- 下载速度计算（滑动窗口 5 秒）
- 预估剩余时间计算
- `DownloadProgressChanged` / `DownloadCompleted` / `DownloadFailed` 事件

**验收**：进度更新平滑，速度显示合理，事件正确触发

---

### Task 4.6：Downloads 页面 UI

**输入**：[06-ModuleDefinitions/Downloads.md](06-ModuleDefinitions/Downloads.md)  
**产出**：
- `DownloadsPage.xaml` + `DownloadsViewModel`
- 下载任务列表（名称、进度条、速度、预估时间、状态）
- 暂停/恢复/取消按钮
- 状态栏下载迷你面板
- 空状态提示

**验收**：下载列表实时更新，暂停/恢复操作响应正确，UI 不卡顿

---

## Phase 5：安装/校验/修复

**目标**：下载完成后的安装管理。

### Task 5.1：Install Worker + Manifest

**输入**：[06-ModuleDefinitions/Installations.md](06-ModuleDefinitions/Installations.md)  
**产出**：
- `IInstallCommandService` / `IInstallReadService` 实现
- Install Worker — 解压/复制到安装目录
- `InstallManifest` 持久化（JSON + 哈希清单）
- Install 状态机
- 安装路径验证（磁盘空间、权限）

**验收**：下载完成后能安装到指定目录，Manifest 正确生成

---

### Task 5.2：Integrity Verifier + Repair

**输入**：[06-ModuleDefinitions/Installations.md](06-ModuleDefinitions/Installations.md)  
**产出**：
- `IIntegrityVerifier` 实现 — 逐文件哈希比对
- `IHashingService` 实现（SHA256）
- Repair Worker — 重新下载损坏文件（复用 ChunkDownloader）
- 校验进度回报
- 并行哈希计算（多线程 + I/O 优化）

**验收**：手动改损一个文件后校验能发现，修复后再校验通过

---

### Task 5.3：Uninstaller + Installations UI

**输入**：[06-ModuleDefinitions/Installations.md](06-ModuleDefinitions/Installations.md)  
**产出**：
- Uninstaller — 清理安装目录 + 数据库记录
- `InstallationsPage.xaml` + `InstallationsViewModel`
- 已安装列表（名称、路径、大小、状态）
- 校验/修复/卸载按钮
- 下载完自动安装开关

**验收**：能查看、校验、修复、卸载已安装资产

---

## Phase 6：Fab 资产库

**目标**：Fab 资产浏览、搜索、下载完整可用。

### Task 6.1：Fab API 客户端

**输入**：[06-ModuleDefinitions/FabLibrary.md](06-ModuleDefinitions/FabLibrary.md)  
**产出**：
- `IFabCatalogReadService` 实现
- `IFabAssetCommandService` 实现
- Fab API HTTP 客户端（搜索、详情、已拥有查询）
- API DTO 映射
- Polly 韧性策略（重试 + 缓存）
- 搜索结果本地缓存（5 分钟过期）

**验收**：能调用 Fab API 获取资产列表和详情

---

### Task 6.2：Fab 资产浏览页

**输入**：[06-ModuleDefinitions/FabLibrary.md](06-ModuleDefinitions/FabLibrary.md)  
**产出**：
- `FabLibraryPage.xaml` + `FabLibraryViewModel`
- 虚拟化网格列表（ItemsRepeater）
- 缩略图懒加载（可视区域加载 + LRU 磁盘缓存）
- 加载中骨架屏
- 分页/无限滚动

**验收**：100+ 卡片滚动流畅（60fps），缩略图正常加载

---

### Task 6.3：搜索/筛选 + 详情页

**输入**：[06-ModuleDefinitions/FabLibrary.md](06-ModuleDefinitions/FabLibrary.md)  
**产出**：
- 搜索框（300ms 防抖）
- 筛选器（类型、引擎版本、价格等）
- 排序（相关度、最新、评分）
- 资产详情页（截图、描述、技术信息）
- 详情页下载按钮 → 调用 Downloads 模块

**验收**：搜索响应流畅，筛选正确，详情页信息完整，点击下载触发下载任务

---

### Task 6.4：已拥有资产 + 下载集成

**输入**：[06-ModuleDefinitions/FabLibrary.md](06-ModuleDefinitions/FabLibrary.md)  
**产出**：
- 「我的资产」Tab（已拥有资产列表）
- 本地缓存 + 远程同步（启动时拉取 + 手动刷新）
- 资产状态标识（已下载、未下载、更新可用）
- 批量下载支持
- 资产与已安装资产的关联

**验收**：已拥有资产正确显示，状态标识准确，下载集成正常

---

## Phase 7：引擎版本 + 插件

**目标**：UE 引擎管理和插件管理可用。

### Task 7.1：引擎版本管理

**输入**：[06-ModuleDefinitions/EngineVersions.md](06-ModuleDefinitions/EngineVersions.md)  
**产出**：
- `IEngineVersionReadService` / `IEngineVersionCommandService` 实现
- 远程引擎版本列表查询
- 本地已安装引擎扫描
- 引擎下载（复用 Downloads）+ 安装（复用 Installations）
- `EngineVersionsPage.xaml` + `EngineVersionsViewModel`

**验收**：能查看可用引擎、下载、安装

---

### Task 7.2：引擎启动 + 插件管理

**输入**：[06-ModuleDefinitions/EngineVersions.md](06-ModuleDefinitions/EngineVersions.md)、[06-ModuleDefinitions/Plugins.md](06-ModuleDefinitions/Plugins.md)  
**产出**：
- 引擎启动（Process.Start 编辑器可执行文件）
- `IPluginReadService` / `IPluginCommandService` 实现
- 已安装插件列表
- 插件兼容性检查
- `PluginsPage.xaml` + `PluginsViewModel`

**验收**：能启动引擎编辑器，能查看和管理插件

---

## Phase 8：自动更新 + 打磨

**目标**：启动器自身更新 + 最终品质提升。

### Task 8.1：自动更新

**输入**：[06-ModuleDefinitions/Updates.md](06-ModuleDefinitions/Updates.md)  
**产出**：
- `IAppUpdateService` 实现
- 后台定时检查更新（每 4 小时）
- 更新下载 + 校验
- 退出后替换执行
- 版本变更日志显示
- 用户通知（有可用更新）

**验收**：检测到新版本后通知用户，用户确认后自动更新

---

### Task 8.2：网络韧性增强

**输入**：[07-DownloadSubsystem.md](07-DownloadSubsystem.md) § 网络韧性  
**产出**：
- 全局网络状态监控（在线/离线检测）
- 断网自动暂停下载 + 恢复后自动继续
- API 限流处理（Retry-After 响应头）
- CDN 回退机制
- 网络状态 UI 指示器

**验收**：拔网线后下载暂停，插网线后自动恢复

---

### Task 8.3：性能优化

**产出**：
- 冷启动优化（目标 < 2 秒显示窗口）
- 内存占用优化（图片缓存 LRU、ViewModel 释放）
- 列表滚动帧率优化（虚拟化验证 + 60fps）
- 数据库查询优化（索引、批量操作）

**验收**：冷启动 < 2 秒，100+ 卡片滚动 60fps，内存占用 < 300MB

---

### Task 8.4：UI 打磨 + 错误闭环

**产出**：
- 页面过渡动画
- 加载骨架屏全面覆盖
- 图标和间距统一
- 所有错误路径 UI 覆盖（Error → 提示 + 重试）
- 无网络状态页面
- 空状态页面（无下载、无安装等）

**验收**：视觉一致、交互流畅、所有边缘情况有提示

---

## 任务总览

| Phase | 任务数 | 编号 |
|-------|--------|------|
| Phase 0：工程骨架 | 7 | Task 0.1 ~ 0.7 |
| Phase 1：Shell 壳层 | 6 | Task 1.1 ~ 1.6 |
| Phase 2：基础模块 | 6 | Task 2.1 ~ 2.6 |
| Phase 3：认证模块 | 3 | Task 3.1 ~ 3.3 |
| Phase 4：下载 MVP | 6 | Task 4.1 ~ 4.6 |
| Phase 5：安装管理 | 3 | Task 5.1 ~ 5.3 |
| Phase 6：Fab 资产库 | 4 | Task 6.1 ~ 6.4 |
| Phase 7：引擎 + 插件 | 2 | Task 7.1 ~ 7.2 |
| Phase 8：更新 + 打磨 | 4 | Task 8.1 ~ 8.4 |
| **总计** | **41** | |

每个任务约 10~30 分钟 AI 编码工作量，可在单次对话内完成。
