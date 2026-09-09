> Imported design reference from the chat demo. Native C# work described below remains a proposal; see this package README for current commands and verification.

# Firefly Synchronization / Kuramoto Foundation

This bundle adds a standalone browser demonstration of an emergence model not currently represented by the repository's boids, Vicsek, ACO, or PSO demos.

## Model

Each oscillator has a phase `theta_i` and natural frequency `omega_i`:

```text
d theta_i = [omega_i + K/N_i * sum_j sin(theta_j - theta_i - alpha)] dt
            + D sqrt(dt) xi_i
```

The macroscopic order parameter is:

```text
r exp(i psi) = (1/N) sum_j exp(i theta_j)
```

`r = 0` indicates broadly incoherent phases; `r = 1` indicates phase lock. No leader or global script dictates the synchronized state.

## Why this is a materially new foundation

This is not a variant of boids steering:

- agent state is phase and natural frequency rather than position/velocity alone;
- the primary emergent observable is phase coherence rather than flock polarization;
- global, local metric, and non-local ring topology are all meaningful model variants;
- force-budget arbitration is irrelevant;
- a phase-lag parameter supports travelling waves, clusters, and partially coherent states;
- stochastic phase diffusion exercises deterministic RNG streams independently of movement.

## Demo capabilities

The HTML is dependency-free and includes:

- fixed-step simulation;
- seeded xorshift32 randomness;
- deterministic same-browser replay self-check;
- global mean-field coupling in O(N);
- local metric coupling using a toroidal uniform grid;
- non-local ring coupling;
- phase noise and heterogeneous natural frequencies;
- order-parameter history and phase histogram;
- presets for disorder, threshold behaviour, global synchronization, local waves, moving fireflies, and a chimera seed;
- interactive perturbations and external entrainment flashes;
- state JSON export/import and PNG export.

## Recommended C# vertical slice

Do not force the current boids `CanonicalWorld` to own oscillator semantics. Add a model adapter behind the future experiment-session seam:

```text
SwarmSim.Core/
  Experiments/
    ISimulationSession.cs
  Foundations/
    Kuramoto/
      KuramotoConfig.cs
      KuramotoState.cs
      KuramotoSession.cs
      KuramotoMetrics.cs
```

Suggested contracts:

```csharp
public interface ISimulationSession
{
    ulong Tick { get; }
    void Step();
    void WriteSnapshot(ISnapshotSink sink);
}

public sealed record KuramotoConfig(
    int Count,
    ulong Seed,
    double FixedDeltaTime,
    double Coupling,
    double FrequencySpread,
    double Noise,
    double PhaseLag,
    CouplingTopology Topology,
    double Range);
```

The first C# slice should be headless. Reuse or widen only:

- deterministic scheduler;
- named RNG streams;
- topology/neighbour query contract;
- experiment manifest and run directory;
- metric probe registry.

Do not reuse:

- boids force budgets;
- steering intents;
- separation/alignment/cohesion policy types;
- canonical turn-rate or whisker stages.

## Acceptance tests

1. Same seed/config/tick count produces bit-identical phase arrays on the supported runtime/platform contract.
2. Adding an unrelated named RNG stream does not change the trajectory.
3. With global coupling and zero noise, increasing `K` across a fixed sweep raises terminal mean `r`.
4. At strong coupling and low frequency spread, mean `r` exceeds a documented threshold across several seeds.
5. At zero coupling and broad frequency spread, mean `r` stays below a documented threshold.
6. Global mean-field O(N) output matches a naive O(N^2) reference within a tight numeric bound for small N.
7. Local grid neighbours match a naive toroidal metric query set exactly.
8. Metric sampling does not alter simulation state or RNG consumption.
9. State serialization round-trips configuration, phase, frequency, tick, and RNG state.
10. The boids golden fixtures remain unchanged when the Kuramoto foundation is introduced.

## Candidate metrics

- global order parameter `r`;
- mean phase `psi`;
- phase entropy;
- locked-frequency fraction;
- number and mass of phase clusters;
- local order distribution;
- synchronization time to a threshold;
- hysteresis under increasing/decreasing coupling sweeps;
- perturbation recovery time;
- neighbour-count distribution for local coupling.

## Research and expansion trails

- Kuramoto critical-coupling sweeps under different frequency distributions;
- Sakaguchi phase lag and travelling-wave regimes;
- local versus small-world versus scale-free synchronization;
- adaptive networks in which synchronized agents strengthen edges;
- pulse-coupled firefly models rather than continuous sinusoidal coupling;
- oscillator communities with bridge nodes;
- synchronization under delay, packet loss, adversarial oscillators, or noisy observations;
- coupling phase synchronization to boid motion, energy, reproduction, or N-person strategy state only after both foundations work independently.
