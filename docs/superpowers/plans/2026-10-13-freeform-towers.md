# Freeform functional tower drawing

> Implementation uses superpowers:executing-plans with an isolated UI subtask and independent code review. User approved implementation of free drawing of functional parts.

**Goal:** Replace tracing in the playable workshop with body/barrel drawing that changes real construction and combat statistics.

**Architecture:** Immutable drawing -> deterministic Core geometry analysis -> TowerCatalog resolved statistics -> existing combat, construction and trial. Presentation edits strokes and displays the same resolved values. Legacy drawings and evaluation stamps remain readable for migration, not a player grading mechanic.

**Tech stack:** Unity 6000.5, C#, existing uGUI and NUnit assemblies, no new dependencies.

## Decisions

- New drawing origin Freeform = 3, profile schema 7. Schema 1-6 remains readable. Geometry rules version 1 is explicit in saved freeform data; unknown versions fail safely.
- Preserve catalog chassis/weapon types as material/ammunition choices. Body geometry multiplies health, capacity, price and build time; barrel length/width multiplies range, shot interval and damage. Multiple barrels share feed. Bounds prevent unbounded power.
- Functional parts use existing part-local coordinates and existing attachment pivots. Barrel forward is +Y; player may invent any contour. Decoration has no statistical effect. No template outlines in editor, quality score, reference pane or quality trial in the player flow.
- Ignore disconnected marks when measuring the main component; duplicate strokes and extra point density cannot increase properties. Body needs area, each equipped barrel needs usable length. Empty/incomplete designs can be edited but cannot be saved/deployed/trialled.
- Preserve legacy artwork and catalog behavior; disable drawing accuracy penalties in live combat. Re-editing a legacy drawing explicitly converts its snapshot to freeform.
- Current live Unity project is the implementation workspace, to validate actual scene integration. No commit/push or real user profile manipulation.

## Tasks

- [x] Core: add FreeformTowerDesign immutable metrics and analyzer, Freeform origin/copy support, schema7 and version checks, TowerCatalog and WeaponSystem integration. Tests cover empty/body-only drafts, body size tradeoffs, barrel range/tempo/damage, repeat strokes, decoration, disconnected marks, stable copies and persistence.
- [x] UI: body/barrel role buttons, empty canvas without tracing, live geometry-driven stats, freeform apply/new flow, remove quality/reference UI. Add integration assertions. Own UI/renderer/text resources only; coordinate shared APIs with Core.
- [x] Verify: compile, targeted tests then full EditMode/PlayMode, independent review, fix findings, update workshop documentation and validation report.

## Shared API for UI

- `DrawingOrigin.Freeform`, `TowerBlueprint.IsFreeform`, `TowerBlueprint.HasPlayerDrawing`.
- `TowerBlueprint.WithFreeformDrawing(DrawingSnapshot)` and `TowerBlueprint.AsFreeform()` preserve assembly/id/name and produce freeform origin.
- `TowerBlueprint.FreeformDesign` returns `FreeformTowerDesign` (null for legacy). `IsComplete`, `Message` (UserText encoded), `BodyArea`, `HealthMultiplier`, `CapacityMultiplier`, `CostMultiplier`, `BuildMultiplier`, `GetBarrel(int)`.
- Barrel metrics: `Length`, `Width`, `RangeMultiplier`, `IntervalMultiplier`, `DamageMultiplier`, `MuzzleOffset`, `IsComplete`.
- `TowerCatalog` and `PreviewBlueprint` expose actual final values, including geometry. No UI formulas.

## Review focus

- Saving/reloading a freeform design must retain origin, geometry and identical statistics.
- Empty parts cannot create invisible shooting towers; preview remains usable for incomplete designs.
- Repeated strokes, decoration and disconnected marks must not inflate power.
- All drawing consumers must recognize both historical player origin and freeform origin.
- Every firing path and trial must use the same geometry values as preview; no template grade penalty remains.
