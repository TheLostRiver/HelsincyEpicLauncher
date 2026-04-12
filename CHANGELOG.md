# Changelog

## [Unreleased]

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
