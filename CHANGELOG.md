# Changelog

## [Unreleased]

### Task 2.4 - Diagnostics 页面 — 系统信息 (2026-04-13)
- SystemDiagnosticsSummary 模型（OS/内存/磁盘/版本/进程内存/数据库大小）
- IDiagnosticsReadService 接口（GetSystemSummaryAsync）
- DiagnosticsService 实现（收集 OS/磁盘/内存/数据库信息）
- DiagnosticsViewModel：系统信息/磁盘空间/内存使用展示 + 手动刷新
- DiagnosticsPage.xaml：系统信息、磁盘空间（进度条）、内存、存储四个区域
- 页面加载时自动采集诊断信息
- Infrastructure DI 注册 DiagnosticsService
- Presentation DI 注册 DiagnosticsViewModel
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 2.3 - SQLite 数据层 + Repository 基础 (2026-04-13)
- Migration_002_Downloads：下载任务表（id/asset_id/status/total_bytes/downloaded_bytes/checkpoint 等）
- Migration_003_Installations：已安装资产表（id/asset_id/version/install_path/size_bytes 等）
- Migration_004_SettingsKv：键值设置表（key/value/category，与 user.settings.json 互补）
- RepositoryBase<T> 泛型基类（Dapper CRUD：GetById/GetAll/Query/Insert/Update/Delete/Count/事务）
- Infrastructure InternalsVisibleTo Tests.Integration
- 6 个 CRUD 集成测试（内存 SQLite：插入查询/全量查询/删除/不存在删除/计数/更新）
- Infrastructure DI 注册 3 个新迁移
- dotnet build 9 个项目零错误零警告，dotnet test 21/21 通过

### Task 2.2 - Settings 页面 UI (2026-04-13)
- SettingsViewModel（CommunityToolkit.Mvvm）：下载/外观/路径/网络四组配置双向绑定
- SettingsPage.xaml 完整 UI：通用、下载、路径、高级四个设置分组
- 主题切换实时生效（ComboBox 选择后即时通知 ThemeService）
- 各分组独立保存按钮 + 全局重置按钮
- 状态消息提示（保存成功/失败/重置完成）
- ViewModelLocator 静态服务定位器（解决 Frame.Navigate 无法构造器注入的问题）
- Presentation DI 注册 SettingsViewModel（Transient）
- App.xaml.cs 启动时配置 ViewModelLocator
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 2.1 - 配置系统完整实现 (2026-04-13)
- 强类型配置类：DownloadConfig / AppearanceConfig / PathConfig / NetworkConfig（Application 层）
- ConfigChangedEvent 配置变更事件（sealed record，Section + NewConfig）
- ISettingsCommandService 写接口（UpdateDownload/Appearance/Path/NetworkConfigAsync + ResetToDefaultsAsync）
- ISettingsReadService 读接口（GetDownload/Appearance/Path/NetworkConfig 同步返回）
- UserSettings JSON 持久化模型（Infrastructure 层）
- SettingsService 双接口实现（ISettingsCommandService + ISettingsReadService）
- user.settings.json 文件读写（DataPath 目录），异步 I/O
- 线程安全（lock）+ 深拷贝隔离（JSON 序列化/反序列化防止外部修改内部状态）
- ConfigChanged 事件通知订阅者配置变更
- Infrastructure DI 注册 SettingsService（Singleton + 双接口映射）
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 1.6 - 系统托盘 (2026-04-13)
- TrayIconManager（System.Windows.Forms.NotifyIcon via FrameworkReference）
- 系统托盘图标 + 右键菜单（显示主窗口 / 退出）
- 托盘双击 → 显示主窗口
- 关闭按钮 → 最小化到托盘（AppWindow.Closing Cancel + Hide）
- ActivateMainWindow 增强：AppWindow.Show() 恢复隐藏窗口
- Phase 1 Shell 壳层全部完成
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 1.5 - 主题切换 + 状态栏 (2026-04-13)
- ThemeService：Light / Dark / System 三种主题切换，即时生效
- 主题持久化到 theme.json（%LOCALAPPDATA%/HelsincyEpicLauncher/Data/）
- ShellPage.Loaded 初始化 ThemeService 并恢复保存的主题
- 底部状态栏：网络状态图标+文字 + 下载速度占位
- DI 注册 ThemeService
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 1.4 - Dialog 对话框服务 (2026-04-13)
- IDialogService 接口（ShowConfirmAsync / ShowInfoAsync / ShowErrorAsync / ShowCustomAsync）
- DialogService 实现：基于 WinUI 3 ContentDialog + XamlRoot 绑定
- 确认对话框（Primary + Close 按钮）、信息对话框、错误对话框（含可重试按钮）
- ShellPage.Loaded 设置 DialogService.XamlRoot
- 抑制 MVVMTK0045 警告（非 AOT 场景无影响）
- DI 注册 DialogService（具体类型 + 接口）
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 1.3 - Toast 通知服务 (2026-04-13)
- INotificationService 接口（ShowSuccess / ShowWarning / ShowError / ShowInfo，支持自定义持续时间）
- NotificationService 实现：基于 WinUI 3 InfoBar 控件，支持 4 种严重级别
- 自动消失（Info/Success 4s、Warning 6s、Error 8s）+ 手动关闭按钮
- ShellPage 添加 ToastHost 覆盖层（右上角，MaxWidth 400）
- DispatcherQueue 线程安全调度，支持后台线程调用
- DI 注册 NotificationService（具体类型 + 接口）
- dotnet build 9 个项目零错误，dotnet test 15/15 通过

### Task 1.2 - ShellPage + NavigationView (2026-04-13)
- NavigationRoute 路由常量定义（FabLibrary / Downloads / EngineVersions / Settings / Diagnostics）
- NavigationService 完整实现：Frame 导航 + 路由映射 + 历史栈跟踪 + Serilog 日志
- ShellViewModel 基础版：5 个导航 RelayCommand + 全局状态 ObservableProperty 占位
- ShellPage UserControl：NavigationView（Left 模式）5 项导航 + ContentFrame
- 5 个模块占位页：FabLibraryPage / DownloadsPage / EngineVersionsPage / SettingsPage / DiagnosticsPage
- MainWindow 加载 ShellPage 到内容区域（DI 解析 ShellViewModel + NavigationService）
- Presentation DI 更新：NavigationService 替换 StubNavigationService + 注册 ShellViewModel
- dotnet build 9 个项目零错误，dotnet test 15/15 通过

### Task 1.1 - MainWindow 自定义标题栏 + Mica 背景 (2026-04-13)
- 自定义标题栏：应用图标占位（品牌色 H 标识） + 标题文字 + 系统最小化/最大化/关闭按钮
- ExtendsContentIntoTitleBar + SetTitleBar 实现窗口拖拽区域
- MicaBackdrop 背景材质
- Win32 子类化拦截 WM_GETMINMAXINFO 强制最小窗口尺寸 1024x640
- PInvoke 扩展：GetDpiForWindow / SetWindowSubclass / DefSubclassProc / MINMAXINFO 结构体
- DPI 感知：自动将 DIP 最小尺寸转为物理像素
- dotnet build 9 个项目零错误，dotnet test 15/15 通过

### Task 0.7 - WinUI 3 空窗口 + 单实例 (2026-04-13)
- 创建 App.xaml + App.xaml.cs（WinUI 3 Application 子类，管理单实例 + DI + Serilog + 数据库迁移）
- 创建 MainWindow.xaml + MainWindow.xaml.cs（空窗口，1280x800 默认尺寸）
- Mutex 单实例保证（HelsincyEpicLauncher_SingleInstance）
- 命名管道通信（第二实例发送 ACTIVATE → 已有实例激活窗口）
- PInvoke: ShowWindow + SetForegroundWindow
- Program.cs 简化为 WinUI 3 启动入口（DISABLE_XAML_GENERATED_MAIN）
- Launcher.App.csproj: AllowUnsafeBlocks + DISABLE_XAML_GENERATED_MAIN
- Phase 0 全部完成，dotnet build 9 个项目零错误，dotnet test 15/15 通过

### Task 0.6 - INavigationService 空实现 (2026-04-12)
- 创建 INavigationService 接口（Presentation/Shell/Navigation — NavigateAsync / GoBackAsync / CanGoBack / CurrentRoute）
- 创建 StubNavigationService 桩实现（导航历史栈 + Serilog 日志记录）
- Presentation DI 注册 INavigationService → StubNavigationService
- 3 个 NSubstitute 导航单元测试（NavigateAsync / GoBackAsync / 默认状态）
- Tests.Unit 添加 Presentation 项目引用
- dotnet build 9 个项目零错误零警告，dotnet test 15/15 通过

### Task 0.5 - SQLite 数据库基础 (2026-04-12)
- 创建 IDbConnectionFactory / IDatabaseInitializer 接口（Application 层）
- 创建 SqliteConnectionFactory（WAL 模式，路径 %LOCALAPPDATA%/HelsincyEpicLauncher/Data/launcher.db）
- 创建 Migration 框架：IMigration 接口 + MigrationRunner（版本化 SQL 脚本执行，__migration_history 表跟踪）
- 创建初始迁移 Migration_001_AppSettings（app_settings 键值表）
- Program.cs 启动时通过 IDatabaseInitializer 执行数据库迁移
- Infrastructure DI 注册 SqliteConnectionFactory、MigrationRunner
- dotnet build 9 个项目零错误零警告，dotnet test 12/12 通过

### Task 0.4 - Shared 层基础类型 (2026-04-12)
- 创建 Error 结构化错误模型（Code / UserMessage / TechnicalMessage / CanRetry / Severity / InnerException）
- 创建 ErrorSeverity 枚举（Warning / Error / Critical / Fatal）
- 创建 Result / Result&lt;T&gt; 统一操作结果（含 Map/Bind 链式操作、IsFailure 便捷属性）
- 创建 Entity&lt;TId&gt; 实体基类（按 Id 判等 + 运算符重载）
- 创建 ValueObject 值对象基类（按属性值判等 + GetEqualityComponents 模式）
- 创建 StateMachine&lt;TState&gt; 泛型状态机基类（DefineTransition + TransitionTo + CanTransitionTo + OnTransitioned 回调）
- 10 个 Result/Error 单元测试（成功/失败/Map/Bind/属性验证）
- dotnet build 9 个项目零错误零警告，dotnet test 12/12 通过

### Task 0.3 - Serilog 日志系统 (2026-04-12)
- 创建 OperationContext（Shared/Logging — CorrelationId + Module + Operation 推入 LogContext）
- 创建 OperationTimer（Shared/Logging — using 模式自动记录操作开始/完成及耗时）
- 创建 LogSanitizer（Shared/Logging — Token 脱敏、URL 敏感参数清理、GeneratedRegex）
- Program.cs 初始化 Serilog：主日志(Info+, 30天)、错误日志(Error+, 90天)、下载日志(Debug, 14天)
- DEBUG 模式启用控制台 Sink（含 Module/Operation 模板）
- 全局 Enricher：ThreadId / AppVersion / LogContext
- Shared 层添加 Serilog 4.2.0 NuGet 引用
- dotnet build 9 个项目零错误零警告，dotnet test 2/2 通过

### Task 0.2 - DI 容器 + 配置系统 (2026-04-12)
- 创建 IAppConfigProvider 接口（Shared 层 — AppVersion / 各路径 / 下载参数）
- 创建 AppConfigProvider 实现（Infrastructure 层 — 读取 IConfiguration，默认 %LOCALAPPDATA%）
- 更新 appsettings.json 添加 Paths 和 Downloads 配置节
- 实现 Program.cs 完整 DI 容器构建流程（ConfigurationBuilder → ServiceCollection → BuildServiceProvider）
- 实现各层 AddXxx() 扩展方法（Domain / Application / Infrastructure / Presentation / Background）
- 为各层项目添加 Microsoft.Extensions.DependencyInjection.Abstractions 包引用
- 为 Infrastructure 添加 Microsoft.Extensions.Configuration.Abstractions 包引用
- dotnet build 9 个项目零错误，dotnet test 2/2 通过

### Task 0.1 - 创建 Solution 和项目文件 (2026-04-12)
- 创建 HelsincyEpicLauncher.slnx 解决方案
- 创建 7 个源码项目：App / Presentation / Application / Domain / Infrastructure / Background / Shared
- 创建 2 个测试项目：Tests.Unit / Tests.Integration
- 配置项目引用关系（按架构依赖图）
- 引入全部 NuGet 包（WindowsAppSDK, CommunityToolkit.Mvvm, Serilog, SQLite, Polly, xUnit 等）
- 创建 Directory.Build.props（统一 TFM/版本/版权 + dotnet CLI 的 AppxPackage 路径修复）
- 创建 global.json（固定 .NET 9.0.309 SDK）
- 创建 .editorconfig 代码风格规范
- 创建 app.manifest（DPI 感知）
- 各层 DependencyInjection.cs 占位
- 2 个 Sanity 测试通过
- dotnet build 零错误零警告
- 技术变更：从 .NET 8 改为 .NET 9（系统无 .NET 8 SDK）

### 文档阶段 (2024-12-15)
- 创建 20+ 架构设计文档（docs/ 目录）
- 覆盖：项目总览、架构原则、解决方案结构、模块依赖、核心接口、10 个模块定义、下载子系统、状态管理、错误处理、启动流程、技术栈、AI 协作、开发阶段、反模式
- 新增日志策略文档（15-LoggingStrategy.md）
- 新增 AI 会话交接协议（12-AICollaboration.md § 8~11）
- 将开发计划细化为 41 个原子任务（13-DevelopmentPhases.md）
