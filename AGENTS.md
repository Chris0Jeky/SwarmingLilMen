# Repository Guidelines

## Project Structure & Module Organization
`SwarmingLilMen.sln` ties together four primary projects: `SwarmSim.Core` (SoA simulation engine), `SwarmSim.Render` (Raylib visualization shell), `SwarmSim.Tests` (xUnit test suite), and `SwarmSim.Benchmarks` (BenchmarkDotNet harness). Docs and helper scripts live under `filesAndResources/`. Keep new runtime assets inside `SwarmSim.Render/assets` (create if needed) and preserve deterministic presets in `SwarmSim.Core/SimConfig*.cs`.

## Build, Test, and Development Commands
- `dotnet restore` – pull NuGet dependencies across the solution.
- `dotnet build` – compile all projects; treat warnings-as-errors for core changes.
- `dotnet run --project SwarmSim.Render` – launch the interactive renderer (Debug for iteration, Release for perf checks).
- `dotnet run --project SwarmSim.Benchmarks -c Release` – gather benchmark baselines; never run in Debug.
- `dotnet test` or `dotnet test --filter "FullyQualifiedName~RngTests"` – execute all or targeted xUnit suites; add `-v detailed` when diagnosing CI issues.

## Coding Style & Naming Conventions
Follow the repo’s `Directory.Build.props`: file-scoped namespaces, tabs converted to 4-space indentation, `PascalCase` for types/methods/properties, `_camelCase` for private fields, and `camelCase` locals. Public APIs require XML docs, and hot paths must avoid allocations, LINQ, and virtual dispatch; prefer `readonly struct` patterns and explicit loops. Nullable reference types are enabled—resolve warnings rather than suppressing them.

## Testing Guidelines
Place tests beside the feature they cover inside `SwarmSim.Tests` (e.g., `WorldTests`, `GenomeTests`). Name facts descriptively: `Component_Action_Expectation`. Favor deterministic seeds so results mirror `World` behavior. Before opening a PR, run `dotnet test --collect:"XPlat Code Coverage"` if you touched core systems and add property/determinism tests whenever a new agent rule is introduced. Failures should be reproducible with `dotnet test -v detailed`.

## Commit & Pull Request Guidelines
Use conventional commits (`feat:`, `fix:`, `perf:`, `docs:`) that summarize the change scope, and keep branches prefixed by feature or bug identifiers (e.g., `feature/boids-grid`). Each PR should include: problem summary, approach, validation commands, and screenshots or perf tables when touching renderer or benchmarks. Update `PROJECT_STATUS.md` for milestone progress and reference any relevant design notes (e.g., `CLAUDE.md`) in your description so reviewers can trace assumptions.

## Performance & Configuration Tips
Benchmark any change affecting the tick loop and capture allocations via Rider/dotTrace before and after. When testing large swarms, seed configurations with `SimConfig.Warbands()` or other presets to guarantee reproducibility. Keep configuration defaults inside `SimConfig` so presets stay synchronized with documentation and Quickstart examples.
