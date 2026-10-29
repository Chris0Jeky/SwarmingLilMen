# Bounded canonical trajectory and selection experiment

The canonical grid can preserve its exact neighbor contract while reducing dense selection work.
A max-heap replaces repeated sorted insertion, with one final in-place heap sort. This improves the
measured dense selection workload, not every input or end-to-end throughput. The dense radius scan
remains quadratic. The later explicit-telemetry change removes per-tick snapshot allocation;
see [experiment foundation](EXPERIMENT_FOUNDATION.md) for the current capture API and evidence.

## Reproduce

From the repository root, with a .NET SDK that targets .NET 8:

```sh
dotnet build SwarmingLilMen.sln -c Release
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project SwarmSim.Benchmarks -c Release --no-build -- --canonical-experiment
```

The last command runs only fixed, headless canonical workloads (at most 256 boids). It does not
launch BenchmarkDotNet or Raylib. Additional arguments are rejected. Failure of any oracle raises
an exception and exits unsuccessfully. Run the build again after editing source before using
`--no-build`. The grid/tick BenchmarkDotNet routes and Render's `--benchmark` use the legacy engine;
`SwarmSim.Benchmarks --canonical` measures complete canonical ticks separately.

## Invariants and change boundary

1. Coordinates are wrapped individually before subtraction. Radius membership uses inclusive
   squared-distance comparison and toroidal minimum-image deltas. Ordinary queries use binary32;
   the later precision fix uses double when squared radii are subnormal or overflow.
2. Self is excluded. Each visited cell is unique. Results contain the lowest qualifying indices
   in ascending order; truncation is true exactly when at least one qualifying index cannot fit,
   including an empty output buffer.
3. The cap applies **before** FOV filtering. Neighbors are not chosen by distance or visibility.
   Ordering matters to floating-point steering accumulation as well as to index membership.
4. Both worlds receive identical settings, ordered spawns, groups, seed, timestep and inputs.
   The index alone differs. The simulation still rebuilds before every step.

For capacity k, maintain the lowest min(k, m) indices seen so far as a max-heap. While it is not
full, insert each hit. Once full, every additional hit marks truncation; replace the root only
with a smaller index. The root is the largest retained index, so this preserves the prefix of
the sorted set by induction. Sort that retained heap ascending only after the scan completes.
The zero-capacity path marks truncation without accessing the buffer. Exactly k hits do not
mark truncation. No cell walk, radius arithmetic, rebuild, FOV, rule, renderer or default changes.

Online selection changes from O(m k) worst-case element moves to O(m log k), plus O(k log k)
sorting; k=0/1 are constant-time per hit. No additional working buffer is allocated. The internal
selection helpers return their actual array-write counts for the experiment; normal queries
ignore those counts. They do not expose a new public simulation configuration or metrics API.

## Trajectory oracle

`CanonicalExperiment` uses 96 boids for 24 steps at dt=1/60:

| Workload | Geometry / cap | Seed |
|---|---|---:|
| Clustered | Six patches; cap 32 | 1701 |
| Seam | Both axes straddle a 101 x 79 torus, including partial terminal cells; cap 32 | 1701 |
| Capped | A 2 x 2 dense patch; cap 8 | 1701 |
| Mixed | Uniform mixed positions, fixed before the candidate change as a holdout; cap 32 | 7919 |

All have three groups, seeded wander, FOV 240 degrees, speed 6, sense radius 12 and force 3;
other settings are the canonical defaults in this source revision. Warmup uses separate disposable
worlds. Three fresh paired runs alternate which index steps first. Every initial state and every
subsequent frame is compared as explicit little-endian bytes, preserving float bits, before hashing.
The comparison includes count/tick, every boid's position/velocity/group, per-boid rule metrics,
steering magnitude, truncation, and every exposed perception snapshot field/array. A deliberately
perturbed velocity is rejected. Private RNG/priority buffers are not serialized; their effects
are exercised by subsequent steps. This is same-binary/runtime/platform trajectory evidence,
not a cross-platform determinism guarantee or a claim about every future tick.

Measured on 28 October 2026: Windows x64, .NET runtime 8.0.24, SDK 9.0.311, Release. The experiment
was first run with the original sorted selection at base `2d7e962ef543fadd417a078973b7466917017d17`,
then with the candidate. All 12 trajectory records matched exactly, including allocation counts.
Each workload repeated with the following whole-trajectory SHA-256 (not just its last frame):

| Workload | SHA-256 | Visible neighbor visits | Truncated queries |
|---|---|---:|---:|
| Clustered | `F2C3627AD48B4EF0286E7903A4EF99E67AB8619C4BC0A2727ED908EE08564CC0` | 17,149 | 0 |
| Seam | `49A1463709BAD9788C535901455E7EE6E33B2DA233FFD32F3D690B17BBBABED1` | 36,174 | 2,304 |
| Capped | `E001E3D9A26A0B07BF7AEE1BE7A11DC4A5D750EF2A89623425CE50899324E13B` | 6,379 | 2,304 |
| Mixed | `D19FCC3A2676EDF2ACC75007844E47C2885A414F71D1A7AB6095A16E0D6ECC13` | 8,054 | 0 |

In that 28 October measurement, all full-step runs allocated 29,376 bytes per index over 24 ticks,
or **1,224 bytes/tick**. The
measurement brackets only synchronous `Step` with `GC.GetAllocatedBytesForCurrentThread`, excluding
construction, serialization and hashing. `UpdatePerceptionSnapshot` copies three N-element arrays
each tick: 1,152 payload bytes plus the observed 72 bytes of array headers at N=96. Retaining those
snapshot copies preserved ownership semantics at that revision. The 29 October telemetry change
moves independent copies to explicit compatibility captures and offers reusable caller-owned
buffers. Its Linux before/after experiment retains every workload hash while both indexes allocate
zero bytes inside all 24-step measurement loops; this is an additional same-platform comparison,
not identity with the historical Windows hashes above.

## Counted selection replay and remaining bottleneck

`CanonicalSelectionExperiment` places all boids at (10,10), queries radius zero for every self,
and uses capacity 32. The grid prepends indices during rebuild, so the exact candidate stream is
N-1 down to 0 excluding self. It replays that stream through the frozen pre-change sorted insertion
and the actual new heap helpers, counting every buffer write, **including final heap sort**.
Both retained lists and truncation are checked against actual grid and naive queries for every self.
These are selection-replay counts, not instrumented whole-engine CPU operations or timing estimates.

| N | Queries | Radius checks per index (derived) | Old sorted writes | Heap writes incl. sort | Warm query bytes: grid / naive |
|---:|---:|---:|---:|---:|---:|
| 4 | 4 | 12 | 24 | 32 | 0 / 0 |
| 32 | 32 | 992 | 15,872 | 5,504 | 0 / 0 |
| 128 | 128 | 16,256 | 456,704 | 84,096 | 0 / 0 |
| 256 | 256 | 65,280 | 1,961,984 | 337,152 | 0 / 0 |

At N=256 this is 82.8% fewer selection writes. The N=4 negative control uses **more** writes,
so there is no universal improvement claim. The allocation measure covers actual warmed queries,
not replay/construction. Comparisons, cell-walk overhead and branch costs are not represented by
array-write counts; no wall-clock speedup is inferred from them.

The radius-check column is derived exactly from the current production loops for this fixture:
both examine all N-1 other boids for each self, or N(N-1). It is explicitly not a runtime counter.
Increasing this dense workload from 128 to 256 raises these checks from 16,256 to 65,280 even with
a fixed cap. The heap removes repeated linear shifting; it does **not** bound the candidate scan
by the output cap. A later scale path must address dense candidate enumeration while preserving
lowest-index selection, explicit truncation and float boundary coverage, then measure full-step
timing distributions. This experiment does not implement that further algorithm.

## Adversarial witnesses and verification

- A single dense cell visits high indices first. Stopping after k encountered hits returns the wrong
  set; exposing the heap without sorting returns the wrong order. Zero, one, below/exact/above-cap
  buffers are checked against literal ascending index sequences.
- The six-boid FOV witness faces right at (50,50), with rear indices 1-4 and forward index 5 at
  (53,51). Cap 4 returns zero visible neighbors and truncation; an uncapped radius buffer returns
  index 5 and changes the first-step Y velocity. The wider-buffer run is a **separate semantic
  counterexample**, not a cap-after-FOV production implementation or a general quantitative FOV study.
- Existing spatial tests retain inclusive-circle, coincident-position, one/two-cell, wrapped-position,
  partial-terminal-cell, floating-point edge/slack, capacity and randomized equivalence coverage.
- Deliberately omitting final heap sorting failed all four trajectory cases at tick 1 and four
  ordered-buffer cases (8 failures, 2 controls passed). Restoring sorting passed the Release gate.
- Release solution build: zero warnings/errors; declared `Category!=Performance` suite: 527 passed.

No renderer, BenchmarkDotNet distribution, large population campaign, GPU, published release or
deployment was run. Neither these small workloads nor legacy timings establish 50k/60FPS, smooth
rendering, production performance or cross-runtime identity. The broader product priorities are
unchanged. This one experiment is complete when its review and repository merge gates pass.
