# CLAUDE.md — SwarmingLilMen

Tier: sandbox (T1) — authority: push free / merge gated · dual-runtime (Claude + Codex).
Global laws are auto-injected; none are restated here. Codex reads `AGENTS.md`, a thin adapter
pointing back here. `merge: gated` is *declared* in `.agent-harness/tier.json`, not inherited from
T1 — changing it is an owner decision. No human-action file exists (`human_todo: null`).

## What this is

Deterministic 2D swarm simulation in C#/.NET 8, ~9k lines: `SwarmSim.Core` (library),
`SwarmSim.Render` (Raylib window + CLI), `SwarmSim.Tests` (xUnit), `SwarmSim.Benchmarks`;
`js-demos/` holds unrelated standalone browser demos. **Two engines coexist on purpose**: legacy
Structure-of-Arrays `Core/World.cs` + `Core/Systems/` drives the default renderer and every
benchmark, and `Core/Canonical/` is its incomplete Reynolds-style per-boid successor (`--canonical`).
"50k agents at 60 FPS" is an unmet target; live state is the verified block atop `PROJECT_STATUS.md`.

## Build, test, run — all green 2026-07-27 at `f68cccc` (SDK 8.0.415, Windows)

```powershell
dotnet build SwarmingLilMen.sln -c Release                          # 3 s · 0 warnings / 0 errors
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true   # 80 passed / 2 s — this IS the CI gate
dotnet run --project SwarmSim.Render -c Release --no-build -- --benchmark --agent-count 2000       # headless 600 ticks + kinematic hash
```

Narrowest seam check: `dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --no-build
--filter "FullyQualifiedName~<Class>"` — CanonicalBoidsTests (12), UniformGridTests (14),
BoidsTests (8), WorldTests (13), SimulationRunnerTests (6), CommandLineOptionsTests (2),
ConfigTests (2); or `--filter "Category=Determinism"` (14) / `"Category=Performance"` (4, ~18 s,
Release only). The **renderer has no automated coverage** — prove a `Program.cs` change by running it.

## Pitfalls

- `TreatWarningsAsErrors` + `Nullable` are on in `Directory.Build.props`; one warning fails CI.
- A green suite proves nothing about throughput: `Category=Performance` gates machine-relative
  ratios only and its absolute numbers are reported-only. Never quote them as the target met.
- Determinism is the product: seeded `Rng`, fixed timestep, stable ordering, no wall clock.
- Snapshot interpolation needs matching capture/mutation versions and array lengths; world
  mutation outside `SimulationRunner.Advance()` routes through `NotifyWorldMutated()`.
- `SwarmSim.Render/Program.cs` (1,951 lines) and `Canonical/CanonicalWorld.cs` (680) are the
  complexity hotspots — add a narrow testable seam rather than growing them.
