# Task 13: final shell visual review

Reviewed the actual final PNGs for canonical IDs 09, 15, 16, 17, 18 and 28 at all five required sizes: 1024×768, 1280×720, 1920×1080, 1280×960 and 1600×900. Also reviewed overview, generator inspection and repair inspection at each size. The source concepts were read directly from `docs/design/game-concept-art-2026-10-16/screens`.

Result: 30 canonical cells PASS and 15 extra cells PASS. The machine-readable fragments are `task13-verdicts-shell.json` and `task13-verdicts-shell-extras.json`. Static shell IDs are covered separately by the catalog reviewer.

The final captures use the v5 catalog, modern camera focus 6 with overview 1, actual valid construction cells at radius 3.5, the stable whole-assembly envelope of at most 2 world units, and the uniform modern enemy factor of 0.7. The fixture places its ring through real quote/build commands; the renderer preserves those recorded actor positions. View transforms leave production simulation rules, saved recipes, rewards and well cells unchanged. Source composition is adapted into an explicit Focus/Overview pair; this review does not claim pixel identity with a painted concept.

The final battlefield separates gun, double gun, piercer and mortar heads visibly, keeps the full assembly and castle silhouettes apart, retains live firing and range marks, and leaves all four approach indicators clear. The scenery is split into corner and side clusters with gaps. Planning shows a real 28-second countdown and its instruction is legible on an opaque paper badge, clear of the overview button, approach indicator and deck. Assault retires that initial instruction and shows the real wave/enemy count. The album prices remain visible at 1024×768 after the compact localized caption fix.

The tower inspector shows the exact frozen double-gun composition and actual HP, load, magazines, next shot and range. Generator inspection shows the actual full-wave eligibility, income and return; repair shows its actual rate and radius. Pause and leave confirmation remain legible over the dimmed real battlefield, with the complete consequence text and actions inside their panels.

One explicit evidence limitation remains in canonical 16 at 1280×960: all 26 live enemies are outside the focused viewport (direction counts 2/18/6/0). The PNG still shows a real shot and Assault state; it does not itself establish body/HP readability. Those are visible in the other four final sizes and verified by the real world/trial mesh regressions. The limitation is recorded in the corresponding verdict cell. The overview shows the real distant well locations; no nearby wells were invented to resemble the concept.

This review changed no Assets or capture state. Production testing, console checks and capture execution belong to the root agent.
