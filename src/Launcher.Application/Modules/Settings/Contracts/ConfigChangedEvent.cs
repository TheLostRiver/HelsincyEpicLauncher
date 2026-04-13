// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.Settings.Contracts;

/// <summary>
/// 配置变更事件。当任何配置节被修改后发布。
/// </summary>
/// <param name="Section">变更的配置节名称（Download / Appearance / Paths / Network）</param>
/// <param name="NewConfig">新的配置对象</param>
public sealed record ConfigChangedEvent(string Section, object NewConfig);
