# Drawing workshop, 2026-10-14

## Accepted design

The player draws functional body and barrel geometry. Attempts and corrections are free. Each construction has an ink capacity; gold permanently unlocks reusable drawing aids and stencils. Combat construction keeps its existing ink price. These are separate limits, clearly named in UI. User explicitly selected this economy.

The editor will offer pencil, line, rectangle, ellipse, whole-stroke eraser, move selected part, resize, mirror, grid/snap, fit/zoom, undo/redo and reusable stencils. Outline tools suit the game's vector paper artwork; image painting/fill and arbitrary color are outside this increment. Functional geometry remains independent of authoring tool.

Stat goals are rapid fire, long range and strong single hits; large bodies improve health/capacity at an ink, build-time and price cost. Weapon type still controls special effects and projectile behavior. UI must show actual resolved stats and contextual tradeoffs, with no template scoring.

Initial balance: allowance 65 + 35 per extra mount; used ink = body area * 50 + sum(barrel length * 25 + barrel width * 35). The full-bonus geometry costs 80 for one mount, so all advantages do not fit together. Typical .56-area body, .8-long/.2-wide barrel costs 55. Decorative strokes do not consume functional allowance. These values are editable in content configuration.

## Global constraints

- Core contains pure rules, geometry, editor commands and immutable config snapshots; Application coordinates profile persistence and queries; Presentation handles input and display only.
- Expose rule coefficients, geometry thresholds, design ink allowance/costs and unlock/stencil definitions through existing content authoring configs. Validate non-finite/invalid inputs; deep-copy mutable authoring data.
- Spend gold only through existing transactional profile-save flow. Purchases are idempotent and survive reload. Stencils remain reusable; manual drawing never requires buying a stencil. Essential pencil/eraser/undo remain free.
- Geometry changes, stamps and transformations are undoable, bounded and atomic. Cancellation never applies the working drawing. Ink overflow remains editable but blocks apply/save/deploy, with a useful explanation. Historical saved geometry must remain recoverable after changed balancing.
- Use the same config snapshot and stat resolution in preview, validation, trial and combat. Preserve existing saves and user changes. No commits, destructive cleanup or scene/profile mutations outside isolated validation.
- Visible text uses the existing UserText resource pipeline. Keep old public UI entry points used by tests where practical.
- Meaningful mechanics tests, integration tests, full existing suites and a live editor screenshot are required. Only the main agent operates the shared Unity Editor.

## Tasks

1. Core/config/economy: configurable freeform rules and budget, reusable drawing unlock catalog, persistence/application seams; pure shape/transform commands; tests. Owned by a focused implementation agent.
2. Editor: improve layout and tool interaction, goals/stats/ink meter, stencil browser and purchase actions, keyboard shortcuts; focused tests. Follows task 1 contracts.
3. Integration: review, fixes, full validation, live demonstration and user/configuration documentation.

## Validation ledger

- Baseline contains uncommitted freeform implementation from previous turn; preserve it. Prior evidence: 799 EditMode + 319 PlayMode passed. New evidence pending.
- Red test `DrawingWorkshopRulesTests.FullCanvasFunctionalGeometryCannotBeSavedOrBuilt` failed as expected: oversized complete construction was accepted. Job c8134f2df7f44fcfab73a6d7613e8178, 1 failed / 1 total before implementation.
- Core final source: full EditMode 811/811 passed, 0 failed/skipped, job82ca6b6b22a84769a7e62f2b1806c500; includes custom-threshold profile recovery. Focused PlayMode4/4 passed purchases/save failure/reload and shared preview/combat. Independent task review underway.
- Task1 review found initial drawing rights not persisted and final numeric overflow; both fixed and scoped re-review approved. Added7 regression cases, all green in full818/818 EditMode (d55b13520f194437b4172223734a374e). Oversized historical UI fixtures adjusted to the new limit without weakening assertions. FullPlay320/321, sole unprepared stored-profile fixture corrected, targetedBootstrap15/15 passed (eccd406c98d94df88dea5a5366c115dc). Task1 complete; final fullPlay will be repeated after UI.
- Task2 implementation dispatched to drawing_ui; review baseline in ignored Library/CodexDrawingReview/ui-baseline.json.
- Task2 complete: focused35/35 PlayMode passed (f1d77287689c4acf9a025fbee8f988ef), includes real NotebookHand font fit at .6/.8 scale. Independent review fixes verified: physical-pixel grid/stencil strokes, shared grid/snap spacing, ownership cache. Live1280×720 final screenshot `docs/validation/2026-10-14-drawing/drawing-workbench.png` shows complete labels and grid.
- Ruling: include trial UI mesh batching in integration task — the accepted editor→trial workflow reproduces a Unity65k-vertex exception with3freebarrels/defaultcrowd — a fix is necessary for cohesive working mechanics; no gameplay rules or balance change. Root measured max87,616vertices (157enemies/1tower) and80,666vertices (110enemies/10towers) at23.333334s. Preserve all geometry through reusable batches rather than truncation. Pre-fix source snapshot `Library/CodexDrawingReview/trial-baseline.json`.
- Trial batching complete: RED131,120vertices reproduced; GREEN3/3 (`afdb4701eaf24bf3807d67323f2cfb60`), root live231frames scanned, max64,998vertices perbatch/2activebatches, noConsoleerrors; screenshot `trial-batched.png`. Static final reviewer found no defect in triangle preservation or pool lifecycle.
- Full verification: Edit818/818 (`54bbc134da57420a9995474f70758a27`); Play344/348 (`9541c9a3d1c44045ba9238382b88a7d2`). Four integrations require one repair: restore FinishStroke no-pending refresh seam used after publicSession mutations (two old scene tests), update title override fixture to newkey, and update viewport-exit test to cancellation/freshpress behavior.
- Ruling: leaving the drawing viewport cancels a pending gesture and re-entry requires a fresh press — aligns with focus/tool cancellation and prevents accidental joined geometry — update old auto-reentry expectation while strengthening no-history/newpress/outside-eraser assertions. No new design question is required for this reversible input detail.
- Final compatibility repair reviewed and passed54/54 focused tests. Final source:818/818EditMode (`e224f18f42b74365ae355fcb278da052`),349/349PlayMode (`ea9c474a3c744cfa9a5c9e713636f7e9`),12/12Python. No failures/skips or open review findings. Task3 complete. All tasks complete; documentation, measured balance examples and final screenshots saved. Existing local branch/worktree retained; no commit/push/merge or user-profile changes. MechanicsHub restored clean and Play stopped.

