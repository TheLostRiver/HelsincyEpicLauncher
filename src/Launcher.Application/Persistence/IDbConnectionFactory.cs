// Copyright (c) Helsincy. All rights reserved.

using System.Data;

namespace Launcher.Application.Persistence;

/// <summary>
/// 数据库连接工厂接口。Infrastructure 层提供具体实现。
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// 创建并打开一个数据库连接。调用方负责 Dispose。
    /// </summary>
    Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default);
}
