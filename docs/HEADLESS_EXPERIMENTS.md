# Headless experiments

`SwarmSim.Cli` runs fixed logical ticks and writes a reproducible experiment record without
opening a window. It references only `SwarmSim.Core`; Raylib and the renderer are not in its
dependency graph. The default engine is canonical.

## Run a single experiment

```sh
dotnet run --project SwarmSim.Cli -c Release -- --scenario balanced --seed 7 --ticks 600 --agents 400 --out runs/
dotnet run --project SwarmSim.Cli -c Release -- --config configs/canonical-balanced.json --seed 7 --ticks 600 --engine canonical --out runs/
dotnet run --project SwarmSim.Cli -c Release -- --config configs/balanced.json --seed 7 --ticks 600 --engine legacy --out runs/
```

Configuration follows **engine defaults → named scenario → native JSON file → explicit seed**.
Canonical files use `TargetSpeed`; legacy files use `MaxSpeed`. Unknown file fields are errors,
so passing a legacy file to the canonical engine fails before output is created. Use the shared
`--scenario balanced` name when you want the authored tuning applied to either engine. See the
[configuration cookbook](CONFIGURATION_COOKBOOK.md) and
[parameter mapping](PARAMETER_GUIDE.md#legacy-to-canonical-mapping) for the differences that remain.

| Option | Default | Meaning |
| --- | --- | --- |
| `--engine legacy\|canonical` | `canonical` | Selects the engine and native file schema |
| `--scenario NAME` | None | Shared named configuration overlay |
| `--config FILE` | None | Sparse native configuration file |
| `--seed N` | Resolved configuration seed | Integer from 0 through 2147483647 |
| `--ticks N` | `600` | Non-negative logical step count; zero records initial state only |
| `--agents N` | `400` | Positive initial population; must fit resolved initial capacity |
| `--out DIRECTORY` | `runs` | Parent of a new run directory; existing runs are preserved |
| `--manifest FILE` | None | Replays a completed manifest into a new run directory |
| `--help` | — | Prints usage |

Duplicate options, unknown options, malformed numeric values, and unexpected positional arguments
are usage errors. Exit codes are `0` for success, `1` for configuration/replay/I/O/run failure,
and `2` for invalid command-line usage. Errors go to standard error; success prints the new run
directory to standard output.

## Run-directory contract

Each successful run publishes a new `runs/run-<random-id>/` child containing exactly:

- **`manifest.json`** — Schema version 1, run ID and creation UTC, engine, seed, tick count,
  agent count, fixed timestep, complete resolved native configuration, requested metrics,
  spawn-model version, hash version, final kinematic hash, CSV SHA-256, runtime identity,
  Git commit and dirty-worktree status.
- **`metrics.csv`** — Deterministic UTF-8 data with LF line endings and invariant decimal
  formatting, including under cultures such as `fr-FR`.
- **`log.txt`** — Run settings, output hashes, and elapsed run time. Timing is operational
  information and is excluded from deterministic metrics.

`gitCommit` comes only from a validated revision embedded in the CLI assembly at build time:
`SourceRevisionId` assembly metadata, or the revision appended to its informational version by
the .NET SDK. It accepts full 40- or 64-character hexadecimal revisions. A build without an
embedded revision, such as a typical uploaded ZIP build, records `null`. Moving the binary or
launching it from a different repository cannot change that identity.

`dirtyWorktree` is `null` because the current build does not embed reliable build-time dirtiness.
The runner never infers binary provenance or cleanliness from the working directory at execution.
A known commit with unknown dirtiness is valid; it identifies the recorded source revision and
does not prove that the binary was built from a clean checkout.
Creation time and run ID differ between repetitions; only the deterministic CSV and state/hash
contract are expected to match.

The runner validates options, configuration, population capacity, and construction before creating
output. It writes into a fresh temporary child and publishes the completed directory by rename.
Files use create-new semantics. A failure removes that attempt's staging directory and never
overwrites a prior run. Generated experiments stay local and are ignored by Git; committing a run
as a fixture is a separate deliberate decision.

## Metrics and initialization

The CSV columns are:

```csv
tick,population,meanSpeed,polarization
```

It contains tick `0` immediately after spawning, then one row after each step through the requested
tick count. Population counts live agents. Mean speed is the arithmetic mean of velocity
magnitudes. Polarization is the magnitude of the mean unit heading, with stationary agents
contributing a zero heading and an empty population reporting zero. It is bounded to `[0, 1]`.
No frame time, wall clock, rendering interpolation, or throughput measurement enters these rows.

The versioned initialization model uses the resolved seed and `Core.Rng` to generate uniform
positions across the configured world and uniform random headings, all in group zero. Agents
start at the selected engine's configured speed. This is an experiment initialization contract,
not the renderer's four-cluster spawning layout.

The final kinematic hash uses `SimulationKinematicHash` version 1: SHA-256 of ordered agent count
and exact position/velocity float bits. It does not include RNG state, groups, energy, configuration,
or every internal engine field, and is not a complete restart checkpoint. The manifest separately
pins configuration and the full CSV hash.

## Replay

```sh
dotnet run --project SwarmSim.Cli -c Release -- --manifest runs/run-<id>/manifest.json --out replays/
```

Replay validates the schema, supported spawn/metric/hash versions, complete native configuration,
and consistency of seed/timestep metadata. Unknown fields and duplicate object fields are errors,
including nested runtime/configuration fields; duplicate-name checks are case-insensitive and run
before deserialization so no earlier value can be silently replaced. It executes the recorded
settings and verifies both the final kinematic hash and the complete metrics-file hash before
publishing a new run directory.
The source manifest is read-only. Manifest content never chooses an output path.

`--manifest` may be combined only with `--out` or `--help`; conflicting run-setting flags are
rejected even when they happen to match the recorded values. To change an experiment, start a new
run with a config/scenario and explicit overrides.

Replay promises are scoped to a matching binary, .NET runtime/platform, configuration, and
initialization contract. The manifest records runtime and source provenance to make those limits
visible. A changed runtime or implementation may fail hash verification; matching a seed alone
is not a cross-platform determinism guarantee.

This runner provides single runs and manifest replay. Parameter sweeps, metric registries, and
experiment comparison are separate work; there is no hidden scheduler or batch execution here.

## Migrating the renderer benchmark flag

`SwarmSim.Render --benchmark` prints a deprecation notice and exits with code `0`; it no longer
executes the old legacy-only 600-tick loop. Use this runner for recorded experiments, selecting
`--engine legacy` to retain the legacy engine. For performance measurements, use
`dotnet run --project SwarmSim.Benchmarks -c Release -- --canonical`.
