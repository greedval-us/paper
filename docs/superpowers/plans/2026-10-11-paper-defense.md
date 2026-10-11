# Paper Defense Implementation Plan

> **For agentic workers:** implement the owned units in parallel, then independently review the integrated result.

**Goal:** playable notebook defense prototype with a Unity-independent extensible simulation.

**Architecture:** Presentation → Application → Core. Editor composes assets and scene. Simulation data and batched drawing are independent.

**Tech Stack:** Unity 6000.5.7f1, C#, URP2D, Input System, uGUI, NUnit.

**Spec:** docs/superpowers/specs/2026-10-11-paper-defense-design.md

## Global Constraints
- No extra product packages; use installed Input System, uGUI and Test Framework.
- Core asmdef has no engine references.
- Do not overwrite SampleScene; generate a separate PaperDefense scene.
- Source and diagnostics remain in the workspace.

## Review Focus
- Invalid or duplicate stroke cells must never partly spend resources.
- Pausing must stop construction and combat while accepting reserved plans.
- A closed wall ring must remain attackable.
- UI clicks must not place world structures; fast cursor movement must produce continuous strokes.
- Restart must replace the session and clear stale presentation state.

### Task 1: Core simulation and tests
Files: Assets/PaperDefense/Core, Assets/PaperDefense/Tests/EditMode.
- [x] Define data and public commands in Core; implement weighted shared navigation and spatial combat.
- [x] Verify transactional drawing, refunds, upgrade cost, construction timing, waves and breach behavior with NUnit.

### Task 2: Batched notebook presentation
Files: Assets/PaperDefense/Presentation/Rendering.
- [x] Render graph paper, castle, structures, distinct enemy silhouettes, preview and transient shots with a few meshes.
- [x] Check construction/health/tier readability in Game View.

### Task 3: Composition and interaction
Files: Assets/PaperDefense/Application, Presentation/Input, Presentation/UI, Editor.
- [x] Adapt editable ScriptableObject settings to Core; run simulation with a fixed 1/30 second step.
- [x] Add stroke input, commands, notebook HUD, wave forecast, tool costs and upgrade choices.
- [x] Create separate scene, config and bootstrap prefab with an idempotent editor command.

### Task 4: Integration and evidence
- [x] Compile and run all project EditMode and PlayMode tests through Unity MCP (21 + 4 passed).
- [x] Play the generated scene, inspect console, exercise inputs and capture a screenshot.
- [x] Execute 3000-enemy production simulation stress probe and record honest limits.
- [x] Independent review; fix observed defects and document extension points and controls.
