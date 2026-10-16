> Imported design reference from the chat demo. Native C# work described below remains a proposal; see this package README for current commands and verification.

# SwarmingLilMen integration notes — Abelian sandpile foundation

## Why this model is strategically useful

The repository's current models are continuous-motion or optimisation systems: boids, Vicsek particles, ACO and PSO, plus the separate firefly oscillator demo. The Abelian sandpile is materially different:

- the state is a discrete lattice field rather than a population of moving agents;
- time is naturally event-driven relaxation rather than force integration under a physical `dt`;
- local threshold events create unbounded cascade work per external drive event;
- open boundaries and internal sinks are first-class dissipation mechanisms;
- the key mathematical property is confluence: any legal toppling order reaches the same stable result.

That makes it a good falsification test for the idea of one universal `CanonicalWorld`. It should reuse experiment, provenance, RNG, metrics and scheduling infrastructure without being forced through boid observations, steering intents or `MaxForce` arbitration.

## Recommended first C# slice

Add a small model package after the experiment spine exists:

```text
SwarmSim.Core/
  Experiments/
  Metrics/
  Random/
SwarmSim.Models.Sandpile/
  SandpileConfig.cs
  SandpileState.cs
  SandpileRunner.cs
  SandpileMetrics.cs
  TopplingQueue.cs
SwarmSim.Cli/
```

The model-specific surface can remain small:

```csharp
public sealed record SandpileConfig(
    int Width,
    int Height,
    int Threshold,
    BoundaryKind Boundary,
    uint Seed);

public sealed class SandpileState
{
    public required int[] Heights { get; init; }
    public required bool[] Sinks { get; init; }
    public long GrainsAdded { get; internal set; }
    public long GrainsDissipated { get; internal set; }
}

public readonly record struct AvalancheResult(
    long Topplings,
    int Area,
    int RelaxationDepth,
    long Dissipated,
    int MaxQueueDepth);
```

A single experiment event is:

```text
drive grain(s)
→ enqueue unstable cells
→ relax until stable
→ sample deterministic scientific metrics
→ append one avalanche record
```

Do not map one toppling to the engine's fixed physical timestep. A runner can expose both `DriveAndRelax()` and a diagnostic `ProcessTopplings(maxCount)` for animation.

## Reusable contracts

The sandpile should reuse:

- versioned experiment manifests;
- deterministic run identifiers;
- RNG streams for drive-position selection;
- atomic run directories;
- CSV/JSONL metric sinks;
- run cancellation and operation budgets;
- provenance and state hashes.

It should not initially reuse:

- `Boid`;
- `IRule`;
- steering observations/intents;
- the continuous spatial index;
- force budgets;
- velocity or integration stages.

This distinction is evidence about the correct kernel boundary rather than an implementation failure.

## Acceptance tests

### Exact deterministic properties

1. Same config, seed and drive sequence produces the same final height array and avalanche records.
2. Every completed event leaves every non-sink cell below threshold.
3. Conservation holds exactly:

```text
initial mass + grains added = final mass + grains dissipated
```

4. FIFO, LIFO and randomized legal toppling orders produce the same final stable lattice.
5. An exported state round-trips without changing the state hash.
6. An additional unrelated RNG stream does not perturb the drive-position stream.

### Structural tests

1. A corner topple dissipates exactly two grains with open boundaries.
2. An edge non-corner topple dissipates exactly one grain.
3. A sink absorbs every grain sent to it and never becomes unstable.
4. A threshold-minus-one cell remains stable; adding one grain makes it unstable.
5. Queue capacity growth, if allowed, does not change toppling order or scientific results.

### Statistical evidence

Avalanche-size distributions should be reported, not used as a narrow per-run CI equality gate. Publication-grade claims require burn-in, independent seeds, finite-size scaling, explicit lower-cutoff selection, goodness-of-fit testing and comparison against alternative heavy-tailed distributions.

## Metrics

Foundation-agnostic candidates:

- tick/event index;
- operation count;
- elapsed wall time in a separate operational stream;
- allocated bytes in a separate operational stream;
- state hash.

Sandpile-specific probes:

- avalanche topplings;
- unique affected area;
- relaxation depth;
- grains dissipated;
- maximum unstable-queue depth;
- mean lattice height;
- fraction at threshold minus one;
- largest avalanche so far;
- logarithmic size histogram.

Scientific metrics must remain deterministic. Wall time and allocation measurements must not be included in byte-identical scientific artefacts.

## Performance design

A C# implementation can avoid per-toppling allocation:

- preallocate or pool an integer queue;
- use a queued-bit array to avoid duplicate queue entries;
- use an integer visit-stamp array for avalanche area rather than clearing a boolean array each event;
- keep heights in a flat row-major `int[]`;
- process batches of topplings for the renderer while keeping full-relaxation semantics available headlessly.

Benchmark separately:

- topplings per second;
- relaxation cost by grid size and avalanche size;
- queue high-water mark;
- branch behaviour for edge-heavy versus interior-heavy cascades.

## Research and expansion trails

1. Compare deterministic BTW and stochastic Manna redistribution.
2. Measure finite-size scaling across 32, 64, 128 and 256-cell lattices.
3. Compare open, periodic-with-sink and heterogeneous dissipation boundaries.
4. Put the sandpile on arbitrary graphs to test topology contracts.
5. Add slowly changing sink geometry and study adaptation of cascade paths.
6. Compare synchronous wave toppling against sequential legal orders: stable state should match, while diagnostic duration semantics differ.
7. Build a forest-fire or earthquake model on the same event-cascade experiment substrate.
8. Couple local load to a graph or spatial resource field only after each foundation works independently.

## Checkpoint boundary

The browser demo now exports stable checkpoints only. A live avalanche contains a mutable toppling queue, queue cursor, queued-bit set, visit stamps, and current avalanche accumulator; omitting those while calling the file a complete checkpoint would be false. The smallest production contract should therefore begin with stable event boundaries:

```text
drive event starts
→ relax completely or record an explicit incomplete outcome
→ stable checkpoint may be emitted
```

A later resumable mid-avalanche checkpoint is possible, but it must version and serialize the complete scheduler state rather than reconstructing a queue from cell heights and hoping to preserve diagnostic depth/order semantics.

## Browser demonstration API

`window.AvalancheLab` exposes reset, drive, settle, bounded toppling processing, sink editing, copied snapshots, and stable checkpoint functions. It exists to make the standalone model testable and scriptable. It is not a proposed C# public API; the C# surface should be designed around the shared experiment contracts available at implementation time.

## Recommended repository decision

Treat this as a post-experiment-spine vertical proof, not as another JavaScript reference that immediately expands the canonical engine. Its main architectural value is demonstrating that the truly reusable layer is likely:

```text
experiment orchestration
+ deterministic randomness
+ artefact/provenance contracts
+ metrics
+ bounded execution
```

rather than one universal continuous-agent world type.
