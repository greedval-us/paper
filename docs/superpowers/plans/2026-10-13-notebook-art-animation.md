# Notebook Art and Animation Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development. User requested continuous implementation without additional approval pauses.

**Goal:** Ship notebook-style modular art, enemy/weapon animations and isolated visual test scenes while preserving player drawings and combat outcomes.

**Architecture:** Resource-authored vector shapes and clips feed existing batched meshes. Add immutable cosmetic event metadata to existing combat branches. Reuse one tower composition for world, preview and replay; player strokes remain the source of authored towers.

**Tech Stack:** Unity 6000.5.7f1, C#, uGUI, ScriptableObject, JSON/SVG sources, existing MeshPen renderer.

**Spec:** ../specs/2026-10-13-notebook-art-animation-design.md

## Global Constraints

- No new packages, gameplay balance changes, per-enemy GameObject/Animator, or mutable simulation references in events.
- Preserve bindings, current pivots, drawing points/hashes/assessments and Historical.
- All visible text resolves from resources. Decorative geometry does not enter quality assessment.
- Root owns Unity tools and test execution; workers edit disjoint scopes. Git metadata is read-only; leave reviewable working-tree changes.

## Review Focus

- Three identical barrels: a successful action animates only its mount, once.
- Old/partial/empty drawings: never substitute reference art or recompute unchanged quality.
- Pause, terminal phase and replay seek: clock does not drift or duplicate effects.
- Missing assets/overflow: bounded fallback without false deaths or persistent effects.
- Large crowds: batching, culling and bounded caches survive rendered Pressure3000.

### Task 1: Cosmetic combat metadata and trial recording

Files: Core/SimulationEvents.cs, weapon/enemy/blot systems, TrialRunner and record data; Editor tests.

Produces: additive SourceStructureId, MountSlotIndex, ActionId, SimulationSeconds and immutable death metadata; one WeaponActionStarted; TrialFrame.Events interval records. Existing APIs and enum values stay compatible.

- [x] Add tests for independent mount actions, fan/ricochet dedup, mortar source lifetime, death/DoT and recording.
- [x] Implement metadata at existing successful branches without altering calculations or RNG.
- [x] Run focused EditMode tests and compare deterministic outcomes.

### Task 2: Art sources, resource catalog and drawing template publication

Files: ArtSource/Notebook, Presentation/Art/NotebookVisualThemeAsset.cs + NotebookArtRenderer.cs, Editor/NotebookArtImportTools.cs, Resources/Art, current Target overrides/test catalogs; Editor tests.

Produces: NotebookVisualThemeAsset.Default, FindShape(id), FindClip(id), FindRecoil(weaponId); NotebookArtImportTools.ImportAll(). Resource data includes 15 parts, 12 enemy variants and clips, objects, FX and icons. Reference group revisions publish to current catalogs only; export SVG for visual inspection.

- [x] Add validation tests for IDs, frame references, finite coordinates and drawing limits.
- [x] Author JSON shapes/key poses and importer with last-good-data preservation.
- [x] Import resources; verify Target and test contours agree while Historical is unchanged.

### Task 3: Batched animation/render integration

Files: Presentation/Rendering/DoodleGlyphs.cs, NotebookWorldView.cs, new animation/clock/composition classes; TrialLaneSketch; rendering tests.

Consumes Task 1 events and Task 2 resource shapes/clips. Produces NotebookTowerComposition shared across standard/authored representations and NotebookWorldView.Paused hook.

- [x] Add rendering tests for three mounts, rigid authored recoil, clock and bounded history.
- [x] Implement art/clip playback, enemy actions/death tails, actual statuses, object variants and cosmetic effects.
- [x] Keep static geometry separate and preserve compact swarm/culling.
- [x] Reuse recorded interval events and playback time in trial.

### Task 4: UI integration and isolated stands

Files: new Presentation/Art/NotebookArtGraphic.cs, art stand component; Editor art scene generator; existing WorkshopHud/Bootstrap outside TrialLaneSketch; text resources; PlayMode tests.

- [x] Add icons from the same resource shapes and reuse tower composition in preview.
- [x] Create TowerArt, EnemyAnimation and InkEffects scenes with camera/light, resource text and memory-only controls.
- [x] Wire pause to world clock and keep the 23 existing mechanics scenes independent.
- [x] Verify saved stand scenes, resource coverage and authored drawing compatibility in PlayMode.

### Task 5: Verification and delivery

- [x] Compile in Unity; resolve console errors before tests.
- [x] Run focused tests, full EditMode and PlayMode regression, resource validation and rendered pressure measurement.
- [x] Inspect screenshots of all three stands, drawing editor and integration battle; fix clipping/readability.
- [x] Save source/assets, usage guide and dated verification report; leave TowerArt open and running.

## Decisions

- User's request to finish assets overrides repeated design/plan approval pauses; proceed in this thread.
- Use current JSON/ScriptableObject and batched geometry rather than adding SVG runtime or Animator dependencies.
- A generated paper texture is optional decoration; gameplay art and player-compatible contours remain resource-authored vectors.
