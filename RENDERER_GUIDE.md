# SwarmingLilMen Renderer Guide

> [`PROJECT_STATUS.md`](PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and performance state.

## Choose the Renderer Path

The default command launches the legacy SoA renderer with **400 agents** split across four groups
(`SwarmSim.Render/Program.cs:66,471,552-586`):

```bash
dotnet run --project SwarmSim.Render
```

The canonical renderer is an opt-in, single-group path while canonical readiness work remains
incomplete (`SwarmSim.Render/CommandLineOptions.cs:101-104`):

```bash
dotnet run --project SwarmSim.Render -- --canonical
```

Use `--agent-count N` to override the initial legacy count. Run `--help` for the authoritative CLI.

## What the Legacy Renderer Shows

- Four initial clusters use white, red, green, and blue for groups 0-3
  (`SwarmSim.Render/Program.cs:220-237,552-575`).
- Neighbor sensing is limited to the same group and current field of view
  (`SwarmSim.Core/Systems/SenseSystem.cs:147-193`).
- Separation, alignment, cohesion, optional wander, and integration are the active systems;
  combat, metabolism, reproduction, and lifecycle behavior are not implemented
  (`SwarmSim.Core/World.cs:122-144`).
- Wrap, reflect, and clamp boundary modes are configuration options. Do not assume wrap mode for
  every preset (`SwarmSim.Core/Systems/IntegrateSystem.cs:7-23`).

Flocking is deterministic for a fixed seed, configuration, and timestep, but its visible shape
depends on the selected preset and live parameter edits.

## Controls

Press **H** in either renderer for its in-app help. The maintained legacy reference is
[`CONTROLS.md`](CONTROLS.md); highlights include spawning with mouse/Space, **R** reset, **V/S/N**
visualization toggles, **F1-F5** presets, **C** CSV export, and **F12** snapshot/debug information
(`SwarmSim.Render/Program.cs:637-739,1244-1277`).

The canonical path has separate controls: **R** reset, **H** help, **O** metrics overlay, **Tab**
tracked-boid selection, parameter keys, and **F1-F5** presets
(`SwarmSim.Render/Program.cs:1343-1379,1556-1607`).

## Performance Interpretation

The legacy renderer requests a **60 FPS** frame cap and its active default configuration uses a
**1/60-second** fixed simulation step (`SwarmSim.Render/Program.cs:245,467`). Those are scheduling
settings, not measured throughput guarantees. Other `SimConfig` consumers may use different steps.

The latest legacy-core sample was captured on 2026-07-25 with the explicit Release
`Performance` test category. Its **50k tick** result was **162.815 ms/tick (6.14 operations/second)**,
so the **16.67 ms/tick** target was unmet. That test does not render a window and therefore is not
renderer FPS evidence. Canonical throughput, renderer FPS, and allocation rates remain unmeasured;
see the verified block in `PROJECT_STATUS.md` for the command and complete sample.

## Data Export

In the legacy renderer, **C** writes a CSV snapshot with agent ID, group, position, velocity, speed,
energy, health, age, and state. The file is named
`swarm_snapshot_YYYYMMDD_HHMMSS_T######.csv` (`SwarmSim.Render/Program.cs:1863-1894`). Generated
operational outputs should not be committed.

## Troubleshooting

- If the window does not open, first run `dotnet run --project SwarmSim.Render -- --help`. If that
  succeeds, verify the machine has an interactive graphics session and the required Raylib native
  dependencies.
- If motion is unexpected, press **H**, inspect the active preset/parameters, and use **R** to reset.
- If diagnostics are needed, use **F12** on the legacy path or **O** on the canonical path.
- Unit tests and headless timing checks do not replace a visual renderer check.

## Current Direction

Canonical milestones 8-10, multi-group semantics, canonical benchmarks, and renderer automation
remain open. Phase 3 combat/metabolism work must wait for that readiness evidence; follow
`PROJECT_STATUS.md` rather than historical phase percentages.
