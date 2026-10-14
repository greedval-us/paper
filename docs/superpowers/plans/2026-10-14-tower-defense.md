# Notebook tower defense implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Execute continuously within the user's authorized request.

**Goal:** Deliver a playable fixed-road defense game with readable enemies, terrain and meaningful construction choices.

**Architecture:** Pure Core resolves optional battlefield data and owns all restrictions, navigation and effects. Application serializes configuration and exposes read models. Presentation consumes that model and canonical vector assets; it does not duplicate gameplay rules.

**Tech Stack:** Unity 6000.5.7f1, C#, uGUI, deterministic vector JSON/SVG authoring, NUnit and Python tests.

**Spec:** docs/superpowers/specs/2026-10-14-tower-defense.md

## Global Constraints

- Preserve drawing workbench, gold unlocks and run-ink separation.
- Latest user direction: main map96×64 is open terrain, enemies approach all four sides, barricades are freely placed. Earlier fixed-road work remains optional configuration only.
- Default legacy test configurations stay open-field unless battlefield data is supplied.
- New mechanics and assets are configurable, snapshots immutable, simulation independent of Unity.
- Only controller operates shared Unity Editor; implementers request test windows and stop C# edits during runs.
- Work in current codex/full-album-loop; do not commit, push, modify user profile or discard existing changes.

## Review Focus

1. Active wells and the castle remain reachable across open terrain; optional fixed roads retain forward-only objective behavior.
2. Existing blueprints cannot bypass terrain/objective reservations or get charged for invalid placement; explicit road scenarios retain their restrictions.
3. Full spawn cells retain wave backlog; large sprites must not change contact geometry.
4. New enum values work across snapshots, content resolution, trial and all menus without missing definitions.
5. Repair, slow and sale cannot form infinite invulnerability or resource loops.

### Task 1: Core battlefield, enemies and construction

**Files:** Create focused Core/BattlefieldDefinition.cs, Core/GameSession.Battlefield.cs and Core/GameSession.Support.cs (or equivalent responsibility split). Modify GameTypes, GameSession, WeightedFlowField, read-model wrappers, validation and affected combat helpers. Extend Configuration/Content/Target.asset and its authoring path. Add Tests/EditMode/TowerDefenseRulesTests.cs and targeted integration tests.

**Interfaces:** Produce optional GameBalance.Battlefield and immutable session/read-model battlefield with road, build and terrain queries; publish exact API in report. Add StructureKind.TarPit and RepairPost, EnemyKind.Brute and Bulwark, enemy VisualScale data and configurable wave health growth. Keep old IDs stable.

- [x] Write behavioral tests for spec rules, including malformed/disconnected maps, well reachability, blueprint placement, atomic ink costs, max health growth, strong slow precedence and nonstacking bounded repair.
- [x] Implement resolved map, road-aware weighted navigation/spawn and shared placement predicates, two structures, two enemy families and snapshots/validation.
- [x] Configure the main Target asset with the spec's calibration; reduce swarm density for legible large enemies. Update actual asset-generation defaults so regeneration preserves this preset while legacy test scenes stay compatible.
- [x] Ask controller for focused Unity tests; fix failures and report exact APIs, measured baseline blueprint damage and remaining presentation work.

### Task 2: Vector art, map rendering and icon palette

**Files:** ArtSource/Notebook/author_notebook.py and notebook-theme.json; generated Art/*.svg and Resources/Art/NotebookVisualTheme.asset; Presentation/Rendering/DoodleGlyphs.cs, NotebookWorldView.cs and focused battlefield renderer; Presentation/UI/NotebookHud.cs, Art/NotebookArtGraphic.cs, Input/DrawingInput.cs and focused catalog/tooltip helpers; PlayMode asset/UI tests.

**Interfaces:** Consume Task 1 map read model, new structure/enemy kinds and visual scale. Core remains placement authority. Publish no mutable config references.

- [x] Extend recipe with terrain, two enemy families/animations and all tool icons; generate deterministically and verify canonical importer.
- [x] Render roads/entrances/marked barricade cells and terrain from map data; enlarge enemy art including death/hit/trial fallback, render visible HP bars with clamped ratio.
- [x] Replace overflowing text-heavy toolbar with icon-led construction cards. Add tar and repair selection, prices and purpose/restriction descriptions, preserve hotkeys and gesture cancellation. Ensure new tools are not offered when definitions/mechanics unavailable.
- [x] Add focused integration coverage for actual scene config, all new asset IDs and tool selection/preview agreement. Controller imports theme and runs checks.

### Task 3: Integrated balance and visual acceptance

**Files:** Tests/EditMode/TowerDefenseBalanceTests.cs as needed; docs/Tower-Defense.md, updated mechanics/balance/art guides and docs/validation/2026-10-14-tower-defense evidence.

- [x] Measure actual starter blueprint shots-to-kill and deterministic opening/mid/late wave performance, tune data with recorded reasons. Verify early viable defense within starting ink and growth in late-wave pressure.
- [x] Run full EditMode, PlayMode and existing Python authoring tests after implementation stabilizes; resolve regressions without weakening assertions of unchanged behavior.
- [x] Play isolated main scene with memory profile, inspect normal-resolution battlefield/UI and capture screenshot. Verify console, restore MechanicsHub and stop Play.
- [x] Fresh final whole-change review, address material findings, document configurations and report limitations honestly.

### Task 4: User-directed open-terrain amendment

- Add explicit open-terrain map policy alongside fixed-road compatibility; immutable walking/build masks respect terrain and objective reservations.
- Main preset: broad clearings, terrain clusters, multiple entrances per side, four-sided waves, freely placed barricades/traps/towers. Update serialized data and authoring recipe together.
- Validate terrain navigation and connectivity, public quote/command agreement, multi-side spawn backlog, well reachability, wall bypass/breach. Keep legacy and explicit road tests meaningful.

### Task 5: Integrate and rebalance open terrain

- Update map/placement language and display for open ground; preserve icons, large enemies, health and construction choices.
- Replace main-balance road-position fixtures with a legal open-map construction plan, measuring first wave and all8 waves within earned ink. Keep historical road results labeled superseded.
- Run appropriate full checks, capture actual map/large enemies and perform final whole-change review. Restore MechanicsHub after isolated live QA.
