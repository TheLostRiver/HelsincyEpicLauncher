// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.FabLibrary.Contracts;

/// <summary>
/// 在受控浏览器上下文中读取 Fab listing 页面内容。
/// </summary>
public interface IFabListingPageReadService
{
    /// <summary>
    /// 尝试读取指定 listing 页面的 HTML 内容。
    /// </summary>
    Task<string?> TryReadListingHtmlAsync(string listingId, CancellationToken ct);
}