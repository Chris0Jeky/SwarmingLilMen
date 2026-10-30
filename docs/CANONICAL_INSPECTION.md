# Canonical inspection

The canonical renderer uses **S / A / C** for the named separation / alignment / cohesion rules.
**8** selects FOV; arrows adjust by 15 degrees, or 1 degree with Shift, within 1–360 degrees.
ON/OFF labels stay visible when inspection is hidden. Parameter/preset recreation and reset preserve
the switches. Weights retain their existing meaning; zero weight is not a disable action.
See [controls](CONTROLS.md).

## Reading the overlay

**O** shows the selected boid's inspection; **Tab** selects another. Diagnostic geometry uses the
**last completed tick's input positions**. Moving green dots are current boids; outlined neighbor
markers and the gold selected marker identify the inspected input positions.

- Gold: the heading-centered FOV arc at the sense radius; a full circle at 360°.
- White arrow: the resolved steering vector before velocity shaping and speed normalization.
  Length is 64 pixels per MaxForce, visually capped at 128 pixels. Zero force has no arrow.
- Gray arrow/circle: heading and whisker geometry, without claiming a whisker force was applied.
- Orange, blue and violet parallel links: weighted inputs to a nonzero **applied** separation,
  alignment or cohesion rule. These are aggregate-rule inputs, not per-neighbor force vectors.
  Separation excludes coincident/tiny-distance inputs and the zero-influence radius boundary.
- Gray links: visible neighbors without a colored applied contribution. Disabled, zero-result
  and budget-suppressed rules have zero magnitude and no colored links.

The panel reports the tick, three applied magnitudes and resolved-force magnitude. Truncation is
explicit. The query uses the simulation's pre-FOV cap and built-in spatial ordering. Enhanced
separation can reserve the budget and suppress alignment/cohesion while their switches remain ON.
Whisker/wander policies and velocity stages remain independently active.

## Data and cost

`RuleInstrumentation.SteeringVectors` stores the actual force at the existing recording point.
The squared-magnitude API is unchanged. Storage adds **8 bytes per capacity slot**, plus one array
header, without per-tick allocation. `CanonicalWorld.LastTickInputs` borrows the existing double
buffer: empty before the first tick and overwritten by the next Step. `QueryLastTickVisibleNeighbors`
requeries that state into caller-owned buffers without replaying policies or consuming randomness;
it refuses calls during Step. These are synchronous diagnostics, not retained restart snapshots.
Requerying cannot promise that an injected custom spatial index is deterministic; the renderer
uses the tested built-in grid.

Inspection rebuilds/queries the grid only while shown. Warm tests cover stepping plus inspection
without allocation, and repeated queries leave future trajectories exact. This is not an FPS or
unchanged-throughput claim.

## Verification on 2026-10-30

The Windows Release build passed without warnings/errors and the complete non-Performance suite
passed **1,113 tests**. Tests cover key-to-rule effects and independent classic contributions,
round-trip switches, recreation/reset copies, last-tick/current-position divergence, cap/truncation,
late spawns, resolved force before velocity stages, zero warm query allocation and arc geometry at
1°, 90°, 270° and 360°. Existing exact golden fixtures are unchanged. Obsolete current-position
status-string tests were replaced with last-tick data/query tests.

Eight **1920 × 1080 native Raylib render-target images** were inspected on Windows x64, .NET 8,
Raylib 5.5 and Intel Arc OpenGL 3.3, using the production overlay/help methods. The explicit six-boid
scene has seed 7, target speed 10, MaxForce 20, sense radius 240, separation radius 60, Enhanced
composition, alignment weight 0.9, cohesion weight 0.5, no wander and zero whisker weight. Positions
and headings are fixed, not randomly initialized. Each case starts fresh and advances one fixed
tick. The separation pair moves one neighbor inside its radius.

| Capture | Observed result |
|---|---|
| [All rules / 270°](verification/canonical-inspection/all-rules.png) | Blue/violet inputs; separation 0, alignment 1.749, cohesion 5.691. |
| [Separation on](verification/canonical-inspection/separation-on.png) / [off](verification/canonical-inspection/separation-off.png) | Orange input and force 20 become alignment 1.486 / cohesion 6.175 after S. |
| [Alignment off](verification/canonical-inspection/alignment-off.png) | Alignment 0; cohesion remains 5.691. |
| [Cohesion off](verification/canonical-inspection/cohesion-off.png) | Cohesion 0; alignment remains 1.749. |
| [90° FOV](verification/canonical-inspection/fov-90.png) / [360° FOV](verification/canonical-inspection/fov-360.png) | One forward neighbor versus five visible neighbors; sector versus full circle. |
| [Help](verification/canonical-inspection/help.png) | 1–8 parameters and S/A/C/O/Tab fit the existing panel. |

These are native **drawing** checks with synthetic key snapshots routed through the production
inventory, not OS keyboard acceptance. The actual renderer window launched and was captured, but
Windows computer-use key/click injection did not change its state. Direct framebuffer export also
showed a DPI-size mismatch; the explicit render target avoids presenting that as a full-window
capture. Physical keyboard input, full-window DPI behavior, Linux/macOS execution of the new UI and
human acceptance remain unverified. **Issue #40 and milestone 7 remain partial.**

Reproduce the headless checks with:

```powershell
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter "FullyQualifiedName~CanonicalInspectionTests|FullyQualifiedName~RendererControlsTests|FullyQualifiedName~CanonicalRendererTelemetryTests|FullyQualifiedName~GoldenTraceTests" -- RunConfiguration.TreatNoTestsAsError=true
```

Remaining native acceptance: launch `dotnet run --project SwarmSim.Render -c Release -- --canonical`,
hide H, show O, cycle Tab, toggle S/A/C separately, adjust 8 with arrows/Shift and verify switches
survive R and a preset. Keep force versus velocity and input versus current positions distinct.
No human sign-off has been inferred; `human_todo` remains null.
