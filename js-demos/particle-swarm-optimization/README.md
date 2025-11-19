# Particle Swarm Optimization (PSO)

An interactive browser-based demonstration of **Particle Swarm Optimization** (Kennedy, Eberhart & Shi, 1995), a global optimization algorithm inspired by the social behavior of bird flocking and fish schooling.

## Features

- **Multiple test functions**: Sphere, Rastrigin, Ackley, Rosenbrock, Himmelblau
- **Fitness landscape visualization**: Beautiful heatmap showing the optimization surface
- **Real-time particle tracking**: Watch particles search the space with motion trails
- **Personal best markers**: See each particle's best position found (orange)
- **Global best highlight**: Glowing green marker shows the swarm's best solution
- **Adjustable PSO parameters**: Inertia weight, cognitive/social coefficients, max velocity
- **Convergence tracking**: Live progress bar and statistics
- **Interactive controls**: Pause, reset, change functions on the fly

## What is Particle Swarm Optimization?

PSO is a **population-based stochastic optimization algorithm** that solves problems by iteratively trying to improve candidate solutions (particles) with regard to a given fitness measure.

### Core Concept

Inspired by bird flocking, PSO models particles flying through a search space. Each particle:
1. Remembers its **personal best** position (pBest)
2. Knows the **global best** position found by any particle (gBest)
3. Adjusts its velocity based on these two attractors
4. Explores the space seeking the optimal solution

**The beauty:** Simple local rules → emergent global optimization

## How to Run

Simply open `index.html` in any modern web browser:

```bash
# From the project root
open js-demos/particle-swarm-optimization/index.html

# Or with a local server
cd js-demos/particle-swarm-optimization
python3 -m http.server 8000
# Visit http://localhost:8000
```

No build tools, dependencies, or installation required!

## The PSO Algorithm

### Initialization
```
For each particle i:
    Initialize position x_i randomly in search space
    Initialize velocity v_i randomly (small magnitude)
    Evaluate fitness f(x_i)
    Set pBest_i = x_i, pBest_fitness_i = f(x_i)

Set gBest = argmin(pBest_fitness_i)
```

### Iteration Loop
```
For each iteration:
    For each particle i:
        // Velocity update (THE CORE EQUATION)
        v_i = w*v_i + c1*r1*(pBest_i - x_i) + c2*r2*(gBest - x_i)

        // Clamp velocity to prevent explosion
        v_i = clamp(v_i, -v_max, v_max)

        // Position update
        x_i = x_i + v_i

        // Evaluate fitness
        fitness = f(x_i)

        // Update personal best
        if fitness < pBest_fitness_i:
            pBest_i = x_i
            pBest_fitness_i = fitness

        // Update global best
        if fitness < gBest_fitness:
            gBest = x_i
            gBest_fitness = fitness
```

### The Velocity Update Equation

This is the heart of PSO:

```
v[t+1] = w*v[t] + c1*r1*(pBest - x[t]) + c2*r2*(gBest - x[t])
```

**Three components:**

1. **Inertia** (`w*v[t]`): Continue in current direction
   - High w → more exploration (global search)
   - Low w → more exploitation (local search)

2. **Cognitive** (`c1*r1*(pBest - x[t])`): Move toward personal best
   - Reflects particle's own experience
   - r1 = random [0,1] for stochasticity

3. **Social** (`c2*r2*(gBest - x[t])`): Move toward global best
   - Reflects swarm's collective knowledge
   - r2 = random [0,1] for stochasticity

**Typical parameter values:**
- w = 0.7-0.9 (or linearly decreasing from 0.9 to 0.4)
- c1 = c2 = 1.5-2.0 (often called "acceleration coefficients")

## Test Functions

The demo includes five classic benchmark functions for testing optimization algorithms:

### 1. Sphere Function
```
f(x,y) = x² + y²
```
- **Shape**: Simple bowl (convex)
- **Optimum**: f(0,0) = 0
- **Difficulty**: ⭐ Easy
- **Purpose**: Test basic convergence

**Characteristics:**
- Unimodal (single minimum)
- Smooth, no local minima
- Gradient points directly to optimum
- Easy for any optimizer

### 2. Rastrigin Function ⭐ (Default)
```
f(x,y) = 20 + x² + y² - 10(cos(2πx) + cos(2πy))
```
- **Shape**: Many local minima in a regular grid pattern
- **Optimum**: f(0,0) = 0
- **Difficulty**: ⭐⭐⭐⭐ Very challenging
- **Purpose**: Test ability to escape local minima

**Characteristics:**
- Highly multimodal (~25 local minima in typical bounds)
- Regular, periodic structure
- Easy to get trapped
- Classic benchmark for global optimization

### 3. Ackley Function
```
f(x,y) = -20·exp(-0.2·√(0.5(x²+y²))) - exp(0.5(cos(2πx)+cos(2πy))) + 20 + e
```
- **Shape**: Nearly flat outer region with central spike
- **Optimum**: f(0,0) = 0
- **Difficulty**: ⭐⭐⭐ Moderate-hard
- **Purpose**: Test exploration in flat regions

**Characteristics:**
- Multimodal with many local minima
- Nearly flat far from origin (hard to find gradient)
- Sharp global minimum
- Tests exploration vs exploitation balance

### 4. Rosenbrock Function
```
f(x,y) = (1-x)² + 100(y-x²)²
```
- **Shape**: Narrow, curved "banana valley"
- **Optimum**: f(1,1) = 0
- **Difficulty**: ⭐⭐⭐⭐ Challenging
- **Purpose**: Test pathological curvature

**Characteristics:**
- Unimodal but non-convex
- Valley is easy to find, minimum is hard to locate
- Slow convergence along valley
- Tests patience and precision

### 5. Himmelblau Function
```
f(x,y) = (x²+y-11)² + (x+y²-7)²
```
- **Shape**: Four symmetric global minima
- **Optima**: Four points, all f = 0
  - (3, 2), (-2.805, 3.131), (-3.779, -3.283), (3.584, -1.848)
- **Difficulty**: ⭐⭐⭐ Moderate
- **Purpose**: Test convergence to multiple optima

**Characteristics:**
- Four equivalent global minima
- Depends on initialization which one is found
- Tests diversity maintenance
- Beautiful symmetry

## PSO Parameters Explained

### Inertia Weight (w)
**Range:** 0.0 - 1.0 (typical: 0.6 - 0.9)

Controls how much of the previous velocity is retained:
- **High (0.9)**: More exploration, particles fly farther
- **Medium (0.7)**: Balanced
- **Low (0.4)**: More exploitation, particles converge faster

**Strategy:** Often use **linearly decreasing** inertia:
- Start high (0.9) for global exploration
- End low (0.4) for local exploitation
- Formula: `w = w_max - (w_max - w_min) * iter / max_iter`

### Cognitive Coefficient (c₁)
**Range:** 0.0 - 3.0 (typical: 1.5 - 2.0)

Strength of attraction to personal best:
- **High**: Particles trust own experience more
- **Low**: Particles ignore own history
- **Zero**: No personal memory (pure social)

**Interpretation:** "How much do I trust myself?"

### Social Coefficient (c₂)
**Range:** 0.0 - 3.0 (typical: 1.5 - 2.0)

Strength of attraction to global best:
- **High**: Particles converge quickly to gBest (risk: premature convergence)
- **Low**: Particles explore independently
- **Zero**: No swarm knowledge sharing (independent random search)

**Interpretation:** "How much do I trust the crowd?"

### Max Velocity
**Range:** 0.05 - 0.5 × search space range

Limits how far particles can move in one iteration:
- **High**: Particles can jump far (exploration, but risk overshooting)
- **Low**: Particles move slowly (exploitation, careful search)

**Purpose:** Prevents particles from flying out of bounds or oscillating wildly

## Experiments to Try

### Experiment 1: Inertia Weight Sweep
**Goal:** Find optimal inertia for different functions

**Setup:**
1. Select Rastrigin function
2. Reset swarm
3. Set w=0.9 (high inertia)
4. Watch for 100 iterations
5. Reset and try w=0.7, then w=0.4

**Expected Result:**
- w=0.9: Broad exploration, slower convergence
- w=0.7: Balanced, good performance
- w=0.4: Quick convergence, might get stuck in local minimum

### Experiment 2: Cognitive vs Social Balance
**Goal:** Understand the c₁/c₂ trade-off

**Setup:**
1. Select Ackley function
2. Try c₁=2.0, c₂=0.5 (individualistic)
3. Try c₁=0.5, c₂=2.0 (social)
4. Try c₁=1.5, c₂=1.5 (balanced)

**Expected Result:**
- High c₁: Particles spread out more, diverse search
- High c₂: Particles cluster around gBest, faster convergence (risk of premature convergence)
- Balanced: Best overall performance

### Experiment 3: Particle Count Effect
**Goal:** How does swarm size affect optimization?

**Setup:**
1. Select Rosenbrock function
2. Try 10 particles
3. Try 30 particles
4. Try 100 particles

**Expected Result:**
- 10: Faster per iteration, might miss optimum
- 30: Good balance
- 100: Slower but more thorough exploration

### Experiment 4: Escaping Local Minima
**Goal:** Test PSO's robustness to local optima

**Setup:**
1. Select Rastrigin (many local minima)
2. Watch where particles get stuck
3. Try increasing inertia (more exploration)
4. Try increasing particle count

**Expected Result:** Higher diversity (more particles, higher inertia) helps escape local minima

### Experiment 5: Multi-Modal Convergence
**Goal:** Which optimum does PSO find in Himmelblau?

**Setup:**
1. Select Himmelblau (4 global optima)
2. Reset several times
3. Note which optimum is found each time

**Expected Result:** Depends on random initialization; different runs find different optima

## Comparison to Other Swarm Algorithms

| Algorithm | Search Space | Update Rule | Communication | Best For |
|-----------|--------------|-------------|---------------|----------|
| **PSO** | Continuous, N-D | Velocity-based | Global best + personal best | Function optimization |
| **ACO** | Discrete paths | Pheromone deposits | Indirect (stigmergy) | Combinatorial (TSP, routing) |
| **Boids** | Continuous 2D/3D | Steering forces | Local neighbors | Realistic motion |
| **Vicsek** | Continuous 2D/3D | Angle averaging | Local neighbors | Phase transitions |

**PSO is unique because:**
- ✅ Designed for **continuous optimization**
- ✅ Each particle has **memory** (personal best)
- ✅ Uses **global information** (not just local)
- ✅ Velocity-based dynamics (has momentum)

## Applications of PSO

### Classic Optimization Problems
- **Function optimization**: Finding min/max of mathematical functions
- **Parameter tuning**: ML hyperparameters, control systems
- **Feature selection**: Choosing best subset of features
- **Neural network training**: Optimizing weights

### Engineering
- **Antenna design**: Optimal antenna parameters
- **Power systems**: Load dispatch, stability
- **Control systems**: PID tuning, optimal control
- **Structural design**: Truss optimization

### Modern Applications
- **Machine learning**: Hyperparameter optimization, ensemble weights
- **Computer vision**: Image segmentation, feature matching
- **Robotics**: Path planning, formation control
- **Finance**: Portfolio optimization, risk management

## PSO Variants

The demo implements basic PSO. Many variants exist:

### 1. Inertia Weight PSO (Standard)
```
w decreases linearly: w = w_max - (w_max - w_min) * iter / max_iter
```
Better balance of exploration/exploitation

### 2. Constriction Coefficient PSO
```
χ = 2 / |2 - φ - √(φ² - 4φ)|, where φ = c1 + c2
v = χ(v + c1*r1*(pBest - x) + c2*r2*(gBest - x))
```
Guaranteed convergence properties

### 3. Adaptive PSO
```
c1, c2, w adapt based on convergence state
```
Self-tuning parameters

### 4. Fully Informed PSO (FIPSO)
```
Uses neighborhood bests instead of single gBest
v = χ(v + Σ(c*r*(nBest_i - x)) / N)
```
More diverse, less prone to premature convergence

### 5. Quantum PSO
```
Particles have quantum behavior (probability clouds)
```
Better exploration in high dimensions

### 6. Multi-Objective PSO (MOPSO)
```
Multiple objectives, Pareto front tracking
```
For multi-objective optimization

## Performance Characteristics

### Computational Complexity
- **Per iteration**: O(N×D) where N=particles, D=dimensions
- **Function evaluations**: N per iteration
- **Memory**: O(N×D) for positions, velocities, pBests

### Convergence Rate
- **Early iterations**: Fast exploration
- **Middle**: Convergence to promising regions
- **Late**: Fine-tuning (slow if valley is narrow)

Typical convergence: **50-200 iterations** for simple functions

### Scalability
- **Particles**: 20-50 typical, 100-200 for hard problems
- **Dimensions**: Works well up to ~30D, struggles beyond 100D
- **Function evaluations**: 1000-10000 typical budget

### Strengths
✅ Easy to implement and tune
✅ Few parameters to adjust
✅ Good balance exploration/exploitation
✅ Works well on continuous problems
✅ Robust to noisy functions

### Weaknesses
❌ Can get stuck in local minima (especially low diversity)
❌ Performance degrades in high dimensions (curse of dimensionality)
❌ Sensitive to parameter settings on some problems
❌ No guaranteed global optimum (heuristic)

## Implementation Details

### Velocity Clamping
```javascript
const velRange = (bounds.max - bounds.min) * config.maxVel;
vx = clamp(vx, -velRange, velRange);
```
Prevents explosive growth and boundary violations

### Boundary Handling
```javascript
// Reflect strategy (used in demo)
if (x < bounds.min) {
    x = bounds.min;
    vx = -vx * 0.5;  // Bounce with damping
}
```

**Alternatives:**
- **Absorbing**: Set velocity to zero at boundary
- **Periodic**: Wrap around (toroidal space)
- **Random**: Reinitialize if out of bounds

### Fitness Landscape Visualization
```javascript
// 200×200 heatmap
// Color: Blue (good) → Red (bad)
// HSL color space for smooth gradients
```

Allows intuitive understanding of the search space

## Tips for Best Results

### 1. Start with Standard Settings
```
w = 0.7
c1 = c2 = 1.5
particles = 30
```
These work well for most problems

### 2. Increase Diversity for Multimodal Functions
- More particles (50-100)
- Higher inertia (0.9)
- Lower social coefficient (c2 = 1.0)

### 3. Speed Up for Simple Functions
- Fewer particles (20)
- Lower inertia (0.5)
- Higher coefficients (c1 = c2 = 2.0)

### 4. Use Decreasing Inertia
```
w(iter) = 0.9 - (0.9 - 0.4) * iter / max_iter
```
Start exploratory, end exploitative

### 5. Monitor Diversity
If particles cluster too early → premature convergence
- Increase inertia
- Reduce social coefficient
- Add more particles

## Future Enhancements

Potential additions:
- [ ] 3D visualization (WebGL)
- [ ] Neighborhood topologies (ring, von Neumann, random)
- [ ] Adaptive parameters (self-tuning)
- [ ] Multi-objective optimization (Pareto front)
- [ ] Constraint handling (penalty methods)
- [ ] Higher dimensional functions (3D+)
- [ ] Convergence plots (fitness vs iteration)
- [ ] Comparative analysis with other optimizers (genetic algorithm, etc.)
- [ ] Export results to CSV
- [ ] Animation speed control

## Scientific References

### Original Papers
- **Kennedy & Eberhart (1995)**: "Particle swarm optimization", *IEEE ICNN*
- **Shi & Eberhart (1998)**: "A modified particle swarm optimizer", *IEEE CEC*
- **Clerc & Kennedy (2002)**: "The particle swarm - explosion, stability, and convergence", *IEEE Trans. Evolutionary Computation*

### Review Papers
- **Poli et al. (2007)**: "Particle swarm optimization: An overview", *Swarm Intelligence*
- **Bonyadi & Michalewicz (2017)**: "Particle swarm optimization for single objective continuous space problems: a review", *Evolutionary Computation*

### Books
- **Kennedy et al. (2001)**: "Swarm Intelligence", Morgan Kaufmann
- **Engelbrecht (2005)**: "Fundamentals of Computational Swarm Intelligence", Wiley

### Applications
- **Zhang et al. (2015)**: "A comprehensive survey on particle swarm optimization", *Mathematical Problems in Engineering*

## Related Demos

- **[Boids](../boids-basic/)**: Steering behaviors for realistic flocking
- **[Vicsek Model](../self-propelled-particles/)**: Phase transitions in active matter
- **[ACO](../ant-colony-optimization/)**: Stigmergy-based combinatorial optimization

PSO demonstrates **velocity-based continuous optimization** while ACO handles **discrete/combinatorial problems**. Both are optimization algorithms but for different problem types!

## License

Part of the SwarmingLilMen project. See main repository for license details.

---

**Try optimizing the test functions and experiment with different parameter combinations!** The emergent search behavior is both beautiful and powerful.
