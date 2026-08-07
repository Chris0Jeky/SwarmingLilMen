# CLAUDE.md — SwarmingLilMen

Tier: sandbox (T1) — push free / **merge free** (declared in `.agent-harness/tier.json`; changing
it is an owner decision, and the owner set `merge: free` there on 2026-07-27) · dual-runtime (Codex
reads `AGENTS.md`, a thin adapter pointing here) · `human_todo: null`. Global laws are
auto-injected; none are restated here.

## What this is

Deterministic 2D swarm simulation in C#/.NET 8, ~9k lines: `SwarmSim.Core` (library),
`SwarmSim.Render` (Raylib window + CLI), `SwarmSim.Tests` (xUnit), `SwarmSim.Benchmarks`;
`js-demos/` holds unrelated standalone browser demos. **Two engines coexist on purpose**: legacy
Structure-of-Arrays `Core/World.cs` + `Core/Systems/` drives the default renderer and every
benchmark, and `Core/Canonical/` is its incomplete Reynolds-style per-boid successor (`--canonical`).
"50k agents at 60 FPS" is an unmet target; live state is the verified block atop `PROJECT_STATUS.md`.

**Which engine gets new work.** Legacy is the *default*, not the *target*: new Phase 3+ behavior
belongs in `Core/Canonical/` unless the task explicitly concerns legacy parity, comparison, or
removal. Neither engine is deleted without a recorded migration decision. Building a new feature on
the legacy default because it is what runs today is exactly the drift this split exists to prevent.

## Build, test, run — all green 2026-07-27 at `f68cccc` (SDK 8.0.415, Windows)

```powershell
dotnet build SwarmingLilMen.sln -c Release                          # 3 s · 0 warnings / 0 errors
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true   # 80 passed / 2 s — this IS the CI gate
dotnet run --project SwarmSim.Render -c Release --no-build -- --benchmark --agent-count 2000       # headless 600 ticks + kinematic hash
```

`--no-build` above is valid **only** as written — immediately after the build on line 1. It runs the
previously compiled assemblies, so after any source or test edit it can pass while the current
sources fail to compile or behave differently. Rebuild first, or drop the flag.

Narrowest seam check — rebuilds, and errors instead of silently matching zero tests when a class is
renamed or a filter goes stale:

```powershell
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter "FullyQualifiedName~<Class>" -- RunConfiguration.TreatNoTestsAsError=true
```

Classes and counts: CanonicalBoidsTests (12), UniformGridTests (14), WorldTests (13),
SimulationRunnerTests (6), CommandLineOptionsTests (2), ConfigTests (2), BoidsTests (8 — use
`~SwarmSim.Tests.BoidsTests`; the bare substring also catches Canonical); or `"Category=Determinism"`
(14) / `"Category=Performance"` (4, ~18 s, Release only). Renderer coverage is partial (~30%:
DeterminismTests drives `Program`'s canonical-settings helpers) — prove UI paths by running the window.

## Pitfalls

- `TreatWarningsAsErrors` + `Nullable` are on in `Directory.Build.props`; one warning fails CI.
- A green suite proves nothing about throughput: `Category=Performance` gates machine-relative
  ratios only and its absolute numbers are reported-only. Never quote them as the target met.
- Determinism is the product: seeded `Rng`, fixed timestep, stable ordering, no wall clock. A
  `SimConfig` default change must also land in `configs/*.json` and `docs/PARAMETER_GUIDE.md`.
- Snapshot interpolation needs matching capture/mutation versions and array lengths; world
  mutation outside `SimulationRunner.Advance()` routes through `NotifyWorldMutated()`.
- Complexity hotspots `SwarmSim.Render/Program.cs` (1,951 lines) and `Canonical/CanonicalWorld.cs` (680): add a narrow testable seam rather than growing them.
- Never stash/reset/clean/switch a checkout to get a clean tree — T1's floor does not guard work-loss, and the main checkout is usually parked on a live feature branch.
