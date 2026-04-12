// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Shared;

/// <summary>
/// 结构化错误信息。所有失败的操作都通过此模型描述错误。
/// </summary>
public sealed class Error
{
    /// <summary>机器可读的错误代码（用于匹配和日志），如 DL_CHUNK_FAILED</summary>
    public required string Code { get; init; }

    /// <summary>面向用户的友好消息</summary>
    public required string UserMessage { get; init; }

    /// <summary>技术细节（仅日志/诊断用，不展示给用户）</summary>
    public string? TechnicalMessage { get; init; }

    /// <summary>是否可以重试</summary>
    public bool CanRetry { get; init; }

    /// <summary>错误严重程度</summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;

    /// <summary>关联的内部异常（仅诊断用）</summary>
    public Exception? InnerException { get; init; }
}
