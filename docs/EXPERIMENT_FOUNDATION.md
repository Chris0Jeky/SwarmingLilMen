# Experiment foundation

The C# engine currently supports two boids implementations. The legacy `World` is a fixed-capacity
structure-of-arrays pipeline; `CanonicalWorld` is the target for new engine work. Neither is yet a
general-purpose dynamics kernel. This guide describes the shared configuration and diagnostics
that experiments can rely on without a renderer.

## Resolve and record configuration

`ConfigurationResolver` applies **defaults → named scenario → file → explicit overrides**. Files
and overrides are sparse layers; matrix values replace the complete matrix. Unknown names in the
new resolver fail instead of silently running a misspelled experiment. The older standalone JSON
loaders retain their permissive unknown-property behavior for compatibility.

```csharp
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Canonical;

var settings = ConfigurationResolver.ResolveCanonicalSettings("balanced");
settings.SaveToJson("resolved-canonical.json");
var roundTrip = CanonicalWorldSettings.LoadFromJson("resolved-canonical.json");
```

Both configuration classes support `FromJson`, `ToJson`, `LoadFromJson` and `SaveToJson`.
`SimConfig.AggressionMatrix` remains a public `float[,]`; a converter writes JSON row arrays so
existing C# callers keep their accessors. JSON cannot represent NaN/infinity. Save validates before
opening the output file. See the [configuration cookbook](CONFIGURATION_COOKBOOK.md) and
[parameter mapping](PARAMETER_GUIDE.md) for the distinct engine semantics and preset names.

The renderer keeps its existing CLI/F-key behavior in this foundation change. The new Core resolver
is an independently testable API; a later renderer integration must be checked in a real window.
In particular, legacy `MaxSpeed` and canonical `TargetSpeed` are related configuration inputs with
different integration semantics. A legacy JSON file is not a native canonical file.

## Spatial semantics

Legacy `BoundaryMode.Wrap` senses through both periodic seams. Candidate-cell traversal visits each
cell once even on one-cell/two-cell axes, and distances, FOV tests, separation and cohesion use the
nearest periodic image. Reflect and clamp continue to use ordinary bounded-world geometry.

`GridCellSize` now controls the legacy grid. Query coverage follows `SenseRadius`, including when
the sensing radius spans many small cells. The default remains 50; it used to be ignored in favor
of `SenseRadius`. This is an intentional legacy behavior change, especially for dense populations.

Candidate coverage is not a promise of unlimited steering neighbors. Legacy sensing still has its
256-entry candidate buffer and configured neighbor limit; `MaxNeighbors <= 0` removes the latter
limit only. In a dense capped run, changing cell size can change which candidates are considered.
Do not treat those steering counts as exact spatial statistics.

Canonical grid and naive queries return the same ascending, lowest-index qualifying prefix, with
self excluded and an inclusive circular boundary. The cap applies before the field-of-view filter.
Normal-range squared-distance comparisons retain binary32 behavior; subnormal or overflowing
squared radii use a double-precision comparison to prevent false neighbors. Zero-radius queries
include only coincident other boids. Grid dimensions remain at least one cell for valid positive
extents, even if the extent/cell-size ratio underflows.

## Explicit researcher telemetry

Canonical `Step` no longer copies snapshot arrays automatically. Allocate a destination once and
capture when the experiment needs it:

```csharp
var world = new CanonicalWorld(settings,
    new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
var telemetry = new CanonicalTelemetry(settings.InitialCapacity);
// Add boids, then advance a fixed number of ticks.
world.Step(settings.FixedDeltaTime);
world.CaptureTelemetryInto(telemetry);
```

`CanonicalTelemetry` is a whole-world researcher view, not a policy observation. It contains
last-tick counts/distances/rule magnitudes and per-boid truncation flags. It does not run another
neighbor query. Advancing the world leaves a captured destination unchanged; another capture into
that destination replaces it. Use separate buffers to retain several ticks. Before the first
step, a capture is empty. A boid added between ticks first appears in telemetry after the next
completed tick. An undersized destination is rejected without modifying its old capture.

The source-compatible `CapturePerceptionSnapshot()` still exists and returns independent arrays,
but allocates on each explicit call. Existing renderer values remain supported through that
adapter. Reusing `CanonicalTelemetry` in the renderer and inspecting its HUD is a separate
follow-up; this change does not claim GUI verification.

## Evidence and reproduction

```sh
dotnet build SwarmingLilMen.sln -c Release
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project SwarmSim.Benchmarks -c Release --no-build -- --canonical-experiment
dotnet run --project SwarmSim.Benchmarks -c Release --no-build -- --canonical
```

The allocation regression reproduced 3,144,000 managed bytes across 1,000 warm ticks with 256 boids
before this change. The same loop now reports zero, both with capture off and with a reused
telemetry destination. The four 96-boid, 24-step trajectory workloads retained every pre-change
Linux hash while their 24-step allocation count fell from 29,376 bytes to zero. Capture-on/off
regressions also compare every boid component by raw float bits for 128 ticks.

The canonical BenchmarkDotNet route includes `[MemoryDiagnoser]` at 1k/10k/50k agents. It uses fixed
1920×1080 extents, radius 10, cap 32, seeded uniform initial positions and evolving worlds through
warmup/measurement. It is a defined workload, not a universal frame-rate claim. The current measured
results and host limitations belong in [PROJECT_STATUS.md](../PROJECT_STATUS.md).

## Next experimental contracts

[Epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10) remains the ordering authority:
golden traces and an explicit platform contract; a Core-only headless runner with manifests;
registered metrics whose analysis neighborhoods are independent of steering caps; deterministic
parameter/seed sweeps; named RNG streams; then actual intent composition. Vicsek and ACO should
test whether those contracts generalize. Their existence as browser demos is not evidence that
the C# kernel already supports those foundations.
