// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Persistence;

/// <summary>
/// 数据库初始化器。启动时执行迁移和初始配置。
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// 执行所有待应用的数据库迁移。
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);
}
