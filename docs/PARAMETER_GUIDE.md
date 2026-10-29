# Parameter Guide & Tuning Notes

`SimConfig` is the shared scenario surface used by the legacy engine and the renderer's
canonical bridge. `CanonicalWorldSettings` is the native canonical surface. Both load and
save JSON. `ConfigurationResolver` applies **defaults → named scenario → file → explicit
overrides**; see [Configuration Cookbook](CONFIGURATION_COOKBOOK.md) for APIs, scenario IDs,
and the distinction between sparse recipes and fully resolved saved files.

The two engines retain different dynamics and different class defaults. Matching names or
numbers does not establish trajectory equivalence.

## Defaults that affect comparisons

| Setting | `SimConfig` default | Native canonical default |
| --- | --- | --- |
| Seed | `42` | `123456` |
| World width / height | `1920 / 1080` | `1920 / 1080` |
| Fixed timestep | `1/120` second | `1/60` second |
| Speed | `MaxSpeed: 200` | `TargetSpeed: 1` |
| Maximum force | `5` | `0.2` |
| Sense radius | `50` | `10` |
| Separation radius | `20` | `5` |
| Separation / alignment / cohesion weights | `1.5 / 1 / 1` | `1.5 / 1 / 1` |
| Field of view | `270` degrees | `270` degrees |
| Neighbor setting | `16` | `32` |
| Wander strength / rate | `0 / 1.5` | `0 / 1.5` |
| Initial capacity | `100000` | `1024` |

The renderer has its own established base configuration: a `1/60` timestep, speed ceiling
`10`, force `2.5`, and its displayed tuning values. Loading a file through
`SimConfig.LoadFromJson` inherits class defaults for omitted fields, not the renderer base.
Named scenarios are sparse overlays; native canonical resolution retains canonical defaults
for fields the scenario does not specify. Map a complete resolved `SimConfig` when comparing
the same authored seed, dimensions, and timestep across engines.

## World, timing, and spatial partitioning

- **Seed** accepts `0` through `2147483647`. Reproduction of a trajectory requires a matching
  binary, runtime/platform, complete configuration, timestep, and ordered input events.
  The engines use different paths and are not expected to agree merely because seeds match.
  Prefer `new World(config)`; the compatible explicit-seed overload is authoritative and
  exposes the effective seed in `World.Config` without changing the caller's config.
- **WorldWidth / WorldHeight** are positive finite extents. Under `Wrap`, both legacy sensing
  and canonical sensing use periodic geometry and minimum-image displacements. Legacy
  `Reflect` and `Clamp` keep Euclidean sensing; canonical worlds are always periodic.
- **BoundaryMode** is a defined numeric enum in JSON: `0` Wrap, `1` Reflect, `2` Clamp.
- **FixedDeltaTime** is positive and finite. It changes trajectories. The native canonical
  `Step(dt)` argument determines each actual step; its configured timestep is the caller's
  fixed-step scheduling value.
- **GridCellSize** defaults to `50` and controls the legacy grid. Queries visit enough cells
  to cover `SenseRadius`, so a smaller cell size does not restrict sensing to a 3×3 area.
  Validation bounds the grid's cell count before allocation. The canonical renderer uses its
  sense radius as the grid cell size; custom canonical callers choose their spatial index.

Legacy sensing still has a bounded, traversal-ordered candidate buffer of 256 entries. Dense
scenes can therefore choose different candidates when the grid layout changes. Radius-complete
cell traversal does not promise identical dense-scene trajectories across grid sizes. Periodic
queries avoid revisiting the same cell, including worlds with only one or two cells per axis.

## Motion and steering

- **MaxSpeed** is the legacy upper speed bound. `ConstantSpeed` is the historical name of the
  undamped mode; it skips friction but does not renormalize slower agents back to the ceiling.
- **MaxForce** caps composed steering. Larger values permit faster steering changes, while
  excessive force can cause abrupt turns. Start around 20–30% of the intended speed, then tune
  with observed trajectories rather than treating that ratio as a physical invariant.
- **Friction** is finite and in `[0, 1]`. Legacy applies this velocity multiplier each tick only
  under **SpeedModel.Damped**. The numeric JSON values are `0` ConstantSpeed and `1` Damped.
  Canonical movement has no friction setting and maintains its target speed.
- **SeparationWeight / AlignmentWeight / CohesionWeight** must be finite. Signed values remain
  valid in legacy for compatibility. The canonical bridge clamps them to zero or above, and
  canonical rules also clamp negative weights.

## Perception, separation, and supported disable modes

| Field | Meaning and accepted edge cases |
| --- | --- |
| `SenseRadius` | Positive finite perception radius |
| `FieldOfView` | Finite degrees in `(0, 360]`; `360` is omnidirectional |
| `MaxNeighbors` | Legacy values `<= 0` remove the contributing-neighbor cap within the bounded candidate buffer; canonical uses at least four candidates |
| `SeparationRadius` | Legacy requires finite `>= 0`; `0` disables its separation accumulation |
| `SeparationCrowdingThreshold` | Legacy non-negative neighbor count; `0` is supported |
| `SeparationCrowdingBoost` | Finite legacy crowding multiplier; existing signed values remain accepted |
| `CollisionAvoidanceRadius` | Finite legacy emergency radius; `0` disables the override, and negative values retain the existing minimum-radius clamp |
| `CollisionAvoidanceBoost` | Finite legacy value; the consumer applies its existing minimum of `1` |

A negative legacy separation radius is rejected because its squared-radius test and signed
radial falloff do not describe a valid protected distance. Non-finite steering values are
rejected before world construction. Zero and unlimited modes above remain valid both through
direct construction and JSON loading.

Legacy separation uses bounded linear radial falloff when accumulating direction; canonical
separation additionally weights by inverse distance. Both use prioritized steering, but their
behavior is not interchangeable.

## Wander and canonical smoothing

**WanderStrength** is finite and non-negative; the default `0` disables it.
**WanderRate** is the finite, non-negative canonical wander-angle change limit in radians per
second. It has no effect while wander is disabled.

The following fields are active only in canonical motion, even when they are stored in
`SimConfig`. `SimConfig.Validate` checks them before a loaded configuration reaches the
canonical bridge.

| Field | Accepted range and interpretation |
| --- | --- |
| `MaxTurnRateDegPerSecond` | Finite, non-negative; `0` prevents heading changes |
| `WhiskerTimeHorizon` | Finite; the engine retains its `0.05`-second minimum |
| `WhiskerWeight` | Finite and non-negative |
| `SeparationPriorityRadiusFactor` | Finite; negative factors retain the effective zero-threshold clamp |
| `SeparationPriorityExitFactor` | Finite; negative factors retain the effective zero-threshold clamp |
| `SeparationPriorityBoost` | Finite and non-negative |
| `SeparationPriorityHoldTime` | Finite; existing minimum-time handling is preserved |
| `SeparationPriorityRampInTime / SeparationPriorityRampOutTime` | Finite; existing timing clamps are preserved |
| `SeparationSpeedDroop` | Finite and in `[0, 1]`, preventing reversal of target speed |

No new ordering constraint is imposed between priority entry and exit factors.

## Legacy-to-canonical mapping

`CanonicalWorldSettings.FromSimConfig(config, initialCapacity: ...)` is the reusable bridge.
It preserves the renderer's established numeric mapping. The renderer still owns how many
agents to allocate and passes `max(2 * initialAgentCount, 1024)`; direct callers can provide a
capacity, or omit it to use `config.InitialCapacity`.

| `SimConfig` field(s) | Canonical field(s) | Mapping and semantic limit |
| --- | --- | --- |
| `Seed` | `Seed` | Copied; different engine algorithms still produce different trajectories |
| `WorldWidth / WorldHeight` | Same names | Copied; canonical geometry is periodic regardless of legacy boundary mode |
| `FixedDeltaTime` | Same name | Copied; canonical callers must use it to schedule `Step` |
| `MaxSpeed` | `TargetSpeed` | `max(0.01, value)`; legacy ceiling becomes canonical fixed target speed |
| `MaxForce` | Same name | `max(0.01, value)`; both bound steering, while speed integration differs |
| `SenseRadius` | Same name | `max(1, value)` |
| `SeparationRadius` | Same name | `max(1, value)`; legacy zero-disable does not survive this compatibility mapping |
| `SeparationWeight / AlignmentWeight / CohesionWeight` | Same names | Each becomes `max(0, value)`; separation weighting laws differ |
| `FieldOfView` | Same name | Copied |
| `MaxNeighbors` | Same name | Copied; legacy nonpositive means bounded unlimited, canonical clamps to at least four |
| `WanderStrength / WanderRate` | Same names | Copied; legacy does not consume `WanderRate` |
| `MaxTurnRateDegPerSecond` | Same name | Copied; legacy has no equivalent heading limiter |
| `WhiskerTimeHorizon / WhiskerWeight` | Same names | Copied; legacy emergency collision override is a different mechanism |
| All `SeparationPriority*` fields and `SeparationSpeedDroop` | Same names | Copied; legacy crowding controls are not equivalents |
| `InitialCapacity` | `InitialCapacity` | Copied unless the caller supplies an explicit capacity |
| `Friction / SpeedModel` | None | Canonical target-speed motion does not model legacy damping |
| `BoundaryMode` | None | Canonical is always periodic |
| `GridCellSize` | None | Canonical spatial-index construction is a separate caller choice |
| `SeparationCrowdingThreshold / SeparationCrowdingBoost` | None | Legacy neighbor-count boost does not map to canonical priority hysteresis |
| `CollisionAvoidanceRadius / CollisionAvoidanceBoost` | None | Legacy immediate override does not map directly to predictive whiskers |
| `CompactionInterval` | None | Legacy storage maintenance has no canonical setting |
| `MaxCapacity` and energy, health, combat, reproduction, forage, aggression fields | None | No corresponding canonical configuration surface |

There is no reverse conversion API: dropping canonical-only dynamics or inventing legacy
friction and boundary values would imply a parity that does not exist. Use this table to choose
a comparison subset rather than assuming the complete configurations represent the same model.

### Native canonical validation

`CanonicalWorldSettings.Validate()` is used by its JSON readers, writers, and resolver.
It requires positive finite target speed, maximum force, sense radius, world extents, and fixed
timestep; field of view must be in `(0, 360]`. Separation radius and the three steering weights
must be finite, while their existing negative-value clamps remain available. Seed and smoothing
controls follow the bounds above.

Native canonical capacity values below one retain the constructor's effective minimum of one;
neighbor settings below four retain the minimum of four. Spatial-index compatibility and grid
allocation limits are checked by index/world construction. Both construction and JSON validation
enforce these base finite ranges, including the configured timestep and rule inputs. The bridge
translates values; callers can use `Validate()` to collect errors before constructing a world.

## Spawn state and reserved fields

**InitialEnergy** and **InitialHealth** are copied into legacy agents at spawn. They must be
finite. Their eventual gameplay ranges remain undecided, so signed finite values retain their
existing behavior. No implemented metabolism, combat, or reproduction system consumes them.

The following properties remain reserved: `AttackDamage`, `AttackRadius`, `AttackCooldown`,
`AggressionMatrix`, `BaseDrain`, `MoveCost`, `MaxEnergy`, `DeathEnergyThreshold`,
`MaxHealth`, `HealthRegenRate`, `ReproductionEnergyThreshold`, `ReproductionEnergyCost`,
`ChildEnergyStart`, `MutationRate`, `MutationStdDev`, `FoodEnergyGain`, and
`ForageRadius`. Changing them does not activate future gameplay systems. Existing validation
of `MutationRate` in `[0, 1]` and a non-null square aggression matrix remains; this work does
not invent gameplay bounds for unused fields. JSON output requires finite numbers.

## Population capacity

Legacy **InitialCapacity** is the positive starting allocation (default `100000`).
**MaxCapacity** is the occupied-slot ceiling (default `200000`) and must be at least
`InitialCapacity`. Both must fit .NET's
[`Array.MaxLength`](https://learn.microsoft.com/en-us/dotnet/api/system.array.maxlength?view=net-8.0)
element-count limit. That limit does not guarantee enough memory for an allocation.

`World.AddAgent`, `AddRandomAgent`, and `SpawnAgentsInCircle` grow storage when the current
allocation fills: double it, capped at `MaxCapacity`. All agent arrays, sensing scratch and grid
links grow together. Existing agent indices, state, random sequence and simulation time survive;
growth does not compact dead slots or reset systems. `Count` includes dead slots until
`CompactDeadAgents` removes them, and compaction retains its existing index-remapping behavior.
**CompactionInterval** must be at least one tick. Storage never shrinks automatically.

At the maximum, the single-agent methods return `-1`; the circle method returns the number it
actually added. Refused capacity requests consume no random draws. Growth allocations occur in
spawn calls, while ordinary spawns with room and later simulation ticks reuse storage. Reacquire
borrowed world-array references and spans after a spawn that may grow capacity. Copied snapshots
remain unchanged; mutations outside `SimulationRunner.Advance` still require
`NotifyWorldMutated()` before capturing the new state for interpolation. Grid object identity is
preserved, and newly spawned agents enter it on the next `Rebuild`, as before.

This intentionally changes the former fixed-allocation behavior: requests between the initial
allocation and the configured maximum now succeed. A rejected `AddRandomAgent` also no longer
advances randomness. Set `MaxCapacity == InitialCapacity` when fixed legacy storage is required.
Canonical settings have no `MaxCapacity`: canonical storage remains fixed at its effective
`InitialCapacity`. Legacy headless runs accept `--agents` through `MaxCapacity` and finish any
required growth before allocating their metric buffers or writing output.

## A practical tuning sequence

1. Choose the engine, resolved timestep, seed, and boundary behavior.
2. Set speed and force, then tune separation before alignment and cohesion.
3. Check candidate counts and truncation before increasing perception or population.
4. Compare the shared named scenarios, keeping distinct recipe files explicit.
5. Save the fully resolved configuration with results so later default changes do not alter the experiment.

Use legacy `F12` diagnostics or canonical `O` / `Tab` inspection as described in
[Controls](CONTROLS.md). Source contracts are in
[SimConfig](../SwarmSim.Core/SimConfig.cs),
[CanonicalWorldSettings](../SwarmSim.Core/Canonical/CanonicalWorldSettings.cs), and
[ConfigurationResolver](../SwarmSim.Core/Configuration/ConfigurationResolver.cs).
