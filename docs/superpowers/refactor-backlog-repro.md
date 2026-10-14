# Separate behavior decisions confirmed during the refactor

**Follow-up authorized on 2026-10-12:** all three decisions below are now implemented by the [combat and drawing fixes plan](plans/2026-10-12-combat-drawing-fixes.md). Reload completes without targets up to one ready action, each barrel acquires independently, and rejection remains until a successful edit/new session. The observations and local harness below remain historical evidence of the previous implementation; the current regression fixtures are `MountReadinessTests`, `BlotterWeaponTests.BlotterReloadFinishesWhileItsSheetIsClean` and `DrawingEditorFeedbackTests`.

These are follow-up tasks, not fixes included in the behavior-preserving refactor. The observations describe existing behavior; the intended product rules remain decisions for a separate change.

## Reproduction harness

The local evidence harness is retained under ignored `Temp` in this working checkout; it is not part of the portable source tree. The task steps and observed values below are the permanent reproduction record. From this project checkout, run a fresh PowerShell process:

```powershell
pwsh -NoProfile -File Temp/refactor-backlog-repro.ps1
```

The harness compiles the production `Assets/PaperDefense/Core/*.cs` sources with `Temp/refactor-backlog-repro.cs` into an isolated process. It does not open Unity, write Assets, or access a player profile. The drawing probe extracts the current production `DrawingCanvasGraphic.FinishStroke` and `DrawingEditorPanel.Refresh` methods unchanged into an event replay with inert UI stubs; it is not a live Unity rendering test. It additionally creates `Temp/refactor-backlog-ui-replay.cs` as generated evidence.

Observed on 2026-10-12: command exited 0 and all three probes passed their assertions.

## Task 1: Decide whether dynamic feed reload advances without a target

Relevant production flow: `WeaponSystem.FireMount` returns when its per-mount target is absent, before `TryBeginAction` updates normalized action progress. `GameSession.Step` separately decrements ordinary cooldowns only when dynamic feed is disabled.

Reproduce:

1. Enable `GameBalance.Construction.DynamicFeed` and create a complete single-Gun tower with an acquired, durable nearby enemy.
2. Call `WeaponSystem.Fire` for `1/30` second so it fires once and sets `PrimaryActionRemaining` to 1.
3. Remove the enemy. Advance 300 more `1/30` second calls, totaling 10 seconds.
4. Restore an enemy and seed `TargetId` to its ID so the separate acquisition delay does not obscure the reload observation. Advance one more `1/30` second call.

Observed:

```text
RELOAD: firstLaunches=1; remainingAfterShot=1; targetlessSeconds=10; remainingAfterIdle=1; remainingAfterReturn=0.920635; launchesAfterReturn=1
```

The partially completed reload does not progress during targetless time. Without the isolated acquired-target setup, reacquisition may delay the return further. The existing `WaitingWithoutTargetsCannotAccumulateAnUnboundedVolley` test starts with ready mounts and therefore does not establish the rule for a reload that has already begun.

Design decision required: should dynamic production freeze without a valid target, finish the current reload up to one ready action, or use another explicitly bounded policy? Preserve the current freeze until that decision is made. A separate implementation should test an initial shot followed by a long targetless interval, as well as absence of an accumulated volley.

## Task 2: Decide whether acquisition belongs to the tower or each barrel

Relevant production flow: `WeaponSystem.Fire` chooses one acquisition candidate over the maximum combat range of both mounts, updates one `TargetId`/`AcquisitionRemaining`, and passes the same `acquired` flag to both `FireMount` calls. Each mount still chooses its own target within its own range.

Reproduce:

1. Enable dynamic feed. Give a complete Heavy tower a Gun with range 2, a Mortar with range 5, and `TargetPriority.Strongest`.
2. Place enemy 1 at distance 1 with 100000 health and enemy 2 at distance 4 with 200000 health. Seed the shared acquired target to enemy 2.
3. Confirm a ready-action control fires both weapons: the Gun targets enemy 1 and the Mortar targets enemy 2. Reset both normalized action remaining values and both cooldowns to zero to isolate acquisition from reload.
4. Add enemy 3 at distance 4.5 with 300000 health. Advance `0.01` second.

Observed:

```text
ACQUISITION: controlLaunchesGun=2; controlLaunchesMortar=2; targetsBefore=1,2; targetsAfter=1,3; sharedTargetAfter=3; sharedAcquisitionRemaining=0.24; launchesAfterChangeGun=2; launchesAfterChangeMortar=2; readyActionsAfterChange=0,0
```

The Gun retains enemy 1 as its per-mount target. The Mortar changes from enemy 2 to enemy 3. Both otherwise-ready mounts are prevented from firing by the new shared acquisition delay. The control confirms that both can fire when the shared candidate stays stable.

Design decision required: is retargeting intended to stall the whole tower, or should an unchanged barrel retain acquisition while another retargets? Preserve shared acquisition until that decision is made. A separate implementation should test distinct ranges, unchanged per-mount targets, target priorities, and the tracking module.

## Task 3: Decide how long rejected-stroke feedback should remain visible

Relevant production flow: the editor subscribes `Canvas.Rejected` to write the message label and `Canvas.Changed` to `Refresh`. `DrawingCanvasGraphic.FinishStroke` invokes `Rejected` when `AddStroke` fails, then immediately invokes `Changed`; `Refresh` unconditionally replaces the same label with quality and part feedback.

Reproduce in the isolated source-extracted event replay:

1. Create an authored single-Gun draft and add 128 valid two-point strokes to its Base part.
2. Wire the same production `Rejected` and `Changed` handlers, adding observers after them.
3. Queue another two-point Base stroke and execute the unchanged production `FinishStroke` method.

Observed:

```text
Rejected: На странице помещается не более 128 штрихов. (Parameter 'strokes')
Changed: Качество 0/100 · 256/2048 точек | Основание: контур 1 ещё не завершён.
strokesAfter=128; pointsAfter=256; drawingHashUnchanged=true
```

Both callbacks happen synchronously in the same `FinishStroke` call. The rejected stroke is correctly absent from the snapshot, but its explanation is overwritten before a rendered frame could display that label value. The exception's parameter suffix depends on the runtime. A live editor reproduction is to reach the page's stroke/point limit and release another valid stroke; this harness confirms the event and label ordering without using the running Unity editor.

Design decision required: should rejection feedback persist until the next successful edit, until an explicit action, or for a timed interval? Preserve the current event order during this refactor. A separate implementation should test the final message after rejection, unchanged drawing/history, and normal refresh after the chosen dismissal condition.
