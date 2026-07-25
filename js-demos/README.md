# JavaScript Demos

> [`../PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified
> implementation, test, and performance state.

Standalone browser-based demonstrations of swarm algorithms and steering behaviors. These demos are simpler, more interactive alternatives to the C# implementation, designed for quick prototyping, education, and demonstrations.

## Available Demos

### [Boids Basic](./boids-basic/)
A beautiful implementation of Craig Reynolds' Boids algorithm with:
- Real-time parameter adjustment
- Multiple behavioral presets
- Motion trails and debug visualization
- Interactive spawning
- No dependencies, runs in any browser

**[Launch Demo](./boids-basic/index.html)**

### [Self-Propelled Particles (Vicsek Model)](./self-propelled-particles/)
An interactive demonstration of the Vicsek model showing phase transitions in active matter:
- Real-time order-disorder phase transitions
- Order parameter tracking and visualization
- Multiple visualization modes (arrows, dots, trails, density heatmap)
- Explore critical noise levels and collective motion
- Educational tool for statistical mechanics and emergent behavior

**[Launch Demo](./self-propelled-particles/index.html)**

### [Ant Colony Optimization](./ant-colony-optimization/)
An interactive demonstration of stigmergy-based pathfinding (Dorigo, 1992):
- Click to place food sources, drag to draw walls/obstacles
- Watch pheromone trails form and evaporate in real-time
- See emergent path optimization through positive feedback
- Adjust evaporation rate, deposit amount, and colony size
- Demonstrates indirect communication and swarm intelligence

**[Launch Demo](./ant-colony-optimization/index.html)**

### [Particle Swarm Optimization](./particle-swarm-optimization/)
Global optimization through velocity-based swarm search (Kennedy, Eberhart & Shi, 1995):
- Five classic test functions (Rastrigin, Ackley, Rosenbrock, Himmelblau, Sphere)
- Beautiful fitness landscape visualization with heatmap
- Watch particles converge to optimal solutions
- Adjust inertia weight, cognitive/social coefficients, max velocity
- Real-time convergence tracking and statistics
- Demonstrates continuous optimization vs ACO's discrete optimization

**[Launch Demo](./particle-swarm-optimization/index.html)**

---

## Purpose

These JavaScript demos complement the main C# engine by providing:

1. **Quick Iteration**: Test parameter ranges and behaviors without recompiling
2. **Easy Sharing**: Send a link, no installation required
3. **Educational Value**: Clean, commented code for learning algorithms
4. **Prototyping**: Experiment with new features before C# implementation
5. **Demonstrations**: Show off during presentations and meetings

## Technical Notes

- All demos are self-contained single HTML files (HTML + CSS + JS)
- No build process or dependencies required
- Vanilla JavaScript (no frameworks)
- HTML5 Canvas for rendering
- Optimized for readability over performance

## Running Demos

### Option 1: Direct Open
Simply open any `index.html` file in your browser:
```bash
open js-demos/boids-basic/index.html
```

### Option 2: Local Server (Recommended)
For best results, serve via HTTP:
```bash
cd js-demos/boids-basic
python3 -m http.server 8000
# Visit http://localhost:8000
```

### Option 3: Live Server (VS Code)
If using VS Code with Live Server extension:
- Right-click `index.html` → "Open with Live Server"

## Future Demos

Planned standalone implementations:
- [ ] **Boids Advanced**: Multi-group, FOV filtering, spatial partitioning
- [ ] **Predator-Prey**: Two groups with chase/flee behaviors
- [ ] **Obstacle Avoidance**: Steering around static/dynamic obstacles
- [ ] **Flow Fields**: Following vector fields
- [ ] **Particle Life**: Chemistry-inspired attraction/repulsion matrix
- [ ] **Kuramoto Model**: Phase-coupled oscillators (firefly synchronization)

## Contributing

When adding new demos:
1. Create a new subdirectory: `js-demos/demo-name/`
2. Include self-contained `index.html` and `README.md`
3. Update this index with a link and description
4. Keep demos simple and educational
5. Comment the code thoroughly
6. Include interactive controls where applicable

## Comparison to C# Engine

| Aspect | C# Engine | JS Demos |
|--------|-----------|----------|
| **Purpose** | Research simulation with explicit scale targets | Education & prototyping |
| **Scale** | Target: 50k-100k agents at 60 FPS; latest 50k legacy core sample is 162.815 ms/tick (2026-07-25), so the target is unmet | Unmeasured; no dated repository benchmark |
| **Complexity** | Legacy and canonical paths; Phase 3+ behavior remains incomplete | Core algorithms only |
| **Optimization** | SoA and uniform grids; SIMD remains planned | Direct, readable browser implementations |
| **Platform** | .NET 8.0, desktop | Any modern browser |
| **Use Case** | Research & development | Demos & learning |

The JS demos prioritize **clarity** and **accessibility**. No cross-runtime performance conclusion
is justified until both paths have a dated benchmark under comparable conditions. The C# sample
above comes from the 2026-07-25 Release `Category=Performance` command recorded in
`PROJECT_STATUS.md`; it is simulation throughput, not renderer FPS.
