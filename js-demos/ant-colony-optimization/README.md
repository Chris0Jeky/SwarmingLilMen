# Ant Colony Optimization (ACO)

An interactive browser-based demonstration of **Ant Colony Optimization** (Dorigo, 1992), showcasing stigmergy-based pathfinding and emergent route optimization through pheromone trails.

## Features

- **Interactive environment building**: Click to place food, drag to draw walls
- **Real-time pheromone visualization**: Watch trails form and evaporate
- **Emergent path optimization**: See ants converge to shortest routes
- **Multiple interaction modes**: Place food, draw walls, erase, or just watch
- **Adjustable parameters**: Colony size, pheromone strength, evaporation rate
- **Live statistics**: Track food collected, ants carrying food, max pheromone
- **Beautiful visualization**: Glowing pheromone trails, animated ants

## What is Ant Colony Optimization?

ACO is a **swarm intelligence algorithm** inspired by the foraging behavior of real ants. It demonstrates how simple agents following local rules can collectively solve complex optimization problems through **stigmergy** - indirect communication via environmental modification.

### The Core Mechanism

Real ants deposit pheromone (a chemical substance) as they walk. Other ants can detect and tend to follow pheromone trails. The key insight:

**Shorter paths get reinforced faster!**

1. Ants exploring randomly find food
2. Successful ants deposit pheromone on their return journey
3. Pheromone evaporates over time
4. Shorter paths accumulate pheromone faster (more round trips)
5. More ants follow stronger trails → positive feedback
6. System converges to near-optimal route

This is **emergent optimization** - no ant knows the "best path", but the colony as a whole discovers it.

## How to Run

Simply open `index.html` in any modern web browser:

```bash
# From the project root
open js-demos/ant-colony-optimization/index.html

# Or with a local server
cd js-demos/ant-colony-optimization
python3 -m http.server 8000
# Visit http://localhost:8000
```

No build tools, dependencies, or installation required!

## How to Use

### Modes

**Place Food** (default)
- Click anywhere to place food sources
- Ants will search for food and bring it back to nest
- Try placing food at different distances to see path optimization

**Draw Walls**
- Click and drag to draw obstacles
- Forces ants to find alternative routes
- Create mazes to test pathfinding capabilities

**Erase**
- Click and drag to remove food sources or walls
- Clean up your environment

**Watch**
- Non-interactive mode
- Observe the colony without accidentally modifying

### Controls

#### Colony
- **Ant Count**: Number of active foragers (10-200)
- **Ant Speed**: How fast ants move (affects convergence time)

#### Pheromone
- **Deposit Amount**: Pheromone strength left by successful ants
  - Higher → stronger trails, faster convergence
  - Lower → weaker trails, more exploration
- **Evaporation Rate**: How quickly pheromone fades (0.001-0.1)
  - Higher → trails disappear faster, more dynamic
  - Lower → trails persist longer, more exploitation
- **Exploration (α)**: Pheromone importance in decision-making
  - Higher → ants follow trails more strictly
  - Lower → more random exploration

#### Actions
- **Clear Pheromones**: Reset all trails (keeps food/walls)
- **Clear Walls**: Remove all obstacles
- **Clear Food**: Remove all food sources
- **Reset All**: Fresh start

## The Algorithm

### Ant Behavior State Machine

Each ant operates in one of two states:

**Searching for Food:**
```
1. Sense pheromone in three directions (left, forward, right)
2. If pheromone detected:
     Turn toward strongest trail (with some randomness)
   Else:
     Random walk
3. Move forward
4. If food source reached → switch to "Returning" state
```

**Returning to Nest:**
```
1. Record current position in path
2. Head toward nest (simple homing)
3. If nest reached:
     Deposit pheromone along recorded path
     Switch to "Searching" state
```

### Pheromone Dynamics

**Deposition:**
```javascript
// When ant returns with food
for (position in ant.path) {
    grid[position] += pheromoneDeposit
}
```

**Evaporation:**
```javascript
// Every frame
for (cell in grid) {
    cell.pheromone *= (1 - evaporationRate)
}
```

### Probabilistic Path Selection

Ants don't deterministically follow trails - they use probabilistic decision making:

```javascript
// Simplified version
probability(direction) ∝ pheromone(direction)^α

where α controls exploration vs exploitation
```

### Implementation Details

- **Grid-based pheromone**: 5×5 pixel cells for efficient storage/rendering
- **Three-sensor model**: Left, forward, right sensors for direction
- **Path recording**: Ants remember their route for pheromone deposition
- **Wall avoidance**: Ants bounce off obstacles
- **Stuck detection**: Teleport home if stuck too long (prevents deadlocks)

## Stigmergy: Indirect Communication

The key concept in ACO is **stigmergy** - communication through environmental modification:

| Direct Communication | Stigmergy (ACO) |
|---------------------|-----------------|
| Agents talk to each other | Agents modify environment |
| Requires proximity | Works asynchronously |
| Limited by bandwidth | Scalable to many agents |
| Example: Boids alignment | Example: Pheromone trails |

**Advantages of stigmergy:**
- **Decentralized**: No leader, no global knowledge required
- **Robust**: System works even if individual ants fail
- **Adaptive**: Trails adjust to environmental changes
- **Scalable**: Adding more ants improves solution quality

## Emergent Phenomena

Watch for these patterns as you experiment:

### 1. Shortest Path Convergence
Place food source, wait 30-60 seconds:
- Initial chaotic exploration
- Multiple paths emerge
- Weaker paths fade
- Strongest (usually shortest) path dominates

### 2. Multi-Path Stability
With multiple food sources:
- Each source develops its own trail network
- Trails can cross without interference
- Ants can switch between food sources

### 3. Adaptation to Obstacles
Place wall across established trail:
- Traffic jam forms briefly
- Ants explore around obstacle
- New optimal path emerges
- Old trail evaporates

### 4. Deadlock Recovery
Draw maze with dead ends:
- Ants occasionally get stuck
- Stuck ants teleport home (implementation detail)
- Colony learns to avoid dead ends via pheromone

### 5. Path Width Modulation
High traffic paths:
- More ants → more pheromone
- Trail "widens" (more grid cells activated)
- Creates highways vs side streets

## Educational Experiments

### Experiment 1: Find the Critical Evaporation Rate
**Setup**: Place food far from nest
- Start with evaporation = 0.001 (very slow)
- Gradually increase evaporation rate
- **Question**: At what rate do trails become too weak to guide ants?

**Expected Result**: Around 0.05-0.08, trails evaporate faster than they're reinforced

### Experiment 2: Shortest Path is Not Always Fastest
**Setup**: Create two paths - one short but narrow, one long but wide
- Short path: Place walls to create single-file bottleneck
- Long path: Clear, wide route
- **Question**: Which path do ants prefer?

**Expected Result**: Initially short path wins, but congestion might shift traffic to long path

### Experiment 3: Pheromone Deposit vs Evaporation Balance
**Setup**: Fixed evaporation rate, vary deposit amount
- Deposit = 5: Weak trails
- Deposit = 20: Strong trails
- Deposit = 50: Very strong trails
- **Question**: What's the optimal balance?

**Expected Result**: Sweet spot around 10-15 provides good convergence without over-exploitation

### Experiment 4: The Double Bridge Experiment
**Setup**: Classic ACO experiment
1. Create two parallel walls from nest toward food
2. Make one path 2× longer than the other
3. Watch which path ants choose

**Expected Result**: Short path receives ~70-80% of traffic due to faster pheromone accumulation

### Experiment 5: Dynamic Environment Adaptation
**Setup**: Establish optimal path, then disrupt it
1. Let ants converge to a path (60+ seconds)
2. Place wall blocking the path
3. Measure how long until new path emerges

**Expected Result**: Recovery time depends on evaporation rate and exploration parameter

## Comparison to Other Swarm Algorithms

| Algorithm | Communication | Goal | Complexity |
|-----------|--------------|------|-----------|
| **Boids** | Direct (neighbor sensing) | Realistic flocking | 3 simple rules |
| **Vicsek** | Direct (alignment) | Phase transitions | 1 rule + noise |
| **ACO** | Indirect (pheromone) | Path optimization | State machine + probabilistic |

**ACO is unique because:**
- ✅ Solves **optimization problems** (not just motion)
- ✅ Uses **environment as memory**
- ✅ Demonstrates **positive feedback**
- ✅ Has **real-world applications** (routing, scheduling)

## Applications of ACO

### Classical Problems
- **Traveling Salesman Problem (TSP)**: Find shortest route visiting all cities
- **Vehicle Routing**: Optimize delivery truck routes
- **Network Routing**: Internet packet routing (AntNet)
- **Job Scheduling**: Factory floor task assignment

### Modern Applications
- **Protein folding**: Biological structure prediction
- **Image processing**: Edge detection, segmentation
- **Data mining**: Classification and clustering
- **Robotics**: Multi-robot path planning

### Why ACO Works Well
1. **Parallel search**: Many ants explore simultaneously
2. **Positive feedback**: Good solutions attract more exploration
3. **Adaptation**: Responds to environmental changes
4. **Simplicity**: Easy to implement and tune

## Algorithm Variants

The demo implements a simplified version. Real ACO has many variants:

### Ant System (AS) - Original
```
τ(t+1) = (1-ρ)·τ(t) + Δτ
where Δτ = Q/L (Q=constant, L=path length)
```

### Ant Colony System (ACS) - Improved
- Local pheromone update (while exploring)
- Global pheromone update (only best path)
- Pseudo-random proportional rule

### Max-Min Ant System (MMAS)
- Pheromone bounds: τ_min ≤ τ ≤ τ_max
- Only best ant updates pheromone
- Periodic reinitialization to avoid stagnation

### Elitist Ant System
- Best-so-far ant deposits extra pheromone
- Faster convergence but risk of premature convergence

## Performance Characteristics

### Computational Complexity
- **Per ant per step**: O(k) where k = number of sensors (3 in our case)
- **Pheromone evaporation**: O(W×H) where W,H = grid dimensions
- **Total per frame**: O(N×k + W×H) where N = ant count

### Convergence Time
Depends on:
- **Distance to food**: Longer distances require more iterations
- **Evaporation rate**: Slower evaporation → faster convergence
- **Ant count**: More ants → faster exploration
- **Pheromone deposit**: Stronger trails → faster convergence

Typical convergence: **30-120 seconds** for simple scenarios

### Scalability
- **50 ants**: Real-time 60 FPS, good exploration
- **200 ants**: Still 60 FPS, faster convergence
- **1000+ ants**: Would require spatial partitioning for neighbor queries

## Implementation Notes

### Why Grid-Based Pheromone?
- **Efficiency**: O(1) lookup, O(W×H) evaporation
- **Rendering**: Easy to visualize as heatmap
- **Memory**: Compact representation

Alternative: Per-ant trails (more accurate but slower)

### Why Synchronous Updates?
All ants update simultaneously (not sequential):
- **Fairness**: No ant gets "first pick" advantage
- **Parallelizable**: Could use Web Workers
- **Deterministic**: Easier to debug

### Simplifications vs Real ACO
1. **No distance heuristic**: Real ACO uses distance to guide exploration
2. **Simple return**: Real ants might follow pheromone home too
3. **No local search**: Real ACO often includes 2-opt improvement
4. **Binary food detection**: Real ACO might have probabilistic pickup

## Future Enhancements

Potential additions:
- [ ] TSP solver mode (multiple cities, find tour)
- [ ] Pheromone type differentiation (to-food vs to-nest trails)
- [ ] Distance heuristic visualization
- [ ] Export/import environment configurations
- [ ] Replay mode with timeline scrubbing
- [ ] Multiple colonies competing for resources
- [ ] 3D obstacles (tunnels, bridges)
- [ ] Performance mode with spatial partitioning
- [ ] Statistical analysis (convergence curves, path length over time)

## Scientific References

### Original Papers
- **Dorigo, M. (1992)**: "Optimization, Learning and Natural Algorithms", PhD thesis
- **Dorigo et al. (1996)**: "Ant System: Optimization by a colony of cooperating agents", *IEEE Transactions on Systems, Man, and Cybernetics*
- **Dorigo & Gambardella (1997)**: "Ant Colony System: A cooperative learning approach to the TSP", *IEEE Transactions on Evolutionary Computation*

### Reviews & Books
- **Dorigo & Stützle (2004)**: "Ant Colony Optimization", MIT Press - the definitive textbook
- **Bonabeau et al. (1999)**: "Swarm Intelligence: From Natural to Artificial Systems", Oxford
- **Blum (2005)**: "Ant colony optimization: Introduction and recent trends", *Physics of Life Reviews*

### Biological Inspiration
- **Deneubourg et al. (1990)**: "The self-organizing exploratory pattern of the Argentine ant", *Journal of Insect Behavior*
- **Goss et al. (1989)**: "Self-organized shortcuts in the Argentine ant", *Naturwissenschaften*

## Related Demos

- **[Boids](../boids-basic/)**: Direct communication, realistic flocking
- **[Vicsek Model](../self-propelled-particles/)**: Phase transitions in collective motion

ACO demonstrates **indirect communication** (stigmergy) while boids/Vicsek use **direct communication** (sensing neighbors).

## License

Part of the SwarmingLilMen project. See main repository for license details.

---

**Try creating interesting maze configurations and watch the ants solve them!** The emergent intelligence is both beautiful and powerful.
