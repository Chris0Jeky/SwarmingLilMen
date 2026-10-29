# SwarmingLilMen

> [`PROJECT_STATUS.md`](PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and performance state; older plans and milestone prose are context only.

SwarmingLilMen explores the smallest reusable contracts for a generalized experimental engine for
emergent multi-agent systems. Its current proving ground is a 2D boids/swarm simulation built from
first principles in C#/.NET 8.0; the generalized kernel remains roadmap work. The scale figures
below are goals, not achieved throughput claims.

![Status](https://img.shields.io/badge/status-early%20development-orange)
![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue)

## 🎯 Project Goals

- **Emergence over scripts**: Few simple, composable rules → rich macro patterns
- **Performance target (unmet)**: 50k-100k agents at 60 FPS interactive; 1M+ headless
- **Determinism**: Fixed-timestep legacy and canonical paths reproduce ordered agent positions and
  velocities for a matching .NET 8 binary, platform, supported seed, configuration, timestep, and
  ordered input sequence. Experiment manifests verify the final kinematic state and complete
  deterministic CSV on replay. Full-state restart checkpoints and arbitrary cross-platform
  identity remain unproven; see [measured determinism scope](docs/DETERMINISM.md)
- **Observability**: Metrics, snapshots, profiling, property tests, benchmarks
- **Extensibility**: Small public API, Structure-of-Arrays internals, modular systems

## 📝 Implementation Note

This project is undergoing an architectural transition from an aggregate-driven systems approach
to independently testable, Reynolds-style steering rules. Two implementations currently exist side-by-side:
- **Legacy** (default renderer): Aggregate-driven SoA steering systems architecture
- **Canonical** (`--canonical` flag): Three steering rules, an equivalence-tested spatial contract,
  reusable researcher telemetry, and named force policies with bounded observations. The default
  enhanced composition preserves its shared force budget; a public classic-Reynolds configuration
  uses independent rule clamps. Instrumentation UX and multi-group semantics remain incomplete

For developers: See [`docs/archive/IMPLEMENTATION_EVOLUTION.md`](docs/archive/IMPLEMENTATION_EVOLUTION.md)
for the preserved story of the pivot and `PROJECT_STATUS.md` for current evidence. New features
should target the canonical implementation.

## ✨ Current Capabilities

- ✅ Data-oriented design with Structure of Arrays (SoA) layout
- ✅ Fixed-timestep seeded legacy and canonical paths have exact 500-tick ordered-kinematic-hash
  coverage, including wander-enabled and two-process headless checks on the same platform/runtime
- ✅ Agent-genome data structures and mutation API
- ✅ Configurable simulation parameters with presets
- ✅ JSON round-trip for both engine configurations and a strict layered scenario resolver
- ✅ Radius-complete legacy grid sensing with periodic seam handling
- ✅ Canonical zero-allocation warm-tick regressions and a dedicated BenchmarkDotNet route
- ✅ Raylib-free experiments with native configurations, versioned manifests and exact CSV replay
- ✅ Registered analysis channels, periodic centroids and opt-in exact nearest-neighbor statistics
- ✅ Sequential seed/parameter sweeps with stable summary rows and replayable cell manifests
- ✅ Named deterministic random streams and bounded, injectable force-policy compositions
- ✅ Public classic-Reynolds configuration with independent rule clamps and named enable controls
- ✅ Legacy uniform-grid boids pipeline and interactive Raylib renderer
- ✅ Opt-in canonical single-group renderer with steering instrumentation
- ✅ xUnit inventory whose non-performance subset forms the hosted CI gate; see the verified-state
  block at the top of `PROJECT_STATUS.md` for the current measured counts

### Remaining Direction

[Epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10) defines the execution order.

- **Composition:** preserve the exact default traces while addressing the measured small-population
  cost of the new bounded policy API (#27). See [composition and its limits](docs/CANONICAL_COMPOSITION.md).
- **Proof of generality:** behavioral parity and legacy retirement criteria (#28), then a Vicsek
  consumer (#29) and a minimal scalar-field/ACO consumer (#30).
- **Renderer readiness:** complete rule-control visuals and the remaining named-composition
  behavioral scenarios (#40/#41). Controls and configuration now have
  [native Linux verification](docs/NATIVE_UI_VERIFICATION.md); the historical allocation
  observation remains tracked in #20.
- **Longer-term research:** multi-group behavior, lifecycle/evolution and performance approaches
  such as SIMD or parallel execution remain proposals, not shipped engine capabilities. The
  [vision](docs/ROADMAP-VISION.md) records the deferral boundary.

## 🚀 Quick Start

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), as selected by `global.json`
- Windows, Linux, or macOS
- (Recommended) [JetBrains Rider](https://www.jetbrains.com/rider/) or Visual Studio 2022

### Building
```bash
# Clone the repository
git clone https://github.com/Chris0Jeky/SwarmingLilMen.git
cd SwarmingLilMen

# Restore and build the solution in the configuration used by CI
dotnet restore SwarmingLilMen.sln
dotnet build SwarmingLilMen.sln -c Release --no-restore

# CI gate. Rebuild after source changes before using --no-build.
# Category=Performance measurements run separately and are excluded from the ordinary gate.
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true
```

### Running the Simulation
```bash
# Launch the interactive renderer
dotnet run --project SwarmSim.Render

# Display CLI options
dotnet run --project SwarmSim.Render -- --help

# Run a headless canonical experiment and write manifest/metrics/log artifacts
dotnet run --project SwarmSim.Cli -c Release -- --scenario balanced --seed 7 --ticks 600 --agents 400 --out runs/

# Use a native legacy configuration
dotnet run --project SwarmSim.Cli -c Release -- --engine legacy --config configs/balanced.json --seed 7 --ticks 600 --out runs/

# Run the bounded 3-seed × 4-value example (12 experiment cells)
dotnet run --project SwarmSim.Cli -c Release -- --sweep configs/sweep-example.json --out sweeps/

# Run the BenchmarkDotNet suite
dotnet run --project SwarmSim.Benchmarks -c Release

# Measure complete canonical ticks and allocations
dotnet run --project SwarmSim.Benchmarks -c Release -- --canonical
```

For reusable configuration and telemetry APIs, see the
[experiment foundation guide](docs/EXPERIMENT_FOUNDATION.md).

While the renderer is running:
- Press **H** to toggle the partial in-app help overlay; use [`docs/CONTROLS.md`](docs/CONTROLS.md) for the
  complete list. Runtime overlay drift is tracked in
  [issue #39](https://github.com/Chris0Jeky/SwarmingLilMen/issues/39).
- Legacy renderer: press **F12** to toggle the snapshot/debug overlay (shows interpolation details).
- Canonical renderer: press **O** to toggle the interaction overlay and **Tab** to cycle the tracked
  boid.
- See [`docs/CONTROLS.md`](docs/CONTROLS.md) for a printable list of every mouse/keyboard shortcut.

### Command-Line Options
SwarmSim.Render now accepts a lightweight CLI so you can start with presets or external configs without modifying code:

```text
Usage: SwarmSim.Render [OPTIONS]

Options:
  -h, --help                Show this help message and exit
  -v, --version             Show version information
  -l, --list-presets        List built-in presets and exit
  -p, --preset NAME         Load a registered preset (e.g., balanced, strong-separation)
  -c, --config FILE         Load configuration from JSON file (e.g., configs/peaceful.json)
  -n, --agent-count N       Override initial agent count (default: 400)
  -b, --benchmark           Print migration guidance for SwarmSim.Cli (deprecated)
      --canonical           Launch the single-group canonical boids renderer
      --minimal             Launch the minimal debugging harness

Examples:
  SwarmSim.Render
  SwarmSim.Render --preset balanced
  SwarmSim.Render --config configs/warbands.json -n 5000
  dotnet run --project SwarmSim.Cli -- --engine canonical --ticks 600

Legacy interactive controls (default renderer):
PARAMETERS (1-8 to select)
  1: Select Separation weight
  2: Select Alignment weight
  3: Select Cohesion weight
  4: Select Separation radius
  5: Select Sense radius
  6: Select Max speed
  7: Select Max force
  8: Select Friction (Damped only)
  Up/+/KP+: Increase selected parameter
  Down/-/KP-: Decrease selected parameter
  Shift: Hold for fine adjustment with any adjust key
  Friction is inactive with ConstantSpeed (Damped only).
PRESETS
  F1: Load Balanced (Recommended)
  F2: Load Strong Separation
  F3: Load Tight Flocking
  F4: Load Fast & Loose
  F5: Load Slow & Cohesive
WORLD
  P: Print current parameters to the console
  R: Reset/respawn the world
  Esc: Quit
  Left click: Spawn up to 50 agents at cursor (group 0)
  Right click: Spawn up to 50 agents at cursor (group 1)
  Space: Spawn up to 100 random agents across groups
  X: Shake (add random velocity)
  C: Export CSV snapshot
VISUALIZATION
  H: Toggle this help overlay
  V: Toggle velocity vectors
  S: Toggle sense radius circles (below 100 agents)
  N: Toggle tracked-agent neighbor connections
  F12: Toggle snapshot/debug overlay

Canonical interactive controls (--canonical):
PARAMETERS (1-7 to select)
  1: Select Separation weight
  2: Select Alignment weight
  3: Select Cohesion weight
  4: Select Separation radius
  5: Select Sense radius
  6: Select Target speed
  7: Select Max force
  Up/+/KP+: Increase selected parameter
  Down/-/KP-: Decrease selected parameter
  Shift: Hold for fine adjustment with any adjust key
PRESETS
  F1: Load Balanced (Recommended)
  F2: Load Strong Separation
  F3: Load Tight Flocking
  F4: Load Fast & Loose
  F5: Load Slow & Cohesive
WORLD
  P: Print current parameters to the console
  R: Reset/respawn the world
  Esc: Quit
VISUALIZATION
  H: Toggle this help overlay
  O: Toggle interaction overlay
  Tab: Cycle inspected boid

Available presets:
  balanced        - Balanced (Recommended) :: Canonical boids tuning with smooth flocking
  strong-separation - Strong Separation :: Agents prioritize personal space, useful for dense scenes
  tight-flocking  - Tight Flocking :: Emphasizes cohesion/alignment for ribbon-like formations
  fast-loose      - Fast & Loose :: Higher speed ceiling with lighter cohesion
  slow-cohesive   - Slow & Cohesive :: Lower speed with high cohesion for schooling behavior
```

`SwarmSim.Render --benchmark` now prints migration guidance and exits successfully without
starting a simulation, including when combined with `--canonical`. Use `SwarmSim.Cli` for
headless runs and replay, or `SwarmSim.Benchmarks` for throughput/allocation measurements.
See [headless experiments](docs/HEADLESS_EXPERIMENTS.md) for configuration, artifact, and replay contracts.

Use `SwarmSim.Cli --list-metrics` to discover channels, and `--metrics population,centroidX,centroidY`
to select their CSV order. Deterministic selections support exact manifest replay; explicit runtime
channels record operational costs and disable exact replay. Definitions and analysis costs are in
[the metrics contract](docs/METRICS.md).

Renderer startup applies `--preset` before `--config`, so omitted file fields retain the selected
preset's values. Both renderer modes use `SimConfig` files; native canonical files belong to the
headless CLI. The existing five-entry F-key menu and default renderer profile are preserved.
See [renderer configuration](docs/RENDERER_CONFIGURATION.md) and
[canonical telemetry](docs/CANONICAL_RENDER_TELEMETRY.md) for the tested contracts and manual-check scope.

Exit codes: `0` success; `1` the `--config` file failed to load; `2` usage error — an unknown
`--preset`, an unrecognised option or unexpected argument (for example a mistyped `--agnet-count`),
or a missing, empty or malformed option value (for example `-n abc`, `-n 0`, `--preset ""`, or
`--config` with no path or followed by another option), reported on stderr with the option and
value. A usage error takes precedence over `--help`, `--version` and `--list-presets`. Agent counts
accept surrounding whitespace and a leading `+` (`-n +5`).

> **Known executable-help defect:**
>
> - The copied F12 instruction applies only to the legacy renderer. Canonical mode uses **O** for
>   its interaction overlay and **Tab** to cycle the tracked boid; executable/runtime-help alignment
>   is tracked in [issue #39](https://github.com/Chris0Jeky/SwarmingLilMen/issues/39).

Example:
```bash
dotnet run --project SwarmSim.Render -- --preset fast-loose --agent-count 5000
dotnet run --project SwarmSim.Render -- --config configs/warbands.json
```

Sample configuration files live in the [`configs/`](configs) directory and demonstrate how to tweak `SimConfig` via JSON.
For recipes see [`docs/CONFIGURATION_COOKBOOK.md`](docs/CONFIGURATION_COOKBOOK.md) and for parameter
effects see [`docs/PARAMETER_GUIDE.md`](docs/PARAMETER_GUIDE.md).

### Understanding Parameters
- [`docs/PARAMETER_GUIDE.md`](docs/PARAMETER_GUIDE.md) explains every major field (vision, weights, collision avoidance, etc.) and how changing it affects behaviour.
- [`docs/CONFIGURATION_COOKBOOK.md`](docs/CONFIGURATION_COOKBOOK.md) provides ready-made recipes (balanced, peaceful, warbands) you can copy and modify.

## 🌐 JavaScript Demos

For quick prototyping, demonstrations, and learning, we provide standalone browser-based implementations:

### [Boids Basic Demo](js-demos/boids-basic/)
A beautiful, interactive implementation of Reynolds' Boids algorithm:
- ✨ Real-time parameter adjustment with sliders
- 🎨 Motion trails and debug visualization
- 🎯 Multiple behavioral presets (chaotic, tight flocks, flowing)
- 🖱️ Click to spawn boids interactively

### [Self-Propelled Particles (Vicsek Model)](js-demos/self-propelled-particles/)
An interactive demonstration of phase transitions in active matter:
- 🔬 Watch order-disorder phase transitions in real-time
- 📊 Live order parameter tracking (measure of collective alignment)
- 🎨 Multiple visualization modes (arrows, trails, density heatmap)
- 🎓 Educational tool for statistical mechanics

### [Ant Colony Optimization](js-demos/ant-colony-optimization/)
Stigmergy-based pathfinding with pheromone trails (Dorigo, 1992):
- 🐜 Click to place food, drag to draw walls/obstacles
- 💫 Watch pheromone trails form, evaporate, and converge to optimal paths
- 🧪 Experiment with evaporation rate, deposit amount, exploration
- 🏆 See emergent optimization through positive feedback loops

### [Particle Swarm Optimization](js-demos/particle-swarm-optimization/)
Global optimization using velocity-based swarm search (Kennedy, Eberhart & Shi, 1995):
- 🎯 Five benchmark functions with beautiful fitness landscape visualization
- 🌊 Watch particles surf the optimization surface with momentum
- ⚡ Adjust inertia weight, cognitive/social coefficients in real-time
- 📈 Track convergence progress and global best solution
- 🧬 Demonstrates continuous optimization (vs ACO's discrete paths)

**Quick start:**
```bash
# Just open in your browser
open js-demos/boids-basic/index.html
open js-demos/self-propelled-particles/index.html
open js-demos/ant-colony-optimization/index.html
open js-demos/particle-swarm-optimization/index.html

# Or serve with a local server
cd js-demos/particle-swarm-optimization  # or any other demo
python3 -m http.server 8000
# Visit http://localhost:8000
```

These demos are perfect for:
- **Quick iteration**: Test parameters without recompiling C#
- **Demonstrations**: Easy to share, no installation needed
- **Learning**: Clean, commented code showing core algorithms
- **Prototyping**: Experiment before implementing in C#

See [`js-demos/README.md`](js-demos/README.md) for all available demos and details.

## 📁 Project Structure

```
SwarmingLilMen/
├── SwarmSim.Core/          # Core simulation library
│   ├── Genome.cs           # Agent genetics
│   ├── AgentState.cs       # Behavioral state flags
│   ├── SimConfig.cs        # Configuration system
│   ├── World.cs            # Main simulation with SoA data
│   ├── Systems/            # Legacy per-tick sense, behavior, wander, and integrate
│   ├── Spatial/            # Uniform grid for neighbor queries
│   ├── Diagnostics/        # Ordered kinematic hash
│   ├── Configuration/      # JSON round-trip and layered scenario resolution
│   ├── Metrics/            # Registered analysis probes and invariant writers
│   ├── Canonical/          # New canonical boids implementation
│   └── Utils/              # Math and RNG utilities
├── SwarmSim.Cli/           # Display-free experiments, artifacts and manifest replay
├── SwarmSim.Render/        # Raylib-cs visualization
├── SwarmSim.Tests/         # xUnit test suite
├── SwarmSim.Benchmarks/    # BenchmarkDotNet performance tests
├── js-demos/               # Browser-based standalone demos
│   ├── boids-basic/        # Interactive boids simulation
│   ├── self-propelled-particles/  # Vicsek model (phase transitions)
│   ├── ant-colony-optimization/   # ACO pathfinding (stigmergy)
│   └── particle-swarm-optimization/ # PSO (continuous optimization)
├── docs/                  # Live guides, roadmap vision, and dated archive
│   ├── CONFIGURATION_COOKBOOK.md
│   ├── CONTROLS.md
│   ├── PARAMETER_GUIDE.md
│   ├── PHASE_3_READINESS_CHECKLIST.md
│   ├── QUICKSTART.md
│   ├── README.md             # Documentation index
│   ├── RENDERER_GUIDE.md
│   ├── ROADMAP-VISION.md
│   ├── SIMULATION_MECHANICS_EXPLAINED.md
│   └── archive/            # Historical plans and narratives
├── configs/                # JSON configuration presets
├── AGENTS.md                # Thin Codex adapter (points at CLAUDE.md)
├── CLAUDE.md               # Shared agent contract: tier, commands, seam checks, pitfalls
├── CONTRIBUTING.md          # Developer setup and workflow
├── LICENSE                  # GNU GPL v3 license
├── PROJECT_STATUS.md       # Implementation tracker
└── README.md               # This file
```

## 🎮 How to Use

### Creating a Simulation

```csharp
using SwarmSim.Core;

// Create a configuration (or use a preset)
var config = SimConfig.PeacefulFlocks(); // or new SimConfig()

// SimConfig is the preferred seed source
var world = new World(config);

// Spawn some agents
world.SpawnAgentsInCircle(
    centerX: 500f,
    centerY: 500f,
    radius: 100f,
    count: 1000,
    group: 0
);

// Run the simulation
while (true)
{
    world.Tick();

    // Access agent data for rendering
    var positionsX = world.GetPositionsX();
    var positionsY = world.GetPositionsY();
    var groups = world.GetGroups();

    // Get statistics
    var stats = world.GetStats();
    Console.WriteLine($"Agents: {stats.AliveAgents}, Avg Energy: {stats.AverageEnergy:F1}");
}
```

### Configuration Presets

```csharp
// Peaceful flocking behavior
var peaceful = SimConfig.PeacefulFlocks();

// Warbands-flavoured group settings; combat remains future Phase 3 behavior
var combat = SimConfig.Warbands();

// High mutation settings; reproduction/evolution remains future Phase 4 behavior
var evolution = SimConfig.RapidEvolution();

// Or customize everything
var custom = new SimConfig
{
    WorldWidth = 1920f,
    WorldHeight = 1080f,
    MaxSpeed = 10f,  // world-position units per second
    MaxForce = 2.5f,
    SenseRadius = 60f,
    // ... 30+ parameters available
};
```

## 🧪 Testing

```bash
# CI gate (same commands as Quick Start). Category=Performance timing facts run Release-only and are reported-only evidence.
dotnet build SwarmingLilMen.sln -c Release
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true

# Run with detailed output
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true

# Run specific test class
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~RngTests" -- RunConfiguration.TreatNoTestsAsError=true

# Run the same gate with coverage (coverlet.collector is already referenced)
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" --collect:"XPlat Code Coverage" -- RunConfiguration.TreatNoTestsAsError=true
```

## 📊 Benchmarking

```bash
# Canonical complete-tick throughput and allocation measurements
dotnet run --project SwarmSim.Benchmarks -c Release -- --canonical

# Legacy tick and grid suites (always use Release configuration)
dotnet run --project SwarmSim.Benchmarks -c Release

# Results will be in BenchmarkDotNet.Artifacts/
```

## 🎨 Architecture Highlights

### Structure of Arrays (SoA)
Agents are stored in parallel arrays for cache efficiency and SIMD potential:
```csharp
float[] X, Y;              // Position
float[] Vx, Vy;            // Velocity
float[] Energy, Health;    // Resources
Genome[] Genomes;          // Genetics
```

### Systems Pipeline
The active legacy path rebuilds the uniform grid, then runs these systems in sequence each tick
(`SwarmSim.Core/World.cs` (`World.Tick`)):
1. **SenseSystem** - Query same-group neighbors and aggregate boids inputs
2. **BehaviorSystem** - Convert separation, alignment, and cohesion inputs to steering
3. **WanderSystem** - Optional wander contribution
4. **IntegrateSystem** - Apply steering, speed constraints, and boundary behavior

Combat, forage, reproduction, metabolism, and lifecycle systems remain future Phase 3+ work.

### Performance Principles
- Allocation-conscious hot paths; canonical warm-step and reusable-capture allocation tests run in CI
- No LINQ, delegates, or boxing in hot paths
- Tight `for` loops with hoisted invariants
- Direct/static calls over virtual in inner loops

## 📈 Performance Targets and Evidence

The interactive objective is **50k-100k agents at 60 FPS** and the headless objective is **1M+
agents**. Both are targets; neither is currently verified. Measured canonical ticks at 50k still
exceed the 16.67 ms/tick budget before renderer work is counted.

Run the legacy timing probes and the separate canonical benchmark:

```bash
dotnet build SwarmingLilMen.sln --configuration Release
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj --configuration Release --no-build --filter "Category=Performance" --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project SwarmSim.Benchmarks -c Release --no-build -- --canonical
```

| Evidence | State |
|----------|-------|
| Legacy tick at 1k / 10k / 50k agents | Historical measurements; see the dated [status archive](docs/archive/PROJECT_STATUS_BEFORE_2026-10-29.md) |
| Legacy 50k grid rebuild | Measured; grid-only, not full simulation cost |
| Canonical throughput | BenchmarkDotNet measurements at 1k/10k/50k, with workload and host caveats in [`PROJECT_STATUS.md`](PROJECT_STATUS.md) |
| Renderer FPS | Unmeasured — the timing tests open no window |
| Allocations per tick | Zero current-thread allocations in 1,000-warm-tick regressions, with capture off or reused; benchmark observations and limits in [`PROJECT_STATUS.md`](PROJECT_STATUS.md) |

The measured numbers, distributions, workload and machine details are maintained in
`PROJECT_STATUS.md`; historical samples remain dated in its archive. Results from different engines,
densities or machines are not interchangeable.

The bounded headless canonical experiment compares complete observable trajectories against the
naive spatial index, reports allocations and replays dense neighbor selection with operation counts:

```bash
dotnet run --project SwarmSim.Benchmarks -c Release -- --canonical-experiment
```

It uses at most 256 agents and does not launch BenchmarkDotNet or a renderer. See
[method, counterexamples and measured limits](docs/CANONICAL_EXPERIMENT.md). General experiments
use the separate [headless runner](docs/HEADLESS_EXPERIMENTS.md), which supports either engine.

## 🤝 Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, IDE configuration, and workflow guidelines.

Quick checklist:
- Follow the architecture principles in `CLAUDE.md`
- Write tests for new features
- Keep the hot path allocation-free
- Update `PROJECT_STATUS.md` when completing milestones
- Profile performance regularly with dotTrace/dotMemory

## 📋 Development Status

Legacy Phases 0-2, fixed-timestep running, snapshot interpolation, and the renderer are implemented.
The experiment foundation now includes headless runs, registered metrics, exact manifest replay
and sequential sweeps. Seeded same-platform reproducibility, spatial-index equivalence,
force-budget enforcement and explicit telemetry are covered. Bounded named force composition and
public classic mode now preserve the default traces and old manifests; their measured performance
cost is recorded. Instrumentation UX, multi-group semantics, behavioral parity and additional
simulation foundations remain open. See
[PROJECT_STATUS.md](PROJECT_STATUS.md) for the verified queue.

## 🔧 Troubleshooting

### Build Issues
```bash
# Clean and rebuild
dotnet clean SwarmingLilMen.sln -c Release
dotnet restore SwarmingLilMen.sln
dotnet build SwarmingLilMen.sln -c Release --no-restore
```

### Test Failures
- Ensure you're using .NET 8.0 SDK
- Tests require deterministic behavior - avoid time-based randomness
- Exact golden fixtures currently cover .NET 8 on Linux/Windows x64. A new runtime/architecture
  needs a reviewed reference; see [the fixture policy](docs/DETERMINISM.md).

### Performance Issues
- Always benchmark in Release mode: `dotnet run --project SwarmSim.Benchmarks --configuration Release`
- Use Rider's profiling tools (CPU/Memory) for analysis
- Check allocations with `dotnet-counters` or dotMemory

## 📚 Documentation

- [CLAUDE.md](CLAUDE.md) - Guidelines for AI assistants
- [PROJECT_STATUS.md](PROJECT_STATUS.md) - Implementation tracker and roadmap
- [Roadmap vision](docs/ROADMAP-VISION.md) - Aspirational reference; epic #10 is the executable plan
- [Documentation index](docs/README.md) - Live guides, security non-goal, extension posture, and archive routing
- [Historical publishing reference](docs/archive/PublishScript.md) - Superseded distribution examples

## 🛠️ Technology Stack

- **Language**: C# 12 (.NET 8.0)
- **Rendering**: [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) 7.0.2
- **Testing**: [xUnit](https://xunit.net/) 2.9.3
- **Benchmarking**: [BenchmarkDotNet](https://benchmarkdotnet.org/) 0.15.6
- **IDE**: JetBrains Rider (recommended) or Visual Studio 2022

## 📝 License

[GNU GPL v3](LICENSE) (`GPL-3.0-only`) - modified distributions must remain under GPLv3; see [RELICENSING.md](RELICENSING.md).

## 🙏 Acknowledgments

- Inspired by boids algorithms, Conway's Game of Life, and emergence theory
- Built with performance lessons from data-oriented design principles
- Architecture influenced by ECS (Entity Component System) patterns

---

**Note**: This project is under active development. Features and APIs may change. Check [PROJECT_STATUS.md](PROJECT_STATUS.md) for the latest progress.
