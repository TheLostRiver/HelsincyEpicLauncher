# Task Plan: Architecture Optimization Implementation

## Goal
Continue the architecture optimization implementation in small, verified, recoverable tasks, while keeping `SessionContextRecord.md` as the primary recovery anchor.

## Current Phase
Branch finish: Option 2 published as draft PR

## Phases

### Phase 5: Options Data Driven
- [x] Task 5.1: Add `DownloadOptions`
- [x] Task 5.2: Add API endpoint Options
- [x] Task 5.3: Clarify OAuth configuration security semantics
- **Status:** complete

### Phase 6: Large-Class Split
- [x] Task 6.1: Extract owned-record loading from `EpicOwnedFabCatalogClient`
- [x] Task 6.2: Extract Fab summary mapping
- [x] Task 6.3: Extract Epic exchange-code login dialog service
- **Status:** complete

### Phase 7: Final Consistency Closure
- [x] Task 7.1: Update architecture documents to match completed code reality
- [x] Task 7.2: Run full verification and record results
- **Status:** complete

### Branch Finish
- [x] Option 2: Push `codex/architecture-optimization-implementation` to `origin`
- [x] Option 2: Create draft Pull Request targeting `main`
- [x] Option 2: Commit and push publishing context records
- **PR:** https://github.com/TheLostRiver/HelsincyEpicLauncher/pull/1

## Key Questions
1. Does each architecture document describe only completed code reality?
2. Are remaining compatibility fields clearly marked as compatibility debt rather than target architecture?
3. Is the next task limited to full verification and result recording?

## Decisions Made
| Decision | Rationale |
|----------|-----------|
| Keep `docs/SessionContextRecord.md` as the authoritative recovery record | User-defined iron rule and existing implementation workflow depend on it. |
| Use root `task_plan.md`, `findings.md`, and `progress.md` for planning-with-files state | User invoked the planning-with-files skill and the skill requires project-root planning files. |
| Start from Task 6.1 only | Implementation document says one task at a time, and SessionContextRecord identifies Task 6.1 as the next task. |
| Stop Task 6.1 at owned-record extraction | Task 6.2 owns mapping extraction, so mapping remains in `EpicOwnedFabCatalogClient` for now. |
| Keep Task 6.2 limited to pure mapping | `MapToDetailAsync` still performs async preview metadata resolution, so only pure helpers moved into `EpicFabSummaryMapper`. |
| Keep ordinary dialogs and Epic login dialogs behind separate interfaces | `IDialogService` now owns ContentDialog-style UI, while `IEpicExchangeCodeLoginDialogService` owns the WebView2 exchange-code login window. |
| Mark Task 7.1 complete after docs-only build verification | The six target architecture/module docs now describe completed code reality, and `dotnet build .\HelsincyEpicLauncher.slnx --no-restore` passed with 0 warnings and 0 errors. |
| Fix Task 7.2 failure in the stale unit test setup | `AddBackground()` correctly registers all workers; the failing test needed substitutes for the non-token workers before resolving `IBackgroundWorker` instances. |
| Create PR as draft | The publish workflow defaults to draft PR unless the user explicitly requests ready-for-review. |

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| Home skill path did not contain `session-catchup.py` | 1 | Used the project-installed skill path at `Q:\MyEpicLauncher\.codex\skills\planning-with-files`. |
| CA1859 warnings after extraction | 1 | Tightened private helper parameter types to concrete collection types already used at call sites. |
| Task 7.1 stale Fab names remained in docs | 1 | Re-scanned the target docs and replaced old `FabCatalogService` / `IFabAssetRepository` / `SqliteFabAssetRepository` examples with current Fab ports and services. |
| Task 7.2 full unit test failed | 1 | Recorded failure and stopped; `AutoInstallWorker` cannot resolve `IDownloadRuntimeStore` when the test resolves registered `IBackgroundWorker` instances. |
| GitHub CLI unavailable | 1 | Used the GitHub connector to create the draft PR after `git push` succeeded. |

## Notes
- Do not touch `Q:\MyEpicLauncher` main workspace except reading the requested skill file.
- Do not delete files unless the user explicitly requests it.
- Before code changes, read the relevant module documentation and target tests.
- Branch finishing option selected by user: Option 2, push `codex/architecture-optimization-implementation` and create a Pull Request targeting `main`.
- Option 2 result: branch pushed successfully and Draft PR #1 created at https://github.com/TheLostRiver/HelsincyEpicLauncher/pull/1.
- Fresh validation before PR creation: solution build succeeded, unit tests 308 passed, integration tests 7 passed.
