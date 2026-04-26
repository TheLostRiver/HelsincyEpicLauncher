// Copyright (c) Helsincy. All rights reserved.

using Launcher.Application.Modules.Auth.Contracts;
using Launcher.Application.Modules.FabLibrary.Contracts;
using Launcher.Application.Modules.Settings.Contracts;
using Serilog;

namespace Launcher.Presentation.Modules.FabLibrary;

internal sealed class FabLibraryWarmupCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<FabLibraryWarmupCoordinator>();
    private const int WarmupPageSize = 20;

    private readonly ISettingsReadService _settingsReadService;
    private readonly IFabCatalogReadService _catalogReadService;
    private readonly IFabLibrarySessionStateStore _sessionStateStore;
    private readonly IAuthService _authService;

    public FabLibraryWarmupCoordinator(
        ISettingsReadService settingsReadService,
        IFabCatalogReadService catalogReadService,
        IFabLibrarySessionStateStore sessionStateStore,
        IAuthService authService)
    {
        _settingsReadService = settingsReadService;
        _catalogReadService = catalogReadService;
        _sessionStateStore = sessionStateStore;
        _authService = authService;
    }

    public async Task WarmAsync(CancellationToken ct)
    {
        var config = _settingsReadService.GetFabLibraryConfig();
        if (!config.AutoWarmOnStartup || !_authService.IsAuthenticated)
        {
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
            Logger.Information("Fab 启动预热未写入快照 | Error={Error}", result.Error?.TechnicalMessage);
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
            AccountScopeKey = _authService.CurrentUser?.AccountId ?? string.Empty,
            AssetSummaries = pagedResult.Items,
        };

        _sessionStateStore.Save(snapshot);
        Logger.Information(
            "Fab 启动预热已写入会话快照 | Count={Count} TotalCount={TotalCount}",
            snapshot.AssetSummaries.Count,
            snapshot.TotalCount);
    }
}