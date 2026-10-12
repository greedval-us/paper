# Drawing policy and profile limit refactor

Baseline: `codex/full-album-loop`, HEAD `5ab2424`. The initial audit was read-only and the checkout was clean. The coordinating agent ran the original suites successfully (351 EditMode and 167 PlayMode tests), then the new characterization cases successfully before authorizing production edits. No Unity operations, player profile IO, commits or pushes were performed by this subtask.

## Changed files

| File | Change |
| --- | --- |
| `Assets/PaperDefense/Core/DrawingPartPolicy.cs` and its new `.meta` | Pure shared `IsEnabled` and `ShouldRetain` rules; one optional drawing-part to module binding switch. |
| `Assets/PaperDefense/Core/DrawingTemplates.cs` | Existing `DrawingTemplateCatalog.IsEnabled` API delegates to the shared policy. Contours, pivots and enabled-part iteration order stay unchanged. |
| `Assets/PaperDefense/Core/TowerBlueprint.cs` | `WithAssembly` uses the retention policy; `ChassisKind` explicitly remains Standard=0/Heavy=1; ID/name validation uses profile limits. Catalog formulas were outside this subtask. |
| `Assets/PaperDefense/Core/DrawingEditSession.cs` | Assembly replacement uses the same retention predicate against the session's unsaved snapshot. The duplicate local switch was removed. |
| `Assets/PaperDefense/Core/ProfileLimits.cs` and its new `.meta` | One definition of 64 album pages, 4 slots, 64-character blueprint IDs and 48-character names, independent of balance. |
| `Assets/PaperDefense/Core/WorkshopProfile.cs` | Limit checks, slot allocation/iteration and corresponding messages use the shared limits. This file was released to the rules agent before its independent catalog changes. |
| `Assets/PaperDefense/Presentation/UI/DrawingEditorPanel.cs` | Point counter and pending-stroke cap use `DrawingSnapshot.MaxPoints`, preserving the current 2048 limit and text. |
| `Assets/PaperDefense/Tests/EditMode/DrawingPolicyContractTests.cs` and its new `.meta` | 22 characterization cases covering both assembly entrypoints, independent pre-refactor geometry hashes, unsaved session strokes, dormant data, origins, activation bindings, numeric contracts and limit boundaries. |

## Preserved behavior and compatibility

- Exact assembly no-ops keep the existing page/snapshot and do not add editor history. Legacy rename/no-op keeps its origin; changing a legacy assembly creates an empty authored draft.
- A changed chassis or mount clears only that part. Decoration survives. Optional module lines survive only when the same module exists before and after.
- Dormant serialized parts remain lossless on load and exact no-op. A real assembly edit retains the original intersection rule, including clearing dormant lines when their module is newly installed.
- Editor replacement operates on `DrawingEditSession.Snapshot`, not `Blueprint.Drawing`; unsaved strokes survive applicable changes. Undo/redo still restores assembly and geometry together.
- Template/render iteration order is Base, Mount0, Mount1, Armor, Feed, Tracking, Rail, Decoration. The UI's independent existing button order was not changed.
- Existing serialized types, namespaces, fields, metadata/format/binding versions and existing `.meta` GUIDs were not changed. `StructureKind`, `DrawingPartKey`, `DrawingOrigin` and `TowerModules` values remain unchanged. The dense 0–7 part index contract and module bits are characterized explicitly.
- Profile structural validation remains independent of current capacity/balance. Shared limits preserve all previous values and current user-visible limit messages.
- Existing immutable DTO-to-model mappings stay in their current assembly. The audit found no useful migration that would justify moving or renaming public persistence types.
- `WorkshopProfileStore` remains the profile IO boundary. Manual commands still publish a candidate only after successful persistence. Earned rewards still advance their in-memory cursor before IO and remain dirty on failure; retry does not award again. These intentionally different transactions were not unified.

## Verification evidence

The original Core compiled successfully in a standalone PowerShell `Add-Type` process before production edits. That original implementation produced these fixture hashes, now stored as literal expectations in the characterization tests:

| Fixture change | Original hash |
| --- | --- |
| All eight marked parts | `7e13935604a832fe` |
| Chassis | `a06d66b6d7fd7891` |
| First mount | `92b6bb72a4ceb3db` |
| Second mount change/removal | `f0ef550f27e73105` |
| Armor removal | `4f184c1b7ba8f7ff` |
| Feed removal | `bd1ffbfff8865229` |
| Tracking removal | `e583295cebaec9ab` |
| Rail removal | `899949a009d7430f` |

`git diff --check` completed with no whitespace errors after the production patch. Git emitted only its existing LF/CRLF normalization notices. A separate standalone NUnit compilation attempt hit PowerShell/.NET facade reference incompatibility; it did not execute tests and is not a Unity failure. The coordinating agent owns Unity compilation, targeted runs and complete post-refactor regression; their final results supersede this interim report.

Existing relevant integration coverage includes `WorkshopDrawingPersistenceTests`, `WorkshopStoreTests` and `WorkshopSaveFailureTests`: complete album, exact historical originals, future data protection, working copies, atomic/manual failures, dirty rewards and retry.

## Separate behavior task: rejected stroke message

The existing disappearing rejection message is still present and was deliberately not fixed in this refactor. Code-path reproduction: fill a page to 128 strokes, draw a 129th visible stroke and release the pointer. `DrawingCanvasGraphic.FinishStroke` fires `Rejected` after `AddStroke` rejects the command, then unconditionally fires `Changed`; the panel's `Changed → Refresh` callback immediately replaces the rejection with quality text. The draft and history correctly stay unchanged.

Relevant code: `DrawingEditorPanel` subscribes `Canvas.Changed += Refresh` and `Canvas.Rejected += ...`; `DrawingCanvasGraphic.FinishStroke` raises those events in that order. A separate future PlayMode/UI task should reproduce the pointer flow and decide how long the rejection remains visible. No pointer/UI reproduction was run by this subtask, and no behavior fix or new UX policy was included.
