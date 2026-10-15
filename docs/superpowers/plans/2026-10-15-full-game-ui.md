# Full game UI implementation

Reference: `docs/design/full-game-ui/DESIGN.md` and its screen album.

User approved implementation in Unity. Work uses branch `codex/full-game-ui` in the existing checkout so the connected Unity Editor continues to compile and render the actual project. No balance, simulation, profile format or authored blueprint data changes are intended.

- [x] Update player primitives and responsive layout. Keep the existing uGUI Text/font stack to preserve the tested drawing/editor components; no unrelated TMP migration.
- [x] Implement home, map, preparation, pause, upgrades, results, settings and help from the album, reading all values from RunController.
- [x] Implement battle layout, world viewport, tool groups and contextual information with existing commands.
- [x] Adapt guided workshop, album, trial, stencils and specialist editors; preserve draft/session state on resize.
- [x] Add search, confirmations and functional settings/error states; preserve modal input boundaries.
- [x] Verify changed navigation and responsive behavior with tests, then run existing suites.
- [x] Capture and inspect Unity pages at 1600×1000, 1280×720, 1024×768, 1920×1080 and 2560×1080. Fix clipping/overlap and record actual evidence.
- [x] Request one fresh final code review, resolve findings, record validation and remaining limitations.

Implementation boundaries: page construction belongs in PlayerShell; navigation in PlayerGameRoot; battle commands in existing RunController/DrawingInput; workshop logic remains in WorkshopWizard and existing specialist editors. Responsive changes must not reconstruct a drawing session or alter accepted strokes.

Completed evidence: [verification report](../../validation/2026-10-15-full-game-ui/REPORT.md), [96 Unity captures](../../validation/2026-10-15-full-game-ui/index.html). Final suites: 878/878 EditMode, 469/469 PlayMode. The original scene and Game View preset were restored.
