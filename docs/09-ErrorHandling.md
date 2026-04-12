# 错误处理

> 本文档定义统一的错误模型、错误分级策略以及 UI 如何对不同错误做出反应。

---

## 1. 核心原则

1. **禁止到处 try/catch 然后 MessageBox**
2. 错误通过 `Result<T>` 在方法返回值中传递，不通过异常控制流
3. 只在系统边界（HTTP、IO、外部进程）catch 异常，转换为 `Result`
4. UI 层根据 Error 的结构化信息决定展示方式

---

## 2. 统一 Result 模型

```csharp
namespace Launcher.Shared;

/// <summary>
/// 统一操作结果。所有可能失败的操作都返回 Result。
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Ok() => new(true, null);
    public static Result Fail(Error error) => new(false, error);
    public static Result Fail(string code, string userMessage)
        => new(false, new Error { Code = code, UserMessage = userMessage });

    public static Result<T> Ok<T>(T value) => new(value, true, null);
    public static Result<T> Fail<T>(Error error) => new(default, false, error);
}

/// <summary>
/// 带值的操作结果。
/// </summary>
public sealed class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T? value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        Value = value;
    }
}
```

---

## 3. 统一 Error 模型

```csharp
namespace Launcher.Shared;

/// <summary>
/// 结构化错误信息。
/// </summary>
public sealed class Error
{
    /// <summary>机器可读的错误代码（用于匹配和日志）</summary>
    public string Code { get; init; } = default!;

    /// <summary>面向用户的友好消息</summary>
    public string UserMessage { get; init; } = default!;

    /// <summary>技术细节（仅日志/诊断用，不展示给用户）</summary>
    public string? TechnicalMessage { get; init; }

    /// <summary>是否可以重试</summary>
    public bool CanRetry { get; init; }

    /// <summary>错误严重程度</summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;

    /// <summary>关联的内部异常（仅诊断用）</summary>
    public Exception? InnerException { get; init; }
}

/// <summary>
/// 错误严重程度。
/// </summary>
public enum ErrorSeverity
{
    /// <summary>信息性警告，不影响功能</summary>
    Warning,

    /// <summary>操作失败，但系统正常</summary>
    Error,

    /// <summary>严重错误，可能影响系统稳定性</summary>
    Critical,

    /// <summary>致命错误，需要重启</summary>
    Fatal
}
```

---

## 4. 错误代码体系

按模块和类型划分错误代码前缀：

| 前缀 | 模块 | 示例 |
|------|------|------|
| `AUTH_` | 认证 | `AUTH_LOGIN_FAILED`, `AUTH_TOKEN_EXPIRED`, `AUTH_REFRESH_FAILED` |
| `DL_` | 下载 | `DL_QUEUE_FULL`, `DL_CHUNK_FAILED`, `DL_DISK_FULL`, `DL_CDN_UNREACHABLE` |
| `INST_` | 安装 | `INST_VERIFY_FAILED`, `INST_DISK_FULL`, `INST_PERMISSION_DENIED` |
| `FAB_` | Fab 资产 | `FAB_API_ERROR`, `FAB_ASSET_NOT_FOUND`, `FAB_RATE_LIMITED` |
| `NET_` | 网络 | `NET_OFFLINE`, `NET_TIMEOUT`, `NET_DNS_FAILED` |
| `IO_` | 文件系统 | `IO_FILE_NOT_FOUND`, `IO_PERMISSION_DENIED`, `IO_DISK_FULL` |
| `CFG_` | 配置 | `CFG_INVALID_PATH`, `CFG_PARSE_ERROR` |
| `SYS_` | 系统 | `SYS_UNEXPECTED`, `SYS_OUT_OF_MEMORY` |

---

## 5. 错误分级处理

### 5.1 按严重程度的 UI 反应

| 严重程度 | UI 反应 | 说明 |
|---------|---------|------|
| `Warning` | Toast 提示 | 短暂显示，不打断操作。例："网络不稳定，已切换 CDN 节点" |
| `Error` | Toast 或内联错误提示 | 操作失败但可重试。例："下载失败，点击重试" |
| `Critical` | 弹窗对话框 | 需要用户确认或操作。例："磁盘空间不足，请清理后重试" |
| `Fatal` | 弹窗 + 建议重启 | 不可恢复。例："数据库损坏，请重启应用" |

### 5.2 CanRetry 的处理

```csharp
if (result.Error is { CanRetry: true })
{
    // UI 显示"重试"按钮
    // 用户点击后重新执行操作
}
else
{
    // UI 显示错误消息 + "知道了"按钮
    // 或写入诊断中心
}
```

---

## 6. 各层的错误处理职责

### 6.1 Infrastructure 层

**职责**：捕获底层异常，转换为 `Result` + `Error`

```csharp
// ✅ 正确：底层异常转换为 Result
public async Task<Result<string>> ComputeSha1Async(string filePath, CancellationToken ct)
{
    try
    {
        // 计算哈希...
        return Result.Ok(hash);
    }
    catch (IOException ex)
    {
        return Result.Fail<string>(new Error
        {
            Code = "IO_HASH_FAILED",
            UserMessage = "文件校验失败，文件可能正在被占用",
            TechnicalMessage = ex.Message,
            CanRetry = true,
            Severity = ErrorSeverity.Error,
            InnerException = ex
        });
    }
}
```

### 6.2 Application 层

**职责**：根据业务规则判断错误类型，补充业务上下文

```csharp
public async Task<Result> StartDownloadAsync(StartDownloadRequest request, CancellationToken ct)
{
    // 前置业务校验
    var space = await _fileSystem.GetAvailableSpaceAsync(request.DestinationPath);
    if (space < request.TotalBytes * 1.2)
    {
        return Result.Fail(new Error
        {
            Code = "DL_DISK_FULL",
            UserMessage = $"磁盘空间不足。需要 {FormatSize(request.TotalBytes)}，当前可用 {FormatSize(space)}",
            CanRetry = false,
            Severity = ErrorSeverity.Critical
        });
    }

    // 调用下层，错误直接向上传递
    var result = await _orchestrator.EnqueueAsync(request, ct);
    return result;
}
```

### 6.3 Presentation 层（ViewModel）

**职责**：根据 Error 结构决定 UI 展示方式

```csharp
[RelayCommand]
private async Task StartDownload()
{
    IsLoading = true;
    var result = await _downloadService.StartAsync(_request, _ct);
    IsLoading = false;

    if (result.IsSuccess)
    {
        _notification.ShowSuccess("下载任务已创建");
        return;
    }

    // 根据错误分级处理
    switch (result.Error!.Severity)
    {
        case ErrorSeverity.Warning:
            _notification.ShowWarning(result.Error.UserMessage);
            break;

        case ErrorSeverity.Error:
            if (result.Error.CanRetry)
                _notification.ShowError($"{result.Error.UserMessage}（点击重试）");
            else
                _notification.ShowError(result.Error.UserMessage);
            break;

        case ErrorSeverity.Critical:
        case ErrorSeverity.Fatal:
            await _dialog.ShowErrorAsync("操作失败", result.Error.UserMessage, result.Error.CanRetry);
            break;
    }
}
```

### 6.4 Domain 层

**职责**：返回业务规则违反的 Result，不捕获异常（Domain 不和外部 IO 打交道）

```csharp
public Result TransitionTo(DownloadState target)
{
    if (!_transitions.TryGetValue(Current, out var allowed) || !allowed.Contains(target))
    {
        return Result.Fail(new Error
        {
            Code = "DL_INVALID_TRANSITION",
            UserMessage = "操作目前不可用",
            TechnicalMessage = $"非法状态转换: {Current} → {target}",
            Severity = ErrorSeverity.Error
        });
    }

    Current = target;
    return Result.Ok();
}
```

---

## 7. 全局异常兜底

虽然业务层走 `Result` 模式，但仍需全局异常处理作为最后防线：

```csharp
// App.xaml.cs
private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
{
    _logger.Fatal(e.Exception, "未处理的异常");

    // 尝试保存正在进行的下载 checkpoint
    TrySaveDownloadCheckpoints();

    // 显示崩溃对话框
    ShowCrashDialog(e.Exception);

    e.Handled = true; // 尝试恢复，如果不可恢复则退出
}
```

---

## 8. 日志记录策略

| 错误严重程度 | 日志级别 | 记录内容 |
|-------------|---------|---------|
| Warning | `Warning` | UserMessage + Code |
| Error | `Error` | UserMessage + TechnicalMessage + Code |
| Critical | `Error` | 全部字段 + StackTrace |
| Fatal | `Fatal` | 全部字段 + StackTrace + 系统状态快照 |

```csharp
// 记录模式
_logger.Error("下载失败 [{ErrorCode}]: {UserMessage}. 技术详情: {TechnicalMessage}",
    error.Code, error.UserMessage, error.TechnicalMessage);
```

---

## 9. 常见错误场景速查

| 场景 | 错误代码 | 严重程度 | 可重试 | UI 反应 |
|------|---------|---------|--------|---------|
| 网络断开 | `NET_OFFLINE` | Warning | 是 | Toast + 自动重连后重试 |
| 下载 chunk 超时 | `DL_CHUNK_TIMEOUT` | Error | 是 | 自动重试，耗尽后 Toast |
| 磁盘空间不足 | `IO_DISK_FULL` | Critical | 否 | 弹窗提示 |
| Token 过期 | `AUTH_TOKEN_EXPIRED` | Warning | 是 | 自动刷新Token |
| Refresh Token 失效 | `AUTH_REFRESH_FAILED` | Critical | 否 | 弹窗提示重新登录 |
| Fab API 限流 | `FAB_RATE_LIMITED` | Warning | 是 | Toast + 自动延迟重试 |
| 文件权限不足 | `IO_PERMISSION_DENIED` | Critical | 否 | 弹窗提示检查权限 |
| 哈希校验失败 | `INST_VERIFY_FAILED` | Error | 是 | 提示是否修复 |
| 数据库损坏 | `SYS_DB_CORRUPTED` | Fatal | 否 | 弹窗 + 建议重启/重建 |
