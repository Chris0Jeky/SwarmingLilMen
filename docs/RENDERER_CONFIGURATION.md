# Renderer startup configuration

The renderer uses the shared configuration resolver at startup. When both **--preset** and
**--config** are supplied, the named preset is applied first and the JSON file overrides only
the properties it contains. The explicit **--agent-count** argument then selects the starting
population. It does not rewrite `InitialCapacity` in the configuration.

```bash
dotnet run --project SwarmSim.Render -- --preset balanced --config my-tuning.json --agent-count 800
dotnet run --project SwarmSim.Render -- --canonical --preset balanced --config my-tuning.json --agent-count 800
```

For example, a file containing `{"MaxSpeed":12,"MaxNeighbors":8}` retains the balanced preset's
other authored values while selecting those two settings. The old file-only branch ignored the
preset whenever both arguments were present.

## Compatibility choices

The resolver's order is defaults, named scenario, file, then explicit overrides. The renderer
selects its default profile according to the existing launch behavior:

| Launch | Starting defaults | Later layers |
| --- | --- | --- |
| No preset or file | Existing renderer defaults, including disabled wander and a 1/60-second step | Population argument |
| Preset, with or without a file | `SimConfig` defaults | Named preset, optional file, population argument |
| File without a preset | `SimConfig` defaults | File, population argument |

This retains the existing values for unauthored launches, each standalone preset, and each
standalone file. A partial file alone still inherits `SimConfig` defaults; it does not silently
inherit the renderer's different default tuning.

The same five preset IDs, display-name aliases, list output, and F1–F5 assignments remain in
place. The interactive preset definitions are unchanged. The core resolver and headless CLI
also expose other named scenarios; this change does not add them to the renderer's five-entry
preset menu.

An unknown preset now fails even if a file is also supplied. Renderer configuration files use
the `SimConfig` vocabulary. Unknown fields and misspellings are rejected by the shared resolver.
In particular, `--canonical --config` still takes a `SimConfig` file with `MaxSpeed`; native
`CanonicalWorldSettings` files with `TargetSpeed` belong to the headless CLI or core API.

## Window and engine mapping

The window and rendered simulation remain fixed at **1920 × 1080**. File values for `WorldWidth`
and `WorldHeight` are read and validated, but the renderer's existing world-construction step
replaces them with the window dimensions. Use the headless CLI or core API for experiments whose
world dimensions come directly from the native configuration.

The canonical renderer now calls `CanonicalWorldSettings.FromSimConfig` instead of maintaining
a second copy of the bridge. It preserves the established numeric clamps and the renderer's
capacity calculation, `max(initialAgentCount * 2, 1024)`. The detailed mapping and engine-specific
omissions are documented in [PARAMETER_GUIDE.md](PARAMETER_GUIDE.md#legacy-to-canonical-mapping).

The renderer's template and live-configuration copies now retain `MaxNeighbors`,
`SeparationCrowdingThreshold`, `SeparationCrowdingBoost`, `CollisionAvoidanceRadius`, and
`CollisionAvoidanceBoost`. Previously both copy paths dropped those five settings and replaced
them with defaults. Custom files that authored those values will now take effect. The registered
presets already use the corresponding defaults, so their values are preserved. Crowding and
collision-avoidance fields remain legacy-specific; `MaxNeighbors` also reaches the canonical
bridge.

## Verification

```bash
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter FullyQualifiedName~RendererConfigurationTests -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project SwarmSim.Render -c Release -- --list-presets
```

The tests compare every property of each registered F-key configuration with startup resolution,
check the balanced file/factory/preset paths, verify preset/file precedence and strict errors,
pin the preset-list transcript and fixed window dimensions, and exercise the canonical bridge's
capacity and clamps. Ten regression cases first demonstrated the five lost fields in both copy
paths before their forwarding was fixed.

Manual F1–F5 application remains unverified in the display-free development environment.
On an interactive machine, check those keys in both renderer modes, alongside reset and live
parameter adjustment. Automated configuration parity does not establish visual behavior.
