# SwarmingLilMen Controls & Shortcuts

> [`PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and renderer state.

The legacy and canonical renderers share parameter/preset controls but expose different world and
visualization actions. Controls below are scoped explicitly; a control is not available in the
other renderer unless it also appears under **Shared Controls**.

## Shared Controls

- **R** – Reset/respawn the current renderer world
- **H** – Toggle that renderer's complete control inventory
- **ESC** – Quit

### Parameter Editing

- **1–7** – Select a parameter (weights, radii, speed, force)
- **↑/↓, +/-, or numeric-keypad +/-** – Increase/decrease the selected parameter
- **SHIFT + any adjustment key** – Fine adjustment (smaller increments; either Shift key works)
- **P** – Print the current parameter configuration to the console
- **F1–F5** – Load built-in presets (balanced, strong separation, tight flocking, fast & loose,
  slow & cohesive)

Keys **1–7** affect both renderer paths. The legacy renderer additionally accepts **8** for
`Friction`; its help and parameter panel explicitly label that value **Damped only**. Every built-in
renderer preset uses `SpeedModel.ConstantSpeed`, where changing friction leaves the simulation
unchanged. An authored legacy configuration must select `SpeedModel.Damped` for friction to affect
motion. The panel, parameter selection/adjustment messages, and **P** output state when friction
is inactive. Canonical uses **8** for field of view and does not display or print friction as a control.

| Key | Parameter | Range | Normal step | Fine step |
| --- | --- | ---: | ---: | ---: |
| 1 | Separation weight | 0–500 | 10 | 0.01 |
| 2 | Alignment weight | 0–500 | 10 | 0.01 |
| 3 | Cohesion weight | 0–10 | 0.1 | 0.0001 |
| 4 | Separation radius | 5–200 | 5 | 1 |
| 5 | Sense radius | 10–300 | 10 | 2 |
| 6 | Max speed (legacy) / target speed (canonical) | 1–500 | 10 | 1 |
| 7 | Max force | 0.1–20 | 0.1 | 0.01 |
| 8 | Friction (legacy, Damped only) | 0.8–1.0 | 0.01 | 0.001 |
| 8 | Field of view (canonical, degrees) | 1–360 | 15 | 1 |

Changing a value rebuilds the current world configuration while preserving agent positions and
velocities. The adjustment applies on the next simulation tick. Press **P** to print the resulting
configuration. The earlier parameter-editor narrative is retained as a dated historical source in
[`archive/DYNAMIC_PARAMETERS.md`](archive/DYNAMIC_PARAMETERS.md).

## Legacy Renderer Only

### Spawning & World Management

- **Left Click** – Spawn up to 50 agents at the cursor (group 0)
- **Right Click** – Spawn up to 50 agents at the cursor (group 1)
- **SPACE** – Spawn up to 100 random agents across all groups
- **X** – “Shake” the simulation by adding small random velocity to every agent
- **C** – Export a CSV snapshot of the current state

Spawning respects available capacity. Mouse and Space messages report the actual number added.

### Visualization Toggles

- **V** – Toggle velocity vectors
- **S** – Toggle sense radius circles (drawn only below 100 agents)
- **N** – Toggle neighbor connections (per tracked agent)
- **F12** – Toggle the snapshot/debug overlay (shows interpolation info)

## Canonical Renderer Only

- **O** – Toggle the selected-boid interaction overlay
- **Tab** – Cycle the boid inspected by the interaction overlay. Parameter and preset changes
  keep the inspected boid when its index still exists (recreation preserves order).
- **S / A / C** – Independently enable/disable the named separation / alignment / cohesion rules.
  Their ON/OFF state is always visible and survives parameter changes, presets and reset.
- **8**, then **↑/↓** – Adjust FOV by 15 degrees; hold **Shift** for 1-degree steps.

The inspection overlay uses the **last completed tick's input positions**, not the moved positions.
Its gold arc is the heading-centered FOV at the sense radius. A white arrow shows the actual resolved
force before velocity shaping/normalization (64 pixels per MaxForce, visually capped at 128 pixels).
Gray arrows/circles show heading and whisker geometry. A zero force has no white arrow.

Orange, blue and violet parallel links identify weighted inputs to applied separation, alignment
and cohesion respectively. They are inputs to aggregate rules, **not per-neighbor force vectors**.
Separation links exclude coincident/tiny-distance neighbors and the zero-influence radius boundary.
Disabled, zero-result or budget-suppressed rules have no colored links; gray links show remaining
visible neighbors. The selected-boid panel reports the three applied magnitudes and the tick number.
The status line reports the recorded tick's input count and truncation separately from the links
displayed and the display cap. The overlay owns 128-entry buffers, so a custom `MaxNeighbors` above
128 can record more inputs than are drawn; the line then says links were omitted.

Rule switches use `SetPolicyEnabled`, not weight zero. Whisker avoidance, wander and velocity stages
retain their own behavior: switching off the three named rules does not switch off those enhancements.

## Presets & CLI

- **Command-line flags**:
  - `-p`, `--preset <name>` – Start with a preset configuration (use `--list-presets` to see options)
  - `-c`, `--config <file>` – Load configuration from JSON (see `configs/` directory)
  - `-n`, `--agent-count <n>` – Override the initial agent count
  - `-b`, `--benchmark` – Print deprecation/migration guidance and exit; no simulation runs
  - `--canonical` – Launch the opt-in single-group canonical renderer
  - `--minimal` – Launch the minimal debugging harness
  - `-h`, `--help` (also `/?`), `-v`, `--version`, `-l`, `--list-presets` – Self-documenting flags

For a headless experiment, use the standalone CLI:

```sh
dotnet run --project SwarmSim.Cli -c Release -- --engine canonical --scenario balanced --seed 7 --ticks 600 --agents 1000 --out runs/
```

`--agent-count` belongs to the renderer; `--agents` belongs to the CLI. See
[HEADLESS_EXPERIMENTS.md](HEADLESS_EXPERIMENTS.md) for the manifest and replay contract.

## Executable inventory and verification

`SwarmSim.Render/RendererControls.cs` owns the engine-specific key/mouse bindings, parameter
names/ranges/steps, preset labels, and help descriptions. Both input loops capture that inventory
before dispatch. Both **H** overlays, the legacy parameter panel, canonical footer, parameter
printing, and the interactive sections of `--help` use the same entries. The legacy heading is
**1–8** in both modes, with an engine-specific eighth parameter. Preset names are supplied by `Program.Presets`, so the inventory does
not maintain another list of preset names.

`RendererControlsTests` covers all pressed-key bindings, both Shift keys, mouse controls,
cross-engine exclusions, canonical FOV adjustment, adjustment/printing semantics,
the actual legacy friction speed-model distinction, and bounded spawn counts. The CLI-help
transcript is also checked against the README. These are headless checks; manual inspection of
both overlays remains an acceptance step for
[issue #39](https://github.com/Chris0Jeky/SwarmingLilMen/issues/39).

`CanonicalInspectionTests` covers named key-to-rule effects, independent classic contributions,
switch persistence, last-tick query identity/caps, unchanged future trajectories, zero warm query
allocation, force-vector identity and arc geometry. [Inspection verification](CANONICAL_INSPECTION.md)
separates native drawing evidence from Windows input and human acceptance still open in
[issue #40](https://github.com/Chris0Jeky/SwarmingLilMen/issues/40).
