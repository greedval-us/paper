# Combat readiness and drawing rejection fixes

User authorized these three behavior changes on 2026-10-12. Starting commit: `1c3bdc8` (clean checkout).

## Contracts

- Dynamic reload progresses during targetless simulation time, including wave gaps, and clamps at one ready action. Pausing does not progress it.
- A mount consumes shared production while reloading or while it has an acquired actionable target. A ready idle mount releases its share. Drawing quality, feed modules and configured intervals continue to apply once.
- Each combat mount acquires its own actual target within its own range. Retargeting, death or lost range restarts only that mount. Tracking applies independently. Support cleanup has no combat acquisition delay. Revalidate after the earlier barrel fires or cleans a blot.
- A rejected stroke leaves the drawing/history intact. Its explanation survives redraws and non-editing controls, beside quality feedback, until an actual successful edit or opening another session.

## Work and verification

1. Reproduce with focused failing Core and live Unity panel tests.
2. Separate reload progression from action launch; replace shared acquisition with per-mount runtime timers in Core. Cover wave gaps through GameSession without firing during preparation.
3. Keep persistent feedback and successful-edit notifications in Presentation; no UI rules in Core.
4. Migrate geometry/color fixtures away from shared acquisition bypasses while preserving their original assertions.
5. Run focused regressions, full EditMode/PlayMode suites, independent review and Windows player compilation. Record results in a new validation report; historical refactor fingerprints describe the previous behavior and remain untouched.

Root owns Core and Unity execution. A parallel agent owns drawing UI and its tests. Validation uses isolated fixtures and memory-only workshop state, without player-profile IO.
