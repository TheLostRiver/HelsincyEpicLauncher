// Copyright (c) Helsincy. All rights reserved.

namespace Launcher.Application.Modules.FabLibrary.Contracts;

/// <summary>
/// 按需解析 Fab 资产预览图地址。
/// </summary>
public interface IFabPreviewUrlReadService
{
    /// <summary>
    /// 尝试基于已有锚点解析缩略图地址。
    /// </summary>
    Task<string?> TryResolveThumbnailUrlAsync(string assetId, string previewListingId, string previewProductId, CancellationToken ct);
}