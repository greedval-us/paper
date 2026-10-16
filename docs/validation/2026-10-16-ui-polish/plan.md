# UI repair scope — 2026-10-16

Requested: review and improve battle and workshop UI, retaining notebook styling.
Skills: Unity uGUI, Unity MCP UI Layout, UI/UX Pro Max (general focus visibility guidance only).

Runtime stack: code-generated uGUI, Unity 6000.5.7f1. Existing paper palette, ink drawings, fonts, and authored tower geometry remain the visual source of truth. No new art assets or mockups are needed for this bounded repair.

Intake: editor scene `Assets/PaperDefense/Scenes/Tests/27_PlayerAssets.unity`, clean, not playing, no selection, no active Canvas, no console errors/warnings. Visual evidence uses the existing memory-only Game View fixture.

Confirmed baseline defects:
- At 1024×768, the drawing work area ends at 500 logical pixels, but the summary begins at 893.6. The 393.6-pixel gap comes from a stale 780-pixel template height. Scroll content height is 1675.6.
- At 1280×720 and UI scale 150%, keyboard selection of Build tool 11 leaves the button outside the horizontal viewport and provides no visible focus outline.
- Battle context text uses a fixed height regardless of the blueprint name.

Bounded implementation:
1. WorkshopResponsiveLayout owns the outer scroll and positions the main area and summary. Derive drawing summary position and scroll extent from the reflowed child sizes; recalculate on stage/catalogue visibility changes. Keep the drawing session and history intact.
2. Retain the manual RectTransform layout used for the drawing canvas and its tool strips; compact short-page spacing to give the canvas more room. Avoid competing LayoutGroup/ContentSizeFitter owners.
3. Reveal focused controls through their containing ScrollRects, in either axis and from inner to outer. Add an ink focus mark to player controls; keep existing workshop focus visuals.
4. Measure battle context copy so wrapped names remain scrollable.

Verification: reproduce focus failure with PlayMode tests, regression-check workshop drawing/undo, inspect real Game View captures at 1280×720, 1024×768, 1920×1080 and UI scale 150%, long text, stencil catalogue, summary, and adjacent wizard stages. Restore the original editor scene and Game View. Report measured checks separately from visual inspection.
