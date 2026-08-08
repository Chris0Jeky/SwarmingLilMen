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
| `Friction`           | 0.90 – 0.99   | Velocity damping — **inert unless `SpeedModel` is `Damped`** |
| `FieldOfView`        | 240° – 300°   | Vision cone; 360° removes blind spots                      |
| `WanderStrength`     | 0 – 1.0       | Optional random steering; omitted/default `0` disables it  |

## Recipes

### Balanced (configs/balanced.json)
- MaxSpeed: 10, MaxForce: 2.5
- SenseRadius: 110, SeparationRadius: 45
- Weights: Sep 7.5, Ali 2.2, Coh 0.35
- Friction: 0.95, WanderStrength: 0.45
- SeparationCrowdingThreshold: 12 (boost separation when >12 neighbors)
- SeparationCrowdingBoost: 2.5 (up to 2.5x stronger separation under heavy crowding)

Good general-purpose flocking with noticeable swirls and separation.

All bundled JSON files inherit `Seed: 42` unless they explicitly override it. The balanced file's
authored `WanderStrength: 0.45` therefore remains deterministic for a matching .NET 8 binary,
platform, timestep, and input sequence. Supported external seed values are `0` through
`2147483647`; larger JSON values are rejected. Omit `WanderStrength` to use the disabled default.

### Peaceful Flocks (configs/peaceful.json)
- Emphasizes cohesion/alignment with a lower `MaxSpeed` (8) and `MaxForce` (1.5) for mellow motion.
- Use when demonstrating schooling/flocking without combat or chaos.

> **The `Friction` values in these three files do nothing today.** None of them sets `SpeedModel`,
> so all three inherit the `SpeedModel.ConstantSpeed` default, and `IntegrateSystem` applies
> friction only under `SpeedModel.Damped`. The authored values (`0.95`, `0.98`, `0.93`) are
> retained as intent for a future `Damped` variant. Add `"SpeedModel": 1` to a copy if you want
> damping to take effect — see the enum note below for why that is a number and not `"Damped"`.

### Enums in JSON must be numbers

`SimConfig.LoadFromJson` uses `System.Text.Json` without a string-enum converter, so an enum field
written as a name fails the whole load. Verified on 2026-08-08:
`{"SpeedModel": "Damped"}` exits with
`The JSON value could not be converted to SwarmSim.Core.SpeedModel`, while `{"SpeedModel": 1}`
loads. Use the ordinal:

| Field | `0` | `1` | `2` |
| --- | --- | --- | --- |
| `BoundaryMode` | `Wrap` (default) | `Reflect` | `Clamp` |
| `SpeedModel` | `ConstantSpeed` (default) | `Damped` | — |

Every bundled config omits both fields and therefore inherits `Wrap` and `ConstantSpeed`.

### Warbands (configs/warbands.json)
- Higher speed/force plus combat values (`AttackDamage`, `AttackRadius`).
- Reduce FieldOfView to 240° to simulate tunnel vision.
- Great for stress-testing combat mechanics once Phase 3 lands.

## Creating Your Own
1. Copy one of the example JSON files.
2. Adjust the values you care about. Any omitted property falls back to the default in `SimConfig`.
3. Run `dotnet run --project SwarmSim.Render -- --config path/to/your.json --agent-count 5000`.
4. Tweak live in the renderer (keys `1`-`8`, UP/DOWN). When you find a sweet spot, press `P` to print the values and feed them back into your JSON file. The on-screen labels still say `1-7`; that drift is tracked in [issue #39](https://github.com/Chris0Jeky/SwarmingLilMen/issues/39).

Remember to validate configs via `SimConfig.Validate()` (automatically run when loading via CLI—warnings print to the console).
