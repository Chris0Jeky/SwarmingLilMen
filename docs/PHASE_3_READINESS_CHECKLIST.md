# Phase 3 Readiness Checklist

> **Historical readiness checklist:** [`PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source
> of truth for verified implementation, test, and performance state. This document preserves prior
> criteria and assessments; its unchecked boxes are not a current work queue. Select work from
> [epic #10's current open children](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10) and their
> dependencies.

**Last Updated**: 2026-10-29 (documentation and scheduling reconciliation)
**Assessment context**: The milestone checklist and estimates below retain the earlier readiness
assessment. This documentation update does not mark milestones complete or revalidate them.

> Use the criteria below to assess evidence gaps before Phase 3 (Combat & Metabolism), after checking current implementation and issue state.

---

## Overview

The earlier assessment required a complete, debuggable canonical foundation before Phase 3.
It recorded the perception radius/self/wrap contract and total `MaxForce` bound, while leaving
instrumentation UX, later milestones, multi-group semantics and performance evidence as gaps.
Some of that work has since shipped, including canonical benchmarks and reusable telemetry.
Current evidence and remaining limitations belong to [PROJECT_STATUS.md](../PROJECT_STATUS.md),
not this preserved assessment.

**Historical Estimated Effort**: 1-2 weeks of focused development; not a current forecast

---

## Historical Critical Path: Correctness Gaps and Readiness

The following checklist records the earlier assessment against `archive/NewImplementation.md`.
Check current code, merged work and open issues before treating an entry as outstanding.

### ✅ Core Scaffolding and Single-Boid Path (Milestones 0-1)
- [x] Core scaffolding (Vec2, Boid, World, Rng, interfaces)
- [x] Single boid motion with constant speed
- [ ] Long-horizon seeded and golden acceptance, tracked in
  [issue #21](https://github.com/Chris0Jeky/SwarmingLilMen/issues/21) (#17 closed with the seeded
  construction work; golden-trace fixtures and the cross-OS job remain #21)

### ✅ Milestone 2: Perception (COMPLETE)
- [x] Field-of-view filtering with weights
- [x] Canonical Grid and Naive indexes enforce inclusive radius filtering, self-exclusion, and
  toroidal minimum-image distance with deterministic capped-result status; focused equivalence tests
  cover seams, corners, coincident agents, one/two-cell worlds, partial terminal cells, unwrapped
  input positions, dense truncation, and 200 randomized scenarios.
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
- [x] Enforce the total steering bound of `MaxForce` ([issue #19](https://github.com/Chris0Jeky/SwarmingLilMen/issues/19)).
  Separation used to clamp to a fresh `MaxForce` on top of an already-spent whisker budget, so one
  tick could compose 2x the ceiling; it now draws from the same per-tick remainder as every other
  contribution. `CanonicalSteeringBudgetTests` pins the bound, the priority-withholding semantics,
  and the instrumentation seam that makes the composed value observable
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

The milestone-7 follow-up was separated into
[issue #40](https://github.com/Chris0Jeky/SwarmingLilMen/issues/40). Its current scope and dependencies
come from the issue and epic, not the archived Wave 0/Wave 1 grouping.

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
  all agents in one cell, wrapped one/two-cell worlds, partial terminal cells, unwrapped positions,
  randomized distributions, and deterministic truncation are covered.
- [x] **Steady-state allocation proof**: both direct query implementations report zero
  current-thread allocations after tiered-compilation warm-up.
- [ ] **Canonical performance validation**: one reported-only 1,000-agent Release diagnostic
  measured Grid at 1.213 ms/tick and Naive at 27.155 ms/tick (22.40x) after matched warm-up, but no
  distribution or scale claim follows from one sample. Canonical BenchmarkDotNet and
  `MemoryDiagnoser` coverage remain tracked in
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
    legacy/canonical speedup calculation (`SwarmSim.Tests/PerformanceTests.cs`).

- [ ] **Identify Regressions**
  - If canonical is slower, profile with dotTrace
  - Document top-3 hotspots
  - Create performance improvement tasks for Phase 5

### 🔍 Allocation Testing

- [ ] **Zero-Allocation Verification**
  - Current canonical `Step()` does not satisfy this target because perception-snapshot refresh
    allocates three arrays (`SwarmSim.Core/Canonical/CanonicalWorld.cs` (`CanonicalWorld.UpdatePerceptionSnapshot`)).
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
> does not estimate issue-level scope, gates, reviews, or merge time for the current epic and its
> open children, so it is not a current end-to-end forecast.

The former Wave 0 and Wave 1 lists are historical ordering context. There is no fixed overnight
queue in this document. Before scheduling work, inspect the epic's current open children,
dependency gates and merged evidence; do not restart closed issues because their numbers appear
in an old plan.

---

## Success Criteria

Phase 3 readiness achieved when:

1. [ ] All milestones 0-10 from `archive/NewImplementation.md` complete
2. [ ] Multi-group support implemented and tested
3. [ ] Performance validated (10k agents @ 60+ FPS)
4. [ ] Zero allocations in hot path confirmed
5. [ ] Visualization tools built for debugging
6. [x] Comprehensive test suite (50+ tests passing)

These checkmarks preserve the earlier assessment. They do not establish present Phase 3 readiness;
each criterion needs current evidence in `PROJECT_STATUS.md` and the corresponding issue.

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

1. Read [PROJECT_STATUS.md](../PROJECT_STATUS.md) for current implementation and evidence.
2. Inspect [epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10), its current open
   children and their dependencies.
3. Select an eligible open task after checking merged work; do not resume from a fixed issue-number list.
4. Use this checklist and `archive/IMPLEMENTATION_EVOLUTION.md` as historical criteria and rationale.
5. Update `PROJECT_STATUS.md` with new evidence and run the Release gate documented in
   [CONTRIBUTING.md](../CONTRIBUTING.md#running-tests).
6. Profile changed hot paths when their performance needs validation.

Current issue dependencies govern scheduling. Historical wave names do not require repeating
completed work, and this checklist does not declare a new milestone completion.
