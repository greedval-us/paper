# Drawing workbench validation

Unity 6000.5.7f1, Windows Editor. Dates use the task's 2026-10-14 context. No Player build has been produced. The working tree includes earlier uncommitted freeform implementation.

## Final result

Final source passed818/818EditMode (`e224f18f42b74365ae355fcb278da052`,26.5876686s) and349/349PlayMode (`ea9c474a3c744cfa9a5c9e713636f7e9`,83.1751148s),0failed/0skipped. Focused compatibility54/54 and batching3/3 also passed. Python text pipeline12/12; whitespace check clean;15newmeta GUIDs valid. Independent final integration review has no open findings.

Final appearance: `drawing-workbench.png`. Live trial: `trial-batched.png`. Unity restored to clean MechanicsHub, not playing/compiling; no scene or project-settings files changed. Three expected console exceptions from negative TextResourceIntegrationTests matched their LogAssert expectations; these test diagnostics were cleared after verification. No errors occurred during final live UI/trial validation.

## Completed evidence

- Core full EditMode: 818/818 passed, 0 failed/skipped, job `d55b13520f194437b4172223734a374e`.
- Core full PlayMode before one fixture correction: 320/321; the stored bootstrap profile needed the newly granted initial stencil rights. Corrected preparation preserves the original assertions. Targeted bootstrap 15/15 passed, job `eccd406c98d94df88dea5a5366c115dc`.
- Workbench and existing feedback focused PlayMode: 32/32 passed, 0 failed/skipped, job `5916e633a545404db3157e599bdf292e`. Includes 21 new cases.
- Text pipeline Python suites: 12/12 passed.
- Working diff whitespace check: passed (line-ending notices only).
- Main-content balance probe: `balance-probe.json`; reproduced in `../../Drawing-Workbench.md`.

## Live integration

Used the isolated memory-profile scene `Assets/PaperDefense/Scenes/Tests/02_Drawing.unity`, 1280×720, HUD scale 0.6. Inserted the free body and three free barrel stencils through button callbacks. Result: functional ink 93.38951/135, usable. Applied drawing, ran normal `RunDrawingTrial()`: report created, draft geometry hash `e7763f5945753b28` retained. The stencil ScrollRect wheel event moved its normalized position from 1 to 0.6299558 and exposed other cards.

The first live pass found cropped runtime-font text and subpixel grid lines; both were fixed and independently re-reviewed. Focused35/35 tests passed (`f1d77287689c4acf9a025fbee8f988ef`), including actual NotebookHand font fit at0.6/0.8scale. Root inspected `drawing-workbench.png` at1280×720: title, headings, all stats/feed/source lines and full grid are visible. `workbench-live.png` and `workbench-before-fit-fix.png` are diagnostic captures, not final appearance. `workbench-empty.png` is a camera-only fallback capture and does not establish UI appearance.

The trial then logged Unity UI's65,000-vertex mesh limit. Root isolated the source by populating each lane's geometry for sampled recording frames. At23.333334s: Blueprint lane87,616vertices/157enemies/1tower; Budget lane80,666vertices/110enemies/10towers. Reusable batching fixed this without dropping geometry: the dedicated RED reproduced131,120vertices; GREEN verified identical triangle stream and lifecycle. Root scanned all119+112recorded frames: each batch≤64,998vertices, at most2activebatches perlane, no consoleerrors. Returned to MechanicsHub and stopped Play; user profile was not touched.
