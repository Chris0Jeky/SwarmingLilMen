# AGENTS.md - SwarmingLilMen operating guide

This is the repository contract for Codex and other coding agents. `CLAUDE.md` is the Claude
entrypoint and points back here for shared project rules. Current state lives in
`PROJECT_STATUS.md`; historical plans are context, not proof.

## Runtime and authority

- Tier: T1 sandbox, public repository, dual runtime (Claude and Codex). The declaration is
  `.agent-harness/tier.json`.
- Work inline by default. Use another agent only for a disjoint read-only review or when the user
  explicitly asks for parallel work. One writer owns the checkout.
- Reversible repository edits, local restores/builds/tests, small commits, pushes, and draft PRs
  are in scope. Destructive operations, deployments, secrets, production data, and public sharing
  outside the existing repository need explicit user scope.
- Preserve unrelated user changes. Never stash, reset, clean, restore, or switch branches merely
  to obtain a clean tree.
- The Codex deny-floor adapter is `.codex/hooks.json`. It is a tripwire only and is not active until
  its exact definition is reviewed and trusted through `/hooks` in a fresh Codex session.

## Mission and current direction

SwarmingLilMen explores high-performance deterministic 2D swarm simulation in C#/.NET 8. The
headline goal is emergent behavior at 50k-100k interactive agents and 1M+ headless agents, with
observable, reproducible behavior and allocation-conscious hot paths.

Two implementations coexist:

- `SwarmSim.Core/World.cs` plus `Systems/` is the legacy SoA pipeline used by the default renderer
  and current BenchmarkDotNet suite.
- `SwarmSim.Core/Canonical/` is the intended future direction: Reynolds-style composable steering,
  fixed speed/turn constraints, spatial-index abstraction, and richer instrumentation. It runs with
  `--canonical` but is not yet the default.

New Phase 3+ behavior belongs on the canonical path unless the task explicitly concerns legacy
parity, comparison, or removal. Do not delete either implementation without a recorded migration
decision.

## Repository map

- `SwarmSim.Core/` - simulation, configuration, deterministic RNG, snapshots, fixed-step runner.
- `SwarmSim.Core/Canonical/` - canonical boids world, rules, spatial indexes, instrumentation.
- `SwarmSim.Render/` - Raylib UI and CLI; `Program.cs` is currently a large integration seam.
- `SwarmSim.Tests/` - xUnit behavior, determinism, CLI/config, and timing-oriented tests.
- `SwarmSim.Benchmarks/` - BenchmarkDotNet coverage for the legacy world and uniform grid.
- `js-demos/` - independent browser demonstrations (boids, Vicsek, ACO, PSO).
- `PROJECT_STATUS.md` - named live-state file. Read its top verified-state section first.
- `IMPLEMENTATION_EVOLUTION.md` and `PHASE_3_READINESS_CHECKLIST.md` - migration rationale and
  remaining canonical work; validate their claims against code/tests before repeating them.

## Commands

```powershell
dotnet restore SwarmingLilMen.sln
dotnet build SwarmingLilMen.sln --configuration Release
dotnet test SwarmingLilMen.sln --configuration Release
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj --filter "FullyQualifiedName~CanonicalBoidsTests"
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj --collect:"XPlat Code Coverage"
dotnet run --project SwarmSim.Render -- --help
dotnet run --project SwarmSim.Render -- --canonical
dotnet run --project SwarmSim.Benchmarks --configuration Release
```

Always benchmark in Release. Renderer launch, visual behavior, profiler captures, and full
BenchmarkDotNet runs are separate evidence; ordinary unit-test success does not prove them.

## Invariants

1. Simulation behavior is deterministic for a fixed seed/configuration/timestep. Do not introduce
   wall-clock randomness or unstable iteration order into the core.
2. Hot paths avoid avoidable allocations, LINQ, boxing, exceptions for flow control, and opaque
   virtual dispatch. Measure before claiming zero allocation.
3. Configuration defaults and presets stay synchronized with docs and JSON examples.
4. Snapshot interpolation is valid only when capture/mutation versions and array lengths are
   compatible. World mutations outside `SimulationRunner.Advance()` call
   `NotifyWorldMutated()` through the renderer refresh path.
5. Public APIs use nullable annotations and XML documentation; warnings are errors.
6. New agent behavior includes deterministic behavior tests and, where meaningful, property or
   equivalence tests.
7. The 50k/60 FPS objective is not currently enforced by the test suite: timing tests may print a
   warning and still pass. Report measured performance separately from pass/fail status.

## Current high-risk seams

- Canonical milestones 8-10 remain incomplete: boundary/reflection coverage, grid-vs-naive
  equivalence, scale properties/metrics, multi-group behavior, and canonical benchmarks.
- The renderer is a 1,800+ line static integration class; changes require narrow scope and a visual
  or CLI-specific check in addition to tests where applicable.
- Legacy and canonical behavior/configuration can drift because both remain live.
- GitHub Actions supplies Release build and non-performance test checks on ubuntu and windows.
  With no branch-protection ruleset, the exact-head CI and independent-review merge gates remain
  process-enforced. Pull-request jobs validate GitHub's head/base merge ref; incorporate the latest
  `main` before merge, and use `workflow_dispatch` when an exact branch-head rerun is needed.
  Coverage, performance, and renderer evidence are separate.
- README/status history contains stale milestone and performance claims. The verified-state section
  at the top of `PROJECT_STATUS.md` wins until broader documentation is reconciled.

## Workflow and completion

1. Inspect instructions, `git status --short --branch`, the top of `PROJECT_STATUS.md`, and the
   files/tests for the requested seam.
2. Make the smallest coherent change. Preserve hot-path and determinism constraints.
3. Run the narrowest proving check, then the solution gate when warranted. Core changes also run
   coverage; tick-loop changes need Release benchmarks and allocation evidence.
4. Review the diff for unrelated edits and update `PROJECT_STATUS.md` when verified facts or the
   priority queue change.
5. Use conventional, present-tense commits (`feat:`, `fix:`, `perf:`, `docs:`, `test:`, `chore:`).
   Do not add agent-attribution trailers.
6. Close with changed / verified / not verified / failures or workarounds / residual risk / next
   safe slice. Never turn warnings, stale prose, or unrun checks into success claims.
