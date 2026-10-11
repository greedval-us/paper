# Workshop and ink loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox syntax for tracking.

**Goal:** A playable pre-run workshop, blueprint album and trial sheet connected to colored objectives and enemy tactics.

**Architecture:** Immutable blueprint values and a persistent workshop profile stay in pure Core. GameSession owns battle state; partial files separate construction/objectives from enemies. Application saves profile snapshots and exposes readonly projections. Presentation sends commands only.

**Tech Stack:** Unity 6000.5, C#, uGUI, Input System, existing batched procedural renderer.

**Spec:** ../specs/2026-10-12-workshop-and-ink-loop.md

## Global Constraints

- Two mounts/load10; Gun4, Piercer6, Mortar7, Armor3. Duplicate modules cannot expand load.
- Gold unlocks rights once; assembly/trials are free; four blueprints limit diversity, not instances.
- Material/color remain field decisions; weapon/module refit is forbidden during a run.
- Preserve pure Core and readonly Presentation boundaries; no per-enemy pathfinding.
- Balance numbers are initial hypotheses. Existing player performance evidence remains dated baseline evidence.

## Review Focus

- Loading corrupted/old profile repairs slots without inventing gold or losing valid unlocks.
- Failed overload/unlock/build/color commands leave all resources and album entries unchanged.
- Well activation during an assault cannot earn that almost-finished wave's payout.
- Repeated color hits refresh bounded effects; damage never heals or chains indefinitely.
- Workshop and training pause battle and never grant gold or consume run resources.

### Task 1: Blueprint domain, permanent profile and shared-feed firing

Files: Core/TowerBlueprint.cs, WorkshopProfile.cs, WeaponSystem.cs, MortarProjectile.cs; Tests/EditMode/WorkshopTests.cs.

Interfaces: immutable TowerBlueprint; TowerCatalog.Validate/Load/Capacity/Cost/Health/BuildSeconds/FeedMultiplier; profile SaveBlueprint/AssignSlot/Unlock/ToData/FromData.

- [x] Write tests for overload rejection, free reuse, permanent one-time unlock, four-slot repair and 1.6× shared feed.
- [x] Implement catalog/profile and immutable instance snapshots; two independent weapon cooldowns share a bounded feed multiplier.
- [x] Run actual Unity EditMode tests; review weapon/color propagation including delayed mortar hits.

### Task 2: Enemy effects and direct ranged attacks

Files: Core/EnemyEffects.cs, EnemyMechanicsTypes.cs, GameSession.Enemies.cs; Tests/EditMode/EnemyMechanicsTests.cs.

Interfaces: TickEnemyMechanics, OnEnemyDeath, TryRangedAttack; EnemyEffects.ApplyHit/Advance/MovementMultiplier; readonly blot/glob views.

- [x] Test one-use merged blots, expiry/refresh, bounded colors, clamped damage and straight glob wall interception.
- [x] Implement grid-based blots, timed status damage, visible windup and finite glob lifetime.
- [x] Run Unity tests after root integration of per-step hooks.

### Task 3: Battle construction, optional wells and profile persistence

Files: Core/GameSession.cs, GameSession.Construction.cs, GameSession.Objectives.cs, ReadModel.cs; Application/RunController.cs, WorkshopReadModel.cs, WorkshopPersistence.cs, GameBalanceAsset.cs; Tests/EditMode/InkLoopTests.cs.

Interfaces: QuoteBlueprint/TryBuildBlueprint/TryPaint/TryActivateWell/ColoredInk; readonly Wells/GoldEarned; Application workshop commands and StartRun.

- [x] Test blueprint atomic cost/construction, instance independence, color payment once, well loss/recapture/whole-wave eligibility and gold only for completed waves.
- [x] Integrate up to four shared flow fields, immutable blueprint loadout, and component-specific construction health/time/refunds.
- [x] Save versioned profile after unlock/album changes and earned wave gold; preserve on defeat, isolate tests from player saves.
- [x] Run EditMode and PlayMode boundary/persistence tests.

### Task 4: Workshop, album, briefing and trial sheet

Files: Presentation/UI/WorkshopHud.cs, NotebookHud.cs; Input/DrawingInput.cs; rendering files; Core/TrialRunner.cs; Tests/PlayMode/WorkshopIntegrationTests.cs.

- [x] Implement three real deterministic training scenarios and equal-budget comparison without profile hooks.
- [x] Add live assembly drawing, visible slots/load/statistics, album names, part unlocks and four chosen blueprints.
- [x] Add map/threat/well briefing, field palette/colors/blotter and quick camera jumps; render tiers, blots, windup and two barrels.
- [x] Verify pointer blocking, frozen battle while workshop is open, trial isolation and gameplay restart.

### Task 5: Integrated verification and documentation

- [x] Independent review of Core/Application/UI contracts; fix findings.
- [x] Fresh complete Unity EditMode/PlayMode tests, real workshop/battle screenshots and Windows development build.
- [x] Record current checks and limits in docs/validation; update architecture and usage instructions.

No git repository is present; changes remain directly reviewable in the project files.

