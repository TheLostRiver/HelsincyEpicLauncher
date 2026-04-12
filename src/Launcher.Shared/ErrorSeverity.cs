// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Shared;

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
