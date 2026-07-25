# Configuration Cookbook

Use these recipes as starting points when building your own `SimConfig` JSON files (see the `configs/` directory for ready-to-run examples).

## Parameter Cheat Sheet

| Parameter            | Typical Range | Effect                                                     |
|----------------------|---------------|------------------------------------------------------------|
| `MaxSpeed`           | 5 – 20        | Higher values make agents more aggressive/frenetic         |
| `MaxForce`           | 1 – 4         | Caps steering strength (avoid oscillation if too high)     |
| `SenseRadius`        | 60 – 140      | Larger radius means more neighbors per agent               |
| `SeparationRadius`   | 15 – 40       | Protected distance; keep < `SenseRadius`                   |
| `SeparationWeight`   | 2 – 12        | Repulsion strength; use larger values for dense scenes     |
| `AlignmentWeight`    | 1 – 5         | Tendency to match neighbor velocity                        |
| `CohesionWeight`     | 0.3 – 3       | Pull toward local center of mass                           |
| `Friction`           | 0.90 – 0.99   | Velocity damping; 1.0 means constant speed                 |
| `FieldOfView`        | 240° – 300°   | Vision cone; 360° removes blind spots                      |
| `WanderStrength`     | 0.1 – 1.0     | Random steering to prevent equilibrium                     |

## Recipes

### Balanced (configs/balanced.json)
- MaxSpeed: 10, MaxForce: 2.5
- SenseRadius: 110, SeparationRadius: 45
- Weights: Sep 7.5, Ali 2.2, Coh 0.35
- Friction: 0.95, WanderStrength: 0.45
- SeparationCrowdingThreshold: 12 (boost separation when >12 neighbors)
- SeparationCrowdingBoost: 2.5 (up to 2.5x stronger separation under heavy crowding)

Good general-purpose flocking with noticeable swirls and separation.

### Peaceful Flocks (configs/peaceful.json)
- Emphasizes cohesion/alignment, higher friction (0.98) for mellow motion.
- Use when demonstrating schooling/flocking without combat or chaos.

### Warbands (configs/warbands.json)
- Higher speed/force plus combat values (`AttackDamage`, `AttackRadius`).
- Reduce FieldOfView to 240° to simulate tunnel vision.
- Great for stress-testing combat mechanics once Phase 3 lands.

## Creating Your Own
1. Copy one of the example JSON files.
2. Adjust the values you care about. Any omitted property falls back to the default in `SimConfig`.
3. Run `dotnet run --project SwarmSim.Render -- --config path/to/your.json --agent-count 5000`.
4. Tweak live in the renderer (1-7, UP/DOWN). When you find a sweet spot, press `P` to print the values and feed them back into your JSON file.

Remember to validate configs via `SimConfig.Validate()` (automatically run when loading via CLI—warnings print to the console).
