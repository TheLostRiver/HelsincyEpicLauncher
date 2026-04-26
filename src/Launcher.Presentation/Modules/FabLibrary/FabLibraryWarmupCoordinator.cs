// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Application.Modules.Network.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Serilog;

namespace Launcher.Presentation.Modules.FabLibrary;

public sealed class FabLibraryWarmupCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<FabLibraryWarmupCoordinator>();
    private const int WarmupPageSize = 20;

    private readonly ISettingsReadService _settingsReadService;
    private readonly IFabCatalogReadService _catalogReadService;
    private readonly IFabLibrarySessionStateStore _sessionStateStore;
    private readonly IAuthService _authService;
    private readonly INetworkMonitor _networkMonitor;

    public FabLibraryWarmupCoordinator(
        ISettingsReadService settingsReadService,
        IFabCatalogReadService catalogReadService,
        IFabLibrarySessionStateStore sessionStateStore,
        IAuthService authService,
        INetworkMonitor networkMonitor)
    {
        _settingsReadService = settingsReadService;
        _catalogReadService = catalogReadService;
        _sessionStateStore = sessionStateStore;
        _authService = authService;
        _networkMonitor = networkMonitor;
    }

    public async Task WarmAsync(CancellationToken ct)
    {
        if (TryGetSkipReason(out var skipReason, out var accountScopeKey))
        {
            Logger.Information("Fab 启动预热已跳过 | Reason={Reason}", skipReason);
            return;
        }

        var query = new FabSearchQuery
        {
            SortOrder = FabSortOrder.Relevance,
            Page = 1,
            PageSize = WarmupPageSize,
        };

        var result = await _catalogReadService.SearchAsync(query, ct);
        if (!result.IsSuccess)
        {
            Logger.Information(
                "Fab 启动预热未写入快照 | Outcome=search_failed ErrorCode={ErrorCode} Error={Error}",
                result.Error?.Code,
                result.Error?.TechnicalMessage);
            return;
        }

        var pagedResult = result.Value!;
        var snapshot = new FabLibrarySessionSnapshot
        {
            Keyword = string.Empty,
            Category = string.Empty,
            SortOrder = query.SortOrder,
            CurrentPage = pagedResult.Page,
            TotalPages = pagedResult.TotalPages,
            HasNextPage = pagedResult.HasNextPage,
            TotalCount = pagedResult.TotalCount,
            VerticalOffset = 0,
            SnapshotAtUtc = DateTime.UtcNow,
            AccountScopeKey = accountScopeKey,
            AssetSummaries = pagedResult.Items,
        };

        _sessionStateStore.Save(snapshot);
        Logger.Information(
            "Fab 启动预热已写入会话快照 | Count={Count} TotalCount={TotalCount}",
            snapshot.AssetSummaries.Count,
            snapshot.TotalCount);
    }

    private bool TryGetSkipReason(out string? reason, out string accountScopeKey)
    {
        var config = _settingsReadService.GetFabLibraryConfig();
        if (!config.AutoWarmOnStartup)
        {
            reason = "disabled";
            accountScopeKey = string.Empty;
            return true;
        }

        accountScopeKey = _authService.CurrentUser?.AccountId ?? string.Empty;
        if (!_authService.IsAuthenticated || string.IsNullOrWhiteSpace(accountScopeKey))
        {
            reason = "unauthenticated";
            return true;
        }

        if (!_networkMonitor.IsNetworkAvailable)
        {
            reason = "offline";
            return true;
        }

        if (HasFreshSnapshot(accountScopeKey))
        {
            reason = "fresh_snapshot";
            return true;
        }

        reason = null;
        return false;
    }

    private bool HasFreshSnapshot(string accountScopeKey)
    {
        if (!_sessionStateStore.TryGet(out var snapshot) || snapshot is null)
        {
            return false;
        }

        if (!string.Equals(snapshot.AccountScopeKey, accountScopeKey, StringComparison.Ordinal))
        {
            return false;
        }

        return FabLibrarySnapshotAgePolicy.Classify(snapshot) == FabLibrarySnapshotAgeCategory.Fresh;
    }
}