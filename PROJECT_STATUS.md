# SwarmingLilMen - Project Status & Implementation Tracker

**Last Updated**: 2025-11-10 (Session 2 - Phase 1 COMPLETE ✅)
**Current Phase**: P1 - Spatial Grid & Basic Movement - 100% COMPLETE

> **IMPORTANT FOR FUTURE CLAUDE INSTANCES**: This document is your primary memory and source of truth for project status. Read this FIRST before making any changes. Update it LAST after completing work. This ensures continuity across conversation restarts.

---

## Quick Context for Future Sessions

### What This Project Is
A high-performance 2D swarm simulation in C#/.NET 8.0 targeting 50k-100k agents @ 60 FPS with emergent behavior from simple rules. Data-oriented design (SoA), deterministic, zero-allocation hot paths.

### What's Been Done

**Session 1 - Phase 0 (Foundation)** ✅
- Solution structure, build system, core data structures
- World class with SoA layout, Rng, MathUtils
- 21 tests, Raylib rendering, full documentation
- **Phase 0 Complete**

**Session 2 - Phase 1 (Spatial Grid & Basic Movement)** ✅
- ✅ **Spatial Grid**: UniformGrid with Head[]/Next[] linked lists, O(1) insertion, 3x3 queries
- ✅ **Systems**: ISimSystem interface, IntegrateSystem, RandomWalkSystem
- ✅ **World Integration**: Systems pipeline, grid rebuild each tick
- ✅ **Tests**: 14 new UniformGrid tests + 4 performance tests (39 total, all passing)
- ✅ **Benchmarks**: WorldTickBenchmarks and GridBenchmarks (BenchmarkDotNet)
- ✅ **Performance Results** (Release mode):
  - **50k agents: 1.92ms/tick (521 FPS)** - 8.7x better than 60 FPS target! 🚀
  - 10k agents: 0.38ms/tick (2,612 FPS)
  - 1k agents: 0.069ms/tick (14,522 FPS)
  - Grid rebuild (50k): 0.093ms
- ✅ **PHASE 1 COMPLETE** - Ready for Phase 2 (Boids)

### What to Work On Next
**Phase 2**: Boids & Flocking
- Implement SenseSystem for neighbor queries
- Create BehaviorSystem with separation, alignment, cohesion
- Replace RandomWalkSystem with proper flocking behavior
- Target: Visible emergent flocking with 50k agents @ 60 FPS

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

### Phase 0: Foundation (P0) - ✅ COMPLETE
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

- [x] **Minimal Render** (SwarmSim.Render/) ✅ COMPLETE
  - [x] Program.cs - Full Raylib implementation with window, world, render loop
  - [x] Draw agents as colored circles (16-color palette for groups)
  - [x] Interactive controls (mouse spawn, keyboard commands)
  - [x] HUD with FPS, stats, controls
  - [x] Starts with 1000 agents in 4 groups

- [x] **Documentation**
  - [x] README.md - Project overview
  - [x] CONTRIBUTING.md - Developer guide with IDE setup
  - [x] QUICKSTART.md - 5-minute guide

**Exit Criteria**: ✅ Can create World with 1000 agents, render them as dots, window runs at 60 FPS

**Status**: ✅ **PHASE 0 COMPLETE** - All features implemented, tested, and documented.

---

### Phase 1: Spatial Grid & Basic Movement (P1) - ✅ COMPLETE
**Goal**: Agents move randomly, spatial grid works, 50k agents @ 60 FPS

- [x] **Spatial Grid** (SwarmSim.Core/Spatial/)
  - [x] `UniformGrid.cs` - Head[]/Next[] arrays, Rebuild(), Query3x3()
  - [x] Grid tests - compare with brute force for correctness (14 tests)

- [x] **Basic Systems** (SwarmSim.Core/Systems/)
  - [x] `ISimSystem.cs` - Interface: `void Run(World world, float dt)`
  - [x] `IntegrateSystem.cs` - Apply velocity, update position, wrap/reflect bounds
  - [x] `RandomWalkSystem.cs` - Add random forces (temporary, for testing)

- [x] **World Integration**
  - [x] Updated World.cs to use systems pipeline
  - [x] Grid rebuild called each tick
  - [x] ClearForces() at tick start
  - [x] All systems run in sequence

- [x] **Performance**
  - [x] Performance tests with 1k, 10k, 50k agents
  - [x] Benchmark suite with WorldTickBenchmarks and GridBenchmarks

**Exit Criteria**: ✅ **ALL MET** - 50k agents @ 521 FPS (8.7x target!), grid working perfectly, 39 tests passing

**Status**: ✅ **PHASE 1 COMPLETE** - Performance far exceeds goals. Ready for Phase 2.

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

### 2025-11-10 (Session 2 - Phase 1 COMPLETE ✅)
- **Implemented Phase 1: Spatial Grid & Basic Movement**:
  - Created ISimSystem interface for all simulation systems
  - Implemented UniformGrid with Head[]/Next[] linked list structure
  - Created IntegrateSystem (velocity → position, boundary conditions)
  - Created RandomWalkSystem (temporary, for testing movement)
  - Updated World.cs to use systems pipeline
- **Tests & Benchmarks**:
  - 14 new UniformGrid tests (brute force comparison, boundary cases, stats)
  - 4 performance tests (1k, 10k, 50k agents)
  - WorldTickBenchmarks and GridBenchmarks (BenchmarkDotNet)
  - **39 tests total, all passing** (21 from P0 + 18 new)
- **Performance Results** (Release mode, exceeds all targets):
  - 50k agents: 1.92ms/tick (**521 FPS**) - 8.7x better than 60 FPS goal!
  - 10k agents: 0.38ms/tick (2,612 FPS)
  - 1k agents: 0.069ms/tick (14,522 FPS)
  - Grid rebuild: 0.093ms (50k agents)
- Solution builds with 0 warnings, 0 errors
- **✅ PHASE 1 COMPLETE** - Ready for Phase 2 (Boids)

### 2025-11-10 (Session 1 - Phase 0 COMPLETE ✅)
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
- **Implemented Raylib rendering**:
  - Full visualization with 1920x1080 window
  - 16-color palette for agent groups
  - Interactive controls (mouse spawn, keyboard, reset)
  - Live stats HUD (FPS, agent count, energy, speed)
  - Starts with 1000 agents in 4 colored groups
- **Created comprehensive documentation**:
  - README.md - Project overview, quick start, features, usage examples
  - CONTRIBUTING.md - Developer setup, IDE configuration, workflow, performance guidelines
  - QUICKSTART.md - 5-minute getting started guide
  - PublishScript.md analysis and explanation
- Solution builds with 0 warnings, 0 errors
- **✅ PHASE 0 COMPLETE** - Ready to move to Phase 1

---

## Performance Baselines

| Phase | Agents | Tick Time | FPS | Allocs/Tick | Notes |
|-------|--------|-----------|-----|-------------|-------|
| P0    | 1k     | 0.069ms   | 14,522 | N/A | Baseline (no systems) |
| **P1**| **1k** | **0.069ms** | **14,522** | **TBD** | **Grid + RandomWalk + Integrate** ✅ |
| **P1**| **10k** | **0.38ms** | **2,612** | **TBD** | **Grid + RandomWalk + Integrate** ✅ |
| **P1**| **50k** | **1.92ms** | **521** | **TBD** | **Grid + RandomWalk + Integrate** ✅ |
| P2    | 50k    | TBD       | 60+ (target) | 0 | With full boids |
| P3    | 50k    | TBD       | 60+ (target) | 0 | With combat/metabolism |
| P4    | 50k    | TBD       | 60+ (target) | 0 | With reproduction |
| P5    | 200k+  | TBD       | 60+ (target) | 0 | With SIMD/parallel |

**Notes**:
- P1 results measured in Release mode on development machine
- Grid rebuild time (50k agents): 0.093ms
- Current phase far exceeds performance targets (521 FPS vs 60 FPS goal)

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
