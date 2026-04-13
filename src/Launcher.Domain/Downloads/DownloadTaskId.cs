// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Domain.Downloads;

/// <summary>
/// 下载任务强类型标识
/// </summary>
public readonly record struct DownloadTaskId(Guid Value)
{
    public static DownloadTaskId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
