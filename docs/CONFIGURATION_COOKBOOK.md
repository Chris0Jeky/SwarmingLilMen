# Configuration Cookbook

Configuration uses one layering rule:

**class defaults → named scenario → JSON file → explicit overrides**

`ConfigurationResolver` implements that order for both `SimConfig` and
`CanonicalWorldSettings`. Each layer replaces only the properties it names; an aggression
matrix replaces the whole matrix. Validation runs after the last layer, so a final override
can repair an invalid numeric value in an earlier layer. The resolver returns a fresh object
and does not mutate its defaults or share aggression arrays between results.

## Loading, saving, and resolving

Both configuration classes support the same file and string operations:

```csharp
SimConfig config = SimConfig.LoadFromJson("configs/balanced.json");
config.SaveToJson("resolved.json");  // All properties, including aggression rows.
SimConfig copy = SimConfig.FromJson(config.ToJson());

CanonicalWorldSettings settings = CanonicalWorldSettings.LoadFromJson("canonical.json");
settings.SaveToJson("resolved-canonical.json");
```

File readers accept comments, trailing commas, and case-insensitive property names. Omitted
fields keep the corresponding class defaults. The direct `LoadFromJson` / `FromJson` methods
ignore unknown fields for compatibility; check spelling carefully. The new resolver rejects
unknown file and override names with `ArgumentException`, including a legacy `MaxSpeed` field
in a native canonical file. Named canonical scenarios deliberately select only applicable fields.
Malformed JSON and malformed matrix rows throw
`JsonException`; invalid configuration values throw `ArgumentException`. Loading JSON
`null` throws `InvalidOperationException`. Saving validates and serializes before opening the
destination, so an invalid configuration does not truncate an existing file.

Use the resolver when an experiment combines a scenario, a file, and command-line values:

```csharp
using System.Text.Json;
using SwarmSim.Core;
using SwarmSim.Core.Configuration;

var overrides = new Dictionary<string, JsonElement>
{
    ["Seed"] = JsonSerializer.SerializeToElement(123u),
    ["MaxNeighbors"] = JsonSerializer.SerializeToElement(32)
};

SimConfig config = ConfigurationResolver.ResolveSimConfig(
    scenarioName: "balanced",
    filePath: "my-experiment.json",
    overrides: overrides);

config.SaveToJson("resolved-experiment.json");
```

An optional `defaults:` argument supplies an existing base configuration. Unspecified
properties survive all later layers. Supplied defaults must already be JSON-serializable:
non-finite values cannot be repaired by a later override because the base is serialized first.
A saved resolved file pins every value; a short authored
recipe continues to inherit omitted defaults. Keep the bundled `configs/*.json` recipes sparse.

For native canonical files, use `ConfigurationResolver.ResolveCanonicalSettings` with the
same argument names. These files use `TargetSpeed`, not `MaxSpeed`. Named scenarios translate
that one name and apply only fields that exist in canonical settings. Unspecified values retain
canonical defaults, including its seed and capacity. To map an already resolved `SimConfig`
instead, call `CanonicalWorldSettings.FromSimConfig(config)`; the mapping and its established
renderer clamps are documented in [Parameter Guide](PARAMETER_GUIDE.md#legacy-to-canonical-mapping).

## One scenario vocabulary, preserved entry points

The shared resolver recognizes the following case-insensitive IDs. Existing static factories
remain available, and the five renderer preset labels and F-key positions remain separate UI
metadata.

| Shared scenario ID | Existing or new entry point | Intent |
| --- | --- | --- |
| `balanced` | `SimConfig.Balanced()`, `configs/balanced.json`, renderer F1 / `--preset balanced` | Smooth general-purpose flocking |
| `strong-separation` | Renderer F2 / `--preset strong-separation` | More personal space in dense scenes |
| `tight-flocking` | Renderer F3 / `--preset tight-flocking` | Strong alignment and cohesion |
| `fast-loose` | Renderer F4 / `--preset fast-loose` | Higher speed ceiling and lighter cohesion |
| `slow-cohesive` | Renderer F5 / `--preset slow-cohesive` | Slower schooling behavior |
| `peaceful-flocks` | `SimConfig.PeacefulFlocks()` | Original neutral-group scenario |
| `warbands` | `SimConfig.Warbands()` | Original hostile-group scenario; combat fields remain reserved |
| `rapid-evolution` | `SimConfig.RapidEvolution()` | Original reproduction/mutation recipe; those systems remain reserved |

The balanced factory, named resolver scenario, and bundled balanced file resolve to identical
`SimConfig` values under class defaults. The three older factories also match their named
resolver scenarios.

Similar filenames do **not** imply identical settings. `configs/peaceful.json` is a file
overlay with `MaxSpeed: 8`, `MaxForce: 1.5`, and `Friction: 0.98`; the original
`peaceful-flocks` scenario uses `10`, `2`, and `1`. Likewise, the warbands file uses
`MaxSpeed: 12` and `AttackDamage: 15`, whereas the original `warbands` scenario inherits
the class speed ceiling of `200` and sets `AttackDamage: 20`. Use the named scenario for
the original factory values, or the file layer for the authored recipe.

The renderer accepts its five existing `--preset` IDs, and `--config` reads `SimConfig` JSON in
either engine mode. Startup now uses the shared resolver: the preset is applied before the file,
and omitted file fields retain that preset's authored values. With no preset or file, the
renderer retains its established default profile. Native `CanonicalWorldSettings` JSON belongs
to the headless CLI and core API, not the renderer's file format. See
[renderer startup configuration](RENDERER_CONFIGURATION.md) for fixed window dimensions and
compatibility choices. A manual F1–F5 window check remains outstanding; automated configuration
parity does not provide visual evidence.

## Aggression matrices and enum values

The public C# type remains `float[,]`, preserving `matrix[groupA, groupB]` and
`GetLength()` callers. A property converter stores it as ordinary JSON row arrays:

```json
{
  "AggressionMatrix": [
    [0, -1],
    [0.5, 0]
  ],
  "BoundaryMode": 0,
  "SpeedModel": 1
}
```

Rows must be non-null and equally sized, and configuration validation requires a square matrix.
The values describe intended group relationships; no combat system consumes them yet. The
converter also works when a manifest embeds a `SimConfig` using ordinary
`JsonSerializer.Serialize`, without custom serializer options.

Enums continue to use numbers for compatibility. String names such as `"Damped"` are not
accepted by these loaders, and undefined numeric enum values are rejected by validation.

| Field | `0` | `1` | `2` |
| --- | --- | --- | --- |
| `BoundaryMode` | `Wrap` (default) | `Reflect` | `Clamp` |
| `SpeedModel` | `ConstantSpeed` (default) | `Damped` | — |

## Tuning recipes

| Parameter | Useful starting range | Effect and limit |
| --- | --- | --- |
| `MaxSpeed` | 5–20 | Legacy speed ceiling; mapped to canonical target speed |
| `MaxForce` | 1–4 | Steering budget; excessive force can cause jitter |
| `SenseRadius` | 60–140 | Perception radius; larger values increase candidate work |
| `GridCellSize` | Near `SenseRadius` | Legacy partition size; queries cover the full sense radius |
| `SeparationRadius` | 15–45 | Protected radius; zero disables legacy separation |
| `SeparationWeight` | 2–12 | Separation influence, with different weighting laws in the two engines |
| `AlignmentWeight` | 1–5 | Tendency to align with neighbor motion |
| `CohesionWeight` | 0.3–3 | Pull toward the local center |
| `Friction` | 0.90–0.99 | Legacy damping only when `SpeedModel` is `Damped` |
| `FieldOfView` | 240–300 | Vision cone in degrees; `360` is omnidirectional |
| `WanderStrength` | 0–1 | Seeded exploration; `0` disables it |

The balanced recipe sets speed `10`, force `2.5`, sense radius `110`, separation radius
`45`, and weights `7.5 / 2.2 / 0.35`. Its authored `WanderStrength: 0.45` is enabled,
while omitted wander strength defaults to zero.

All three legacy bundled recipes omit `SpeedModel`, so their authored friction values are inactive.
Add `"SpeedModel": 1` to a copy to enable legacy damping. Canonical movement maintains its
target speed and has no friction parameter.

All legacy bundled recipes inherit `Seed: 42` unless overridden. Supported external seeds are
`0` through `2147483647`. A seed reproduces trajectories only with a matching binary,
runtime/platform, resolved configuration, timestep, and ordered input events.

## Running a recipe

1. Copy a bundled JSON file and edit the properties that matter to the experiment.
2. Run `dotnet run --project SwarmSim.Render -- --config path/to/your.json --agent-count 5000`.
3. Add `--canonical` to use the canonical engine through the documented `SimConfig` bridge.
4. Adjust the renderer controls and press `P` to print values for your recipe.

Invalid loaded values are errors, not tuning warnings. See
[Parameter Guide](PARAMETER_GUIDE.md) for accepted zero/clamped modes and fields that are still
reserved, and [Controls](CONTROLS.md) for the renderer shortcuts.
