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
| `--metrics NAME,NAME,...` | `population,meanSpeed,polarization` | Registered channel names, case-sensitive, in exact CSV order |
| `--list-metrics` | — | Lists names, applicability and cost for `--engine`; creates no run |
| `--out DIRECTORY` | `runs` | Parent of a new run directory; existing runs are preserved |
| `--manifest FILE` | None | Replays a completed manifest into a new run directory |
| `--help` | — | Prints usage |

Duplicate options, unknown options, malformed numeric values, and unexpected positional arguments
are usage errors. Exit codes are `0` for success, `1` for configuration/replay/I/O/run failure,
and `2` for invalid command-line usage. Errors go to standard error; success prints the new run
directory to standard output.

## Run-directory contract

Each successful run publishes a new `runs/run-<random-id>/` child containing exactly:

- **`manifest.json`** — Schema version 2, run ID and creation UTC, engine, seed, tick count,
  agent count, fixed timestep, complete resolved native configuration, requested metrics,
  metric-contract version and determinism flag, spawn-model version, hash version, final
  kinematic hash, CSV SHA-256, runtime identity,
  Git commit and dirty-worktree status.
- **`metrics.csv`** — Selected channels as UTF-8 data with LF line endings and invariant decimal
  formatting, including under cultures such as `fr-FR`. Default channels and other deterministic
  selections support exact replay. Explicit runtime-cost channels make the CSV nondeterministic.
- **`log.txt`** — Run settings, output hashes, and elapsed run time. Timing is operational
  information and is excluded from the default metrics. Runtime metric scope is recorded here
  when those channels are selected.

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

The runner validates options, metric names/applicability, configuration, population capacity,
construction, and the initial metric sample before creating output. It writes into a fresh
temporary child and publishes the completed directory by rename.
Files use create-new semantics. A failure removes that attempt's staging directory and never
overwrites a prior run. Generated experiments stay local and are ignored by Git; committing a run
as a fixture is a separate deliberate decision.

## Metrics and initialization

The default CSV columns remain:

```csv
tick,population,meanSpeed,polarization
```

It contains tick `0` immediately after spawning, then one row after each step through the requested
tick count. Population counts live agents. Mean speed is the arithmetic mean of velocity
magnitudes. Polarization is the magnitude of the mean unit heading, with stationary agents
contributing a zero heading and an empty population reporting zero. It is bounded to `[0, 1]`.
No frame time, wall clock, rendering interpolation, or throughput measurement enters these default rows.

### Select and discover channels

```sh
dotnet run --project SwarmSim.Cli -c Release -- --list-metrics
dotnet run --project SwarmSim.Cli -c Release -- --engine legacy --list-metrics
dotnet run --project SwarmSim.Cli -c Release -- --seed 7 --ticks 100 --agents 64 --metrics population,centroidX,centroidY,nearestNeighborP50,nearestNeighborP95 --out analysis/
dotnet run --project SwarmSim.Cli -c Release -- --ticks 100 --agents 400 --metrics population,boids.steeringMagnitudeMean,boids.truncatedQueryFraction --out rules/
```

`--metrics` selects at least one registered name. Names are case-sensitive; duplicates, unknown
names and channels unavailable for the selected engine fail before output. Whitespace around
comma-separated names is trimmed; empty entries are usage errors. The shared `MetricRegistry`
builds the requested plan, and its channel order determines the CSV header and manifest array.
`--list-metrics` may be combined with `--engine` and `--help`, without run-setting arguments.

Both engines expose population, mean speed, polarization, centroid and exact nearest-neighbor
statistics. The canonical engine additionally exposes the `boids.*` instrumentation channels.
Definitions, percentile interpolation, stationary-member convention and missing-value behavior
are specified in [METRICS.md](METRICS.md). The CLI selects live agents from engine state; legacy
dead slots are excluded. Canonical and legacy Wrap worlds use periodic distances/circular
centroids; legacy Reflect and Clamp worlds use Euclidean distances/arithmetic centroids.

**Exact nearest-neighbor channels are opt-in O(N²) analysis plus sorting.** They consider the
complete supplied population and never reuse a truncated steering query. Choosing one of these
channels is suitable only for affordable populations; the CLI does not silently substitute an
approximation or subsample. Its scratch capacity is allocated before output and exhaustion is
an error. Requesting only O(N) channels does not allocate the capacity-sized nearest-neighbor
buffers.

Missing values are empty CSV fields. A singleton has no nearest-neighbor distance; an empty or
antipodally balanced periodic axis can have no defined circular centroid. Canonical `boids.*`
values are missing at tick 0 because no step has completed. From tick 1 onward they describe
the steering observations from that completed step, while position-derived metrics describe
the resulting state. The wrapper preserves the core probe's population-mismatch check after
completed steps and never modifies engine instrumentation to manufacture an initial observation.

### Operational runtime metrics

```sh
dotnet run --project SwarmSim.Cli -c Release -- --ticks 1000 --agents 400 --metrics population,runtime.wallMilliseconds,runtime.allocatedBytes --out operational/
```

These explicit channels measure **the synchronous simulation Step only**, excluding capture,
metric sampling, formatting and file I/O. `runtime.allocatedBytes` is the calling thread's GC
allocation delta; it does not claim process-wide allocation coverage. Tick 0 has no completed
interval and therefore leaves both fields empty. Deterministic plans do not sample per-step
clocks or allocation counters; overall operational run timing is still recorded in `log.txt`.

A run selecting either runtime channel records `metricsDeterministic: false`. Such manifests
are rejected by exact `--manifest` replay before output is created. They remain valid operational
records with an integrity hash, but elapsed times and allocation observations cannot be reproduced
byte for byte. Start a separate run with deterministic channels when exact CSV replay is needed.

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

New runs use schema 2 with `metricContractVersion: 1`, the exact `requestedMetrics` order and
`metricsDeterministic`. The reader validates that classification against the actual registry,
so a manifest cannot relabel runtime channels as deterministic. Replay verifies the entire
selected deterministic CSV, including header/order and missing fields; it is not limited to the
original three columns or the final row. Schema 1 manifests remain supported with their original
fixed `population,meanSpeed,polarization` contract and omitted schema 2 metadata. Replaying one
publishes a schema 2 manifest while preserving the original CSV bytes.

`--manifest` may be combined only with `--out` or `--help`; conflicting run-setting flags are
rejected even when they happen to match the recorded values. To change an experiment, start a new
run with a config/scenario and explicit overrides.

Replay promises are scoped to a matching binary, .NET runtime/platform, configuration, and
initialization contract. The manifest records runtime and source provenance to make those limits
visible. A changed runtime or implementation may fail hash verification; matching a seed alone
is not a cross-platform determinism guarantee.

This runner provides single runs, registry-selected metrics and deterministic manifest replay.
Parameter sweeps and experiment comparison remain separate work.

## Allocation verification

`ExperimentMetrics` allocates its `KinematicSample[]`, output values and plan scratch once.
Each sample copies into that reusable buffer, validates a borrowed frame and invokes the plan.
The shared span-based CSV writer formats numbers without allocating per-number strings.
This is a bounded observer contract; initial setup, manifest/log creation and hashing are outside
the per-tick measurement.

The dedicated sparse 50k pipeline check is opt-in because it advances 50,000 agents for 100 warm
steps and 1,000 measured steps. It selects all five motion channels, all five canonical boids
channels and both runtime channels; it excludes quadratic nearest-neighbor analysis. Its world
is 100000 × 100000, SenseRadius 100, MaxNeighbors 32, and capacity 50000. It measures physics,
capture/frame/plan and real buffered CSV writes separately, including a separately recorded
final flush. A small 32-agent test exercises every registered canonical channel, including exact
nearest neighbors, for 1,000 samples.

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter FullyQualifiedName~SparseCanonical50k_SeparatesPhysicsObserverAndBufferedFileAllocations --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true
```

One Linux x64 / .NET 8.0.21 Release run observed the following across the 1,000 measured steps:

| Measured interval | Current-thread allocated bytes |
| --- | ---: |
| Simulation Step | 0 |
| Reusable capture, frame validation and metric plan | 0 |
| CSV numeric formatting and buffered file writes | 0 |
| Final file flush, measured separately | 0 |

The file contained 183,030 bytes, including warm-up rows. Physics took 34.476 seconds and the
measured full loop took 39.338 seconds on the shared development host. These values describe one
sparse scene and one runtime; they do not establish process-wide zero allocation or dense-scene
throughput. Exact nearest-neighbor analysis at 50k was not run.

The current motion probe computes all five of its outputs when any one is selected. Consequently
the default three-column periodic run also pays for circular-centroid trigonometry. The dedicated
check reports periodic versus Euclidean motion-only sampling times on the same captured frame
to make that overhead visible. In the same run, 1,000 samples of the captured 50k frame took
390 milliseconds in Euclidean mode and 3,974 milliseconds in periodic mode. Both paths computed
all five motion outputs and excluded engine stepping, capture and I/O. Timing observations are
not a throughput guarantee.

## Migrating the renderer benchmark flag

`SwarmSim.Render --benchmark` prints a deprecation notice and exits with code `0`; it no longer
executes the old legacy-only 600-tick loop. Use this runner for recorded experiments, selecting
`--engine legacy` to retain the legacy engine. For performance measurements, use
`dotnet run --project SwarmSim.Benchmarks -c Release -- --canonical`.
