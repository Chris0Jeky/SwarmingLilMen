# Parameter Guide & Tuning Notes

This guide explains the main fields in `SimConfig`, what they control, and how increasing/decreasing each value affects flocking behaviour. Use it alongside `CONFIGURATION_COOKBOOK.md` when crafting custom JSON configs.

## Motion & Steering
- **MaxSpeed** – Upper bound on velocity magnitude. Higher values create more energetic movement but require higher `MaxForce` (or stronger weights) to turn quickly. Keep `MaxForce ≥ MaxSpeed * 0.2` for responsive steering.
- **MaxForce** – Steering budget per tick. A larger value lets agents change direction faster. Too low relative to `MaxSpeed` yields “train” formations; too high can cause jitter.
- **Friction** – Velocity damping after applying steering. Values near `0.90–0.98` simulate drag. `1.0` keeps constant speed (only appropriate when steering budgets are high).
- **SpeedModel** – `ConstantSpeed` skips friction; `Damped` applies friction even if you set `Friction < 1`.

## Perception & Separation
- **SenseRadius** – Max distance for considering neighbors. Large radius (>120) increases neighbor counts, making alignment/cohesion stronger but also more expensive.
- **FieldOfView** – Vision cone in degrees (e.g., 270°). Reduce it to create blind spots and more emergent patterns.
- **MaxNeighbors** – Hard cap on neighbors contributing to steering. Use 8–16 for classic boids behaviour, higher for smoother large-scale flow.
- **SeparationRadius** – Distance within which separation activates. Increase this to prevent following-in-line behaviour.
- **SeparationWeight** – Scales desired separation speed (`maxSpeed * weight`). Higher values push agents apart faster.
- **SeparationCrowdingThreshold** – Neighbor count that triggers crowding boosts. Lower this if you want separation to react sooner.
- **SeparationCrowdingBoost** – Maximum multiplier applied to separation when neighbor count exceeds the threshold (e.g., 2.5 = up to 2.5× stronger).
- **CollisionAvoidanceRadius** – “Emergency” bubble. If a neighbor enters this radius, the agent ignores all other rules and steers directly away.
- **CollisionAvoidanceBoost** – Multiplier for the emergency steer. Increase it to make close contacts explode apart instantly.

## Alignment & Cohesion
- **AlignmentWeight** – Strength of the tendency to match average neighbor velocity. Increasing it forms ribbon-like formations; decreasing it gives more chaotic swirls.
- **CohesionWeight** – Pull toward the local center of mass. Too high relative to separation creates clumps; a value around 1/10th of separation is typical.

## Wander & Noise
- **WanderStrength** – Random steering magnitude. Use small values (0.1–0.5) to keep agents from freezing. Higher values break up synchronized lines.

## Energy / Combat (Phase 3+)
- **AttackDamage / AttackRadius / AttackCooldown** – Enable combat behaviour when aggression matrices are non-zero. Increase to make encounters more lethal.
- **BaseDrain, MoveCost, InitialEnergy** – Control metabolism; use higher drain for survival-style simulations where agents must forage.

## How to Tune
1. **Set Speed & Force First** – Decide how fast you want agents to travel, then set `MaxForce` to at least 20–30% of `MaxSpeed`.
2. **Balance Separation vs Cohesion** – Start with only separation active to achieve proper spacing, then add alignment, then cohesion.
3. **Use Crowding Boosts** – `SeparationCrowdingBoost` helps resolve cramped nuclei automatically without needing huge base weights.
4. **Test with Presets** – Use `--preset balanced` or `--preset fast-loose` to compare behaviours, then copy from `configs/` and tweak.
5. **Observe Diagnostics** – Press `F12` for snapshot/debug info and look at neighbor counts + steering saturation to know if forces are maxing out.

Refer to `CONTROLS.md` for runtime shortcuts and `CONFIGURATION_COOKBOOK.md` for concrete recipes. When saving custom configs, only override the fields you change—the loader falls back to defaults for everything else.
