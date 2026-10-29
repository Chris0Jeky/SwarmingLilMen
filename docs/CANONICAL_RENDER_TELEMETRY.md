# Canonical renderer telemetry

The canonical renderer explicitly captures researcher telemetry once per rendered frame, after
all fixed simulation steps for that frame. Its HUD reads the same completed tick's neighbor,
weight, steering, distance, and separation-priority values as the compatibility snapshot API.
It reuses one `CanonicalTelemetry` buffer instead of allocating three snapshot arrays per frame.
Respawning or changing parameters clears the previous world's diagnostics; a replacement world
with a larger capacity grows the buffer once.

This allocation guarantee applies to the telemetry capture path. HUD text formatting, Raylib,
world recreation, and the rest of rendering are not claimed to be allocation-free.

## Reading the interaction overlay

Press **O** to show the interaction overlay and **Tab** to choose its subject. The existing
status row distinguishes two moments:

- **Now: whisker hits …** describes the drawn links and markers, queried from the boids' current
  positions. A following **query capped at …** belongs to that current-position query.
- **Tick N: query capped at …** reports the selected boid's recorded truncation during completed
  tick N, before integration. **Tick N: no truncation** means that query omitted no candidates.
  Before the first completed tick, the label reads **Tick: pending**. A subject absent from that
  tick is explicitly marked **subject not recorded**.

For example, `Now: whisker hits 0 | Tick 2: query capped at 4` is possible after a dense cluster
disperses: steering used a capped query before the agents moved, and the current view no longer
needs that cap. The two values may also disagree in the opposite direction as agents converge.

The current-position visualization still calls `QueryVisibleNeighbors`, which rebuilds the
spatial index. That work runs only while the interaction overlay is shown. Capturing telemetry
does not query or rebuild the index. Splitting the core query API into explicit refresh/read
operations is outside this renderer change.

Controls, HUD coordinates, font sizes, colors, and simulation settings are unchanged. **F12**
belongs to the legacy renderer's snapshot/debug overlay; the canonical interaction control is
**O**.

## Verification and remaining manual check

The renderer tests exercise the actual capture and status-formatting helpers without opening
a graphics window:

```bash
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter FullyQualifiedName~CanonicalRendererTelemetryTests -- RunConfiguration.TreatNoTestsAsError=true
```

They cover 1,000 warm ticks with zero current-thread allocation from stepping plus reused
capture, existing HUD value parity, reset and capacity changes, differing current/tick query
caps, and subjects without a completed-tick record. They do not prove visual parity or rendered
frame rate.

The manual acceptance check remains outstanding. This change was developed without an
interactive display or virtual framebuffer. On a machine with the Raylib native dependencies
and an interactive graphics session, run:

```bash
dotnet run --project SwarmSim.Render -c Release -- --canonical
```

Confirm the HUD values and layout, toggle **O**, cycle subjects with **Tab**, respawn with **R**,
and change a parameter or preset. Check that the two status labels remain readable and that
recreated worlds do not display the previous world's diagnostics. Separately launch the default
legacy renderer and check **F12** for the issue's legacy overlay parity check.
