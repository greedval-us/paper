# Variant composition review

Current result: **PASS for the v5 variant contact sheets**. Independently reopened all six fresh 1920 × 1080 pages after the v5 rebuild, including every full composition and its adjacent detail. The complete frame roof/front now remain beneath light/reinforced hulls, the heavy mount sits inside its plate, and the precision feed meets the actual front facade. Standard and boosted feeds are visible on the facade; the boosted belt differs from the stock box. Scope and tracking optics now expose attached shapes beside the receiver, and the burst controller projects a distinct larger silhouette on the opposite side. Drum and belt remain connected and visible. Seven weapon families, alternate heads, all four support roles, shell/utility variants and both explicit neutral labels remain readable.

All 54 definitions also found a real numeric and physical fixture; 52 selected raster PartIds occur in the complete compositions, and the two neutral options have none. The semantic/geometry CSV corroborates the inspected drawings; the PASS above is based on the actual images, not the PartId/vertex counts. This is the six-page native variant gate, separate from the workshop's five-size screen audit.

The following is the **historical v4 failure record**, captured at 06:42 on 2026-10-19. These defects are resolved in the v5 images reviewed above.

The v4 set failed visual acceptance:

| Definitions / page | Actual pixel defect |
| --- | --- |
| `hull.light`, `hull.reinforced` / 1 | The weapon floats above and left of the replacement walls. The walls sit separately in the middle of the frame; they do not form the platform beneath the selected weapon. |
| `frame.heavy` / 1 | The weapon sits on the outer edge of the large authored roof disk. |
| `frame.precision` / 1 | The socket requires a precise disk-center recheck. A separate tiny feed fragment appears below and left of the foundation. |
| `feed.standard`, `feed.boost` / 2 and full foundations on 1 | Their complete standard assemblies look identical; the defining feed box is covered. On the heavy/precision fixtures it instead appears as a disconnected island. |
| `sight.scope`, `sight.tracking` / 4 | Neither the full assembly nor the enlarged complete weapon shows a recognizable optical/tracking attachment. They look essentially identical to `sight.iron` despite containing the selected PartId. |
| `trigger.burst`, `trigger.standard` / 5 | The defining trigger attachment is not distinguishable on the enlarged real receiver. A standalone part icon would not establish that the full composition is correct. |

The seven weapon families and their alternate heads were already recognizable. The drum and belt appeared beside and in contact with the receiver. All four support families, shell variants, armor/range/economy utilities and both labelled neutral options were recognizable. The v5 correction was made in the shared authored tree; the capture harness did not replace missing parts, move vertices or weaken fit rules.
