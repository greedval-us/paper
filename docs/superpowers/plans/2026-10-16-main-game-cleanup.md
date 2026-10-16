# Main game cleanup implementation plan

> **For agentic workers:** Execute the authorized cleanup in this session and verify it with Unity. Track completed steps below.

**Goal:** Leave the player game as the only game scene, remove development stands and their launch paths, and preserve the existing sketches for the next development stage.

**Architecture:** Keep `PlayerGameRoot` and the shared gameplay, workshop and rendering layers. Remove scene-specific harnesses; retain useful automated regression scenarios in an Editor-only assembly.

**Tech Stack:** Unity 6000.5.7f1, C#, Unity Test Framework.

**Spec:** User request in this chat: remove/disable test scenes, clean their code, retain the main game and the sketches already in the project.

## Constraints and review focus

- Preserve `ArtSource`, `docs/design`, runtime art, main scene and its balance assets; compare against the saved SHA256 baseline.
- Preserve the gameplay workshop and trial range, profile persistence, core mechanics and automated regression coverage.
- Remove test scene routes, generators, Resources catalogs and command-line test launch hooks.
- Keep exactly one enabled build scene: `Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity`.
- Check compilation, serialized references, EditMode coverage and player journey tests; leave the main scene open.

## Tasks

- [x] Audit main scene dependencies and test-only files, switch the Editor from `27_PlayerAssets` to the main game.
- [x] Delete 27 stands, the hub, legacy technical scene, acceptance scene and scene-only scripts/configuration/prefab. Remove stand-only presentation branches, redirect the old Open Game menu, and isolate retained validation helpers from player builds.
- [x] Update tests and current launch documentation. Run Unity compilation, all EditMode/PlayMode tests, preservation checks and an independent review.

## Decisions

- Work directly in the connected Unity project so the cleanup is visible in the user's Editor; no separate checkout or commit is required.
- Retain acceptance data and pure numeric/pressure/ink-loop scenarios as automated Editor-only fixtures. Delete their standalone runtime entry points.
- Keep historical reports as historical evidence; current launch documentation must describe the cleaned project.
