# Contributing to SwarmingLilMen

Thank you for your interest in contributing! This document provides guidelines for development, IDE setup, and workflow.

## 📋 Table of Contents
- [Development Environment Setup](#development-environment-setup)
- [IDE Configuration (Rider)](#ide-configuration-rider)
- [Project Architecture](#project-architecture)
- [Development Workflow](#development-workflow)
- [Testing Guidelines](#testing-guidelines)
- [Performance Guidelines](#performance-guidelines)
- [Code Style](#code-style)
- [Publishing Builds](#publishing-builds)

---

## Development Environment Setup

### Required Software
1. **.NET 8.0 SDK** or later
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify: `dotnet --version` (should be 8.0.x or higher)

2. **Git**
   - For version control and collaboration

3. **IDE** (choose one):
   - **JetBrains Rider** (recommended) - Best C# experience, includes profilers
   - Visual Studio 2022 - Full-featured, Windows only
   - VS Code with C# extension - Lightweight option

### Optional Tools
- **dotCover** - Code coverage (included with Rider Ultimate)
- **dotTrace** - CPU profiling (included with Rider Ultimate)
- **dotMemory** - Memory profiling (included with Rider Ultimate)
- **BenchmarkDotNet** - Already in project, no install needed

### Initial Setup
```bash
# Clone the repository
git clone <repository-url>
cd SwarmingLilMen

# Restore NuGet packages
dotnet restore

# Verify build
dotnet build

# Run tests to ensure everything works
dotnet test

# (Optional) Run benchmarks
dotnet run --project SwarmSim.Benchmarks -c Release
```

---

## IDE Configuration (Rider)

### Opening the Project
1. Launch JetBrains Rider
2. **Open** → Select `SwarmingLilMen.sln`
3. Wait for Rider to restore NuGet packages and index

### Recommended Settings

#### Solution Configuration
- Go to **Tools → Options → Build → Solution**
- Enable "Build project on save" for fast iteration
- Set default configuration to **Debug** for development, **Release** for benchmarking

#### Code Style
The project uses standard C# conventions:
- **PascalCase** for types, methods, properties
- **_camelCase** for private fields
- **File-scoped namespaces** (C# 10+)
- **Nullable reference types** enabled

Rider should pick up these settings from `Directory.Build.props`.

#### Run Configurations

Create these run configurations in Rider:

**1. Run Renderer (Development)**
```
Name: SwarmSim.Render (Debug)
Project: SwarmSim.Render
Configuration: Debug
Arguments: (none)
```

**2. Run Tests**
```
Name: All Tests
Type: .NET Test
Test Runner: xUnit
Projects: SwarmSim.Tests
```

**3. Run Benchmarks**
```
Name: Benchmarks (Release)
Project: SwarmSim.Benchmarks
Configuration: Release
Arguments: (optional) --filter *
```

**4. CPU Profile**
```
Name: Profile CPU
Project: SwarmSim.Render
Configuration: Release
Enable profiling: dotTrace
Profile startup: Yes
```

**5. Memory Profile**
```
Name: Profile Memory
Project: SwarmSim.Render
Configuration: Release
Enable profiling: dotMemory
Profile startup: Yes
```

### Debugging

#### Breakpoints
- Set breakpoints in **World.cs** `Tick()` method to inspect agent data
- Use conditional breakpoints: `Count > 1000` to trigger on specific conditions
- Pin variables to watch: Right-click variable → Pin to view

#### Data Visualizers
When debugging arrays:
```csharp
// View first 10 agents in debugger
var preview = Enumerable.Range(0, Math.Min(10, Count))
    .Select(i => new { X = X[i], Y = Y[i], Energy = Energy[i] })
    .ToArray();
```

#### Performance Debugging
- Use **Rider's Performance Profiler** (Run → Profile 'SwarmSim.Render')
- Check allocations with **dotMemory** (Run → Profile Memory 'SwarmSim.Render')
- Timeline profiler shows frame times and GC pauses

---

## Project Architecture

### Solution Structure
```
SwarmingLilMen.sln
├── SwarmSim.Core/          - Core library (no dependencies)
│   ├── Genome.cs           - Agent genetics (readonly record struct)
│   ├── AgentState.cs       - Behavioral flags
│   ├── SimConfig.cs        - Configuration with validation
│   ├── World.cs            - Main simulation (SoA layout)
│   └── Utils/
│       ├── Rng.cs          - Deterministic RNG
│       └── MathUtils.cs    - Vector math utilities
├── SwarmSim.Render/        - Visualization (references Core)
├── SwarmSim.Tests/         - Tests (references Core)
└── SwarmSim.Benchmarks/    - Benchmarks (references Core)
```

### Key Design Principles

**1. Structure of Arrays (SoA)**
```csharp
// Agent data stored in parallel arrays
float[] X, Y;              // NOT: Agent[] { float X, Y }
float[] Vx, Vy;            // Cache-friendly, SIMD-ready
```

**2. Zero Allocations in Hot Path**
```csharp
// BAD: Allocates on every tick
var forces = new List<Vector2>();

// GOOD: Pre-allocated arrays
float[] Fx = new float[Capacity];
Array.Clear(Fx, 0, Count); // Reuse each tick
```

**3. Determinism**
```csharp
// BAD: Non-deterministic
var rng = new Random();

// GOOD: Seeded for reproducibility
var rng = new Rng(seed: 12345);
```

**4. Systems Architecture**
Each system is stateless and operates on World data:
```csharp
public interface ISimSystem
{
    void Run(World world, float dt);
}
```

---

## Development Workflow

### Typical Development Cycle
1. **Pull latest changes**: `git pull origin main`
2. **Create feature branch**: `git checkout -b feature/my-feature`
3. **Make changes** to code
4. **Run tests**: `dotnet test` (ensure all pass)
5. **Run simulation**: `dotnet run --project SwarmSim.Render`
6. **Profile if needed**: Use Rider's CPU/Memory profilers
7. **Update PROJECT_STATUS.md**: Check off completed items
8. **Commit**: `git commit -m "Add feature X"`
9. **Push**: `git push origin feature/my-feature`
10. **Create Pull Request** (when ready)

### Before Committing
- [ ] All tests pass: `dotnet test`
- [ ] Solution builds: `dotnet build`
- [ ] No warnings in build output
- [ ] Performance verified if hot path changed: Run benchmarks
- [ ] PROJECT_STATUS.md updated if milestone reached

### Git Commit Messages
Follow conventional commits:
```
feat: Add genome mutation with Gaussian noise
fix: Correct wrapping behavior at world boundaries
test: Add property tests for spatial grid
perf: Optimize neighbor queries with caching
docs: Update README with quick start guide
```

---

## Testing Guidelines

### Types of Tests

**1. Unit Tests** - Test individual components
```csharp
[Fact]
public void Rng_SameSeed_ProducesSameSequence()
{
    var rng1 = new Rng(12345);
    var rng2 = new Rng(12345);
    Assert.Equal(rng1.Next(), rng2.Next());
}
```

**2. Property Tests** - Verify invariants
```csharp
[Fact]
public void World_SpeedNeverExceedsMax()
{
    var world = CreateWorldWith100Agents();
    for (int i = 0; i < 1000; i++)
    {
        world.Tick();
        Assert.True(AllSpeedsWithinLimit(world));
    }
}
```

**3. Determinism Tests** - Ensure reproducibility
```csharp
[Fact]
public void World_SameSeed_ProducesSameResults()
{
    var world1 = new World(config, seed: 42);
    var world2 = new World(config, seed: 42);
    // ... spawn same agents, run same ticks ...
    Assert.Equal(world1.X[0], world2.X[0]);
}
```

### Running Tests
```bash
# All tests
dotnet test

# Specific test class
dotnet test --filter "FullyQualifiedName~RngTests"

# Specific test method
dotnet test --filter "FullyQualifiedName~RngTests.Determinism_SameSeed_ProducesSameSequence"

# With coverage (requires coverlet)
dotnet test --collect:"XPlat Code Coverage"

# Verbose output
dotnet test -v detailed
```

### Test Organization
```
SwarmSim.Tests/
├── RngTests.cs           - Random number generation
├── WorldTests.cs         - World lifecycle and agents
├── GenomeTests.cs        - Genetics and mutation
├── SpatialGridTests.cs   - (future) Grid correctness
└── SystemTests.cs        - (future) Individual systems
```

---

## Performance Guidelines

### Rules for Hot Paths (Tick Loop)

**DO:**
- ✅ Use `for` loops with index
- ✅ Pre-allocate all arrays
- ✅ Use `stackalloc` for small temp buffers
- ✅ Hoist invariant calculations outside loops
- ✅ Use direct/static method calls
- ✅ Use `readonly ref` for large structs

**DON'T:**
- ❌ Use LINQ in hot paths
- ❌ Allocate collections (List, Dictionary)
- ❌ Use delegates or lambdas
- ❌ Box value types
- ❌ Throw exceptions for control flow
- ❌ Use virtual/interface calls in inner loops

### Profiling Workflow

**CPU Profiling** (dotTrace):
1. Run → Profile → Profile 'SwarmSim.Render'
2. Select **Timeline** or **Sampling** mode
3. Run simulation for 10-30 seconds
4. Get snapshot
5. Analyze **Hot Spots** (methods consuming most CPU)
6. Check **Call Tree** for expensive call chains

**Memory Profiling** (dotMemory):
1. Run → Profile → Profile Memory 'SwarmSim.Render'
2. Run simulation until tick loop stabilizes
3. Force GC: Memory → Force GC
4. Take memory snapshot
5. Check **Allocations** view for new objects during tick
6. Target: **0 bytes allocated in Tick() method**

**Benchmarking**:
```bash
# Run benchmarks (always Release)
dotnet run --project SwarmSim.Benchmarks -c Release

# Results in: BenchmarkDotNet.Artifacts/results/
```

### Performance Checklist
- [ ] Run dotMemory: 0 allocations in Tick()
- [ ] Run dotTrace: No unexpected hot spots
- [ ] Run benchmarks: Compare against baseline
- [ ] Test at scale: 50k agents @ 60 FPS

---

## Code Style

### Naming Conventions
```csharp
// Types, methods, properties: PascalCase
public class World { }
public void Tick() { }
public int Count { get; private set; }

// Private fields: _camelCase
private float[] _forces;

// Local variables: camelCase
var agentCount = 100;

// Constants: PascalCase
public const float MaxSpeed = 200f;
```

### File Organization
```csharp
using SwarmSim.Core.Utils;  // Usings at top

namespace SwarmSim.Core;     // File-scoped namespace

/// <summary>
/// XML doc comments for public APIs
/// </summary>
public class World
{
    // Fields first
    private float[] _x;

    // Properties next
    public int Count { get; private set; }

    // Constructor
    public World() { }

    // Public methods
    public void Tick() { }

    // Private methods
    private void Integrate() { }
}
```

### Comments
```csharp
// Inline comments for non-obvious logic
float invLen = 1f / len;  // Avoid division in tight loop

/// <summary>
/// XML docs for public APIs explaining purpose, not implementation
/// </summary>
```

---

## Publishing Builds

See [filesAndResources/PublishScript.md](filesAndResources/PublishScript.md) for detailed publishing instructions.

### Quick Publish (Local)

**JIT (Framework-Dependent)**:
```bash
dotnet publish SwarmSim.Render -c Release -o ./publish/jit
```

**ReadyToRun (Self-Contained, Single File)**:
```bash
dotnet publish SwarmSim.Render -c Release -r win-x64 \
  --self-contained true \
  -p:PublishReadyToRun=true \
  -p:PublishSingleFile=true \
  -o ./publish/r2r
```

**NativeAOT (Smallest, Fastest)**:
```bash
dotnet publish SwarmSim.Render -c Release -r win-x64 \
  --self-contained true \
  -p:PublishAot=true \
  -p:PublishSingleFile=true \
  -o ./publish/aot
```

### When to Publish
- **Development**: Use `dotnet run` (no publishing needed)
- **Testing distributable**: Use ReadyToRun (R2R)
- **Performance benchmarking**: Use NativeAOT
- **Releases**: Create all three flavors

---

## What Needs Manual Setup

### 1. IDE Configuration
**Action Required**: Open `SwarmingLilMen.sln` in Rider and create run configurations (see [IDE Configuration](#ide-configuration-rider) section above).

### 2. Profiling Tools
**Action Required**: If using Rider Community, profiling tools (dotTrace, dotMemory) are not available. Upgrade to Rider Ultimate or use alternative profilers.

### 3. GitHub Actions (Optional)
**Action Required**: If you want CI/CD:
1. Push repository to GitHub
2. Copy `.github/workflows/ci.yml` from PublishScript.md
3. GitHub Actions will run automatically on push

### 4. Publish Profiles (Optional)
**Action Required**: To use Rider's "Publish..." UI:
1. Create `SwarmSim.Render/Properties/PublishProfiles/` folder
2. Copy `.pubxml` files from PublishScript.md
3. Files will appear in Rider's Publish dialog

### 5. Cross-Platform Testing
**Action Required**: Test on your target platforms:
- **Windows**: Should work out of the box
- **Linux**: May need `apt-get install libraylib-dev`
- **macOS**: May need Raylib via Homebrew

### 6. License File
**Action Required**: If distributing, add a LICENSE file (MIT suggested in README).

---

## Questions or Issues?

- Check [README.md](README.md) for general project info
- Check [PROJECT_STATUS.md](PROJECT_STATUS.md) for current implementation status
- Check [CLAUDE.md](CLAUDE.md) for AI assistant guidelines (useful for understanding architecture)
- Open an issue on GitHub (once repository is public)

---

**Happy coding! 🎉**
