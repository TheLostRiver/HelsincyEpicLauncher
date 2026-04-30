# Findings & Decisions

## Requirements
- Continue the existing architecture optimization implementation according to `docs/18-ArchitectureOptimizationImplementation.md`.
- Use the project-installed `planning-with-files` skill.
- Preserve the iron rule: read and update `docs/SessionContextRecord.md` around atomic tasks and before risky context transitions.
- Do not delete files.
- Keep work in the isolated worktree `C:\tmp\superpowers\worktrees\MyEpicLauncher\architecture-optimization-implementation`.

## Research Findings
- `git status --short` is clean at the start of this session.
- `git log --oneline -5` shows HEAD is `1093e58 docs: 记录 Task 5.3 完成上下文`.
- `docs/18-ArchitectureOptimizationImplementation.md` identifies Task 6.1 as the next task: extract owned-record loading from `EpicOwnedFabCatalogClient`.
- Task 6.1 scope is limited to owned records retrieval, paging, cursor, and cache behavior; summary mapping must not be moved in this task.
- The first attempted `session-catchup.py` path under `%USERPROFILE%\.codex\skills` was missing; the project skill path contains the script.
- `EpicOwnedFabCatalogClient.cs` is 1298 lines; owned-record methods currently occupy roughly lines 176-817, while `LoadSummariesAsync`, `GetCatalogItemAsync`, and `MapToSummary`/`MapToDetailAsync` begin after that and must remain in the original class for Task 6.1.
- Existing `EpicOwnedFabCatalogClientTests` cover progressive owned-record window expansion, detail lookup in preview, format/detail mapping, preview screenshot fallback, and preview trace metadata.
- `OwnedRecord`, `OwnedRecordWindow`, `OwnedRecordSnapshot`, `OwnedRecordLoadState`, `OwnedRecordRequirement`, `LibraryResponse`, and `LibraryRecord` are currently nested private types in `EpicOwnedFabCatalogClient`.
- After Task 6.1, `OwnedRecord`, `OwnedRecordWindow`, `OwnedRecordSnapshot`, and `OwnedRecordRequirement` are internal namespace-level records in `EpicOwnedRecordsClient.cs` so both the records client and catalog client can use them.
- `EpicOwnedFabCatalogClient` no longer contains curl, preview stream parsing, cursor pagination, or records cache fields; it delegates these to `EpicOwnedRecordsClient`.
- `MapToSummary`, `MapToDetailAsync`, thumbnail/category/format/listing extraction, catalog item loading, and catalog cache remain in `EpicOwnedFabCatalogClient`.
- Task 6.2 mapping boundary: move `MapToSummary`, thumbnail selection, listing identifier extraction, category normalization, screenshot URL extraction, and format extraction into `EpicFabSummaryMapper`.
- `MapToDetailAsync` should remain in `EpicOwnedFabCatalogClient` because it invokes `IFabPreviewMetadataResolver`; it may call pure mapper helpers after extraction.
- `EpicCatalogItem` and related catalog response DTOs need to become namespace-level internal types so both catalog loading and the mapper can share them.
- Task 6.2 completed: `EpicFabSummaryMapper` now owns `MapToSummary`, screenshot URL extraction, listing identifier extraction, category normalization, thumbnail selection, and format extraction.
- `EpicOwnedFabCatalogClient` now delegates summary/detail pure mapping helpers to `EpicFabSummaryMapper`; catalog loading, catalog cache, and preview metadata enrichment remain in the catalog client.
- A side-effect scan of `EpicFabSummaryMapper.cs` for `HttpClient`, send calls, file/directory APIs, process start APIs, and curl found no matches.
- Task 6.3 boundary: `DialogService` currently mixes ordinary `ContentDialog` methods with the WebView2 exchange-code login window.
- `ShellViewModel` currently calls `_dialogService.ShowEpicExchangeCodeLoginAsync(...)`; Task 6.3 should move that call behind a dedicated login-dialog interface.
- `ShellPage` currently injects the concrete `DialogService` only to call `SetXamlRoot(this.XamlRoot)` after `Loaded`; the extracted Epic login dialog service also needs a XamlRoot setter because it creates the login window from Presentation.
- `MainWindow` currently resolves the concrete `DialogService` to construct `ShellPage`; after extraction it should resolve both concrete dialog services or otherwise pass both XamlRoot-aware shell services into ShellPage.
- Task 6.3 completed: `IDialogService` no longer exposes `ShowEpicExchangeCodeLoginAsync`; `IEpicExchangeCodeLoginDialogService` exposes the dedicated Epic exchange-code login capability.
- `DialogService` now only contains ordinary confirm/info/error/text input/custom dialog responsibilities.
- `EpicExchangeCodeLoginDialogService` owns WebView2 setup, window sizing, exchange-code message handling, external Epic link launch, cancellation, and temporary WebView2 user-data cleanup.
- `ShellViewModel` now depends on both `IDialogService` and `IEpicExchangeCodeLoginDialogService`, so auth login no longer expands the ordinary dialog contract.
- Task 7.1 start: worktree is clean at `6065a17 docs: 记录 Task 6.3 完成上下文`.
- `Launcher.Presentation.csproj` now references only `Launcher.Application` and `Launcher.Shared`, not `Launcher.Domain`.
- Downloads completed realities: `DownloadCommandService` lives in Application, `StartDownloadUseCase` exists, `DownloadOrchestrator` subscribes `IDownloadScheduler.TaskReady` to `IDownloadTaskExecutor.ExecuteAsync`, and `DownloadWorker` exists in Infrastructure.
- Contracts completed realities: `DownloadTaskKey`, `DownloadStatusKind`, and `InstallStatusKind` are Contract-owned projections; legacy Domain fields remain for compatibility.
- Options completed realities: `DownloadOptions`/`IDownloadOptionsProvider`, `EpicApiOptions`, `FabApiOptions`, `UpdateOptions`, OAuth env/local override, and configured API base addresses exist.
- Background completed realities: `IBackgroundWorker`, `WorkerStatus`, `IBackgroundTaskHost`, and `BackgroundTaskHost` exist; TokenRefresh, AutoInstall, AppUpdate, NetworkMonitor, and Fab warmup are registered/started through the unified host.
- Fab completed realities: owned-record retrieval is in `EpicOwnedRecordsClient`; pure summary/category/image/format/listing mapping is in `EpicFabSummaryMapper`.
- Task 7.1 updated the six target docs to reflect completed code reality only: solution structure, dependency rules, core interfaces, Downloads, Installations, and FabLibrary.
- Task 7.1 stale-name scan found old Fab examples (`FabCatalogService`, `IFabAssetRepository`, `SqliteFabAssetRepository`) and those were corrected to current ports/services (`FabCatalogReadService`, `IFabDownloadInfoProvider`, `EpicOwnedRecordsClient`, `EpicFabSummaryMapper`).
- Task 7.1 verification passed: `dotnet build .\HelsincyEpicLauncher.slnx --no-restore` completed with 0 warnings and 0 errors.

## Technical Decisions
| Decision | Rationale |
|----------|-----------|
| Initialize planning files in the implementation worktree root | This is the active project directory for the current implementation work. |
| Treat Task 6.1 as a refactor protected by existing tests | The implementation plan names existing `EpicOwnedFabCatalogClientTests` as the verification target. |
| Keep summary/detail mapping in `EpicOwnedFabCatalogClient` for Task 6.1 | Task 6.1 explicitly says not to migrate summary mapping; Task 6.2 handles mapping extraction. |
| Use internal namespace-level owned-record records | They preserve the existing internal boundary while allowing the extracted records client and original catalog client to share the record model. |
| Keep `MapToDetailAsync` in the catalog client for Task 6.2 | It performs async preview metadata resolution, while Task 6.2 is limited to pure mapping logic. |
| Move Epic catalog DTOs to internal namespace-level types | Both `EpicOwnedFabCatalogClient` and `EpicFabSummaryMapper` need the same deserialization model without widening it outside Infrastructure. |
| Add a dedicated Epic login dialog interface | It lets `ShellViewModel` depend on an explicit login-window capability while keeping `IDialogService` focused on ordinary dialogs. |
| Keep XamlRoot setters on concrete shell services | `ShellPage` already wires UI-only concrete services after `Loaded`; using the same pattern keeps XamlRoot setup local to Shell composition. |
| Keep Task 7.1 docs descriptive, not aspirational | Implementation doc requires recording only completed code reality, so remaining debts are described as compatibility or future work rather than as already-finished architecture. |

## Issues Encountered
| Issue | Resolution |
|-------|------------|
| `SessionContextRecord.md` current-status table still describes the state before the Task 5.3 context commit | Update it before beginning Task 6.1. |
| New CA1859 warnings appeared after extraction | Changed private helper signatures to concrete collection types where call sites already use concrete collections. |
| Task 6.3 red test failed at compile time because `IEpicExchangeCodeLoginDialogService` did not exist | Added the dedicated interface/service and moved the login call site to it. |
| Task 7.1 scan found old Fab repository/service names in architecture docs | Corrected those snippets to current implementation and re-scanned the target docs. |

## Resources
- `docs/17-ArchitectureOptimizationPlan.md`
- `docs/18-ArchitectureOptimizationImplementation.md`
- `docs/SessionContextRecord.md`
- `Q:\MyEpicLauncher\.codex\skills\planning-with-files\SKILL.md`

## Visual/Browser Findings
- No visual or browser findings in this session.
