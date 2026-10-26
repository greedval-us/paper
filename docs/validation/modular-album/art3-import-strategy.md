# Art 3 import strategy

Audit baseline: `77e0e4917d6e051c1ecb8f76f0305c662506bdac`, inspected 2026-10-24. This audit changed no product assets, generated no images, invoked no Unity operations, and left dependency changes and `New Unity Project/` untouched.

## Recommended replacement boundary

Keep all 54 production part IDs, definition revisions, categories, compatibility flags, socket schema/type labels, profile formats, balance data, authored logical mount positions, aim/recoil limits and existing valid recipes. Replace raster art and its measured visual bindings. Change the visual asset revision consistently; do not bump gameplay part revisions merely because drawings change.

`Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset` is authoritative. The current source manifest has 54 visual definitions, 77 fragment bindings and 57 distinct modern sprite crops. Two definitions (`armor.none`, `utility.none`) are intentionally neutral. Seven weapon mechanisms, ten heads, four frames, fourteen foundation choices in total and four structure families must remain covered. The complete atlas includes 140 crops and 431 aliases, so modern assembly art is only part of the live atlas.

All paths below are relative to `D:/Projekt Unity/paper`.

| Current native PNG under `ArtSource/Notebook/NewStyle/` | Native size | Modern use |
| --- | --- | --- |
| `StandardFoundation-v9.png` | 1536×1024 | standard frame and retained roof/front hull bands |
| `FullVariantParts-v1.png` | 1254×1254 | heavy/precision frames, alternate hulls, armor/front, feed/boost, turn/quick, optics, magazines, burst trigger; 14 crops |
| `BatteryFoundation-v3.png` | 1254×1254 | three-seat battery frame, top-left 627×627 author canvas |
| `AssemblyAccessories-v1.png` | 1536×1024 | armor, feed, iron sight, standard magazine and trigger; 5 modern crops plus historical bearing |
| `GunPilotParts-v1.png` | 1254×1254 | modern gun barrels and turn support; 2 modern crops plus historical cradle/collar |
| `FullWeaponParts-v1.png` | 1254×1254 | gun/piercer shared gearbox and six other mechanism/head pairs, short/long/wide heads; 15 crops |
| `FullStructureParts-v1.png` | 1254×1254 | wall, slow, generator sites/functions/roofs; repair toolbox; armor/flywheel; 12 crops |
| `RepairParts-v1.png` | 1254×1254 | repair site/function/roof and range utility; 4 crops |
| `FullSupportAttachments-v1.png` | 1254×1254 | light/reinforced shell panels and generator pump; 3 crops |

The seven mechanisms are gun, piercer, mortar, fan, ricochet, stapler and blotter. Structures are wall, slow, repair and generator; shared shell definitions have family-specific fragment bindings. A replacement of only `FullWeaponParts` leaves the modern gun barrel and shared turn hub in old art. A replacement of only `FullStructureParts` leaves repair and shell attachments in old art.

Historical and fallback visuals also remain referenced. `sprites.source.json` still includes `ModularSprites.png` (15 chassis/weapon/module crops), `ScenerySprites.png`, `PreparationTowers.png`, enemies, effects, interface and other illustrations. `assembly-parts.source.json` retains `foundationParts`, `foundations`, `weapons`, and `pilots` used by `ComposeLegacy`/`NotebookMountLayout`. Do not classify those PNGs as unused from `visualParts` alone. Replacing a shared modern/historical sheet requires rebinding and verifying both consumers.

## Native image editing capability and limits

The built-in image generator supports reference-guided editing and transparent output. Load each local edit target with `view_image` first, then use it as an edit target with the sketch/style reference separately identified. Ask for blue-ballpoint redraw, the existing object identity/order, transparent background, unchanged canvas/aspect, unchanged source cell occupancy, centers, socket pads, pivot/contact feet, muzzle exits, layer pieces and clear gutters. Save accepted native output as a new versioned PNG and record its actual dimensions and SHA256. Never infer actual landmarks from requested prompt coordinates.

This is a probabilistic redraw, not a deterministic style-transfer or registration operation. The exposed tool provides no exact-size/cell/pivot locking API. It can attempt layout preservation but cannot guarantee original dimensions, socket alignment, pixel positions, count or silhouette. Therefore a returned PNG is a candidate, never an automatic drop-in. Inspect actual alpha, cell separation, identity and landmarks before authoring bindings. Reject merged cells, clipped exits, extra parts or a geometry change that cannot fit the certified envelope; regenerate instead of masking, resampling, stretching or shrinking collision regions to hide it. Native output pixels can remain unchanged because the existing atlas importer copies integer RGBA rows.

For tightly registered multi-part sheets, editing the existing sheet best preserves context. If it repeatedly changes positions or tangles separate parts, generate smaller coherent groups or individual parts and author explicit source rectangles; uniform scale/position metadata accommodates native images without raster editing. This requires updating the hardcoded generator rather than assuming its 4×4 cell rules still apply.

## Authoring contract and coordinate conversion

`Tools/ArtAssembly13/build-catalog.mjs` currently regenerates `sprites.source.json`, `assembly-parts.source.json`, and `full-catalog-layout.json`. It contains source filenames, irregular cell gutters, anchors, dimensions, contact points, split bands, family mappings and placements. A manual edit to generated JSON alone is overwritten by the next build. Update the generator's authoring entries too, or deliberately move these inputs to an explicit declarative manifest consumed by the generator. Preserve the existing source definitions outside this replacement's scope.

`sprites.source.json` entries provide `id`, `source`, integer source-sheet `x/y/width/height`, and local `bounds`. Source rectangles use **bottom-left PNG coordinates**, not top-left screenshot coordinates. For a native sheet height H, crop left L/top T/width W/height Hc converts to `x=L, y=H-T-Hc`. The canvas inside a source cell is untrimmed. Its `trim.x/y` is relative to that full cell; `trim.width/height` must match the imported source crop. Alpha inspection selects padded native rectangles; alpha centers do not define mechanical anchors.

`assembly-parts.source.json.visualParts[].fragments[]` provides:

| Field | Meaning and safe adaptation |
| --- | --- |
| `canvas` | Full untrimmed cell/native object canvas; set to actual new canvas dimensions. |
| `trim` | Native crop rectangle on that canvas, inside canvas bounds; dimensions must match sprite crop. |
| `region` | Actual rendered/occupied rectangular band inside trim; keep complete real solid silhouettes and true occlusion split bands. |
| `attach` | Inspected mechanical anchor in full canvas pixels; never use crop center by default. |
| `position` | Anchor position in assembly units before aim/recoil. |
| `scale` | One positive uniform assembly-units-per-native-pixel factor. |
| `radians` | Authored rest orientation; keep mechanical forward axis consistent. |
| `mounts[].position` | Measured socket centers on frame canvas; transformed into certified logical mount positions. |
| `muzzle`, `hasMuzzle` | Real exit point on head canvas, exactly one muzzle-bearing fragment per head. |
| `hullBaseFraction`, `hullFrontFraction` | Actual contiguous frame base/roof/front split positions relative to trim height. |
| `motion`, `layer`, `collisionMode`, `family` | Fixed/rotating/recoiling behavior, draw order, physical region participation and family selection; retain semantics. |

For any canvas point q, rest geometry is `P(q) = position + R(radians) * ((q - attach) * scale)`. Root placement/aim and the existing recoil translation are applied later. A measured new anchor a' can use the same `position` to keep its world attachment fixed. A measured socket m' must satisfy this same transform against the baseline logical socket. Every real head exit must satisfy `position + R(radians) * ((muzzle - attach) * scale) = (0, positiveY)` within the importer's 1e-5 X tolerance.

If a complete source canvas changes by a uniform pixel factor k with identical composition, multiply canvas, trim, region, attach, mounts and muzzle coordinates by k, divide `scale` by k, and preserve `position`/`radians`. Source crop integer rounding and actual landmarks still need verification. If only sheet padding changes, update sheet crop offsets while keeping cell-relative geometry. If content is translated within its canvas, translate every pixel-space point/region consistently; anchor subtraction cancels translation. A nonuniform X/Y redraw cannot be exactly undone with this uniform-scale schema. Two frame seats can only retain both old positions if the new artwork admits one similarity transform; three battery seats additionally require congruent relative geometry. Reject or regenerate inconsistent pad geometry.

Current measured witnesses illustrate why a size-only edit is unsafe: standard frame canvas 1536×1024, trim `(17,290,1503,444)`, seats `(368,640)/(1163,640)`, scale `0.0006276353885315664`, position `(-0.4822571556263724,-0.43584730777451763)`, band edges native y384/y511. Battery uses canvas 627×627, crop `(32,187,564,192)`, seats `(168,292)/(312,344)/(459,290)`, scale `1.95/627`, position `(-.975,-.88)`. These are inspected baseline facts, not coordinates to blindly apply to a new redraw.

Do not change `fits.Parts` identity/revision/category/socket labels or production compatibility to accommodate art. `AssemblyVisualComposer.Catalog.cs::PrepareFullCatalog` derives frame keepouts and full weapon swept regions from these same visual transforms, including every compatible variant and all turn supports. The baseline uses clearance .01 and recoil .085. Existing valid saved recipes must stay physically valid; baseline has an intentional restriction: reinforced hull fails standard frame but has witnesses on battery/heavy/precision. Record and compare the validity matrix rather than demanding every option fit every frame or relaxing Core validation.

## Packing, shaders and publication

Actual importer: `Assets/PaperDefense/Editor/NotebookSpriteImportTools.cs`, menu `Paper Defense/Art/Rebuild blue pen atlas`. It validates source, animation, full production coverage, sockets, transforms and packing before publication. It copies unchanged source RGBA rows into the atlas and preserves resource GUIDs. It stages/replaces atlas/catalog/mounts together, reimports, updates only the visual theme asset, and resets theme/sprite/mount/composer caches. It does not invoke Node. Keep that transaction path.

Run the updated authoring generator, then `node Tools/ArtAssembly13/pack-catalog.mjs 0`. Despite its stale “read-only” header, the packing script **writes** `Tools/ArtAssembly13/packing-4096.json`. It packs native, non-rotated crops plus the 9×9 pen tile with genuine four-pixel gutters. Current bin is 4096×4352; importer accepts only width4096 and height4096/4352. A native output that will not fit requires a deliberate capacity change and matching importer/tests, or regeneration at a more suitable native composition. Do not silently downsample PNGs. Seed relocation requires unchanged seed rectangle dimensions and is not automatically applicable to newly sized crops.

Keep the current import settings: Default texture, NPOT None, maxTextureSize8192, mipmaps disabled, Clamp, Bilinear, Uncompressed, source alpha, alphaIsTransparency enabled. Transparent RGB dilation can occur during Unity import; byte identity applies to the generated atlas PNG, not Unity's internal imported texture pixels.

`Assets/PaperDefense/Presentation/Rendering/Resources/NotebookInk.shader` and `NotebookAtlasUi.shader` both average four mip-zero taps bounded inside the four-pixel gutters. UI uses premultiplied output; world uses regular source-alpha blending. Neither shader converts colored drawings into blue-ballpoint style. Correct the source art; preserve these filtering/alpha contracts unless a real rendering defect is demonstrated. `NotebookSpriteCatalog` UV rectangles must follow the new packing; do not create separate Unity Sprite pivots for this custom mesh pipeline.

## Batch implementation and checks

1. Snapshot/hash baseline source metadata, logical sockets, true muzzle lengths, geometry envelopes, gameplay catalog, recipe/profile schemas, and current valid recipe witnesses. Preserve user profile/drawing files and the 12 unrelated dependency modifications. Save accepted art candidates under new source filenames with prompt/reference/hash provenance.
2. Replace one representative low standard foundation and one complete weapon group first. Measure alpha and mechanical points; author full canvases, crops, anchors, true regions and split bands. Prove registration and readability before scaling the same style across the nine-sheet scope.
3. Complete four frames; seven mechanisms/ten heads; foundation/accessory variants; four structure families/shared shell/utility fragments. Include shared legacy consumers of pilot/accessory sheets. Keep neutral choices neutral.
4. Update authoring source, generate manifests, pack natively, then import through the existing transaction and cache reset. Avoid product mechanics changes. Do not publish an incomplete intermediate catalog to live Resources.
5. Run source and runtime fit audits; compare baseline validity, every starter, independent aim/recoil extremes, alpha contacts/occlusion, native RGBA copying and player/world/UI composition. Capture in real scenes and multiple final screen sizes, then explicitly review source/before/after. Existence/hash checks alone are not an art verdict.
6. Delete only reference-proven unused obsolete artifacts after import, tests and reviewed captures. Keep approved source concepts and historical evidence unless deletion is explicitly intended. Commit regenerated metadata/packing/resources and evidence as one coherent change.

Read-only commands actually run in this audit (Node v24.14.1):

```text
node Tools/ArtAssembly13/audit-catalog.mjs --source-fit --variants
node Tools/ArtAssembly13/audit-catalog.mjs --runtime --variants
node Tools/ArtAssembly13/contact-audit.mjs --probes
```

Both fit audits: 54 definitions, 140 crops, 28 standard frame/family configurations, 56 placed mounts, zero collisions/problems. Fourteen foundation choices have actual valid witnesses. Reinforced hull has 77/98 valid baseline configurations; the other hulls have 98/98. Contact probes locate receiver feet at local y=-.08525 and report sight-foot receiver alpha252/253. Probes are measurements, not a complete pass/fail suite. No Unity test pass is claimed by this audit.

Meaningful Unity verification after replacement:

- `NotebookNativeAtlasTests`: every copied source pixel, native dimensions, four-pixel separation, no mipmaps, correct imported size.
- `AssemblyArtCoverageTests`, `AssemblyArtImportValidationTests`: all54 mappings, stable sockets/revisions, real muzzle anchors, invalid inputs rejected before publication, fit coverage.
- `AssemblyFoundationVariantArtTests`: actual frame base/roof/front retained under selected hull, facade attachment/visibility, visible optics/trigger after full draw order.
- `AssemblyArtSilhouetteTests`: weapon/base readability, receiver/hub/sight contacts, battery three-seat silhouette, repair eave/foot contact, slow bowl/grate seating. Several assertions name old source coordinates/IDs; update only assertions tied to intentionally replaced inspected art and retain their physical intent.
- `AssemblyFitTests`, `AssemblyWorldViewFitTests`, `AssemblyWorldArtTests`, `AssemblyModernAdapterTests`, `AssemblyPilotAdapterTests`: independent full motion envelopes, fixed support collisions, same recipe transforms/muzzles across UI/world/legacy adapters.
- Existing profile persistence/snapshot/resolver/transaction tests and catalog count checks: verify the art-only change leaves saved recipe identities and gameplay values intact. Test actual frozen recipes without saving or migrating user data.

Old `build-fidelity-gallery.mjs` hardcodes visual revision v7 and previous acceptance files; `verify-art2.mjs` verifies old hashed evidence and writes an old verification report. Neither is a generic Art3 gate. Produce Art3 evidence/revisions without overwriting historical review claims or treating stale hashes as fresh acceptance.

## Deletion reference check

Build the retained graph from all `sprites.source.json` sprite sources and alias targets, `assembly-parts.source.json` visualParts/foundationParts/pilots/weapons, `animation.json` bindings, production rendering IDs, tests, generation/packing scripts, provenance and validation capture sidecars. A PNG is still an import dependency whenever any source crop references it, even if no modern fragment uses it. A sprite ID can be reached through any of431 aliases.

For each proposed obsolete file, search both its repository-relative path and basename with `rg -n --fixed-strings` over `Assets`, `ArtSource`, `Tools/ArtAssembly13`, relevant `docs`, and project configuration, excluding dependency/vendor directories. For an asset under Assets, read its `.meta` GUID and search that literal across serialized scenes/prefabs/assets/materials; also check code `Resources.Load` paths and assembly/animation IDs. Rebuild the graph after deleting stale manifest entries and regenerate packing before considering removal. Delete an Assets artifact together with its `.meta` only after zero retained references; keep GUIDs of replaced runtime resources. Shared modern/historical sheets currently listed above are not deletion candidates. Do not touch user data, `Tools/BalanceWorkbook/node_modules`, `New Unity Project/`, unrelated assets or historical evidence merely to reduce duplicate filenames.
