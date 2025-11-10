# SwarmingLilMen - Project Status & Implementation Tracker

**Last Updated**: 2025-11-10 (Session 1 - Foundation Complete)
**Current Phase**: P0 - Skeleton (Foundation) - 80% COMPLETE

> **IMPORTANT FOR FUTURE CLAUDE INSTANCES**: This document is your primary memory and source of truth for project status. Read this FIRST before making any changes. Update it LAST after completing work. This ensures continuity across conversation restarts.

---

## Quick Context for Future Sessions

### What This Project Is
A high-performance 2D swarm simulation in C#/.NET 8.0 targeting 50k-100k agents @ 60 FPS with emergent behavior from simple rules. Data-oriented design (SoA), deterministic, zero-allocation hot paths.

### What's Been Done (Session 1)
- ✅ Solution structure reorganized and cleaned up
- ✅ Project references properly configured (all projects reference SwarmSim.Core)
- ✅ Directory.Build.props created with shared build configuration
- ✅ CLAUDE.md documentation created
- ✅ PROJECT_STATUS.md created as persistent memory
- ✅ **Core data structures implemented**: Genome, AgentState, SimConfig
- ✅ **Utilities implemented**: Rng (deterministic RNG), MathUtils (vector operations)
- ✅ **World class implemented**: Full SoA layout, agent management, tick loop skeleton
- ✅ **Tests created**: 21 tests (RngTests + WorldTests), all passing
- ✅ **Solution builds successfully** with no warnings

### What to Work On Next
- Minimal rendering (Program.cs in SwarmSim.Render)
- Then move to Phase 1 (Spatial Grid & Systems)

### Key Files to Reference
- `filesAndResources/swarming_lil_men_master_plan_v_1.md` - Detailed design document
- `CLAUDE.md` - Development guidelines and commands
- This file (`PROJECT_STATUS.md`) - Current status and next steps

---

## Architecture Quick Reference

### Project Structure
```
SwarmSim.Core/          - Core library (SoA data, systems, deterministic logic)
SwarmSim.Render/        - Raylib-cs visualization (references Core)
SwarmSim.Tests/         - xUnit tests (references Core)
SwarmSim.Benchmarks/    - BenchmarkDotNet suite (references Core)
```

### Systems Pipeline Order
1. SenseSystem → 2. BehaviorSystem → 3. CombatSystem → 4. ForageSystem → 5. ReproductionSystem → 6. MetabolismSystem → 7. IntegrateSystem → 8. LifecycleSystem

### Data Layout (SoA)
Agent arrays: `X[]`, `Y[]`, `Vx[]`, `Vy[]`, `Energy[]`, `Health[]`, `Age[]`, `Group[]`, `State[]`, `Genome[]`

---

## Implementation Priority Queue

### Phase 0: Foundation (P0) - 80% COMPLETE
**Goal**: Basic data structures, World skeleton, can compile and run minimal simulation

- [x] **Core Data Structures** (SwarmSim.Core/)
  - [x] `Genome.cs` - readonly record struct with traits, mutation logic
  - [x] `AgentState.cs` - byte flags enum with extension methods
  - [x] `SimConfig.cs` - Complete configuration with presets (Peaceful, Warbands, Evolution)

- [x] **Utilities** (SwarmSim.Core/Utils/)
  - [x] `Rng.cs` - Full deterministic RNG with Gaussian, unit vectors, shuffle, etc.
  - [x] `MathUtils.cs` - Complete vector math, distance, normalization, wrapping, reflection

- [x] **World Class** (SwarmSim.Core/)
  - [x] `World.cs` - Complete SoA arrays (13 arrays), agent management
  - [x] Add/Remove/Spawn methods with multiple overloads
  - [x] Basic integration loop with boundary handling
  - [x] Compaction for dead agents
  - [x] Stats retrieval and readonly span accessors

- [x] **Basic Tests** (SwarmSim.Tests/)
  - [x] `RngTests.cs` - 8 tests covering determinism, distributions, shuffling
  - [x] `WorldTests.cs` - 13 tests covering agents, boundaries, determinism
  - [x] **All 21 tests passing**

- [ ] **Minimal Render** (SwarmSim.Render/) - REMAINING WORK
  - [ ] Program.cs - Initialize Raylib window, create World, basic render loop
  - [ ] Draw agents as colored pixels/dots

**Exit Criteria**: Can create World with 1000 agents, render them as dots, window runs at 60 FPS

**Status**: Core foundation is solid. Only rendering implementation remains for P0 completion.

---

### Phase 1: Spatial Grid & Basic Movement (P1)
**Goal**: Agents move randomly, spatial grid works, 50k agents @ 60 FPS

- [ ] **Spatial Grid** (SwarmSim.Core/Spatial/)
  - [ ] `UniformGrid.cs` - Head[]/Next[] arrays, Rebuild(), Query3x3()
  - [ ] Grid tests - compare with brute force for correctness

- [ ] **Basic Systems** (SwarmSim.Core/Systems/)
  - [ ] `ISimSystem.cs` - Interface: `void Run(World world, float dt)`
  - [ ] `IntegrateSystem.cs` - Apply velocity, update position, wrap/reflect bounds
  - [ ] `RandomWalkSystem.cs` - Add random forces (temporary, for testing)

- [ ] **Performance**
  - [ ] Zero allocations in Tick() verified with dotMemory
  - [ ] Benchmark: full Tick with 50k agents

**Exit Criteria**: 50k agents moving randomly, spatial grid queries working, zero allocs, 60+ FPS

---

### Phase 2: Boids & Flocking (P2)
**Goal**: Implement separation, alignment, cohesion - see emergent flocking

- [ ] **Boids Systems** (SwarmSim.Core/Systems/)
  - [ ] `SenseSystem.cs` - Query neighbors via grid, compute local aggregates
  - [ ] `BehaviorSystem.cs` - Boids rules → forces into Fx[]/Fy[]
  - [ ] Remove RandomWalkSystem, use BehaviorSystem

- [ ] **Tests**
  - [ ] Small scenarios (5-10 agents) verify boids math
  - [ ] Property test: agents stay within max speed

- [ ] **Tuning**
  - [ ] SimConfig for boids weights and radii
  - [ ] HUD overlay showing parameters

**Exit Criteria**: Visible flocking behavior, agents group together, parameters tunable

---

### Phase 3: Groups, Combat, Energy (P3)
**Goal**: Multiple groups, aggression matrix, combat interactions, metabolism

- [ ] **Combat & Metabolism** (SwarmSim.Core/Systems/)
  - [ ] `CombatSystem.cs` - Resolve attacks within radius
  - [ ] `MetabolismSystem.cs` - Energy drain, age increment, mark deaths
  - [ ] `LifecycleSystem.cs` - Compact dead slots, free-list management

- [ ] **Group Aggression**
  - [ ] Aggression matrix in SimConfig
  - [ ] Modify BehaviorSystem to use aggression values

- [ ] **Tests**
  - [ ] Combat scenarios: verify energy transfer
  - [ ] Death and cleanup tests

**Exit Criteria**: Two groups fight/flee based on aggression matrix, deaths occur, population stable

---

### Phase 4: Reproduction & Evolution (P4)
**Goal**: Agents reproduce, genomes mutate, observe trait drift

- [ ] **Reproduction** (SwarmSim.Core/Systems/)
  - [ ] `ReproductionSystem.cs` - Energy threshold → spawn child with mutated genome
  - [ ] Genome mutation logic with clamped normal noise

- [ ] **Foraging**
  - [ ] `ForageSystem.cs` - Simple uniform food field or point sources
  - [ ] Add energy to agents

- [ ] **Metrics & Events**
  - [ ] Event DTOs (Birth, Death, Mutation)
  - [ ] Metrics: births/deaths per tick, avg traits

**Exit Criteria**: Population self-sustaining, trait histograms change over time, 2+ evolutionary regimes

---

### Phase 5: Performance & Parallel (P5)
**Goal**: Optimize for 200k+ interactive or 1M+ headless

- [ ] **SIMD**
  - [ ] Replace scalar math with System.Numerics.Vector2
  - [ ] Benchmark improvements

- [ ] **Parallelization**
  - [ ] Row/tile partitioning with Parallel.For
  - [ ] Private worker accumulators
  - [ ] Measure speedup vs single-threaded

- [ ] **NativeAOT**
  - [ ] Publish profiles (see PublishScript.md)
  - [ ] Test startup time and memory

**Exit Criteria**: 200k+ agents interactive OR 1M+ headless, documented perf baselines

---

### Phase 6: Polish & Tooling (P6)
**Goal**: Presets, replay, snapshots, better UX

- [ ] **Presets**
  - [ ] Peaceful Flocks, Warbands, Rapid Evolution configs
  - [ ] Load from JSON files

- [ ] **Observability**
  - [ ] Snapshot export (CSV/binary) every N ticks
  - [ ] HUD with detailed metrics

- [ ] **Replay**
  - [ ] Record inputs for deterministic replay
  - [ ] Replay viewer

**Exit Criteria**: Presets work, can export data for Python analysis, replay system functional

---

## Implementation Guidelines for Future Claude

### Before Starting Work
1. **Read this file completely** - Understand current phase and status
2. **Check CLAUDE.md** - Refresh on build commands and architecture
3. **Review master plan** - Reference filesAndResources/swarming_lil_men_master_plan_v_1.md for details
4. **Use TodoWrite** - Create task list for your session
5. **Build and test** - Ensure solution builds before making changes

### While Working
1. **Follow SoA principles** - No allocations in hot paths, use arrays not lists
2. **Determinism first** - Stable iteration order, seeded RNG, no time-based randomness
3. **Test as you go** - Write tests for data structures and systems
4. **Document invariants** - Comment at top of each system class
5. **Profile regularly** - Check allocations with dotMemory, CPU with dotTrace
6. **Update todos** - Mark tasks complete as you finish them

### After Completing Work
1. **Verify build** - `dotnet build` and `dotnet test` must pass
2. **Update this file** - Check off completed items, note any blockers
3. **Update phase status** - If phase complete, update "Current Phase" at top
4. **Commit frequently** - Small, atomic commits with clear messages
5. **Note for next session** - Add any "TODO" or "FIXME" comments for future work

### Performance Rules (CRITICAL)
- **Zero allocations** in Tick() - Use stackalloc or ArrayPool for temp buffers
- **No LINQ** in hot paths - Use for loops
- **No boxing** - Avoid object, dynamic, delegates in systems
- **No exceptions** for control flow - Use return codes
- **Hoist invariants** - Move calculations outside loops when possible
- **Prefer structs** - Especially for small DTOs like Genome
- **readonly ref** - Pass large structs by ref when reading

### Common Pitfalls to Avoid
- ❌ Don't use List<T> for agent data - Use arrays
- ❌ Don't allocate in Tick() - Pre-allocate everything
- ❌ Don't use virtual/interface calls in inner loops - Direct/static only
- ❌ Don't iterate agents with foreach - Use for loop with index
- ❌ Don't modify collection while iterating - Use two-pass (mark, then act)
- ❌ Don't forget to update Count when adding/removing agents
- ❌ Don't use Time.Now or Random() - Use injected Rng with seed

---

## Current Blockers & Questions

None currently.

---

## Recent Changes Log

### 2025-11-10 (Session 1 - Foundation)
- Created PROJECT_STATUS.md as persistent memory document
- Solution reorganized: removed root project, added Directory.Build.props
- **Implemented complete P0 foundation**:
  - Genome.cs with Random() and Mutate() methods
  - AgentState.cs enum with HasFlag/SetFlag/ClearFlag extensions
  - SimConfig.cs with validation and 3 presets
  - Rng.cs with Gaussian, unit vectors, circle sampling, shuffle
  - MathUtils.cs with full 2D vector operations
  - World.cs with 13 SoA arrays, agent lifecycle, boundary modes
- **Created 21 passing tests** verifying determinism and core functionality
- Solution builds with 0 warnings, 0 errors
- **P0 is 80% complete** - only rendering remains

---

## Performance Baselines (To Be Established)

| Phase | Agents | FPS | Allocs/Tick | Notes |
|-------|--------|-----|-------------|-------|
| P0    | 1k     | TBD | TBD         | Baseline with minimal systems |
| P1    | 50k    | 60+ | 0           | With grid and integration |
| P2    | 50k    | 60+ | 0           | With full boids |
| P3    | 50k    | 60+ | 0           | With combat/metabolism |
| P4    | 50k    | 60+ | 0           | With reproduction |
| P5    | 200k+  | 60+ | 0           | With SIMD/parallel |

---

## External Resources

### Documentation
- Master Plan: `filesAndResources/swarming_lil_men_master_plan_v_1.md`
- Publish Scripts: `filesAndResources/PublishScript.md`
- Claude Guidelines: `CLAUDE.md`

### Dependencies
- Raylib-cs 7.0.2 (SwarmSim.Render)
- BenchmarkDotNet 0.15.6 (SwarmSim.Benchmarks)
- xUnit 2.9.3 (SwarmSim.Tests)

### Useful Commands
```bash
# Build
dotnet build

# Test
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj

# Run renderer
dotnet run --project SwarmSim.Render/SwarmSim.Render.csproj

# Benchmark (always Release)
dotnet run --project SwarmSim.Benchmarks/SwarmSim.Benchmarks.csproj -c Release

# Profile in Rider
# Use CPU Profile / Memory Profile run configurations
```

---

## Notes for Next Session

**Where We Left Off**: About to start P0 implementation - basic data structures first.

**Next Steps**:
1. Create Core/Genome.cs
2. Create Core/Utils/Rng.cs
3. Create Core/World.cs skeleton
4. Write basic tests

**Remember**: Focus on getting the foundation right. Don't rush to features. The architecture is more important than speed at this stage.
