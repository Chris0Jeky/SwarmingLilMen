# Boids Debugging Approach

## Problem Summary
The boids simulation shows **binary behavior**: agents either instantly hit maximum speed or don't move at all. There's no gradual acceleration, indicating a fundamental physics integration issue.

## Debugging Strategy

### 1. Minimal Test Program
Run the minimal test to isolate issues:
```bash
dotnet run --project SwarmSim.Render -- --minimal
```

This provides 9 progressive test stages:
- **Stage 0**: Two agents, no forces (baseline)
- **Stage 1**: Two agents, manual constant force
- **Stage 2**: Two agents, separation only
- **Stage 3**: Three agents, separation
- **Stage 4**: Three agents, alignment
- **Stage 5**: Three agents, cohesion
- **Stage 6**: Three agents, all forces
- **Stage 7**: Ten agents, all forces
- **Stage 8**: Gradual spawn (adds 10 every 2 seconds)

### Controls:
- **SPACE**: Advance to next stage
- **R**: Restart current stage
- **D**: Detailed debug output
- **0-8**: Jump to specific stage

### 2. What to Look For

#### Stage 0: No Forces
- Agents should maintain initial velocity
- Friction should gradually slow them down
- No sudden stops or accelerations

#### Stage 1: Manual Force
- Constant force of 5.0 applied to agent 0
- Should see gradual acceleration
- Speed should increase smoothly until hitting max

#### Stage 2-5: Individual Forces
- Test each boids component in isolation
- Forces should produce smooth, predictable motion
- No binary on/off behavior

### 3. Key Metrics to Monitor

1. **Force Magnitude**: Should be reasonable (1-100, not 30,000)
2. **Velocity Change**: Should be gradual (0.1-1.0 per tick)
3. **Speed**: Should vary smoothly from 0 to max
4. **Friction Effect**: Should be visible but not dominant

### 4. Diagnostic Output

The minimal test shows:
- **Green arrows**: Velocity vectors
- **Red arrows**: Force vectors
- **Yellow agent**: Agent 0 (for tracking)
- **Real-time stats**: Speed, force, position

### 5. Expected Behavior vs Current

**Expected**:
- Smooth acceleration from forces
- Gradual speed changes
- Natural flocking patterns
- Speed range: 0 to max with all values between

**Currently Seeing**:
- Binary speed (0 or max)
- Massive forces (30,000+) with no effect
- Tiny forces (0.5) produce no movement
- Static equilibrium in all configurations

## Root Cause Analysis

### Scale Mismatch Theory
The system has incompatible scales:
- **Forces**: 0.5 to 45,000 (90,000x range!)
- **Timestep**: 0.0167 (1/60 second)
- **Max Speed**: 6 to 200
- **Friction**: 0.92 to 0.99

### Math Breakdown
With dt = 1/60:
```
Large force: 45,000 * 0.0167 = 750 velocity/tick → instant max speed
Small force: 0.5 * 0.0167 = 0.008 velocity/tick → eaten by friction
```

There's no middle ground where forces produce reasonable acceleration.

## Potential Fixes

### Option 1: Scale Forces Properly
Forces should be in range where `force * dt` = 0.5 to 5.0 velocity change
- For dt=1/60, forces should be 30-300
- Not 0.5 or 45,000

### Option 2: Adjust Timestep
Larger timestep allows smaller forces to have effect:
```csharp
FixedDeltaTime = 1f / 30f;  // Double the timestep
```

### Option 3: Fix Force Calculation
The 1/r² separation force explodes near zero:
```csharp
// Current (explodes)
float invDistSq = 1f / distSq;  // Can be 10,000+

// Better (bounded)
float force = Math.Max(1f, separationRadius - distance);
```

### Option 4: Remove Speed Clamping
Current clamping is too aggressive:
```csharp
// Currently clamps immediately to max
if (speed > maxSpeed) {
    // This creates binary behavior
}

// Better: gradual limiting
velocity *= 0.95f; // Soft limit
```

## Next Steps

1. **Run Minimal Test Stage 1**
   - See if manual force produces gradual acceleration
   - If not, integration is broken

2. **Check Force Magnitudes**
   - Print raw forces before integration
   - Should be 1-100, not thousands

3. **Verify Integration Math**
   - Check dt value (should be 0.0167)
   - Verify friction application
   - Check speed clamping logic

4. **Test Parameter Ranges**
   - Find working force range for smooth motion
   - Adjust weights/radii accordingly

## Success Criteria

The simulation works correctly when:
1. Agents accelerate gradually from rest
2. Speed varies continuously from 0 to max
3. Forces in range 10-500 produce visible motion
4. Flocking patterns emerge naturally
5. No static equilibrium states

## Running the Tests

```bash
# Run minimal test
dotnet run --project SwarmSim.Render -- --minimal

# Run full simulation with dynamic parameters
dotnet run --project SwarmSim.Render

# Key combinations to test:
# F1: Traditional Boids (should work if algorithm correct)
# F2: Original High Forces (shows the problem)
# F3: Medium Forces (compromise)
```

The minimal test is the key diagnostic tool. Start there and work up in complexity.