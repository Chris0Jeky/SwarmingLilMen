# Phase 3 Readiness Checklist

**Last Updated**: 2025-11-12
**Current Status**: Canonical implementation ~70% complete

> This document outlines the concrete steps needed to complete the canonical boids implementation before starting Phase 3 (Combat & Metabolism).

---

## Overview

Before proceeding to Phase 3, we must complete the canonical boids implementation to provide a solid, debuggable foundation. The current implementation is ~70% complete (milestones 0-7 done, 8-10 in progress).

**Estimated Effort**: 1-2 weeks of focused development

---

## Critical Path: Complete Milestones 8-10

These are the remaining milestones from `NewImplementation.md` that must be completed:

### ✅ Milestone 0-7: COMPLETE
- [x] Core scaffolding (Vec2, Boid, World, Rng, interfaces)
- [x] Single boid motion with constant speed
- [x] Perception (radius + FOV filtering with weights)
- [x] Separation rule (1/d weighting, linear falloff)
- [x] Alignment rule (match neighbor headings)
- [x] Cohesion rule (move toward center of mass)
- [x] Rule composition with force clamping
- [x] Instrumentation (neighbor counts, weights, rule contributions)

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

- [ ] **Property Test: Grid vs Naive**
  ```csharp
  [Theory]
  [InlineData(10, 42)]
  [InlineData(100, 123)]
  [InlineData(1000, 456)]
  public void SpatialIndex_GridMatchesNaive_RandomScenarios(int agentCount, int seed)
  {
      // Generate random agent positions
      // Query neighbors with both NaiveSpatialIndex and GridSpatialIndex
      // Assert neighbor sets are identical (within FP tolerance)
  }
  ```
  - Test at 10, 100, 1k agents
  - Test various density configurations (sparse, dense, clustered)
  - Verify neighbor sets match exactly (order doesn't matter)
  - Use multiple random seeds

- [ ] **Performance Comparison**
  ```csharp
  [Fact]
  public void SpatialIndex_GridIsFasterThanNaive()
  {
      // Benchmark both implementations at 1k, 10k agents
      // Expect O(n) vs O(n²) behavior
      // Grid should be 10x+ faster at 10k agents
  }
  ```

- [ ] **Edge Case Tests**
  - All agents in one grid cell
  - Agents uniformly distributed across grid
  - Agents at grid cell boundaries

**Exit Criteria**: Grid and Naive produce identical results for all test cases; performance improvement validated

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

- [ ] **Property Test: Speed Limits**
  ```csharp
  [Theory]
  [InlineData(1.0f)]
  [InlineData(5.0f)]
  [InlineData(10.0f)]
  public void PropertyTest_SpeedAlwaysAtTarget(float targetSpeed)
  {
      // Random scenario, all rules enabled
      // Run for 100 ticks
      // Assert: all agents have |velocity| = targetSpeed (± epsilon)
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

**Exit Criteria**: Two groups can coexist with independent flocking behaviors; tests validate group isolation

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
  - Run benchmarks for both implementations
  - Create comparison table:
    | Agents | Legacy (ms/tick) | Canonical (ms/tick) | Speedup |
    |--------|------------------|---------------------|---------|
    | 1k     | 0.117            | TBD                 | TBD     |
    | 10k    | 7.75             | TBD                 | TBD     |
    | 50k    | 154              | TBD                 | TBD     |

- [ ] **Identify Regressions**
  - If canonical is slower, profile with dotTrace
  - Document top-3 hotspots
  - Create performance improvement tasks for Phase 5

### 🔍 Allocation Testing

- [ ] **Zero-Allocation Verification**
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

**Target**: 10k agents @ 60+ FPS (match or beat legacy 129 FPS)

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

**Critical Path**: Milestones 8-10 + Multi-Group + Performance (8 days minimum)

---

## Success Criteria

Phase 3 readiness achieved when:

1. ✅ All milestones 0-10 from `NewImplementation.md` complete
2. ✅ Multi-group support implemented and tested
3. ✅ Performance validated (10k agents @ 60+ FPS)
4. ✅ Zero allocations in hot path confirmed
5. ✅ Visualization tools built for debugging
6. ✅ Comprehensive test suite (50+ tests passing)

At that point, we have a **solid, debuggable, performant foundation** for Phase 3 combat and metabolism systems.

---

## References

- `IMPLEMENTATION_EVOLUTION.md` - Why we pivoted and technical details
- `NewImplementation.md` - Original TDD roadmap with milestones
- `PROJECT_STATUS.md` - Current implementation status
- `CLAUDE.md` - Development guidelines

---

## Next Session Tasks

When starting work on Phase 3 readiness:

1. ✅ Read `IMPLEMENTATION_EVOLUTION.md` to understand context
2. ✅ Read this document to understand what needs doing
3. Pick a milestone (8, 9, or 10) and work through it
4. Update `PROJECT_STATUS.md` as you complete tasks
5. Run tests frequently to ensure nothing breaks
6. Profile performance after each milestone

**Start with Milestone 8** (Boundaries) - it's the quickest and unblocks testing.
