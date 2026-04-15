# 第4遍审查：Bug与边界条件

> 审查人：AI Agent  
> 日期：2026-04-16  
> 前序审查：01/02/03-Review-*.md  
> 审查重点：空引用、资源泄漏、线程安全、异常吞噬、CancellationToken 传递、async/await 陷阱、DB 操作、路径安全、并发竞态

---

## 审查范围

逐文件排查 Infrastructure/Downloads（最复杂子系统）、Infrastructure/Auth（安全敏感）、Infrastructure/Installations、Background Workers、Persistence、Shell/ViewModel 层的潜在 Bug 和边界条件。

---

## 一、下载子系统（Infrastructure/Downloads）

### R4-01（🔴 严重）— DownloadScheduler 调度异常静默丢失

**文件**：`Infrastructure/Downloads/DownloadScheduler.cs` L42, L90, L100

```csharp
_ = TryScheduleNextAsync(ct);
return Task.CompletedTask;
```

`TryScheduleNextAsync` 以 fire-and-forget 方式调用，如果抛出异常（如 `TaskReady` 委托抛出未预期异常），异常被完全吞噬——无日志、无传播。

**影响**：调度器静默停止工作，队列中的任务永远不会被处理，但不会有任何错误提示。

**修复建议**：将 fire-and-forget 包裹在异常处理中，或使用 `Task.Run(() => ...).ContinueWith(t => Log.Error(...), OnlyOnFaulted)`。

---

### R4-02（🟡 中等）— Polly 重试误捕 TaskCanceledException（用户取消被重试）

**文件**：`Infrastructure/Downloads/ChunkDownloadClient.cs` L168

```csharp
.Handle<TaskCanceledException>()
```

Polly 重试策略将 `TaskCanceledException` 配置为可重试异常，**但这包括用户主动取消**（通过 CancellationToken）。当用户暂停下载时，Polly 会重试 5 次（指数退避 1+2+4+8+16=31秒）才最终传播取消。

**影响**：暂停下载响应延迟最长 31 秒。

**修复建议**：在 Handle 条件中排除 CT 触发的取消：
```csharp
.Handle<TaskCanceledException>(ex => !ct.IsCancellationRequested)
```

---

### R4-03（🟡 中等）— ChunkDownloadClient HttpRequestMessage 创建后未使用且未释放

**文件**：`Infrastructure/Downloads/ChunkDownloadClient.cs` L60-61

```csharp
var httpRequest = new HttpRequestMessage(HttpMethod.Get, request.Url);
httpRequest.Headers.Range = new RangeHeaderValue(actualRangeStart, request.RangeEnd);
```

外层创建的 `httpRequest` 从未被使用——resilience pipeline 内部 lambda 会创建新的 `HttpRequestMessage`。外层 request 既是死代码又是资源泄漏（未 `Dispose`）。

---

### R4-04（🟡 中等）— DownloadOrchestrator.RecoverAsync 状态转换逻辑矛盾

**文件**：`Infrastructure/Downloads/DownloadOrchestrator.cs` L181-195

```csharp
if (task.CanTransitionTo(DownloadState.Failed))
    task.TransitionTo(DownloadState.Failed);
    
if (task.CanTransitionTo(DownloadState.Queued))
{
    task.TransitionTo(DownloadState.Queued);
```

先转为 `Failed`，然后立即检查能否转为 `Queued`。如果 `Failed → Queued` 不是有效转换，任务留在 `Failed`；如果有效，中间 `Failed` 状态从未被持久化（`UpdateAsync` 只在第二次转换后调用）。

**影响**：崩溃恢复可能将任务锁定在 `Failed` 状态，而非预期的重新入队。

---

### R4-05（🟡 中等）— DownloadOrchestrator.EnqueueAsync Path.GetPathRoot 空引用

**文件**：`Infrastructure/Downloads/DownloadOrchestrator.cs` L41

```csharp
var driveInfo = new DriveInfo(Path.GetPathRoot(request.DestinationPath)!);
```

`Path.GetPathRoot` 对相对路径或畸形路径返回 `null`，null-forgiving `!` 会导致 `DriveInfo` 构造函数抛出 `ArgumentNullException`。

**影响**：传入无效目标路径时直接崩溃。

---

### R4-06（🟡 中等）— CancellationTokenSource 作用域泄漏

**文件**：`Infrastructure/Downloads/DownloadScheduler.cs` L115-116

```csharp
var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
```

`ct` 来自 `QueueAsync` 的调用方。调用方的操作完成后 CT 可能被取消，导致与之关联的所有正在运行的下载被意外取消。

---

### R4-07（🟢 轻微）— SpeedCalculator Queue.Last() 是 O(n) 操作

**文件**：`Infrastructure/Downloads/DownloadRuntimeStore.cs` L139

```csharp
var newest = _samples.Last();
```

`Queue<T>.Last()` 需要枚举整个队列。在高频调用场景下（每个进度回调）可能有性能影响。建议改用单独的变量跟踪最新样本。

---

## 二、认证子系统（Infrastructure/Auth）

### R4-08（🔴 严重）— ShellViewModel.OnSessionExpired 在非 UI 线程修改 ObservableProperty

**文件**：`Presentation/Shell/ShellViewModel.cs` L177-180

```csharp
private void OnSessionExpired(SessionExpiredEvent evt)
{
    ClearUserInfo();  // 修改 IsAuthenticated, DisplayName 等 ObservableProperty
    Logger.Warning("会话已过期 | 原因={Reason}", evt.Reason);
}
```

`SessionExpired` 事件从 `TokenRefreshBackgroundService`（后台线程）触发。`ClearUserInfo()` 直接设置 `[ObservableProperty]` 值，触发 `PropertyChanged` 事件——在非 UI 线程触发 WinUI 属性变更会导致 `COMException` / 应用崩溃。

对比 `OnUpdateAvailable` 和 `OnNetworkStatusChanged` 正确使用了 `_dispatcherQueue.TryEnqueue()`，但 `OnSessionExpired` 遗漏了。

**修复建议**：
```csharp
private void OnSessionExpired(SessionExpiredEvent evt)
{
    _dispatcherQueue.TryEnqueue(() => ClearUserInfo());
    Logger.Warning("会话已过期 | 原因={Reason}", evt.Reason);
}
```

---

### R4-09（🟡 中等）— AuthService Token 刷新存在 TOCTOU 竞态

**文件**：`Infrastructure/Auth/AuthService.cs` L118-166

```csharp
lock (_lock) { tokens = _currentTokens; }
// ... 多个 await 调用（锁外） ...
lock (_lock) { _currentTokens = newTokens; }
```

锁仅保护 `_currentTokens` 的单次读写，但在读和写之间有多个 await。其他线程的 `LogoutAsync()` 可在此间隙清除 token，之后本方法又写回新 token——导致状态不一致（用户认为已登出但 token store 仍有 token）。

**影响**：已登出状态被刷新回已登录。

---

### R4-10（🟡 中等）— EpicOAuthHandler 客户端密钥硬编码

**文件**：`Infrastructure/Auth/EpicOAuthHandler.cs` L30-31

```csharp
private const string ClientId = "34a02cf8f4414e29b15921876da36f9a";
private const string ClientSecret = "daafbccc737745039dffe53d94fc76cf";
```

虽然 Epic 的启动器 Client ID/Secret 是半公开的，但硬编码凭据是安全反模式（OWASP: Sensitive Data Exposure）。

**建议**：移至配置文件或环境变量。

---

### R4-11（🟡 中等）— FileTokenStore 反序列化 DateTime.Kind 问题

**文件**：`Infrastructure/Auth/AuthService.cs` L31 + `FileTokenStore.cs`

```csharp
return _currentTokens is not null && _currentTokens.ExpiresAt > DateTime.UtcNow;
```

`ExpiresAt` 通过 JSON 往返后 `DateTime.Kind` 可能变为 `Unspecified`（`System.Text.Json` 默认行为），导致与 `DateTime.UtcNow` 比较时偏移时区差。

**影响**：应用重启后 Token 状态判断可能不准确。

---

### R4-12（🟡 中等）— EpicOAuthHandler.WaitForCallbackAsync CT 不一致

**文件**：`Infrastructure/Auth/EpicOAuthHandler.cs` L343

```csharp
await context.Response.OutputStream.WriteAsync(responseBytes, ct);
```

使用外层 `ct` 而非内部链接的 `cts.Token`。3 分钟超时和调用方取消是独立的，可能导致超时后响应写入挂起，或 CTS 泄漏。

---

## 三、App 启动（App.xaml.cs）

### R4-13（🔴 严重）— `.GetAwaiter().GetResult()` 死锁风险

**文件**：`App/App.xaml.cs` L286-287

```csharp
dbInitializer.InitializeAsync().GetAwaiter().GetResult();
```

在 UI 线程上同步等待异步操作。如果 `InitializeAsync` 内部任何 `await` 捕获了 SynchronizationContext（忘记 `ConfigureAwait(false)`），将导致死锁。当前代码之所以能工作是因为 `InitializeAsync` 的所有内部 await 都未捕获上下文——但这非常脆弱。

**修复建议**：将数据库初始化改为同步方法，或在 `Task.Run` 中调用：
```csharp
await Task.Run(() => dbInitializer.InitializeAsync());
```

---

### R4-14（🟡 中等）— StartPipeListener 无限循环无取消机制

**文件**：`App/App.xaml.cs` L207-225

```csharp
_ = Task.Run(async () => {
    while (true) {
        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
        await server.WaitForConnectionAsync();
```

无 `CancellationToken`，线程无法被干净终止。如果 `NamedPipeServerStream` 构造失败（系统资源耗尽），会进入无限紧循环消耗 CPU。

---

### R4-15（🟡 中等）— ShellViewModel 6 个事件订阅无退订（内存泄漏）

**文件**：`Presentation/Shell/ShellViewModel.cs` L82-93

```csharp
_authService.SessionExpired += OnSessionExpired;
_runtimeStore.SnapshotChanged += OnDownloadSnapshotChanged;
_runtimeStore.DownloadCompleted += _ => RefreshDownloadStatus();  // Lambda 无法退订
_runtimeStore.DownloadFailed += _ => RefreshDownloadStatus();     // Lambda 无法退订
_appUpdateService.UpdateAvailable += OnUpdateAvailable;
_networkMonitor.NetworkStatusChanged += OnNetworkStatusChanged;
```

ShellViewModel 订阅了 Singleton 服务的 6 个事件但**从未退订**（无 Dispose 方法）。Lambda 表达式 `_ => RefreshDownloadStatus()` 即使添加 Dispose 也无法退订。

**影响**：如果 ShellViewModel 被重建，旧实例不会被 GC 回收，且继续响应事件。

---

## 四、安装子系统（Infrastructure/Installations）

### R4-16（🟡 中等）— InstallationRepository.GetManifestPath 路径遍历风险

**文件**：`Infrastructure/Installations/InstallationRepository.cs` L102-106

```csharp
private static string GetManifestPath(string assetId)
{
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    return Path.Combine(appData, "HelsincyEpicLauncher", "manifests", $"{assetId}.json");
}
```

如果 `assetId` 包含路径遍历字符（如 `../../`），生成的路径可能逃逸目标目录，读写任意位置文件。

**影响**：路径遍历攻击（OWASP A01: Broken Access Control）

**修复建议**：对 `assetId` 进行清洗，移除路径分隔符和 `..`。

---

### R4-17（🟡 中等）— SemaphoreSlim 未释放

**文件**：
- `Infrastructure/Installations/HashingService.cs` L74
- `Infrastructure/Installations/IntegrityVerifier.cs` L72

```csharp
var semaphore = new SemaphoreSlim(maxParallelism);
// 使用后未 Dispose
```

`SemaphoreSlim` 实现 `IDisposable`，方法内创建但未释放，频繁调用会导致资源泄漏。

---

### R4-18（🟡 中等）— InstallationRepository 同步文件 IO 包装为 async

**文件**：`Infrastructure/Installations/InstallationRepository.cs` L84-86

```csharp
File.WriteAllText(path, json);     // 同步！
var json = File.ReadAllText(path); // 同步！
```

`SaveManifestAsync` / `GetManifestAsync` 方法签名为 `Task` 但内部使用同步 `File.ReadAllText` / `File.WriteAllText`，阻塞调用线程。

---

## 五、数据库层（Persistence/Sqlite）

### R4-19（🟡 中等）— RepositoryBase SQL 表名拼接（潜在注入向量）

**文件**：`Infrastructure/Persistence/Sqlite/RepositoryBase.cs` L49, L60, L73, L86, L96, L106, L110

```csharp
$"SELECT * FROM {TableName} WHERE id = @Id"
```

`TableName` 通过字符串插值拼入 SQL。虽然当前所有子类使用硬编码表名（安全），但模式本身是 SQL 注入向量。未来维护者可能引入动态表名。

---

### R4-20（🟡 中等）— 每次连接执行 PRAGMA journal_mode=WAL

**文件**：`Infrastructure/Persistence/Sqlite/SqliteConnectionFactory.cs` L36-39

```csharp
cmd.CommandText = "PRAGMA journal_mode=WAL;";
await cmd.ExecuteNonQueryAsync(ct);
```

WAL 模式是数据库级持久设置，每次创建连接都执行一次是浪费。在频繁创建连接的 Repository 模式下（每个方法一个连接），增加了不必要的延迟。

---

### R4-21（🟡 中等）— DeleteCheckpointAsync 非事务操作

**文件**：`Infrastructure/Downloads/DownloadTaskRepository.cs` L149-152

```csharp
await conn.ExecuteAsync("DELETE FROM chunk_checkpoints ...");
await conn.ExecuteAsync("DELETE FROM download_checkpoints ...");
```

两个 DELETE 操作未包裹在事务中（与 `SaveCheckpointAsync` 使用事务不同）。崩溃时可能导致孤立记录。

---

## 六、后台 Worker

### R4-22（🟡 中等）— TokenRefreshBackgroundService Timer 回调重叠

**文件**：`Background/Auth/TokenRefreshBackgroundService.cs` L50

```csharp
private async void OnTimerTick(object? state)
```

`Timer` 不等待回调完成就触发下一次。如果 `GetAccessTokenAsync` 耗时超过 2 分钟（慢网络），多个并发刷新请求可能互相竞争。

**修复建议**：在回调开始时禁用 Timer，完成后重新启用。

---

### R4-23（🟡 中等）— AutoInstallWorker 安装操作不可取消

**文件**：`Background/Installations/AutoInstallWorker.cs` L58

```csharp
private async void OnDownloadCompleted(DownloadCompletedEvent evt)
// 使用 CancellationToken.None
```

整个安装过程使用 `CancellationToken.None`，应用关闭时无法优雅取消正在进行的安装。

---

## 七、其他

### R4-24（🟢 轻微）— TrayIconManager ContextMenuStrip 未释放

**文件**：`App/TrayIconManager.cs` L35-39

`ContextMenuStrip` 是 `IDisposable`，但 `Dispose()` 只释放了 `_notifyIcon`，menu 未被跟踪释放。

---

### R4-25（🟢 轻微）— FabLibraryViewModel 裸 catch 无日志

**文件**：`Presentation/Modules/FabLibrary/FabLibraryViewModel.cs` L260-262

```csharp
catch
{
    _dispatcherQueue.TryEnqueue(() => IsThumbnailLoading = false);
}
```

裸 `catch` 无异常类型无日志记录。如果缩略图加载因系统性原因（磁盘满、权限不足）失败，不会有任何提示。

---

### R4-26（🟢 轻微）— DownloadsViewModel 所有命令使用 CancellationToken.None

**文件**：`Presentation/Modules/Downloads/DownloadsViewModel.cs`

所有异步操作传入 `CancellationToken.None`。用户离开页面后操作继续无意义消耗。

---

## 审查总结

| 类别 | 通过 | 🔴 | 🟡 | 🟢 |
|------|------|-----|-----|-----|
| 下载子系统 | - | 1 | 5 | 1 |
| 认证子系统 | - | 1 | 4 | 0 |
| App 启动 | - | 1 | 2 | 0 |
| 安装子系统 | - | 0 | 3 | 0 |
| 数据库层 | - | 0 | 3 | 0 |
| 后台 Worker | - | 0 | 2 | 0 |
| 其他 | - | 0 | 0 | 3 |

### 发现汇总

| ID | 严重度 | 位置 | 摘要 |
|----|--------|------|------|
| R4-01 | 🔴 | DownloadScheduler.cs | 调度火后不管，异常静默丢失 |
| R4-02 | 🟡 | ChunkDownloadClient.cs | Polly 误重试用户取消（CT），暂停响应延迟 31 秒 |
| R4-03 | 🟡 | ChunkDownloadClient.cs | HttpRequestMessage 未用未释放 |
| R4-04 | 🟡 | DownloadOrchestrator.cs | RecoverAsync 双重状态转换逻辑矛盾 |
| R4-05 | 🟡 | DownloadOrchestrator.cs | Path.GetPathRoot 空引用 |
| R4-06 | 🟡 | DownloadScheduler.cs | CTS 作用域泄漏 |
| R4-07 | 🟢 | DownloadRuntimeStore.cs | Queue.Last() O(n) 性能 |
| R4-08 | 🔴 | ShellViewModel.cs | OnSessionExpired 非 UI 线程修改 ObservableProperty（崩溃） |
| R4-09 | 🟡 | AuthService.cs | Token 刷新 TOCTOU 竞态 |
| R4-10 | 🟡 | EpicOAuthHandler.cs | 客户端密钥硬编码 |
| R4-11 | 🟡 | AuthService.cs + FileTokenStore | DateTime.Kind 反序列化问题 |
| R4-12 | 🟡 | EpicOAuthHandler.cs | CT 使用不一致 |
| R4-13 | 🔴 | App.xaml.cs | GetAwaiter().GetResult() UI 线程死锁风险 |
| R4-14 | 🟡 | App.xaml.cs | 管道监听无取消无错误恢复 |
| R4-15 | 🟡 | ShellViewModel.cs | 6 个事件订阅无退订（内存泄漏） |
| R4-16 | 🟡 | InstallationRepository.cs | assetId 路径遍历风险 |
| R4-17 | 🟡 | HashingService/IntegrityVerifier | SemaphoreSlim 未释放 |
| R4-18 | 🟡 | InstallationRepository.cs | 同步文件 IO 包装为 async |
| R4-19 | 🟡 | RepositoryBase.cs | SQL 表名拼接（注入向量） |
| R4-20 | 🟡 | SqliteConnectionFactory.cs | 每次连接执行 PRAGMA WAL |
| R4-21 | 🟡 | DownloadTaskRepository.cs | DeleteCheckpoint 非事务 |
| R4-22 | 🟡 | TokenRefreshBackgroundService | Timer 回调并发重叠 |
| R4-23 | 🟡 | AutoInstallWorker.cs | 安装操作不可取消 |
| R4-24 | 🟢 | TrayIconManager.cs | ContextMenuStrip 未释放 |
| R4-25 | 🟢 | FabLibraryViewModel.cs | 裸 catch 无日志 |
| R4-26 | 🟢 | DownloadsViewModel.cs | CancellationToken.None 资源浪费 |

**总计**：26 个问题（3 🔴 严重 + 19 🟡 中等 + 4 🟢 轻微）

### 累计发现（第1+2+3+4遍）

| 统计 | 第1遍 | 第2遍 | 第3遍 | 第4遍 | 累计 |
|------|-------|-------|-------|-------|------|
| 🔴 严重 | 1 | 1 | 1 | 3 | **6** |
| 🟡 中等 | 4 | 4 | 8 | 19 | **35** |
| 🟢 轻微 | 1 | 2 | 3 | 4 | **10** |
| **合计** | 6 | 7 | 12 | 26 | **51** |
