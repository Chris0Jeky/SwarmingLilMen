# SwarmingLilMen Renderer Guide

> [`PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and performance state.

## Choose the Renderer Path

The default command launches the legacy SoA renderer with **400 agents** split across four groups
(`Program._initialAgentCount`, `Program.Main`, `Program.SpawnInitialAgents`):

```bash
dotnet run --project SwarmSim.Render
```

The canonical renderer is an opt-in, single-group path while canonical readiness work remains
incomplete (`CommandLineOptions.Parse`, `--canonical` case):

```bash
dotnet run --project SwarmSim.Render -- --canonical
```

Use `--agent-count N` to override the initial population in either renderer
(`Program.Main`, `Program.SpawnInitialAgents`, `Program.CreateCanonicalWorld`). Run `--help` for
the authoritative renderer options. The former `--benchmark` flag now prints migration guidance
and exits; fixed-tick experiments use `SwarmSim.Cli` and its `--agents` option:

```sh
dotnet run --project SwarmSim.Cli -c Release -- --engine canonical --scenario balanced --seed 7 --ticks 600 --agents 1000 --out runs/
```

See [HEADLESS_EXPERIMENTS.md](HEADLESS_EXPERIMENTS.md) for recorded runs and manifest replay.

## What the Legacy Renderer Shows

- Four initial clusters use white, red, green, and blue for groups 0-3
  (`Program.GroupColors`, `Program.SpawnInitialAgents`).
- Neighbor sensing is limited to the same group and current field of view
  (`SenseSystem.Run`).
- Separation, alignment, cohesion, optional wander, and integration are the active systems;
  combat, metabolism, reproduction, and lifecycle behavior are not implemented
  (`World.Initialize`, systems registration).
- Every registered renderer preset and bundled JSON example currently uses the inherited `Wrap`
  default. `Reflect` and `Clamp` remain available to custom configurations and other callers
  (`SimConfig.BoundaryMode`, `SimConfig.Validate`; `Program.Presets`; `configs/*.json`).

Legacy wander now consumes the world's configured RNG, and canonical initialization plus
per-agent wander streams derive from `SimConfig.Seed`. The supported `--minimal` harness likewise
uses `World.Rng` for random velocities and staged spawn positions. For the same .NET 8 binary,
platform, configuration, timestep, and input sequence, both paths have exact 500-tick
ordered-kinematic-hash
coverage; the bundled balanced configuration is also exercised in two separate headless processes.
The hash covers agent count plus ordered X/Y/Vx/Vy bits, not configuration, clocks, RNG/wander
state, groups, lifecycle/resources, or genomes. This is not a cross-platform/runtime guarantee, and
reset or live-parameter event sequences remain part of the deterministic input contract.
Cross-platform measurement belongs to
[issue #21](https://github.com/Chris0Jeky/SwarmingLilMen/issues/21).

The shared unauthored renderer default uses `WanderStrength = 0` for both paths. Authored presets
and JSON files that set a positive value still enable wander. Canonical construction also maps
seed, wander-rate, turn-rate, whisker, and separation-priority settings from `SimConfig` instead
of silently using unrelated canonical defaults.

## Controls

Press **H** in either renderer for its complete control inventory. The maintained reference is
[`CONTROLS.md`](CONTROLS.md). Legacy highlights include spawning with mouse/Space, **R** reset,
**V/S/N** visualization toggles, **F1-F5** presets, **C** CSV export, and **F12** snapshot/debug information
(`Program.HandleInput`, `Program.DrawHelpOverlay`).

Sense-radius circles are drawn only below 100 agents; the S binding's help states this limit.
If parameter/preset changes and spawn/reset/shake actions arrive in the same frame, the later
actions operate on the recreated active world rather than the discarded instance.

`RendererControls` supplies the bindings consumed by both input paths and the labels in both
overlays, the legacy parameter panel, canonical footer, parameter printing, and console help.
Legacy labels advertise **1–8**; canonical accepts and advertises **1–7**. Headless regression
coverage checks the shared inventory and effective parameter semantics. The
[native verification record](NATIVE_UI_VERIFICATION.md) includes direct captures of both panels,
real input/preset/custom-startup checks, exact binary identities and the same-frame spawn regression.
Its scope is Linux software rendering, without a human sign-off or cross-platform GUI claim.

Friction key **8** is currently a no-op for every registered legacy preset because those presets
inherit `SpeedModel.ConstantSpeed`; it affects only an explicit custom legacy `Damped`
configuration. The legacy panel, help and console messages label that limitation explicitly.
Canonical settings do not consume friction, and canonical input now ignores key **8** instead
of changing an unused value (`SimConfig.SpeedModel`; `IntegrateSystem.Run`;
`RendererControls.ApplyParameters`; `Program.ProcessCanonicalParameters`).

The canonical path has separate controls: **R** reset, **H** help, **O** selected-boid interaction
overlay, **Tab** tracked-boid selection, parameter keys, and **F1-F5** presets
(`Program.RunCanonicalMode`, `Program.ProcessCanonicalParameters`).

## Performance Interpretation

The legacy renderer requests a **60 FPS** frame cap and its active default configuration uses a
**1/60-second** fixed simulation step (`Program.CreateDefaultBaseConfig` sets `FixedDeltaTime = 1f/60f`; `Program.Main` calls
`Raylib.SetTargetFPS(60)`). Those are scheduling
settings, not measured throughput guarantees. Other `SimConfig` consumers may use different steps.

Canonical full-step throughput and allocation measurements are available through
`SwarmSim.Benchmarks --canonical` (`CanonicalWorldStepBenchmarks`), with telemetry capture off.
Current results, workload definitions and measurement caveats are maintained in
[PROJECT_STATUS.md](../PROJECT_STATUS.md#performance-evidence-and-its-limits). Earlier legacy
timings remain in the [historical status archive](archive/PROJECT_STATUS_BEFORE_2026-10-29.md).
These headless core measurements do not establish renderer FPS or a universal allocation bound.
The interactive 50k/60 FPS target remains unmet, and full renderer performance remains unverified.

`CanonicalWorld.Step` no longer creates a perception snapshot automatically. Callers can reuse
`CanonicalTelemetry` with `CaptureTelemetryInto`; the canonical renderer does so explicitly once
per frame through `Program.CaptureCanonicalTelemetry`. The older `CapturePerceptionSnapshot`
compatibility adapter still allocates three arrays on calls after the first tick. Allocation
regressions cover the bounded step/capture path, not all rendering, logging or overlay drawing.

## Data Export

In the legacy renderer, **C** writes a CSV snapshot with agent ID, group, position, velocity, speed,
energy, health, age, and state. The file is named
`swarm_snapshot_YYYYMMDD_HHMMSS_T######.csv` (`Program.ExportCSV`). Generated
operational outputs should not be committed.

## Troubleshooting

- If the window does not open, first run `dotnet run --project SwarmSim.Render -- --help`. If that
  succeeds, verify the machine has an interactive graphics session and the required Raylib native
  dependencies.
- If motion is unexpected, press **H**, inspect the active preset/parameters, and use **R** to reset.
- If diagnostics are needed, use **F12** on the legacy path or **O** on the canonical path.
- Unit tests and headless timing checks do not replace a visual renderer check.

## Current Direction

Canonical perception semantics ([issue #18](https://github.com/Chris0Jeky/SwarmingLilMen/issues/18))
and force-budget enforcement ([issue #19](https://github.com/Chris0Jeky/SwarmingLilMen/issues/19))
are closed. Canonical benchmarks and explicit reusable telemetry are implemented. Remaining
readiness, multi-group semantics, renderer acceptance and Phase 3 gates are tracked in
[PROJECT_STATUS.md](../PROJECT_STATUS.md); historical phase percentages are not current acceptance
evidence.
