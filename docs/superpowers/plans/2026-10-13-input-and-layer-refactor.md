# Input and layer refactor

Goal: improve reliability and drawing/building ergonomics while retaining the game's existing rules, assets and profile format.

User priorities: reliability and convenience; drawing and tower assembly; combat improvements where justified.

Design: Presentation translates Unity device/UI/camera state. Application owns a cancellable construction gesture, the active run loadout and persistence transaction status. Core continues to own prices, placement, economy and progression. Profile IO stays behind IWorkshopProfileStore. Existing RunController entry points remain compatible.

Alternatives considered: reorganizing the entire project would add migration risk without clear player benefit; cosmetic file splitting would not remove responsibilities. Extract the two concrete policies and fix the observed interaction boundaries.

## Work

- [x] Establish existing PlayMode baseline: 296/296 passed.
- [x] Add regressions for release-frame construction, continuous erasure, invalid/UI-origin drags, cancellation, and intermission choice time; five input regressions and the countdown test failed for the expected assertions before fixes.
- [x] Extract ConstructionGesture into Application and integrate DrawingInput. Only presses that begin on the map own a gesture; leaving the map/UI cancels it. Include the release position. Interpolate walls/spikes and erasure; towers remain single-cell. Cancel on disable/focus loss/tool change.
- [x] Expose the active run loadout to combat UI and route build quotes through RunController so the same application restrictions apply to quotes and execution.
- [x] Extract ProfileSaveCoordinator from RunController; preserve candidate-before-publish, protected source, dirty reward and retry behavior. Existing persistence regressions passed in the 35/35 targeted PlayMode run.
- [x] Freeze intermission countdown during upgrade selection, resuming after selection; preserve normal construction/reload progression. Full EditMode suite: 780/780 passed.
- [x] Run EditMode and PlayMode suites, perform independent code review, update architecture and verification report. Final: 780/780 EditMode, 311/311 PlayMode passed; report in docs/validation/2026-10-13-input-and-layer-refactor/report.md.

Review focus: mouse release at a new cell; held mouse after cancellation or UI press; immutable active loadout versus live configuration; unsaved rewards and rejected profile edits; transitions between workshop and battle.

Execution: implement in this workspace, preserve scene references and existing public APIs. No dependency additions, balance asset changes or profile migration. Verification via connected Unity 6000.5.7f1.

Review follow-up: batched input exposed loss of mouse-down coordinates inside Input System's event merging. Capture the down position with InputAction and acquire/release precise mouse-event settings for the component lifetime. Shared ownership restores the previous setting only after the last input disables. Two event-order regressions and ownership restoration verified RED→GREEN; focus and current-pointer priority tests added. All 15 input tests and final suites passed.
