> Imported design reference from the chat demo. Native C# work described below remains a proposal; see this package README for current commands and verification.

# Integration notes: reaction–diffusion as a SwarmingLilMen foundation

## Recommendation

Treat this as a **field-foundation probe**, not as another canonical boid policy.

The most useful contribution is architectural pressure: it tests whether the emerging experiment spine can run a deterministic system that has no agents, velocities, neighbourhood cap, steering intents, or population metric. If the runner, manifest, metric registry, or snapshot contract assumes those concepts, it is not yet a general emergence experiment spine.

Do not start by creating a large new package hierarchy. The smallest credible slice can live under:

```text
SwarmSim.Core/
  Foundations/
    ReactionDiffusion/
      GrayScottSettings.cs
      GrayScottField.cs
      GrayScottSimulation.cs
      GrayScottMetrics.cs
```

A renderer adapter should follow only after the headless model, tests, manifest, and metrics exist.

## Architectural fit

### Reuse unchanged

- deterministic fixed-step scheduling;
- experiment IDs and resolved manifests;
- run-directory artefacts;
- seed and RNG version metadata;
- metric-probe registration;
- CSV/JSONL sinks;
- sweep expansion and stable row order;
- operational telemetry kept separate from scientific output.

### Widen only when this consumer proves the need

- simulation adapter: must not require an agent count or boid snapshot;
- snapshot/view contract: needs a dense scalar-field view;
- metric context: should expose model-defined views rather than hard-coded kinematics;
- boundary semantics: a reusable 2D sampler may be earned here, but avoid a universal space abstraction until another field model needs it.

### Do not reuse

- `IRule`;
- boid `Observation` / steering `Intent` types;
- `MaxForce` arbitration;
- `ISpatialIndex<Boid>`;
- agent lifecycle, group, health, energy, or genome storage.

## Minimal headless contract

A model-neutral experiment adapter can remain small:

```csharp
public interface ISimulationSession
{
    string ModelId { get; }
    ulong Tick { get; }
    double SimulatedTime { get; }
    void Step();
}

public interface IFieldView2D<T> where T : unmanaged
{
    int Width { get; }
    int Height { get; }
    ReadOnlySpan<T> Values { get; }
}
```

The reaction–diffusion session can expose two named field views without making the generic runner understand their chemistry:

```csharp
public interface IGrayScottView
{
    IFieldView2D<float> SubstrateA { get; }
    IFieldView2D<float> ActivatorB { get; }
}
```

Metric probes should depend on `IGrayScottView` through registration or a typed adapter, not through conditionals inside the runner.

## Data model

Use four reusable arrays:

```csharp
private float[] _a;
private float[] _b;
private float[] _nextA;
private float[] _nextB;
```

Per iteration:

1. sample the current buffers only;
2. compute the 9-point Laplacian;
3. compute reaction/feed/kill terms;
4. write the next buffers;
5. swap the pairs;
6. increment the deterministic tick.

No allocation is required after construction.

Recommended first settings surface:

```csharp
public sealed record GrayScottSettings
{
    public int Width { get; init; } = 220;
    public int Height { get; init; } = 140;
    public float DiffusionA { get; init; } = 1.0f;
    public float DiffusionB { get; init; } = 0.5f;
    public float Feed { get; init; } = 0.029f;
    public float Kill { get; init; } = 0.057f;
    public float DeltaTime { get; init; } = 1.0f;
    public FieldBoundary Boundary { get; init; } = FieldBoundary.Toroidal;
    public uint Seed { get; init; } = 24_051_995;
    public InitialPattern InitialPattern { get; init; } = InitialPattern.Speckle;
}
```

Validate every numeric field with `float.IsFinite`. Width × height must have an explicit upper bound before allocation.

## Numeric choices

The browser demo clamps concentrations to `[0,1]` after each explicit Euler step. That is robust for an interactive lab but is a modelling choice, not a mathematically neutral implementation detail.

The C# implementation should record:

- integrator: explicit Euler v1;
- stencil: center `-1`, cardinal `0.2`, diagonal `0.05`;
- concentration policy: clamp, reject, or instrument out-of-range values;
- float width: binary32;
- boundary policy;
- RNG algorithm/version;
- update discipline: synchronous double-buffering.

Changing any of these is a model-version change and can legitimately invalidate golden outputs.

## Scientific metrics

### Foundation-agnostic candidates

These can be expressed for many dense-field simulations:

- scalar mean;
- variance;
- minimum and maximum;
- histogram entropy;
- occupied-area fraction above a declared threshold;
- mean local gradient magnitude;
- finite/non-finite count.

### Gray–Scott-specific candidates

- mean reaction rate `A·B²`;
- activator mass;
- connected-component count at a declared B threshold;
- component area distribution;
- interface length estimate;
- radial spectrum or dominant wavelength;
- temporal autocorrelation;
- morphology persistence under perturbation.

Do not call the simple UI label a phase classifier. A scientific morphology classifier needs defined features, calibration data, and validation.

## Required tests

### Exact deterministic tests

- same settings and seed produce identical A/B bit patterns after N steps;
- state export/import round-trips byte-identically;
- initial pattern construction has golden RNG and state vectors;
- synchronous update is pinned against an in-place mutation mutant;
- adding an unrelated named RNG stream does not change initialization;
- a homogeneous `A=1, B=0` field remains a fixed point;
- no non-finite values for every shipped preset over a declared horizon;
- `Step()` allocates zero bytes after warm-up.

### Boundary and stencil tests

- a toroidal translated field evolves into the equivalently translated result;
- a perturbation at the left edge influences the right edge only in toroidal mode;
- sealed mode duplicates edge samples consistently with the chosen no-flux approximation;
- the stencil weights sum to zero;
- a constant field has zero discrete Laplacian;
- rectangular and very small fields are covered explicitly.

### Behavioural envelopes

Use these only after measuring distributions across seeds and platforms:

- Maze preset reaches non-zero variance and occupied area after a declared horizon;
- clear field remains homogeneous;
- a chemical perturbation increases edge density before relaxing;
- changing feed/kill across a small sweep produces distinguishable terminal morphology metrics.

Avoid locking complete large-field hashes as the only behavioural test. They are sensitive to legitimate numeric changes and do not explain what regressed.

## Experiment-manifest extension

A model-specific block is preferable to growing `SimConfig`:

```json
{
  "schemaVersion": 1,
  "model": "gray-scott",
  "seed": 24051995,
  "ticks": 5000,
  "modelConfig": {
    "width": 220,
    "height": 140,
    "diffusionA": 1.0,
    "diffusionB": 0.5,
    "feed": 0.029,
    "kill": 0.057,
    "deltaTime": 1.0,
    "boundary": "toroidal",
    "initialPattern": "speckle"
  },
  "metrics": [
    "field.b.mean",
    "field.b.variance",
    "field.b.active_fraction",
    "field.b.edge_density",
    "field.b.entropy"
  ]
}
```

The resolved manifest should also carry the exact integrator, stencil, clamping, generator, and model-version identifiers.

## Suggested implementation order

1. Add settings validation and a headless `GrayScottSimulation`.
2. Add deterministic initial-pattern generation.
3. Add exact stencil, fixed-point, boundary, and replay tests.
4. Adapt it to the shared experiment-session contract.
5. Add core field metrics and CSV output.
6. Add one serial sweep across feed and kill.
7. Only then add a renderer using the existing Raylib project or a separate browser demo link.

This sequence gives the project a useful result even if graphical integration is deferred.

## Relationship to ACO issue #30

Reaction–diffusion and ACO both use fields, but they prove different things:

- **ACO** proves bidirectional coupling between agents, resources, and a deposited/evaporating field.
- **Gray–Scott** proves that the runner and observability stack can support a field as the entire simulation state.

Two reasonable orderings exist:

1. **After ACO:** reuse the scalar-field storage and scheduler phase earned by the ACO slice.
2. **Before ACO as a bounded spike:** use Gray–Scott to design and benchmark a dense double-buffered field without simultaneously solving ant state, resources, sensors, and stochastic policy behaviour.

Recommendation: implement it after the Wave-2 experiment spine but before committing to a generalized field interface. Build one concrete field type first; allow ACO to determine what is genuinely common.

## Expansion trails

- produce a deterministic feed/kill morphology atlas;
- locate bifurcation boundaries with sweep-derived metrics;
- compare explicit Euler, Runge–Kutta, and semi-implicit integration;
- add spatially varying feed/kill fields;
- couple moving agents to nutrient depletion or activator deposition;
- evolve local reaction parameters and study selection over morphology;
- use inverse search to find parameters matching a target image descriptor;
- extend to three dimensions and volume rendering;
- compare Gray–Scott against FitzHugh–Nagumo and activator–inhibitor systems through the same field experiment surface;
- use dynamic masks to study growth around wounds or obstacles.

## Risks and edge cases

- large `DeltaTime` values can produce numerical instability;
- clamping hides some instability and changes the model;
- GPU implementations may not match CPU binary32 order exactly;
- colour palettes can make visually different concentrations look similar;
- connected-component metrics depend heavily on threshold and topology;
- toroidal topology can create apparent structures that cross opposite display edges;
- sealed-edge approximations need an explicit mathematical definition;
- a full field in JSON is large, so production artefacts should use a compact versioned binary format rather than decimal arrays;
- parameter names `F` and `K` should not be mixed with steering force terminology in shared schemas.
