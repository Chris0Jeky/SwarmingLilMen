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

Deterministic 2D swarm simulation in C#/.NET 8, ~10.7k lines: `SwarmSim.Core` (library),
`SwarmSim.Render` (Raylib window + CLI), `SwarmSim.Tests` (xUnit), `SwarmSim.Benchmarks`;
`js-demos/` holds unrelated standalone browser demos. **Two engines coexist on purpose**: legacy
Structure-of-Arrays `Core/World.cs` + `Core/Systems/` drives the default renderer and every
benchmark, and `Core/Canonical/` is its incomplete Reynolds-style per-boid successor (`--canonical`).
"50k agents at 60 FPS" is an unmet target; live state is the verified block atop `PROJECT_STATUS.md`.

**Which engine gets new work.** Legacy is the *default*, not the *target*: new Phase 3+ behavior
belongs in `Core/Canonical/` unless the task explicitly concerns legacy parity, comparison, or
removal. Neither engine is deleted without a recorded migration decision. Building a new feature on
the legacy default because it is what runs today is exactly the drift this split exists to prevent.

## Build, test, run — all green 2026-08-08 (SDK 8.0.415, Windows)

The counts below are a snapshot on that date and move with every merge; **the command is the source
of truth, not the number beside it.** They are deliberately not stamped with a commit SHA — the
merge commit does not exist yet when the doc is written, and stamping the merge base advertises a
commit at which the recorded count provably does not reproduce.

```powershell
dotnet build SwarmingLilMen.sln -c Release                          # 3 s · 0 warnings / 0 errors
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true   # 114 passed / 2 s — this IS the CI gate
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

Classes and counts: SpatialIndexEquivalenceTests (23), UniformGridTests (14), WorldTests (14),
DeterminismTests (14), CanonicalBoidsTests (12), RngTests (9), CommandLineOptionsTests (7),
SimulationRunnerTests (6), CanonicalSteeringBudgetTests (5), PerformanceTests (4), ConfigTests (2),
BoidsTests
(8 — use `~SwarmSim.Tests.BoidsTests`; the bare substring also catches Canonical); or
`"Category=Determinism"` (14) / `"Category=Performance"` (4, Release only). **`RngTests` carries no
`Category=Determinism` trait**, so an RNG change must run `~RngTests` explicitly — the determinism
category alone skips the bounds, sequence, and integer-generation tests.

Evidence the commands above do **not** produce, each needing its own run:

```powershell
dotnet run --project SwarmSim.Benchmarks -c Release                    # the only real BenchmarkDotNet evidence
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --collect:"XPlat Code Coverage"
dotnet run --project SwarmSim.Render -c Release -- --canonical         # opens the window; the only proof of UI paths
```

Renderer coverage is partial (~30%: DeterminismTests drives `Program`'s canonical-settings helpers),
so UI behaviour is proven only by running the window. Note `--benchmark` measures the **legacy**
world even with `--canonical`, because `RunBenchmark` returns before the canonical branch.

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
- **Do not vendor a `.claude` hook into this repo.** The floor arrives from the global PreToolUse
  hook; a second repo-level hook can double-dispatch. This repo deliberately declares none.
- **Committed permissions belong in `.claude/settings.json`; personal bypasses belong in gitignored
  `.claude/settings.local.json`.** The committed file already sets `defaultMode: bypassPermissions`,
  so widening it further is a repo-wide decision, not a personal convenience.
- **CI validates GitHub's head/base merge ref, not your exact branch head.** Incorporate the latest
  `main` before merging, and use the workflow's `workflow_dispatch` when an exact-head rerun matters.

## Pitfalls

- `TreatWarningsAsErrors` + `Nullable` are on in `Directory.Build.props`; one warning fails CI.
- A green suite proves nothing about throughput: `Category=Performance` gates machine-relative
  ratios only and its absolute numbers are reported-only. Never quote them as the target met.
- Determinism is the product: seeded `Rng`, fixed timestep, stable ordering, no wall clock. A
  `SimConfig` default change must also land in `docs/PARAMETER_GUIDE.md` and in any JSON example
  written to demonstrate that field. Do **not** copy it into `configs/*.json`: those recipes set
  17-19 of the 55 properties deliberately and inherit the rest through `SimConfig.LoadFromJson`, so
  pinning a new default there freezes them against later fixes and can change a named scenario.
- Snapshot interpolation needs matching capture/mutation versions and array lengths; world
  mutation outside `SimulationRunner.Advance()` routes through `NotifyWorldMutated()`.
- Complexity hotspots `SwarmSim.Render/Program.cs` (~2,010 lines) and `Canonical/CanonicalWorld.cs` (~755): add a narrow testable seam rather than growing them. Both grow steadily — treat the figures as scale, not as a value to keep in sync.
- Never stash/reset/clean/switch a checkout to get a clean tree — T1's floor does not guard work-loss, and the main checkout is usually parked on a live feature branch.
