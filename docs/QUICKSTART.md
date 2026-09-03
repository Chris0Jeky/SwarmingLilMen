# Quick Start Guide

> [`PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and performance state.

Get SwarmingLilMen running in 5 minutes!

## Prerequisites Check

Open a terminal and verify you have .NET 8.0:
```bash
dotnet --version
# Should show 8.0.x or higher
```

If not, install from: https://dotnet.microsoft.com/download/dotnet/8.0

## 1. Get the Code

```bash
git clone <repository-url>
cd SwarmingLilMen
```

## 2. Build

```bash
dotnet build
```

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`

## 3. Run Tests

```bash
dotnet test SwarmingLilMen.sln --configuration Release --filter "Category!=Performance"
```

Expect `Failed: 0`. The pass count grows with every merge, so the live figure lives in the
verified-state block at the top of [`../PROJECT_STATUS.md`](../PROJECT_STATUS.md) rather than being
copied here — a number pinned in a quickstart goes stale silently and makes a correct run look
wrong. The four timing facts are excluded by this filter and live in the separate `Performance`
category.

## 4. Play with the Code

### Option A: Run Headless

Use the renderer project's headless benchmark mode; there is no root executable project.

```bash
dotnet run --project SwarmSim.Render --configuration Release -- --benchmark --agent-count 1000
```

### Option B: Run the Renderer (Interactive)

```bash
# Launch with the built-in renderer defaults (no preset applied; wander is off)
dotnet run --project SwarmSim.Render

# List command-line options
dotnet run --project SwarmSim.Render -- --help

# Start with 5k agents using the fast-loose preset
dotnet run --project SwarmSim.Render -- --preset fast-loose --agent-count 5000

# Load a JSON config from the configs/ directory
dotnet run --project SwarmSim.Render -- --config configs/warbands.json

# Launch the opt-in single-group canonical renderer
dotnet run --project SwarmSim.Render -- --canonical
```

Tips:
- Press **H** for the partial in-app quick reference; use [`CONTROLS.md`](CONTROLS.md) for the
  complete list (runtime overlay drift is tracked in
  [issue #39](https://github.com/Chris0Jeky/SwarmingLilMen/issues/39)).
- Legacy renderer only: press **F12** for the snapshot/debug overlay.
- Canonical renderer only: press **O** for the interaction overlay and **Tab** to cycle the tracked
  boid.
- See [`CONTROLS.md`](CONTROLS.md) for the complete reference sheet.
- For parameter explanations see [`PARAMETER_GUIDE.md`](PARAMETER_GUIDE.md).

## 5. Explore the Code

### Key Files to Look At

**Core Simulation**:
- `SwarmSim.Core/World.cs` - Main simulation loop
- `SwarmSim.Core/Canonical/CanonicalWorld.cs` - Opt-in canonical steering path
- `SwarmSim.Core/Genome.cs` - Agent genetics
- `SwarmSim.Core/SimConfig.cs` - Configuration options

**Utilities**:
- `SwarmSim.Core/Utils/Rng.cs` - Random number generation
- `SwarmSim.Core/Utils/MathUtils.cs` - Vector math

**Tests**:
- `SwarmSim.Tests/WorldTests.cs` - World behavior tests
- `SwarmSim.Tests/CanonicalBoidsTests.cs` - Canonical steering and determinism tests
- `SwarmSim.Tests/SimulationRunnerTests.cs` - Fixed-step and interpolation tests
- `SwarmSim.Tests/CommandLineOptionsTests.cs` - CLI parsing/help tests
- `SwarmSim.Tests/RngTests.cs` - RNG determinism tests

## 6. Try the Configuration Presets

```csharp
// Peaceful flocking
var peaceful = SimConfig.PeacefulFlocks();

// Warbands-flavoured group settings; combat is future Phase 3 work
var warbands = SimConfig.Warbands();

// High mutation settings; reproduction/evolution is not active yet
var evolution = SimConfig.RapidEvolution();

// Fully custom
var custom = new SimConfig
{
    WorldWidth = 800f,
    WorldHeight = 600f,
    MaxSpeed = 10f,  // world-position units per second
    MaxForce = 2.5f,
    SenseRadius = 40f,
    SeparationWeight = 2.0f,
    // ... many more options
};
```

## 7. Experiment with Parameters

Try changing these in SimConfig:

**Physics**:
- `MaxSpeed` - Velocity cap in world-position units per second; current renderer presets use 5-15
- `Friction` - Velocity-retention multiplier per simulation step (0.9-1.0), used only by
  `SpeedModel.Damped`
- `BoundaryMode` - Wrap, Reflect, or Clamp

**Active legacy behavior** (current registered-preset values, not validation limits):
- `SenseRadius` - How far agents can "see" (80-120 across the shipped presets)
- `SeparationWeight` - Avoid neighbors (2-10)
- `AlignmentWeight` - Match neighbor velocity (2-5)
- `CohesionWeight` - Move toward group center (0.3-3)

`SimConfig` and the live parameter editor allow wider experiments; these ranges only summarize the
five registered renderer presets (`SwarmSim.Render/Program.cs:110-215`).

**Reserved Phase 3 energy settings** (only initial values are active today):
- `InitialEnergy` - Starting energy (50-200)
- `BaseDrain` - Energy per second (0.1-1.0)

**Reserved Phase 4 evolution settings**:
- `MutationRate` - Probability of trait mutation (0.01-0.5)
- `MutationStdDev` - Size of mutations (0.1-0.5)

## 8. Run Benchmarks

```bash
dotnet run --project SwarmSim.Benchmarks -c Release
```

This will create `BenchmarkDotNet.Artifacts/` with performance results.

## Next Steps

1. **Read [README.md](../README.md)** - Full project overview
2. **Read [CONTRIBUTING.md](../CONTRIBUTING.md)** - Development setup and IDE configuration
3. **Check [PROJECT_STATUS.md](../PROJECT_STATUS.md)** - See what's implemented and what's next
4. **Explore Tests** - See how the system behaves in `SwarmSim.Tests/`

## Common Issues

### "dotnet: command not found"
Install .NET 8.0 SDK from: https://dotnet.microsoft.com/download

### Tests fail with "File not found"
Run `dotnet restore` first

### Render window doesn't open
Confirm `dotnet run --project SwarmSim.Render -- --help` works, then check that the machine has an
interactive graphics session and the required Raylib native dependencies. The renderer is
implemented; this symptom is an environment/runtime problem, not a missing project phase.

### Want to contribute?
See [CONTRIBUTING.md](../CONTRIBUTING.md) for guidelines!

---

**Questions?** Check the [README.md](../README.md) or [CONTRIBUTING.md](../CONTRIBUTING.md) for more details.
- Sample configuration files live in the [`configs/`](../configs) directory; copy and tweak them to experiment with `SimConfig`.
