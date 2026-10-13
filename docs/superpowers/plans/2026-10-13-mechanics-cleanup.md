# Mechanics cleanup implementation plan

> **For agentic workers:** Use the existing delegated audits and verification-before-completion; continue the authorized cleanup in this session.

**Goal:** Keep the working game mechanics, remove verified obsolete implementation remnants, and synchronize current documentation and the balance workbook.

**Architecture:** Preserve the production path `PaperDefense.unity → NotebookGame.prefab → PaperDefenseBootstrap → RunController` and its data presets. Remove only unreachable helpers, write-only state, completed one-time migration tools and duplicate generated resources. Preserve stable IDs, profile migrations, historical fixtures and dated evidence.

**Tech stack:** Unity 6000.5.7f1, C#, Unity Test Runner, Python, bundled Node artifact-tool.

**Spec:** User request in this chat: audit all mechanics, remove leftovers of old changes, keep working mechanics, update documentation.

## Constraints and review focus

- Preserve the pre-existing user edit to `paper.slnx`; work in the current checkout connected to the live Unity editor.
- Keep migration support for profile versions 1–5 into v6 and legacy drawing formats.
- Preserve independent state, drawing bindings and footprints for all three weapon mounts.
- Preserve the historical balance and replay tools used by regression and acceptance fixtures.
- Remove scene/resource references together with obsolete assets; check all remaining GUIDs and documentation links.
- Existing baseline: 780 EditMode and 296 PlayMode tests passed before edits.

## Tasks

- [x] Audit Core/Application, Presentation/scenes, and documentation independently; verify candidate removals against references and serialization.
- [x] Remove obsolete ExpandedCatalogAssetTools and its sole test, unused Core/rendering helpers, and unused two-mount copy parameters.
- [x] Replace prototype scene regeneration with opening the saved production scene; verify the production scene configuration and existing integration tests.
- [x] Remove the unused SampleScene and its build/default-scene references, merged text fragment manifests, and tracked Python bytecode; ignore future bytecode.
- [x] Update README, current architecture/contracts/guides and a complete current mechanics register; regenerate the workbook from Unity.
- [x] Run complete EditMode and PlayMode suites, tool tests, reference checks and a Windows build; record fresh evidence and obtain an independent diff review.

## Decisions

`LegacyContentCatalog`, enum adapters, old master gates and historical balance assets remain necessary for compatibility and fixtures. Historical reports remain dated evidence. No gameplay balance or mechanic is redesigned by this cleanup.

No artificial tests are added for unreachable helpers or the removed scene-regeneration path: existing integration coverage and scene/reference audits check preservation without temporarily removing production assets.
