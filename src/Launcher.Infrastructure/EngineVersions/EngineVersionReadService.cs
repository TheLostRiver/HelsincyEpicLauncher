// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.EngineVersions.Contracts;
using Launcher.Application.Modules.Installations.Contracts;
using Launcher.Shared;
using Serilog;

namespace Launcher.Infrastructure.EngineVersions;

/// <summary>
/// 引擎版本查询服务实现。合并远程可用版本与本地已安装版本。
/// </summary>
public sealed class EngineVersionReadService : IEngineVersionReadService
{
    private static readonly ILogger Logger = Log.ForContext<EngineVersionReadService>();

    private readonly EngineVersionApiClient _apiClient;
    private readonly IInstallReadService _installReadService;

    // 缓存（5 分钟）
    private IReadOnlyList<EngineVersionSummary>? _cachedVersions;
    private DateTime _cachedAt;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public EngineVersionReadService(EngineVersionApiClient apiClient, IInstallReadService installReadService)
    {
        _apiClient = apiClient;
        _installReadService = installReadService;
    }

    public async Task<Result<IReadOnlyList<EngineVersionSummary>>> GetAvailableVersionsAsync(CancellationToken ct)
    {
        // 缓存命中
        if (_cachedVersions is not null && DateTime.UtcNow - _cachedAt < CacheDuration)
            return Result.Ok(_cachedVersions);

        var apiResult = await _apiClient.GetAvailableVersionsAsync(ct);
        if (!apiResult.IsSuccess)
            return Result.Fail<IReadOnlyList<EngineVersionSummary>>(apiResult.Error!);

        // 获取已安装列表
        var installed = await GetInstalledVersionIds(ct);

        var versions = apiResult.Value!.Versions
            .Select(dto => new EngineVersionSummary
            {
                VersionId = dto.VersionId,
                DisplayName = dto.DisplayName,
                DownloadSize = dto.DownloadSize,
                ReleaseDate = dto.ReleaseDate,
                IsInstalled = installed.ContainsKey(dto.VersionId),
                InstalledPath = installed.GetValueOrDefault(dto.VersionId),
            })
            .OrderByDescending(v => v.ReleaseDate)
            .ToList();

        _cachedVersions = versions;
        _cachedAt = DateTime.UtcNow;

        Logger.Information("引擎版本列表已加载：{Count} 个版本，{Installed} 个已安装",
            versions.Count, installed.Count);

        return Result.Ok<IReadOnlyList<EngineVersionSummary>>(versions);
    }

    public async Task<Result<IReadOnlyList<InstalledEngineSummary>>> GetInstalledVersionsAsync(CancellationToken ct)
    {
        try
        {
            var installations = await _installReadService.GetInstalledAsync(ct);

            // 过滤引擎类型的安装（AssetId 以 "UE_" 开头）
            var engineInstalls = installations
                .Where(i => i.AssetId.StartsWith("UE_", StringComparison.OrdinalIgnoreCase))
                .Select(i => new InstalledEngineSummary
                {
                    VersionId = i.AssetId,
                    DisplayName = i.AssetName,
                    InstallPath = i.InstallPath,
                    SizeOnDisk = i.SizeOnDisk,
                    InstalledAt = i.InstalledAt,
                })
                .OrderByDescending(v => v.InstalledAt)
                .ToList();

            return Result.Ok<IReadOnlyList<InstalledEngineSummary>>(engineInstalls);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "获取已安装引擎版本失败");
            return Result.Fail<IReadOnlyList<InstalledEngineSummary>>(new Error
            {
                Code = "ENGINE_SCAN_ERROR",
                UserMessage = "扫描已安装引擎版本失败",
                TechnicalMessage = ex.Message,
                CanRetry = true,
                Severity = ErrorSeverity.Warning,
            });
        }
    }

    private async Task<Dictionary<string, string>> GetInstalledVersionIds(CancellationToken ct)
    {
        var installations = await _installReadService.GetInstalledAsync(ct);
        return installations
            .Where(i => i.AssetId.StartsWith("UE_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(i => i.AssetId, i => i.InstallPath);
    }
}
