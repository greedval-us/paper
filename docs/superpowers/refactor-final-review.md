# Independent final code review

Reviewed on 2026-10-12 against baseline `5ab2424285855fb4fd0747797cb79f0e8f0fb8b9`. Scope: all 17 changed production C# files and all 12 new C# files under `Assets/PaperDefense`, including tests and the Editor evidence helper. Read the user specification, plan, rules report and prior independent drawing review. This reviewer performed no Unity operations and changed only this report.

## Verdict

**PASS. No actionable P0–P3 production issue found.** The implementation preserves the examined behavior while centralizing part metadata, drawing bindings, format limits, immediate-shot publication, scenario conditions and the direct-tool price formula. The added input validation and corrected HUD price are the explicit defect fixes.

## Findings and compatibility

- `BalanceValidation` covers all current scalar float tuning fields, all definition floats, enum identity, catalog completeness/uniqueness, rule references and wave structure. It does not normalize wave fractions or remove the historical runtime clamps. Empty waves and zero count/speed/damage/build duration remain accepted. Asset adapters retain finite-value clamping, omitted-array defaults, repeated-setting last-wins order and target-preset precedence; nonfinite source values now fail before clamps disguise them.
- The new part catalog retains the 13 exact IDs, order, names, descriptions, five initial rights and version-two membership. Configured unlock prices still come from `TowerConstructionRules`; historical adapters retain the original default values. Profile loading keeps its unknown/future-part handling. Mount/module order and labels in the workshop remain equivalent to the old lists. No manual transaction or reward persistence code changed.
- `TowerCatalog.ToolCost` copies the old payment expression, including int/float multiplication and `Math.Ceiling`. Assembly pricing is not rewritten or merged into that expression. The HUD now consumes this payment rule through `GameBalanceView`. The regression test fixes a concrete Mono rounding discrepancy and checks quote, label and actual debit.
- `WeaponSystem` retains hit application, statistics, shot list append and event publication order. Mortar launch/impact and blot cleanup remain distinct; range extraction selects the same validated structure definition and same expression. Trial fixture constants retain their literal types and values, iteration order, random draws, exit test, frame recording and counterfire order. Dynamic reload without a target and shared acquisition were not changed.
- Drawing policy and limits agree with the earlier independent PASS. The final HUD updates also address that review's observation about remaining four-slot array/loop literals. Existing serialized types/fields, configuration assets and tracked `.meta` files have no changes. Core gains no Unity or IO dependency.
- `GameSession` continues borrowing the supplied mutable balance; this patch does not silently introduce ownership or a deep copy. Asset adaptation creates fresh data and trials snapshot their input. Documentation must preserve that distinction, which the coordinator confirmed it is doing.

## Evidence checked

- Saved final EditMode result: **405 passed, 0 failed, 0 skipped**.
- Saved final PlayMode result: **174 passed, 0 failed, 0 skipped**.
- All 28 before/after trial fingerprint lines compare equal.
- New publication tests contain independent literal damage/count/end-position expectations and verify paired shot/event representations; they do not derive expectations from the extracted helper.
- Catalog tests pin IDs, order, initial rights, configured/default price distinction, numeric enum contracts and observed Mono payment. Drawing tests pin pre-extraction hashes and independent point/stroke expectations.
- `git diff --check` passed. Existing balance assets, persistence coordinator/store and tracked `.meta` files have no diff.

## Review limits and nonblocking follow-up evidence

The saved tests and trial outputs were inspected, not independently rerun. Full campaign/pressure replay, final performance results, isolated visual acceptance and development build remain coordinator acceptance evidence; this code review does not claim completion of those activities. Range-validation tests sample the important malformed-input categories rather than every field/range, so this PASS also relies on direct source inspection of the validation lists.

The review noticed that the Editor helper originally shared an unsynchronized reflection-field dictionary between asynchronous replay workers. The coordinator added `lock (fields)`; the revised source was inspected. The helper also now marks whether Mono's allocation counter works, avoiding interpreting unavailable zero values as zero allocation. Neither issue affects production gameplay; no outstanding finding remains.

## Documentation and measurement-script follow-up

Reviewed `docs/Extending-Mechanics.md`, `docs/PaperDefense-Architecture.md`, `docs/Balance-Parameters.md`, `Tools/Run-FullAlbumPressure.ps1` and the evolving `docs/validation/verification-core-refactor-2026-10-12.md`. **PASS for factual source consistency and script safety; no new actionable code finding.** No Unity operations or measurements were run by this reviewer.

The documentation accurately distinguishes fresh asset adaptation, borrowed direct-session balance and trial snapshots. Catalog responsibilities, profile compatibility, the separate int/float tool-price path, long/double assembly pricing, interval order, trial conditions and the manual/reward IO distinction agree with current source. The extension guide acknowledges fixed UI capacity and explicit handlers/masks/format obligations rather than promising automatic support for arbitrary new mechanics.

The PowerShell change adds a selectable executable and validated report tag, preserving isolated pressure/memory-only arguments, hidden launch, collision refusal and successful exit/report/MemoryOnly checks. Its 30-second polling retains the existing eleven-minute timeout policy and introduces no deletion or process termination.

The acceptance report explicitly discloses the initial failed isolation attempt that read the saved profile, and distinguishes the repeated temporary-scene acceptance from it. This reviewer inspected the written disclosure; it does not independently attest to those live UI actions. The report also correctly treats unavailable Editor allocation counters as unavailable and unmatched timing runs as insufficient evidence of a speed change.

At follow-up review time, warm/Player metrics paragraphs were still pending replacement by the coordinator. Final acceptance must contain completed results instead of those placeholders. The reported original/new Player CPU totals imply increases of about 5.8% at x1 and 11.3% at x2; ambient-load attribution requires the planned old-build control and must remain qualified if evidence cannot separate it. No performance speedup or absence of regression is established by this documentation review.

## Final rendering predicate follow-up

Inspected the exact `DoodleGlyphs.cs` / `WorkshopHud.cs` diff after the final cleanup. **PASS.** `UsesTemplateMount` explicitly selects Fan, Ricochet, Stapler and Blotter, exactly the set previously selected by the ordinal comparison plus catalog membership. Both world and album rendering use this same Presentation helper. All geometry, pivots, scales, aim transforms and numeric values are unchanged. Future catalog entries no longer silently select this renderer merely by enum order.

The existing four `ExpandedMountRenderingTests` cases verify the selected mount's vertex count and each transformed vertex against template geometry within `.0001f`; they cover the affected world-rendering branch. Scoped `git diff --check` passed. Subsequently inspected saved `docs/validation/refactor-2026-10-12-final-play-rendering.json`: **174 passed, 0 failed, 0 skipped**, 9.482 seconds after the predicate cleanup. Inspected `docs/validation/refactor-2026-10-12-final-build.json`: **StandaloneWindows64 build succeeded**, 9.485 seconds, 198.51 MB, **0 errors and 0 warnings**. These are coordinator-produced results, not independent reruns. Core sources remain unchanged since the 405-test EditMode result and exact replay comparisons.

The coordinator subsequently reported an old-build control of 3580.764ms/x1 and 3543.695ms/x2 against the first new-build 3731.388ms/3855.484ms, approximately +4.2%/+8.8%. This remains measured CPU growth to disclose, without assigning a cause or claiming a frame-rate change. Further final-build results should be retained alongside these measurements rather than replacing unfavorable samples.

The final script adds optional `[ValidateSet(1, 2)][int[]]$Speeds = @(1, 2)` and iterates that array, preserving the default two-speed run and all isolation/checks. This source change was inspected. The coordinator's final rebuilt-Player x2 smoke was still running at this review update and is not claimed complete here; the coordinator owns its final acceptance.
