# Quick Start Guide

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
cd "C:\Users\jekyt\Desktop\Printer Config\Others\Git\SwarmingLilMen"
```

## 2. Build

```bash
dotnet build
```

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`

## 3. Run Tests

```bash
dotnet test
```

Expected output: `Passed!  - Failed: 0, Passed: 21`

## 4. Play with the Code

### Option A: Use the World Programmatically

Create a simple test file to experiment:

```csharp
// In SwarmingLilMen/Program.cs (if it still exists)
using SwarmSim.Core;

var config = SimConfig.PeacefulFlocks();
var world = new World(config, seed: 12345);

// Spawn 100 agents in a circle
world.SpawnAgentsInCircle(
    centerX: 500f,
    centerY: 500f,
    radius: 100f,
    count: 100,
    group: 0
);

Console.WriteLine($"Spawned {world.Count} agents");

// Run 100 simulation ticks
for (int i = 0; i < 100; i++)
{
    world.Tick();
}

// Get stats
var stats = world.GetStats();
Console.WriteLine($"After 100 ticks:");
Console.WriteLine($"  Alive: {stats.AliveAgents}");
Console.WriteLine($"  Avg Energy: {stats.AverageEnergy:F1}");
Console.WriteLine($"  Avg Speed: {stats.AverageSpeed:F1}");
```

Run it:
```bash
dotnet run --project SwarmingLilMen.csproj
```

### Option B: Run the Renderer (When Implemented)

```bash
dotnet run --project SwarmSim.Render
```

**Note**: Rendering is not yet implemented (20% of Phase 0 remaining).

## 5. Explore the Code

### Key Files to Look At

**Core Simulation**:
- `SwarmSim.Core/World.cs` - Main simulation loop
- `SwarmSim.Core/Genome.cs` - Agent genetics
- `SwarmSim.Core/SimConfig.cs` - Configuration options

**Utilities**:
- `SwarmSim.Core/Utils/Rng.cs` - Random number generation
- `SwarmSim.Core/Utils/MathUtils.cs` - Vector math

**Tests**:
- `SwarmSim.Tests/WorldTests.cs` - World behavior tests
- `SwarmSim.Tests/RngTests.cs` - RNG determinism tests

## 6. Try the Configuration Presets

```csharp
// Peaceful flocking
var peaceful = SimConfig.PeacefulFlocks();

// Combat between groups
var warbands = SimConfig.Warbands();

// High mutation rates
var evolution = SimConfig.RapidEvolution();

// Fully custom
var custom = new SimConfig
{
    WorldWidth = 800f,
    WorldHeight = 600f,
    MaxSpeed = 100f,
    SenseRadius = 40f,
    SeparationWeight = 2.0f,
    // ... many more options
};
```

## 7. Experiment with Parameters

Try changing these in SimConfig:

**Physics**:
- `MaxSpeed` - How fast agents can move (50-500)
- `Friction` - Velocity decay per tick (0.9-1.0)
- `BoundaryMode` - Wrap, Reflect, or Clamp

**Behavior** (when systems are implemented):
- `SenseRadius` - How far agents can "see" (20-100)
- `SeparationWeight` - Avoid neighbors (0-3)
- `AlignmentWeight` - Match neighbor velocity (0-3)
- `CohesionWeight` - Move toward group center (0-3)

**Energy**:
- `InitialEnergy` - Starting energy (50-200)
- `BaseDrain` - Energy per second (0.1-1.0)

**Evolution**:
- `MutationRate` - Probability of trait mutation (0.01-0.5)
- `MutationStdDev` - Size of mutations (0.1-0.5)

## 8. Run Benchmarks

```bash
dotnet run --project SwarmSim.Benchmarks -c Release
```

This will create `BenchmarkDotNet.Artifacts/` with performance results.

## Next Steps

1. **Read [README.md](README.md)** - Full project overview
2. **Read [CONTRIBUTING.md](CONTRIBUTING.md)** - Development setup and IDE configuration
3. **Check [PROJECT_STATUS.md](PROJECT_STATUS.md)** - See what's implemented and what's next
4. **Explore Tests** - See how the system behaves in `SwarmSim.Tests/`

## Common Issues

### "dotnet: command not found"
Install .NET 8.0 SDK from: https://dotnet.microsoft.com/download

### Tests fail with "File not found"
Run `dotnet restore` first

### Render window doesn't open
Rendering is not yet implemented (Phase 0 - 20% remaining work)

### Want to contribute?
See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines!

---

**Questions?** Check the [README.md](README.md) or [CONTRIBUTING.md](CONTRIBUTING.md) for more details.
