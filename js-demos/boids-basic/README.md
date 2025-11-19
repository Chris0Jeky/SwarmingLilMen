# Boids Simulation - JavaScript Demo

A beautiful, interactive browser-based implementation of Craig Reynolds' Boids algorithm, demonstrating emergent flocking behavior from simple steering rules.

![Boids Demo](../../docs/boids-demo-screenshot.png)

## Features

- **Real-time steering behaviors**: Separation, Alignment, and Cohesion
- **Interactive controls**: Live parameter adjustment via sliders
- **Visual polish**: Motion trails, debug visualization, gradient backgrounds
- **Preset configurations**: Quick-load different behavioral patterns
- **Performance optimized**: Smooth 60 FPS with hundreds of boids
- **Click to spawn**: Add boids dynamically by clicking the canvas

## How to Run

Simply open `index.html` in any modern web browser:

```bash
# From the project root
open js-demos/boids-basic/index.html

# Or with a local server (recommended)
cd js-demos/boids-basic
python3 -m http.server 8000
# Then visit http://localhost:8000
```

No build tools, dependencies, or installation required!

## Controls

### Presets
- **Default**: Balanced flocking behavior
- **Chaotic**: Low cohesion, high speed, chaotic movement
- **Tight Flocks**: Strong alignment and cohesion, dense groups
- **Flowing**: Emphasis on alignment for smooth, flowing motion

### Parameters

#### Population
- **Boid Count**: Number of agents in the simulation (50-1000)

#### Steering Weights
- **Separation**: Strength of avoidance behavior (prevents crowding)
- **Alignment**: Tendency to match neighbor velocities (creates flow)
- **Cohesion**: Attraction to group center (keeps groups together)

#### Physics
- **Target Speed**: Constant speed maintained by all boids
- **Max Force**: Maximum steering force (affects turning agility)

#### Perception
- **Sense Radius**: How far boids can "see" neighbors
- **Separation Radius**: Distance threshold for separation behavior

#### Visualization
- **Motion Trails**: Fade effect showing boid paths
- **Debug Vectors**: Show perception radii and velocity vectors

### Interactions
- **Click canvas**: Spawn 10 new boids at cursor position
- **Reset**: Reinitialize simulation with current count

## Algorithm Overview

This implementation follows Reynolds' canonical steering behaviors:

### 1. Separation
Steer away from nearby neighbors to avoid crowding:
- Compute repulsion vector from each close neighbor
- Weight by inverse distance (closer = stronger)
- Linear falloff within separation radius

### 2. Alignment
Match velocity with nearby neighbors:
- Average the velocities of all neighbors in sense radius
- Steer toward that average heading

### 3. Cohesion
Move toward the center of mass of nearby neighbors:
- Compute average position of neighbors
- Steer toward that center point

### Integration
For each boid, each frame:
1. Query neighbors within sense radius
2. Compute steering forces from each rule
3. Sum all forces (separation + alignment + cohesion)
4. Update velocity with steering force
5. Normalize velocity to target speed (constant-speed model)
6. Update position
7. Wrap around screen edges (toroidal world)

## Implementation Details

### Vector Math
- Custom `Vec2` class for 2D vector operations
- Methods: add, subtract, multiply, normalize, limit, distance
- No external dependencies

### Boid Class
- Position and velocity vectors
- Acceleration accumulator (reset each frame)
- Three steering methods (separate, align, cohere)
- Update and draw methods

### Rendering
- HTML5 Canvas with 2D context
- Triangle representation pointing in velocity direction
- HSL color space for visual variety
- Trail effect using semi-transparent fills
- Debug overlay with circles and velocity vectors

### Performance
- O(n²) neighbor search (acceptable for <1000 boids)
- Single-threaded JavaScript
- ~60 FPS with 300-500 boids on modern hardware
- Could be optimized with spatial partitioning (future enhancement)

## Comparison to C# Implementation

This demo mirrors the canonical boids implementation in the main C# project (`SwarmSim.Core/Canonical/`):

| Feature | C# Implementation | JS Demo |
|---------|------------------|---------|
| Steering model | Reynolds steering | ✅ Same |
| Speed model | Constant speed | ✅ Same |
| Separation weighting | 1/d with falloff | ✅ Same |
| Alignment | Average neighbor velocity | ✅ Same |
| Cohesion | Center of mass | ✅ Same |
| Spatial optimization | Uniform grid (O(n)) | ❌ Naive (O(n²)) |
| FOV filtering | ✅ Cone-based | ❌ Omnidirectional |
| Multi-group | ✅ Supported | ❌ Single group |
| Combat/metabolism | ✅ Planned | ❌ Not applicable |

The JS demo prioritizes simplicity and educational clarity over the C# version's performance and feature completeness.

## Educational Use

This demo is excellent for:
- **Learning steering behaviors**: Adjust sliders to see how rules affect flocking
- **Algorithm visualization**: Enable debug mode to see perception radii and vectors
- **Quick prototyping**: Test parameter ranges before implementing in C#
- **Demonstrations**: Shareable link, no installation required

## Future Enhancements

Potential additions for future standalone demos:
- [ ] Spatial partitioning (uniform grid or quadtree) for better performance
- [ ] Field-of-view filtering (270° vision cone)
- [ ] Multiple groups with different colors
- [ ] Obstacle avoidance
- [ ] Mouse interaction (attract/repel)
- [ ] Export/import configuration JSON
- [ ] Performance profiler overlay
- [ ] WebGL renderer for 10k+ boids

## References

- [Craig Reynolds - Steering Behaviors](https://www.red3d.com/cwr/steer/)
- [Craig Reynolds - Boids](https://www.red3d.com/cwr/boids/)
- [The Nature of Code - Autonomous Agents](https://natureofcode.com/autonomous-agents/)

## License

Part of the SwarmingLilMen project. See main repository for license details.
