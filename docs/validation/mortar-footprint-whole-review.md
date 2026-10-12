# Final mortar footprint review — 2026-10-12

## Scope and evidence

Reviewed the approved design and implementation plan, the supplied `task-6-final-review.diff` against base `273022d2c82154f11627f21b8820cdecf35c094e`, Task 5 manifest/report, and ledger. Main review evidence was the supplied package; no git diff was regenerated. Focused surrounding reads covered the profile store, full-trial renderer, weapon dispatch, blueprint validation, and HUD draft validation. Current evidence-only documentation updates were read after the package was frozen. No code changes, Unity calls, test reruns, or subagents were used.

Read raw full EditMode 445/445 and PlayMode 206/206 succeeded (zero failed/skipped); Windows64 Development build succeeded with zero errors/warnings. Read actual MemoryOnly UI/normal field evidence, and rendered Player ×2 completed/passed with Victory, 3000 kills, 250 HP, tick 5687. Its fingerprint `9a4e907503fce63c` matches the preceding baseline Player report. Exit 0 and Console zero errors are controller-reported; Player JSON and build/test counts were independently read. Existing ComputeBuffer disposal/MemoryLeaks shutdown diagnostics occur in both old/new logs and are disclosed by documentation. Screenshots are controller-inspected evidence, not independently rendered by this reviewer.

## Strengths

- Core owns the validated immutable per-mount shape and resolved launch geometry. Blueprint copies retain shapes and origin; refitting only resets the changed weapon's footprint. Authored drawing format and quality remain separate.
- Weapon dispatch resolves the actual primary/secondary mortar shape and captures it in shell/event/read-model values. Flight survives source removal/refit. The ellipse broad phase is conservative relative to its boundary tolerance; q=1 retains the old spatial-query and target-order path and shared color budget.
- Profile v4 explicitly retains v1/v2/v3 reads. Missing/corrupt footprint metadata fails validation, future metadata maps to protected sources, and existing atomic store safeguards remain intact. Tests cover byte-preserved v3 original and failed preservation.
- The narrow probe owns one balance snapshot and immutable frame/report copies, isolates one mortar, preserves source quality/equipment/price, removes the source after launch, and records actual impact. The 9/6 versus 3/9 fixtures test real WeaponSystem behavior rather than duplicate geometry alone.
- Footprint UI shares one replay clock, invalidates both reports on pointer start, commits only on release, cancels on context change, and uses isotropic world-to-view scaling. Nonempty authored HUD -> two mount edits -> variant -> isolated store roundtrip closes the Tasks 1/3 deferred art-preservation gap.

## Issues

### Critical — 0

None.

### Important — 1 (blocking)

1. **[P2] Handle invalid workshop drafts before invoking the throwing probe contract.**
   - Primary reference: `Assets/PaperDefense/Presentation/UI/MortarFootprintPanel.cs:65`; producer `Assets/PaperDefense/Core/MortarFootprintProbe.cs:30`; exposure `Assets/PaperDefense/Presentation/UI/WorkshopHud.cs:275`.
   - Ordinary repro: in the workshop choose Standard + Mortar + Mortar (load 14, capacity 10), then click either visible enabled “След выстрела” button. The workshop deliberately supports over-capacity intermediate drafts and displays their deficit. The button is gated only by presence of a mortar. `Open` activates the sheet and `Record` directly calls `Compare`, whose `TowerCatalog.Validate` failure throws `ArgumentException`. There is no UI/Application handling. A blank-name draft after an assembly edit can follow the same path.
   - This is a routine editing state, not malformed external data. It opens a broken sheet/logs an unhandled exception instead of giving an actionable explanation; subsequent repeat/formation/edit operations throw again.
   - Fix at the UI/Application boundary: validate and display the reason, clear any stale recording, and either retain shape editing with the probe unavailable or prevent opening with a clear explanation. Keep strict Core validation. Add a meaningful HUD regression for an overloaded draft and recovery after a valid assembly is restored (and preferably invalid name).

### Minor — 1 (nonblocking)

1. **[P3] Expand the compressed new UI control and rendering code.**
   - `Assets/PaperDefense/Presentation/UI/MortarFootprintPanel.cs:70` and `:86`; `Assets/PaperDefense/Presentation/UI/FootprintProbeSketch.cs:19`.
   - State transitions, pointer guards, object creation, and mesh operations are packed into long multi-operation lines. This makes future invalidation/error-handling changes harder to review and contributed to a fragile boundary being easy to miss.
   - Expand operations and conditionals into conventional blocks and extract small named UI helpers where useful. This is the existing Task 4 readability minor, triaged as nonblocking; it does not justify rerunning the whole validation matrix by itself.

## Task 5 documentation spec/quality gate

**Documentation content: approved, zero documentation findings at review time. Overall Task 5: not complete until the blocking code finding is resolved and recorded.**

The five manifest documents accurately describe q [1,3], equal area, flight direction/capture, independent mount metadata, art/quality separation, strict v4 migration, extension seams, selected one-volley scope, shared A/B clock and finite limit. Current verification report records actual full tests/build and Player results, retains RED/GREEN history, distinguishes Editor footprint UI evidence from the built Player circular pressure scenario, and discloses existing shutdown diagnostics. It correctly keeps the whole-diff gate pending and does not claim ideal balance or deferred mechanics. After the fix, update this status and validation provenance rather than representing the pre-fix full suites/build as testing changed code.

## Recommendations

Resolve P2 through a small validation/error-state change and focused regression, then request a targeted review of that change. Refresh affected validation/build evidence as needed for the final shipped source. Preserve all existing historical raw reports. Cosmetic formatting can remain a separately tracked follow-up.

## Declined to judge

- Extending the existing 60-second `TrialLaneSketch` to draw `TrialFrame.Impacts`/ellipse outlines: its unchanged renderer currently draws only shots and shells (`WorkshopHud.cs:630`). Controller explicitly ruled this deferred: the approved first slice binds correct display to short A/B and normal field, and Task 3 scopes full TrialRunner changes to recording. Existing full-trial scenarios/results are retained. Cost if this interpretation is wrong: extend the existing large renderer and its render coverage later. Controller will record the limitation in the handoff; full-trial visual support is not claimed completed.
- Arbitrary/polygon/transverse footprints, other weapon specializations, stroke animation, and post-battle analytics: explicitly deferred by the approved design.
- Ideal gameplay balance or cross-platform float determinism: fixed deterministic coverage and same-environment regression evidence do not establish these; the design/docs make no such promise.
- Fixing historical Unity/graphics shutdown ComputeBuffer/MemoryLeaks diagnostics: same diagnostics occur in the baseline and this slice does not change that resource lifecycle; documented runtime success is narrower than a completely clean Player log.

## Assessment

**Ready to merge? With fixes.**

The shape/combat/persistence architecture and documentation are sound and validation is substantial, but an ordinary invalid draft reaches an unhandled exception at the new HUD/probe boundary. Counts: **0 critical, 1 important/blocking, 1 minor/nonblocking**. Task 5 documentation spec/quality is approved; whole-change completion remains blocked on P2.
