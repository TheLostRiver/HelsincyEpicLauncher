// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Domain.Installations;
using Serilog;

namespace Launcher.Infrastructure.Installations;

/// <summary>
/// 安装只读查询服务实现。
/// </summary>
public sealed class InstallReadService : IInstallReadService
{
    private readonly IInstallationRepository _repository;
    private readonly ILogger _logger = Log.ForContext<InstallReadService>();

    public InstallReadService(IInstallationRepository repository)
    {
        _repository = repository;
    }

    public async Task<InstallStatusSummary?> GetStatusAsync(string assetId, CancellationToken ct)
    {
        var installation = await _repository.GetByAssetIdAsync(assetId, ct);
        return installation is null ? null : MapToSummary(installation);
    }

    public async Task<IReadOnlyList<InstallStatusSummary>> GetInstalledAsync(CancellationToken ct)
    {
        var installations = await _repository.GetAllAsync(ct);
        return installations.Select(MapToSummary).ToList();
    }

    private static InstallStatusSummary MapToSummary(Installation entity)
    {
        return new InstallStatusSummary
        {
            AssetId = entity.AssetId,
            AssetName = entity.AssetName,
            InstallPath = entity.InstallPath,
            Version = entity.Version,
            SizeOnDisk = entity.SizeBytes,
            InstalledAt = entity.InstalledAt.UtcDateTime,
            State = entity.State,
            NeedsRepair = entity.State == InstallState.NeedsRepair,
        };
    }
}
