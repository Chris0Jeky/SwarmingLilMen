# Native renderer verification

On 2026-10-29, the integrated Release renderer was exercised through real X11 keyboard and mouse events. Console/CSV assertions and inspection of direct 1920 × 1080 captures supplement the headless tests. This records automated native UI verification, not human manual sign-off or Windows/macOS GUI coverage.

The tested build includes the RNG, capacity and behavioral-scenario changes through base commit `16086df94b0b99c6f8822df91830430083cb9104`, plus the controls follow-up. [The manifest](verification/native-ui/manifest.json) records the exact binary/source identities, checks, and screenshot hashes. All seven final launches closed normally with Esc.

## Observed behavior

| Area | Result |
|---|---|
| Both H panels | Complete panels fit on screen. Legacy lists 1–8, identifies friction as Damped-only, and states when it is inactive. Canonical lists 1–7, O/Tab, F1–F5, adjustment, P/R/H/Esc, and no friction control. |
| Canonical key 8 | Select target speed with 6, press 8, then Up: speed changes 10→20. Key 8 did not select a hidden friction slot. |
| F1–F5 in both modes | P output matched every authored value for all five presets, including rule weights and legacy friction. Speeds were 10/10/10/15/5, sense radii 110/100/80/120/80, and maximum forces 2.5/2/2/3/1.5. |
| Custom startup | A sparse file overriding MaxSpeed to 12 retained balanced weights. Authored 80 × 60 dimensions still produced the documented 1920 × 1080 renderer world. Damped mode identified friction as active in H/HUD/console and accepted 0.80→0.81; ConstantSpeed explicitly identified it as inactive. |
| Canonical O/Tab and telemetry | O toggled the drawing; Tab moved it to another boid. A two-second fixed step exposed pending, completed and reset states. Fresh `Now` and captured `Tick` status remained distinct. Authored MaxNeighbors 1 retained the bridge's effective minimum of 4. |
| Legacy F12/spawn/reset | F12 displayed snapshot populations, capture/mutation versions, count delta and interpolation state. Space plus two mouse spawns produced 328 agents, confirmed by CSV and both snapshots. R restored 128 agents. |
| Adjustments and visualization | Up/Down, equal/minus, keypad plus/minus and both Shift keys produced their documented increments. V/N/X responded. S drew circles at 64 agents and suppressed them at 164, with the below-100 limit stated in help. |
| Display labels | The selected-parameter `>`, `Count delta`, and canonical heading's hyphen rendered without the previous missing-glyph placeholders. |

### Simultaneous recreation and spawn

The original build reproduced a real defect for both F1+Space and Up+Space. Starting from 64 agents, the console reported 100 spawns but C still exported 64: later actions had mutated the discarded world. The fixed integrated build exported **164** for both combinations.

To make both events reach one input snapshot, the test briefly stopped only the renderer process, queued both XTest keydown events, resumed it, held the keys for 200 ms, and then released them. No application fields or simulation data were injected. Binary, CSV and log hashes for the before/after cases are in the manifest.

## Selected captures

- [Canonical help](verification/native-ui/canonical-help.png), [legacy help](verification/native-ui/legacy-help.png), and [Damped help](verification/native-ui/legacy-damped-help.png).
- [Legacy 328-agent snapshots](verification/native-ui/legacy-spawn-snapshots.png) and [active Damped friction](verification/native-ui/legacy-damped-friction.png).
- [Pending tick](verification/native-ui/canonical-tick-pending.png), [completed tick](verification/native-ui/canonical-tick-completed.png), and [pending after reset](verification/native-ui/canonical-reset-pending.png).
- [Sense circles at 64 agents](verification/native-ui/legacy-sense64.png) and [suppressed at 164](verification/native-ui/legacy-sense164.png).

The environment was Ubuntu 24.04 x64, Raylib 5.5, GLFW/X11, Mesa llvmpipe with LLVM 20.1.2, and OpenGL 4.5 Core Profile. Software rendering was used. Screenshot FPS counters are not performance evidence.

## Reproduce a basic capture

On Linux with the .NET 8 SDK, Xvfb, xauth, xdotool and ImageMagick available, run from the repository root:

```bash
dotnet build SwarmSim.Render/SwarmSim.Render.csproj -c Release
mkdir -p artifacts/native-ui
xvfb-run -a -s '-screen 0 1920x1080x24 -nolisten tcp' sh -eu <<'SH'
export LIBGL_ALWAYS_SOFTWARE=1
export GALLIUM_DRIVER=llvmpipe
export LP_NUM_THREADS=2
renderer="$(pwd)/SwarmSim.Render/bin/Release/net8.0/SwarmSim.Render.dll"
evidence="$(pwd)/artifacts/native-ui"
cd "$evidence"
dotnet "$renderer" --canonical --agent-count 128 >canonical.log 2>&1 &
renderer_pid=$!
trap 'kill "$renderer_pid" 2>/dev/null || true' EXIT INT TERM
window=$(timeout 15s xdotool search --sync --onlyvisible --pid "$renderer_pid" | head -n 1)
xdotool windowfocus --sync "$window"
sleep 1
import -window "$window" canonical-help.png
xdotool key --clearmodifiers --delay 80 h o Tab
sleep 0.2
import -window "$window" canonical-interactions.png
xdotool key Escape
wait "$renderer_pid"
trap - EXIT INT TERM
SH
```

The readiness delay allows the first input frame to start; a visible window can precede the application's input loop. For legacy mode, omit `--canonical` and press H before capturing help. Use `xdotool mousedown 1`, wait at least 150 ms, then `xdotool mouseup 1` for mouse input across rendered frames. The final verification used 180 ms mouse holds. Keep generated CSV/PNG files in the evidence directory and run visual checks outside performance measurement windows.

The first development trial used shorter mouse pulses that were missed by frame polling; its 228-agent CSV and failed assertion were retained. Longer holds then produced the intended spawns. An early custom-startup H event was also missed. The final integrated run waited for the input loop and verified all three help states successfully.

For the slow telemetry case, launch `--canonical --preset balanced --agent-count 128 --config custom.json` with:

```json
{
  "MaxSpeed": 12,
  "SenseRadius": 300,
  "MaxNeighbors": 1,
  "FieldOfView": 360,
  "FixedDeltaTime": 2,
  "WanderStrength": 0,
  "InitialCapacity": 256,
  "MaxCapacity": 512,
  "WorldWidth": 80,
  "WorldHeight": 60,
  "SeparationCrowdingThreshold": 7,
  "SeparationCrowdingBoost": 3,
  "CollisionAvoidanceRadius": 5,
  "CollisionAvoidanceBoost": 2
}
```

After readiness, hide H, enable O, reset with R, capture pending, wait more than two seconds for the completed tick, then reset and capture again. The renderer-specific world dimensions and capacity clamps remain as documented in [Renderer configuration](RENDERER_CONFIGURATION.md).

## Environment setup and acceptance boundaries

The development session used signature- and hash-verified official Ubuntu packages extracted temporarily because the display tools were absent. System packages were unchanged, archive/TLS verification stayed enabled, and the X server used authentication with TCP disabled. The portable command above lets xvfb-run manage temporary authentication and contains no session credential.

This supplies native rendering evidence for the renderer portions of issues #20/#22/#39. It does not resolve the separate canonical BenchmarkDotNet 5 B/op observation, establish cross-platform GUI parity, or replace human usability/accessibility review.
