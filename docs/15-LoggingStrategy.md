# 日志策略

> 日志是项目的「黑匣子」。当 AI 上下文丢失、用户反馈 bug、后台任务静默失败时，  
> 唯一可靠的信息来源就是日志。本文档定义日志架构、分级策略、字段规范和运维规则。

---

## 1. 核心原则

| 编号 | 原则 | 说明 |
|------|------|------|
| L-01 | **每个操作都有迹可循** | 任何可能失败的操作都必须留下日志 |
| L-02 | **结构化优先** | 所有日志必须是结构化的（不是拼接字符串） |
| L-03 | **关联 ID 贯穿全链路** | 一个用户操作从 UI 到底层的所有日志共享同一个 CorrelationId |
| L-04 | **绝不记录敏感信息** | Token、密码、个人信息禁止写入日志 |
| L-05 | **日志不影响性能** | 异步写入，高频路径用采样或节流 |
| L-06 | **日志可被 AI 解析** | 格式统一、字段固定，AI 能自动分析日志排查问题 |

---

## 2. 技术选型

```
Serilog                          → 结构化日志核心
Serilog.Sinks.File               → 文件输出（主要 Sink）
Serilog.Sinks.Console            → 开发时控制台输出
Serilog.Enrichers.Thread         → 线程 ID 附加
Serilog.Enrichers.Environment    → 机器名/进程名附加
Serilog.Formatting.Compact       → JSON 紧凑格式
```

---

## 3. 日志级别定义

| 级别 | 用途 | 示例 |
|------|------|------|
| **Verbose** | 极细节调试信息，仅开发时开启 | 分块下载每个 chunk 的字节范围 |
| **Debug** | 开发/调试用信息 | 状态机转换细节、DI 注册列表 |
| **Information** | 关键业务操作的正常执行 | 下载开始、安装完成、用户登录 |
| **Warning** | 异常但可恢复的情况 | Token 即将过期、磁盘空间低、重试第 N 次 |
| **Error** | 操作失败但应用可继续运行 | 下载失败、API 调用失败、文件校验失败 |
| **Fatal** | 应用无法继续运行 | DI 初始化失败、数据库损坏、未处理异常 |

### 3.1 生产级别配置

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Launcher.Infrastructure.Network": "Debug",
        "Launcher.Background.Downloads": "Debug"
      }
    }
  }
}
```

> 下载和网络模块默认 Debug 级别，因为这是最常出问题的地方。

---

## 4. 结构化字段规范

### 4.1 全局字段（自动附加到每条日志）

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `Timestamp` | DateTime | UTC 时间戳 |
| `Level` | string | 日志级别 |
| `ThreadId` | int | 线程 ID |
| `SourceContext` | string | 产生日志的类型全名 |
| `MachineName` | string | 机器名 |
| `AppVersion` | string | 启动器版本号 |

### 4.2 业务字段（手动传入）

| 字段名 | 类型 | 使用场景 | 说明 |
|--------|------|---------|------|
| `CorrelationId` | Guid | 所有操作 | 从 UI 事件开始到底层结束的唯一 ID |
| `Module` | string | 所有操作 | 模块名（Auth / Downloads / Fab 等） |
| `Operation` | string | 所有操作 | 操作名（StartDownload / Login / SearchAssets） |
| `TaskId` | Guid | 下载/安装 | 下载或安装任务 ID |
| `Phase` | string | 下载/安装 | 当前阶段（Downloading / Verifying / Extracting） |
| `UserId` | string | 认证后 | Epic 用户 ID（脱敏） |
| `Duration` | long | 完成时 | 操作耗时（毫秒） |
| `ErrorCode` | string | 失败时 | 统一错误码（DL_003, AUTH_001 等） |
| `RetryCount` | int | 重试时 | 第几次重试 |
| `BytesTotal` | long | 下载 | 总字节数 |
| `BytesCompleted` | long | 下载 | 已完成字节数 |

### 4.3 示例：完整日志条目（JSON）

```json
{
  "Timestamp": "2024-12-15T08:30:15.234Z",
  "Level": "Information",
  "ThreadId": 12,
  "SourceContext": "Launcher.Background.Downloads.DownloadWorker",
  "AppVersion": "1.0.0",
  "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Module": "Downloads",
  "Operation": "StartDownload",
  "TaskId": "d1e2f3a4-b5c6-7890-abcd-1234567890ef",
  "Message": "下载任务开始 {TaskId}，总大小 {BytesTotal} bytes",
  "BytesTotal": 1073741824,
  "Properties": {}
}
```

---

## 5. 关联 ID（CorrelationId）机制

### 5.1 原理

CorrelationId 是一个 Guid，在用户触发操作时生成，然后沿调用链向下传递：

```
用户点击"下载" 
  → ViewModel（生成 CorrelationId）
    → Application.StartDownloadHandler（携带 CorrelationId）
      → Domain.DownloadStateMachine（携带 CorrelationId）
        → Infrastructure.ChunkDownloadClient（携带 CorrelationId）
          → Background.DownloadWorker（携带 CorrelationId）
```

同一个 CorrelationId 贯穿所有日志，搜索这个 ID 就能看到完整链路。

### 5.2 实现方式

```csharp
namespace Launcher.Shared.Logging;

/// <summary>
/// 操作上下文。每次用户操作创建一个，沿调用链传递。
/// </summary>
public sealed class OperationContext
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
    public string Module { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// 将上下文字段推入 Serilog LogContext
    /// </summary>
    public IDisposable PushToLogContext()
    {
        return LogContext.PushProperty("CorrelationId", CorrelationId)
            .Chain(LogContext.PushProperty("Module", Module))
            .Chain(LogContext.PushProperty("Operation", Operation));
    }
}
```

### 5.3 ViewModel 用法

```csharp
[RelayCommand]
private async Task StartDownloadAsync(FabAssetSummary asset)
{
    var ctx = new OperationContext
    {
        Module = "Downloads",
        Operation = "StartDownload"
    };

    using (ctx.PushToLogContext())
    {
        _logger.Information("用户触发下载 {AssetName}", asset.Name);

        var result = await _downloadCommandService.StartDownloadAsync(
            asset.DownloadUrl, asset.TotalSize, ctx.CorrelationId);

        if (result.IsFailure)
        {
            _logger.Warning("下载创建失败 {ErrorCode}: {ErrorMessage}",
                result.Error.Code, result.Error.TechnicalMessage);
        }
    }
}
```

---

## 6. 日志模板规范

### 6.1 消息模板规则

```csharp
// ✅ 正确：使用结构化模板参数
_logger.Information("下载任务 {TaskId} 开始，总大小 {BytesTotal} bytes", taskId, totalSize);

// ❌ 错误：字符串拼接
_logger.Information($"下载任务 {taskId} 开始，总大小 {totalSize} bytes");  

// ❌ 错误：没有命名参数
_logger.Information("下载任务 {0} 开始", taskId);
```

### 6.2 各操作标准模板

#### 操作开始

```csharp
_logger.Information("{Operation} 开始 | {TaskId}", "StartDownload", taskId);
```

#### 操作成功

```csharp
_logger.Information("{Operation} 完成 | {TaskId} | 耗时 {Duration}ms", 
    "StartDownload", taskId, sw.ElapsedMilliseconds);
```

#### 操作失败

```csharp
_logger.Error(ex, "{Operation} 失败 | {TaskId} | 错误码 {ErrorCode} | {ErrorMessage}",
    "StartDownload", taskId, error.Code, error.TechnicalMessage);
```

#### 重试

```csharp
_logger.Warning("{Operation} 第 {RetryCount} 次重试 | {TaskId} | 原因: {Reason}",
    "ChunkDownload", retryCount, taskId, reason);
```

#### 状态变更

```csharp
_logger.Information("状态转换 {TaskId}: {FromState} → {ToState}",
    taskId, fromState, toState);
```

---

## 7. 各模块日志要求

### 7.1 Auth 模块

| 事件 | 级别 | 必带字段 | 注意事项 |
|------|------|---------|---------|
| 开始 OAuth 授权 | Info | CorrelationId | **不记录 client_secret** |
| OAuth code 获取成功 | Info | CorrelationId | **不记录 code 值** |
| Token 兑换成功 | Info | CorrelationId, UserId | **不记录 token 值** |
| Token 刷新成功 | Info | CorrelationId | |
| Token 刷新失败 | Error | CorrelationId, ErrorCode | |
| 会话恢复成功 | Info | UserId | |
| 会话恢复失败 | Warning | ErrorCode | |
| 登出 | Info | UserId | |

### 7.2 Downloads 模块

| 事件 | 级别 | 必带字段 |
|------|------|---------|
| 创建下载任务 | Info | TaskId, BytesTotal, CorrelationId |
| 开始下载 | Info | TaskId |
| 分块分配 | Debug | TaskId, ChunkIndex, ByteStart, ByteEnd |
| 分块完成 | Debug | TaskId, ChunkIndex, Duration |
| 分块失败 | Warning | TaskId, ChunkIndex, RetryCount, ErrorCode |
| Checkpoint 保存 | Debug | TaskId, BytesCompleted |
| 暂停 | Info | TaskId, BytesCompleted |
| 恢复 | Info | TaskId, BytesCompleted |
| 下载完成 | Info | TaskId, Duration, AverageSpeed |
| 下载失败 | Error | TaskId, ErrorCode, RetryCount |
| 崩溃恢复 | Info | TaskId, BytesCompleted |
| CDN 回退 | Warning | TaskId, OriginalUrl, FallbackUrl |
| 速率限制 | Warning | TaskId, RetryAfterSeconds |

### 7.3 Installations 模块

| 事件 | 级别 | 必带字段 |
|------|------|---------|
| 开始安装 | Info | TaskId, InstallPath |
| 文件解压/复制 | Debug | TaskId, FileName |
| 安装完成 | Info | TaskId, Duration, FileCount |
| 校验开始 | Info | TaskId, FileCount |
| 校验失败 | Warning | TaskId, CorruptedFiles |
| 修复开始 | Info | TaskId, FilesToRepair |
| 卸载 | Info | TaskId, InstallPath |

### 7.4 FabLibrary 模块

| 事件 | 级别 | 必带字段 |
|------|------|---------|
| 搜索请求 | Info | Query, Filters, CorrelationId |
| 搜索结果 | Debug | Query, ResultCount, Duration |
| 资产详情加载 | Debug | AssetId |
| 缩略图缓存命中 | Verbose | AssetId |
| 缩略图下载 | Debug | AssetId, Duration |
| API 调用失败 | Error | Endpoint, ErrorCode, StatusCode |

### 7.5 高频日志节流

下载进度和分块更新是高频事件，需要节流：

```csharp
// 分块进度每 5 秒记录一次，而不是每次更新
private readonly TimeSpan _progressLogInterval = TimeSpan.FromSeconds(5);
private DateTime _lastProgressLog = DateTime.MinValue;

private void OnChunkProgress(ChunkProgressEventArgs e)
{
    if (DateTime.UtcNow - _lastProgressLog >= _progressLogInterval)
    {
        _logger.Debug("下载进度 {TaskId}: {Percent:F1}% ({BytesCompleted}/{BytesTotal})",
            e.TaskId, e.Percent, e.BytesCompleted, e.BytesTotal);
        _lastProgressLog = DateTime.UtcNow;
    }
}
```

---

## 8. 文件输出与轮转策略

### 8.1 日志文件布局

```
%LOCALAPPDATA%/MyEpicLauncher/Logs/
├─ app-20241215.log          ← 当天日志
├─ app-20241214.log          ← 前一天
├─ app-20241213.log          ← ...
├─ app-20241212.log
├─ error-20241215.log        ← 仅 Error + Fatal 级别（单独文件）
└─ download-20241215.log     ← 下载模块独立日志（Debug 级别）
```

### 8.2 Serilog 配置

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("AppVersion", appVersion)
    .Enrich.FromLogContext()
    // 主日志文件：Information 及以上
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(logDir, "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,        // 保留 30 天
        fileSizeLimitBytes: 100_000_000,   // 单文件最大 100MB
        rollOnFileSizeLimit: true,
        restrictedToMinimumLevel: LogEventLevel.Information)
    // 错误日志：仅 Error + Fatal
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(logDir, "error-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90,        // 错误日志保留 90 天
        restrictedToMinimumLevel: LogEventLevel.Error)
    // 下载专用日志：Debug 级别
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(Matching.FromSource("Launcher.Background.Downloads"))
        .WriteTo.File(
            new CompactJsonFormatter(),
            Path.Combine(logDir, "download-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,    // 下载日志保留 14 天
            fileSizeLimitBytes: 200_000_000))
    // 开发时控制台
    #if DEBUG
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{Module}/{Operation}] {Message:lj}{NewLine}{Exception}")
    #endif
    .CreateLogger();
```

### 8.3 磁盘空间保护

```csharp
/// <summary>
/// 日志写入前检查磁盘空间。低于阈值时仅写 Error/Fatal。
/// </summary>
internal sealed class DiskSpaceAwareLogFilter : ILogEventFilter
{
    private const long MinFreeSpaceBytes = 500_000_000; // 500MB
    private readonly string _logDrive;
    private DateTime _lastCheck = DateTime.MinValue;
    private bool _lowDiskSpace = false;

    public bool IsEnabled(LogEvent logEvent)
    {
        // 每分钟检查一次
        if (DateTime.UtcNow - _lastCheck > TimeSpan.FromMinutes(1))
        {
            var driveInfo = new DriveInfo(_logDrive);
            _lowDiskSpace = driveInfo.AvailableFreeSpace < MinFreeSpaceBytes;
            _lastCheck = DateTime.UtcNow;
        }

        // 磁盘空间不足时只保留 Error 和 Fatal
        if (_lowDiskSpace)
            return logEvent.Level >= LogEventLevel.Error;

        return true;
    }
}
```

---

## 9. 安全规则

### 9.1 禁止记录的内容

| 类别 | 禁止记录 | 替代方案 |
|------|---------|---------|
| 认证 | access_token, refresh_token, client_secret | 记录 token 类型和有效期 |
| 用户信息 | 邮箱、真实姓名 | 使用脱敏的 UserId |
| URL 参数 | 包含 token 的 URL | 截断 query string |
| 文件路径 | 用户个人目录完整路径 | 使用 `%USERPROFILE%` 替代 |
| HTTP Body | 包含凭证的请求体 | 记录 Content-Length |

### 9.2 脱敏工具

```csharp
namespace Launcher.Shared.Logging;

/// <summary>
/// 日志字段脱敏工具
/// </summary>
internal static class LogSanitizer
{
    /// <summary>
    /// 脱敏 token：保留前 4 位 + ... + 后 4 位
    /// </summary>
    public static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 12)
            return "***";
        return $"{token[..4]}...{token[^4..]}";
    }

    /// <summary>
    /// 脱敏 URL：移除 query string 中的敏感参数
    /// </summary>
    public static string SanitizeUrl(string url)
    {
        var sensitiveParams = new[] { "token", "code", "key", "secret", "password" };
        // 将匹配的参数值替换为 ***
        foreach (var param in sensitiveParams)
        {
            url = Regex.Replace(url, $@"({param}=)[^&]*", $"$1***", RegexOptions.IgnoreCase);
        }
        return url;
    }
}
```

---

## 10. 诊断集成

### 10.1 Diagnostics 页面日志查看器

日志查看器需要支持：

| 功能 | 实现方式 |
|------|---------|
| 实时日志流 | FileSystemWatcher 监听 + 尾读 |
| 级别筛选 | 按 Level 字段过滤 |
| 模块筛选 | 按 Module 字段过滤 |
| CorrelationId 追踪 | 输入 ID 查看完整操作链路 |
| 关键字搜索 | 全文搜索 Message 字段 |
| 时间范围筛选 | 按 Timestamp 过滤 |
| 导出 | 导出选中的日志条目 |

### 10.2 诊断报告生成

当用户提交问题时，自动生成诊断报告：

```csharp
/// <summary>
/// 生成诊断报告，包含最近的错误日志和系统信息
/// </summary>
public async Task<DiagnosticReport> GenerateReportAsync()
{
    return new DiagnosticReport
    {
        AppVersion = _appConfig.Version,
        OsVersion = Environment.OSVersion.ToString(),
        DotNetVersion = Environment.Version.ToString(),
        RecentErrors = await _logReader.GetRecentErrorsAsync(count: 50),
        ActiveDownloads = await _downloadReadService.GetActiveTasksAsync(),
        DiskSpace = GetDiskSpaceInfo(),
        MemoryUsage = Process.GetCurrentProcess().WorkingSet64,
        Uptime = DateTime.UtcNow - _startTime,
        GeneratedAt = DateTime.UtcNow
    };
}
```

---

## 11. AI 日志分析支持

### 11.1 日志格式设计目标

日志格式专门为 AI 可解析而设计：

1. **统一的 JSON 格式** — AI 可直接 parse
2. **固定的字段集** — AI 不需要猜字段名
3. **CorrelationId 串联** — AI 搜索一个 ID 就能看到完整上下文
4. **错误码前缀分类** — AI 通过错误码前缀判断模块（DL_ / AUTH_ / INST_ 等）

### 11.2 AI 排查日志时的典型查询

```
# 查找某次下载的完整链路
grep "CorrelationId\":\"a1b2c3d4" download-20241215.log | jq .

# 查找下载失败
grep "\"Level\":\"Error\"" download-20241215.log | jq -r '.ErrorCode + ": " + .Message'

# 查找特定错误码
grep "DL_003" app-20241215.log | jq .

# 查找最近 1 小时的 Warning+
jq 'select(.Timestamp > "2024-12-15T07:30:00")' app-20241215.log | jq 'select(.Level == "Warning" or .Level == "Error")'

# 查找超时操作（Duration > 10000ms）
jq 'select(.Duration > 10000)' app-20241215.log
```

### 11.3 错误上下文自动采集

当 Error 日志产生时，自动采集前后 N 条相关日志写入上下文：

```csharp
/// <summary>
/// Error 事件发生时，收集关联上下文写入错误日志
/// </summary>
internal sealed class ErrorContextEnricher : ILogEventEnricher
{
    // 环形缓冲区保存最近 100 条日志
    private readonly ConcurrentQueue<LogEvent> _recentEvents = new();
    private const int BufferSize = 100;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        // 缓存所有事件
        _recentEvents.Enqueue(logEvent);
        while (_recentEvents.Count > BufferSize)
            _recentEvents.TryDequeue(out _);

        // 如果是 Error 级别，附加最近的相关事件作为上下文
        if (logEvent.Level >= LogEventLevel.Error)
        {
            var correlationId = GetCorrelationId(logEvent);
            if (correlationId != null)
            {
                var relatedEvents = _recentEvents
                    .Where(e => GetCorrelationId(e) == correlationId)
                    .Select(e => e.RenderMessage())
                    .ToArray();

                logEvent.AddPropertyIfAbsent(
                    factory.CreateProperty("ContextTrail", relatedEvents));
            }
        }
    }
}
```

---

## 12. 性能监控日志

### 12.1 操作计时器

```csharp
namespace Launcher.Shared.Logging;

/// <summary>
/// 自动计时并记录操作耗时。用 using 块包裹操作即可。
/// </summary>
public sealed class OperationTimer : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly Stopwatch _sw;
    private readonly LogEventLevel _level;

    public OperationTimer(ILogger logger, string operation, 
        LogEventLevel level = LogEventLevel.Information)
    {
        _logger = logger;
        _operation = operation;
        _level = level;
        _sw = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _sw.Stop();
        _logger.Write(_level, "{Operation} 完成 | 耗时 {Duration}ms",
            _operation, _sw.ElapsedMilliseconds);

        // 超过阈值自动升级为 Warning
        if (_sw.ElapsedMilliseconds > 5000)
        {
            _logger.Warning("{Operation} 耗时过长 {Duration}ms，可能需要优化",
                _operation, _sw.ElapsedMilliseconds);
        }
    }
}

// 使用方式
using (new OperationTimer(_logger, "SearchFabAssets"))
{
    var assets = await _fabCatalogService.SearchAsync(query, ct);
}
```

### 12.2 启动性能日志

```csharp
// 在 10-StartupPipeline 的每个阶段记录耗时
_logger.Information("启动阶段 {Phase} 完成 | 耗时 {Duration}ms | 累计 {Total}ms",
    "Phase0_Window", phase0Ms, totalMs);
_logger.Information("启动阶段 {Phase} 完成 | 耗时 {Duration}ms | 累计 {Total}ms",
    "Phase1_DI", phase1Ms, totalMs);
```

---

## 13. 检查清单

每个模块开发完成时，对照此清单检查日志覆盖率：

| 检查项 | 要求 |
|--------|------|
| 操作入口有 Information 日志？ | 是 |
| 操作成功有 Information + Duration？ | 是 |
| 操作失败有 Error + ErrorCode？ | 是 |
| 重试有 Warning + RetryCount？ | 是 |
| 状态变更有 Information？ | 是 |
| 高频路径有节流/采样？ | 是 |
| 敏感信息已脱敏？ | 是 |
| CorrelationId 贯穿全链路？ | 是 |
| 异常有 Exception 对象传入？ | 是 |
| 日志消息用结构化模板？ | 是 |
