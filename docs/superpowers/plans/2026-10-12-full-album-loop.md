# Full authored album loop implementation

User authorization: full task attached at e82f62d9-4add-46a4-887d-180a35e5b2bc. It supersedes per-stage approval gates in earlier documents. Continue sequentially after checks; no push, publication, data loss or profile reset.

Baseline: main 1268ed4d99924783aec206a0460b694a3ba24fd2, Unity6000.5.7f1. Existing 110 EditMode/29 PlayMode baseline. No AGENTS. Existing scene stays attached to live Unity; no recreation.

- [x] 1. Implement protected profile store, exact working copies, transactional commands/dirty rewards, shared balance snapshot and trial. Fresh Unity: 122 EditMode + 65 PlayMode passed; RED and GREEN reports retained under docs/validation/full-loop-stage1-*.json.
- [x] 2. Editable normalized part strokes, template catalog, undo/redo/eraser/zoom/part replacement, immutable album pages, v1-safe migration, author geometry cache and independently rotating mount views. Fresh143 EditMode +90 PlayMode passed, including pointer→save→reload→copies and full64×2048points UTF8 album.
- [x] 3. Deterministic contour assessment with tolerance/anti-duplicate/anti-fill, explanations, configurable 0.85–1 execution coefficient exactly once, same-construction trial comparison. Fresh166 EditMode +93 PlayMode passed. Viewport escape regression reproduced live and fixed.
- [x] 4. Shared construction/effect/progression rules and parameter dictionary, with unchanged historical defaults and reflection/snapshot coverage. Fresh196 EditMode +100 PlayMode passed. Cheap-assembly price guard and configurable Gnawer siege behavior verified.
- [x] 5. Separate target preset: finite rewards (no time income), adapt Salvage, material damage neutral, armor defense, defense cap0.6, dynamic active-mount feed, shared bounded color exposure; compare historical preset. Fresh238 EditMode +105 PlayMode;36 equal-budget comparisons and material/armor table saved. Separate target asset referenced by existing scene prefab.
- [x] 6. Sequentially add/test Fan, Ricochet, Stapler, Blotter and Rail module: permanent unlock, load/cost/rules/template/rendering/save/trials, no recursive capacity. Explicit finite blot training scenario. Fresh347Edit+147Play allpassed; individual RED/GREEN reports retained. Drawingformat2 migrates strict seven-part format1 losslessly, binding1/profilev3 unchanged.
- [x] 7. Fresh351 EditMode+167 PlayMode passed. Isolated authored double-gun reload/copies/UI scenario, actual target campaign3180/base250 with well/color mechanics,120 authored-copy mesh load,3000 headless and rendered Windows Player x1/x2 matching fingerprint, Windows development build, current docs and inspected screenshots. See docs/validation/verification-full-album-2026-10-12.md.

Interfaces remain Presentation→Application→Core. Core has no Unity/IO. New numbers are adjustable hypotheses, recorded in validation and guide. Preserve raw author strokes and legacy pages. No unlimited geometry/currency/status budgets.
