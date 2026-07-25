# Phase 3 Readiness Checklist

> [`PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and performance state; this checklist defines remaining canonical readiness work.

**Last Updated**: 2026-07-25 (toroidal spatial-index contract and equivalence proof)
**Current Status**: Core scaffolding, steering rules, and the perception contract exist;
composition and instrumentation UX are partial, and milestones 8-10 remain incomplete

> This document outlines the concrete steps needed to complete the canonical boids implementation before starting Phase 3 (Combat & Metabolism).

---

## Overview

Before proceeding to Phase 3, we must complete the canonical boids implementation to provide a
solid, debuggable foundation. The perception contract now enforces its radius/self/wrap
requirements; milestone 6 still violates its total `MaxForce` bound, and milestone 7 does not meet
its full UX/test acceptance. Milestones 8-10, multi-group semantics, and canonical performance
evidence remain incomplete.

**Estimated Effort**: 1-2 weeks of focused development

---

## Critical Path: Resolve Correctness Gaps and Complete Readiness

These are the remaining milestones from `archive/NewImplementation.md` that must be completed:

### ✅ Core Scaffolding and Single-Boid Path (Milestones 0-1)
- [x] Core scaffolding (Vec2, Boid, World, Rng, interfaces)
- [x] Single boid motion with constant speed
- [ ] Long-horizon seeded and golden acceptance, tracked in #17 and #21

### ✅ Milestone 2: Perception (COMPLETE)
- [x] Field-of-view filtering with weights
- [x] Canonical Grid and Naive indexes enforce inclusive radius filtering, self-exclusion, and
  toroidal minimum-image distance with deterministic capped-result status; focused equivalence tests
  cover seams, corners, coincident agents, one/two-cell worlds, dense truncation, and 200 randomized scenarios.
- [x] FOV, whiskers, neighbor statistics, separation, and cohesion use the same toroidal
  minimum-image displacement; the full Release suite passes.

### ✅ Steering Rule Implementations (Milestones 3-5)
- [x] Separation rule (1/d weighting, linear falloff)
- [x] Alignment rule (match neighbor headings)
- [x] Cohesion rule (move toward center of mass)
- [ ] Add the prescribed head-on/overtaking, heading-convergence, no-neighbor, and
  single-neighbor/cluster scenarios after the milestone-2 contract is fixed; tracked in
  [issue #41](https://github.com/Chris0Jeky/SwarmingLilMen/issues/41)

### 🔄 Milestone 6: Rule Composition & Stability (PARTIAL)
- [x] Compose separation, alignment, cohesion, whisker, and wander contributions
- [ ] Enforce the total steering bound of `MaxForce`; whisker plus separation can currently
  overspend it, tracked in [issue #19](https://github.com/Chris0Jeky/SwarmingLilMen/issues/19)
- [ ] Verify named-rule independence, classic-mode constant speed, and combined no-NaN/no-stuck
  stability after #27; tracked in #41

### 🔄 Milestone 7: Instrumentation & UX (PARTIAL)
- [x] Instrumentation (neighbor counts, weights, rule contributions)
- [x] Basic selected-boid inspection overlay (perception bounds, neighbor links, whisker markers,
  aggregate metrics)
- [ ] Complete FOV arc visualization
- [ ] Rule-colored neighbor/contribution links
- [ ] Actual steering-vector arrow
- [ ] Individual rule enable/disable controls and FOV adjustment
- [ ] Acceptance tests proving rule toggles change the recorded contributions

The missing milestone-7 slice is tracked in
[issue #40](https://github.com/Chris0Jeky/SwarmingLilMen/issues/40) and is intentionally outside
epic #10's ordered Wave 0/Wave 1 execution.

### 🔄 Milestone 8: Boundaries & Worlds (IN PROGRESS)
**Goal**: Predictable edges so tests don't flake

- [ ] **Toroidal Wrapping Tests**
  ```csharp
  [Fact]
  public void CanonicalWorld_ToroidalWrapping_AgentsWrapCorrectly()
  {
      // Agent at (WorldWidth - 1, 0) moving right
      // After step, should be at (0, 0) or nearby
  }
  ```
  - Test X wrapping (left/right edges)
  - Test Y wrapping (top/bottom edges)
  - Test corner wrapping (diagonal)
  - Verify velocity is preserved through wrap

- [ ] **Wall Reflection (Optional)**
  - Add `BoundaryMode.Reflect` option to settings
  - Implement reflection steering behavior
  - Test agents bounce off walls correctly
  - Verify no agents "leak" through boundaries

**Exit Criteria**: Position wrapping verified in all tests; optional wall reflection working

---

### 🔄 Milestone 9: Spatial Index Equivalence (IN PROGRESS)
**Goal**: Same behavior, faster performance

- [x] **Deterministic Grid vs Naive equivalence**: 200 randomized scenarios plus direct radius,
  self, coincident, seam, corner, dense-truncation, and one/two-cell cases pass. A no-wander,
  200-tick 12-agent canonical trajectory matched positions and velocities within `1e-4`.
- [x] **Edge cases and bounded output**: inclusive radius boundaries, zero-radius coincident agents,
  all agents in one cell, wrapped one/two-cell worlds, randomized distributions, and deterministic
  truncation are covered.
- [x] **Steady-state allocation proof**: both direct query implementations report zero
  current-thread allocations after tiered-compilation warm-up.
- [ ] **Canonical performance comparison**: no grid-over-naive speedup is claimed. Canonical
  BenchmarkDotNet and `MemoryDiagnoser` coverage remain tracked in
  [issue #20](https://github.com/Chris0Jeky/SwarmingLilMen/issues/20).

**Exit Criteria**: Behavioral equivalence is verified; performance improvement remains unverified.

---

### 🔄 Milestone 10: Property Tests & Metrics (IN PROGRESS)
**Goal**: Confidence at scale with quantitative behavioral validation

- [ ] **Implement Flock Metrics**
  ```csharp
  public class FlockMetrics
  {
      public float Polarization { get; set; }  // |mean normalized heading|
      public float AvgNearestNeighborDist { get; set; }
      public float ClusteringCoefficient { get; set; }
      public float SpeedVariance { get; set; }
  }

  public static FlockMetrics ComputeMetrics(ReadOnlySpan<Boid> boids) { ... }
  ```

- [ ] **Property Test: Separation Increases Distance**
  ```csharp
  [Fact]
  public void PropertyTest_SeparationOn_DistanceIncreases()
  {
      // Dense initial placement
      // Only separation rule enabled
      // Run for T ticks
      // Assert: median pairwise distance increases over first T ticks
  }
  ```

- [ ] **Property Test: Alignment Converges Headings**
  ```csharp
  [Fact]
  public void PropertyTest_AlignmentOn_HeadingVarianceDecreases()
  {
      // Random initial headings
      // Only alignment rule enabled
      // Run for T ticks
      // Assert: heading variance (std dev of angles) decreases
  }
  ```

- [ ] **Property Test: Cohesion Reduces Spread**
  ```csharp
  [Fact]
  public void PropertyTest_CohesionOn_FlockRadiusDecreases()
  {
      // Scattered initial placement
      // Only cohesion rule enabled
      // Run for T ticks
      // Assert: flock radius (max distance from centroid) decreases
  }
  ```

- [ ] **Property Tests: Speed Semantics** ([issue #41](https://github.com/Chris0Jeky/SwarmingLilMen/issues/41))
  ```csharp
  [Fact]
  public void PropertyTest_ClassicComposition_SpeedStaysAtTarget()
  {
      // Named classic-Reynolds composition from #27; priority/droop disabled
      // Run for 100 ticks
      // Assert: all agents have |velocity| = targetSpeed (± epsilon)
  }

  [Fact]
  public void PropertyTest_DefaultPriority_SpeedMatchesAllowedSpeed()
  {
      // Default composition with priority activation and configured SeparationSpeedDroop
      // Assert: speed matches the blend-adjusted allowed speed, remains finite, and never stalls
  }
  ```

- [ ] **Add Metrics to Instrumentation**
  - Extend `RuleInstrumentation` to track flock-level metrics
  - Compute metrics every N ticks (configurable)
  - Export to CSV for analysis

**Exit Criteria**: All property tests pass; metrics computed and validated against known good parameter sets

---

## Secondary: Multi-Group Support

The canonical implementation currently supports only single-group flocking. Phase 3 requires multiple groups with distinct behaviors.

### 🆕 Add Group Configuration

- [ ] **Extend Settings**
  ```csharp
  public class CanonicalWorldSettings
  {
      // ...existing fields...

      public Dictionary<byte, GroupConfig> GroupConfigs { get; init; } = new();
  }

  public record GroupConfig(
      float SeparationWeight,
      float AlignmentWeight,
      float CohesionWeight,
      bool AlignWithinGroupOnly  // true = only align with same group
  );
  ```

- [ ] **Update Rules for Group Awareness**
  ```csharp
  // In AlignmentRule and CohesionRule:
  if (context.AlignWithinGroupOnly)
  {
      if (boids[neighborIndex].Group != self.Group)
          continue;  // Skip different-group neighbor
  }
  ```

- [ ] **Separation Always Cross-Group**
  - Separation should apply to ALL neighbors (collision avoidance)
  - Alignment/cohesion can be within-group only (flocking behavior)

- [ ] **Test Multi-Group Scenarios**
  ```csharp
  [Fact]
  public void CanonicalWorld_MultiGroup_MaintainsSeparation()
  {
      // Two groups, close initial placement
      // AlignWithinGroupOnly = true
      // Run simulation
      // Assert: groups maintain separation, each internally cohesive
  }
  ```

**Exit Criteria**: Two groups can coexist with independent flocking behaviors; tests validate group separation

---

## Tertiary: Visualization & Debugging Tools

To fully leverage the canonical implementation's debuggability, we need visualization tools.

### 🎨 Renderer Enhancements

- [ ] **Perception Visualization** (Toggle with `V` key)
  - Draw sense radius circle around selected agent
  - Draw FOV cone (wedge shape)
  - Highlight neighbors within FOV

- [ ] **Neighbor Link Rendering** (Toggle with `N` key)
  - Draw lines from agent to neighbors
  - Color by rule contribution:
    - Red = separation
    - Blue = alignment
    - Green = cohesion
  - Line thickness = contribution magnitude

- [ ] **Steering Vector Display** (Toggle with `S` key)
  - Draw arrow from agent showing total steering direction
  - Length = steering magnitude
  - Color = clamped vs unclamped

- [ ] **Inspector Panel** (Click agent to select, `I` to toggle panel)
  - Show agent details:
    - Position, velocity, group
    - Neighbor count, total weight
    - Per-rule contributions (separation, alignment, cohesion)
  - List neighbor indices

- [ ] **Metrics Overlay** (Toggle with `M` key)
  - Show flock-level metrics:
    - Polarization
    - Avg nearest-neighbor distance
    - Speed variance
  - Update every frame or every N ticks

**Exit Criteria**: Can visually inspect any agent's decision-making process; metrics visible in real-time

---

## Quaternary: Performance Validation

Ensure the canonical implementation meets or exceeds the legacy performance.

### ⚡ Benchmark Suite

- [ ] **Port WorldTickBenchmarks**
  ```csharp
  [Benchmark]
  public void CanonicalWorld_Step_1k_Agents()
  {
      _world.Step(_dt);
  }

  [Benchmark]
  public void CanonicalWorld_Step_10k_Agents() { ... }

  [Benchmark]
  public void CanonicalWorld_Step_50k_Agents() { ... }
  ```

- [ ] **Compare Old vs New**
  - Run both implementations in the same BenchmarkDotNet job with matched agent initialization,
    settings, workload, warmup, runtime, and diagnostics; only that paired run may report a speedup.
  - Historical legacy-only timing-test context captured 2026-07-25 (not a canonical comparison
    seed):

    ```powershell
    dotnet build SwarmingLilMen.sln --configuration Release
    dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj --configuration Release --no-build --filter "Category=Performance" --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true
    ```

    | Agents | Legacy sample (ms/tick) | Timing-test configuration |
    |--------|-------------------------|---------------------------|
    | 1k     | 0.172                   | `SimConfig` defaults |
    | 10k    | 8.839                   | `SimConfig` defaults |
    | 50k    | 162.815                 | Heavier legacy BenchmarkDotNet-like steering weights |

    These stopwatch samples come from separate test cases and are not valid inputs to a future
    legacy/canonical speedup calculation (`SwarmSim.Tests/PerformanceTests.cs:15-99`).

- [ ] **Identify Regressions**
  - If canonical is slower, profile with dotTrace
  - Document top-3 hotspots
  - Create performance improvement tasks for Phase 5

### 🔍 Allocation Testing

- [ ] **Zero-Allocation Verification**
  - Current canonical `Step()` does not satisfy this target because perception-snapshot refresh
    allocates three arrays (`SwarmSim.Core/Canonical/CanonicalWorld.cs:496-508`).
  ```csharp
  [Fact]
  public void CanonicalWorld_Step_AllocatesNothing()
  {
      // Warmup
      for (int i = 0; i < 10; i++)
          _world.Step(_dt);

      // Measure
      long before = GC.GetAllocatedBytesForCurrentThread();
      for (int i = 0; i < 100; i++)
          _world.Step(_dt);
      long after = GC.GetAllocatedBytesForCurrentThread();

      // Assert
      Assert.Equal(0, after - before);
  }
  ```

- [ ] **Profile with dotMemory**
  - Run 50k agent scenario for 10 seconds
  - Take before/after snapshots
  - Verify no allocations in `Step()` method
  - Document any allocation sources

**Target**: 10k agents at 60+ rendered FPS. Renderer FPS and canonical tick throughput are
currently unmeasured.

**Exit Criteria**: Performance validated, allocation-free hot path confirmed

---

## Final: Migration Decision

Once all above tasks are complete, decide on migration strategy:

### Option A: Full Replacement
- Delete `SwarmSim.Core/Systems/` and legacy `World.cs`
- Rename `Canonical/` to root namespace
- Update all references
- **Pros**: Clean codebase, no confusion
- **Cons**: Irreversible

### Option B: Keep Both
- Maintain both implementations
- Use `--legacy` vs `--canonical` flags
- **Pros**: Can compare behaviors
- **Cons**: Double maintenance

### Option C: Gradual Migration (RECOMMENDED)
- Port Phase 3 features (combat, metabolism) to canonical first
- Keep legacy for reference/comparison
- Remove legacy only after full validation in Phase 5
- **Pros**: Safest, allows side-by-side comparison
- **Cons**: Longest timeline

**Recommendation**: **Option C** - Build Phase 3 on canonical, validate thoroughly, then remove legacy in Phase 5.

---

## Timeline Estimate

| Task | Estimated Time | Priority |
|------|---------------|----------|
| Milestone 8 (Boundaries) | 1 day | HIGH |
| Milestone 9 (Spatial Index) | 2 days | HIGH |
| Milestone 10 (Property Tests) | 3 days | HIGH |
| Multi-Group Support | 2 days | MEDIUM |
| Visualization Tools | 3-4 days | MEDIUM |
| Performance Validation | 2 days | HIGH |
| **Total** | **13-14 days** | |

> **Planning boundary:** The table above is a historical estimate for its listed tasks. Its total
> does not estimate issue-level scope, gates, reviews, or merge time for epic #10's full ordered
> #11-#21 run or dependency-gated #40/#41, so it is not a current end-to-end forecast.

**Canonical-readiness subset after Wave 0**: epic #10's ordered #17-#21 gates. The full overnight
queue still begins with Wave 0 issues #11-#16. Multi-group and dependency-gated
acceptance/instrumentation backlog (#40 and #41) remains outside the current overnight scope.

---

## Success Criteria

Phase 3 readiness achieved when:

1. [ ] All milestones 0-10 from `archive/NewImplementation.md` complete
2. [ ] Multi-group support implemented and tested
3. [ ] Performance validated (10k agents @ 60+ FPS)
4. [ ] Zero allocations in hot path confirmed
5. [ ] Visualization tools built for debugging
6. [x] Comprehensive test suite (50+ tests passing)

Only the test-count criterion is currently satisfied; the unchecked criteria remain required and
the verified state above governs their evidence.

At that point, we have a **solid, debuggable, performant foundation** for Phase 3 combat and metabolism systems.

---

## References

- `archive/IMPLEMENTATION_EVOLUTION.md` - Why we pivoted and technical details
- `archive/NewImplementation.md` - Original TDD roadmap with milestones
- `../PROJECT_STATUS.md` - Current implementation status
- `../CLAUDE.md` - Development guidelines

---

## Next Session Tasks

When starting work on Phase 3 readiness:

1. ✅ Read `archive/IMPLEMENTATION_EVOLUTION.md` to understand context
2. ✅ Read this document to understand what needs doing
3. Resume the first unmerged issue in epic #10's ordered #11-#21 queue
4. Update `PROJECT_STATUS.md` as you complete tasks
5. Run tests frequently to ensure nothing breaks
6. Profile performance after each milestone

Do not select work by apparent speed; the epic's dependency order is authoritative.
The #11-#16 Wave 0 gates precede this checklist's #17-#21 canonical behavior/fixture subset.
