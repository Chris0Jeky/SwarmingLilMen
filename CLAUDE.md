# CLAUDE.md — SwarmingLilMen

Tier: sandbox (T1) — push free / **merge free** (declared in `.agent-harness/tier.json`; changing
it is an owner decision, and the owner set `merge: free` there on 2026-07-27) · dual-runtime (Codex
reads `AGENTS.md`, a thin adapter pointing here) · `human_todo: null`.

**If `~/.claude/` is absent, the global laws are NOT loaded.** They are normally auto-injected,
which is why they are not restated here — but in a fresh clone, a CI or cloud container, or any
machine without the estate profile, nothing injects them and no PreToolUse floor is installed.
`AGENTS.md` is a thin Codex adapter in every other respect, but its **Fail-safe floor** section is
runtime-neutral and is then the entire policy: read it, and treat merge as human-only regardless of
what `authority.merge` says, until a human says otherwise. Do not infer from "merge free" that an
unreviewed merge is ever in scope — that dial presumes the laws it is declared under.

## What this is

Deterministic 2D swarm simulation in C#/.NET 8: `SwarmSim.Core` (library),
`SwarmSim.Cli` (display-free runs, replay and sweeps), `SwarmSim.Render` (Raylib window),
`SwarmSim.Tests` (xUnit), `SwarmSim.Benchmarks`;
`js-demos/` holds unrelated standalone browser demos. **Two engines coexist on purpose**: legacy
Structure-of-Arrays `Core/World.cs` + `Core/Systems/` drives the default renderer, and
`Core/Canonical/` is its incomplete Reynolds-style per-boid successor (`--canonical`). Both engines
have headless experiments and BenchmarkDotNet entry points.
"50k agents at 60 FPS" is an unmet target; live state is the verified block atop `PROJECT_STATUS.md`.

**Which engine gets new work.** Legacy is the *default*, not the *target*: new Phase 3+ behavior
belongs in `Core/Canonical/` unless the task explicitly concerns legacy parity, comparison, or
removal. Neither engine is deleted without a recorded migration decision. Building a new feature on
the legacy default because it is what runs today is exactly the drift this split exists to prevent.

## Build, test, run

`global.json` selects .NET 8. Test counts and measured platform evidence live in `PROJECT_STATUS.md`;
these commands remain the executable gates.

```powershell
dotnet build SwarmingLilMen.sln -c Release
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project SwarmSim.Cli -c Release --no-build -- --engine canonical --agents 400 --ticks 600 --seed 7 --out runs
```

`--no-build` above is valid **only** as written — immediately after the build on line 1. It runs the
previously compiled assemblies, so after any source or test edit it can pass while the current
sources fail to compile or behave differently. Rebuild first, or drop the flag.

Narrowest seam check — rebuilds, and errors instead of silently matching zero tests when a class is
renamed or a filter goes stale:

```powershell
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter "FullyQualifiedName~<Class>" -- RunConfiguration.TreatNoTestsAsError=true
```

Useful seam filters include `SpatialIndexEquivalenceTests`, `UniformGridTests`, `WorldTests`,
`DeterminismTests`, `GoldenTraceTests`, `CanonicalBoidsTests`, `SimulationRunnerTests`,
`CanonicalSteeringBudgetTests`, and `ConfigTests`. Use `~SwarmSim.Tests.BoidsTests` to avoid also
matching canonical tests. **Run `~RngTests` explicitly for RNG changes**: it includes legacy,
PCG and named-stream tests; the `Determinism` category alone omits several bounds/sequence tests.
`Category=Performance` is opt-in and Release-only.

Evidence the commands above do **not** produce, each needing its own run:

```powershell
dotnet run --project SwarmSim.Benchmarks -c Release -- --canonical      # real canonical BenchmarkDotNet evidence
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --collect:"XPlat Code Coverage"
dotnet run --project SwarmSim.Render -c Release -- --canonical         # opens the window; the only proof of UI paths
```

Headless tests cover renderer configuration, telemetry and command handling; they do not prove
graphics or native input. Actual UI behavior requires running the window. Renderer `--benchmark`
prints migration guidance; use `SwarmSim.Cli` for experiments and `SwarmSim.Benchmarks` for timing.

For canonical overlay/control changes, also run `dotnet run --project tools/RendererProbe -c Release`.
It asserts native event playback and pixels, then prints an HTML gallery and JSON report under
`artifacts/renderer-probe/`. Linux CI runs it with Mesa/Xvfb. Inspect changed captures; do not confuse
this controlled render-target proof with OS keyboard/focus, full-window DPI or human acceptance.
See `docs/CANONICAL_INSPECTION.md` for scope and failure interpretation.

## Rules that bind

These lived in the old long-form `AGENTS.md`. Claude does not read `AGENTS.md`, so they are stated
here rather than referenced.

- **Hot paths avoid avoidable allocations, LINQ, boxing, exceptions for flow control, and opaque
  virtual dispatch.** `Directory.Build.props` global-imports `System.Linq` into every file, so
  nothing stops you — measure before claiming zero allocation.
- **New behavior ships with deterministic tests**, plus a property or equivalence test where one is
  meaningful. Do not land new agent behavior on assertion-free evidence.
- **Update `PROJECT_STATUS.md` when a verified fact or the priority queue changes** — test counts,
  measured hashes, implementation status. Its top block is what the validator and the rest of the
  docs are told to trust, so a change that leaves it stale silently misleads every later session.
- **Do not vendor a `.claude` hook into this repo.** Whether a user-scope floor runs depends on the
  host (none on DESKTOP-IHKOOJS; Kraspyon keeps one), and a second repo-level hook would double-dispatch
  there. Act as if nothing catches an irreversible command.
- **Committed permissions belong in `.claude/settings.json`; personal overrides belong in gitignored
  `.claude/settings.local.json`; bypass comes only from user settings or a launch flag.** A project
  or local `defaultMode` of bypass is ignored (Claude Code 2.1.257+), so widening committed
  permissions is a repo-wide decision, not a personal convenience.
- **CI validates both GitHub's head/base merge ref and the exact PR head** on Ubuntu and Windows.
  Incorporate the latest `main` before merging; inspect the exact-head SHA and logs as well as the
  integration result. Golden-trace records make the measured platform/runtime band explicit.

## Pitfalls

- `TreatWarningsAsErrors` + `Nullable` are on in `Directory.Build.props`; one warning fails CI.
- A green suite proves nothing about throughput: `Category=Performance` gates machine-relative
  ratios only and its absolute numbers are reported-only. Never quote them as the target met.
- Determinism is the product: named `RandomStreams` for new consumers (legacy retains `Rng`),
  fixed timestep, stable ordering, no wall clock. See `docs/RANDOM_STREAMS.md` for the byte contract
  and per-consumer ownership rules.
  Exact replay is scoped to the recorded implementation/configuration/platform/runtime band.
  Canonical Windows and Linux traces differ and have separately reviewed exact fixtures; legacy
  and RNG fixtures currently match. See `docs/DETERMINISM.md`; never widen a tolerance or silently
  regenerate a reference to make drift pass. A
  `SimConfig` default change must also land in `docs/PARAMETER_GUIDE.md` and in any JSON example
  written to demonstrate that field. Do **not** copy it into `configs/*.json`: those recipes set
  17-19 of the 55 properties deliberately and inherit the rest through `SimConfig.LoadFromJson`, so
  pinning a new default there freezes them against later fixes and can change a named scenario.
- Snapshot interpolation needs matching capture/mutation versions and array lengths; world
  mutation outside `SimulationRunner.Advance()` routes through `NotifyWorldMutated()`.
- Complexity hotspots `SwarmSim.Render/Program.cs` (~2,010 lines) and `Canonical/CanonicalWorld.cs` (~755): add a narrow testable seam rather than growing them. Both grow steadily — treat the figures as scale, not as a value to keep in sync.
- Never stash/reset/clean/switch a checkout to get a clean tree — no floor guards work-loss here, and the main checkout is usually parked on a live feature branch.
