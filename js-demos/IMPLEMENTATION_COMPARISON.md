# Implementation Comparison: JavaScript and C# Boids

> [`../PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified
> implementation, test, and performance state.

This document was reconciled against the code on 2026-07-25. It is a compact qualitative reference,
not a promise that the independent browser demo and either C# path will remain feature-identical.

## Executable Paths

| Path | Current role | Evidence |
|------|--------------|----------|
| JavaScript boids demo | Standalone browser teaching/prototyping demo | `js-demos/boids-basic/index.html:386-542` |
| C# legacy | Default renderer and current BenchmarkDotNet target | `SwarmSim.Render/Program.cs:449-471`; `SwarmSim.Benchmarks/WorldTickBenchmarks.cs:12-53` |
| C# canonical | Opt-in, single-group future path via `--canonical` | `SwarmSim.Render/Program.cs:458-462,1707-1723`; `SwarmSim.Core/Canonical/CanonicalWorld.cs:80-100` |

## Source-Backed Behavior Comparison

| Fact | JavaScript demo | C# legacy | C# canonical | Evidence |
|------|-----------------|------------|---------------|----------|
| Neighbor candidates | Each rule scans the boid collection directly. | `SenseSystem` queries a rebuilt uniform grid. | Renderer injects `GridSpatialIndex`; `NaiveSpatialIndex` also exists as a reference implementation. | `js-demos/boids-basic/index.html:398-448`; `SwarmSim.Core/World.cs:110-112,299`; `SwarmSim.Render/Program.cs:1707-1712`; `SwarmSim.Core/Canonical/NaiveSpatialIndex.cs:3-24` |
| Field of view | No angle filter in the separation/alignment/cohesion loops. | Binary field-of-view filter after same-group filtering. | Field-of-view filtering also emits continuous neighbor weights. | `js-demos/boids-basic/index.html:398-448`; `SwarmSim.Core/Systems/SenseSystem.cs:147-193`; `SwarmSim.Core/Canonical/CanonicalWorld.cs:135-149,395-451` |
| Separation | Normalized away direction with linear falloff divided by distance. | Normalized away direction with bounded linear falloff; not inverse-square aggregation. | Linear falloff multiplied by inverse distance and the FOV weight. | `js-demos/boids-basic/index.html:398-417`; `SwarmSim.Core/Systems/SenseSystem.cs:204-219`; `SwarmSim.Core/Canonical/Rules/SeparationRule.cs:30-50` |
| Alignment/cohesion | Average neighbor velocity/position, then steer toward the desired velocity. | Sense aggregates feed `BehaviorSystem` steering. | Dedicated rules compute FOV-weighted averages. | `js-demos/boids-basic/index.html:421-462`; `SwarmSim.Core/Systems/BehaviorSystem.cs:159-205`; `SwarmSim.Core/Canonical/Rules/AlignmentRule.cs:14-43`; `SwarmSim.Core/Canonical/Rules/CohesionRule.cs:14-42` |
| Speed/turn model | Velocity is renormalized to `targetSpeed` after steering. | Friction applies only in `Damped` mode; the active renderer configuration uses `ConstantSpeed`, skips friction, then constrains speed. | Target-derived speed with priority-mode separation droop plus an angular turn-rate limiter. | `js-demos/boids-basic/index.html:467-485`; `SwarmSim.Core/Systems/IntegrateSystem.cs:43-78`; `SwarmSim.Render/Program.cs:245-249`; `SwarmSim.Core/Canonical/CanonicalWorld.cs:297-336` |
| Collision response | Separation reacts to current positions; no look-ahead pass exists. | Separation/crowding steering only; Phase 3 combat is not installed. | Whisker look-ahead, priority hysteresis, and shaped avoidance contribute steering; they are not a collision-free guarantee. | `js-demos/boids-basic/index.html:398-417`; `SwarmSim.Core/World.cs:122-144`; `SwarmSim.Core/Canonical/CanonicalWorld.cs:151-178,207-234,302-324` |
| Group semantics | One undivided boid collection. | Perception excludes other groups. | `Boid` stores a group, but the renderer creates only default-group boids and multi-group semantics remain incomplete. | `js-demos/boids-basic/index.html:386-472`; `SwarmSim.Core/Systems/SenseSystem.cs:147-180`; `SwarmSim.Core/Canonical/Boid.cs:3-24`; `SwarmSim.Core/Canonical/CanonicalWorld.cs:80-100`; `SwarmSim.Render/Program.cs:1707-1723`; `PROJECT_STATUS.md:52-60,597-605` |
| Allocation evidence | Unmeasured. | Unmeasured; no enforced allocation gate exists. | `Step()` refreshes a perception snapshot that allocates three result arrays. | `PROJECT_STATUS.md:58-60`; `SwarmSim.Core/Canonical/CanonicalWorld.cs:103-342,496-508` |

## Performance Evidence

Renderer FPS has not been measured for any of these three paths. JavaScript throughput and canonical
throughput are also **unmeasured**. The repository's only current comparable sample is the legacy
core timing category, captured on 2026-07-25 with:

```powershell
dotnet build SwarmingLilMen.sln --configuration Release
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj --configuration Release --no-build --filter "Category=Performance" --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true
```

| Measurement | 2026-07-25 result | Interpretation | Evidence |
|-------------|-------------------|----------------|----------|
| Legacy 1k tick | 0.172 ms/tick (5,801 operations/second) | One local simulation sample, not renderer FPS | `PROJECT_STATUS.md:29-48` |
| Legacy 10k tick | 8.839 ms/tick (113.1 operations/second) | One local simulation sample | `PROJECT_STATUS.md:29-48` |
| Legacy 50k tick | 162.815 ms/tick (6.14 operations/second) | The 16.67 ms/tick target is unmet | `PROJECT_STATUS.md:29-48` |
| Legacy 50k grid rebuild | 0.102 ms | Grid-only cost, not a full tick | `PROJECT_STATUS.md:29-48` |
| JavaScript renderer/core | Unmeasured | The UI displays instantaneous FPS, but this comparison records no dated result | `js-demos/boids-basic/index.html:306-307,641-644,666` |
| C# legacy renderer FPS | Unmeasured | The core timing test does not render | `SwarmSim.Tests/PerformanceTests.cs:15-52` |
| C# canonical core/renderer | Unmeasured | No canonical BenchmarkDotNet comparison exists | `SwarmSim.Benchmarks/WorldTickBenchmarks.cs:12-53`; `PROJECT_STATUS.md:52-60` |

## Choosing a Path

- Use the JavaScript demo for browser-local teaching and rapid visual experimentation.
- Use the legacy C# path when comparing with the current renderer, tests, and benchmark suite.
- Use the canonical C# path for new steering work, while preserving its current single-group and
  unmeasured-performance limitations.

Subjective judgments such as which path “looks better” require a visual comparison and are not
encoded here as facts. Performance, collision quality, and allocation claims require their own
dated measurements rather than inference from data structures or algorithm names.
