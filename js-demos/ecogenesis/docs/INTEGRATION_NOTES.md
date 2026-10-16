> Imported design reference from the chat demo. Native C# work described below remains a proposal; see this package README for current commands and verification.

# Ecogenesis integration notes for SwarmingLilMen

## Recommendation in one sentence

Treat Ecogenesis as a **post-experiment-spine ecology/evolution foundation**, not as another rule inside `CanonicalWorld` and not as justification for an immediate universal-agent rewrite.

The demo is intentionally larger than a normal vertical slice because its job is to expose the contracts that boids, Vicsek and ACO do not exercise together: lifecycle, resource contention, multiple mutable fields, predation, disease, heredity, speciation evidence, lineage and ecological shocks.

## What this foundation proves

Ecogenesis is a useful architectural stress test because it needs all of the following in one deterministic run:

- bounded local spatial observations;
- multiple organism types emerging from continuous genes rather than a type enum;
- mutable environmental fields;
- explicit resource demand and conflict resolution;
- agent birth and death;
- variable population size;
- inherited controller state;
- named stochastic consumers;
- lineage/provenance records;
- foundation-specific metrics;
- stable checkpoint and replay semantics;
- serial parameter sweeps;
- researcher interventions clearly separated from endogenous events.

It does **not** fit cleanly into a force-only steering kernel. That is a useful finding.

## Do not force it through boids types

Avoid designs such as:

```csharp
CanonicalWorld<EcogenesisBoid>
IRule.Compute(...) -> Vec2
MaxForce
SeparationRule / AlignmentRule / CohesionRule
```

Ecogenesis has steering, but steering is only one output among feeding, attack and reproduction intents. Its environment and lifecycle are first-class state. A generic type parameter on `CanonicalWorld` would preserve the wrong world model and spread boids assumptions through a nominally generic API.

The common layer should be the experiment/session substrate:

```text
manifest
scheduler
RNG streams
run identity and provenance
checkpoint contract
metric registration
artifact writers
bounded execution
```

Observation/intent concepts can also be shared at a structural level, but each foundation should own its concrete observation and intent schemas.

## Suggested package placement

Do not begin with the 11-package aspirational masterplan. A minimal placement after the experiment spine exists is:

```text
SwarmSim.Core/
  Experiments/
    ISimulationSession.cs
    SimulationClock.cs
    RunManifest.cs
    MetricRegistry.cs
    CheckpointEnvelope.cs
    RngStreamFactory.cs

SwarmSim.Foundations.Ecogenesis/
  EcogenesisSession.cs
  EcogenesisConfig.cs
  EcogenesisState.cs
  Environment/
  Organisms/
  Observation/
  Intents/
  Resolution/
  Metrics/
  Checkpoints/

SwarmSim.Cli/
  existing experiment command
  foundation = "ecogenesis"
```

If creating another project is premature, place the first headless slice under:

```text
SwarmSim.Core/Foundations/Ecogenesis/
```

but keep its namespace and dependencies isolated so it can be extracted without a rewrite.

## Session contract

A narrow experiment-facing contract is sufficient:

```csharp
public interface ISimulationSession
{
    string FoundationId { get; }
    ulong Tick { get; }

    void Step();
    void Step(int ticks);

    SimulationSnapshot CaptureSnapshot(SnapshotRequest request);
    CheckpointEnvelope CreateCheckpoint();
    void RestoreCheckpoint(CheckpointEnvelope checkpoint);

    void RegisterMetrics(IMetricSink sink);
}
```

This contract should not expose `Boid`, `World`, `Vec2`, `MaxForce`, a renderer, or a concrete RNG.

For large simulations, `CaptureSnapshot` should support selected channels rather than copying every array on every tick.

## Deterministic phase architecture

The browser demo’s phase order should survive into C# because it prevents iteration-order accidents:

```text
1. EnvironmentUpdate
2. SpatialIndexRebuild
3. ObservationBuild
4. PolicyEvaluation
5. ResourceIntentResolution
6. KinematicsAndMetabolism
7. DamageResolution
8. DiseaseResolution
9. ReproductionResolution
10. DeathAndNutrientReturn
11. SpeciesAndLineageAccounting
12. MetricSampling
```

A simple phase interface is enough:

```csharp
public interface IEcogenesisPhase
{
    void Execute(ref EcogenesisContext context);
}
```

Do not make the general kernel enumerate these phase names. The Ecogenesis module assembles its own phase pipeline. The shared scheduler only executes an ordered session step.

## Data model

### Organism store

Use a Structure-of-Arrays layout from the start because population is dynamic and the policy loop is hot:

```csharp
public sealed class OrganismStore
{
    public int Count { get; private set; }
    public int Capacity { get; private set; }

    public float[] X = null!;
    public float[] Y = null!;
    public float[] Vx = null!;
    public float[] Vy = null!;
    public float[] Heading = null!;

    public float[] Energy = null!;
    public float[] Health = null!;
    public float[] Infection = null!;
    public float[] Age = null!;

    public int[] SpeciesId = null!;
    public int[] ParentA = null!;
    public int[] ParentB = null!;
    public int[] Generation = null!;

    // Packed [capacity * TraitCount]
    public float[] Traits = null!;

    // Packed [capacity * WeightCount]
    public float[] ControllerWeights = null!;

    // Packed [capacity * OutputCount]
    public float[] ControllerMemory = null!;
}
```

The lifecycle policy must be explicit:

- stable IDs are not array indexes;
- deaths are accumulated during a tick;
- compaction occurs at a deterministic phase boundary;
- parent/species records use stable IDs;
- compaction order is deterministic;
- births are appended in deterministic intent order;
- capacity growth, if supported, happens only outside hot iteration.

A fixed capacity with explicit rejection is preferable for the first C# slice. Do not repeat the current `MaxCapacity` ambiguity.

### Environment fields

The smallest useful field substrate is concrete rather than maximally generic:

```csharp
public sealed class EcogenesisFields
{
    public readonly int Width;
    public readonly int Height;

    public float[] Elevation = null!;
    public float[] MoistureBase = null!;
    public float[] Fertility = null!;
    public float[] TemperatureBase = null!;
    public byte[] Water = null!;

    public float[] Plants = null!;
    public float[] Nutrients = null!;
    public float[] Carrion = null!;
    public float[] Temperature = null!;
    public float[] Moisture = null!;
}
```

This can later reveal a reusable scalar-field contract with ACO, but do not invent an `IUniversalField<TCell,TKernel,TBoundary>` before two real consumers agree on it.

## Observation contract

The observation should contain bounded, relative evidence. A policy should not receive the full organism store.

```csharp
public readonly ref struct EcogenesisObservation
{
    public readonly OrganismSelfView Self;
    public readonly ReadOnlySpan<NeighbourView> Neighbours;
    public readonly LocalFieldView Fields;
    public readonly ControllerMemory Memory;
}

public readonly record struct NeighbourView(
    StableAgentId Id,
    float DeltaX,
    float DeltaY,
    float Distance,
    float RelativeSize,
    float ThreatScore,
    float PreyScore,
    int SpeciesId,
    float Infection);
```

Cap the neighbour span and report truncation. Use deterministic truncation semantics—preferably nearest-first with stable-ID tie breaking for this foundation—because mate and predator selection are sensitive to omitted neighbours.

## Intent contract

Ecogenesis should produce multiple intent channels:

```csharp
public readonly record struct EcogenesisIntent(
    StableAgentId Actor,
    MovementIntent Movement,
    ResourceDemand PlantDemand,
    ResourceDemand CarrionDemand,
    AttackIntent? Attack,
    ReproductionIntent? Reproduction);
```

Do not collapse this to one vector. The resolver phases own conflicts.

## Resource contention

The important invariant is order independence within one cell.

For resource `R`, available amount `A`, and demands `d_i`:

```text
if Σd_i <= A:
    allocation_i = d_i
else:
    allocation_i = A * d_i / Σd_i
```

The test should shuffle organism storage order while keeping stable IDs and assert identical allocations and next scientific state.

Floating-point accumulation order can still differ. If exact equality matters, aggregate demands in stable-ID order or use fixed-point resource units for the first implementation.

## Attack resolution

Collect attack intents first. Accumulate target damage. Apply all damage simultaneously. Only then process death and biomass return.

This prevents an early attacker from removing a target before later same-tick attacks are evaluated.

Energy transfer needs a written rule for overkill. Reasonable options are:

1. reward all inflicted damage, including overkill;
2. cap reward at remaining target health and allocate proportionally;
3. create carrion only and require scavenging.

The browser demo uses a simplified immediate damage reward plus carrion. For the C# research model, option 2 or 3 is cleaner.

## Reproduction and heredity

The first slice should support:

- maturity;
- energy threshold;
- reproduction cooldown;
- optional compatible partner;
- deterministic crossover;
- bounded mutation;
- parent energy investment;
- stable parent IDs;
- child placement;
- population-cap rejection.

Every stochastic operation gets a named stream:

```text
world/terrain
world/founders
agent/{stableId}/controller-noise
reproduction/{birthOrdinal}/crossover
reproduction/{birthOrdinal}/trait-mutation
reproduction/{birthOrdinal}/weight-mutation
disease/{tick}/transmission
predation/{tick}/damage
```

A new stochastic consumer must not perturb existing streams.

## Controller implementation

The browser controller has 19 inputs and four outputs. The C# version should keep the dimensions fixed in v1 and use packed arrays.

A scalar implementation first:

```csharp
for (int output = 0; output < OutputCount; output++)
{
    float sum = 0f;
    int offset = agentWeightOffset + output * InputCount;

    for (int input = 0; input < InputCount; input++)
        sum += weights[offset + input] * observations[input];

    outputs[output] = MathF.Tanh(sum);
}
```

Benchmark before adding SIMD. Controller evaluation is likely to become measurable only after neighbourhood queries and field sampling are efficient.

## Species evidence

Keep species classification in the analysis layer unless a policy consumes species IDs.

The browser demo lets species IDs influence kin and mate observations, so its operational clusters are part of dynamics. A C# research version should make this choice explicit:

- **Analysis-only clustering:** scientifically cleaner; clusters cannot create self-reinforcing reproductive isolation.
- **Dynamics-visible clustering:** supports reproductive isolation but makes the clustering algorithm a causal biological mechanism.

Recommended first C# choice: use a heritable lineage/family ID for kin behaviour and compute genome clusters only as metrics. Add cluster-mediated mate compatibility as a separate experiment.

## Metrics

Foundation-agnostic channels:

- tick;
- population;
- elapsed operational time;
- allocated bytes;
- mean energy;
- births and deaths.

Ecogenesis-specific probes:

- plant biomass;
- nutrient and carrion pools;
- herbivore/omnivore/predator counts;
- predation events;
- infection prevalence and incidence;
- mean and quantile trait values;
- genome diversity;
- lineage depth;
- living operational clusters;
- normalized cluster entropy;
- extinction/origination rate;
- trophic biomass ratios;
- spatial occupancy and habitat specialization;
- recovery time after intervention.

Separate deterministic scientific metrics from non-deterministic operational telemetry. Wall time and allocation evidence must not be embedded in an artifact expected to replay byte-identically.

## Checkpoint format

Use a versioned envelope:

```json
{
  "schemaVersion": 1,
  "foundation": "ecogenesis",
  "tick": 1800,
  "seed": 424242,
  "rngAlgorithm": "pcg32-v1",
  "configHash": "...",
  "stateHash": "...",
  "payload": "foundation-owned binary or JSON data"
}
```

Validation must happen before replacing the live session. Reject:

- wrong foundation or version;
- field-shape mismatch;
- population beyond capacity;
- duplicate stable IDs;
- parent/species references to absent records where prohibited;
- non-finite values;
- malformed controller dimensions;
- unsupported RNG algorithm/state.

For large fields, a binary payload with an independently hashed manifest is preferable to JSON arrays. JSON is acceptable for the first slice because inspectability has value.

## Suggested implementation sequence

### E0 — Demo adoption

- Add the HTML demo under `js-demos/ecogenesis/`.
- Add a brief source-of-truth banner.
- Link it from the JS demo index.
- Do not claim it runs through the C# kernel.

### E1 — Headless deterministic fields

- `EcogenesisConfig`.
- deterministic terrain generation;
- plant/nutrient/carrion fields;
- finite-value and checkpoint tests;
- no organisms yet.

### E2 — Organism lifecycle

- fixed-capacity store;
- stable IDs;
- spawn, metabolism, death, compaction;
- deterministic resource consumption;
- population and biomass metrics.

### E3 — Observation and controller

- exact neighbourhood contract;
- field sampling;
- fixed controller dimensions;
- movement and thermal/habitat costs;
- no predation or reproduction yet.

### E4 — Intent resolvers

- proportional plant/carrion allocation;
- simultaneous attacks;
- disease update;
- deterministic tests under storage-order permutations.

### E5 — Heredity

- reproduction intents;
- crossover and mutation streams;
- lineage records;
- trait-distribution metrics;
- population-cap behaviour.

### E6 — Experiment proof

- one versioned manifest;
- one 10-seed sweep;
- one intervention A/B;
- checkpoint/replay artifact;
- baseline performance and allocation measurements.

### E7 — Generality assessment

Write a seam inventory comparing boids, Vicsek, ACO and Ecogenesis:

```text
reused unchanged
widened
foundation-owned
bypassed
proved unnecessary
```

Only then decide whether further kernel generalization is warranted.

## Acceptance tests

At minimum:

### Determinism

- same manifest and named streams produce the same full-state hash;
- adding a new unused stream does not change the run;
- checkpoint → 500 ticks equals uninterrupted 500 ticks;
- a changed seed changes the state;
- invalid checkpoint leaves the live state unchanged.

### Fields

- no field becomes NaN or infinite;
- plant/carrion/nutrient bounds hold;
- sealed and toroidal samplers match their contracts;
- user intervention is represented in provenance.

### Resource resolution

- allocations never exceed available resource;
- allocations never exceed individual demand;
- allocations are invariant under storage-order permutation;
- zero total demand does not divide by zero.

### Predation

- same-tick damage is simultaneous;
- dead targets create biomass exactly once;
- overkill policy is explicitly tested;
- attackers cannot target themselves;
- attack range uses the settled topology.

### Lifecycle

- stable IDs never collide after compaction or restore;
- parent IDs remain stable;
- births at capacity fail explicitly;
- compaction copies every component;
- reproduction cannot create negative parent energy under the chosen contract.

### Genome/controller

- every trait remains within its documented domain;
- controller dimensions are versioned;
- mutation at zero strength is an exact copy;
- crossover is deterministic under a fixed stream;
- no controller output is non-finite.

### Performance

- no steady-state per-tick allocations in the headless path after warm-up;
- 1k-agent and 10k-agent BenchmarkDotNet cases;
- fields-only and observation-only benchmarks;
- detailed metrics off/on deltas;
- population-density sweeps rather than one uniform random case.

## Edge cases

- total extinction;
- population at exact capacity;
- all organisms in one cell;
- no plants and no carrion;
- all-water or all-land world;
- zero seasonal amplitude;
- thermal optimum at boundaries;
- extreme sensor ranges;
- identical organisms and coincident positions;
- lineage archive truncation;
- species/cluster extinction and later reappearance;
- plague with transmission zero;
- simultaneous reproduction claims on one mate;
- child placement across a boundary;
- resource demand underflow/overflow;
- very small and very large body sizes;
- field resolution not dividing world extent;
- dirty-worktree and git-commit provenance unavailable.

## Research trails

### Coevolutionary arms races

Sweep predation lethality and mutation rate. Measure body size, speed, defence, camouflage, predator/prey ratios and extinction probability across seeds.

### Island radiation

Use fragmented terrain and varying water-crossing cost. Measure migration, genome distance, habitat specialization and lineage branching.

### Disease evolution

Make transmission and virulence pathogen traits. Coevolve host immunity, pathogen spread and metabolic cost. Preserve separate RNG streams and explicit epidemiological assumptions.

### Niche construction

Allow organisms to alter fertility, plant distribution or water access. This tests feedback from policies into the environmental substrate.

### Sexual selection

Add mate-choice observations and display traits. Compare ecological fitness against reproductive success and track runaway selection.

### Sociality and collective defence

Add bounded alarm or aggregation intents. Measure whether kin-biased coordination evolves under predation pressure.

### Catastrophe resilience

Apply drought, winter, plague or local extinction at a recorded tick. Measure recovery time, functional diversity, lineage survival and hysteresis.

### Trophic-network emergence

Replace the scalar diet axis with multiple resource/prey channels. Infer a food web from observed energy transfers rather than declaring fixed species roles.

### Lifetime learning

Add a deliberately bounded plastic controller or reinforcement rule. Compare genetic adaptation, lifetime adaptation and their interaction without conflating them.

## Bottom line

Ecogenesis should influence the architecture by revealing reusable contracts, not by becoming the architecture itself. Its strongest contribution is proving that the project’s reusable core must sit above boids-level mechanics: reproducible experiments, bounded state transitions, named randomness, metrics, artifacts and checkpoints.
