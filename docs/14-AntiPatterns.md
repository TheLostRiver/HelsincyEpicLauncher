# 反模式清单

> 本文档列出必须避免的架构灾难和代码坏味道。  
> 每条反模式都说明：问题症状、为什么危险、正确做法。

---

## 1. 架构级反模式

### AP-01：God Service（万能服务）

**症状**：

```csharp
// ❌ 一个 Service 管所有事
public class LauncherService
{
    public Task DownloadAsync(...) { }
    public Task InstallAsync(...) { }
    public Task RepairAsync(...) { }
    public Task ScanLibraryAsync(...) { }
    public Task RefreshCatalogAsync(...) { }
    public Task LoginAsync(...) { }
    public Task UpdateSettingsAsync(...) { }
    // ... 50+ 方法
}
```

**为什么危险**：
- AI 无法在上下文限制内理解整个类
- 改一个功能可能误伤其他功能
- 无法独立测试
- 职责边界完全消失

**正确做法**：
按业务域拆分为独立 Service，每个 Service 职责单一清晰。

---

### AP-02：Page Code-Behind 写业务

**症状**：

```csharp
// ❌ XAML Code-Behind 里写下载逻辑
public sealed partial class DownloadsPage : Page
{
    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(url);
        using var fs = File.Create(path);
        await response.Content.CopyToAsync(fs);
        // 更新 UI...
        // 写数据库...
        // 发通知...
    }
}
```

**为什么危险**：
- 无法测试（依赖 UI 生命周期）
- 业务逻辑和 UI 绑死
- 复杂度集中在无法复用的地方

**正确做法**：
Code-Behind 只处理纯视觉逻辑。业务通过 ViewModel → Application → Infrastructure。

---

### AP-03：ViewModel 巨石化

**症状**：

```csharp
// ❌ 一个 ViewModel 1500+ 行
public class DownloadsViewModel : ObservableObject
{
    // 下载逻辑
    // 筛选逻辑
    // 排序逻辑
    // 安装逻辑
    // 导航逻辑
    // 弹窗逻辑
    // 持久化逻辑
    // 日志逻辑
    // 统统在这里
}
```

**为什么危险**：
- AI 处理这种文件时上下文容易溢出
- 修改某个功能影响不可控
- 测试复杂度指数级增长

**正确做法**：
一个页面一个 ViewModel（< 400 行），列表项拆子 VM，弹窗拆独立 VM。复杂逻辑下沉到 Application 层。

---

### AP-04：跨模块共享可变对象

**症状**：

```csharp
// ❌ FabLibrary 和 Downloads 共用同一个 GameAsset 实例
var asset = await _fabRepo.GetByIdAsync(id);
await _downloadService.Download(asset);  // Downloads 内部修改 asset.State
// FabLibrary 的 asset 也被改了 ← 灾难
```

**为什么危险**：
- 隐式数据流，调试极困难
- 模块之间产生不可见的耦合
- AI 几乎不可能追踪这种共享状态的副作用

**正确做法**：
跨模块传递不可变 DTO（`init` 属性），各自持有独立副本。

---

### AP-05：全局静态状态横飞

**症状**：

```csharp
// ❌ 到处出现
public static class AppState
{
    public static UserInfo CurrentUser;
    public static bool IsDownloading;
    public static List<DownloadTask> ActiveTasks;
    public static SqliteConnection Database;
}
```

**为什么危险**：
- 任何地方都能读写，副作用不可控
- 无法在测试中隔离
- AI 很难可靠追踪静态全局状态的修改链

**正确做法**：
通过 DI 注入作用域化的 State 对象，显式声明依赖。

---

### AP-06：下载进度直接高频刷新 UI

**症状**：

```csharp
// ❌ 每个 chunk 每写入一次就更新 UI
chunk.BytesReceived += count;
DispatcherQueue.TryEnqueue(() => {
    ProgressBar.Value = chunk.BytesReceived;
    SpeedText.Text = CalculateSpeed();
});
// 1 秒内触发几百次 → UI 卡死
```

**为什么危险**：
- 这是官方 Epic Launcher 卡死的主要原因之一
- UI 线程被打爆，导致操作无响应

**正确做法**：
后台聚合进度（500ms），写入 RuntimeStore，ViewModel 订阅事件更新。

---

### AP-07：Repository 直接返回 UI 模型

**症状**：

```csharp
// ❌ 数据库查出来直接给 UI 用
public Task<DownloadCardModel> GetDownloadForUiAsync(int id)
{
    return _db.QueryAsync<DownloadCardModel>("SELECT ...");
}
```

**为什么危险**：
- 数据库 schema 变了，UI 直接崩
- UI 需求变了（多一个字段），要改 Repository
- 层次污染，重构极痛

**正确做法**：
Repository 返回领域模型或 DTO，ViewModel 通过 Mapper 转换为 UI 模型。

---

### AP-08：启动流程贪心初始化

**症状**：

```csharp
// ❌ App.OnLaunched 里同步做所有事
protected override void OnLaunched(...)
{
    InitializeDatabase();          // 同步等数据库
    ScanAllInstalledGames();       // 全盘扫描
    RefreshCatalog();              // 同步刷网络
    PreloadAllThumbnails();        // 同步加载缩略图
    RestoreAllDownloads();         // 同步恢复下载
    CheckForUpdates();             // 同步检查更新
    // 用户等了 10 秒还看不到窗口...
}
```

**为什么危险**：
- 造成"启动卡住、窗口白屏、面板点不开"
- 这也是官方 Epic Launcher 的典型问题

**正确做法**：
分阶段启动（参见 [10-StartupPipeline.md](10-StartupPipeline.md)）。Phase 0 先显示窗口，Phase 2/3 在后台异步执行。

---

## 2. 代码级反模式

### CP-01：UI 直接调 HttpClient

```csharp
// ❌
var client = new HttpClient();
var json = await client.GetStringAsync("https://api.fab.com/...");
```

**正确做法**：ViewModel → Application UseCase → Infrastructure ApiClient

---

### CP-02：绕过状态机直接改状态

```csharp
// ❌
downloadTask.State = DownloadState.Completed;

// ✅
var result = downloadTask.TransitionTo(DownloadState.Completed);
if (!result.IsSuccess) { /* 处理非法转换 */ }
```

---

### CP-03：省略 CancellationToken

```csharp
// ❌ 无法取消的操作
Task<Result> DownloadAsync(string url, string path);

// ✅
Task<Result> DownloadAsync(string url, string path, CancellationToken ct);
```

---

### CP-04：try/catch + MessageBox 暴力处理

```csharp
// ❌
try { await DoSomething(); }
catch (Exception ex) { MessageBox.Show(ex.Message); }

// ✅ 返回结构化 Result
var result = await DoSomething();
if (!result.IsSuccess)
{
    HandleError(result.Error);
}
```

---

### CP-05：自行 Task.Run 脱离生命周期

```csharp
// ❌ 后台任务无法优雅停止
Task.Run(async () => {
    while (true) { await ScanFiles(); await Task.Delay(60000); }
});

// ✅ 注册到 BackgroundTaskHost
public class LibraryScanWorker : IBackgroundWorker
{
    public async Task StartAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ScanFilesAsync(ct);
            await Task.Delay(60000, ct);
        }
    }
}
```

---

### CP-06：事件当作调用主干

```csharp
// ❌ 用事件替代正常方法调用
_messenger.Send(new PleaseStartDownloadMessage(assetId));  // 谁在处理？不确定

// ✅ 直接调用
await _downloadCommandService.StartAsync(request, ct);      // 明确、可追踪
```

事件只用于松耦合通知（已发生的事实），不用于请求/命令。

---

### CP-07：一个 DTO 承载过多职责

```csharp
// ❌ 万能模型
public class GameAssetModel
{
    // Fab 市场信息
    public string Title { get; set; }
    public decimal Price { get; set; }
    // 下载信息
    public double DownloadProgress { get; set; }
    public long BytesPerSecond { get; set; }
    // 安装信息
    public string InstallPath { get; set; }
    public bool NeedsRepair { get; set; }
    // 引擎信息
    public string EngineVersion { get; set; }
    // ... 50 个字段
}

// ✅ 按场景拆分
FabAssetSummary           // Fab 市场展示
DownloadStatusSummary     // 下载状态
InstallStatusSummary      // 安装状态
```

---

## 3. 检测方式

| 反模式 | 怎么检测 |
|--------|---------|
| God Service | 一个类超过 500 行或 15+ 公共方法 |
| ViewModel 巨石 | 一个 ViewModel 超过 400 行 |
| Code-Behind 业务 | Code-Behind 文件包含 using System.Net / System.IO / System.Data |
| 高频 UI 刷新 | Dispatcher 调用频率 > 10次/秒 |
| 贪心启动 | Phase 0 耗时 > 1s |
| 跨模块内部引用 | 非 Contracts 命名空间的跨模块 using |
| 全局静态状态 | `public static` 可变字段 |
| 缺少 CancellationToken | 接口方法缺少 `CancellationToken` 参数 |
