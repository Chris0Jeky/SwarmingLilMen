# Vicsek foundation and seam inventory

`VicsekWorld` is the second foundation in the C# engine and the first honest test of the kernel
claim in [epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10). It shares kinematics
and neighborhood queries with boids and replaces all steering. The inventory below is the main
result: what the existing seams carried, what had to widen and what was bypassed. It stays in this
file, one inventory for both later foundations; the [ACO model and evidence](ACO.md) have their own.

This is a **finite noise-response result, not a phase-transition measurement**. No system-size or
convergence study has been run.

## Model

The target is the original model
([Vicsek et al. 1995](https://arxiv.org/abs/cond-mat/0611743), equations 1-3):

```text
x_i(t+1) = x_i(t) + v_i(t) dt                     old velocity
θ_i(t+1) = arg Σ_{j in r} v_j(t) + ξ_i(t)         old headings, self included once
ξ uniform on [-η/2, η/2), η in radians
```

| Decision | Choice |
|---|---|
| Update ordering | Synchronous double buffer. This differs from the sequential browser demo, which is a qualitative reference only. |
| Neighborhood | Inclusive radius on the torus, using the existing index contract |
| Truncation | None. The result buffer holds every agent, and a truncated query throws. |
| Zero resultant | When the mean velocity is below `1e-6 × Speed`, the agent keeps its own heading, then noise applies. |
| Noise schedule | One draw per agent per tick in index order, whatever the neighborhood, so runs differing only in η share a noise sequence |
| Streams | `vicsek.spawn` and `vicsek.noise`, derived from the run seed. Spawn is independent of η, so noise comparisons share initial state. |
| Precision | Binary32 state; the velocity sum and `atan2` use doubles |

The noise interval is half-open because the generator's float adapter is. Headings pass through
platform trigonometry, so exact traces are promised only for repeats on the same platform and
runtime. Vicsek has no committed golden trace.

## Run it

```powershell
dotnet run --project SwarmSim.Cli -c Release -- --engine vicsek --agents 400 --ticks 2000 --seed 7 --out runs
dotnet run --project SwarmSim.Cli -c Release -- --sweep scenarios/vicsek/noise-sweep.json --out runs
```

Native configuration uses `Noise`, `Speed`, `InteractionRadius`, `WorldWidth`, `WorldHeight`,
`FixedDeltaTime` and `Seed`. Defaults are the paper's r = 1, v = 0.03 and dt = 1 in a 10 × 10 box.
Named `--scenario` recipes are boids tunings and are refused for this engine. The order parameter
is the existing `polarization` channel, which a test pins to `|Σ v| / (N v0)`.

## Measured noise response

Exploratory `noise-sweep.json`: N = 400, L = 10, density 4, five seeds, mean order over ticks
2001-3000 on Windows x64 .NET 8.

| η (radians) | Mean | Seed min | Seed max |
|---:|---:|---:|---:|
| 0.05 | 1.000 | 1.000 | 1.000 |
| 0.2 | 0.998 | 0.998 | 0.998 |
| 0.5 | 0.987 | 0.986 | 0.988 |
| 1.0 | 0.946 | 0.942 | 0.949 |
| 2.0 | 0.790 | 0.772 | 0.797 |
| 3.0 | 0.560 | 0.552 | 0.581 |
| 4.0 | 0.267 | 0.224 | 0.320 |
| 5.0 | 0.067 | 0.062 | 0.070 |
| 2π | 0.045 | 0.043 | 0.046 |

At η = 2π the heading is independent of the neighborhood, and the measured value agrees with the
`0.886 / √N = 0.044` expected from random headings. The sweep ran once; its data supports a
smooth response at this size and says nothing about the order of the transition.

## Locked regression gate

`VicsekCliTests.LockedRecipeOrdersAtLowNoiseAndDisordersAtHighNoise` runs the committed
`noise-response.json` through the real sweep path. Its window is ticks 1001-2000.

| | Calibration, seeds 1-5 | **Locked band** | Regression, seeds 31-35 |
|---|---|---|---|
| η = 0.5, each seed | 0.988-0.989 | **≥ 0.95** | 0.988 at all five |
| η = 5, each seed | 0.124-0.152 | **≤ 0.25** | 0.126-0.136 |
| Ensemble difference | | **≥ 0.65** | 0.858 |

The recipe is N = 100, L = 5, density 4. An earlier locked 400-agent recipe on seeds 11-15 passed
but added 30 seconds to the test suite, so it was replaced for cost, not because of a failure. The
replacement bands were recalibrated on seeds 1-5 and committed in `0cb7a8f` before seeds 31-35
first ran. Do not move these bands or change the seeds to make a failure pass.

Two complete runs of the recipe produced a byte-identical `summary.csv`.

## Mutation witnesses

Each change was applied to `VicsekWorld.cs`, tested, then restored.

| Mutation | Failing tests |
|---:|---|
| Ignore neighbors | 6, including the locked gate |
| Remove noise | 3, including the locked gate |
| Treat η as a fraction of 2π | 2, including the locked gate |
| Count self twice | 5 |
| Read neighbors updated earlier in the tick | 3 |
| Advance position with the new velocity | 1 |

The last mutation first survived, because `Assert.Equal(float, float, 5f)` is a tolerance of five
rather than five decimal places. The assertions now pass an integer precision.

## Seam inventory

**Boids stayed untouched.** `Canonical/`, legacy `World` and `Systems/` have no changes, and the
exact boids goldens still pass.

### Reused unchanged

| Seam | How Vicsek uses it |
|---|---|
| `ISpatialIndex`, `GridSpatialIndex`, `NaiveSpatialIndex` | Stores `Boid` values directly. Grid and naive runs have identical hashes for 128 noisy ticks. |
| `RandomStreams`, `Pcg32Rng` | Two named world streams |
| `Boid`, `Vec2`, `MathUtils.WrapPosition` | Position, velocity and the periodic box |
| `MotionMetricProbe` and exact nearest-neighbor probe | All channels work unchanged. `polarization` is the order parameter. |
| `MetricRegistry`, `MetricCsvWriter`, `ExperimentMetrics` | No change |
| `ExperimentRunner` staging, publish and replay verification | No logic change |
| `SweepRunner`, `SweepSummary` | No change; `Noise` is an ordinary numeric axis |

### Widened

| Seam | Change | Finding |
|---|---|---|
| `SimulationKinematicHash` | New overload sharing one span helper | Version-1 bytes are unchanged |
| `ConfigurationResolver` | `ResolveVicsekSettings` on the existing generic layering | The private `canonical` flag and the boids-only scenario table are not foundation-neutral |
| `RunManifest` | Spawn model is per engine; Vicsek requires schema 2 | Replay had assumed a single spawn model |
| `RunRequest`, `PreparedSimulation`, `RunOptions`, `MetricCatalog`, `SweepPlan` | One more branch in each, at the time | **Engine was a closed list spread across seven CLI files.** ACO replaced it; see below. |

### Bypassed

| Seam | Why |
|---|---|
| `CanonicalWorld.Step` and force composition | Vicsek replaces a heading. Force policies, resolvers and velocity stages cannot express that, which confirms the gap recorded in #27. |
| `SimulationRunner` | It is typed on legacy `World`, and no headless engine uses it. The CLI loop is the actual headless scheduler. |
| `PreparedSimulation.Spawn` | Boids spawning uses legacy `Rng`; new consumers use named streams. |
| Named scenarios and the parity harness | Both are boids-specific |

### What was not earned

`ISpatialIndex` stays typed on `Boid`. The issue allowed generalizing it here, but Vicsek agents
have the same position and velocity shape, so a type parameter would solve nothing yet. `Boid`
carries an unused `Group` byte in this foundation. Agent-state generics stay deferred until a
foundation has a genuinely different shape.

## Seam inventory update: ACO

ACO (#30) is the first foundation with a different agent shape and non-agent state. Boids and
Vicsek behaviour did not change: `Canonical/`, legacy `World`, `Systems/` and `Vicsek/` have no
edits, and the goldens and Vicsek tests pass unchanged.

### What the registry replaced

Four engines met the rule of three, so the branching was removed before ACO was added. One
`EngineDefinition` per engine (name, spawn model, minimum manifest schema, scenario support,
resolve, default settings JSON, extra probes, factory) and one `IHeadlessSimulation` adapter now
live in `SwarmSim.Cli/Engines.cs`. `RunOptions`, `RunRequest`, `PreparedSimulation`, `MetricCatalog`,
`RunManifest`, `SweepPlan` and `ExperimentRunner` consult the registry; settings pass through the
spine as an opaque object. There is no plugin system, reflection or public Core API. Final hashes
and `metrics.csv` hashes of all three existing engines were byte-identical before and after.

With the registry, adding the engine outside `Core/Aco/` took one definition, one adapter of 18
lines, one resolver method and one hash overload. `ExperimentRunner`, `ExperimentMetrics`,
`SweepPlan`, `SweepRunner`, `SweepSummary`, `RunManifest`, `RunOptions` and the visual report needed
no ACO edit at all.

One wart remains: a `RunRequest` constructor taking the old legacy and canonical arguments exists
for two existing tests, which were kept unchanged on purpose.

### What the field forced

| Finding | Detail |
|---|---|
| **No non-agent state existed anywhere** | `ScalarField` is new: one sealed class, not `IField`, no layers. |
| **The kernel has no scheduler** | The agent and field phases are distinct inside `AntColonyWorld`. `IHeadlessSimulation.Step` hides them. Two field foundations would earn a shared phase; one does not. |
| **The kinematic hash cannot see the field** | Version 1 covers motion only, by design. Field, load and food divergence reaches it only through later motion. The gap is recorded, tested and left unwidened; field bits have their own repeat test. |
| **`MetricFrame` carries agents only** | World-state channels need a probe holding the world, as the boids instrumentation probe already did. Two probes now take that shape, so a world view in the frame is a candidate at the next one. |
| **Probes need a metadata form** | Selection is validated before any world exists, so a probe must construct without one. `AntColonyMetricProbe` accepts null and refuses to sample. |
| **The filmstrip sees kinematics only** | It works unchanged and draws ants well, but neither the field nor the nest and food. Pheromone visualization is a stated non-goal. |

### Reused unchanged by ACO

| Seam | How ACO uses it |
|---|---|
| `RandomStreams`, `Pcg32Rng` | One stream per ant, with draw counts conditional on state. The first real stress test of per-agent streams passed with no contract change. |
| `MetricRegistry`, `MetricCsvWriter`, `ExperimentMetrics` | No change |
| `MotionMetricProbe`, exact nearest-neighbor probe | Foundation-neutral through `KinematicSample` |
| `ExperimentRunner`, `SweepRunner`, `SweepSummary`, `--visual` | No change; `Deposit` is an ordinary numeric axis |
| `MathUtils.WrapPosition`, `MinimumImageDelta`, `Vec2` | Periodic box |

### Bypassed by ACO

| Seam | Why |
|---|---|
| `ISpatialIndex` | **Ants need no neighbor query at all.** They interact only through the field. The index stays typed on `Boid`; ACO supplies no reason to change that, and is no evidence for agent-state generics. |
| `Boid` | Heading and load are the state; velocity is derived for analysis. |
| `CanonicalWorld.Step` and force composition | As for Vicsek. |
| `SimulationRunner` | Still typed on legacy `World`. |

### What was not earned

Agent-state generics, a scheduler abstraction, `IField`, field layers and diffusion all stay
deferred. Each has exactly one consumer or none.

## Limits

- Noise response at one density and two sizes only; no finite-size scaling or Binder cumulant.
- The renderer has no Vicsek view (a stated non-goal).
- No throughput target. The sweep above took 156 seconds for 45 runs on a busy desktop, so ordered
  flocks are not cheap: clumping raises neighbor counts well above the mean density.
- The legacy and canonical determinism bands in [determinism](DETERMINISM.md) do not extend here.
