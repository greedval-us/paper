# Drawing UI independent review

Scope: current UI delta in `docs/validation/2026-10-14-drawing/ui-review.patch.txt`, compared with the pre-UI baseline. Read-only reviewer; no Unity operations. Focused evidence: 32/32 PlayMode passed, job `5916e633a545404db3157e599bdf292e`.

Strengths: pending gestures remain outside committed geometry, atomic history commands, resolved design allowance, current read-model purchase queries, separate permanent unlock and undoable insertion.

Findings:

- P2: one-reference-unit grid/border/thumbnail lines become 0.6 physical pixels at the supported stand scale. Rasterization drops portions of the grid and thumbnails. Enforce a physical-pixel minimum.
- P2: text blocks truncate with the actual NotebookHand font. Title, headings, final stats/feed lines and gold-source explanation do not fit. Allocate runtime-font line height and test real text fit, not just rectangle overlap.
- P3: snap spacing .05 differs from visible grid .1. Share spacing or render the intermediate lines.
- Scalability recommendation: stencil ownership refresh repeatedly scans the catalog and becomes quadratic; cache against current read-model/catalog identity. No measured performance failure.

Declined to judge: Core balance/persistence already separately reviewed; unrelated pre-existing dirty changes outside UI delta; raster fill/color/point editing explicitly outside this increment.

Initial verdict: with fixes. No additional confident P1/P2 input, transaction or history issue. Root reproduced visual defects at 1280×720 and requested corrections with runtime-font regressions.

Scoped recheck: all source fixes approved. Shared GridSpacing=.1; minimum1.25physical pixels; ownership HashSet invalidated on catalog/read-model replacement. Runtime-font text tests35/35 passed, job `f1d77287689c4acf9a025fbee8f988ef`. Root final live screenshot `docs/validation/2026-10-14-drawing/drawing-workbench.png` confirms the previously missing labels/grid are visible. UI review complete; separate trial mesh integration fix remains outside this review.
