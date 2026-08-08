# Parameter Guide & Tuning Notes

This guide explains the main fields in `SimConfig`, what they control, and how increasing/decreasing each value affects flocking behaviour. Use it alongside `CONFIGURATION_COOKBOOK.md` when crafting custom JSON configs.

`SimConfig` has 55 properties. Not all of them do anything yet — see
[Fields that are declared but not consumed](#fields-that-are-declared-but-not-consumed) before
tuning one and wondering why nothing changed.

## World & Timing
- **WorldWidth / WorldHeight** – Simulation extent in world-position units (defaults `1920` /
  `1080`). Under the default `Wrap` boundary these define the torus, so they change neighbour
  topology and not just the visible area.
- **BoundaryMode** – `Wrap` (default, toroidal), `Reflect`, or `Clamp`. In JSON this is an
  **ordinal**, not a name: `0`/`1`/`2`. See the enum note in `CONFIGURATION_COOKBOOK.md`.
- **FixedDeltaTime** – Fixed simulation timestep in seconds. The `SimConfig` default is `1/120`;
  the renderer's own base config and every registered preset use `1/60`. Changing it changes
  trajectories, so it is part of the determinism contract.

## Motion & Steering
- **Seed** – Controls deterministic legacy and canonical construction. Reusing a seed reproduces
  a trajectory only when the .NET binary/runtime, platform, configuration, timestep, and ordered
  input events also match; it is not a cross-platform promise. Supported external values are
  `0` through `2147483647`; larger values are rejected. Prefer `new World(config)`. The existing
  explicit-seed overload remains compatible: its argument is authoritative, and `World.Config`
  exposes that effective value without mutating the caller's object.
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
- **WanderStrength** – Finite, non-negative random steering magnitude; the default is `0`
  (disabled). Use small explicit values (0.1–0.5) to keep agents from freezing. Higher values break
  up synchronized lines.
- **WanderRate** – Finite, non-negative canonical wander-angle change limit in radians per second.
  It has no effect while `WanderStrength` is zero.

## Canonical Smoothing
- **MaxTurnRateDegPerSecond** – Finite, non-negative canonical heading-change limit; `0` prevents
  heading changes, while negative and non-finite values are rejected by canonical construction.
- **WhiskerTimeHorizon / WhiskerWeight** – Predictive collision lookahead and steering weight. Both
  must be finite; weight must be non-negative, while a finite horizon below `0.05` uses the existing
  `0.05`-second minimum.
- **SeparationPriorityRadiusFactor / SeparationPriorityExitFactor** – Entry and hysteresis-exit
  radii as finite fractions of `SenseRadius`; finite negative values retain the existing effective
  zero-threshold clamp.
- **SeparationPriorityBoost / SeparationPriorityHoldTime** – Strength and minimum duration of
  canonical separation priority. The boost must be finite and non-negative, and hold time must be
  finite.
- **SeparationPriorityRampInTime / SeparationPriorityRampOutTime / SeparationSpeedDroop** –
  Transition timing and temporary target-speed reduction while priority is active. Ramp times must
  be finite. Speed droop must be finite and remain in `[0, 1]`, preventing priority from reversing
  velocity.

These fields are read from `SimConfig` only by the canonical renderer path; the similarly named
legacy crowding controls retain their existing legacy semantics.

## Energy / Combat (Phase 3+ — reserved, not yet active)
- **AttackDamage / AttackRadius / AttackCooldown** – Intended to enable combat behaviour when aggression matrices are non-zero. No system reads them today.
- **BaseDrain, MoveCost** – Intended to control metabolism. No system reads them today, so energy neither drains nor gates death.
- **InitialEnergy / InitialHealth** – These two *are* applied, at spawn. Every other energy, health, reproduction, and forage field is inert.

## Fields that are declared but not consumed

Measured on 2026-08-08 by searching every read of each property across `SwarmSim.Core`,
`SwarmSim.Render`, and `SwarmSim.Benchmarks`. Nineteen of the 55 properties are validated and
copied but never read by any system. Setting them in JSON is silently a no-op.

Seventeen of them are the reserved Phase 3/4 surface above: `AttackDamage`, `AttackRadius`,
`AttackCooldown`, `AggressionMatrix`, `BaseDrain`, `MoveCost`, `MaxEnergy`,
`DeathEnergyThreshold`, `MaxHealth`, `HealthRegenRate`, `ReproductionEnergyThreshold`,
`ReproductionEnergyCost`, `ChildEnergyStart`, `MutationRate`, `MutationStdDev`, `FoodEnergyGain`,
and `ForageRadius`.

Two are **not** reserved-phase fields and look active but are not:

- **GridCellSize** (default `50`, validated `> 0`, and its source comment says "Should be ≈
  SenseRadius"). `World` hard-codes its spatial grid to `cellSize: Config.SenseRadius` and never
  reads this property, so tuning it changes nothing. Tracked as a defect, not a documented
  design choice.
- **MaxCapacity** (default `200_000`, validated `>= InitialCapacity`). The agent arrays are
  allocated once at `InitialCapacity` and never grow; spawning past that returns `-1`. `MaxCapacity`
  is therefore an unenforced ceiling. `InitialCapacity` is the real limit.

## How to Tune
1. **Set Speed & Force First** – Decide how fast you want agents to travel, then set `MaxForce` to at least 20–30% of `MaxSpeed`.
2. **Balance Separation vs Cohesion** – Start with only separation active to achieve proper spacing, then add alignment, then cohesion.
3. **Use Crowding Boosts** – `SeparationCrowdingBoost` helps resolve cramped nuclei automatically without needing huge base weights.
4. **Test with Presets** – Use `--preset balanced` or `--preset fast-loose` to compare behaviours, then copy from `configs/` and tweak. `--list-presets` prints the registered set.
5. **Observe Diagnostics** – On the legacy renderer press `F12` for snapshot/debug info; on the canonical renderer press `O` for the selected-boid interaction overlay and `Tab` to cycle the tracked boid. Watch neighbor counts and steering saturation to see whether forces are maxing out.

Refer to `CONTROLS.md` for runtime shortcuts and `CONFIGURATION_COOKBOOK.md` for concrete recipes. When saving custom configs, only override the fields you change—the loader falls back to defaults for everything else.
