# Combat and presentation consistency

Scope: the requested refactor of responsibility boundaries, graphics, balance,
asset accuracy and mechanic interactions. Preserve the existing game and save
formats; correct demonstrated inconsistencies rather than inventing new rules.

Baseline: clean working tree; Unity 6000.5.7f1; 866/866 EditMode tests passed.

1. Reproduce damage-over-time/defense expiry inconsistencies with independent
   timing expectations. Keep status chronology in Core and make large/small
   advances agree, including historical scalar statuses.
2. Reproduce mortar replay footprint and custom palette errors. Extract shared
   effect/footprint presentation from NotebookWorldView, and use it in replay.
   Captured geometry determines the visible area, orientation and size.
3. Exercise the actual PlayerRules asset in campaign balance checks, alongside
   the existing Target asset. Verify finite income, affordable opening and eight
   waves with ordinary construction commands.
4. Run focused regressions, complete EditMode/PlayMode suites, inspect Unity
   visuals and console, and document results and remaining limitations.

The status correction affects defense-sensitive DOT. No unsupported numerical
balance changes or changes to saved player profiles are required.

Completed: status and rendering regressions reproduced and fixed; captured attack
metadata added; both balance assets passed; stale 26-scene checks repaired to cover
the full 27-scene catalog. Final verification: 880/880 EditMode, 388/388 PlayMode,
visual inspection, clean compiler console, independent review. Evidence:
`docs/validation/2026-10-14-combat-consistency/report.md`.
