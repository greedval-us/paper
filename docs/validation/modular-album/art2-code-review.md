# Art2 code review

**No blocking findings** in the reviewed snapshot against `c4d89c533a00c8f8e3fb9e3a3755c3a7afb73228`.

Reviewer: `/root/art2_battle_audit`. Scope: every current product diff under `Assets/PaperDefense`, the new `NotebookPaperArt.cs`, both new PNG/import settings, and the accompanying regression diffs. Read-only source review; Unity was not used. This review does not certify pixel identity or replace final runtime verification.

Reviewer ownership: this reviewer originally implemented the four terrain islands and the Single tracer change. They received a second source reading here with separate root-operated regression evidence; the remaining product changes were authored by other executors.

## Checks and conclusions

- **Sprite/cache lifetime and cost:** `NotebookPaperArt` caches one generated Sprite, reuses the imported Texture2D, destroys its previous Sprite on replacement and resets it at `SubsystemRegistration`. It performs no texture pixel reads/copies and creates no material clones. The cache is populated on screen construction rather than an Update loop. Native card dimensions are 1086×1448; the display region `(24,48,1038,1328)` and 96-pixel slice borders remain inside that texture. Its decorative Image has `raycastTarget=false`, is stretched behind content, and callers retain their procedural fallback if the native asset is missing or has unexpected dimensions. New textures are non-readable, without mipmaps; HelpLandscapeV2 is 1672×941. No unbounded runtime cache or per-frame allocation loop was introduced.

- **Part scale, pivot and family:** diagrams retain `ComposePart` source fragments and fit only their presentation transform from `VisibleBounds`; no trims, physical sockets, collider regions, recoil, atlas crops or authoritative component stats were changed. Final `ComposePart(reference, family)` filters family-specific fragments using the active structure recipe. The original overload retains its previous default selection. `WorkshopAssemblyDiagram` now passes `recipe.Structure.Family`; the added Slow/Repair/Generator regression compares the resulting fragment identity and transforms with the completed recipe. Canonical part order matches caption/slot order (foundation 5, weapon 5, structure 4), and neutral options retain empty artwork.

- **Latest help fit:** the native open-book Image is fitted first; the child real assembly now uses the fitted book rectangle's width/height, avoiding the previous taller illustration slot. The last shell fit23 change was reread in the final source snapshot.

- **Map and balance boundary:** both serialized production assets now contain exactly 20 added lines for four Pine props, with no other semantic diff. The canonical open-terrain preset adds the same four 2×3 footprints; the legacy fixed-road recipe is unchanged. The previous 25×25 clearing is still exhaustively checked with only those exact 24 blocked cells allowed, plus open core/corridor and production build/entrance/well checks. No wave, cost, health, range, income or other numeric balance edit remains in the YAML diff.

- **Single and specialist effects:** only `AttackKind.Single` takes the new bounded travelling segment. Length is clamped to the actual distance, division is guarded, and its endpoints stay on the supplied actual-muzzle→captured-target path. The impact resource still uses the captured target. The original Line/Ricochet/Cone/Radial/Cleanup branches, widths, colours and event/damage handling remain unchanged. The tests exercise the real Draw geometry, three ages, progressive movement, and full specialist endpoints.

- **Recovery modal hit areas and state:** the custom paper/mark use `WorkshopUi.Graphic`, which disables their raycast targets. The full overlay still blocks the covered view; the original retry/continue button rectangles, save-variant closure, dismiss flag and `WorkshopModal` focus/restoration flow remain intact. Paper and caption geometry introduce no new control or hit area. New modal artwork remains separate from the profile transaction code.

- **Other presentation changes:** hatching and native target illustrations stay inside read-only mechanic diagrams; resolved radius/cone and assembly anchor selection remain authoritative. Result embellishments are Graphic mesh generation on rebuild, not new simulation/update work. The result album icon resolves to an existing native atlas symbol; no duplicate `icon.book` atlas child is created because that identifier is absent from the actual catalogue.

## Verification and limits

`git diff --check -- Assets/PaperDefense` returned exit 0 (only normal LF→CRLF notices). Native PNG headers and importer settings were inspected directly. Final runtime tests, renderer import and interaction evidence are owned by root; this reviewer did not call Unity or independently run its profiler/test runner.

The latest structure-family fix and help fit23 were read after the initial review. All paths below were SHA-256 snapshotted after those updates. A subsequent product edit invalidates this review for the changed file until it is reread. The visual review is separately recorded in `art2-battle-visual-review.json`; concept residuals remain explicit there.

## Reviewed snapshot

| Path | SHA-256 |
| --- | --- |
| `Assets/PaperDefense/Configuration/Content/Target.asset` | `70f6614a173a55e60e03d66222bbdfe1fbc8a79c3757b4f2ef1a1a0c51f36db0` |
| `Assets/PaperDefense/Configuration/Game/PlayerRules.asset` | `dcaf91b459e03bf7642ff5c3908815a2ec485f509cf3dafef988867758a43f7d` |
| `Assets/PaperDefense/Core/NotebookTowerDefensePreset.cs` | `85864f39346d0da2ce7f991a8bf8d44ad5dc236dc29009cd7bf0cffb0c29a926` |
| `Assets/PaperDefense/Presentation/Art/AssemblyVisualComposer.Catalog.cs` | `f3398447daf564ea6723dba84f8e871284a0efb13a79f392234195ffdccf3226` |
| `Assets/PaperDefense/Presentation/Player/NotebookPaperArt.cs` | `e0ab9327bbeae550a33759e40499c97e3282744902547aeb6978780e660c8e3d` |
| `Assets/PaperDefense/Presentation/Player/NotebookPaperArt.cs.meta` | `5b307c24b96e0017f7f80d12297a78d2f798e873c913de33552b2c3d61a78844` |
| `Assets/PaperDefense/Presentation/Player/PlayerShell.Dialogs.cs` | `1c2cc831d4bcfeb257f56e139fdc76d4aa2a60d2c10a1e7abc4fb669a12b0c75` |
| `Assets/PaperDefense/Presentation/Player/PlayerShell.Result.cs` | `f21051d4aa7592e61a131ef662cd1510a584ee71f44e4d27c0bfa05dfc88b507` |
| `Assets/PaperDefense/Presentation/Player/PlayerShell.Settings.cs` | `852bed135a6c87b3a7bb8a5df709429f9f6a0c713518d758262e34c70482f9d1` |
| `Assets/PaperDefense/Presentation/Rendering/NotebookEffectRenderer.cs` | `2f68c54f0b435ad73cc309bca7c6fe9b8782ff12a6c93884c75f6215b6ed88dc` |
| `Assets/PaperDefense/Presentation/UI/ComponentCatalogPanel.Sketch.cs` | `5c6ef139836b088bdb2442e31f0ff567d4b3a17dc687f61cdae3ba4bb1aaeb05` |
| `Assets/PaperDefense/Presentation/UI/ComponentCatalogPanel.cs` | `3ba8ccd72496df9b319a70e51cee56109709b5b23ab6f51062f64fa9c6573be5` |
| `Assets/PaperDefense/Presentation/UI/CompositeWorkshopPanel.Review.cs` | `c30a88920e3f9705ee95500beafe6c20706de5df66e8c6b8ddf28e15342fd9b8` |
| `Assets/PaperDefense/Presentation/UI/CompositeWorkshopPanel.cs` | `93d4114cfa6d4ee50d0dd387a9985ebd7a513af948ed665fcf170aa43206fb29` |
| `Assets/PaperDefense/Presentation/UI/WorkshopAssemblyDiagram.cs` | `c65e46dd63eaadbc57854d899280f4187e4de65fb8910a83b1d8d20f4631023a` |
| `Assets/PaperDefense/Resources/Art/Player/HelpLandscapeV2.png` | `9c88fc7d630cf918e128340b877f3e144aceb64e0f6dba82f09d10e8c293c9e5` |
| `Assets/PaperDefense/Resources/Art/Player/HelpLandscapeV2.png.meta` | `3f6a82e934686f6738975d402ddcba483d12b6ea9586ac9f863d46fd8ddaeb6c` |
| `Assets/PaperDefense/Resources/Art/Player/NotebookPaperCardV1.png` | `b5afc593f0ffec864012f60b95102c15891a709ed28a1bd06061b2620e29a41e` |
| `Assets/PaperDefense/Resources/Art/Player/NotebookPaperCardV1.png.meta` | `28f97e04ef01a2ee4dad4e30cc452dd131c30a97b87861723702198eb95064d2` |
| `Assets/PaperDefense/Tests/EditMode/TowerDefenseRulesTests.cs` | `5fb42ebb83647a4fc4e9c642d8a949840b86df1bfbc72817e6a98e2425f83840` |
| `Assets/PaperDefense/Tests/Editor/AssemblyArtCoverageTests.cs` | `f07b6b3305a151eb242a58e4e2acf451eb69edb77d6e3e632ed1f019476632d0` |
| `Assets/PaperDefense/Tests/Editor/TowerDefenseBalanceTests.cs` | `c7acad05f310c7712aabdf6b276d0595aa3c26530682070b53b188dda69a4878` |
| `Assets/PaperDefense/Tests/PlayMode/NotebookAnimationRenderingTests.cs` | `e388fa851670407178240c3d2b65c81a784d66c8997424c37e039cb5e47df35f` |
