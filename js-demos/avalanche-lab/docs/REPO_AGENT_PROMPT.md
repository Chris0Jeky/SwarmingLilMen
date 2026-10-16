> Imported design reference from the chat demo. Native C# work described below remains a proposal; see this package README for current commands and verification.

# In-repository agent prompt — evaluate the Abelian sandpile foundation

You are working inside `Chris0Jeky/SwarmingLilMen`. This prompt proposes a new emergence foundation represented by the standalone `avalanche_lab.html` demo. It does **not** authorize blindly copying the JavaScript into production or widening the project beyond its current ordered roadmap.

## First actions

1. Read `AGENTS.md`, `CLAUDE.md`, the verified section of `PROJECT_STATUS.md`, epic #10, and every currently open prerequisite issue.
2. Refresh the exact repository `HEAD`, worktree state, CI state, pull requests, issue dependencies, licence posture, and active agent-harness authority. Do not trust dated counts in this bundle over live repository evidence.
3. Inspect `INTEGRATION_NOTES.md` and the demo. Run the standalone page and read `VALIDATION_REPORT.json`.
4. Determine whether the experiment spine, deterministic RNG streams, metrics registry, and run-artifact contracts required by this model actually exist. At the reviewed state, they were roadmap work.

## Architectural rule

Do not force the sandpile through `CanonicalWorld`, `Boid`, steering rules, velocity integration, `MaxForce`, or continuous-neighbour observations.

The reusable layer this model should test is narrower:

```text
experiment orchestration
+ deterministic randomness
+ bounded execution
+ metrics and artefacts
+ provenance and checkpoint contracts
```

The model itself is a discrete lattice with event-driven relaxation.

## Suggested decision

Treat this as a post-experiment-spine vertical proof. Prefer a small model package such as:

```text
SwarmSim.Models.Sandpile/
  SandpileConfig.cs
  SandpileState.cs
  SandpileSimulation.cs
  SandpileMetrics.cs
  TopplingQueue.cs
```

Do not begin implementation until the owner or current roadmap explicitly schedules it.

## Minimum production slice

When authorized, implement only:

- square 2D lattice;
- threshold four;
- orthogonal redistribution;
- open boundaries;
- optional fixed sink mask;
- seeded drive-position stream;
- `DriveAndRelax()` returning one immutable avalanche record;
- stable checkpoint export/import;
- headless metrics output through the shared experiment infrastructure.

Defer stochastic redistribution, arbitrary graphs, visual effects, distributed execution, and new universal field abstractions.

## Required deterministic tests

1. Same seed and drive sequence produce identical final arrays and avalanche records.
2. Every completed drive event leaves all non-sink cells below threshold.
3. Exact conservation holds:
   `initial mass + added = final mass + dissipated`.
4. FIFO, LIFO, and randomized legal toppling orders reach the same stable lattice.
5. Corners dissipate two grains and non-corner edges dissipate one per toppling.
6. Sink cells absorb grains and never become unstable.
7. Stable checkpoint round-trip preserves the full scientific state.
8. Malformed or unstable checkpoints fail before mutating the destination simulation.
9. Adding an unrelated named RNG stream does not perturb drive positions.
10. Operation and queue limits fail deterministically and leave an explicit incomplete-run record.

## Performance evidence

Measure, do not assume:

- topplings per second;
- allocation per drive-and-relax event;
- queue high-water mark;
- scaling by lattice size and avalanche size;
- cost of visit stamps and queued-bit tracking.

Scientific artefacts must not include wall-clock timings or other nondeterministic operational measurements.

## Deliverable when only planning is authorized

Produce a repository-native proposal that:

- maps prerequisites and blockers;
- recommends whether to seed one issue or defer entirely;
- identifies reusable contracts versus sandpile-specific code;
- records owner decisions without treating recommendations as ratified;
- updates no live roadmap authority unless explicitly instructed.
