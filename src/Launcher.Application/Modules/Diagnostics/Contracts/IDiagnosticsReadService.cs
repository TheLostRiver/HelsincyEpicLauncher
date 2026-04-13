// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Diagnostics.Contracts;

/// <summary>
/// 诊断只读查询服务。提供系统信息和日志查询功能。
/// </summary>
public interface IDiagnosticsReadService
{
    /// <summary>获取系统诊断摘要</summary>
    Task<SystemDiagnosticsSummary> GetSystemSummaryAsync(CancellationToken ct = default);
}
