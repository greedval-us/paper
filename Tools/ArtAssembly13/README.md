# Production modular art integration

The current source and runtime catalog revision is `modular-full-catalog-2026-10-24-v8`: 54 production part definitions, 141 native raster crops and 431 aliases, for 572 runtime sprite entries. Modern parts use 10 source PNGs, including 51 `part.full.*` crops. The actual atlas PNG is 4096×4352 (68 MiB RGBA), with four-pixel gutters and a separate 9×9 pen tile. Counts and dimensions describe the data; they are not a test or visual acceptance verdict.

The three approved sketch sheets are the art authority: [towers and foundations](../../docs/design/album-assembly-2026-10-17/art/01-towers.png), [weapon parts](../../docs/design/album-assembly-2026-10-17/art/02-weapon-parts.png), and [structures](../../docs/design/album-assembly-2026-10-17/art/03-structures.png). Preserve their family silhouettes, materials and blue ballpoint hatching on warm ivory surfaces. Full concept pages are references, never modular sprite inputs.

## Sources and rebuild

`build-catalog.mjs` reads all 54 definitions from `PlayerAssemblyCatalog.asset`; it does not change balance values or profile data. From the repository root:

```powershell
node Tools/ArtAssembly13/build-catalog.mjs
node Tools/ArtAssembly13/pack-catalog.mjs 0
```

Then use **Paper Defense → Art → Rebuild blue pen atlas** in Unity. Keep generated source JSON and `packing-4096.json` together. The Editor reads the declarative packing layout and does not invoke Node.

Current replacement sources are `StandardFoundation-v10.png` (1536×1024), `BatteryFoundation-v4.png` (1254×1254), `AssemblyAccessories-v2.png` (1536×1024), and the separate clean `TurnHub-v1.png` support. The standard has two seats; the battery has three seats on its top-left 627-pixel cell. The clean hub replaces the unwanted old plate footprint rather than concealing it. The v8 hub import is present in source and runtime catalogs as `part.full.turn.standard`; geometry checks and fresh captures remain separate from successful import. Previous images do not establish its acceptance.

`FullWeaponParts-v1`, `FullVariantParts-v1`, `FullStructureParts-v1`, `RepairParts-v1` and `FullSupportAttachments-v1` supply other modern families and variants. The gun and accessory sources also participate in historical composition. `armor.none` and `utility.none` deliberately have no raster fragments.

`full-catalog-layout.json` records the full-catalog sheets and exact native crops. `sprites.source.json` also includes shared and compatibility artwork outside that subset. `assembly-parts.source.json` holds the modern 54-part mapping and independent historical foundation/pilot mapping. Direct `Resources/Art/Player` images are separate resources; rebuilding the atlas does not recreate them.

The decoder inspects alpha to select padded crops and never writes source PNG pixels. Attach, pivot and muzzle points are authored separately on full canvases. `inspect-alpha.mjs <PNG> [...]` reports connected alpha bounds; connected components do not determine anchors. Exact prompts, reference roles and selected outputs belong in adjacent provenance records. Do not copy clearance measurements or verdicts from superseded v9/v3 sources into the current revision.

## Shared composition and profiles

`AssemblyVisualComposer.Compose`, `ComposeFoundation`, `ComposeWeapon` and `ComposePart` use the same authored fragments, transforms and layers as world/trial rendering. For seven known `ui.weapon.*` families, `NotebookArtGraphic.Bind` without an animation clip constructs the family mechanism/head with standard sight, magazine and trigger, then renders `ComposeWeapon` with `AssemblyPreviewPose.ForWeapon()`. Family icons and modern recipe previews share component art and registration; selected recipe previews also show upgrades. Layout scale and pose can differ between views.

Legacy textures remain reachable because `ProfileMigrationV8` retains old pages as `Legacy` when it cannot prove equivalent recipe conversion. Their blueprint geometry, historical pilot adapter and compatible `tower.*` illustrations remain supported. Replacing modern parts or known weapon icons does not retire every `ModularSprites`, `PreparationTowers` or `ScenerySprites` consumer. The scenery sheet also contains live terrain. Preserve required IDs and existing-profile behavior until those independent paths have replacements.

The bounded cleanup ledger is [art3-retirement-plan.json](../../docs/validation/modular-album/art3-retirement-plan.json). It records exact obsolete paths, hashes, baseline recovery blobs and deletion gates; it is a plan, not proof of deletion. Preserve reference sketches, provenance and historical screenshots. Absence from `full-catalog-layout.json` alone does not establish that an asset is unused.

## Validation

`pack-catalog.mjs 0` searches deterministic non-rotating MaxRects heuristics. It writes `packing-4096.json` only when every crop and the pen tile fit with genuine four-pixel gutters. Fallback relocation can use immutable `packing-baseline-v5.json` or `packing-baseline-v3.json`; unchanged seed rectangles must retain dimensions, valid bounds and padded separation.

The Editor validates IDs, order, native dimensions, bounds, count and separation before publication. It copies original RGBA rows without resampling or rotation and preserves resource GUIDs. Whole-atlas mipmaps stay disabled. Unity's transparent RGB dilation is an importer operation; byte-for-byte preservation refers to the generated atlas PNG. `NotebookNativeAtlasTests` checks native pixels and imported dimensions; mentioning the test does not assert a current PASS.

Import checks part identity, revision, category, semantic socket labels, schema and collision modes against the production catalog before deriving runtime fit data. Unknown revisions and unsupported combinations fail instead of silently altering saved definitions.

```powershell
node Tools/ArtAssembly13/audit-catalog.mjs --source-fit
node Tools/ArtAssembly13/audit-catalog.mjs --runtime
node Tools/ArtAssembly13/audit-catalog.mjs --source-fit --variants
node Tools/ArtAssembly13/contact-audit.mjs --probes
```

The audits examine authored mounts, transformed solid bands, fixed supports, swept weapon circles, neighbouring mounts and component choices. Variant checks seek an actual fit witness rather than assuming every hull fits the standard frame. Contact probes read current source alpha without changing pixels or metadata. Geometry checks do not certify artistic fidelity to the sketches.

`build-fidelity-gallery.mjs --require-complete --require-reviewed` validates the historical 30×5 evidence matrix and explicit reviewer records. A changed source/capture hash or revision invalidates old visual proof. Historical gallery reports and Unity results apply to their recorded revision only. Current acceptance requires matching final captures, manual inspection and actual test output. Image existence, source metadata and this README supply no visual PASS verdict.
