# Drawing core implementation report

Implemented Core/Application/config authoring. No Presentation files edited, no Unity Editor operations, no commits. Existing dirty work preserved.

## Contracts available to Presentation

- `ContentCatalogInput.FreeformRules`: mutable `FreeformRulesInput`; validated immutable `ContentCatalogSnapshot.FreeformRules`. Copy/snapshot isolation includes all rule scalars.
- `ContentCatalogInput.DrawingUnlocks`: mutable list of `DrawingUnlockDefinitionInput`; `ContentCatalogSnapshot.DrawingUnlocks`: immutable `IReadOnlyList<DrawingUnlockDefinition>`; `TryGetDrawingUnlock(string, out DrawingUnlockDefinition)`.
- `DrawingUnlockDefinition`: read-only `Id`, `Name`, `Description`, `GoldCost`, `InitiallyUnlocked`, `Role`, `Strokes`. `Role` is `DrawingStencilRole.Body` or `Barrel`; `Strokes` is immutable `IReadOnlyList<DrawingStroke>`. Name/description may be semantic `UserText` messages and must use existing text resolution.
- `WorkshopReadModel.DrawingUnlocks`, `WorkshopReadModel.IsDrawingUnlocked(string id)` expose definitions/rights separately from equipment `Parts`.
- `RunController.UnlockDrawingAid(string id, out string reason)` uses candidate-copy/persist/publish flow. Unknown IDs reject; repeat ownership succeeds without charge or redundant save/event; a failed save leaves live gold/ownership unchanged.
- `FreeformTowerDesign`: existing completeness/stat properties plus `InkUsed`, `InkBudget`, `IsWithinBudget`, `IsUsable`, useful `Message`. Completeness and budget are independent; current-use validation requires both.
- `RunController.PreviewBlueprint(...)` still returns `TowerPreview`; added `FreeformDesign` (resolved under preview content) and `Weapons` (`IReadOnlyList<WeaponPreview>`). Each `WeaponPreview` exposes readonly `SlotIndex`, `Damage`, `Range`, `FireRate`, `Interval`, `FeedMultiplier`. Aggregate fields remain available. UI does not need stat formulas.

All following commands operate on the selected part binding and return `bool`, with `out string reason`:

```csharp
AddLine(string part, DrawingPoint from, DrawingPoint to, out string reason)
AddRectangle(string part, DrawingPoint from, DrawingPoint to, out string reason)
AddEllipse(string part, DrawingPoint from, DrawingPoint to, out string reason)
ReplaceFromStencil(string part, DrawingUnlockDefinition stencil, out string reason)
TranslatePart(string part, float dx, float dy, out string reason)
ScalePart(string part, float scaleX, float scaleY, out string reason)
MirrorPartHorizontal(string part, out string reason)
ErasePath(string part, IReadOnlyList<DrawingPoint> path, float radius, out string reason)
```

`ScalePart` and mirror use the part-local origin (mount pivot for a barrel). Ellipse uses 32 segments. Transforms reject out-of-bounds operations atomically; no clipping. Stamp replacement checks role and keeps all other parts. A command contributes one history step; identical snapshots/zero transforms and rejected operations contribute none. `ErasePath` detects swept intersections between samples and erases whole touched strokes in a single step. Existing `AddStroke`/`Erase` entry points retained.

## Rules, configuration and economy

Design budget is **65 + 35 per additional occupied mount**, revised by root from the suggested 100/45 so maxing every bonus actually creates a tradeoff. Functional cost is `body area * 50 + sum(barrel length * 25 + barrel width * 35)`. Stats saturate at configured geometry references; ink continues counting full dimensions. Geometry uses the existing deterministic connected-envelope measurement, so retracing/sample subdivision do not add cost. Decorations have no functional ink cost and remain subject to shared stroke/point limits.

All former stat formula coefficients and minimum valid dimensions now live in `FreeformRulesInput`; finite/sign/positive denominators, supported drawing extent and arithmetic-overflow checks apply at snapshot creation. Version-1 vector measurement and save `RuleVersion` remain unchanged.

Five reusable stencils are authored explicitly in:

- `Assets/PaperDefense/Configuration/Content/NotebookContent.asset`
- `Assets/PaperDefense/Configuration/Tests/drawing/Catalog.asset`

IDs: `drawing.body.basic` and `drawing.barrel.basic` are free/initial; `drawing.rapid` costs 24 gold, `drawing.sniper` 32, `drawing.heavy` 40. Paid contours contain actual editable tapered/faceted barrel geometry, collars, or vents; no hidden stat modifiers. Each works within allowance with the starter body. Drawing manually and shape tools remain free.

`ResolvedTowerAssembly.FreeformDesign` evaluates the same snapshot chosen by existing catalog precedence (explicit `balance.Content` remains authoritative). Range, interval, damage, health, capacity, build cost/time and combat validity all consume that design. `RunController` binds save/trial input to current content; preview binds before final validation. Saved geometry is retained independently of a reduced current ink allowance.

Drawing purchase rights use the existing persistent unlocked-ID collection, preserving version 7 and unknown-future-version protection. Separate catalog prevents equipment shop assumptions from seeing drawing IDs. No user profile is touched by tests (in-memory injected store only).

Text additions staged in `2026-10-14-drawing-core-texts.json`, merged via `python Tools/Extract-UserText.py --merge ...` into manifest, resource and generated contracts.

## Validation ledger

- RED verified by root, Unity job `c8134f2df7f44fcfab73a6d7613e8178`: complete full-canvas geometry incorrectly passed validation before implementation; expected false / actual true.
- New focused suites: `DrawingWorkshopRulesTests` (12 original tests including loader recovery, plus 7 review regression cases), `DrawingUnlockTransactionTests` (2 tests).
- Python text pipeline tests: 12 passed (`python -m unittest Tools.test_extract_user_text Tools.tests.test_extract_user_text_diagnostics`).
- `git diff --check`: no whitespace errors (existing LF/CRLF notices only).
- Unity focused EditMode: **30/30 passed**, job `40a17fee0db54669a5fc32941589e81f` (`DrawingWorkshopRulesTests` before the additional loader test + `FreeformTowerTests`).
- Unity focused PlayMode: **4/4 passed**, job `be9eeef15b5c4cd681fadef7c010f50a` (`DrawingUnlockTransactionTests` + `FreeformWorkshopTests`).
- Additional loader-recovery RED verified by root, job `138cd1ccd92f458a99a7567c8676e92a`: expected failure was `ArgumentException` / `freeform.missing_body` in `WorkshopProfile.FromStrictData`. Loader now retains structurally valid geometry without applying default gameplay completeness.
- Full pre-review EditMode: **811/811 passed**, 0 failed/skipped, job `82ca6b6b22a84769a7e62f2b1806c500`, 26.5833892 s. Includes loader GREEN.
- Full pre-review PlayMode: **307 passed / 14 failed / 321 total**, job `43e88d277f254b91922687532abcf139`. Every failure is recorded and diagnosed below; none is omitted or treated as green.
- Full Unity suites and live visual validation belong to root integration; not claimed by this agent.

Loader recovery fix completed after the initial focused suites: `WorkshopProfile.FromStrictData` still validates profile version, part structure, drawing format/bounds and future geometry rule protection, but no longer applies default gameplay completeness. A drawing authored under lower configured thresholds survives reload and remains editable; current-use validation under stricter rules rejects deploy/save until corrected. This also keeps pages recoverable after ink allowance reductions.

## Independent review round 1

Both P2 findings in `2026-10-14-drawing-core-review.md` were reproduced with regressions before fixes. Root RED job `e67cb82998314c6ba3acb74c5c509550`: **7/7 failed as expected** — initial grant absent from saved IDs; preparation did not report the new grant; finite-input health/damage/range/interval/build overflow still passed use validation.

1. `ConfigureContent` now grants `InitiallyUnlocked` drawing IDs into the persistent unlock set and includes them in its changed result. Explicit initial unlock calls likewise persist without charging. New tests prove reload after the initial flag is removed preserves ownership and that adding initial rights marks content dirty exactly once. `WorkshopSaveFailureTests` now prepares its stored profile before injection, keeping its existing assertion specifically about failed manual candidates rather than conflating it with new-content initial grants.
2. Final resolved health, damage, range, interval and build duration reject non-finite arithmetic. Validation exercises the actual resolved assembly, module operations, every material tier and the production floor; finite coefficient sums are no longer assumed sufficient. Preview converts numeric configuration exceptions into an invalid preview instead of publishing infinity or throwing through UI. Combat damage has a final finite guard while preserving the original multiplication order and nonnegative clamp. Catalog precedence and default tuning are unchanged.

### Full PlayMode failure diagnosis and fixture repairs

The shared `FreeformUiTestDrawing.Complete` previously generated a body with half-size .7 (98 ink for body alone). Its default is now .4 (about 32 ink). Existing pointer/barrel strokes are retained; the helper asserts the completed result is actually usable. Explicit .224 bodies used by overload tests remain unchanged.

- `AuthoredRenderingTests.FreeformProjectileOriginFollowsTheDrawnBarrelTip(0.75f)`: 50 body ink + 18.75 barrel ink exceeded 65; reduce the unrelated body from half-size .5 to .4. Muzzle endpoint assertions remain exact.
- `AuthoredWorkshopTests.MouseStrokesSurviveEditorAlbumReloadAndSeveralFieldCopies`: oversized shared body blocked save; file existence failed before reload. Shared fixture fixed.
- `AuthoredWorkshopTests.RealHudRestartPreservesPointerDrawingProgressAndSharesQualityWithTrialAndBattle`: oversized shared body blocked save/assignment; restart retained starter slot. Shared fixture fixed.
- `AuthoredWorkshopTests.SaveAsNewKeepsOriginalPageAndEditingOneMountKeepsOtherStrokes`: oversized shared body blocked variant save; original ID remained. Shared fixture fixed.
- `DataDrivenWorkshopUiTests.EmptyAndPartiallyAssignedStartupPagesRenderAndCanBeFilledInWorkshop(False)` and `(True)`: 112.5/65 ink blocked new album page. Shared fixture fixed.
- `DataDrivenWorkshopUiTests.RepeatedNewEquipmentCanBeDrawnIndependentlyWithoutNewDrawingEnumValues`: oversized body blocked variant save, so the prior last album page had no module stroke. Shared fixture fixed.
- `DrawingQualityHudTests.FreeformEditorPreviewAndNormalTrialUseTheSameConstructionWithoutGrading`: explicit half-size .65 body plus two .16×.6 barrels exceeded 100. Reduce only body half-size to .4; retain preview/trial assertions.
- `FocusedWorkshopSceneTests.DrawingSceneEditsThirdLayerAndFootprintWithoutAssemblyAlbumOrBattle`: explicit half-size .7 body and three barrels exceeded 135, so trial returned null. Reduce only body half-size to .4; retain third layer, footprint and recording assertions.
- `MortarFootprintHudTests.BlankNameDraftExplainsUnavailableProbeAndRecoversAfterRename`: name repair left shared oversized body invalid; probe still null. Shared fixture fixed.
- `MortarFootprintHudTests.WorkshopControlsPreserveAuthoredArtThroughVariantAndTemporaryStore`: 123/100 ink prevented save. Shared fixture fixed.
- `WorkshopIntegrationTests.LockedTrialShowsAnExplanationWithoutThrowing`: overbudget preview returned before reaching ownership failure, leaving previous applied-drawing status. Shared fixture now fits so the unchanged ownership explanation assertion exercises its intended branch.
- `WorkshopIntegrationTests.SavedDraftAndFieldPlacementAreIndependentOfLaterEditing`: overbudget shared drawing blocked save/assignment; starter remained assigned. Shared fixture fixed.
- `WorkshopReadabilityTests.AlbumCardsRenderCompositionHealthAndFireRateBelowTheirNames`: oversized body prevented fifth album card. Shared fixture fixed; expected card count and text assertions retained.

Round 1 full EditMode: **818/818 passed**, 0 failed/skipped, job `d55b13520f194437b4172223734a374e`, 26.287 s. This includes all seven review regressions GREEN. Root reports the scoped independent reviewer considers both P2 findings addressed, with no new findings.

Round 1 full PlayMode: **320 passed / 1 failed / 321 total**, job `12a25967a99f439a88609e85dae74c21`. All 14 prior failures now pass. Sole failure: `BootstrapProfileIsolationTests.OrdinaryLaunchStillLoadsItsConfiguredProfileStoreExactlyOnce` expected `Saved`, actual `Unsaved`, line 76. Its fake stored profile was freshly constructed without current permanent initial grants, so preparation correctly marked it dirty. The fixture now receives the actual configured workshop catalog and applies its grants before returning a modeled already-saved primary profile. The exact single-load, Saved and 321-gold assertions remain unchanged. No production change was required.

Targeted bootstrap suite after fixture repair: **15/15 passed**, job `eccd406c98d94df88dea5a5366c115dc`, .8616 s. Core task complete and scoped review clean. No remaining known core failures; a fresh full PlayMode run after UI integration remains root's responsibility. Source remains frozen.
