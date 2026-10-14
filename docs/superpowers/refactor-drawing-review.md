# Independent drawing refactor review

Reviewed against baseline `5ab2424285855fb4fd0747797cb79f0e8f0fb8b9` on 2026-10-12. Inputs: `plans/2026-10-12-core-refactor.md`, `refactor-drawing-report.md`, `refactor-drawing-review.patch`, current source and the saved pre-extraction characterization result. Unity C# scripting guidance was applied. No Unity operations, test reruns, player profile IO or source edits were performed by this reviewer.

## Verdict

**Spec compliance: PASS for the assigned drawing/limits package.**

**Quality: PASS. No actionable P0–P3 issue found in the assigned changes.** Full post-refactor EditMode/PlayMode and broader integration acceptance remain the coordinating agent's responsibility; this review does not claim those runs have completed.

## Behavior and compatibility assessment

- `DrawingPartPolicy` preserves the old activation and retention predicates for every valid persisted `DrawingPartKey`. The module binding switch is shared by activation and retention. Base and first mount compare their respective assembly identities; the second mount requires the same installed weapon; optional parts require their module in both assemblies; decoration survives.
- `TowerBlueprint.WithAssembly` still returns the existing instance for an exact assembly no-op and keeps the legacy-to-empty-authored-draft branch before retention. `DrawingEditSession.ReplaceBlueprint` still evaluates its no-op before building history and retains from `Snapshot.Parts`, so unsaved strokes remain the source. Undo/redo state publication is unchanged.
- The old editor switch's default returned true, whereas the shared policy returns false for unknown keys. This is not a reachable compatibility regression: `DrawingPart` rejects undefined enum keys and `DrawingSnapshot` rejects keys outside its canonical eight parts. Decoration is handled explicitly.
- Dormant parts survive snapshot loading and exact no-op. A real assembly replacement keeps the historical before/after module intersection, so installing a previously missing module does not revive dormant lines. Template order, pivots and contour generation are unchanged.
- `ProfileLimits` preserves the previous 64 pages, 4 slots, 64-character IDs and 48-character names. Core allocation, iteration, strict-load validation, command limits and associated messages retain their previous behavior. `DrawingEditorPanel` uses the existing `DrawingSnapshot.MaxPoints` value of 2048 for both its counter and pending stroke cap.
- Serialization DTO declarations, field names, namespaces, current profile/drawing/binding/metadata versions, existing GUID files and persistent IDs are unchanged within this package. Explicit `ChassisKind` numbers remain the previous 0/1. Existing drawing part/module numeric values stay unchanged. No balance arithmetic, RNG, persistence transaction or reward code was changed by this package.

## Characterization evidence

The eight replacement cases compare both production entrypoints against literal expected hashes and explicit stroke/point expectations. They do not obtain expectations from the extracted helper or merely compare the two entrypoints to each other. Undo, redo and original snapshot preservation have separate assertions. The unsaved-stroke case begins with an empty blueprint drawing and adds strokes only to the editor, so it detects replacement accidentally using `Blueprint.Drawing`.

The current replacement literals match the saved pre-extraction case names exactly: chassis `a06d66b6d7fd7891`, first mount `92b6bb72a4ceb3db`, second mount/change or removal `f0ef550f27e73105`, armor `4f184c1b7ba8f7ff`, feed `bd1ffbfff8865229`, tracking `e583295cebaec9ab`, rail `899949a009d7430f`. The all-parts literal is `7e13935604a832fe`, matching the original capture documented in the report. Thus no fixture rewrite is evident between the captured run and current tests.

`docs/validation/refactor-2026-10-12-characterization.json` records all 22 drawing cases as Passed before extraction. Its overall Failed status comes from a separate catalog price expectation, not a drawing test. Boundary and numeric expectations in these tests are literals, independent of the new constants. Existing persistence coverage remains relevant for legacy load, maximum pages, future format protection and atomic/manual save behavior.

Scoped `git diff --check` passed. New GUIDs occur once each in the asset metadata search; existing scoped `.meta` files have no baseline diff.

## Broader integration observation

Outside this assigned package, `WorkshopHud` still contains its existing four-slot array/loops at lines 28, 169 and 305, and `NotebookHud` has four-slot selection logic. If the overall refactor's final claim is that the slot count has one definition across every UI consumer, the coordinating agent should include those consumers or describe the narrower extent of centralization. The fixed four-slot behavior is unchanged; this observation is not a runtime defect introduced by this patch.
