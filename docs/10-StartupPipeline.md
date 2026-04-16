# 启动流程

> 启动器冷启动体验是用户的第一印象。本文档定义分阶段启动策略，确保最快显示可交互界面。

---

## 1. 核心目标

- **首帧展示 < 500ms**：窗口出现，显示骨架屏
- **可交互 < 2s**：Shell 导航栏可点击，主页面骨架展示
- **完全就绪 < 5s**：数据加载完成，后台服务全部启动

---

## 2. 启动阶段

### Phase 0：最小可显示（< 500ms）

**目标**：窗口出现在屏幕上，不白屏。

**执行内容**：
1. 单实例检查（Mutex）
2. 如果已有实例 → 通知前台 → 退出
3. 创建 `MainWindow`
4. 加载最小资源字典（主题色、图标）
5. 显示窗口 + 启动画面（Splash）或骨架屏

**此阶段禁止**：
- 读数据库
- 发网络请求
- 扫描文件系统
- 初始化 DI 容器

### Phase 1：核心初始化（500ms ~ 1.5s）

**目标**：DI 容器就绪，核心服务可用。

**执行内容**：
1. 初始化 DI 容器（注册所有服务）
2. 加载配置文件（`appsettings.json` → `user.settings.json`）
3. 初始化日志系统
4. 初始化 SQLite 数据库连接（含 Migration 检查）
5. 创建 ShellPage + ShellViewModel
6. 注册导航路由
7. 导航到默认页面（FabLibrary）
8. Shell 进入可交互状态（导航栏可点击）

**注意**：此阶段主页面数据还没加载，显示骨架屏/加载占位符。

### Phase 2：必要后台初始化（1.5s ~ 3s）

**目标**：恢复上次中断的状态，核心业务就绪。

**执行内容**（全部在后台线程）：
1. 会话恢复：`IAuthService.TryRestoreSessionAsync()`
2. 下载恢复：`IDownloadOrchestrator.RecoverAsync()`

> 实际实现中 Phase 2 通过 `InitializePhase2Async` 独立执行，
> 完成后才进入 Phase 3（`StartBackgroundServicesAsync`）。

**此阶段 UI 已经可用**：用户可以点导航栏、看骨架屏，但数据还在加载。

### Phase 3：延迟初始化（3s ~ 5s+）

**目标**：非关键功能就绪。

**执行内容**（后台，不阻塞 UI）：
1. Fab 资产目录刷新（远程同步）
2. 缩略图预热（预加载首屏缩略图）
3. 引擎版本列表刷新
4. 自动更新检查
5. 诊断信息收集（磁盘空间、内存等）
6. 清理上次崩溃遗留的临时文件

---

## 3. 启动流程时序图

```
App.OnLaunched
       │
       ├─ Phase 0 ─────────────────────────────────>
       │   Mutex 检查
       │   创建 MainWindow
       │   显示骨架屏
       │                                    [窗口可见]
       │
       ├─ Phase 1 ─────────────────────────────────>
       │   初始化 DI
       │   加载配置
       │   初始化日志
       │   打开 SQLite
       │   创建 Shell
       │   导航到默认页
       │                                    [可交互]
       │
       ├─ Phase 2（后台线程）──────────────────────>
       │   恢复会话
       │   恢复下载
       │   加载索引
       │                                    [核心就绪]
       │
       └─ Phase 3（后台，完全不阻塞）──────────────>
           刷新目录
           预热缩略图
           检查更新
           清理临时文件
                                             [完全就绪]
```

---

## 4. 实现要点

### 4.1 Phase 切换代码结构

```csharp
// App.xaml.cs — 实际实现
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    // === Phase 0：单实例检查 + 最小可显示 ===
    if (!EnsureSingleInstance()) { Environment.Exit(0); return; }
    StartPipeListener();

    // === Phase 1：核心初始化（DI + 配置 + 日志 + 数据库） ===
    InitializeCoreServices();
    _mainWindow = new MainWindow();
    InitializeTrayIcon();
    ConfigureCloseToTray();
    _mainWindow.Activate();
    _mainWindow.HideLoadingIndicator();

    // === Phase 2 + 3：后台恢复 + 延迟服务 ===
    _ = Task.Run(() => StartPostLaunchAsync(_appCts.Token));
}

private static async Task StartPostLaunchAsync(CancellationToken ct)
{
    await Task.Delay(200, ct).ConfigureAwait(false); // 等首帧渲染
    await InitializePhase2Async(ct).ConfigureAwait(false);
    await StartBackgroundServicesAsync(ct).ConfigureAwait(false);
}

// Phase 2：会话恢复 + 下载恢复
private static async Task InitializePhase2Async(CancellationToken ct)
{
    var authService = Services.GetRequiredService<IAuthService>();
    await authService.TryRestoreSessionAsync(ct).ConfigureAwait(false);
    var orchestrator = Services.GetRequiredService<IDownloadOrchestrator>();
    await orchestrator.RecoverAsync(ct).ConfigureAwait(false);
}

// Phase 3：后台 Worker 启动 + 延迟任务 TODO
private static Task StartBackgroundServicesAsync(CancellationToken ct)
{
    Services.GetRequiredService<TokenRefreshBackgroundService>().Start();
    Services.GetRequiredService<AutoInstallWorker>().Start();
    Services.GetRequiredService<AppUpdateWorker>().Start();
    Services.GetRequiredService<NetworkMonitorWorker>().Start();
    // TODO: 缩略图预热、诊断信息收集、Fab 目录远程同步、临时文件清理
    return Task.CompletedTask;
}
```

### 4.2 骨架屏策略

```
Phase 0 ~ Phase 1：
  整个窗口显示 Splash 或最简骨架屏

Phase 1 完成：
  Shell 框架展示，内容区显示页面级骨架屏
  （灰色占位卡片、加载动画）

Phase 2 完成：
  真实数据替换骨架屏
  （如果 Phase 2 很快，用户可能不会注意到骨架屏）

Phase 3 完成：
  缩略图全部就绪，后台服务全部运行
```

### 4.3 错误处理

| 阶段 | 失败处理 |
|------|---------|
| Phase 0 | 另一实例已运行 → 退出 |
| Phase 1 数据库 | Migration 失败 → 尝试重建；仍失败 → 弹窗提示 |
| Phase 1 配置 | 配置损坏 → 使用默认配置 + Toast 告知 |
| Phase 2 会话恢复 | Token 失效 → Shell 显示"请登录" |
| Phase 2 下载恢复 | 恢复失败的任务标记为 Error，不阻塞启动 |
| Phase 3 任何失败 | 仅记日志，不影响用户操作 |

---

## 5. 命令行参数

启动器支持以下命令行启动参数：

| 参数 | 说明 | 示例 |
|------|------|------|
| `--minimized` | 启动后最小化到托盘 | 开机自启场景 |
| `--download=<assetId>` | 启动后自动开始下载指定资产 | 外部链接唤起 |
| `--navigate=<route>` | 启动后导航到指定页面 | `--navigate=downloads` |

### 处理流程

```
1. App.OnLaunched 解析命令行参数
2. 如果 --minimized → Phase 0 后最小化到托盘，不显示窗口
3. 如果 --download=xxx → Phase 2 完成后自动创建下载任务
4. 如果 --navigate=xxx → Phase 1 的 NavigateToDefault 改为导航到指定路由
```

---

## 6. 系统托盘集成

```
启动时：
  注册系统托盘图标

关闭按钮行为：
  默认 → 最小化到托盘（不退出）
  Shift + 关闭 或 托盘右键"退出" → 真正退出

托盘菜单：
  显示主窗口
  暂停全部下载
  恢复全部下载
  ──分割线──
  关于
  退出

退出前检查：
  如果有活跃下载 → 弹窗确认"是否暂停并退出？"
  用户确认 → 暂停所有下载 → 保存 checkpoint → 退出
```

---

## 7. 开机自启动（可选）

```
设置中提供开关：
  "开机时自动启动"（默认关闭）

实现方式：
  在 Windows 注册表 HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
  写入启动路径 + --minimized 参数
```
