# Implementation Comparison: JavaScript and C# Boids

> [`../PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified
> implementation, test, and performance state.

This document was reconciled against the code on 2026-07-25. It is a compact qualitative reference,
not a promise that the independent browser demo and either C# path will remain feature-identical.

## Executable Paths

| Path | Current role | Evidence |
|------|--------------|----------|
| JavaScript boids demo | Standalone browser teaching/prototyping demo | `js-demos/boids-basic/index.html` (`Boid`, `animate`) |
| C# legacy | Default renderer and current BenchmarkDotNet target | `SwarmSim.Render/Program.cs` (`Program.Main`); `SwarmSim.Benchmarks/WorldTickBenchmarks.cs` (`WorldTickBenchmarks`, `Setup`) |
| C# canonical | Opt-in, single-group future path via `--canonical` | `SwarmSim.Render/Program.cs` (`Program.RunCanonicalMode`, `Program.CreateCanonicalWorld`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld`) |

## Source-Backed Behavior Comparison

| Fact | JavaScript demo | C# legacy | C# canonical | Evidence |
|------|-----------------|------------|---------------|----------|
| Neighbor candidates | Each rule scans the boid collection directly. | `SenseSystem` queries a rebuilt uniform grid. | Renderer injects `GridSpatialIndex`; `NaiveSpatialIndex` also exists as a reference implementation. | `js-demos/boids-basic/index.html` (`separate`, `align`, `cohere`); `SwarmSim.Core/World.cs` (`World.Grid`, `World.Tick`); `SwarmSim.Render/Program.cs` (`Program.CreateCanonicalWorld`, `GridSpatialIndex`); `SwarmSim.Core/Canonical/NaiveSpatialIndex.cs` (`NaiveSpatialIndex`) |
| Field of view | No angle filter in the separation/alignment/cohesion loops. | Binary field-of-view filter after same-group filtering. | Field-of-view filtering also emits continuous neighbor weights. | `js-demos/boids-basic/index.html` (`separate`, `align`, `cohere`); `SwarmSim.Core/Systems/SenseSystem.cs` (`SenseSystem.Run`); `SwarmSim.Core/Utils/MathUtils.cs` (`MathUtils.IsWithinFieldOfView`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.FilterByFieldOfView`, `CanonicalWorld.QueryVisibleNeighbors`) |
| Separation | Normalized away direction with linear falloff divided by distance. | Normalized away direction with bounded linear falloff; not inverse-square aggregation. | Linear falloff multiplied by inverse distance and the FOV weight. | `js-demos/boids-basic/index.html` (`separate`); `SwarmSim.Core/Systems/SenseSystem.cs` (`SenseSystem.Run`); `SwarmSim.Core/Canonical/Rules/SeparationRule.cs` (`SeparationRule`) |
| Alignment/cohesion | Average neighbor velocity/position, then steer toward the desired velocity. | Sense aggregates feed `BehaviorSystem` steering. | Dedicated rules compute FOV-weighted averages. | `js-demos/boids-basic/index.html` (`align`, `cohere`); `SwarmSim.Core/Systems/BehaviorSystem.cs` (`BehaviorSystem.Run`); `SwarmSim.Core/Canonical/Rules/AlignmentRule.cs` (`AlignmentRule`); `SwarmSim.Core/Canonical/Rules/CohesionRule.cs` (`CohesionRule`) |
| Speed/turn model | Velocity is renormalized to `targetSpeed` after steering. | Friction applies only in `Damped` mode; the active renderer configuration uses `ConstantSpeed`, skips friction, then constrains speed. | Target-derived speed with priority-mode separation droop plus an angular turn-rate limiter. | `js-demos/boids-basic/index.html` (`flock`, `update`); `SwarmSim.Core/Systems/IntegrateSystem.cs` (`IntegrateSystem.Run`); `SwarmSim.Render/Program.cs` (`Program.Main`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.Step`) |
| Collision response | Separation reacts to current positions; no look-ahead pass exists. | A highest-priority hard override steers away inside `CollisionAvoidanceRadius` and consumes up to the remaining budget; separation/crowding steering runs only if budget remains. Phase 3 combat is not installed. | Whisker look-ahead, priority hysteresis, and shaped avoidance contribute steering; they are not a collision-free guarantee. | `js-demos/boids-basic/index.html` (`separate`); `SwarmSim.Core/Systems/BehaviorSystem.cs` (`BehaviorSystem.Run`, `CollisionAvoidanceRadius`); `SwarmSim.Render/Program.cs` (`Program.Main`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.Step`) |
| Group semantics | One undivided boid collection. | Perception excludes other groups. | `Boid` stores a group, but the renderer creates only default-group boids and multi-group semantics remain incomplete. | `js-demos/boids-basic/index.html` (`Boid`, `flock`); `SwarmSim.Core/Systems/SenseSystem.cs` (`SenseSystem.Run`); `SwarmSim.Core/Canonical/Boid.cs` (`Boid`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld`); `SwarmSim.Render/Program.cs` (`Program.SelectCanonicalTracked`); `PROJECT_STATUS.md` (`Canonical Boids Implementation (Post-P2 Rewrite)`) |
| Allocation evidence | Unmeasured. | Unmeasured; no enforced allocation gate exists. | `Step()` refreshes a perception snapshot that allocates three result arrays. | `PROJECT_STATUS.md` (`Verified Live State (2026-08-08)`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.Step`, `CanonicalWorld.CapturePerceptionSnapshot`, `PerceptionSnapshot`) |

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
| Legacy 1k tick | 0.172 ms/tick (5,801 operations/second) | One local simulation sample, not renderer FPS | `PROJECT_STATUS.md` (`Verified Live State (2026-08-08)`) |
| Legacy 10k tick | 8.839 ms/tick (113.1 operations/second) | One local simulation sample | `PROJECT_STATUS.md` (`Verified Live State (2026-08-08)`) |
| Legacy 50k tick | 162.815 ms/tick (6.14 operations/second) | The 16.67 ms/tick target is unmet | `PROJECT_STATUS.md` (`Verified Live State (2026-08-08)`) |
| Legacy 50k grid rebuild | 0.102 ms | Grid-only cost, not a full tick | `PROJECT_STATUS.md` (`Verified Live State (2026-08-08)`) |
| JavaScript renderer/core | Unmeasured | The UI displays instantaneous FPS, but this comparison records no dated result | `js-demos/boids-basic/index.html` (`animate`, `fps`) |
| C# legacy renderer FPS | Unmeasured | The core timing test does not render | `SwarmSim.Tests/PerformanceTests.cs` (`PerformanceTests`, `Tick_With50kAgents_StaysWithinScalingEnvelope`) |
| C# canonical core/renderer | Unmeasured | No canonical BenchmarkDotNet comparison exists | `SwarmSim.Benchmarks/WorldTickBenchmarks.cs` (`WorldTickBenchmarks`, `Setup`); `PROJECT_STATUS.md` (`Verified Live State (2026-08-08)`) |

## Choosing a Path

- Use the JavaScript demo for browser-local teaching and rapid visual experimentation.
- Use the legacy C# path when comparing with the current renderer, tests, and benchmark suite.
- Use the canonical C# path for new steering work, while preserving its current single-group and
  unmeasured-performance limitations.

Subjective judgments such as which path “looks better” require a visual comparison and are not
encoded here as facts. Performance, collision quality, and allocation claims require their own
dated measurements rather than inference from data structures or algorithm names.
