# SwarmingLilMen Controls & Shortcuts

> [`PROJECT_STATUS.md`](PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and renderer state.

The legacy and canonical renderers share parameter/preset controls but expose different world and
visualization actions. Controls below are scoped explicitly; a control is not available in the
other renderer unless it also appears under **Shared Controls**.

## Shared Controls

- **R** – Reset/respawn the current renderer world
- **H** – Toggle that renderer's partial in-app help
- **ESC** – Quit

### Parameter Editing

- **1–8** – Select a parameter (weights, radii, speed, force, friction)
- **↑/↓ or +/-** – Increase/decrease the selected parameter
- **SHIFT + ↑/↓** – Fine adjustment (smaller increments)
- **P** – Print the current parameter configuration to the console
- **F1–F5** – Load built-in presets (balanced, strong separation, tight flocking, fast & loose,
  slow & cohesive)

Keys **1–7** affect both renderer paths. Key **8** changes `Friction`, which is used only by the
legacy `SpeedModel.Damped` path; canonical settings do not consume friction.

## Legacy Renderer Only

### Spawning & World Management

- **Left Click** – Spawn 50 agents at the cursor (group 0)
- **Right Click** – Spawn 50 agents at the cursor (group 1)
- **SPACE** – Spawn 100 random agents across all groups
- **X** – “Shake” the simulation by adding small random velocity to every agent
- **C** – Export a CSV snapshot of the current state

### Visualization Toggles

- **V** – Toggle velocity vectors
- **S** – Toggle sense radius circles
- **N** – Toggle neighbor connections (per tracked agent)
- **F12** – Toggle the snapshot/debug overlay (shows interpolation info)

## Canonical Renderer Only

- **O** – Toggle the selected-boid interaction overlay
- **Tab** – Cycle the boid inspected by the interaction overlay

## Presets & CLI

- **Command-line flags**:
  - `--preset <name>` – Start with a preset configuration (use `--list-presets` to see options)
  - `--config <file>` – Load configuration from JSON (see `configs/` directory)
  - `--agent-count <n>` – Override the initial agent count
  - `--benchmark` – Run a headless benchmark (no window)
  - `--canonical` – Launch the opt-in single-group canonical renderer
  - `--minimal` – Launch the minimal debugging harness
  - `--help`, `--version`, `--list-presets` – Self-documenting flags

The legacy help/parameter panels and canonical footer currently display `1-7` although input accepts
keys **1-8**, and the canonical **H** panel lists only its minimal R/H/Esc subset. This split
reference includes the missing canonical O/Tab controls and scopes legacy-only actions; executable
synchronization and regression coverage are tracked in
[issue #39](https://github.com/Chris0Jeky/SwarmingLilMen/issues/39)
(`SwarmSim.Render/Program.cs:596-749,1231-1277,1340-1380,1447-1455,1553-1612,1687-1704`).
