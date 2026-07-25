# Implementation Evolution: From SoA Systems to Canonical Boids

> [`PROJECT_STATUS.md`](PROJECT_STATUS.md) is the live source of truth for verified implementation,
> test, and performance state; this document explains migration rationale and may retain history.

**Last Updated**: 2026-07-25 (verified-state reconciliation)
**Status**: Transition in progress - core scaffolding and steering-rule implementations exist;
perception semantics, rule-composition enforcement, and instrumentation UX are partial. Readiness
milestones 8-10, multi-group semantics, and canonical performance evidence remain incomplete

---

## Executive Summary

SwarmingLilMen underwent a significant architectural pivot starting around commit `f5d9dca` (Create NewImplementation.md). The project transitioned from a Structure-of-Arrays (SoA) systems-based approach to a cleaner, test-driven canonical boids implementation based on Reynolds' steering behaviors. This document explains **why** the change happened, **what** was wrong with the old approach, **how** the new approach differs, and **what needs to happen** before Phase 3.

---

## Table of Contents

1. [The Old Implementation (Systems-Based SoA)](#the-old-implementation-systems-based-soa)
2. [Problems with the Old Approach](#problems-with-the-old-approach)
3. [The New Implementation (Canonical Boids)](#the-new-implementation-canonical-boids)
4. [Key Algorithmic Differences](#key-algorithmic-differences)
5. [Migration Status](#migration-status)
6. [Path Forward Before Phase 3](#path-forward-before-phase-3)

---

## The Old Implementation (Systems-Based SoA)

### Architecture Overview

The original implementation (still present in `SwarmSim.Core/Systems/` and `World.cs`) followed a classic ECS-inspired pattern:

```
World (SoA arrays) → Systems Pipeline → Per-Tick Updates
  ├─ X[], Y[], Vx[], Vy[], Fx[], Fy[] arrays
  ├─ SenseSystem: Query neighbors → Compute aggregates
  ├─ BehaviorSystem: Aggregates → Forces (Fx[], Fy[])
  └─ IntegrateSystem: Forces → Velocity → Position
```

### Key Characteristics

1. **Data Layout**: Structure of Arrays (SoA)
   - Agent data stored in parallel arrays: `X[]`, `Y[]`, `Vx[]`, `Vy[]`, `Energy[]`, etc.
   - Good for cache locality in theory
   - Complex to reason about and debug in practice

2. **Two-Pass Processing**:
   - **SenseSystem** queries spatial grid, computes aggregates:
     - `NeighborCount[]` - how many neighbors each agent has
     - `SeparationX[]`, `SeparationY[]` - accumulated repulsion vectors
     - `AlignmentVx[]`, `AlignmentVy[]` - sum of neighbor velocities
     - `CohesionX[]`, `CohesionY[]` - sum of neighbor positions
   - **BehaviorSystem** reads these aggregates and writes forces to `Fx[]`, `Fy[]`

3. **Force-Based Physics**:
   - Boids rules generated **forces** (not steering)
   - Forces accumulated in scratch buffers
   - Integration: `v += F*dt; v *= friction; x += v*dt`
   - Relied on force/friction equilibrium for speed control

4. **Separation Weighting**:
   - The current legacy `SenseSystem` accumulates normalized away directions with bounded linear
     radial falloff: `strength = 1 - distance / radius`
   - Earlier prototype notes discussed inverse-square weighting, but that is not the current legacy
     implementation

### File Structure (Old Implementation)

```
SwarmSim.Core/
├── World.cs                    # SoA arrays, Tick() orchestration
├── Systems/
│   ├── ISimSystem.cs          # System interface
│   ├── SenseSystem.cs         # Neighbor queries, aggregate computation
│   ├── BehaviorSystem.cs      # Force generation from aggregates
│   └── IntegrateSystem.cs     # Velocity/position integration
└── Spatial/
    └── UniformGrid.cs         # Spatial partitioning
```

---

## Problems with the Old Approach

### 1. **Debugging Nightmare**

The two-pass architecture made it extremely difficult to trace behavior:
- To understand why an agent moved incorrectly, you had to:
  1. Check what neighbors SenseSystem found
  2. Verify the aggregates it computed
  3. Check how BehaviorSystem interpreted those aggregates
  4. Verify the forces it generated
  5. Check IntegrateSystem's application of those forces
- **No single place** to inspect the full decision-making process
- Aggregate arrays (`SeparationX[]`, etc.) were opaque intermediate state

### 2. **Force-Friction Equilibrium Pathologies**

The force-based approach created tuning nightmares:
- **Problem**: Agents needed to reach equilibrium speeds through force/friction balance
- **Symptom**: Setting friction < 1.0 caused agents to "get stuck" in low-speed states
- **Root Cause**: The earlier prototype's separation/clamping and continuous damping could leave
  too little effective acceleration after `dt`; the current legacy separation has since changed to
  bounded linear radial falloff
- **Bandaid Fix**: Setting friction = 1.0 "worked" but eliminated natural speed variation

`MakingBoidsBetter.md` preserves the investigation of that earlier inverse-square variant; it is
historical design context rather than a description of the current `SenseSystem`.

### 3. **Parameter Sensitivity**

The force-based model was extremely sensitive to parameter tuning:
- Small changes in weights could cause dramatic behavioral shifts
- Separation weight vs. friction vs. maxSpeed all interacted in non-obvious ways
- Hard to predict the effect of changing one parameter
- No "standard" parameter ranges from literature

### 4. **Non-Canonical Algorithm**

The implementation diverged from Reynolds' original steering behaviors:
- **Reynolds' approach**: Compute *desired velocity* → steer toward it with bounded force
- **Old approach**: Compute raw forces → hope friction creates equilibrium
- This made it impossible to reference standard boids literature for tuning guidance

### 5. **Testing Challenges**

The SoA + systems architecture made unit testing difficult:
- Testing a single rule required:
  - Creating a full World with capacity
  - Populating all arrays
  - Running SenseSystem first
  - Then running BehaviorSystem
  - Inspecting force arrays
- Couldn't easily test "separation rule in isolation"
- Most tests were integration tests, not unit tests

### 6. **Lack of Instrumentation**

The old implementation provided little visibility into decision-making:
- No way to see *why* an agent chose a particular direction
- No per-rule contribution tracking
- No neighbor weight information
- Debugging required printf debugging in hot loops

---

## The New Implementation (Canonical Boids)

### Philosophy

Starting with commit `f5d9dca` (Create NewImplementation.md), a fresh implementation was begun in the `SwarmSim.Core.Canonical` namespace with these principles:

1. **Steering Behaviors, Not Forces** - Follow Reynolds' canonical formulation
2. **Test-Driven Development** - Write tests first, code second
3. **Incremental Milestones** - Build up complexity gradually
4. **Immutable Data** - `readonly struct Boid`, functional transformations
5. **Clear Abstractions** - Separate concerns cleanly

### Architecture Overview

```
CanonicalWorld
  ├─ Boid[] (immutable structs)
  ├─ ISpatialIndex (pluggable neighbor search)
  ├─ List<IRule> (separation, alignment, cohesion)
  └─ Step() method:
      1. Rebuild spatial index
      2. For each boid:
          a. Query broad-phase candidates; apply FOV filtering
          b. Accumulate whisker look-ahead steering within the budget
          c. Update priority/hysteresis; evaluate positional core-rule slots
          d. Apply wander only when force budget remains
          e. Integrate: v += steer*dt; shape avoidance; limit turn; normalize to allowed speed
          f. Integrate: x += v*dt; wrap boundaries
      3. Double-buffer swap
```

This is the intended flow. The current grid index leaves radius candidates unfiltered
([issue #18](https://github.com/Chris0Jeky/SwarmingLilMen/issues/18)), and the current whisker plus
separation path can exceed the intended `MaxForce` budget
([issue #19](https://github.com/Chris0Jeky/SwarmingLilMen/issues/19)). Rule slots are hard-coded,
later results are discarded, and a clamped separation vector whose squared magnitude exceeds the
current `1e-6` cutoff exhausts the remainder before alignment, cohesion, and wander
([issue #27](https://github.com/Chris0Jeky/SwarmingLilMen/issues/27)).

### Key Characteristics

1. **Immutable Boids**:
   ```csharp
   public readonly struct Boid
   {
       public Vec2 Position { get; }
       public Vec2 Velocity { get; }
       public byte Group { get; }
   }
   ```
   - No mutable state
   - Transformations create new instances
   - Easy to reason about

2. **Reynolds Steering**:
   ```csharp
   // Conceptual flow; CanonicalWorld.Step currently performs composition inline.
   Vec2 desired = ComputeDesiredVelocity();
   Vec2 steering = desired - current;
   ComposeThroughPriorityBudget(steering);
   Vec2 newVelocity = IntegrateWithAvoidanceAndTurnLimit(allowedSpeed);
   ```
   - Each rule computes a *desired velocity*
   - Steering = desired - current; the caller clamps and composes contributions
   - Final velocity is normalized to `TargetSpeed`, reduced by the configured separation-speed
     droop while the priority blend is active

3. **Rule Interface (composition remains positional)**:
   ```csharp
   public interface IRule
   {
       Vec2 Compute(
           int selfIndex,
           Boid self,
           ReadOnlySpan<Boid> boids,
           ReadOnlySpan<int> neighborIndices,
           ReadOnlySpan<float> neighborWeights,
           RuleContext context);
   }
   ```
   - Each rule is isolated and testable
   - `CanonicalWorld` currently assigns separation/alignment/cohesion semantics to slots 0/1/2;
     reordering changes those semantics, and results from later slots are discarded
   - Named enable/disable/reorder composition is owned by
     [issue #27](https://github.com/Chris0Jeky/SwarmingLilMen/issues/27)

4. **FOV-Weighted Neighbor Candidates**:
   - The intended spatial-index contract filters by radius; the current grid implementation does
     not yet honor that argument ([issue #18](https://github.com/Chris0Jeky/SwarmingLilMen/issues/18))
   - Candidates are then filtered by field-of-view cone
   - **Weighted by position in FOV**: neighbors at edge of vision have less influence
   - `neighborWeights[]` passed to each rule

5. **Rich Instrumentation**:
   ```csharp
   public class RuleInstrumentation
   {
       // Per-agent metrics
       int[] _neighborCounts;
       float[] _neighborWeightSums;
       float[] _separationMagnitudes;
       float[] _alignmentMagnitudes;
       float[] _cohesionMagnitudes;
   }
   ```
   - Track exactly what each agent "sees"
   - Record each rule's contribution
   - Enable data-driven debugging

### File Structure (New Implementation)

```
SwarmSim.Core/Canonical/
├── Vec2.cs                     # 2D vector math
├── Boid.cs                     # Immutable agent struct
├── CanonicalWorld.cs           # Main simulation orchestrator
├── CanonicalWorldSettings.cs  # Configuration
├── RuleContext.cs              # Shared context for rules
├── RuleInstrumentation.cs      # Metrics collection
├── IRule.cs                    # Rule interface
├── ISpatialIndex.cs            # Spatial query interface
├── NaiveSpatialIndex.cs        # O(n²) reference implementation
├── GridSpatialIndex.cs         # O(n) grid-based implementation
└── Rules/
    ├── SeparationRule.cs       # Steer away from close neighbors
    ├── AlignmentRule.cs        # Match neighbor headings
    └── CohesionRule.cs         # Move toward center of mass
```

---

## Key Algorithmic Differences

### Separation

**Old (Force-Based)**:
```csharp
// In the current legacy SenseSystem
for each neighbor within separationRadius:
    towardNeighbor = neighbor.pos - self.pos
    distance = |towardNeighbor|
    if distance > 0:
        away = -(towardNeighbor / distance)
        strength = (separationRadius - distance) / separationRadius
        separation[i] += away * strength  // bounded linear radial falloff

// In BehaviorSystem
float sepX = separationX[i];
float sepY = separationY[i];
float sepMag = sqrt(sepX² + sepY²);
if (sepMag > 0):
    float desiredSpeed = maxSpeed * separationWeight;
    float desiredVx = (sepX / sepMag) * desiredSpeed;
    float desiredVy = (sepY / sepMag) * desiredSpeed;
    float steerX = desiredVx - currentVx;
    float steerY = desiredVy - currentVy;
    (steerX, steerY) = ClampMagnitude(steerX, steerY, maxForce);
    fx[i] += steerX;
    fy[i] += steerY;
```

**New (Steering-Based)**:
```csharp
// In SeparationRule.Compute()
Vec2 accumulator = Vec2.Zero;
for each neighbor within separationRadius:
    Vec2 delta = self.Position - neighbor.Position;
    float distSq = delta.LengthSquared;
    if distSq > 0 and distSq <= radiusSq:
        float dist = sqrt(distSq);
        Vec2 direction = delta / dist;
        float strength = max(0, 1 - dist/radius);  // Linear falloff
        float influence = strength / dist * fovWeight; // inverse-distance and FOV weighting
        accumulator += direction * influence;

Vec2 desired = accumulator.WithLength(
    context.TargetSpeed * ruleWeight * context.SeparationPriorityBoost);
Vec2 steer = desired - self.Velocity;
return steer;
```

**Key Differences**:
1. **Legacy aggregate**: Normalized away direction with bounded linear radial falloff
2. **Canonical weighting**: The same radial falloff is multiplied by inverse distance and FOV weight
3. **Priority boost**: Desired speed is multiplied by `SeparationPriorityBoost`
4. **Caller budget**: The rule returns unclamped steering; `CanonicalWorld` clamps/composes it

### Alignment

**Old**: Accumulate sum of neighbor velocities → compute average → steer toward it
**New**: Same algorithm, but computed in isolated rule with neighbor weights

### Cohesion

**Old**: Accumulate sum of neighbor positions → compute average → steer toward it
**New**: Same algorithm, but computed in isolated rule with neighbor weights

### Integration

**Old**:
```csharp
// Forces accumulated in Fx[], Fy[]
vx[i] += Fx[i] * dt;
vy[i] += Fy[i] * dt;
vx[i] *= friction;  // Speed control via damping
vy[i] *= friction;
x[i] += vx[i] * dt;
y[i] += vy[i] * dt;
```

**New**:
```csharp
// Simplified shape of the current integration path
Vec2 nextVelocity = boid.Velocity + steering * deltaTime;
float allowedSpeed = Settings.TargetSpeed
    * (1f - Settings.SeparationSpeedDroop * priorityBlend);
nextVelocity = ApplyShapedAvoidance(nextVelocity, allowedSpeed);
nextVelocity = LimitTurnAndNormalize(nextVelocity, allowedSpeed);

Vec2 nextPosition = boid.Position + nextVelocity * deltaTime;
nextPosition = WrapToroidally(nextPosition);
```

**Key Differences**:
1. **Speed control**: Old used friction; new normalizes directly to an allowed speed
2. **Priority droop**: Allowed speed can fall below `TargetSpeed` by the configured droop while
   priority blending is active (3% at the current default and full blend)
3. **Shaping and turn limit**: Nearest-neighbor avoidance can bias velocity before the angular-rate
   limiter normalizes it

### Field of View

**Old**:
```csharp
// In SenseSystem, per neighbor:
if (fieldOfView < 360f) {
    float vMag = sqrt(vx[i]² + vy[i]²);
    if (vMag > epsilon) {
        Vec2 forward = (vx[i]/vMag, vy[i]/vMag);
        Vec2 toNeighbor = (dx, dy);
        float dist = sqrt(dx² + dy²);
        Vec2 dirToNeighbor = (dx/dist, dy/dist);
        float dot = Dot(forward, dirToNeighbor);
        float threshold = cos(fieldOfView * 0.5 * DEG2RAD);
        if (dot < threshold)
            continue;  // Skip neighbor
    }
}
// Binary include/exclude, no weighting
```

**New**:
```csharp
// In CanonicalWorld.FilterByFieldOfView()
Vec2 forward = boid.Forward;
float fieldOfViewCos = context.FieldOfViewCos;
bool fullCircle = fieldOfViewCos <= -1f;
float range = max(1e-6f, 1f - fieldOfViewCos);

for each candidate neighbor:
    Vec2 delta = neighbor.Position - self.Position;
    if (delta.IsNearlyZero()) {
        weight = 1.0;  // Special case
        include;
    }
    Vec2 direction = delta.Normalized;
    float dot = Dot(forward, direction);
    float normalized = (dot - fieldOfViewCos) / range;
    if (normalized <= 0)
        exclude;
    float weight = min(normalized, 1.0);  // Linear falloff
    include with weight;
```

**Key Differences**:
1. **Weighted influence**: New approach weights neighbors by how "centered" they are in FOV
2. **Smooth falloff**: Neighbors at edge of vision contribute less, not binary include/exclude
3. **Better behavior**: Reduces "flickering" as neighbors enter/leave FOV boundary

---

## Smoothing & Collision Avoidance System (Session 3.3)

After implementing the canonical boids foundation, testing revealed collision issues and "bouncy" behavior. A comprehensive smoothing system was designed and implemented following the plan documented in `filesAndResources/CanonicalBoids_SmoothingPlan.md`.

### Problems Identified

1. **Sharp Separation Activation**: Priority mode engaged abruptly at threshold, causing jerky movements
2. **Chaotic Wander**: Random angle each tick created jittery, unnatural movement
3. **Missing Collision Avoidance**: Removing the hard "snap-away" left agents with no emergency response
4. **Hard Priority Gating**: Alignment/cohesion completely off during separation, breaking group cohesion

### Solutions Implemented

**1. Budget-Gated Smooth Wander** (`CanonicalWorld.cs:288-295`)
- While `WanderStrength > 0` and force budget remains, each agent maintains a persistent wander
  angle that evolves smoothly
- `WanderRate` parameter (1.5 rad/s) controls turn rate
- Eligible ticks change the angle by small random amounts: `±WanderRate * dt`; disabled wander or
  an exhausted budget pauses both the angle update and its steering contribution
- Creates flowing, natural movement instead of discrete direction changes

**2. Gradual Avoidance Falloff** (`CanonicalWorld.cs:300-322`)
- Avoidance begins at `rGradualStart = SeparationRadius * 2.0` (gentle)
- Influence increases quadratically: `distanceRatio²` as agents approach threshold
- Combines with priority blend at close range for emergency response
- No sharp activation - steering smoothly intensifies with proximity

**3. Shaped Separation** (`CanonicalWorld.cs:309-320`)
- Blends two escape directions:
  - **Lateral**: Perpendicular "shoulder past" deflection (at medium distance)
  - **Away**: Direct repulsion from nearest neighbor (at close range)
- Smoothstep transition between zones: `blendWeight = SmoothStep(rHard, rSoft, distance)`
- Prevents head-on bouncing, creates natural lane-change behavior

**4. Attempted Soft Gating** (`CanonicalWorld.cs:247-273`)
- Alignment/cohesion vectors are multiplied by
  `attenuation = 1.0 - (priorityBlend * 0.7)`
- A clamped separation vector whose squared magnitude exceeds the current `1e-6` cutoff sets the
  remaining force budget to zero before those vectors or wander are applied; wander's angle also
  does not advance on that tick
- [Issue #27](https://github.com/Chris0Jeky/SwarmingLilMen/issues/27) owns named composition and the
  explicit arbitration contract

**5. Enhanced Perception** (`CanonicalWorld.cs:21-23, 174-198`)
- Per-agent perception data in snapshot:
  - `NearestDistances[]` - distance to closest neighbor
  - `NearestAngles[]` - angular position of nearest neighbor
  - `WhiskerCounts[]` - neighbors in lookahead capsule
- Enables data-driven analysis of flocking quality

### Historical Qualitative Observations

Earlier interactive sessions described smooth movement, shoulder-passing, and reduced ping-pong,
but no retained capture or automated acceptance proves those visual claims. The 12 current canonical
tests include angular-limiter and hysteresis coverage; they do not prove group cohesion while
separation consumes the remaining force budget.

### Visualization

The **blue circle** in the overlay is the **whisker lookahead capsule** - it shows the predictive collision detection zone ahead of the tracked boid (radius = SeparationRadius, lookahead = TargetSpeed * WhiskerTimeHorizon).

---

## Migration Status

### Implemented Components and Known Gaps

✅ **Core Infrastructure** (Milestones 0-1 plus shared scaffolding):
- `Vec2` struct with all vector operations
- `Boid` readonly struct
- `CanonicalWorld` with double-buffered stepping
- `IRule` interface and `RuleContext`
- Fixed timestep integration with semi-implicit Euler
- `NaiveSpatialIndex` (O(n²) reference)
- `GridSpatialIndex` (O(n) using UniformGrid)
- Full long-horizon deterministic/golden acceptance remains pending #17 and #21

✅ **Steering Rule Implementations** (Milestones 3-5):
- `SeparationRule` with 1/d weighting and falloff
- `AlignmentRule` with neighbor averaging
- `CohesionRule` with center-of-mass steering
- The prescribed multi-tick separation/alignment/cohesion behavior scenarios remain open in
  [issue #41](https://github.com/Chris0Jeky/SwarmingLilMen/issues/41)

⚠️ **Partial Perception** (Milestone 2):
- Field-of-view cone filtering
- FOV-based neighbor weighting (linear falloff)
- `GridSpatialIndex` does not currently honor radius or self-exclusion, so alignment and cohesion
  can consume the wrong neighborhood; neither index/rule path applies toroidal neighbor deltas
- The full contract and equivalence fix are owned by
  [issue #18](https://github.com/Chris0Jeky/SwarmingLilMen/issues/18)
- The rule implementations above exist, but their integrated behavior must be revalidated against
  the corrected perception contract

⚠️ **Partial Rule Composition** (Milestone 6):
- Separation, alignment, and cohesion are hard-coded to slots 0/1/2; results from later `AddRule`
  slots are discarded, and clamped separation above the current cutoff exhausts the budget before
  alignment, cohesion, and wander
- [Issue #27](https://github.com/Chris0Jeky/SwarmingLilMen/issues/27) owns the named composition and
  arbitration contract
- Whisker steering can be followed by a separation contribution clamped to the full `MaxForce`,
  so the total can exceed the intended bound; the invariant fix and trajectory evidence are owned
  by [issue #19](https://github.com/Chris0Jeky/SwarmingLilMen/issues/19)

⚠️ **Partial Instrumentation** (Milestone 7 backend and basic overlay only):
- `RuleInstrumentation` for metrics collection
- Per-agent neighbor counts and weight sums
- Per-rule contribution magnitudes
- Metrics accessible via `TryGetMetrics()`
- Basic selected-boid overlay with perception bounds, neighbor links, whisker markers, and metrics
- Missing: an FOV arc, rule-colored links, a steering-vector arrow, rule/FOV controls, and
  rule-toggle acceptance coverage; tracked in
  [issue #40](https://github.com/Chris0Jeky/SwarmingLilMen/issues/40)

✅ **Existing Test Coverage**:
- `CanonicalBoidsTests` with 12 unit tests
- Vec2 math tests
- Single boid constant speed test
- FOV filtering tests
- Same-process determinism comparisons (not long-horizon/golden coverage)
- Per-rule behavior tests (separation, alignment, cohesion)
- Angular rate limiter tests
- Priority hysteresis tests
- Whisker capsule detection tests
- Per-agent perception snapshot tests

✅ **Smoothing System** (Phase A-C from CanonicalBoids_SmoothingPlan.md):
- Angular rate limiter (MaxTurnRateDegPerSecond)
- Priority hysteresis with enter/exit/hold thresholds
- Shaped separation with lateral+away blending
- Gradual avoidance falloff (quadratic distance function)
- Smooth wander angle evolution while force budget remains; qualifying separation pauses it
- Candidate attenuation for alignment/cohesion during priority; current budget starvation remains
  open in [issue #27](https://github.com/Chris0Jeky/SwarmingLilMen/issues/27)
- Enhanced PerceptionSnapshot with per-agent data

✅ **Renderer Integration** (Partial):
- `--canonical` flag to use CanonicalWorld
- Single-group visualization mode
- Runs alongside legacy renderer

### In Progress

🔄 **Renderer Polish**:
- Visualization of a complete FOV arc
- Neighbor link rendering with per-rule colors
- Steering vector display
- Instrumentation overlay (counts, weights, contributions)
- Rule enable/disable and FOV controls

🔄 **Testing Coverage**:
- Boundary/wrapping tests (Milestone 8)
- Spatial index equivalence tests (Milestone 9)
- Property tests at scale (Milestone 10)
- Metrics/polarization tests (Milestone 10)

### Not Yet Started

❌ **Full Renderer Migration**:
- Multi-group support in CanonicalWorld
- Group aggression matrix
- Combat interactions
- Replace legacy World/Systems with Canonical implementation

❌ **Advanced Features**:
- Obstacle avoidance
- Boundary reflection (currently only wrapping)

❌ **Performance**:
- SIMD with `Vector2<T>`
- Parallelization with per-worker accumulators
- Benchmark suite comparison (old vs. new)

---

## Path Forward Before Phase 3

To complete the migration and reach a solid foundation for Phase 3 (Combat & Metabolism), we need to:

### 1. Resolve the Ordered Wave 1 Correctness Gates

- [ ] #17 - remove wall-clock/seed-wiring determinism breaks and prove long-horizon reproducibility
- [ ] #18 - enforce the perception/spatial-index radius, self-exclusion, and wrap contract
- [ ] #19 - enforce the total `MaxForce` composition budget
- [ ] #20 - make telemetry opt-in/zero-allocation and add canonical benchmarks; complete the agent
  work, leave the PR open for the owner-only F12/HUD-parity check, then continue
- [ ] #21 - lock and compare deterministic traces after #17-#19 land

Execute these strictly in epic #10 order. Under the owner-authorized exception for this overnight
run, the #20 PR remains open at its manual gate while #21 proceeds. The missing milestone 3-6
scenario coverage (#41) and milestone 7 renderer UX (#40) deliberately wait for their later
contracts rather than introducing temporary fixture, telemetry, configuration, or positional-rule
seams.

### 2. Complete Core Testing (Milestones 8-10)

**Goal**: Achieve feature parity with old implementation and verify correctness

- [ ] **Milestone 8 - Boundaries**:
  - [ ] Test toroidal wrapping behavior
  - [ ] Implement wall reflection option
  - [ ] Test agents don't "leak" through boundaries

- [ ] **Milestone 9 - Spatial Index Equivalence**:
  - [ ] Property test: `GridSpatialIndex` == `NaiveSpatialIndex` for random scenarios
  - [ ] Verify neighbor sets match exactly (within FP tolerance)
  - [ ] Benchmark performance improvement (expect ~O(n) vs O(n²))

- [ ] **Milestone 10 - Metrics & Properties**:
  - [ ] Implement polarization metric (mean normalized heading)
  - [ ] Implement clustering metric (avg nearest-neighbor distance)
  - [ ] Property test: with separation on, distance increases over time
  - [ ] Property test: with alignment on, heading variance decreases
  - [ ] Add metrics to instrumentation

**Why Critical**: These tests provide confidence that the canonical implementation is correct and equivalent to the old one. Without them, migrating to Phase 3 risks building on a shaky foundation.

### 3. Add Multi-Group Support

**Goal**: Restore multi-group capabilities from old implementation

- [ ] Add `GroupSettings` to `CanonicalWorldSettings`:
  ```csharp
  public record GroupSettings(
      byte GroupId,
      float SeparationWeight,
      float AlignmentWeight,
      float CohesionWeight
  );
  ```

- [ ] Add per-group filtering to rules:
  ```csharp
  // In rules, only consider same-group neighbors for alignment/cohesion
  if (context.SameGroupOnly && boids[neighborIndex].Group != self.Group)
      continue;
  ```

- [ ] Add cross-group perception:
  ```csharp
  // Agents can *see* all groups (for separation)
  // But only *align/cohere* with same group
  ```

- [ ] Test multi-group flocking behavior:
  - [ ] Two groups maintain separation
  - [ ] Groups internally cohesive
  - [ ] Groups pass through each other cleanly

**Why Critical**: Phase 3 requires multiple groups with aggression matrices. Need solid multi-group foundation first.

### 4. Enhanced Instrumentation & Debugging

**Goal**: Make the canonical implementation as observable as possible

- [ ] **Visualization Toggles** (press keys to enable):
  - [ ] `V` - Show perception radius circles
  - [ ] `F` - Show field-of-view cones
  - [ ] `N` - Show neighbor links (color by rule: red=sep, blue=align, green=coh)
  - [ ] `S` - Show steering vector arrows
  - [ ] `I` - Show instrumentation overlay (neighbor counts, weights, contributions)

- [ ] **Inspector Mode**:
  - [ ] Click agent to "select"
  - [ ] Sidebar shows:
    - Position, velocity, group
    - Neighbor count, total weight
    - Per-rule contributions (magnitudes)
    - List of neighbor indices

- [ ] **Logging/Export**:
  - [ ] CSV export of per-agent metrics every N ticks
  - [ ] Snapshot full world state for replay/analysis
  - [ ] Python scripts to analyze exported data

**Why Critical**: The whole point of the canonical rewrite was to enable easier debugging. Need to actually build the tools to leverage it.

### 5. Performance Validation

**Goal**: Ensure canonical implementation meets performance targets

- [ ] **Benchmark Suite**:
  - [ ] Port `WorldTickBenchmarks` to use `CanonicalWorld`
  - [ ] Compare old vs. new implementation at 1k, 10k, 50k agents
  - [ ] Identify performance regressions

- [ ] **Allocation Testing**:
  - [ ] Verify zero allocations per `Step()` call
  - [ ] Use dotMemory to profile
  - [ ] Eliminate any `new` in hot paths

- [ ] **Profiling**:
  - [ ] Run dotTrace on 50k agent scenario
  - [ ] Identify top-3 hotspots
  - [ ] Document optimization opportunities for Phase 5

**Target**: Match or exceed the dated legacy simulation-tick baseline. Canonical throughput and
renderer FPS are currently unmeasured; use the verified command/results in `PROJECT_STATUS.md`.

**Why Critical**: Can't justify the rewrite if performance regresses. Need data to make optimization decisions.

### 6. Full Migration Decision

At this point, decide whether to:

**Option A: Replace Old Implementation**
- Delete `SwarmSim.Core/Systems/` and old `World.cs`
- Rename `Canonical/` to root namespace
- Update all references
- **Pros**: Clean codebase, no confusion
- **Cons**: Irreversible, lose SoA architecture learnings

**Option B: Keep Both**
- Maintain both implementations side-by-side
- Use command-line flag to select: `--legacy` vs `--canonical`
- **Pros**: Can compare behaviors, keep SoA for reference
- **Cons**: Double maintenance burden, confusing to newcomers

**Option C: Gradual Migration**
- Port Phase 3 features (combat, metabolism) to canonical first
- Only once fully validated, remove old implementation
- **Pros**: Safest, allows comparison
- **Cons**: Longest timeline

**Recommendation**: **Option C** - Gradual Migration
- Gives us confidence before burning bridges
- Allows side-by-side behavioral comparison
- If canonical has issues, old implementation is still there

---

## Lessons Learned

### What Went Right

1. **TDD Approach**: Writing tests first (via `NewImplementation.md` milestones) caught issues early
2. **Steering vs. Forces**: Reynolds' steering formulation is objectively better for boids
3. **Immutable Data**: `readonly struct Boid` made reasoning about state much simpler
4. **Clear Abstractions**: `IRule` interface allowed easy testing and composition
5. **Instrumentation**: Rich metrics from the start made debugging tractable

### What Went Wrong (Old Implementation)

1. **Premature Optimization**: SoA layout chosen for performance before correctness was proven
2. **Two-Pass Design**: Separating sensing from behavior seemed clean but was debugging nightmare
3. **Force-Based Physics**: Non-canonical approach made parameter tuning impossible
4. **Lack of Testing**: Integration tests only, no unit tests for individual rules
5. **No Instrumentation**: Had to printf debug to understand what was happening

### Recommendations for Phase 3+

1. **Stick to Canonical**: Don't deviate from Reynolds' formulations without strong reason
2. **Test First**: Write unit tests before implementation, not after
3. **Instrument Everything**: Metrics and observability are not "nice to have", they're essential
4. **Composition Over Complexity**: Simple, composable rules beat clever optimizations
5. **Validate Before Optimizing**: Get correctness first, then profile and optimize

---

## References

### External Resources

- **Reynolds' Steering Behaviors**: https://www.red3d.com/cwr/steer/
- **Boids Algorithm**: https://www.red3d.com/cwr/boids/
- **Nature of Code (Boids)**: https://natureofcode.com/autonomous-agents/
- **Fix Your Timestep**: https://gafferongames.com/post/fix_your_timestep/

### Internal Documents

- `NewImplementation.md` - TDD roadmap and milestones
- `MakingBoidsBetter.md` - Diagnosis of force-based approach issues
- `PROJECT_STATUS.md` - Current implementation status
- `CLAUDE.md` - Development guidelines

### Commit History

Key commits in the transition:
- `f5d9dca` - Create NewImplementation.md (the pivot point)
- `a544180` - Add Vec2 struct
- `025051a` - Add Boid struct
- `2305a86` - Add RuleContext
- `6d16999` - Add CanonicalWorld class
- `783e691` - Add CohesionRule
- `5a303e1` - Add AlignmentRule
- `002a37a` - Add SeparationRule
- `24225a0` - Add GridSpatialIndex
- `0cefcfc` - Add metrics and instrumentation

---

## Conclusion

The transition from the systems-based SoA approach to the canonical boids implementation represents a **necessary course correction**. The old implementation, while architecturally sound on paper, proved extremely difficult to debug and tune in practice. The force-based physics model created parameter sensitivity issues that made emergent behavior unpredictable.

The new canonical implementation, while less "architecturally pure", is:
- **Easier to understand**: One clear place to see decision-making
- **Easier to test**: Isolated, composable rules
- **Easier to debug**: Rich instrumentation and metrics
- **Easier to tune**: Canonical steering parameters with known ranges
- **True to literature**: Follows Reynolds' proven formulations

**Current Status**: Core infrastructure and rule implementations exist. Seeded reproducibility,
perception semantics, force-budget enforcement, milestone 7 UX/test acceptance, readiness
milestones 8-10, multi-group support, and performance validation remain incomplete before Phase 3.

**Recommendation**: Follow epic #10's ordered correctness/fixture gates, then finish the remaining
milestone 7 UX and milestones 8-10, add multi-group support, and validate performance. Proceed
afterward with **gradual migration** (Option C) - port Phase 3 features to canonical implementation
while keeping the old implementation as reference. Once validated, remove the old implementation
in Phase 5.

The extra few weeks of work to do this right will pay dividends in developer velocity for Phases 3-6.
