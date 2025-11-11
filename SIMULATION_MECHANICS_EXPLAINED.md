# Simulation Mechanics Explained

## Table of Contents
1. [Physics Integration Pipeline](#physics-integration-pipeline)
2. [Forces Explained](#forces-explained)
3. [Parameters Reference](#parameters-reference)
4. [Configuration Examples](#configuration-examples)
5. [Parameter Interactions](#parameter-interactions)
6. [Troubleshooting Guide](#troubleshooting-guide)

---

## Physics Integration Pipeline

The simulation runs at 60 FPS (frames per second) with a fixed timestep. Each frame, every agent goes through this pipeline:

### 1. Force Accumulation Phase
```
Forces start at (0, 0) each frame
↓
Systems add forces:
- BehaviorSystem adds: separation + alignment + cohesion
- WanderSystem adds: random exploration force
↓
Total force = sum of all system forces
```

### 2. Integration Phase (IntegrateSystem)
```csharp
// Step 1: Add force to velocity (F = ma, assuming mass = 1)
velocity += force * dt

// Step 2: Apply friction (velocity damping)
velocity *= friction

// Step 3: Clamp to maximum speed
if (speed > maxSpeed) {
    velocity = normalize(velocity) * maxSpeed
}

// Step 4: Update position
position += velocity * dt
```

### Key Insight: **Timestep (dt)**
- dt = 1/60 = 0.0167 seconds
- This is the "integration step" - how much real time passes per frame
- Forces are multiplied by dt, so a force of 100 adds `100 × 0.0167 = 1.67` to velocity per frame
- **Smaller dt = smoother motion but forces have less impact per frame**
- **Larger dt = choppier motion but forces have more impact per frame**

---

## Forces Explained

Forces are vectors (have direction and magnitude) that pull/push agents in specific directions.

### 1. Separation Force (Repulsion)

**Purpose**: Prevent agents from overlapping - personal space

**How it works**:
```
For each neighbor within separationRadius:
  1. Calculate direction away from neighbor
  2. Calculate repulsion strength based on distance:
     strength = (separationRadius - distance) / separationRadius
     → Closer = stronger repulsion (max 1.0 at distance 0)
     → Further = weaker repulsion (0.0 at separationRadius)
  3. Add weighted force: separation += direction * strength * separationWeight
```

**Parameter**: `SeparationWeight`
- **Low (1.0)**: Gentle avoidance, agents can get close
- **Medium (5.0)**: Clear personal space maintained
- **High (20.0)**: Strong repulsion, agents scatter aggressively

**Parameter**: `SeparationRadius`
- Distance at which agents start pushing each other away
- **Small (15)**: Agents only avoid when very close (tight flocks)
- **Large (50)**: Agents maintain large personal bubbles (spread out)

**Visual Effect**:
- Too low: Agents clump into tight balls
- Too high: Agents scatter, never form groups
- Balanced: Dynamic spacing with natural flow

---

### 2. Alignment Force (Velocity Matching)

**Purpose**: Match velocity with nearby agents - synchronized movement

**How it works**:
```
For each neighbor within senseRadius:
  1. Sum up all neighbor velocities
  2. Calculate average velocity of group
  3. Calculate desired velocity change:
     desired = averageVelocity - myVelocity
  4. Add weighted force: alignment += desired * alignmentWeight
```

**Parameter**: `AlignmentWeight`
- **Low (0.5)**: Loose synchronization, independent movement
- **Medium (2.5)**: Good coordination, flowing together
- **High (10.0)**: Tight synchronization, moves as single unit

**Visual Effect**:
- Too low: Chaotic, each agent goes its own way
- Too high: Rigid, loses natural variation
- Balanced: Smooth flowing streams, coordinated turns

---

### 3. Cohesion Force (Attraction to Center)

**Purpose**: Pull agents toward the center of their local group - group formation

**How it works**:
```
For each neighbor within senseRadius:
  1. Calculate center of mass (average position) of all neighbors
  2. Calculate direction toward center: centerOfMass - myPosition
  3. Add weighted force: cohesion += direction * cohesionWeight
```

**Parameter**: `CohesionWeight`
- **Low (0.1)**: Weak attraction, loose groups
- **Medium (0.5)**: Clear grouping tendency
- **High (5.0)**: Strong attraction, tight clusters

**Visual Effect**:
- Too low: Agents wander apart, no group formation
- Too high: Agents collapse into tight balls
- Balanced: Distinct groups that flow together

---

### 4. Wander Force (Random Exploration)

**Purpose**: Add unpredictability and exploration behavior

**How it works**:
```
Each frame:
  1. Generate random angle
  2. Calculate force in that direction
  3. Scale by wanderStrength
```

**Parameter**: `WanderStrength`
- **0.0**: No randomness, purely deterministic flocking
- **0.5**: Slight jitter, natural variation
- **5.0**: Chaotic, random walk dominates

**Visual Effect**:
- Zero: Predictable, can feel mechanical
- Low: Natural variation, organic feel
- High: Erratic, loses flocking structure

---

## Parameters Reference

### Physics Parameters

#### **Friction** (velocity damping)
```
velocity = velocity * friction (each frame)
```

- **Value Range**: 0.0 to 1.0
- **Meaning**: What fraction of velocity is retained each frame

| Friction | Effect | Velocity Per Second | Use Case |
|----------|--------|---------------------|----------|
| 1.0 | No damping | 100% retained | Frictionless space, momentum preserved |
| 0.99 | Very light | 54.7% retained (lose 45%) | Gentle drag |
| 0.95 | Light | 8% retained (lose 92%) | Responsive steering |
| 0.90 | Medium | 0.1% retained (lose 99.9%) | High drag, slow motion |
| 0.85 | Heavy | ~0% retained | Extreme drag, forces must constantly push |

**Key Formula**: After 1 second (60 frames), velocity is multiplied by `friction^60`

**Examples**:
- **Friction = 1.0 (You)**: Agent accelerates once and coasts forever (no energy loss)
  - Good for: Space-like environments, long flowing movements
  - Bad for: Precise control, stopping behavior

- **Friction = 0.99 (Too High)**: Agent loses almost all velocity every second, forces barely overcome drag
  - Result: Near-static, microscopic motion (the original bug!)

- **Friction = 0.95 (Balanced)**: Agent has momentum but forces can steer effectively
  - Good for: Responsive flocking with smooth motion

#### **MaxSpeed** (velocity clamp)
```
if (|velocity| > maxSpeed) {
    velocity = normalize(velocity) * maxSpeed
}
```

- **Value Range**: Any positive number (typically 5-20)
- **Meaning**: Maximum velocity magnitude (pixels per frame)

| MaxSpeed | Pixels/Frame | Pixels/Second | Effect |
|----------|--------------|---------------|--------|
| 5 | 5 | 300 | Slow, gentle motion |
| 10 | 10 | 600 | Moderate, natural pace |
| 20 | 20 | 1200 | Fast, dynamic |
| 50 | 50 | 3000 | Very fast, chaotic |

**Impact**:
- Too low: Sluggish, can't escape danger or explore quickly
- Too high: Agents zip around, hard to form stable patterns
- Balanced: Smooth motion with visible dynamics

#### **Timestep (dt)**
- **Fixed at**: 1/60 = 0.0167 seconds
- **Impact**: Determines how much forces affect velocity per frame
  - `velocity += force * dt`
  - Larger dt = forces have bigger impact
  - Smaller dt = smoother but forces need to be larger

---

### Force Weight Parameters

These scale the strength of each force type:

| Parameter | Typical Range | Effect at Low | Effect at High |
|-----------|---------------|---------------|----------------|
| **SeparationWeight** | 1.0 - 20.0 | Agents clump together | Agents scatter apart |
| **AlignmentWeight** | 0.5 - 10.0 | Independent motion | Synchronized movement |
| **CohesionWeight** | 0.1 - 5.0 | Loose groups | Tight clusters |
| **WanderStrength** | 0.0 - 5.0 | Deterministic | Chaotic exploration |

---

### Spatial Parameters

#### **SeparationRadius**
- Distance at which separation force activates
- **Small (15)**: Only avoid when very close → tight packing possible
- **Large (50)**: Avoid from far away → spread out groups

#### **SenseRadius**
- Distance at which agents detect neighbors for alignment/cohesion
- **Small (50)**: Only aware of very close neighbors → small groups
- **Large (150)**: Aware of distant neighbors → large coordinated groups

**Key Relationship**:
- `SeparationRadius < SenseRadius` is typical
- Example: SenseRadius = 100, SeparationRadius = 30
  - Agents see neighbors from 100 units away (start moving toward them)
  - But push away if they get within 30 units (maintain spacing)
  - Result: Groups with internal structure

---

## Configuration Examples

### Example 1: Frictionless Space (Your Current Setup)
```csharp
Friction = 1.0f               // No velocity loss
MaxSpeed = 10f                // Moderate speed cap
SeparationWeight = 5.0f       // Clear personal space
AlignmentWeight = 2.5f        // Good coordination
CohesionWeight = 0.5f         // Moderate grouping
SeparationRadius = 30f
SenseRadius = 100f
```

**Expected Behavior**:
- Agents accelerate until hitting maxSpeed (10)
- Once at maxSpeed, maintain momentum indefinitely
- Forces only change direction, not speed (since speed is maxed and no friction slows them)
- Smooth, flowing patterns like schools of fish in water
- Groups orbit and flow without stopping

**Visual**: Continuous motion, agents at nearly constant speed, graceful curves and turns

---

### Example 2: Tight Flocking (Bird-like)
```csharp
Friction = 0.98f              // Light drag
MaxSpeed = 15f                // Faster movement
SeparationWeight = 3.0f       // Allow closer approach
AlignmentWeight = 8.0f        // Strong synchronization
CohesionWeight = 2.0f         // Strong grouping
SeparationRadius = 20f        // Small personal space
SenseRadius = 120f            // Large awareness
```

**Expected Behavior**:
- Agents form tight, coordinated groups
- Strong alignment = synchronized turns, moves as unit
- Light friction = can accelerate to high speeds
- Small separation radius = dense packing
- Like starling murmurations or bird flocks

**Visual**: Dense, synchronized clouds that turn and flow together

---

### Example 3: Loose Swarms (Insect-like)
```csharp
Friction = 0.92f              // Medium drag
MaxSpeed = 8f                 // Slower movement
SeparationWeight = 10.0f      // Strong personal space
AlignmentWeight = 1.0f        // Weak synchronization
CohesionWeight = 0.3f         // Weak grouping
SeparationRadius = 40f        // Large personal space
SenseRadius = 80f             // Moderate awareness
WanderStrength = 2.0f         // High randomness
```

**Expected Behavior**:
- Agents spread out (high separation)
- Independent motion (low alignment)
- Loose groups (low cohesion, high wander)
- Medium friction = slows down naturally
- Like flies or gnats

**Visual**: Spread out cloud, each agent doing its own thing, gentle attraction to group

---

### Example 4: Chaotic Bouncing (Particle-like)
```csharp
Friction = 0.85f              // Heavy drag
MaxSpeed = 25f                // High speed cap
SeparationWeight = 15.0f      // Strong repulsion
AlignmentWeight = 0.2f        // Almost no sync
CohesionWeight = 0.05f        // Almost no grouping
SeparationRadius = 50f        // Large avoidance
WanderStrength = 5.0f         // Maximum chaos
```

**Expected Behavior**:
- Agents scatter and bounce off each other
- Heavy friction = must constantly accelerate
- High wander = unpredictable paths
- Strong separation = repulsion dominates
- Like gas particles or bouncing balls

**Visual**: Chaotic scattering, constant motion, no stable patterns

---

### Example 5: Slow Orbiting Groups
```csharp
Friction = 0.96f              // Light-medium drag
MaxSpeed = 6f                 // Slow speed
SeparationWeight = 4.0f       // Moderate spacing
AlignmentWeight = 5.0f        // Strong sync
CohesionWeight = 3.0f         // Strong attraction
SeparationRadius = 25f
SenseRadius = 100f
WanderStrength = 0.1f         // Minimal randomness
```

**Expected Behavior**:
- Agents form distinct groups
- Strong cohesion + strong alignment = orbiting behavior
- Groups circle their own center of mass
- Light friction = smooth, slow motion
- Like planets orbiting or whirlpools

**Visual**: Circular/spiral patterns, stable rotating groups

---

## Parameter Interactions

### The Friction-Force Dance

**At Equilibrium** (when acceleration = deceleration):
```
Force added per frame = Velocity lost per frame
force * dt = velocity * (1 - friction)

Solving for velocity:
velocity_equilibrium = (force * dt) / (1 - friction)
```

**Example with Friction = 1.0 (Frictionless)**:
```
velocity_equilibrium = (force * dt) / (1 - 1.0)
                     = (force * dt) / 0
                     = undefined (infinite!)
```
**This means**: Without friction, agents accelerate forever until hitting maxSpeed

**Example with Friction = 0.95**:
```
velocity_equilibrium = (5.0 * 0.0167) / (1 - 0.95)
                     = 0.0835 / 0.05
                     = 1.67
```
**This means**: With friction 0.95 and force 5.0, agents stabilize at speed ~1.67

**Example with Friction = 0.99 (Original Bug)**:
```
velocity_equilibrium = (5.0 * 0.0167) / (1 - 0.99)
                     = 0.0835 / 0.01
                     = 0.835
```
**This means**: With friction 0.99, agents barely move (equilibrium at 0.835)

---

### Separation vs Cohesion Tug-of-War

- **Separation pulls apart**, cohesion pulls together
- Balance determines group density

| Separation/Cohesion Ratio | Result |
|---------------------------|--------|
| 50:1 (e.g., 10.0 / 0.2) | Scattered individuals |
| 10:1 (e.g., 5.0 / 0.5) | Loose groups with spacing |
| 5:1 (e.g., 5.0 / 1.0) | Balanced flocks |
| 2:1 (e.g., 4.0 / 2.0) | Dense clusters |
| 1:1 (e.g., 3.0 / 3.0) | Unstable, oscillating |

**Rule of Thumb**: Separation should be 3-10x stronger than cohesion for stable flocking

---

### Alignment + Cohesion = Orbiting

When both are strong, groups develop circular motion:
1. Cohesion pulls agents toward center
2. Alignment makes them match velocity
3. Combined: Everyone orbits the center at similar speed
4. Result: Spiral or circular patterns

**How to create orbiting**:
- High alignment (8.0+)
- High cohesion (2.0+)
- Moderate separation (3.0-5.0)
- Low friction (0.95-0.98)

---

### SenseRadius vs SeparationRadius

**Key Insight**: SenseRadius should be larger than SeparationRadius

```
Agent's awareness zones:
|-------|-------------|
0       30           100
        ↑            ↑
   Separation    Sense
```

**With SenseRadius = 100, SeparationRadius = 30**:
- From 100 → 30 units: "I see you, let's coordinate and approach"
- From 30 → 0 units: "Too close! Push away"
- Result: Groups with internal structure

**If SeparationRadius > SenseRadius** (inverted):
- Agents push away before they can coordinate
- Result: Scattering, no groups form

---

### MaxSpeed as a Damper

MaxSpeed acts as a "ceiling" for all motion:

**With Friction = 1.0 (Your Setup)**:
- Agents accelerate until hitting maxSpeed
- Once there, forces can only change direction, not speed
- Result: Most agents move at exactly maxSpeed (constant velocity)

**With Friction < 1.0**:
- Equilibrium velocity might be below maxSpeed
- Agents move at equilibrium speed, not max
- Result: Variable speeds based on local forces

**Tuning Tip**:
- If agents all move at maxSpeed: Forces are too strong OR friction is too low
- If agents barely move: Forces are too weak OR friction is too high

---

## Troubleshooting Guide

### Problem: Agents Not Moving

**Symptoms**: Agents frozen or "giggling" in place

**Causes**:
1. **Friction too high** (e.g., 0.99)
   - Fix: Reduce to 0.95 or lower

2. **Forces too weak** (e.g., weights all < 1.0)
   - Fix: Increase all weights 5-10x

3. **MaxSpeed too low** (e.g., 0.5)
   - Fix: Increase to 10+

**Debug**: Check average speed
- Should be 20-80% of maxSpeed
- If < 5%, forces or friction problem

---

### Problem: Agents Move Too Fast

**Symptoms**: Agents zipping around, can't see behavior

**Causes**:
1. **MaxSpeed too high** (e.g., 100)
   - Fix: Reduce to 10-20

2. **Forces too strong** with low friction
   - Fix: Reduce weights or increase friction

**Debug**: Check if most agents at maxSpeed
- If yes: Hitting speed cap, need better balance

---

### Problem: Agents Clump Into Ball

**Symptoms**: All agents collapse to single point

**Causes**:
1. **Separation too weak** compared to cohesion
   - Fix: Increase separationWeight 5-10x

2. **SeparationRadius too small**
   - Fix: Increase to 30-50

**Debug**: Check neighbor counts
- If agents have 10+ neighbors constantly: Too dense

---

### Problem: Agents Scatter Everywhere

**Symptoms**: No groups form, everyone goes alone

**Causes**:
1. **Separation too strong** compared to cohesion
   - Fix: Decrease separationWeight or increase cohesion

2. **Cohesion/alignment too weak**
   - Fix: Increase both by 2-5x

3. **Wander too strong**
   - Fix: Reduce wanderStrength below 1.0

---

### Problem: No Visible Flocking Patterns

**Symptoms**: Random motion, no coordination

**Causes**:
1. **Alignment too weak**
   - Fix: Increase alignmentWeight to 5.0+

2. **SenseRadius too small**
   - Fix: Increase to 100+

3. **Too much wander**
   - Fix: Reduce wanderStrength

**Debug**: Watch if agents ever move in same direction
- If no: Increase alignment dramatically

---

## Quick Reference: Good Starting Values

### Balanced Flocking (Recommended)
```csharp
Friction = 0.95f
MaxSpeed = 10f
SeparationWeight = 5.0f
AlignmentWeight = 2.5f
CohesionWeight = 0.5f
SeparationRadius = 30f
SenseRadius = 100f
WanderStrength = 0.5f
```

### Frictionless (Your Current Style)
```csharp
Friction = 1.0f
MaxSpeed = 10f
SeparationWeight = 5.0f
AlignmentWeight = 2.5f
CohesionWeight = 0.5f
SeparationRadius = 30f
SenseRadius = 100f
WanderStrength = 0.5f
```

### Experimentation Tips

1. **Start with one force at a time**:
   - Separation only: See avoidance
   - Alignment only: See synchronization
   - Cohesion only: See grouping
   - Then combine

2. **Use ratios, not absolutes**:
   - Separation:Cohesion = 10:1 (loose)
   - Separation:Cohesion = 5:1 (balanced)
   - Separation:Cohesion = 2:1 (tight)

3. **Friction controls motion style**:
   - 1.0 = Spaceship (frictionless)
   - 0.98 = Bird (light drag)
   - 0.95 = Fish (medium drag)
   - 0.90 = Underwater (heavy drag)

4. **MaxSpeed sets the pace**:
   - 5 = Slow and meditative
   - 10 = Natural pace
   - 20 = Fast and dynamic
   - 50 = Chaotic blur

---

## Advanced: Friction = 1.0 Behavior

**With friction = 1.0**, the simulation becomes **Newtonian** (no energy loss):

1. **Agents accelerate to maxSpeed quickly**
   - Once there, forces can't increase speed (clamped)
   - Forces only change direction

2. **Constant kinetic energy**
   - Like objects in space or frictionless surface
   - Motion continues indefinitely

3. **Different flocking dynamics**:
   - Traditional boids assumes friction (energy loss)
   - With friction = 1.0, you need different tuning:
     - Lower force weights (agents stay at maxSpeed longer)
     - Focus on directional forces, not magnitude

4. **Expected patterns**:
   - Smooth flowing streams
   - Less stopping and starting
   - More orbital/circular patterns
   - Agents rarely below 80% maxSpeed

**Tuning for Friction = 1.0**:
- Use moderate weights (2.0-5.0 range)
- MaxSpeed becomes the main speed control
- Increase SenseRadius (agents need more lookahead)
- Reduce WanderStrength (randomness has lasting impact)

---

## Summary

The simulation is a **force-based physics system** where:

1. **Each frame**:
   - Forces are calculated (separation, alignment, cohesion, wander)
   - Forces are integrated into velocity (`v += f * dt`)
   - Friction damps velocity (`v *= friction`)
   - Speed is clamped (`if |v| > maxSpeed`)
   - Position is updated (`pos += v * dt`)

2. **Key insight**: Equilibrium between forces and friction
   - Friction = 1.0: No equilibrium, accelerate to maxSpeed
   - Friction < 1.0: Equilibrium at `v = (f * dt) / (1 - friction)`

3. **Tuning philosophy**:
   - Start with friction (sets motion style)
   - Set maxSpeed (sets pace)
   - Balance forces (sets behavior)
   - Adjust radii (sets scale)

4. **Visual goals**:
   - Clear motion (agents visibly moving)
   - Distinct behavior (can identify forces in action)
   - Stable patterns (groups form and persist)
   - Natural feel (not robotic or chaotic)

Experiment, observe, iterate. Every configuration creates different emergent patterns!
