# Task Plan: Architecture Optimization Implementation

## Goal
Continue the architecture optimization implementation in small, verified, recoverable tasks, while keeping `SessionContextRecord.md` as the primary recovery anchor.

## Current Phase
Phase 6: Large-class split and performance convergence

## Phases

### Phase 5: Options Data Driven
- [x] Task 5.1: Add `DownloadOptions`
- [x] Task 5.2: Add API endpoint Options
- [x] Task 5.3: Clarify OAuth configuration security semantics
- **Status:** complete

### Phase 6: Large-Class Split
- [x] Task 6.1: Extract owned-record loading from `EpicOwnedFabCatalogClient`
- [ ] Task 6.2: Extract Fab summary mapping
- [ ] Task 6.3: Extract Epic exchange-code login dialog service
- **Status:** in_progress

### Phase 7: Final Consistency Closure
- [ ] Task 7.1: Update architecture documents to match completed code reality
- [ ] Task 7.2: Run full verification and record results
- **Status:** pending

## Key Questions
1. Which parts of `EpicOwnedFabCatalogClient` are strictly owned-record retrieval, paging, cursor, and cache behavior?
2. How can Task 6.1 preserve public behavior without also moving summary mapping?
3. Which tests already cover `EpicOwnedFabCatalogClient` behavior and can protect the extraction?

## Decisions Made
| Decision | Rationale |
|----------|-----------|
| Keep `docs/SessionContextRecord.md` as the authoritative recovery record | User-defined iron rule and existing implementation workflow depend on it. |
| Use root `task_plan.md`, `findings.md`, and `progress.md` for planning-with-files state | User invoked the planning-with-files skill and the skill requires project-root planning files. |
| Start from Task 6.1 only | Implementation document says one task at a time, and SessionContextRecord identifies Task 6.1 as the next task. |
| Stop Task 6.1 at owned-record extraction | Task 6.2 owns mapping extraction, so mapping remains in `EpicOwnedFabCatalogClient` for now. |

## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| Home skill path did not contain `session-catchup.py` | 1 | Used the project-installed skill path at `Q:\MyEpicLauncher\.codex\skills\planning-with-files`. |
| CA1859 warnings after extraction | 1 | Tightened private helper parameter types to concrete collection types already used at call sites. |

## Notes
- Do not touch `Q:\MyEpicLauncher` main workspace except reading the requested skill file.
- Do not delete files unless the user explicitly requests it.
- Before code changes, read the relevant module documentation and target tests.
