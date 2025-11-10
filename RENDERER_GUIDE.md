# SwarmingLilMen Renderer Guide

## What You're Seeing

The renderer shows **800 agents** (200 per group) performing **boids flocking behavior**:

### The 4 Groups (Colors)
- **White** - Group 0
- **Red** - Group 1
- **Green** - Group 2
- **Blue** - Group 3

**Important**: Each group only flocks with its own color. Different colored agents ignore each other (this is by design).

## Expected Behavior

### What You Should See:
1. **Initial Movement**: Agents start with random velocities (50-100 units/sec)
2. **Flocking Emerges**: Within seconds, each colored group forms cohesive flocks
3. **Behaviors**:
   - **Separation**: Agents maintain personal space from nearby neighbors
   - **Alignment**: Agents match the velocity of nearby neighbors
   - **Cohesion**: Agents are attracted to the center of nearby neighbors
4. **Group Movement**: Flocks will swirl, merge, split, and flow around the screen
5. **Wrapping**: When agents reach screen edges, they wrap to the opposite side

### Visualization Options (Press Keys to Toggle):
- **V** - Velocity vectors (ON by default): Shows direction and speed of movement
- **S** - Sense radius circles: Shows how far each agent can "see" (80 pixels)
- **N** - Neighbor connections: Draws lines between nearby agents in the same group

### What Makes It Dynamic:
- **MaxSpeed**: 300 pixels/sec
- **SenseRadius**: 120 pixels (larger interaction range)
- **Friction**: 0.95 (less friction = more sustained movement)
- **Initial velocity**: 100-200 pixels/sec (agents start fast)
- **Stronger Forces**: All boids weights doubled for more dramatic flocking

## Interaction Controls

- **Left Click**: Spawn 50 white agents at mouse position
- **Right Click**: Spawn 50 red agents at mouse position
- **Space**: Spawn 100 random agents
- **R**: Reset to initial state (800 agents in 4 groups)
- **ESC**: Quit

## Understanding the Flocking

Each agent follows three simple rules:

1. **Separation (weight 1.5)**:
   - Repels from agents within 25 pixels
   - Force is 1/r² (inverse square, like physics)
   - Prevents overcrowding

2. **Alignment (weight 1.0)**:
   - Steers toward the average velocity of neighbors
   - Creates coordinated group movement
   - Makes flocks flow together

3. **Cohesion (weight 0.8)**:
   - Attracts toward center of mass of neighbors
   - Keeps groups from spreading apart
   - Creates the "flocking" effect

## Performance Notes

- **FPS**: Should be 60 (or close) with 800 agents
- **Avg Speed**: Will vary as agents accelerate/decelerate, typically 100-200
- The simulation runs at 120 Hz internally, renders at 60 FPS

## Troubleshooting

**"Not much is happening"**:
- Make sure velocity vectors are ON (press V if gray)
- Watch for several seconds - flocking takes time to emerge
- Groups may be moving slowly; press R to reset with new velocities

**"Agents not flocking"**:
- Each color only flocks with itself
- Agents need to be within 80 pixels (SenseRadius) to interact
- Press N to see neighbor connections

**"Too slow"**:
- This is normal for the initial convergence phase
- After a few seconds, flocks become more dynamic
- Try spawning more agents with Space for chaos

## What's Next

This is Phase 2 - basic boids flocking. Future phases will add:
- **Phase 3**: Combat between groups, energy/metabolism, death
- **Phase 4**: Reproduction and evolution
- **Phase 5**: Performance optimizations (SIMD, parallelization) for 50k+ agents

---

**Enjoy watching the emergent behavior!** Each group operates on simple local rules, but creates complex global patterns.
