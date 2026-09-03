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

Use `--agent-count N` to override the initial legacy count. Run `--help` for the authoritative CLI.

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

Press **H** in either renderer for a partial in-app quick reference. The maintained complete
reference is
[`CONTROLS.md`](CONTROLS.md). Legacy highlights include spawning with mouse/Space, **R** reset,
**V/S/N** visualization toggles, **F1-F5** presets, **C** CSV export, and **F12** snapshot/debug information
(`Program.HandleInput`, `Program.DrawHelpOverlay`).

Runtime parameter/help labels currently advertise `1-7` even though input accepts **1-8**, and the
canonical panel omits several active controls; executable synchronization is tracked in
[issue #39](https://github.com/Chris0Jeky/SwarmingLilMen/issues/39).

Friction key **8** is currently a no-op for every registered legacy preset because those presets
inherit `SpeedModel.ConstantSpeed`; it affects only an explicit custom legacy `Damped`
configuration. Canonical settings do not consume friction. Issue #39 owns the executable/help
alignment (`SimConfig.SpeedModel`; `IntegrateSystem.Run`; `Program.Parameters`, `Program.Presets`,
`Program.ProcessCanonicalParameters`).

The canonical path has separate controls: **R** reset, **H** help, **O** selected-boid interaction
overlay, **Tab** tracked-boid selection, parameter keys, and **F1-F5** presets
(`Program.RunCanonicalMode`, `Program.ProcessCanonicalParameters`).

## Performance Interpretation

The legacy renderer requests a **60 FPS** frame cap and its active default configuration uses a
**1/60-second** fixed simulation step (`Program.CreateDefaultBaseConfig` sets `FixedDeltaTime = 1f/60f`; `Program.Main` calls
`Raylib.SetTargetFPS(60)`). Those are scheduling
settings, not measured throughput guarantees. Other `SimConfig` consumers may use different steps.

The legacy-core sample comes from the explicit Release `Performance` test category. Its **50k tick**
result misses the **16.67 ms/tick** target by more than an order of magnitude. The dated figures are
deliberately not copied here — see the verified block in
[`../PROJECT_STATUS.md`](../PROJECT_STATUS.md) for the command and the complete sample, which is one
local measurement rather than a benchmark distribution. That test does not render a window and
therefore is not renderer FPS evidence. Canonical throughput, renderer FPS, and allocation rates
remain unmeasured.

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
are closed. Milestone 7 UX/test acceptance, milestones 8-10, multi-group semantics, canonical
benchmarks, and renderer automation remain open.
Phase 3 combat/metabolism work must wait for that readiness evidence; follow
`PROJECT_STATUS.md` rather than historical phase percentages.
