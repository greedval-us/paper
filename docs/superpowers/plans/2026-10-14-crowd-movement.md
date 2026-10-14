# Natural enemy crowds

Goal: wide advances from all four edges, continuous movement and physical spacing between enemies.

Architecture: keep weighted flow fields responsible for route/breach cost, a separate spatial crowd solver responsible for local steering and swept separation, and GameSession responsible for combat and spawning. Serialized CrowdMovementRules own tuning; enable in the main preset, preserve legacy fixtures. Terrain and completed structures block swept movement. Rendering uses actual simulation positions.

Tech stack: deterministic C# Core, Unity serialized balance asset, NUnit EditMode and PlayMode.

Constraints: no per-enemy GameObjects or all-pairs runtime scan; no lost spawn backlog; preserve ranged attacks, slow effects, free barricades and objective routing. Tight passages may form short queues. Body spacing scales with enemy artwork; terrain navigation uses the existing cell footprint.

Review focus: corner clipping, overlap at spawn or while crossing, stuck queues, balance completion, snapshot isolation, local-query performance.

- [x] Add failing behavioral regressions for broad entrances and separation.
- [x] Implement configurable spawn bands, continuous flow steering and local avoidance.
- [x] Exercise narrow passages, terrain, barricades, deterministic replay and many enemies.
- [x] Verify full EditMode, presentation/pressure PlayMode and live map; document observed results.

Evidence: [validation report](../../validation/2026-10-14-crowd-movement/report.md). 862 EditMode + 30 PlayMode cases pass; main scene and a 100-live mixed crowd inspected. Two full eight-wave replays win using finite earned ink and ordinary rebuild/refund commands.
