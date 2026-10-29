# Implementation Comparison: JavaScript and C# Boids

> [`../PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified
> implementation, test, and performance state.

This document was reconciled against the code on 2026-10-29. It is a compact qualitative reference,
not a promise that the independent browser demo and either C# path will remain feature-identical.

## Executable Paths

| Path | Current role | Evidence |
|------|--------------|----------|
| JavaScript boids demo | Standalone browser teaching/prototyping demo | `js-demos/boids-basic/index.html` (`Boid`, `animate`) |
| C# legacy | Default renderer; selectable headless engine; legacy tick/grid benchmarks | `SwarmSim.Render/Program.cs` (`Program.Main`); `SwarmSim.Cli/RunOptions.cs` (`RunOptions.Parse`); `SwarmSim.Benchmarks/WorldTickBenchmarks.cs` (`WorldTickBenchmarks`, `Setup`) |
| C# canonical | Default headless engine; opt-in single-group renderer via `--canonical`; dedicated full-step benchmarks | `SwarmSim.Cli/RunOptions.cs` (`RunOptions.Engine`); `SwarmSim.Render/Program.cs` (`Program.RunCanonicalMode`, `Program.CreateCanonicalWorld`); `SwarmSim.Benchmarks/CanonicalWorldStepBenchmarks.cs` (`CanonicalWorldStepBenchmarks`) |

## Source-Backed Behavior Comparison

| Fact | JavaScript demo | C# legacy | C# canonical | Evidence |
|------|-----------------|------------|---------------|----------|
| Neighbor candidates | Each rule scans the boid collection directly. | `SenseSystem` queries a rebuilt uniform grid. | Renderer injects `GridSpatialIndex`; `NaiveSpatialIndex` also exists as a reference implementation. | `js-demos/boids-basic/index.html` (`separate`, `align`, `cohere`); `SwarmSim.Core/World.cs` (`World.Grid`, `World.Tick`); `SwarmSim.Render/Program.cs` (`Program.CreateCanonicalWorld`, `GridSpatialIndex`); `SwarmSim.Core/Canonical/NaiveSpatialIndex.cs` (`NaiveSpatialIndex`) |
| Field of view | No angle filter in the separation/alignment/cohesion loops. | Binary field-of-view filter after same-group filtering. | Field-of-view filtering also emits continuous neighbor weights. | `js-demos/boids-basic/index.html` (`separate`, `align`, `cohere`); `SwarmSim.Core/Systems/SenseSystem.cs` (`SenseSystem.Run`); `SwarmSim.Core/Utils/MathUtils.cs` (`MathUtils.IsWithinFieldOfView`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.FilterByFieldOfView`, `CanonicalWorld.QueryVisibleNeighbors`) |
| Separation | Normalized away direction with linear falloff divided by distance. | Normalized away direction with bounded linear falloff; not inverse-square aggregation. | Linear falloff multiplied by inverse distance and the FOV weight. | `js-demos/boids-basic/index.html` (`separate`); `SwarmSim.Core/Systems/SenseSystem.cs` (`SenseSystem.Run`); `SwarmSim.Core/Canonical/Rules/SeparationRule.cs` (`SeparationRule`) |
| Alignment/cohesion | Average neighbor velocity/position, then steer toward the desired velocity. | Sense aggregates feed `BehaviorSystem` steering. | Dedicated rules compute FOV-weighted averages. | `js-demos/boids-basic/index.html` (`align`, `cohere`); `SwarmSim.Core/Systems/BehaviorSystem.cs` (`BehaviorSystem.Run`); `SwarmSim.Core/Canonical/Rules/AlignmentRule.cs` (`AlignmentRule`); `SwarmSim.Core/Canonical/Rules/CohesionRule.cs` (`CohesionRule`) |
| Speed/turn model | Velocity is renormalized to `targetSpeed` after steering. | Friction applies only in `Damped` mode; the active renderer configuration uses `ConstantSpeed`, skips friction, then constrains speed. | Target-derived speed with priority-mode separation droop plus an angular turn-rate limiter. | `js-demos/boids-basic/index.html` (`flock`, `update`); `SwarmSim.Core/Systems/IntegrateSystem.cs` (`IntegrateSystem.Run`); `SwarmSim.Render/Program.cs` (`Program.Main`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.Step`) |
| Collision response | Separation reacts to current positions; no look-ahead pass exists. | A highest-priority hard override steers away inside `CollisionAvoidanceRadius` and consumes up to the remaining budget; separation/crowding steering runs only if budget remains. Phase 3 combat is not installed. | Whisker look-ahead, priority hysteresis, and shaped avoidance contribute steering; they are not a collision-free guarantee. | `js-demos/boids-basic/index.html` (`separate`); `SwarmSim.Core/Systems/BehaviorSystem.cs` (`BehaviorSystem.Run`, `CollisionAvoidanceRadius`); `SwarmSim.Render/Program.cs` (`Program.Main`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.Step`) |
| Group semantics | One undivided boid collection. | Perception excludes other groups. | `Boid` stores a group, but the renderer creates only default-group boids and multi-group semantics remain incomplete. | `js-demos/boids-basic/index.html` (`Boid`, `flock`); `SwarmSim.Core/Systems/SenseSystem.cs` (`SenseSystem.Run`); `SwarmSim.Core/Canonical/Boid.cs` (`Boid`); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld`); `SwarmSim.Render/Program.cs` (`Program.CreateCanonicalWorld`); [current status](../PROJECT_STATUS.md) |
| Allocation evidence | No dated allocation measurement recorded here. | Legacy tick/grid benchmarks are separate workloads; their evidence does not establish renderer allocations. | `Step()` no longer allocates a snapshot. Bounded regressions cover stepping and explicit reusable telemetry; the compatibility snapshot adapter still allocates three arrays after the first tick. | [current measurements and limits](../PROJECT_STATUS.md#performance-evidence-and-its-limits); `SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.Step`, `CanonicalWorld.CaptureTelemetryInto`, `CanonicalWorld.CapturePerceptionSnapshot`); `SwarmSim.Tests/CanonicalTelemetryTests.cs` (`StepAndExplicitReusableCaptureDoNotAllocateOverOneThousandWarmTicks`) |

## Performance Evidence

Current C# throughput and allocation evidence, workload definitions, runtime details and measurement
caveats live in [PROJECT_STATUS.md](../PROJECT_STATUS.md#performance-evidence-and-its-limits).
Canonical full-step BenchmarkDotNet cases now exist at 1k, 10k and 50k agents with telemetry capture
off. Run them explicitly:

```powershell
dotnet build SwarmingLilMen.sln --configuration Release
dotnet run --project SwarmSim.Benchmarks --configuration Release --no-build -- --canonical
```

The earlier legacy timing table has been superseded as current evidence. Historical legacy samples
and phase claims are preserved in the
[archived status record](../docs/archive/PROJECT_STATUS_BEFORE_2026-10-29.md). They are not
measurements of the current revision and should not be compared with newer canonical results as
though the workload, host and code were controlled.

Full renderer FPS remains unverified for both C# paths. This comparison also records no dated
JavaScript throughput or allocation benchmark; an instantaneous FPS display is not a retained
comparison. The interactive 50k/60 FPS target remains unmet. The canonical BenchmarkDotNet results
and zero-allocation regressions have distinct measurement boundaries and do not establish a
universal zero-allocation promise for rendering or arbitrary rules.

For telemetry, reuse a caller-owned `CanonicalTelemetry` and call
`CanonicalWorld.CaptureTelemetryInto` explicitly. The renderer follows that path once per frame
through `Program.CaptureCanonicalTelemetry`. Calling the older `CapturePerceptionSnapshot`
adapter intentionally allocates an independent snapshot; those allocations are no longer an
automatic side effect of every `Step()`.

## Choosing a Path

- Use the JavaScript demo for browser-local teaching and rapid visual experimentation.
- Use the legacy C# path when comparing with the default renderer and legacy tick/grid suites.
- Use the canonical C# path for new steering work, while preserving its current single-group and
  renderer-acceptance limitations. Use the standalone CLI for recorded fixed-tick runs and the
  canonical benchmark route for scoped core measurements.

Subjective judgments such as which path “looks better” require a visual comparison and are not
encoded here as facts. Performance, collision quality, and allocation claims require their own
dated measurements rather than inference from data structures or algorithm names.
