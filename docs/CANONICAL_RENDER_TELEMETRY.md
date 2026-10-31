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

Press **O** to show the inspection overlay and **Tab** to choose its subject; the geometry, links
and force arrow are described in [canonical inspection](CANONICAL_INSPECTION.md). It draws from the
last completed tick's recorded inputs, not the moved positions, so its picture matches the forces
the HUD telemetry reports. Its status line is built by
`CanonicalInspectionOverlay.FormatStatus` and keeps the recorded tick separate from the display:

- **Tick N recorded: K inputs, no truncation** (or **simulation query truncated**) is the selected
  boid's `Instrumentation.NeighborCounts` and `QueryTruncated` value for completed tick N.
- **Displayed: D links, cap C** is what the overlay's fixed 128-entry buffers could draw. When
  fewer links are drawn than the tick recorded it adds **reached, links omitted**: with `MaxNeighbors` above 128 the
  tick can record more inputs than are shown.
- **waiting for first completed tick** appears before any tick has completed, and **subject not
  recorded** when the selected boid was spawned after the last tick.

The overlay requeries the spatial index through `QueryLastTickVisibleNeighbors` only while it is
shown; capturing telemetry does not query or rebuild the index.

The HUD gains the S/A/C ON/OFF row and the **8** field-of-view parameter; telemetry fields,
buffers and simulation settings are unchanged. **F12** belongs to the legacy renderer's
snapshot/debug overlay; the canonical inspection control is **O**.

## Verification and remaining manual check

The renderer tests exercise the actual capture helpers without opening a graphics window:

```bash
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter FullyQualifiedName~CanonicalRendererTelemetryTests -- RunConfiguration.TreatNoTestsAsError=true
```

They cover 1,000 warm ticks with zero current-thread allocation from stepping plus reused
capture, existing HUD value parity, and reset and capacity changes. The overlay's status strings,
the 140-boid `MaxNeighbors=256` display-cap case and reset persistence are pinned in
`CanonicalInspectionTests`. None of this proves visual parity or rendered frame rate.

The manual acceptance check remains outstanding. This change was developed without an
interactive display or virtual framebuffer. On a machine with the Raylib native dependencies
and an interactive graphics session, run:

```bash
dotnet run --project SwarmSim.Render -c Release -- --canonical
```

Confirm the HUD values and layout, toggle **O**, cycle subjects with **Tab**, respawn with **R**,
and change a parameter or preset. Check that the status line remains readable, that the inspected
boid survives a parameter change, and that recreated worlds do not display the previous world's
diagnostics. Separately launch the default
legacy renderer and check **F12** for the issue's legacy overlay parity check.
