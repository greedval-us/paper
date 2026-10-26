# Workshop visual review

## Current v5 audit

The final native v5 screenshots have been inspected individually at 1280 × 720, 1280 × 960, 1600 × 900 and 1920 × 1080. All non-trial workshop states pass at those sizes: 06, 07 (head, drum and belt), 11–14, 25, 26 (failure and successful retry), 27, 29 (all four support roles) and 30 (foundation parts and library). The illustrations are complete and connected, the comparison figures and role-specific information are legible, and the warning/retry and incompatibility controls occupy their own regions.

The 1024 × 768 audit found a real clipping defect in positive cost/load contributions on the narrow left cards. The source now uses the compact localized caption `Постр. {0} штр. · нагр. {1}` below 280 layout units; both signed values and the full right-side description are retained. The fresh 1024 screenshots were reopened: all affected states 06, 07, 11, 14, 27 and 29 now pass, including drum/belt and all four support extras. All 55 canonical non-trial workshop cells therefore pass their five-size pixel audit.

The screenshot harness now selects one common elapsed time after both reports' first real shot/launch, with action start only as a fallback. Reopening all five synchronized focus/overview pairs exposed one final real camera defect: the lower lane's primary victim body/health bar crossed the focus's left edge. Its recorded position was inside the focus, but its drawn envelope extended beyond the fixed padding. The meaningful Gun/Mortar regression failed twice in `bbeeb6fb8cae4f91ae255baffb12a30c`; the source now measures the actual body, marks, HP and corresponding death pose through `MeshPen.TryGetBounds` and includes that envelope. Wall/Slow use the same measurement for their already selected real enemy; Repair/Generator remain unchanged. All 25 trial UI cases passed in `f191677584ac4cc484d18a86dd28cd84`.

The final five focus/overview pairs were then reopened individually: screen 10 passes at all sizes. Both selected shooters and actual targets are recognizable and wholly inside their respective fields, including the lower target's complete HP bar; the overview keeps the recorded copies. Unrelated copies can cross a close-up edge as expected. No simulation coordinates, budgets, report values, playback clocks or shared mesh scales changed.

The final capture also changed 15 non-trial images (twelve 1920 workshop states plus purchase at three sizes); each was reopened and still passes. The other screenshots remained byte-identical to the already inspected images. `task13-verdicts-workshop.json` therefore records **60/60 PASS** for the twelve owned canonical screens at five sizes, aggregating the drum/belt, retry, support-role, foundation/library and overview extras into their screen verdicts.

The reviews below are historical defect records, not current failures.

## First 1280 × 720 capture

Reviewed actual Task 13 screenshots in `screenshots/` against concept screens 04–08, 10–14, 25–27, 29 and 30. This is a defect review, not visual acceptance. The first capture does **not** pass the sketch-fidelity gate.

| Screen / state | Observed result | Required correction |
| --- | --- | --- |
| 04 — foundation | Foundation cards show a narrow base strip; the main preview already includes a weapon. | Compose the complete foundation in both places; reserve more card area for the illustration. |
| 05 — weapons | Seven native illustrations exist, but their roughly 40 px silhouettes are hard to distinguish. | Give the drawings more vertical space and recheck after the catalog joint/weapon-scale correction. |
| 06 — assembly | Complete assembly and live statistics are readable. The page is still sparse relative to the sketch. | Retain the real recipe and add the available authored scenery around it without obscuring controls. |
| 07 — barrel comparison | Current and candidate parts, actual resolved arrows and purchase state are legible. | Keep the prospective resolver and immutable draft. Magazine inspection requires a separate visible firing-cycle comparison. |
| 08 — review | Large assembly, name and save controls are visible; the composition uses its own scroll region. | Recheck the composition viewport after the illustration/layout changes. |
| 10 — two-page trial | Real synchronized reports exist, but the towers occupy only about 12–16 px and most of each field is empty. | Improve the presentation scale/focus using actual report positions; field-height assertions alone are insufficient. |
| 11 — locked part | Unlock conditions and prospective comparison are readable. | Preserve the separate inspect, purchase and install actions. |
| 12 — mortar | Resolved minimum range and impact radius are shown; the fixed diagram and weapon are too small for the sketch composition. | Increase the diagram's usable area and keep its values tied to the resolver. |
| 13 — fan | Real cone angle and target count are shown. The selected fan may be outside the catalog viewport in this capture. | Reveal the selected row and increase the fixed diagram's usable area. |
| 14 — armor | General and frontal defense are separate and readable. | Retain this distinction during layout updates. |
| 25 — purchase dialog | The 94 → 14 transaction preview is legible. An unrelated “Отмена” tooltip appears below the modal. | Hide transient tooltips in the capture harness before taking the screenshot. |
| 26 — profile failure | Retry text is present, but the failed save is painted green because the draft itself remains valid. | Use warning color for the last failed command independently of draft validity. |
| 26 — retry | Successful retry is visible. | Verify exactly one saved entry and one successful durable write remain true. |
| 27 — incompatible parts | The diagnostic paragraph overlaps the native trigger illustration. | Reserve a diagnostic region before sizing the illustration; do not overlay the warning on the drawing. |
| 29 — generator | Native assembly and income/payback/cap information are readable. | Recheck after shared art correction. |
| 29 — wall | Native assembly and wall-specific role information are readable. | Recheck after shared art correction. |
| 29 — slow | The grate visibly floats above its pedestal. | Catalog/composer owner must repair and validate the authored joint. |
| 29 — repair | Site, body and roof are visibly separated. | Catalog/composer owner must repair and validate all authored joints. |
| 30 — foundation parts | The component preview includes a weapon although the page edits a foundation. | Preview only the complete foundation; individual part cards retain their distinct source geometry. |
| 30 — library | The saved foundation uses its complete composition and readable card text. | Retain the real library contents; do not invent entries to fill empty space. |

The shared catalog owner confirmed that `ComposePart(frame)` deliberately represents only the frame's base strip. Complete foundation previews must use `ComposeFoundation`; the hull owns the remaining wall and roof layers. Slow/repair joint corrections and relative weapon scale belong to that owner's source/composer validation work.

After correction, capture all required states at all five supported sizes and review the pixels again. Passing semantic/layout tests is necessary but does not establish visual fidelity.

Source correction checkpoint: after observed UI RED `0a6addeef8614e75b4402ca124ebfdac` (five missing magazine-cycle regions and one out-of-view equipped fan), the UI now uses a dedicated current/candidate cycle, repeats the selected-row reveal on final reflow, previews complete foundations and standalone weapons, enlarges the seven weapon cards, reserves a diagnostic area and colors failed saves as warnings. Mortar/fan now receive a 65% field with statistics and a compact variant list on the right. These changes remain **implemented_unverified** until the fresh test run and pixel review. The additional drum/belt screenshots in the capture harness specifically cover the inspection defect.

Second pixel review (v2 / 4K source, 1280 × 720): all 23 available workshop states were opened. The large standalone component previews and head/magazine comparisons are readable. Screen 26 uses a warning color and screen 27 no longer overlays its diagnostic on the trigger drawing. Repair/slow have connected visible layers. Remaining material UI defects were: purchased Long head scrolled out of view after purchase; duplicated failure text with a generic Save caption; variant availability touching the selected-card underline; the stock mortar circle extending about 7 px into the heading. Source fixes now reveal after purchase, use one distinct warning card and an explicit retry caption, reserve card label space and fit the mortar diagram at one fixed world scale. Those fixes require fresh screenshots. The redundant Cancel tooltip came from a catalog-owned sibling overlay under the Canvas; the capture harness now hides overlays across that Canvas.

Screen 10 still fails in this v2 capture because towers remain tiny. Root owns a subsequent shared whole-assembly world transform; the UI must not enlarge weapon parts separately. Its new output and all five final sizes must be reviewed before writing the final workshop verdict JSON.

Third pixel review (v3 source / shared world scale 4, fresh 1280 × 720 captures): 06, 07 (head, drum and belt), 11, 12, 13, 14, 25, 26 (failure and successful retry), 27, 29 (all four roles) and 30 (parts and library) pass the workshop readability/clipping quick gate. Purchased Long remains visible; the magazine capacity/interval/reload arrows and the actual drum/belt assembly pieces are visible. The mortar circle stays below the title, the fan's eight targets and variant availability are unobstructed, and no stray tooltip is present. The failed save has one distinct warning card and an explicit retry action.

Screen 10 remains **FAIL** in both focus and overview: the shared transform now makes individual weapons recognizable, but adjacent actual copies overlap into continuous foundation strips. The recorded fixture uses 2-unit spacing while one transformed standard foundation is about 3.88 units wide. The UI includes the shared transformed silhouette bounds and does not clip the outermost copies. Correcting the inter-copy collision requires an agreed real fixture/world-scale change; rearranging figures only in the drawing would misrepresent the recording. Final all-size acceptance remains pending.

Subsequent shared stable whole-tree fit removed the scale-4 collisions without changing placements. The 06:28 close-up still showed weapons at roughly 30–40 px because it included all actual copies. After observed RED, the default camera now frames the first real engagement; supports use recorded effects/beneficiaries and the wall's closest recorded approach. The complete overview retains all copies and spatial events. Trial regression tests passed in `450f110980034cfc8c58ca113e9cfbe0`; this is not a pixel PASS. A fresh 1280 gate and the five-size audit remain required.
