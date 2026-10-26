# Workshop composition pass — 2026-10-19

This is a new comparison against the concept sketches, separate from the earlier functional/readability verdicts. The new presentation is awaiting Unity compilation and fresh captures; it is not yet a visual acceptance result.

The source and the preceding 1280×720 capture of every owned scene were visually inspected. The largest differences were a repeated generic catalogue layout, small upright drawings, unstructured statistics, and missing exploded diagrams. The following changes use the live draft, the numerical resolver, and individual native part compositions.

| Scene | Change | Presentation owner |
| --- | --- | --- |
| 06 | A large exploded drawing of the actual selected weapon parts with dashed assembly callouts; separate characteristic rows. | `ComponentCatalogPanel.Sketch.cs`, `WorkshopAssemblyDiagram.cs` |
| 07 | A wide pair of angled native head drawings, a compact variant strip below, and a framed comparison at the right. | `ComponentCatalogPanel.cs`, `ComponentCatalogPanel.Sketch.cs` |
| 10 | Larger real report values and visible row/column rules; more visible existing scenery. The parent owns the whole-world reflection and field decoration. | `CompositeWorkshopPanel.Trial.cs` |
| 11 | Three tall native part cards, with the inspected candidate and unlock details in a separate right column. | `ComponentCatalogPanel.cs`, `ComponentCatalogPanel.Sketch.cs` |
| 12 | Two tall variant cards at the left, an independent central mortar field, and a right characteristic column. | `ComponentCatalogPanel.cs`, `ComponentCatalogPanel.Sketch.cs` |
| 13 | A more visible hatched sector, larger target illustrations, and an angle taken from the resolved weapon. | `ComponentCatalogPanel.cs`, `ComponentCatalogPanel.Sketch.cs` |
| 14 | The actual foundation above a row of three armour variants; a separate direction illustration and real comparison rows on the right. No damage result or hit is invented. | `ComponentCatalogPanel.cs`, `ComponentCatalogPanel.Sketch.cs`, `WorkshopAssemblyDiagram.cs` |
| 29 | Exploded selected structure parts and a separate table of real support capabilities. | `ComponentCatalogPanel.Sketch.cs`, `WorkshopAssemblyDiagram.cs` |
| 30 | Exploded selected foundation parts and foundation-only health, capacity, mounts, feed and price. The price calls the shared `AssemblyResolver.FoundationCost` fold. | `ComponentCatalogPanel.cs`, `ComponentCatalogPanel.Sketch.cs`, `WorkshopAssemblyDiagram.cs` |

Category tabs and characteristic rows now have consistent vector icons. Individual previews use the shared static preview pose; actual trial recordings retain their original simulation coordinates, clock, budget and reports. The common typeface, page skin, initial recipe poses and scenes 04/05/08 are owned by the parent/catalogue agent.

Verification pending: foundation quote regressions previously failed for the missing API and the displayed whole-tower price. The parent supplied the shared API, and this pass supplies the UI call. The first new capture should check rotated drawing edges, tall card metadata, the mortar column separation, armour table height, and whether the new exploded drawings have enough visual weight.
