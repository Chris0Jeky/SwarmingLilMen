# CLAUDE.md - SwarmingLilMen

Tier: T1 sandbox, public repository, dual runtime (Claude and Codex). Authority and flags live in
`.agent-harness/tier.json`. `AGENTS.md` is the shared project operating guide and wins if this thin
Claude entrypoint drifts from it.

## Start here

1. Read `AGENTS.md`.
2. Inspect `git status --short --branch` and recent commits.
3. Read the verified-state section at the top of `PROJECT_STATUS.md`.
4. For simulation work, read the relevant implementation and tests. For migration decisions, also
   read `IMPLEMENTATION_EVOLUTION.md` and `PHASE_3_READINESS_CHECKLIST.md`.
5. Run a baseline check before changing behavior.

## Project in one paragraph

SwarmingLilMen is a deterministic C#/.NET 8 swarm-simulation research project targeting rich
emergent behavior and very large agent counts. The default renderer and existing benchmarks use a
legacy Structure-of-Arrays `World`/systems pipeline. The intended future path is
`SwarmSim.Core/Canonical`, a composable Reynolds-style implementation available through
`--canonical`. The canonical migration, multi-group model, Phase 3 survival/combat mechanics, and
performance validation are incomplete.

## Essential commands

```powershell
dotnet restore SwarmingLilMen.sln
dotnet build SwarmingLilMen.sln --configuration Release
dotnet test SwarmingLilMen.sln --configuration Release
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj --filter "FullyQualifiedName~CanonicalBoidsTests"
dotnet run --project SwarmSim.Render -- --help
dotnet run --project SwarmSim.Render -- --canonical
dotnet run --project SwarmSim.Benchmarks --configuration Release
```

Use Release for any performance statement. A green test run does not prove the 50k/60 FPS goal:
some timing tests are observational and emit warnings without failing.

## Claude-specific safety

- The shared global Claude PreToolUse hook supplies the irreversible-command floor. Do not vendor a
  second Claude hook into this repo; duplicate hooks can double-dispatch.
- Committed permissions live in `.claude/settings.json`. Personal bypass choices belong in
  gitignored `.claude/settings.local.json`.
- Never commit secrets, tokens, generated profiler/test output, private data, or agent-attribution
  trailers.
- Work inline at T1 unless the user explicitly requests delegation or a read-only independent lens
  is materially useful. Keep one writer for this checkout.
- Small, evidence-backed diffs are preferred. Do not treat historical session prose as live proof.

## Architecture guardrails

- New Phase 3+ behavior targets the canonical implementation unless the task explicitly says
  otherwise.
- Preserve deterministic seeds, fixed timesteps, stable ordering, snapshot mutation-version rules,
  and allocation-conscious hot paths.
- Do not claim canonical/legacy parity, zero allocations, renderer correctness, or target-scale
  performance without the check that directly proves that claim.
- `Program.cs` and `CanonicalWorld.cs` are complexity hotspots. Avoid expanding them when a narrow,
  testable seam exists.

## Current priority

Complete canonical readiness before Phase 3: boundary tests/reflection decision, spatial-index
equivalence, scale properties/metrics, multi-group semantics, canonical benchmarks/allocation
measurement, then a documented migration decision. Keep `PROJECT_STATUS.md` synchronized with
verified evidence.
