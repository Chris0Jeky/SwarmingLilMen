# Self-Propelled Particles (Vicsek Model)

An interactive browser-based demonstration of the **Vicsek model**, showcasing emergent collective motion and order-disorder phase transitions in active matter systems.

## Features

- **Real-time phase transitions**: Watch ordered/disordered transitions as you adjust noise
- **Order parameter tracking**: Live visualization of collective alignment (φ)
- **Multiple visualization modes**: Arrows, dots, motion trails, density heatmap
- **Interactive controls**: Adjust noise level, interaction radius, speed, and population
- **Preset configurations**: Ordered, critical, disordered, and band formation states
- **Performance optimized**: Smooth 60 FPS with hundreds to thousands of particles

## What is the Vicsek Model?

The Vicsek model is a minimal model of self-propelled particles that demonstrates **emergent collective motion** from simple local rules. It's widely studied in physics to understand flocking, swarming, and active matter.

### The Rules

Each particle follows three simple rules:

1. **Constant Speed**: Move at fixed speed `v` in direction `θ`
2. **Local Alignment**: Average the direction of neighbors within radius `r`
3. **Random Noise**: Add random perturbation `η` to direction

```
θ(t+1) = ⟨θ⟩_neighbors + η
position(t+1) = position(t) + v * (cos(θ), sin(θ))
```

### Phase Transition

The magic happens when you vary the **noise parameter η**:

- **Low noise (η < 0.2)**: Particles spontaneously align → **Ordered phase**
- **High noise (η > 0.4)**: Random motion dominates → **Disordered phase**
- **Critical point (η ≈ 0.2-0.4)**: **Phase transition** between order and disorder

The **order parameter φ** measures collective alignment:
- **φ = 1.0**: Perfect alignment (all moving same direction)
- **φ = 0.0**: Random motion (no collective order)

## How to Run

Simply open `index.html` in any modern web browser:

```bash
# From the project root
open js-demos/self-propelled-particles/index.html

# Or with a local server (recommended)
cd js-demos/self-propelled-particles
python3 -m http.server 8000
# Then visit http://localhost:8000
```

No build tools, dependencies, or installation required!

## Controls

### Presets
- **Ordered**: Low noise (η = 0.05) - particles align into coherent flow
- **Critical**: Near phase transition (η = 0.25) - fluctuating order
- **Disordered**: High noise (η = 0.8) - random motion, no collective behavior
- **Bands**: Low noise + high density - traveling wave patterns

### Parameters

#### Phase Transition
- **Noise η**: The key parameter! Controls randomness in direction updates
  - `η = 0`: Perfect alignment (deterministic)
  - `η = 0.2`: Near critical point (phase transition)
  - `η = 1.0`: Maximum randomness (gas-like)

#### Population
- **Particle Count**: Number of active particles (100-2000)
- **Speed**: Velocity magnitude (all particles move at constant speed)

#### Interaction
- **Interaction Radius**: How far particles "see" neighbors for alignment
  - Larger radius → more global coordination
  - Smaller radius → more local clustering

#### Visualization
- **Arrows**: Show velocity direction (default)
- **Dots**: Simple particle representation
- **Motion Trails**: Fade effect showing particle paths
- **Density Heatmap**: Color-coded density distribution

### Interactions
- **Click canvas**: Spawn 20 new particles at cursor position
- **Reset**: Reinitialize simulation with current parameters

## Physics Background

### Order Parameter

The order parameter φ (phi) quantifies collective alignment:

```
φ = |⟨v⟩| = |∑_i e^(iθ_i)| / N
```

Where:
- `θ_i` is the direction of particle `i`
- `N` is the total number of particles
- `⟨v⟩` is the average velocity vector

**Interpretation:**
- φ = 1: All particles moving in same direction
- φ = 0: Directions uniformly distributed (no order)

### Phase Diagram

The Vicsek model exhibits a **continuous phase transition**:

```
                 Ordered Phase
                 (φ ≈ 1)
                     ↑
    η_c ≈ 0.2-0.4 ←┼→ Critical Point
                     ↓
                 Disordered Phase
                 (φ ≈ 0)
```

The critical noise level `η_c` depends on:
- **Density**: Higher density → easier to maintain order
- **Radius**: Larger radius → more neighbors → stronger alignment
- **System size**: Finite-size effects at small N

### Universality Class

The Vicsek model belongs to the **XY model universality class** in 2D, similar to:
- Superfluid helium films
- 2D magnets
- Josephson junction arrays

This means it shares critical exponents with these seemingly unrelated systems!

## Algorithm Details

### Update Step

For each particle `i` at each timestep:

1. **Find neighbors**: All particles within radius `r`
   ```javascript
   for (let j of particles) {
       const dist = distance(i, j);
       if (dist < radius) {
           neighbors.push(j);
       }
   }
   ```

2. **Calculate average angle**:
   ```javascript
   let sumSin = 0, sumCos = 0;
   for (let neighbor of neighbors) {
       sumSin += Math.sin(neighbor.angle);
       sumCos += Math.cos(neighbor.angle);
   }
   const avgAngle = Math.atan2(sumSin, sumCos);
   ```

3. **Add noise and update**:
   ```javascript
   const eta = (Math.random() - 0.5) * noise;
   particle.angle = avgAngle + eta;

   particle.x += speed * Math.cos(particle.angle);
   particle.y += speed * Math.sin(particle.angle);
   ```

4. **Periodic boundaries**: Wrap around edges (toroidal world)

### Complexity

- **Neighbor search**: O(N²) naive (acceptable for < 2000 particles)
- **Per particle update**: O(N) worst case
- **Total per frame**: O(N²)

For larger simulations, use spatial partitioning (quadtree or cell list).

## Comparison to Boids

| Feature | Vicsek Model | Boids |
|---------|-------------|-------|
| **Rules** | Alignment only | Separation + Alignment + Cohesion |
| **Speed** | Constant | Variable or constant |
| **Physics focus** | Phase transitions | Realistic flocking |
| **Complexity** | Minimal | Moderate |
| **Noise** | Explicit parameter | Optional wander |
| **Applications** | Statistical physics | Animation, games |
| **Phase behavior** | Yes (order-disorder) | No (always ordered) |

**Vicsek is simpler and more fundamental** - it's the minimal model for collective motion. **Boids is richer and more realistic** - it produces lifelike flocking behavior.

## Emergent Phenomena

Watch for these patterns as you adjust parameters:

### 1. Spontaneous Symmetry Breaking
At low noise, random initial conditions spontaneously align in a *random* direction (not predetermined).

### 2. Traveling Bands
At intermediate noise + high density, particles form **traveling wave patterns** moving perpendicular to local alignment.

### 3. Critical Fluctuations
Near the phase transition, the order parameter φ rapidly fluctuates - watch the order bar!

### 4. Density Clustering
Even without explicit attraction (no cohesion rule!), particles can form transient high-density regions through alignment dynamics.

## Educational Use

This demo is excellent for:
- **Statistical mechanics**: Demonstrating phase transitions
- **Complex systems**: Emergent behavior from simple rules
- **Active matter**: Non-equilibrium physics
- **Parameter exploration**: Effect of noise on collective behavior
- **Order parameters**: Quantifying emergent order

## Extensions & Experiments

Try these experiments:

### 1. Find the Critical Point
- Start at η = 0
- Slowly increase noise
- Watch order parameter
- Critical point is where φ starts to drop rapidly

### 2. Finite-Size Effects
- Run with 100 particles vs 2000 particles
- Compare critical noise levels
- Smaller systems have sharper transitions

### 3. Density Dependence
- Fixed noise, vary particle count
- Higher density → higher order (for same η)

### 4. Interaction Range
- Very small radius → local clusters only
- Very large radius → global alignment

## Future Enhancements

Potential additions:
- [ ] Cell list optimization for O(N) neighbor search
- [ ] Metric-free topology (nearest N neighbors instead of radius)
- [ ] Anisotropic interaction (vision cone like boids)
- [ ] Multiple species with different noise levels
- [ ] Obstacles and boundaries
- [ ] 3D version with WebGL
- [ ] Export order parameter time series (CSV)
- [ ] Interactive phase diagram (η vs ρ)

## Scientific References

### Original Papers
- **Vicsek et al. (1995)**: "Novel type of phase transition in a system of self-driven particles", *Physical Review Letters*
- **Grégoire & Chaté (2004)**: "Onset of collective and cohesive motion", *Physical Review Letters*
- **Chaté et al. (2008)**: "Collective motion of self-propelled particles interacting without cohesion", *Physical Review E*

### Reviews
- **Vicsek & Zafeiris (2012)**: "Collective motion", *Physics Reports* - comprehensive review
- **Marchetti et al. (2013)**: "Hydrodynamics of soft active matter", *Reviews of Modern Physics*

### Applications
- Bird flocks, fish schools, bacterial colonies
- Crowd dynamics, pedestrian flow
- Molecular motors, cytoskeletal dynamics
- Robotic swarms, UAV coordination

## Technical Notes

- **Language**: Pure JavaScript (no dependencies)
- **Canvas**: HTML5 2D rendering context
- **Boundary**: Toroidal (periodic) wrapping
- **Update**: Synchronous (all particles use t=n state to compute t=n+1)
- **Noise**: Uniform distribution in [-η/2, η/2]

## License

Part of the SwarmingLilMen project. See main repository for license details.
