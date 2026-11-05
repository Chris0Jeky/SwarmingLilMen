# Firefly phase synchronization

`FireflyWorld` adds a stationary oscillator foundation to the shared experiment machinery. Its
state is `Oscillator(Position, Phase, NaturalFrequency)`; no boids steering or physical motion
is involved. The model uses the shared `DoubleBufferedState<T>` and `SimulationSchedule` with
its own typed state and update equation.

The phase-coupling model follows [Kuramoto's original research](https://doi.org/10.1007/BFb0013365)
(1975, *Lecture Notes in Physics* 39, pp. 420–422). The explicit numerical and initialization
choices below define this implementation; they are not claims about a biological firefly model.

This is a bounded global Kuramoto model inspired by
[the browser firefly demo](../js-demos/firefly-synchrony/README.md). The C# model supports global
coupling, heterogeneous natural frequencies and synchronous updates. The browser demo also has
spatial and ring coupling, phase lag, noise, movement, interventions and restorable checkpoints;
those features are outside this port. The C# uniform frequency distribution also differs from
the browser's Gaussian distribution. Browser and C# trajectories are not parity targets.

## Model and numerical contract

For each oscillator, one step of duration `FixedDeltaTime` applies forward Euler to:

```text
dθ_i/dt = ω_i + (K/N) Σ_j sin(θ_j - θ_i)
θ_i(next) = wrap_[0,2π)(θ_i + dt × dθ_i/dt)
```

The sum includes all N oscillators, including self, whose contribution is zero. Coupling is
nonnegative. Natural frequency may be negative, which rotates phase in the opposite direction.
Every oscillator reads the previous population. Updates are never fed to a later oscillator
in the same tick.

| Phase | Work |
| --- | --- |
| `mean-field` | Sum the sine and cosine of every current phase in stable agent order, then divide by N. |
| `oscillators` | Write every next-state slot using `K × (meanSin × cos(θ_i) - meanCos × sin(θ_i))`. |
| `commit` | Publish the complete next generation and increment the tick counter. |

The mean-field identity makes each tick O(N) without a neighbor graph or spatial index.
Position, phase and natural frequency use binary32 storage. Trigonometry, mean-field sums,
Euler drift and phase wrapping use binary64 intermediates. Phases are stored in `[0, 2π)`;
rounding up to the binary32 representation of `2π` maps to zero. No other state changes.
The implementation uses platform trigonometry, so exact repeatability is scoped to the same
implementation, platform and runtime. Agent order fixes floating-point reduction order.

The settings validator rejects nonfinite parameters and invalid domains. It does not make a
large Euler timestep accurate: reducing `FixedDeltaTime` is necessary when investigating
convergence or using substantially larger coupling/frequencies. Phase wrapping keeps finite
extreme inputs bounded but provides no numerical-accuracy claim for them.

## Initialization and settings

| Setting | Default | Meaning |
| --- | ---: | --- |
| `Coupling` | 1.55 | Global phase coupling strength K |
| `MeanFrequency` | 1.8 | Centre of the natural-frequency distribution, radians per time unit |
| `FrequencySpread` | 0.35 | Uniform distribution half-width, not standard deviation |
| `WorldWidth` | 100 | Display-position extent |
| `WorldHeight` | 60 | Display-position extent |
| `FixedDeltaTime` | 0.02 | Simulation time per tick |
| `Seed` | 123456 | Root seed for named initialization streams |

Initialization uses three independent streams: `firefly.positions`, `firefly.phases` and
`firefly.frequencies`. Positions and phases are uniform in their domains; frequency is generated
as `MeanFrequency + (2u - 1) × FrequencySpread` for `u` in `[0,1)`, then rounded to binary32.
The finite-precision result can round to either distribution endpoint. Changing only coupling
preserves the complete initial population. Changing display geometry preserves phases and
frequencies, and consequently preserves all future phase dynamics.

The streams are discarded after initialization. There is no random draw during `Step`, no
ongoing noise and no RNG cursor to restore. The explicit-state constructor copies supplied
oscillators, wraps positions and phases, and retains each supplied natural frequency.

## Run and inspect

```powershell
dotnet run --project SwarmSim.Cli -c Release -- --engine firefly --config scenarios/firefly/base.json --agents 128 --ticks 1000 --seed 21 --metrics population,firefly.coherence,firefly.meanNaturalFrequency --visual --out runs
dotnet run --project SwarmSim.Cli -c Release -- --sweep scenarios/firefly/coupling-sweep.json --out runs
```

The sweep compares K = 0, 0.5, 1.55 and 3 with the same initial conditions for each seed.
It is an exploratory finite experiment. It is not a measured critical-coupling or phase-transition
result, and the committed recipe does not claim a calibrated acceptance band.

The shared [behaviour gallery](../scenarios/gallery.json) separately includes a coupled/control
pair at seed 7, N = 128 and 1,000 ticks. Its predeclared final-quarter mean coherence checks are
at least 0.95 for K = 3 and at most 0.4 for K = 0. These are coarse implementation checks for
that finite recipe, not population-wide bounds. Default run metrics are `population` and
`firefly.coherence`; `--engine firefly --describe-engine --json` exposes the complete native
configuration, phase metrics and model-state profile without starting a run.

`CopyKinematics` captures fixed positions and **zero velocity**. The ordinary speed channel is
therefore zero and motion polarization cannot represent synchrony. The optional circular-color
observation colors these fixed positions by phase in CLI filmstrips. Native boids renderer
controls do not apply to this engine.

## Phase metrics

| Channel | Definition |
| --- | --- |
| `firefly.coherence` | `sqrt(mean(cos θ)^2 + mean(sin θ)^2)`, clamped to `[0,1]` for rounding. One means aligned phase. |
| `firefly.meanPhase` | `atan2(mean(sin θ), mean(cos θ))`, normalized into `[0,2π)`. NaN when coherence ≤ 10⁻⁶ because the mean direction is then beneath the binary32 resolution threshold. |
| `firefly.meanNaturalFrequency` | Arithmetic mean of the retained natural frequencies; constant for this model. |

The probe reads typed world state directly. Sampling allocates no managed memory, does not
advance the world and does not reinterpret phase as physical heading. Its null-world form is
metadata for metric discovery and refuses sampling.

## State integrity

The version-1 kinematic hash intentionally stays constant because positions and velocities
stay fixed. The additional `firefly-state-v1` profile hashes resolved settings, tick count,
population size and each oscillator's ordered position, phase and natural-frequency bits.
That profile detects phase or frequency changes that kinematic-only replay would miss.
Mean-field scratch is overwritten before every read and is excluded. There is no retained RNG.

The profile uses the shared little-endian `StateHashWriter`; its field order is Coupling,
MeanFrequency, FrequencySpread, WorldWidth, WorldHeight, FixedDeltaTime, Seed, TickCount, Count,
then X, Y, Phase and NaturalFrequency for each oscillator. Changing that layout or its meaning
requires a new profile version. A hash is a comparison witness, not serialized checkpoint state.

## Verification scope

`FireflyModelTests` covers the synchronous two-agent update, an independent pairwise sine oracle,
the singleton self-coupling case, uncoupled analytical rotation including negative frequency,
an already synchronized population and a finite heterogeneous coupling/control example.
It also checks initialization stream identity, coupling controls with identical initial state,
geometry-independent dynamics, seeded repeats, settings round trips, model-state hash coverage,
stationary physical capture, circular metrics, invalid inputs and warmed zero-allocation steps,
capture and metric sampling.

These are numerical and implementation regression checks. There is no cross-platform fixed
trajectory fixture, finite-size scaling study, timestep-convergence study or biological firefly
validation. Spatial coupling, graph topology, noisy frequencies, state interventions and checkpoint
restore remain separate extensions that need their own numerical contracts and evidence.
