// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Settings.Contracts;

namespace Launcher.Infrastructure.Settings;

/// <summary>
/// 用户设置持久化模型。对应 user.settings.json 的完整结构。
/// </summary>
internal sealed class UserSettings
{
    public DownloadConfig Download { get; set; } = new();
    public AppearanceConfig Appearance { get; set; } = new();
    public PathConfig Paths { get; set; } = new();
    public NetworkConfig Network { get; set; } = new();
}
