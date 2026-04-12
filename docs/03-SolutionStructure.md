# 解决方案结构

> 本文档定义 HelsincyEpicLauncher.sln 的项目拆分、每个项目的职责边界以及完整目录树。

---

## 1. 解决方案总览

```
HelsincyEpicLauncher.sln
│
├─ src/
│  ├─ Launcher.App                  // WinUI 3 启动项目（宿主壳）
│  ├─ Launcher.Presentation         // 页面、ViewModel、导航、UI 控件
│  ├─ Launcher.Application          // 用例、命令/查询、DTO、应用事件
│  ├─ Launcher.Domain               // 领域模型、状态机、规则、值对象
│  ├─ Launcher.Infrastructure       // HTTP、SQLite、文件系统、认证、缓存
│  ├─ Launcher.Background           // 后台 Worker、调度器、队列
│  └─ Launcher.Shared               // Result<T>、Error、Guard、基础类型
│
├─ tests/
│  ├─ Launcher.Tests.Unit           // 单元测试
│  └─ Launcher.Tests.Integration    // 集成测试
│
└─ docs/                            // 架构文档（即本目录）
```

> **注意**：原始设计中有独立的 `Launcher.Contracts` 项目。本方案改为 **每个模块自带 Contracts 目录**，避免 Contracts 项目成为新的耦合中心。跨模块接口放在对应模块的 `Contracts/` 子目录下，编译为同一程序集但命名空间隔离。

---

## 2. 各项目职责

### 2.1 Launcher.App

**类型**：WinUI 3 可执行项目（Packaged / Unpackaged）

**职责**：

- `App.xaml` / `App.xaml.cs` — 应用入口
- `MainWindow.xaml` — 主窗口
- DI 容器初始化（`ConfigureServices`）
- 启动流程编排（分阶段启动）
- 主题 / 资源字典 / 窗口生命周期
- 单实例保证（Mutex + 管道通信）
- 系统托盘注册
- 命令行参数解析

**它只是宿主**：不包含任何业务逻辑、页面、ViewModel。

**引用关系**：
```
Launcher.App → Launcher.Presentation
             → Launcher.Infrastructure （DI 注册实现类）
             → Launcher.Background     （DI 注册 Worker）
```

---

### 2.2 Launcher.Presentation

**类型**：类库（WinUI 3 XAML）

**职责**：

- Pages（所有页面 XAML + code-behind）
- UserControls（自定义控件）
- ViewModels（页面状态 + Command）
- Navigation（路由服务）
- Shell（主壳层：导航栏、标题栏、Frame、Toast 宿主、Dialog 宿主）
- Converters / Behaviors / Templates
- UI Mapper（领域模型 → UI 展示模型）

**使用的库**：`CommunityToolkit.Mvvm`

**引用关系**：
```
Launcher.Presentation → Launcher.Application
                      → Launcher.Domain（仅 Contracts / DTO / 枚举）
                      → Launcher.Shared
```

---

### 2.3 Launcher.Application

**类型**：类库（纯 C#）

**职责**：

- Commands / Queries — 用例入口
- Handlers — 用例编排
- DTOs — 进出应用层的数据传输对象
- Facades — 对外简化入口
- Application Events — 应用级事件
- Orchestrators — 复杂流程编排（如下载编排器）
- 前置校验 / 权限检查

**示例用例**：

```csharp
// 开始下载 Fab 资产
StartFabAssetDownloadUseCase

// 暂停下载
PauseDownloadUseCase

// 扫描本地已安装资产
ScanInstalledAssetsUseCase

// 刷新 Fab 资产目录
RefreshFabCatalogUseCase

// 修复损坏安装
RepairInstallationUseCase
```

**引用关系**：
```
Launcher.Application → Launcher.Domain
                     → Launcher.Shared
                     → 各模块 Contracts（接口，不引用实现）
```

---

### 2.4 Launcher.Domain

**类型**：类库（纯 C#，零外部依赖）

**职责**：

- 实体（Entity）— `DownloadTask`、`InstallJob`、`FabAsset`
- 值对象（ValueObject）— `DownloadTaskId`、`ChunkHash`、`AssetVersion`
- 枚举 — `DownloadState`、`InstallState`、`AssetCategory`
- 状态机 — `DownloadStateMachine`、`InstallStateMachine`
- 领域服务 — 纯业务规则
- 领域事件 — `DownloadCompletedEvent`、`InstallationFailedEvent`
- 业务规则 — 安装合法性、状态转换、磁盘空间约束

**铁律**：Domain 不依赖任何基础设施（不引用 HTTP、SQLite、文件系统）。

**引用关系**：
```
Launcher.Domain → Launcher.Shared（仅基础类型）
```

---

### 2.5 Launcher.Infrastructure

**类型**：类库

**职责**：

- `Persistence/` — SQLite 仓储实现、JSON 配置读写、Migration
- `Network/` — Epic/Fab API 客户端、Chunk 下载客户端、Manifest 客户端
- `FileSystem/` — 文件操作、磁盘空间检测、哈希计算、安装目录布局
- `Packaging/` — 压缩/解压、Patch 应用
- `Security/` — Token 存储（Windows Credential Locker）、OAuth 客户端
- `Logging/` — Serilog 适配器
- `Configuration/` — 配置提供器、用户设置仓储
- `Caching/` — 缩略图缓存、Manifest 缓存、搜索结果缓存

**铁律**：Infrastructure 实现 Contracts 中定义的接口，不向上层泄漏具体技术细节。

**引用关系**：
```
Launcher.Infrastructure → Launcher.Domain
                        → Launcher.Shared
                        → 各模块 Contracts（实现接口）
```

---

### 2.6 Launcher.Background

**类型**：类库

**职责**：

- 下载调度器（`DownloadScheduler`）
- 下载 Worker（`DownloadWorker`）
- 校验 Worker（`VerificationWorker`）
- 安装 Worker（`InstallWorker`）
- 修复 Worker（`RepairWorker`）
- 本地扫描 Worker（`LibraryScanWorker`）
- 缩略图预热 Worker（`ThumbnailPreloadWorker`）
- 自动更新检查 Worker（`AppUpdateWorker`）
- 统一后台任务宿主（`IBackgroundTaskHost`）

**设计原则**：所有 Worker 注册到统一的 `IBackgroundTaskHost`，按"队列 + 限流 + 可取消 + 可恢复"设计。不自己偷开 `Task.Run`。

**引用关系**：
```
Launcher.Background → Launcher.Application
                    → Launcher.Domain
                    → Launcher.Shared
                    → 各模块 Contracts
```

---

### 2.7 Launcher.Shared

**类型**：类库（纯 C#，零外部依赖）

**职责（严格控制体积）**：

- `Result<T>` / `Result` — 统一结果模型
- `Error` — 统一错误模型
- `PagedResult<T>` — 分页结果
- `Guard` — 参数校验
- `AsyncLock` — 异步锁
- `Debouncer` — 防抖
- `Clock` — 时间抽象（方便测试）
- 常量 / 基础扩展方法

**铁律**：Shared 只放真正跨模块的基础类型。一旦发现某个类只被一个模块使用，立刻移入该模块。

---

### 2.8 Launcher.Tests.Unit

**测试目标**：

- 状态机转换
- 领域规则
- 用例处理器
- ViewModel Command 行为
- Mapper 映射正确性

### 2.9 Launcher.Tests.Integration

**测试目标**：

- SQLite 仓储读写
- 下载断点恢复
- Manifest 解析
- 文件修复流程
- 配置加载

---

## 3. 完整目录树

### 3.1 Launcher.Presentation

```
Launcher.Presentation/
├─ Shell/
│  ├─ Views/
│  │  ├─ ShellPage.xaml
│  │  ├─ ShellPage.xaml.cs
│  │  └─ TitleBarView.xaml
│  ├─ ViewModels/
│  │  └─ ShellViewModel.cs
│  └─ Navigation/
│     ├─ INavigationService.cs
│     ├─ NavigationService.cs
│     └─ NavigationRoute.cs
│
├─ Modules/
│  ├─ FabLibrary/
│  │  ├─ Views/
│  │  │  ├─ FabLibraryPage.xaml         // Fab 资产浏览主页
│  │  │  ├─ FabAssetDetailPage.xaml     // 资产详情
│  │  │  └─ FabOwnedAssetsPanel.xaml    // 已拥有资产面板
│  │  ├─ ViewModels/
│  │  │  ├─ FabLibraryViewModel.cs
│  │  │  ├─ FabAssetCardViewModel.cs
│  │  │  └─ FabAssetDetailViewModel.cs
│  │  └─ Mappers/
│  │     └─ FabAssetUiMapper.cs
│  │
│  ├─ Downloads/
│  │  ├─ Views/
│  │  │  ├─ DownloadsPage.xaml
│  │  │  └─ DownloadItemControl.xaml
│  │  ├─ ViewModels/
│  │  │  ├─ DownloadsPageViewModel.cs
│  │  │  └─ DownloadItemViewModel.cs
│  │  └─ Mappers/
│  │
│  ├─ EngineVersions/
│  │  ├─ Views/
│  │  ├─ ViewModels/
│  │  └─ Mappers/
│  │
│  ├─ Plugins/
│  │  ├─ Views/
│  │  ├─ ViewModels/
│  │  └─ Mappers/
│  │
│  ├─ Settings/
│  │  ├─ Views/
│  │  │  ├─ SettingsPage.xaml
│  │  │  ├─ GeneralSettingsSection.xaml
│  │  │  └─ DownloadSettingsSection.xaml
│  │  └─ ViewModels/
│  │     └─ SettingsViewModel.cs
│  │
│  └─ Diagnostics/
│     ├─ Views/
│     │  ├─ DiagnosticsPage.xaml
│     │  └─ LogViewerPanel.xaml
│     └─ ViewModels/
│        └─ DiagnosticsViewModel.cs
│
└─ Common/
   ├─ Controls/                  // 自定义通用控件
   ├─ Converters/                // 值转换器
   ├─ Behaviors/                 // 附加行为
   ├─ Templates/                 // 数据模板
   └─ Theme/                     // 主题资源字典
```

### 3.2 Launcher.Application

```
Launcher.Application/
├─ Abstractions/
│  ├─ IAppSession.cs
│  └─ IFeatureFlagProvider.cs
│
├─ Modules/
│  ├─ FabLibrary/
│  │  ├─ Contracts/
│  │  │  ├─ IFabCatalogReadService.cs
│  │  │  ├─ IFabAssetCommandService.cs
│  │  │  └─ FabDtos.cs
│  │  ├─ Commands/
│  │  │  └─ DownloadFabAssetCommand.cs
│  │  ├─ Queries/
│  │  │  ├─ SearchFabAssetsQuery.cs
│  │  │  └─ GetOwnedAssetsQuery.cs
│  │  └─ Handlers/
│  │     ├─ DownloadFabAssetHandler.cs
│  │     ├─ SearchFabAssetsHandler.cs
│  │     └─ GetOwnedAssetsHandler.cs
│  │
│  ├─ Downloads/
│  │  ├─ Contracts/
│  │  │  ├─ IDownloadReadService.cs
│  │  │  ├─ IDownloadCommandService.cs
│  │  │  ├─ IDownloadOrchestrator.cs
│  │  │  └─ DownloadDtos.cs
│  │  ├─ Commands/
│  │  │  ├─ StartDownloadCommand.cs
│  │  │  ├─ PauseDownloadCommand.cs
│  │  │  └─ ResumeDownloadCommand.cs
│  │  └─ Handlers/
│  │     ├─ StartDownloadHandler.cs
│  │     ├─ PauseDownloadHandler.cs
│  │     └─ ResumeDownloadHandler.cs
│  │
│  ├─ Installations/
│  │  ├─ Contracts/
│  │  │  ├─ IInstallCommandService.cs
│  │  │  ├─ IInstallReadService.cs
│  │  │  └─ InstallDtos.cs
│  │  ├─ Commands/
│  │  └─ Handlers/
│  │
│  ├─ Auth/
│  │  ├─ Contracts/
│  │  │  ├─ IAuthService.cs
│  │  │  └─ AuthDtos.cs
│  │  ├─ Commands/
│  │  └─ Handlers/
│  │
│  ├─ EngineVersions/
│  │  ├─ Contracts/
│  │  ├─ Commands/
│  │  └─ Handlers/
│  │
│  └─ Settings/
│     ├─ Contracts/
│     ├─ Commands/
│     └─ Handlers/
│
└─ Events/
   ├─ DownloadQueuedEvent.cs
   ├─ DownloadCompletedEvent.cs
   ├─ InstallationCompletedEvent.cs
   ├─ InstallationFailedEvent.cs
   └─ SessionExpiredEvent.cs
```

### 3.3 Launcher.Domain

```
Launcher.Domain/
├─ Common/
│  ├─ Entity.cs
│  ├─ ValueObject.cs
│  ├─ DomainEvent.cs
│  ├─ Enumeration.cs
│  └─ StateMachine.cs          // 状态机基类
│
├─ Modules/
│  ├─ Downloads/
│  │  ├─ Entities/
│  │  │  ├─ DownloadTask.cs
│  │  │  ├─ DownloadCheckpoint.cs
│  │  │  └─ ChunkProgress.cs
│  │  ├─ ValueObjects/
│  │  │  ├─ DownloadTaskId.cs
│  │  │  ├─ ChunkId.cs
│  │  │  └─ DownloadPriority.cs
│  │  ├─ Enums/
│  │  │  ├─ DownloadState.cs
│  │  │  └─ DownloadUiState.cs  // 对外 UI 投影状态（收敛后）
│  │  ├─ Services/
│  │  │  └─ DownloadStateMachine.cs
│  │  └─ Rules/
│  │     └─ DownloadTransitionRules.cs
│  │
│  ├─ Installations/
│  │  ├─ Entities/
│  │  │  ├─ InstallJob.cs
│  │  │  └─ PatchPlan.cs
│  │  ├─ ValueObjects/
│  │  │  └─ InstallPath.cs
│  │  ├─ Enums/
│  │  │  └─ InstallState.cs
│  │  └─ Services/
│  │     └─ InstallStateMachine.cs
│  │
│  ├─ FabLibrary/
│  │  ├─ Entities/
│  │  │  └─ FabAsset.cs
│  │  ├─ ValueObjects/
│  │  │  ├─ FabAssetId.cs
│  │  │  ├─ AssetVersion.cs
│  │  │  └─ AssetCategory.cs
│  │  └─ Enums/
│  │     └─ AssetOwnershipState.cs
│  │
│  └─ EngineVersions/
│     ├─ Entities/
│     │  └─ EngineVersion.cs
│     └─ ValueObjects/
│        └─ EngineVersionId.cs
```

### 3.4 Launcher.Infrastructure

```
Launcher.Infrastructure/
├─ Persistence/
│  ├─ Sqlite/
│  │  ├─ LauncherDbContext.cs
│  │  ├─ Repositories/
│  │  │  ├─ SqliteDownloadTaskRepository.cs
│  │  │  ├─ SqliteFabAssetRepository.cs
│  │  │  └─ SqliteInstallationRepository.cs
│  │  └─ Migrations/
│  └─ Json/
│     └─ JsonUserSettingsRepository.cs
│
├─ Network/
│  ├─ Epic/
│  │  ├─ EpicAuthClient.cs
│  │  └─ EpicOAuthHandler.cs
│  ├─ Fab/
│  │  ├─ FabCatalogApiClient.cs
│  │  ├─ FabManifestApiClient.cs
│  │  └─ FabModels/              // API 响应模型（内部使用）
│  └─ Download/
│     ├─ ChunkDownloadClient.cs
│     └─ CdnFallbackHandler.cs
│
├─ FileSystem/
│  ├─ FileSystemService.cs
│  ├─ InstallLayoutService.cs
│  ├─ DiskSpaceService.cs
│  └─ HashingService.cs
│
├─ Packaging/
│  ├─ ArchiveExtractor.cs
│  └─ PatchApplier.cs
│
├─ Security/
│  ├─ WindowsCredentialStore.cs
│  └─ TokenRefreshService.cs
│
├─ Logging/
│  └─ SerilogAdapter.cs
│
├─ Caching/
│  ├─ ThumbnailCacheService.cs
│  ├─ ManifestCacheService.cs
│  └─ SearchResultCacheService.cs
│
└─ Configuration/
   ├─ AppSettingsProvider.cs
   └─ UserSettingsRepository.cs
```

### 3.5 Launcher.Background

```
Launcher.Background/
├─ Hosting/
│  ├─ IBackgroundTaskHost.cs
│  └─ BackgroundTaskHost.cs
│
├─ Downloads/
│  ├─ DownloadOrchestrator.cs
│  ├─ DownloadScheduler.cs
│  ├─ DownloadWorker.cs
│  ├─ VerificationWorker.cs
│  └─ DownloadRecoveryService.cs
│
├─ Installations/
│  ├─ InstallWorker.cs
│  └─ RepairWorker.cs
│
├─ FabLibrary/
│  ├─ FabCatalogSyncWorker.cs
│  └─ ThumbnailPreloadWorker.cs
│
└─ Updates/
   └─ AppUpdateWorker.cs
```

---

## 4. 项目引用关系图

```
                    ┌──────────────┐
                    │ Launcher.App │
                    └──────┬───────┘
            ┌──────────────┼──────────────────┐
            ▼              ▼                  ▼
   ┌─────────────┐  ┌──────────────┐  ┌─────────────┐
   │ Presentation │  │Infrastructure│  │  Background  │
   └──────┬──────┘  └──────┬───────┘  └──────┬──────┘
          │                │                  │
          ▼                │                  │
   ┌─────────────┐        │                  │
   │ Application  │◄───────┘──────────────────┘
   └──────┬──────┘
          │
          ▼
   ┌─────────────┐
   │   Domain     │
   └──────┬──────┘
          │
          ▼
   ┌─────────────┐
   │   Shared     │
   └─────────────┘
```

> 箭头方向 = 引用方向 = 依赖方向。一切依赖最终汇聚到 Shared。

---

## 5. 依赖注入注册策略

在 `Launcher.App` 的 `ConfigureServices` 中统一注册：

```csharp
// === 生命周期原则 ===
// 全局状态/协调器：Singleton
// 页面 ViewModel：Transient
// 轻量无状态 Service：Singleton
// 每次用例对象：Transient

// --- Shell & 导航 ---
services.AddSingleton<INavigationService, NavigationService>();
services.AddSingleton<IDialogService, DialogService>();
services.AddSingleton<INotificationService, NotificationService>();

// --- 认证 ---
services.AddSingleton<IAuthService, EpicAuthService>();
services.AddSingleton<ITokenStore, WindowsCredentialStore>();

// --- Fab 资产库 ---
services.AddSingleton<IFabCatalogReadService, FabCatalogService>();
services.AddSingleton<IFabAssetRepository, SqliteFabAssetRepository>();

// --- 下载 ---
services.AddSingleton<IDownloadOrchestrator, DownloadOrchestrator>();
services.AddSingleton<IDownloadScheduler, DownloadScheduler>();
services.AddSingleton<IDownloadRuntimeStore, DownloadRuntimeStore>();
services.AddSingleton<IChunkDownloader, ChunkDownloadClient>();

// --- 安装 ---
services.AddSingleton<IIntegrityVerifier, IntegrityVerifier>();
services.AddSingleton<IInstallApplier, InstallApplier>();

// --- 基础设施 ---
services.AddSingleton<IFileSystemService, FileSystemService>();
services.AddSingleton<IHashingService, HashingService>();

// --- 后台任务 ---
services.AddSingleton<IBackgroundTaskHost, BackgroundTaskHost>();

// --- ViewModel ---
services.AddTransient<FabLibraryViewModel>();
services.AddTransient<DownloadsPageViewModel>();
services.AddTransient<SettingsViewModel>();
services.AddTransient<DiagnosticsViewModel>();
```

---

## 6. 单实例与系统托盘

### 6.1 单实例保证

启动时用 `Mutex` 检测是否已有实例运行：

```csharp
// App.xaml.cs
private static Mutex? _mutex;

protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    _mutex = new Mutex(true, "HelsincyEpicLauncher_SingleInstance", out bool isNew);
    if (!isNew)
    {
        // 通过命名管道通知已有实例
        NotifyExistingInstance(args);
        Environment.Exit(0);
        return;
    }
    // 正常启动...
}
```

### 6.2 系统托盘

使用 Windows App SDK 的通知 API 或 Win32 `NotifyIcon` 互操作：
- 最小化到托盘
- 托盘右键菜单：显示主窗口 / 暂停全部下载 / 退出
- 下载完成时托盘气泡通知
