# Shared simulation mechanisms

The engine is growing by extracting mechanisms that distinct models actually use. The first
shared kernel mechanisms are an ordered synchronous phase runner and typed double-buffered
state. They complement the existing experiment registry, named random streams, metrics,
manifests, sweeps and reports. [Epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10)
remains the ordering authority; [#223](https://github.com/Chris0Jeky/SwarmingLilMen/issues/223)
tracks this bounded implementation.

## Why these two mechanisms

Vicsek headings must all read the preceding population before any new heading becomes visible.
ACO ants must all read the preceding pheromone field before their deposits are applied. These
are different scientific rules with a common need: explicit order and publication boundaries.
Neither needs to pretend that its update is a boids steering force.

`SimulationSchedule` owns the phase order. `DoubleBufferedState<T>` owns two generations of
arbitrary struct state. A model owns the meaning of that state and decides when to commit it.
The implementation uses the existing .NET 8 stack and adds no package dependency.

| Consumer | Ordered phases | State visibility |
| --- | --- | --- |
| Vicsek | `index`, `agents`, `commit` | Rebuild from current agents; compute every next agent from that same generation; publish all agents together. Positions still advance with the old velocity. |
| ACO | `agents`, `field` | Ants read field(t), buffer deposits, and retain stable food-contention order. The field phase deposits, evaporates and advances the tick. |

Legacy and canonical boids keep their existing update implementations. These mechanisms do not
establish that all foundations share a complete kernel, spatial representation or renderer.

## Public contracts

```csharp
var state = new DoubleBufferedState<Particle>(initialParticles);
var schedule = new SimulationSchedule(
    new SimulationPhase("compute", ComputeNext),
    new SimulationPhase("commit", state.Commit));

void ComputeNext()
{
    ReadOnlySpan<Particle> current = state.Current;
    Span<Particle> next = state.Next;
    for (int i = 0; i < state.Count; i++)
        next[i] = AdvanceFromOldState(current, i);
}

schedule.Run();
```

The example's `Particle` and `AdvanceFromOldState` are model-owned. Phase registration is copied
at construction, names are unique and case-sensitive, and inspection exposes a read-only list.
`Run()` executes each retained action once in that order. Instances are single-threaded;
recursive execution throws before a nested phase can run. There is no task scheduler, implicit
parallelism, dependency sorting, wall clock, timestep choice or hidden random draw.

If an action throws, later phases do not run and the original exception propagates. Earlier
writes remain. The runner releases its recursion guard, but it does not rewind a world or make
retry safe. A model that can fail mid-step must define its own recovery. In particular ACO's
existing public `AgentPhase()` / `FieldPhase()` alternation guards remain in effect.

The double buffer copies its initial span. `Current` is read-only; the compute phase must write
every `Next` slot. `Commit()` swaps the arrays without copying or clearing: the next write buffer
then contains the old generation. Borrowed spans must not be retained across commits. Structs
containing references are allowed, but reference targets are not deep-copied or made immutable.
Empty buffers are supported for future field-only models; the current CLI still requires a
positive population. Construction allocates; neither `Run()` nor `Commit()` allocates by design.

## Verification and limits

`SimulationKernelTests` checks ordered dependent writes, defensive registration, duplicate and
invalid phases, exceptions, recursion, generation publication, initial-state ownership, empty
state, warm managed allocation and scheduled ACO versus explicit public phases. Existing
Vicsek platform-specific golden fixtures and ACO analytical/locked regressions remain unchanged.

Run the narrow check after rebuilding:

```sh
dotnet test SwarmSim.Tests -c Release --filter "FullyQualifiedName~SimulationKernelTests|FullyQualifiedName~Vicsek|FullyQualifiedName~AntColony" -- RunConfiguration.TreatNoTestsAsError=true
```

Then use the full ordinary suite and behavior gallery from `CLAUDE.md`. A passing allocation
check does not establish throughput or remove the existing performance qualification issues.
The next reuse proof is richer oscillator state on these same mechanisms. General topology,
multi-field evolution, coupled foundations, dynamic lifecycle and checkpoint restore remain
separate design questions with their own acceptance evidence.
