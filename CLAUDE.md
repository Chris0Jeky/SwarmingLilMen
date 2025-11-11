# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SwarmingLilMen is a high-performance 2D swarm simulation built from first principles in C#/.NET 8.0. The goal is to simulate 50k-100k agents at 60 FPS with emergent behavior from simple rules. This is a data-oriented, performance-critical project focusing on deterministic simulation, zero-allocation hot paths, and extensive observability.

## Solution Structure

The solution follows a clean multi-project architecture with clear separation of concerns:

- **SwarmSim.Core** - Core simulation library with data-oriented design (SoA layout), deterministic systems, allocation-free tick loop
- **SwarmSim.Render** - Raylib-cs visualization layer for live interaction and parameter tweaking (references SwarmSim.Core)
- **SwarmSim.Tests** - xUnit test suite for property tests, unit tests, and regression tests (references SwarmSim.Core)
- **SwarmSim.Benchmarks** - BenchmarkDotNet performance benchmarking suite (references SwarmSim.Core)

All projects share common settings via `Directory.Build.props`:
- Target framework: .NET 8.0
- Nullable reference types enabled
- Implicit usings enabled
- Treat warnings as errors
- Deterministic builds
- Embedded debug symbols

## Build and Development Commands

### Quick Reference
```bash
# Run renderer
dotnet run --project SwarmSim.Render/SwarmSim.Render.csproj

# Run benchmarks (always use Release for accurate results)
dotnet run --project SwarmSim.Benchmarks/SwarmSim.Benchmarks.csproj -c Release

# Run tests
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj
```

### Building
```bash
# Build entire solution
dotnet build SwarmingLilMen.sln

# Build specific project
dotnet build SwarmSim.Core/SwarmSim.Core.csproj

# Release build for performance testing
dotnet build -c Release
```

### Running Projects
```bash
# Run renderer
dotnet run --project SwarmSim.Render/SwarmSim.Render.csproj

# Run benchmarks (always use Release for accurate results)
dotnet run --project SwarmSim.Benchmarks/SwarmSim.Benchmarks.csproj -c Release
```

### Testing
```bash
# Run all tests
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj

# Run specific test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"

# Run with detailed output
dotnet test -v detailed
```

### Restore Dependencies
```bash
dotnet restore
```

## Architecture Principles

### Data-Oriented Design (SoA)
The core uses Structure of Arrays (SoA) layout for cache efficiency and SIMD potential:
- Agent data stored in parallel arrays: `X[]`, `Y[]`, `Vx[]`, `Vy[]`, `Energy[]`, `Group[]`, etc.
- Avoid `List<T>`, LINQ, or allocations in hot paths
- Use `ArrayPool<T>` for temporary buffers
- Fixed capacity with free-list for dead agents; periodic compaction

### Systems Pipeline (ECS-ish)
Stateless systems run in sequence each tick:
1. **SenseSystem** - Build spatial queries via uniform grid
2. **BehaviorSystem** - Boids rules (separation, alignment, cohesion) + group aggression
3. **CombatSystem** - Resolve attacks within range
4. **ForageSystem** - Sample food field and add energy
5. **ReproductionSystem** - Spawn children with mutated genomes
6. **MetabolismSystem** - Energy drain, aging, mark deaths
7. **IntegrateSystem** - Apply forces, update velocities and positions
8. **LifecycleSystem** - Compact dead slots, emit events

Each system reads current state and writes to scratch buffers (forces/decisions), then integrates in a separate pass.

### Spatial Partitioning
Uses uniform grid (linked lists per cell) for O(1) neighbor queries:
- `Head[cell]` + `Next[agent]` arrays rebuilt each tick
- Cell size ≈ interaction radius for optimal performance
- Query scans 3×3 neighborhood for local agents

### Determinism
- Fixed timestep simulation
- Seeded RNG (no time-based randomness in core)
- Stable iteration order
- Record/replay capability planned
- Snapshot pipeline uses immutable `SimSnapshot` objects (capture + mutation versions).
- Renderer only interpolates when snapshot versions match; otherwise it renders the latest snapshot with `alpha = 0`.
- Any world mutation outside `SimulationRunner.Advance()` must call `SimulationRunner.NotifyWorldMutated()` (see `Program.ForceSnapshotRefresh`) to keep interpolation safe.

### Runtime UX & CLI
- `SwarmSim.Render` now exposes a CLI (`--help`, `--version`, `--list-presets`, `--preset`, `--config`, `--agent-count`, `--benchmark`, `--minimal`).
- Presets live in code and JSON examples in `configs/`. `CONFIGURATION_COOKBOOK.md` documents parameter ranges and recipes.
- In-app overlays:
  - **H** toggles the interactive help panel (spawning, visualization, parameters).
  - **F12** toggles the debug overlay (snapshot counts, versions, accumulator/alpha).

## Performance Requirements

### Hot Path Rules
- **Zero allocations** during tick loop (enforce with dotMemory tests)
- No LINQ, delegates, boxing, or exceptions in critical paths
- Tight `for` loops with hoisted invariant calculations
- Prefer direct/static calls over virtual/interface in inner loops
- SoA layout with `readonly ref` accessors where appropriate

### Performance Targets
- 50k-100k agents @ 60 FPS (interactive with rendering)
- 1M+ agents (headless)
- Zero bytes allocated per tick in Release builds

### Weekly Performance Ritual
- Run BenchmarkDotNet suite in Release mode; track medians
- Profile with dotTrace at 50k/100k agents; document top-3 hotspots
- Verify zero allocations with dotMemory before/after 3-second runs

### Future Optimizations
- SIMD with `System.Numerics.Vector2` / `Vector<T>`
- Row/tile parallelism with private worker accumulators
- NativeAOT publish for headless scenarios

## Code Style and Standards

### Conventions
- `PascalCase` for types, methods, properties
- `_camelCase` for private fields
- File-scoped namespaces
- Nullable reference types enabled
- Treat warnings as errors (especially in Core/Tests)

### Core Library Guidelines
- Document invariants at the top of each system (inputs, writes, side effects)
- No exceptions in hot paths; use return codes/flags
- Guard inputs once at API boundaries
- Use `readonly record struct` for DTOs (e.g., `Genome`, config values)

## Testing Strategy

### Test Types
- **Unit tests**: Deterministic micro-scenarios (3-10 agents) for individual rules
- **Property tests**: Verify invariants (speed limits, no NaN/Inf, grid correctness vs brute-force)
- **Regression tests**: Snapshot state hashes after K ticks with fixed seed; compare across runs
- **Performance gates**: BenchmarkDotNet baselines with regression detection

### Test Execution
```bash
# Run all tests with coverage (when dotCover configured)
dotnet test --collect:"XPlat Code Coverage"

# Run property tests only
dotnet test --filter "Category=Property"
```

## Key Dependencies

- **Raylib-cs 7.0.2** - Rendering and windowing (SwarmSim.Render)
- **BenchmarkDotNet 0.15.6** - Performance benchmarking (SwarmSim.Benchmarks)
- **xUnit 2.9.3** - Unit testing framework (SwarmSim.Tests)
- **Microsoft.NET.Test.Sdk 17.8.0** - Test SDK (SwarmSim.Tests)
- **coverlet.collector 6.0.0** - Code coverage collection (SwarmSim.Tests)

All projects reference SwarmSim.Core as their central dependency.

## Configuration and Presets

Configuration via `SimConfig` (JSON-serializable):
- Physics parameters (dt, maxSpeed, friction)
- Interaction radii and weights
- Energy costs and thresholds
- Group aggression matrix
- Reproduction settings (thresholds, mutation rate/std)
- World size and boundary conditions (wrap/reflect)

### Planned Presets
- **Peaceful Flocks**: High cohesion/alignment, no combat, low energy drain
- **Warbands**: Multiple groups with positive aggression values, combat enabled
- **Rapid Evolution**: High mutation rates, cheap reproduction for trait drift observation

## Observability

### Metrics (per tick)
- Agent counts, births/deaths
- Average energy, speed, health
- CPU time per system
- Neighbor query statistics

### Events
Event DTOs emitted via non-blocking `Channel<T>`:
- Birth, Death, Attack, Mutation, ConfigChanged

### Snapshots
Export full or sampled agent arrays to CSV/binary every N ticks for Python analysis.

### Profiling
- Use Rider run configurations for CPU Profile (dotTrace) and Memory Profile (dotMemory)
- Cache profiles as artifacts

## Development Workflow

### Typical Development Cycle
1. Make changes to Core/Render/Tests
2. Run tests to verify correctness: `dotnet test`
3. Run benchmarks to check performance: `dotnet run --project SwarmSim.Benchmarks -c Release`
4. Profile if needed using Rider's CPU/Memory profilers
5. Commit with clear messages following repository style

### Branch Strategy
- `main` - Stable branch
- Short-lived feature branches
- PRs with performance checks before merge

## Project Organization

### Directory.Build.props
Shared MSBuild properties are defined in `Directory.Build.props` at the solution root. This ensures consistency across all projects:
- Target framework and language version
- Nullable and implicit usings settings
- Warning treatment and deterministic builds
- Debug symbol configuration
- Global usings (System, System.Collections.Generic, System.Linq)

When adding new projects, they automatically inherit these settings. Only override in individual .csproj files when necessary.

## Important Notes

- The project is in **early development**; core implementation has not yet begun
- Implementation follows the detailed master plan in `filesAndResources/swarming_lil_men_master_plan_v_1.md`
- Focus is on performance-first design: measure, profile, optimize in that order
- Keep systems small, testable, and composable
- All core simulation code must be deterministic and allocation-free
- The solution is properly structured with project references; SwarmSim.Core has no dependencies, while Render/Tests/Benchmarks all reference Core
