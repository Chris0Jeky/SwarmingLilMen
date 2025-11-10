# Dynamic Parameter Adjustment System

## Overview
The boids simulation now has a comprehensive dynamic parameter adjustment system that allows you to modify parameters in real-time while the simulation is running, making it much easier to understand how each parameter affects flocking behavior.

## Key Features

### 1. Real-Time Parameter Adjustment
- **Select Parameters**: Press number keys 1-7 to select which parameter to adjust
- **Adjust Values**: Use ↑/↓ arrows or +/- keys to increase/decrease the selected parameter
- **Fine Tuning**: Hold SHIFT while adjusting for smaller step sizes (fine adjustment mode)

### 2. Adjustable Parameters
| Key | Parameter | Range | Normal Step | Fine Step |
|-----|-----------|-------|-------------|-----------|
| 1 | Separation Weight | 0 - 500 | 10.0 | 0.01 |
| 2 | Alignment Weight | 0 - 500 | 10.0 | 0.01 |
| 3 | Cohesion Weight | 0 - 10 | 0.1 | 0.0001 |
| 4 | Separation Radius | 5 - 200 | 5.0 | 1.0 |
| 5 | Sense Radius | 10 - 300 | 10.0 | 2.0 |
| 6 | Max Speed | 1 - 500 | 10.0 | 1.0 |
| 7 | Friction | 0.8 - 1.0 | 0.01 | 0.001 |

### 3. Presets for Quick Testing
Press F1-F5 to instantly load preset configurations:

- **F1: Traditional Boids** - Low weights (0.05), small radii, slow speeds
- **F2: Original (High Forces)** - Very high weights (300/150/10), fast speeds
- **F3: Medium Forces** - Moderate weights (30/15/1), balanced settings
- **F4: Separation Only** - Only separation enabled, good for testing repulsion
- **F5: Alignment Only** - Only alignment enabled, good for testing velocity matching

### 4. Visual Feedback
- **Left Panel**: Shows all parameters with the selected one highlighted
- **Right Panel**: Displays current metrics (max force, average speed)
- **Console Output**: Prints parameter changes and values
- **On-Screen Indicators**: Shows when in fine adjustment mode

### 5. Additional Controls
- **P**: Print all current parameters to console
- **R**: Reset world with current parameters
- **V/S/N**: Toggle velocity vectors, sense radius, neighbor connections
- **X**: Shake agents to break equilibrium
- **C**: Export CSV snapshot

## How It Works

### Implementation Details
1. **Immutable Config**: SimConfig uses init-only properties for thread safety
2. **World Recreation**: When parameters change, the world is recreated with new config
3. **Agent Preservation**: Agent positions and velocities are preserved during recreation
4. **Instant Feedback**: Changes apply immediately on the next simulation tick

### Technical Architecture
```csharp
// Parameter values stored as mutable fields
private static float _separationWeight = 0.05f;
private static float _alignmentWeight = 0.05f;
// ... etc

// When parameter changes:
1. Update the field value
2. Create new SimConfig with updated values
3. Create new World with new config
4. Copy agent states from old world
5. Continue simulation with new parameters
```

## Testing the Boids Fix

With this dynamic system, you can now easily test different theories about why the boids aren't flocking properly:

1. **Start with Traditional Boids (F1)**
   - Should see smooth flocking if the algorithm is correct

2. **Try Original High Forces (F2)**
   - Will likely show the clustering/equilibrium problem

3. **Adjust Parameters Individually**
   - Press 1, then use arrows to change separation weight
   - See immediate effect on agent behavior
   - Try setting separation to 0 to see pure alignment/cohesion

4. **Test Force Clamping Theory**
   - With high weights, forces may be clamped
   - With low weights, forces stay within reasonable ranges

5. **Find Your Sweet Spot**
   - Experiment with different combinations
   - When you find good settings, press P to print them

## Running the System

```bash
dotnet run --project SwarmSim.Render
```

The simulation will start with traditional boids parameters. Use the controls above to experiment!

## Debugging Tips

1. **If agents cluster**: Increase separation weight or radius
2. **If agents scatter**: Decrease separation, increase cohesion
3. **If no movement**: Check friction isn't too high (should be <1.0)
4. **If chaotic**: Reduce max speed or increase friction
5. **Use X to shake**: Breaks equilibrium states for testing

## Next Steps

The dynamic parameter system makes it easy to:
- Find optimal parameters for realistic flocking
- Debug issues with the boids implementation
- Test different flocking behaviors
- Create interesting emergent patterns

The system preserves agent states during parameter changes, so you can adjust settings without disrupting the simulation flow.