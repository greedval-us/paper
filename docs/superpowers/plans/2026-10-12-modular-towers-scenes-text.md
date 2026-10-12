# Modular Towers, Mechanics Scenes and Text Resources Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved modular tower system, independently runnable mechanics scenes and resource-backed player copy.

**Architecture:** Extend the existing catalog and blueprint with indexed weapon mounts and independent equipment slots. Preserve old adapters and profile readers. A pure C# semantic text token preserves current reason-string APIs while Unity resolves resource text at the presentation boundary. Validation scenes share an isolated, deterministic scenario harness.

**Tech Stack:** Unity 6000.5.7f1, C#, existing asmdefs and Unity Test Framework. No new packages.

**Spec:** `docs/superpowers/specs/2026-10-12-modular-towers-test-scenes-text-resources-design.md`.

## Global Constraints

- Keep legacy enum numbers, ID strings, existing drawing bindings and historical combat behavior.
- Target: standard 2/2/1, heavy 2/2/1, precision 1/3/1, battery 3/1/1 weapon/upgrade/armor slots.
- New capacities 16/22, base costs 20/34, feed capacities 1.0/1.8 for precision/battery.
- Target ranges gun 0–8, piercer 0–12, mortar 6–18. Historical remains 0–11/14/16.
- Core has no Unity or filesystem dependencies; profile v6 reads v1–v5 and protects future formats.
- Test scenes never use the player's profile. Production scene/prefab GUIDs are preserved.
- Do not commit, reset, discard existing work, or modify unrelated assets.

## Review Focus

- A nearby invalid mortar target cannot block a farther valid target or incorrectly reserve feed.
- Sparse slot 2 on a two-slot base must fail deployment without destroying stored pages.
- Three mount drawings/footprints and immutable archived assessments survive migration and edits.
- Scenario reset recreates profile/session/clock and has only one simulation driver.
- Resource substitution changes visible reasons and labels without altering simulation or saved player names.

## Task 1 — Core tower model and profile compatibility

**Owner:** tower implementation worker.

**Files:** Core tower/catalog/weapon/drawing/profile DTO files; Application persistence/read-model integration; focused EditMode tests.

**Interfaces:** `WeaponMountSelection(int slotIndex, string weaponId, MortarFootprint footprint = null)`; `TowerBlueprint.WeaponMounts`, `GetWeaponId(int)`, `WithWeaponMount(int,string)`; Chassis `UpgradeSlotCount`, `ArmorSlotCount`, `FeedCapacity`; Weapon `MinimumRange`; Module `SlotKind`; indexed runtime mount access. First/Second APIs remain adapters.

- [x] Write failing tests for three mounts, slot/load limits, min-range candidate choice and profile round trip.
- [x] Observe baseline failures through the central Unity test coordinator.
- [x] Implement indexed assembly and independent combat state, preserving legacy behavior and hash paths.
- [x] Implement profile 6 read/write and legacy migration.
- [x] Run focused tests, then regression suite; review arithmetic and snapshot ownership.

## Task 2 — Resource text infrastructure and UI extraction

**Owner:** text implementation worker; coordinate file ownership with root/tower worker.

**Files:** new `Core/UserText.cs`; `Application/TextResourcesAsset.cs`; resource files; Presentation UI/Input; text extraction/audit tools; text tests.

**Interfaces:** pure `UserText.Message(string key, params object[] arguments)` returns a semantic descriptor using the current string APIs. `TextResourcesAsset` creates a validated immutable snapshot; `GameText.Resolve(string)` resolves descriptors at the Unity UI boundary. Resource ownership/overrides are instance-scoped for test harnesses.

- [x] Add failing resource substitution, placeholder and missing-key tests.
- [x] Implement immutable resource snapshot, invariant semantic arguments and culture-aware final formatting.
- [x] Extract player-visible literals from HUD/Input; root finishes Core/Application extraction after model edits settle.
- [x] Resolve button/label/input/save/feedback text at display boundaries; retain technical selectors.
- [x] Verify sentinel resource substitution, saved player names, archive feedback and literal audit.

## Task 3 — Independently runnable mechanics scenes

**Owner:** scene implementation worker.

**Files:** new Validation scenario/harness, Editor scene generator, Scenes/Tests and test scenario assets; scenario tests and guide.

**Interfaces:** scenario ID, text keys/resource override, effective preset, initial mode/seed/resources and exact flags; bootstrap preparation assigned before activation. Root owns production bootstrap and shares a safe preparation hook if required.

- [x] Add a failing test for scene inventory and memory-only preparation.
- [x] Implement scenario initialization/reset and controlled single-step simulation.
- [x] Create 23 saved scenes from shared harness data; preserve production scenes/build settings.
- [x] Implement profile fixture's explicitly temporary store and distinct reset/read actions.
- [x] Load scenes in PlayMode, verify target behavior, isolation and reset.

## Task 4 — Presentation and active catalog integration

**Owner:** root.

**Files:** Presentation Rendering, PaperDefenseBootstrap, configuration assets and targeted integration tests. Workshop UI changes coordinate with text worker.

- [x] Extend visual geometry/aim/range rings for three mounts and mortar dead zone.
- [x] Populate new base data, templates, slots and Target overrides using the existing data-driven pipeline.
- [x] Update workshop selection/slot summaries to expose all supported mounts and independent armor/upgrades.
- [x] Integrate text/scenario preparation into bootstrap without changing production startup behavior.
- [x] Verify ordinary workshop → drawing → trial → battle and legacy profile behavior.

## Task 5 — Integration verification and balance evidence

**Owner:** root plus independent review workers.

- [x] Refresh Unity and resolve all compilation errors before new component execution.
- [x] Run all EditMode and PlayMode tests; report individual failures and repair regressions.
- [x] Exercise saved scene wiring, resource override, all three aims, minimum range and reset.
- [x] Compare affordable compositions at budgets 200/400/800, mark zero-copy rows unavailable and record actual spend.
- [x] Run asset-backed campaign and pressure/integration smoke; record fresh evidence and remaining limits.
- [x] Review whole change, fix actionable findings, update README and dated verification report.

## Execution ledger

- User reviewed the concrete specification and explicitly requested implementation in the current chat. Proceed with parallel workers for disjoint files, coordinated integration and fresh verification; no repeated approval pause.
- Ruling: already serialized content names/descriptions remain valid resource-owned copy; do not introduce a second copy solely to centralize their storage. All code-owned player copy moves to text resources.
- Ruling: preserve current `string reason` APIs via semantic tokens rather than destabilizing every caller at once. Formatting occurs at presentation, never inside simulation; the token is structured message data, not translated prose.
- Completed: fresh full EditMode 748/748 and PlayMode 266/266; extractor regression 4/4. All 23 saved scenes prepare, exercise their focus and reset. Text resource has 763 validated entries; malformed numeric formats fail before profile IO.
- Evidence: 162 budget trials at seeds 1337/7331/2026, 216 counterfire/color trials, 18 paired survival trials. Default, mixed and specialized asset-backed campaigns all clear 3180 enemies with finite resources and zero failed commands. Full Pressure reaches 3000 and clears all enemies.
- Review fixes: archived feedback preservation, independent key/argument/format contracts, categorized IO errors, integration pause/speed, external pressure event delivery, indexed validation coverage and mortar dead zone. Visual stand header/footer overlap repaired; production scene/prefab/build settings unchanged.
- Final report: `docs/validation/2026-10-12-modular-verification.md`, with job IDs, raw reports, matrices, GameView captures and explicit Player/performance limits.
