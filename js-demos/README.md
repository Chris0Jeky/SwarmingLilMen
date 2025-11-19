# JavaScript Demos

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
- [ ] **Ant Colony**: Pheromone trails and emergent pathfinding

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
| **Purpose** | High-performance simulation | Education & prototyping |
| **Scale** | 50k-100k agents @ 60 FPS | 300-1000 agents @ 60 FPS |
| **Complexity** | Full feature set | Core algorithms only |
| **Optimization** | SoA, SIMD, spatial grids | Naive, readable code |
| **Platform** | .NET 8.0, desktop | Any modern browser |
| **Use Case** | Research & development | Demos & learning |

The JS demos prioritize **clarity** and **accessibility** over the C# engine's **performance** and **scale**.
