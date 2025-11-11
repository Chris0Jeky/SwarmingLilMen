# SwarmingLilMen

A high-performance 2D swarm simulation built from first principles in C#/.NET 8.0, targeting 50k-100k interactive agents at 60 FPS with emergent behavior from simple rules.

![Status](https://img.shields.io/badge/status-early%20development-orange)
![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## 🎯 Project Goals

- **Emergence over scripts**: Few simple, composable rules → rich macro patterns
- **Performance**: 50k-100k agents @ 60 FPS interactive; 1M+ headless
- **Determinism**: Fixed timestep, reproducible with seeded RNG, record/replay
- **Observability**: Metrics, snapshots, profiling, property tests, benchmarks
- **Extensibility**: Small public API, Structure-of-Arrays internals, modular systems

## ✨ Features (Planned)

### Current (Phase 0 - 80% Complete)
- ✅ Data-oriented design with Structure of Arrays (SoA) layout
- ✅ Deterministic simulation with seeded random number generation
- ✅ Agent genetics with mutation
- ✅ Configurable simulation parameters with presets
- ✅ Comprehensive test suite (21 tests)
- ⏳ Basic visualization (in progress)

### Upcoming Phases
- **Phase 1**: Spatial partitioning (uniform grid), basic movement systems
- **Phase 2**: Boids flocking behavior (separation, alignment, cohesion)
- **Phase 3**: Multi-group interactions, combat, metabolism
- **Phase 4**: Reproduction, evolution, trait drift
- **Phase 5**: SIMD optimization, parallelization, NativeAOT compilation
- **Phase 6**: Presets, replay system, advanced metrics

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
- Press **H** to toggle the in-app help overlay with every control.
- Press **F12** to toggle the snapshot/debug overlay (shows interpolation details).
- See [`CONTROLS.md`](CONTROLS.md) for a printable list of every mouse/keyboard shortcut.

### Command-Line Options
SwarmSim.Render now accepts a lightweight CLI so you can start with presets or external configs without modifying code:

```
SwarmSim.Render [OPTIONS]

  -h, --help                Show help and exit
  -v, --version             Show version information
  -l, --list-presets        Print available presets
  -p, --preset NAME         Load a preset (balanced, strong-separation, etc.)
  -c, --config FILE         Load configuration from JSON (see configs/ folder)
  -n, --agent-count N       Override initial agent count (default 400)
  -b, --benchmark           Run a headless benchmark (no window)
      --minimal             Launch the minimal debugging harness
```

Example:
```bash
dotnet run --project SwarmSim.Render -- --preset fast-loose --agent-count 5000
dotnet run --project SwarmSim.Render -- --config configs/warbands.json
```

Sample configuration files live in the [`configs/`](configs) directory and demonstrate how to tweak `SimConfig` via JSON.
For recipes see [`CONFIGURATION_COOKBOOK.md`](CONFIGURATION_COOKBOOK.md) and for parameter effects see [`PARAMETER_GUIDE.md`](PARAMETER_GUIDE.md).

## 📁 Project Structure

```
SwarmingLilMen/
├── SwarmSim.Core/          # Core simulation library
│   ├── Genome.cs           # Agent genetics
│   ├── AgentState.cs       # Behavioral state flags
│   ├── SimConfig.cs        # Configuration system
│   ├── World.cs            # Main simulation with SoA data
│   └── Utils/              # Math and RNG utilities
├── SwarmSim.Render/        # Raylib-cs visualization
├── SwarmSim.Tests/         # xUnit test suite
├── SwarmSim.Benchmarks/    # BenchmarkDotNet performance tests
├── filesAndResources/      # Documentation and scripts
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

// Aggressive warbands (groups fight)
var combat = SimConfig.Warbands();

// Rapid evolution with high mutation rates
var evolution = SimConfig.RapidEvolution();

// Or customize everything
var custom = new SimConfig
{
    WorldWidth = 1920f,
    WorldHeight = 1080f,
    MaxSpeed = 150f,
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
Stateless systems run in sequence each tick:
1. **SenseSystem** - Query neighbors via spatial grid
2. **BehaviorSystem** - Boids rules → forces
3. **CombatSystem** - Resolve attacks
4. **ForageSystem** - Energy gain from food
5. **ReproductionSystem** - Spawn offspring with mutations
6. **MetabolismSystem** - Energy drain, aging
7. **IntegrateSystem** - Apply forces, update positions
8. **LifecycleSystem** - Compact dead agents

### Performance Principles
- **Zero allocations** during tick loop (enforced by tests)
- No LINQ, delegates, or boxing in hot paths
- Tight `for` loops with hoisted invariants
- Direct/static calls over virtual in inner loops

## 📈 Performance Targets

| Phase | Agents | FPS | Allocs/Tick | Status |
|-------|--------|-----|-------------|--------|
| P0    | 1k     | TBD | TBD         | In Progress |
| P1    | 50k    | 60+ | 0           | Planned |
| P2    | 50k    | 60+ | 0           | Planned |
| P5    | 200k+  | 60+ | 0           | Planned |

## 🤝 Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, IDE configuration, and workflow guidelines.

Quick checklist:
- Follow the architecture principles in `CLAUDE.md`
- Write tests for new features
- Keep the hot path allocation-free
- Update `PROJECT_STATUS.md` when completing milestones
- Profile performance regularly with dotTrace/dotMemory

## 📋 Development Status

This project is in **early development** (Phase 0). See [PROJECT_STATUS.md](PROJECT_STATUS.md) for detailed implementation progress.

**Current Phase**: P0 - Foundation (80% complete)
- ✅ Core data structures
- ✅ World management
- ✅ Test suite
- ⏳ Rendering layer

**Next Phase**: P1 - Spatial Grid & Basic Movement

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
- Always benchmark in Release mode: `dotnet run -c Release`
- Use Rider's profiling tools (CPU/Memory) for analysis
- Check allocations with `dotnet-counters` or dotMemory

## 📚 Documentation

- [CLAUDE.md](CLAUDE.md) - Guidelines for AI assistants
- [PROJECT_STATUS.md](PROJECT_STATUS.md) - Implementation tracker and roadmap
- [Master Plan](filesAndResources/swarming_lil_men_master_plan_v_1.md) - Detailed design document
- [Publish Scripts](filesAndResources/PublishScript.md) - Distribution and CI/CD setup

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
