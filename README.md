# SwarmingLilMen

> [`PROJECT_STATUS.md`](PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and performance state; older plans and milestone prose are context only.

SwarmingLilMen explores the smallest reusable contracts for a generalized experimental engine for
emergent multi-agent systems. Its current proving ground is a 2D boids/swarm simulation built from
first principles in C#/.NET 8.0; the generalized kernel remains roadmap work. The scale figures
below are goals, not achieved throughput claims.

![Status](https://img.shields.io/badge/status-early%20development-orange)
![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## 🎯 Project Goals

- **Emergence over scripts**: Few simple, composable rules → rich macro patterns
- **Performance target (unmet)**: 50k-100k agents at 60 FPS interactive; 1M+ headless
- **Determinism goal**: Fixed timestep, reproducible seeded runs, and record/replay; current seed
  wiring limitations are tracked in [issue #17](https://github.com/Chris0Jeky/SwarmingLilMen/issues/17)
- **Observability**: Metrics, snapshots, profiling, property tests, benchmarks
- **Extensibility**: Small public API, Structure-of-Arrays internals, modular systems

## 📝 Implementation Note

This project is undergoing an architectural transition from an aggregate-driven systems approach
to isolated, Reynolds-style steering rules. Two implementations currently exist side-by-side:
- **Legacy** (default): Aggregate-driven SoA steering systems architecture
- **Canonical** (`--canonical` flag): Core scaffolding and the three steering-rule implementations
  exist, but the perception contract, force-budget enforcement, and instrumentation UX remain
  partial; milestones 8-10 and multi-group semantics are also incomplete

For developers: See [`docs/archive/IMPLEMENTATION_EVOLUTION.md`](docs/archive/IMPLEMENTATION_EVOLUTION.md)
for the preserved story of the pivot and `PROJECT_STATUS.md` for current evidence. New features
should target the canonical implementation.

## ✨ Current Capabilities

- ✅ Data-oriented design with Structure of Arrays (SoA) layout
- ⚠️ Fixed-timestep seeded paths exist, but legacy wander and canonical seed-wiring defects
  currently prevent a general reproducibility claim; see
  [issue #17](https://github.com/Chris0Jeky/SwarmingLilMen/issues/17)
- ✅ Agent-genome data structures and mutation API
- ✅ Configurable simulation parameters with presets
- ✅ Legacy uniform-grid boids pipeline and interactive Raylib renderer
- ✅ Opt-in canonical single-group renderer with steering instrumentation
- ✅ 68-test xUnit inventory; the 64 non-performance facts form the hosted CI gate

### Remaining Direction
- **Canonical readiness**: Seeded reproducibility (#17), the perception/spatial-index contract
  (#18), force-budget enforcement (#19), prescribed behavioral scenarios (#41), instrumentation
  UX (#40), boundary/reflection coverage, scale properties/metrics, multi-group behavior, and
  canonical benchmarks
- **Phase 3**: Multi-group interactions, combat, metabolism
- **Phase 4**: Reproduction, evolution, trait drift
- **Phase 5**: SIMD optimization, parallelization, NativeAOT compilation
- **Phase 6**: Additional scenario presets, replay system, advanced metrics

## 🚀 Quick Start

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows, Linux, or macOS
- (Recommended) [JetBrains Rider](https://www.jetbrains.com/rider/) or Visual Studio 2022

### Building
```bash
# Clone the repository
git clone <repository-url>
cd SwarmingLilMen

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Running the Simulation
```bash
# Launch the interactive renderer
dotnet run --project SwarmSim.Render

# Display CLI options
dotnet run --project SwarmSim.Render -- --help

# Run a headless benchmark (no window)
dotnet run --project SwarmSim.Render -- --benchmark --agent-count 20000

# Run the BenchmarkDotNet suite
dotnet run --project SwarmSim.Benchmarks -c Release
```

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
  -p, --preset NAME         Load preset configuration (e.g., peaceful, warbands)
  -c, --config FILE         Load configuration from JSON file
  -n, --agent-count N       Override initial agent count (default: 400)
  -b, --benchmark           Run in headless benchmark mode (no window)
      --canonical           Launch the single-group canonical boids renderer
      --minimal             Launch the minimal debugging harness

Examples:
  SwarmSim.Render
  SwarmSim.Render --preset peaceful
  SwarmSim.Render --config configs/warbands.json -n 5000
  SwarmSim.Render --benchmark --agent-count 20000

Interactive Controls:
  Press H inside the application to toggle the help overlay.
  Press F12 to view snapshot/debug information.

Available presets:
  balanced        - Balanced (Recommended) :: Canonical boids tuning with smooth flocking
  strong-separation - Strong Separation :: Agents prioritize personal space, useful for dense scenes
  tight-flocking  - Tight Flocking :: Emphasizes cohesion/alignment for ribbon-like formations
  fast-loose      - Fast & Loose :: Higher speed ceiling with lighter cohesion
  slow-cohesive   - Slow & Cohesive :: Lower speed with high cohesion for schooling behavior
```

> **Known executable-help defects:**
>
> - `peaceful` and `warbands` in the verbatim help text are JSON configuration names, not registered
>   `--preset` IDs. Use one of the five IDs under **Available presets**, or load
>   `configs/peaceful.json` / `configs/warbands.json` with `--config`. The preset correction is
>   tracked in [issue #38](https://github.com/Chris0Jeky/SwarmingLilMen/issues/38).
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
│   ├── Canonical/          # New canonical boids implementation
│   └── Utils/              # Math and RNG utilities
├── SwarmSim.Render/        # Raylib-cs visualization
├── SwarmSim.Tests/         # xUnit test suite
├── SwarmSim.Benchmarks/    # BenchmarkDotNet performance tests
├── js-demos/               # Browser-based standalone demos
│   ├── boids-basic/        # Interactive boids simulation
│   ├── self-propelled-particles/  # Vicsek model (phase transitions)
│   ├── ant-colony-optimization/   # ACO pathfinding (stigmergy)
│   └── particle-swarm-optimization/ # PSO (continuous optimization)
├── docs/                  # Live guides, roadmap vision, and dated archive
│   ├── CONTROLS.md         # Keyboard/mouse reference
│   ├── PARAMETER_GUIDE.md  # Detailed explanation of SimConfig fields
│   └── archive/            # Historical plans and narratives
├── configs/                # JSON configuration presets
├── CLAUDE.md               # AI assistant guidelines
├── PROJECT_STATUS.md       # Implementation tracker
└── README.md               # This file
```

## 🎮 How to Use

### Creating a Simulation

```csharp
using SwarmSim.Core;

// Create a configuration (or use a preset)
var config = SimConfig.PeacefulFlocks(); // or new SimConfig()

// Initialize the world with a seed for determinism
var world = new World(config, seed: 12345);

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
# Run all tests
dotnet test

# Run with detailed output
dotnet test -v detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~RngTests"

# Run with coverage (requires dotCover or coverlet)
dotnet test --collect:"XPlat Code Coverage"
```

## 📊 Benchmarking

```bash
# Run benchmarks (always use Release configuration)
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
(`SwarmSim.Core/World.cs:119-137,294-320`):
1. **SenseSystem** - Query same-group neighbors and aggregate boids inputs
2. **BehaviorSystem** - Convert separation, alignment, and cohesion inputs to steering
3. **WanderSystem** - Optional wander contribution
4. **IntegrateSystem** - Apply steering, speed constraints, and boundary behavior

Combat, forage, reproduction, metabolism, and lifecycle systems remain future Phase 3+ work.

### Performance Principles
- Allocation-conscious hot paths; there is currently **no enforced allocation gate**
- No LINQ, delegates, or boxing in hot paths
- Tight `for` loops with hoisted invariants
- Direct/static calls over virtual in inner loops

## 📈 Performance Targets and Evidence

The interactive objective is **50k-100k agents at 60 FPS** and the headless objective is **1M+
agents**. Both are targets; neither is currently verified. The latest comparable legacy tick sample
was captured on 2026-07-25 with:

```bash
dotnet build SwarmingLilMen.sln --configuration Release
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj --configuration Release --no-build --filter "Category=Performance" --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true
```

| Evidence | Result | Interpretation |
|----------|--------|----------------|
| Legacy 1k tick | 0.172 ms/tick | One local reported sample, not a renderer FPS result |
| Legacy 10k tick | 8.839 ms/tick | One local reported sample |
| Legacy 50k tick | 162.815 ms/tick (6.14 operations/second) | The 16.67 ms/tick target is unmet |
| Legacy 50k grid rebuild | 0.102 ms | Grid-only measurement, not full simulation cost |
| Canonical throughput | Unmeasured | No canonical BenchmarkDotNet comparison exists |
| Allocations per tick | Unmeasured | No allocation gate exists |

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
The project is now completing canonical readiness before Phase 3; seeded reproducibility,
perception semantics, force-budget enforcement, instrumentation UX, milestones 8-10, multi-group
semantics, canonical performance evidence, and renderer automation remain open. See
[PROJECT_STATUS.md](PROJECT_STATUS.md) for the verified queue.

## 🔧 Troubleshooting

### Build Issues
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Test Failures
- Ensure you're using .NET 8.0 SDK
- Tests require deterministic behavior - avoid time-based randomness

### Performance Issues
- Always benchmark in Release mode: `dotnet run --project SwarmSim.Benchmarks --configuration Release`
- Use Rider's profiling tools (CPU/Memory) for analysis
- Check allocations with `dotnet-counters` or dotMemory

## 📚 Documentation

- [CLAUDE.md](CLAUDE.md) - Guidelines for AI assistants
- [PROJECT_STATUS.md](PROJECT_STATUS.md) - Implementation tracker and roadmap
- [Roadmap vision](docs/ROADMAP-VISION.md) - Aspirational reference; epic #10 is the executable plan
- [Live guides](docs/) - Controls, configuration, renderer, mechanics, and readiness documentation
- [Historical publishing reference](docs/archive/PublishScript.md) - Superseded distribution examples

## 🛠️ Technology Stack

- **Language**: C# 12 (.NET 8.0)
- **Rendering**: [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) 7.0.2
- **Testing**: [xUnit](https://xunit.net/) 2.9.3
- **Benchmarking**: [BenchmarkDotNet](https://benchmarkdotnet.org/) 0.15.6
- **IDE**: JetBrains Rider (recommended) or Visual Studio 2022

## 📝 License

[MIT License](LICENSE) - Feel free to use this project for learning or as a base for your own simulations.

## 🙏 Acknowledgments

- Inspired by boids algorithms, Conway's Game of Life, and emergence theory
- Built with performance lessons from data-oriented design principles
- Architecture influenced by ECS (Entity Component System) patterns

---

**Note**: This project is under active development. Features and APIs may change. Check [PROJECT_STATUS.md](PROJECT_STATUS.md) for the latest progress.
