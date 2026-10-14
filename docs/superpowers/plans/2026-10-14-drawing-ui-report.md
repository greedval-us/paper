# Drawing workbench UI implementation report

## Scope and ownership

Implemented Presentation workbench, WorkshopHud integration and dedicated PlayMode regressions. No Core geometry/economy production edits, no scene/profile mutations, no commits and no Unity Editor actions. Root owns compilation, Unity tests and live visual QA. Existing dirty implementation was preserved.

## Files and contracts

- `Assets/PaperDefense/Presentation/UI/DrawingEditorPanel.cs`: stable panel hierarchy, part/tool selection, ink capacity, resolved whole-tower and selected-mount statistics, goals, transform controls and guarded apply.
- `Assets/PaperDefense/Presentation/UI/DrawingCanvasGraphic.cs`: moved the existing public canvas type into its own file. Adds `DrawingTool`, `Tool`, `ShowGrid`, `SnapToGrid`, `Pan`, `HasPendingGesture`, `CancelGesture()`, `Fit()`, `Undo()` and `Redo()`. Existing `Eraser`, `SelectedPart`, `SelectedBinding`, `Zoom`, `FinishStroke()` and pointer entry points remain compatible.
- `Assets/PaperDefense/Presentation/UI/DrawingWorkbenchUi.cs`: shared visual helpers and optional public `DrawingEditorContext`. Context exposes `Func<TowerBlueprint,TowerPreview> Preview`, `Func<WorkshopReadModel> Workshop`, and `DrawingAidPurchase Purchase` (`bool (string id, out string reason)`). Existing five-argument panel constructor remains valid; context is optional sixth argument.
- `Assets/PaperDefense/Presentation/UI/DrawingStencilBrowser.cs`: bounded vertical ScrollRect, reusable cards, vector previews, separate permanent unlock and insert actions. The current read model is requested afresh after every purchase because successful transactions may replace it.
- `Assets/PaperDefense/Presentation/UI/WorkshopHud.cs`: supplies RunController preview, read-model and unlock callbacks to the drawing panel.
- `Assets/PaperDefense/Tests/PlayMode/DrawingWorkbenchTests.cs`: dedicated interaction and transaction regressions.
- `Assets/PaperDefense/Tests/PlayMode/DrawingEditorFeedbackTests.cs`: intentional baseline expectations updated for the new visible sheet boundary and separate whole-tower/mount-stat blocks; historical rejection persistence checks remain intact.
- `docs/superpowers/plans/2026-10-14-drawing-ui-texts.json`: reviewed additions merged through the required extraction tool into `Tools/user-texts.json`, `Resources/Text/ru.asset` and `Core/UserTextContracts.Generated.cs`.

## Behavior

The panel occupies `(20,30,1560,940)` within the existing 1600×1000 reference board. The left side contains parts, six tools, undo/redo, mirror, width/height controls and clear. The middle contains an 808×607 sheet, grid/snap and zoom/fit controls. The right side contains capacity, resolved statistics, selected-part goal guidance and scrollable stencil thumbnails. Cancel and primary Apply remain separated in the footer. Grid and snapping share `DrawingCanvasGraphic.GridSpacing` (`.1` part-local units); rendered strokes enforce a 1.25 physical-pixel minimum to remain visible at the stand's .6 Canvas scale.

Pencil, line, rectangle, ellipse, eraser and move gestures keep pending geometry outside the saved snapshot. Release includes the final sample and commits one command. Erasing uses Core's swept-path operation. Move preview shows the original position faintly and turns red when the part would leave its bounds; Core rejects the command atomically. Switching tool/part/zoom, disabling, focus loss, pause and Escape discard pending input. Middle-button pan and scroll zoom affect only the view. Keyboard Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z use Input System and skip focused text inputs. PointerEventPolicy acquisition/release shares the existing reference-counted policy.

Ink overflow remains editable and turns the usage meter red. Both the Apply button and direct `Close(true)` require `IsUsable`, using the Application preview's FreeformDesign where available. All stat calculations come from the preview or Core design; UI contains no gameplay formulas. Actual selected mount damage per hit, range, rate and interval are displayed. A restrictive shared-feed multiplier is explained.

Unlocking never inserts a stencil and is not part of drawing cancellation/history. Inserting an owned stencil is free, replaces only the selected part and is undoable. Standalone panels expose initially unlocked stencils, disable paid purchases, and explain that a profile is required. Shop copy explains gold from completed waves and victory, with no gold earned from drawing/trials. Free basic body and barrel stencils provide an immediate beginner path.

## Validation ledger

- Root RED job `aa5729e5e41e440dbd03d0531976561c`: all four initial regressions failed for the expected reasons (dropped release endpoint, stray drag, interrupted gesture, overbudget direct apply).
- Expanded `DrawingWorkbenchTests`: 21 cases covering shape release/one-step history, atomic swept erase, tool/part/zoom/cancel lifecycle, move preview/rejection, pan/zoom/fit/snap, free beginner insertion and undo/cancel, failed purchase and successful retry, owned insertion, standalone purchase disabling, resolved stat refresh, visible region bounds/non-overlap, focus-loss/reopen cancellation and actual queued Input System history/input-field/Escape behavior.
- `python -m unittest Tools.test_extract_user_text Tools.tests.test_extract_user_text_diagnostics`: 12 passed after text merge.
- `git diff --check`: no whitespace errors; only existing CRLF notices.
- Root confirmed clean Unity compilation and focused GREEN job `5916e633a545404db3157e599bdf292e`: `DrawingWorkbenchTests` + `DrawingEditorFeedbackTests`, **32/32 passed**, zero failures/skips (includes all 21 new cases).
- Independent review, full suites and live screenshots are pending root execution. No completion claim is made for those checks.

### Live-font and independent-review follow-up

Root's live 1280×720 stand screenshot exposed clipping that the initial LegacyRuntime font fixture did not catch: the shipped NotebookHand line metrics are taller. Section headings, title, three-line statistics, four-line selected-mount/shared-feed statistics, gold explanation and keyboard shortcuts now have explicit adequate heights. The right column was reflowed without overlapping sections; stencil descriptions/cards were enlarged. Rejection and gesture guidance now share one feedback region with only one visible at a time.

All workbench fixtures now load the actual shipped `NotebookHand` resource and assert that it exists. Two new font-fit cases inspect every visible Text's preferred height at Canvas scales .6 and .8, including three mounts, restrictive shared feed, body/barrel/decoration guidance and stencil descriptions. A mesh regression checks physical grid/thumbnail stroke widths. Total workbench cases: **24**; combined focused count with historical feedback: **35**. Root confirmed clean compilation and **35/35 PlayMode passed**, zero failures/skips, job `f1d77287689c4acf9a025fbee8f988ef`.

Reviewer feedback also identified half-grid snapping and repeated linear ownership lookups. Snapping now matches the visible .1 grid. Stencil rights use a HashSet refreshed only when the read-model/catalog reference changes, calling the existing direct profile-rights query once per definition. The transaction test now proves cache invalidation after read-model replacement and persistent ownership across zoom/part changes.

Root also found a separate trial UI mesh overflow after launching a three-mount freeform trial. Static evidence: `TrialLaneSketch.OnPopulateMesh` renders the full `NotebookTrialRenderer` scene through one `MeshPen.CopyTo` into one VertexHelper; initial MeshPen capacity is not a vertex cap. No speculative rendering changes were made here; root owns isolation and the integration fix.

### Full-suite compatibility follow-up

Root reported full EditMode **818/818 passed**. Initial integrated full PlayMode job `9541c9a3d1c44045ba9238382b88a7d2` finished **344 passed / 4 failed / 348 total**. All four failures were traced:

- Two stand tests (`FocusedWorkshopSceneTests.DrawingSceneEditsThirdLayerAndFootprintWithoutAssemblyAlbumOrBattle` and `NotebookArtStandSceneTests.TowerSceneShowsFifteenSelectablePartsAndFiveValidCompositions`) directly mutate the public Session, then call `Canvas.FinishStroke()` as the existing refresh boundary. The new no-pending early return suppressed Changed, leaving Apply disabled. Restored the no-gesture Changed/redraw notification, without Edited, geometry or history mutations. A new dedicated regression checks two external geometry commands, refreshed Apply, exact notification counts and exactly two undo steps.
- `AuthoredWorkshopTests.LeavingViewportEndsStrokeAndOutsideEraserCannotChangeDrawing` assumed automatic drawing after re-entry without a new press. Renamed/revised it to enforce the authorized cancellation policy: unchanged empty snapshot/no history after leaving and re-entry; exact two-point stroke from a fresh press; one undo step; outside erasure changes neither geometry nor history.
- `TextResourceIntegrationTests.InstanceOverrideChangesHudNestedPanelsAndRealCoreReasonWithoutLeakingToOtherHud` still overrode the replaced old title key. Its fixture now overrides `drawing.workbench.title`; all sentinel, nested-panel, core-reason and independent-resolver isolation assertions remain unchanged.

Final workbench case count is **25**, or **36** with the historical feedback suite. Root execution of this compatibility follow-up is pending. No other production changes were made for these four failures.

## Intentional limitations

Outline vector tools, whole-stroke erasure and selected-part transforms are supported; raster painting, arbitrary colors and point-level selection are outside the authorized design. Fit restores the whole allowed drawing sheet. Parts outside the selected local coordinate frame remain faint contextual geometry. The workbench uses the project's existing legacy Text/font conventions; it does not introduce a second typography stack.
