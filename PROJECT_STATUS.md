# SwarmingLilMen — verified project state

**Updated: 2026-10-29.** This file describes current capabilities and the evidence behind them.
[The earlier tracker](docs/archive/PROJECT_STATUS_BEFORE_2026-10-29.md) preserves historical logs,
measurements and superseded plans. [Epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10)
is the roadmap and ordering authority; historical phase checklists are not a second work queue.

## What the engine is today

SwarmingLilMen is a C#/.NET 8 research foundation for deterministic 2D swarms. Two boids engines
coexist deliberately: legacy `World` is a fixed-capacity SoA pipeline and the default renderer;
`CanonicalWorld` is the target for new simulation work. The reusable general-purpose kernel,
Vicsek/ACO foundations, named RNG streams and intent composition remain roadmap work.

The browser demos are separate implementations. The existing public Pages site is unchanged by
these native-engine changes. Combat, metabolism, reproduction and cross-group interactions are
not made active merely because `SimConfig` contains related settings.

## Verified live state

- **CI and regression gates:** PR #168 retains Ubuntu/Windows merge-result builds/tests and adds
  independent jobs for the exact PR head, logging its SHA. All four jobs passed 536 tests at
  `f342bc9695e2707e930e0e5e0fac85db84a2c449`. Genome validation tests reject every covered invalid
  rate/deviation without consuming RNG draws. Removing the guards in an isolated copy produced
  eight expected failures. The main branch received this change through a merge commit.
- **Foundation gate:** PR #170 passed all 697 Release non-performance tests on Ubuntu and Windows,
  against both the merge result and exact head `5f08a9f331944f65c7665ab91fd86dd3c7ea6614`.
- **Configuration:** both config types load and save JSON, including the legacy rectangular
  aggression matrix via a compatible converter. `ConfigurationResolver` provides defaults →
  named scenario → file → explicit overrides. New resolver file/override typos fail; existing raw
  loaders retain permissive unknown-property handling. The constructor/JSON validation boundary
  rejects invalid active values while preserving documented finite clamps, zero disable modes
  and legacy unlimited-neighbor configuration. Renderer startup uses the same preset-before-file
  layering while preserving unauthored defaults and the five-entry F-key registry. Five previously
  dropped neighbor/crowding/collision settings now survive its copy paths. See
  [renderer configuration](docs/RENDERER_CONFIGURATION.md),
  [configuration cookbook](docs/CONFIGURATION_COOKBOOK.md) and
  [parameter guide](docs/PARAMETER_GUIDE.md).
- **Legacy spatial correctness:** Wrap now queries across periodic seams and computes nearest-image
  distances/FOV/separation/cohesion. `GridCellSize` is honored, with radius-complete candidate-cell
  traversal and a matching preallocation budget. One/two-cell axes are visited without duplicates;
  valid positive worlds retain at least one cell if a dimension ratio underflows. Reflect/Clamp
  keep bounded-world geometry. These are intentional legacy behavior corrections.
- **Canonical spatial precision:** both indexes keep normal binary32 query semantics and use a
  double comparison only when squared radii are subnormal or overflow. Tiny distinct offsets no
  longer count as coincident; large finite radii no longer admit farther points through infinity
  comparisons. Lowest-index ordering, inclusive boundaries and truncation behavior are tested.
- **Canonical telemetry:** `Step` no longer allocates snapshots. Explicit caller-owned
  `CanonicalTelemetry` captures last-tick diagnostics and truncation without a spatial re-query.
  A 256-boid/1,000-warm-tick regression fell from 3,144,000 allocated bytes to zero, including a
  capture-every-tick variant. The old `CapturePerceptionSnapshot()` is an allocating compatibility
  adapter. The canonical renderer now explicitly reuses telemetry once per frame. Its overlay
  labels the fresh current-position query separately from recorded tick truncation. Headless tests
  cover HUD value parity and recreation; actual graphics/manual verification remains outstanding.
  See [renderer telemetry](docs/CANONICAL_RENDER_TELEMETRY.md).
- **Trajectory preservation:** all four bounded canonical workloads retained their pre-change
  Linux hashes for all three repeats. Grid and naive state matches at every observed frame;
  capture on/off also matches every boid float bit for 128 ticks. This does not establish
  cross-platform identity. [Trajectory experiment](docs/CANONICAL_EXPERIMENT.md).

- **Determinism contract:** exact replay is scoped to the recorded implementation, configuration,
  seed, timestep, OS/architecture and .NET band. Committed eight-agent traces pin ticks
  0/1/16/64/256 and RNG sequences. Ubuntu x64 .NET 8.0.21 and 8.0.33 match; Windows x64 .NET 8.0.33
  matches legacy/RNG but has a separate exact canonical reference. The first canonical difference
  is 1 ULP at tick 1; at tick 256 the largest component difference is 0.00012969970703125
  (maximum 322 ULP). Both Windows CI jobs independently recorded the same result in
  [run 40804377401](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/40804377401).
  One-ULP force changes fail the ordinary golden assertions. The golden suite enforces the
  reviewed .NET 8 / x64 / Linux-or-Windows band; new bands require reviewed measured references.
  See [determinism and fixture policy](docs/DETERMINISM.md).
- **Headless experiments:** `SwarmSim.Cli` references only Core and runs either engine without a
  display. A completed run contains a versioned manifest with the full resolved native config,
  deterministic invariant-culture CSV, and an operational log. Seeded initialization and manifest
  replay verify both exact state and CSV hashes. Duplicate manifest fields and unknown fields are
  rejected. Git provenance comes from the built assembly when available; unknown build dirtiness
  remains null and the launch directory cannot invent source identity. Output is published into
  a fresh run directory after completion. See [headless experiments](docs/HEADLESS_EXPERIMENTS.md).

[Experiment foundation](docs/EXPERIMENT_FOUNDATION.md) describes contracts, ownership, examples
and limits. Configuration/spatial/telemetry checks run in the normal non-Performance test gate;
allocation regressions are not hidden behind timing-only test categories.

The analysis library now exposes a foundation-neutral metric frame, registered reusable probes,
ordered selection, and invariant CSV/JSONL writers. Motion and periodic centroids are separate
from exact all-population nearest-neighbor distributions; canonical boids instrumentation is a
registered foundation-specific probe. Runtime costs explicitly declare nondeterminism. The
nearest-neighbor implementation is O(N²), opt-in, and not a 50k-agent throughput claim. Periodic
analysis preserves tiny signed offsets even in very large worlds. See [metrics](docs/METRICS.md).
CLI selection, manifest integration and the full headless allocation proof remain #24 follow-up.

The isolated foundation Release build (SDK 8.0.415, Linux x64) completed with no warnings/errors;
all **697 non-Performance tests passed** before publication. The isolated golden-trace addition
passes **704 tests** locally. The separately developed headless CLI is excluded from those counts.
Hosted exact-head checks remain the merge gate; platform-specific golden fixtures are verified
against their own platform, not by widening tolerances.

The isolated CLI addition passes **763 non-Performance tests** locally with a clean Release build.
The display-free smoke step runs 400 agents for 600 ticks in both engines, repeats each run, and
replays each manifest: all three CSVs are byte-identical per engine and contain 602 lines.
The CLI dependency graph contains only Cli and Core. CI runs that same smoke on both platforms
and additionally checks the embedded build commit against the tested checkout.

The renderer follow-up adds 39 regression cases and passes **802 tests** in its isolated Release
gate. The default canonical initial/500-tick hashes are unchanged, actual --list-presets output
matches, and preset definitions plus both F1–F5 dispatch blocks are byte-identical to the prior
source. This verifies configuration and diagnostic behavior without claiming window interaction.

## Reproduce validation

```sh
dotnet build SwarmingLilMen.sln -c Release
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project SwarmSim.Benchmarks -c Release --no-build -- --canonical-experiment
dotnet run --project SwarmSim.Benchmarks -c Release --no-build -- --canonical
dotnet run --project SwarmSim.Cli -c Release --no-build -- --scenario balanced --seed 7 --ticks 600 --out runs/
```

Rebuild after source changes before using `--no-build`. The canonical benchmark route accepts
BenchmarkDotNet arguments, such as `--canonical --list flat`. Render's older `--benchmark` now
prints migration guidance and exits successfully; experiments use the standalone CLI.

## Performance evidence and its limits

Canonical BenchmarkDotNet measures complete ticks with capture off at 1k/10k/50k agents, seeded
uniform starts, fixed 1920×1080 extents, radius 10, cap 32 and dt=1/60. Worlds evolve during warmup
and measurement. Zero steady-state allocation is separately asserted in the ordinary regression
gate. This is not renderer FPS and does not cover arbitrary densities or active third-party rules.

The target of 50k agents at 60 interactive FPS is **unmet**. Prior legacy measurements and older
canonical experiments are retained with their original dates in the historical tracker; they
must not be presented as measurements of the current revision.

Final foundation-worktree run (2026-10-29, BenchmarkDotNet 0.15.6, SDK 8.0.415/runtime 8.0.21,
Ubuntu 24.04.3 x64, AMD EPYC 9V74 2.60 GHz):

| Agents | Mean ms/tick | Error (99.9% CI) | Standard deviation | BDN allocated/op |
|---:|---:|---:|---:|---:|
| 1,000 | 0.678402 | 0.018490 | 0.054229 | 0 B |
| 10,000 | 10.0191 | 0.47451 | 1.39910 | 5 B |
| 50,000 | 120.7328 | 5.29927 | 15.45822 | 0 B |

The host denied high-priority scheduling and BDN warned of bimodal distributions at 10k/50k.
The 10k allocation row came from 336 bytes across 64 operations; a previous run
reported zero for all cases and the direct current-thread allocation regressions report zero.
A targeted follow-up using the same 10k settings measured 1,320 ticks after warmup with zero
current-thread and process-total allocated-byte deltas and zero collections. This does not
identify the cause of the BDN reading. The nonzero observation is retained rather than rounded
away. These noisy cloud measurements do not establish a performance improvement or a universal
zero-allocation promise.

The issue #67 query probe used 512 boids, seed 8181, radius/cell size 50, a 64-slot buffer, 4,096
warmup queries and seven batches of 32,768 queries with tiering disabled. Adjacent baseline/final
median ns/query were: grid sparse 439.5/453.2, grid dense 14,708.8/14,415.6, naive sparse
23,638.8/19,196.0 and naive dense 18,903.0/17,951.3. All measured query allocations were zero.
The earlier grid-sparse baseline was 370.1 ns, demonstrating substantial host variation. These
measurements check the cold-path precision fix for a gross regression; they do not establish a
speedup or machine-independent performance bound.

## Current limitations and next work

1. Preserve the committed platform-specific golden traces and their explicit runtime band (#21).
2. Extend the single-run CLI with registered analysis probes (#24) and seed/parameter sweeps
   (#25). Timing data must stay separate from byte-repeatable physical metrics.
3. Collect the outstanding manual HUD/O/Tab/F12 and F1–F5 evidence (#20/#22). The implementation
   now uses reusable telemetry and shared startup configuration. Current overlay geometry still
   performs the existing index refresh only when shown; the recorded tick label does not re-query.
4. Introduce named RNG streams and real observation/intent arbitration (#26/#27), preserving the
   settled trace contract except for the explicitly documented RNG transition. Extra positional
   rules are not yet a general composition surface.
5. Prove generality with Vicsek then a minimal scalar-field/ACO consumer (#29/#30), not speculative
   state generics. Behavioral parity/retirement criteria (#28) and named rule-control visuals (#40)
   depend on those settled seams. Neither engine is deleted without a migration decision.

Other tracked residue: fixed legacy capacity versus reserved `MaxCapacity` (#62); runtime help
synchronization (#39); remaining scenario/composition coverage (#41); source-citation/doc cleanup
(#60/#63); and publication-scanner indirection gaps (#164). A green engine suite does not validate
renderer interaction, browser demos, or a deployment.

## Trust and publication boundary

In-process simulation rules have the host process's authority. The engine is a modeling boundary,
not a security isolation mechanism. Untrusted code requires the separate containment workstream
(#44). Static-demo publication remains allowlisted and manually dispatched; this research work
does not change repository settings, publish releases, or deploy Pages.
