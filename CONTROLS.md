# SwarmingLilMen Controls & Shortcuts

## Spawning & World Management
- **Left Click** – Spawn 50 agents at the cursor (group 0)
- **Right Click** – Spawn 50 agents at the cursor (group 1)
- **SPACE** – Spawn 100 random agents across all groups
- **R** – Reset the world to the initial state
- **X** – “Shake” the simulation by adding small random velocity to every agent
- **C** – Export a CSV snapshot of the current state

## Visualization Toggles
- **V** – Toggle velocity vectors
- **S** – Toggle sense radius circles
- **N** – Toggle neighbor connections (per tracked agent)
- **F12** – Toggle the snapshot/debug overlay (shows interpolation info)
- **H** – Toggle the full help overlay (quick reference inside the renderer)

## Parameter Editing
- **1–7** – Select a parameter (weights, radii, speed, friction)
- **↑/↓ or +/-** – Increase/decrease the selected parameter
- **SHIFT + ↑/↓** – Fine adjustment (smaller increments)
- **P** – Print the current parameter configuration to the console

## Presets & CLI
- **F1–F5** – Load built-in presets (balanced, strong separation, tight flocking, fast & loose, slow & cohesive)
- **Command-line flags**:
  - `--preset <name>` – Start with a preset configuration (use `--list-presets` to see options)
  - `--config <file>` – Load configuration from JSON (see `configs/` directory)
  - `--agent-count <n>` – Override the initial agent count
  - `--benchmark` – Run a headless benchmark (no window)
  - `--help`, `--version`, `--list-presets` – Self-documenting flags

The in-app help overlay (press **H**) mirrors this document and displays the current ON/OFF state of the main toggles.
