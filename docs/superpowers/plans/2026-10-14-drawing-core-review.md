# Drawing core independent review

Verdict: changes requested for two P2 issues below. No critical issue found in the default gameplay path. Review was read-only except this report; no Unity operations or tests were run.

Reviewed the task brief, implementation report, global constraints, supplied `core-review.patch.txt`, and relevant final source. The package includes the earlier uncommitted freeform baseline; findings below concern the new configurable rules/unlock work.

## Findings

### P2 — Persist initially granted drawing rights

Location: `Assets/PaperDefense/Core/WorkshopProfile.cs:87` and `:95` (related preparation at `:65-71`).

Drawing ownership currently returns `InitiallyUnlocked || IsUnlocked(id)`, and `UnlockDrawingAid` immediately succeeds for an initial definition without adding its ID to `unlocked`. `ConfigureContent` persists initial equipment rights but does not process `catalog.DrawingUnlocks`. Therefore a player who receives an initially unlocked stencil, saves, and reloads after that definition's `InitiallyUnlocked` flag changes to false loses that right and is asked to purchase it. Even explicitly calling the unlock API before saving cannot preserve it. This conflicts with permanent reusable unlocks and the existing configuration-preparation contract that initial rights are granted without removing saved progress.

Concrete trigger: create a profile with a catalog where `drawing.sniper.InitiallyUnlocked=true`; serialize it; load the data with the same stable ID set to `InitiallyUnlocked=false`. `IsDrawingUnlocked("drawing.sniper")` changes from true to false because the saved `Unlocked` array never contained the ID.

Fix: grant initially unlocked drawing IDs into the profile's persistent set during content preparation, including returning the changed flag so the coordinator records pending persistence. Retain the current no-charge/idempotent purchase behavior. Add a reload/configuration-change regression covering an initial drawing grant.

### P2 — Validate final stat arithmetic, not just the sum of coefficients

Location: `Assets/PaperDefense/Core/FreeformRules.cs:106-109`; affected resolution: `Assets/PaperDefense/Core/TowerCatalog.cs:73` and `:86`.

The new overflow check only proves that the freeform coefficient sum is finite. A finite positive `HealthBase=1e38f` (or `DamageBase=1e38f`) passes both individual validation and that sum check, but multiplying it by ordinary chassis health/weapon damage produces positive infinity. `ResolvedTowerAssembly.Validate` checks foundation health and other bases before applying freeform multipliers (`:44-58`), so a complete in-budget tower can still pass `TowerCatalog.Validate` and deploy with infinite health/damage. An analogous build multiplier can produce a build that never completes. This defeats the validated-rules boundary and the report's arithmetic-overflow guarantee.

Concrete trigger: use an otherwise normal catalog/balance, change only `FreeformRules.HealthBase` to `1e38f`, and resolve a normal valid freeform gun tower. Snapshot creation accepts the value; `TowerCatalog.Health` multiplies normal foundation health by that coefficient and returns infinity, while assembly validation has checked only the pre-geometry health.

Fix: reject non-finite final resolved values after geometry, material and module multipliers are applied, or validate safe catalog-dependent coefficient bounds at content/balance preparation. Include health, damage, range, interval and build duration rather than relying on one aggregate sum. Add a focused finite-input-overflow regression.

## Compliance and validation assessment

The implementation otherwise follows the planned split: immutable copied configuration; 65 + 35 ink allowance; geometry-based functional costs with cosmetic decoration; separate stencil/equipment catalogs; candidate-save-publish purchases; immutable stencil geometry; bounded atomic editing commands; shared assembly stat resolution; and structurally preserved historical drawings under stricter thresholds. The loader correction keeps format/future-rule protection while dropping inappropriate gameplay-completeness checks during restoration.

Validation evidence supplied by the parent: full EditMode 811/811 passed, zero failed/skipped, job `82ca6b6b22a84769a7e62f2b1806c500` (26.58 seconds), including the loader recovery regression; focused PlayMode 4/4 previously passed. These are parent-supplied results, not tests executed by this reviewer. The two findings above were established by static control/data-flow review and are not covered by the reported focused cases.

## Scoped re-review of round 1 fixes

Updated verdict: **both original P2 findings addressed**. No new critical or important defect found in `core-fix-review.patch.txt`. This verdict supersedes the initial changes-requested verdict for these two findings; broader integration validation remains with the parent.

- **Initial drawing rights — addressed.** `WorkshopProfile.ConfigureContent` now adds initial drawing IDs to the persistent unlock set and returns their changed state to the existing persistence coordinator. The explicit initial-unlock path also records the ID without charging. The two regressions cover save/reload after removing the initial flag and one-time change reporting for newly granted initial rights.
- **Resolved-stat overflow — addressed.** `TowerCatalog` now checks finite final health, damage, range, interval and build duration. `ResolvedTowerAssembly.Validate` invokes those same resolved calculations, checks all configured material tiers and exercises the production floor for interval arithmetic. Overflow becomes a rejected use validation instead of a deployable infinite stat. Preview catches the resulting configuration exception and returns an invalid result with zeroed stat fields. The combat damage guard preserves the original operation order. The five parameterized regressions cover the originally reported finite-input overflow cases.

The fixture changes reduce only unrelated body geometry to fit the new real allowance; existing muzzle, stroke-preservation, trial, ownership and persistence assertions are retained. The shared drawing helper additionally asserts a complete in-budget result. Preparing the save-failure fixture with current content keeps its manual-save failure assertions separate from newly granted-content persistence.

Fresh parent-supplied evidence: full EditMode **818/818 passed**, zero failed/skipped, job `d55b13520f194437b4172223734a374e`, including all seven new regressions. The updated report also records the initial 14 PlayMode failures and their fixture diagnoses; full PlayMode rerun is pending at this review boundary. No tests, Unity operations, implementation edits or unrelated review expansion were performed by this reviewer.
