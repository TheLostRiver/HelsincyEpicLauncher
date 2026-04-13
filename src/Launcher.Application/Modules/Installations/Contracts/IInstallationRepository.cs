// Copyright (c) Helsincy. All rights reserved.

using Launcher.Domain.Installations;

namespace Launcher.Application.Modules.Installations.Contracts;

/// <summary>
/// 安装记录持久化仓储接口。
/// </summary>
public interface IInstallationRepository
{
    Task<Installation?> GetByIdAsync(string id, CancellationToken ct);
    Task<Installation?> GetByAssetIdAsync(string assetId, CancellationToken ct);
    Task<IReadOnlyList<Installation>> GetAllAsync(CancellationToken ct);
    Task InsertAsync(Installation entity, CancellationToken ct);
    Task UpdateAsync(Installation entity, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);

    /// <summary>保存安装清单 JSON 文件</summary>
    Task SaveManifestAsync(string assetId, InstallManifest manifest, CancellationToken ct);

    /// <summary>读取安装清单</summary>
    Task<InstallManifest?> GetManifestAsync(string assetId, CancellationToken ct);

    /// <summary>删除安装清单</summary>
    Task DeleteManifestAsync(string assetId, CancellationToken ct);
}
