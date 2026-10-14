# Freeform UI implementation

- New workshop drafts use Freeform origin. Opening a legacy drawing converts the private edit session; applying uses WithFreeformDrawing.
- Canvas contains authored strokes, a small attachment anchor and a barrel forward arrow. Template contours are removed.
- Functional role buttons identify body and numbered barrels; decoration remains optional. Module layers are preserved in legacy snapshots but are no longer offered as functional geometry.
- Editor previews use RunController.PreviewBlueprint and Core barrel multipliers. Incomplete geometry disables Apply and direct commit also rejects it with the Core reason.
- Playable quality details and comparison buttons removed; normal construction trial remains available in the drawing-focused scene and full workshop. Trial UI has its quality toggle hidden. Legacy public comparison API delegates to the normal trial for compatibility.
- World, UI, album and trial rendering now use HasPlayerDrawing for historical and freeform artwork.
- Russian resource copy describes geometry mechanics; visible statistics no longer show tracing grades.

Validation: git diff --check passed. DrawingEditorFeedbackTests updated for removal of grading and extended with private conversion, incomplete commit and empty canvas tests. DrawingQualityHudTests and FocusedWorkshopSceneTests now draw complete functional parts and verify normal trial behavior. Validation harness calls RunDrawingTrial. Unity compilation and test execution are owned by the coordinating agent to avoid simultaneous Editor runs. Generated text contracts updated for changed stats placeholders and new freeform keys.

Follow-up: migrated authored workshop, data-driven UI, footprint UI, readability, workshop integration and art stand tests to complete required functional geometry before applying. Art stand callback now applies and restores Freeform drawings; rejecting an incomplete gallery edit preserves the applied snapshot. Removed obsolete disabled-system quality-details assertions.

Final UI cleanup: error and live-stat columns now occupy separate rectangles beneath a shorter viewport. Default canvas scale fits the entire permitted ±1 drawing area. Removed all quality panel builders, hidden quality toggle creation, trial grading branches, and unused grade arguments from workshop stat rendering. Added layout/full-canvas regression assertion; compatibility CompareDrawingQuality delegates to the normal trial.

PlayMode follow-up: resolved eleven old test assumptions by refreshing direct session edits before interactable checks, testing actual viewport exit after scale change, using first-mount geometry interval for cooldown, selecting the VertexHelper mesh overload explicitly, supplying complete freeform geometry to ownership/footprint probes, and asserting module roles are absent. Footprint overload probes use a body tuned to retain the configured capacity, so they continue testing actual overload/recovery. Added short/long rendered projectile-origin regressions and removed remaining footprint/stand grade labels. Full verification remains with the coordinating agent.
