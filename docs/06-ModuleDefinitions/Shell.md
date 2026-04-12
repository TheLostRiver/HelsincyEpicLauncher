# Shell 模块

---

## 架构定义

### 职责

- 主窗口壳层（`MainWindow` + `ShellPage`）
- 左侧导航栏
- 顶部标题栏（自定义 TitleBar）
- 全局搜索入口
- 内容区 Frame（页面宿主）
- 下载悬浮面板入口（Mini 下载面板）
- Toast 通知宿主
- Dialog 弹窗宿主
- 全局状态栏（底部：网络状态、活跃下载数）

### 不负责

- 任何业务逻辑
- 任何数据持久化
- 任何后台任务
- 模块内部页面内容

### 依赖

| 依赖目标 | 用途 |
|---------|------|
| 所有模块的 Contracts | 路由到各模块页面 |
| `INavigationService` | 页面导航 |
| `IDialogService` | 弹窗管理 |
| `INotificationService` | Toast 通知 |
| `IDownloadReadService` | 下载面板状态 |
| `IAuthService` | 用户头像/登录状态 |

### 谁可以依赖 Shell

无。Shell 是顶层壳，不应被其他业务模块依赖。业务模块通过 `INavigationService`、`IDialogService`、`INotificationService` 间接通信。

---

## API 定义

### ShellViewModel

```csharp
public partial class ShellViewModel : ObservableObject
{
    // 导航状态
    [ObservableProperty] private string _currentRoute = "";
    [ObservableProperty] private bool _canGoBack;

    // 全局状态
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private int _activeDownloadCount;
    [ObservableProperty] private bool _isNetworkAvailable = true;

    // 导航命令
    [RelayCommand] private Task NavigateToFabLibrary();
    [RelayCommand] private Task NavigateToDownloads();
    [RelayCommand] private Task NavigateToEngineVersions();
    [RelayCommand] private Task NavigateToSettings();
    [RelayCommand] private Task NavigateToDiagnostics();
    [RelayCommand] private Task GoBack();
}
```

---

## 关键流程

### Shell 初始化流程

```
1. App 启动 → 创建 MainWindow
2. MainWindow 加载 ShellPage
3. ShellPage 初始化：
   a. 注入 ShellViewModel
   b. 注册导航路由表
   c. 恢复上次导航位置（或默认到 FabLibrary）
   d. 订阅 IDownloadReadService.ActiveCount 变化
   e. 订阅 IAuthService 状态变化
4. Shell 进入就绪状态
```

### 导航流程

```
1. 用户点击左侧导航项
2. ShellViewModel.NavigateToXxx 命令触发
3. 调用 INavigationService.NavigateAsync(route)
4. NavigationService 在 Frame 中加载目标 Page
5. 更新 ShellViewModel.CurrentRoute
6. 更新导航栏高亮状态
```

### UI 结构

```
MainWindow
└─ ShellPage
   ├─ TitleBar（自定义：应用图标 + 标题 + 最小化/最大化/关闭）
   ├─ NavigationRail（左侧导航栏）
   │  ├─ Fab 资产库
   │  ├─ 下载管理
   │  ├─ 引擎版本
   │  ├─ 插件管理
   │  ├─ ── 分割线 ──
   │  ├─ 设置
   │  └─ 诊断
   ├─ ContentFrame（页面宿主）
   ├─ DownloadMiniPanel（下载悬浮入口/迷你面板）
   ├─ StatusBar（底部状态栏）
   ├─ ToastLayer（Toast 通知层）
   └─ DialogLayer（弹窗层）
```
