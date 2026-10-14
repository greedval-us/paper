# Trial UI mesh batching — integration report

## Problem and evidence

The drawing editor's normal three-barrel design could enter a recorded trial whose detailed crowd exceeded Unity UI's per-mesh vertex limit. `TrialLaneSketch` copied the entire `MeshPen` into one `VertexHelper`; the local UGUI implementation rejects vertex counts **greater than or equal to 65,000**. The world renderer's UInt32 meshes do not have that UI restriction.

The focused regression was added before production changes. Root ran `DetailedCrowdAboveUiVertexLimitRetainsEveryTriangleInDrawOrder`: **RED, 1/1 failed**, actual **131,120 vertices**, expected `< 65,000`; job `64481d98f50b4c98946e5c0730e33389`. It uses a 240-enemy detailed recording and compares the full triangle stream against the unchanged trial renderer.

## Implementation

- `TrialLaneSketch.cs` prepares one shared composition in `Update`/`SetResult`. Its root Graphic renders the first slice and reusable child Graphics render the remaining consecutive slices. Pool creation, activation and dirty registration stay outside Canvas rebuild callbacks.
- `MeshPen.cs` adds triangle-aligned slices with at most **64,998 vertices**. Every triangle retains its position, color, UV, winding and draw order. No simulation, entity count, geometry or level of detail is changed.
- `TrialLaneBatchGraphic.cs` provides pooled `MaskableGraphic` continuations with stretched layout, inherited material/masking, and disabled raycasts. Sparse/empty recordings hide surplus batches; disabling the owner hides its children; re-enabling reuses the same pool. Owner destruction releases its children.

The deliberate tradeoff is to expand indexed triangles into their corner stream. Quads use six UI vertices instead of four (at most 1.5× for the current quad primitives), avoiding per-frame remapping dictionaries or additional slice buffers. The unchanged indexed source geometry remains shared, and child count grows only when a new peak requires it.

Public trial playback APIs remain unchanged. Diagnostic code that sets the private playback offset must call the existing private `Update` before inspecting meshes, then inspect all child Graphics as well as the root; `OnPopulateMesh` now only copies prepared geometry.

## Validation

Root confirmed a clean compile and **GREEN, 3/3 passed, 0 failed, 0 skipped**, job `afdb4701eaf24bf3807d67323f2cfb60`:

- `DetailedCrowdAboveUiVertexLimitRetainsEveryTriangleInDrawOrder`
- `BatchesAreReusedAfterSparseEmptyAndDisabledPlayback`
- `PausedBatchesFollowResizingWithoutCanvasRebuildErrors`

These verify bounded individual meshes, exact triangle-stream retention, pool reuse, sparse/empty frames, component disable/re-enable, paused resizing/pivot changes, and no unexpected Canvas errors. Root's live drawing-to-trial replay and full suites remain the final integration checks. This subagent did not operate Unity, modify scenes/profiles, or commit changes. Production sources are frozen after the focused green run.
