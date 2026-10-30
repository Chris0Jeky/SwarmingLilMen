# Headless experiments

`SwarmSim.Cli` runs fixed logical ticks and writes a reproducible experiment record without
opening a window. It references only `SwarmSim.Core`; Raylib and the renderer are not in its
dependency graph. The default engine is canonical.

## Run a single experiment

```sh
dotnet run --project SwarmSim.Cli -c Release -- --scenario balanced --seed 7 --ticks 600 --agents 400 --out runs/
dotnet run --project SwarmSim.Cli -c Release -- --config configs/canonical-balanced.json --seed 7 --ticks 600 --engine canonical --out runs/
dotnet run --project SwarmSim.Cli -c Release -- --config configs/canonical-classic.json --seed 7 --ticks 600 --engine canonical --agents 400 --out runs/
dotnet run --project SwarmSim.Cli -c Release -- --config configs/balanced.json --seed 7 --ticks 600 --engine legacy --out runs/
```

Configuration follows **engine defaults → named scenario → native JSON file → explicit seed**.
Canonical files use `TargetSpeed`; legacy files use `MaxSpeed`. Unknown file fields are errors,
so passing a legacy file to the canonical engine fails before output is created. Use the shared
`--scenario balanced` name when you want the authored tuning applied to either engine. See the
[configuration cookbook](CONFIGURATION_COOKBOOK.md) and
[parameter mapping](PARAMETER_GUIDE.md#legacy-to-canonical-mapping) for the differences that remain.

Canonical defaults use the **Enhanced** composition. A native file can select
`"Mode": "ClassicReynolds"` together with `"FieldOfView": 360`, as in
[canonical-classic.json](../configs/canonical-classic.json). Classic installs only separation,
alignment and cohesion, clamps each force independently to `MaxForce`, sums them without a shared
post-clamp, then normalizes target speed. Its total force can exceed `MaxForce`; the enhanced
mode retains its shared total budget. The classic mode omits whisker, wander, priority, shaping
and turn-rate stages. See [canonical composition](CANONICAL_COMPOSITION.md) for the public API,
candidate-cap limits and exact semantics. In sweep files, put `Mode` in the base configuration;
v1 sweep axes remain numeric scalar fields.

| Option | Default | Meaning |
| --- | --- | --- |
| `--engine legacy\|canonical` | `canonical` | Selects the engine and native file schema |
| `--scenario NAME` | None | Shared named configuration overlay |
| `--config FILE` | None | Sparse native configuration file |
| `--seed N` | Resolved configuration seed | Integer from 0 through 2147483647 |
| `--ticks N` | `600` | Non-negative logical step count; zero records initial state only |
| `--agents N` | `400` | Positive initial population; at most legacy `MaxCapacity` or canonical effective `InitialCapacity` |
| `--metrics NAME,NAME,...` | `population,meanSpeed,polarization` | Registered channel names, case-sensitive, in exact CSV order |
| `--list-metrics` | — | Lists names, applicability and cost for `--engine`; creates no run |
| `--out DIRECTORY` | `runs` | Parent of a new run directory; existing runs are preserved |
| `--manifest FILE` | None | Replays a completed manifest into a new run directory |
| `--sweep FILE` | None | Sequential parameter sweep into a new sweep directory; combine only with `--out`, `--visual` or `--help` |
| `--visual` | Off | Also write `filmstrip.png` and `report.html` per run (and `index.html` per sweep); see [visual reports](#visual-reports) |
| `--help` | — | Prints usage |

Duplicate options, unknown options, malformed numeric values, and unexpected positional arguments
are usage errors. Exit codes are `0` for success, `1` for configuration/replay/I/O/run failure,
and `2` for invalid command-line usage. Errors go to standard error; success prints the new run
directory to standard output.

Legacy spawning may grow its arrays from `InitialCapacity` up to `MaxCapacity` during preflight.
The recorded configuration retains both configured limits; metric capture and exact-neighbor
scratch use the resulting world capacity, so every requested agent is included. Setting the two
limits equal keeps storage fixed. Canonical capacity remains fixed at `max(1, InitialCapacity)`.
The same rules apply to every sweep cell, including a grid over legacy `InitialCapacity`.
See [population capacity](PARAMETER_GUIDE.md#population-capacity) for growth and snapshot contracts.

## Run-directory contract

Each successful run publishes a new `runs/run-<random-id>/` child containing exactly (plus the
`--visual` files described under [visual reports](#visual-reports) when requested):

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

Enhanced canonical `Mode` is intentionally omitted from resolved JSON. A missing mode in old
schema 1 or schema 2 manifests is pinned to Enhanced; classic manifests record their mode
explicitly. This is the only optional-field exception to the complete-configuration check.
Other required fields cannot be omitted and inherited from current defaults.

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

## Visual reports

`--visual` makes any run show its behaviour without a GPU, display or extra dependency. It works
alone, with `--manifest` replay (hashes are still verified) and with `--sweep`, for either engine:

```powershell
dotnet run --project SwarmSim.Cli -c Release -- --engine canonical --agents 400 --ticks 600 --seed 7 --out runs --visual
```

A visual run directory holds the usual three files plus:

- **`filmstrip.png`** — 8 panels (4x2) at evenly spaced ticks including 0 and the last (fewer
  panels, deduplicated, when ticks < 7). Each panel keeps the world's aspect ratio at about 480 px
  wide. An agent is a short line along its heading, coloured by heading angle on a hue wheel (east
  is red): an ordered flock is one colour, disorder is a rainbow; stationary agents are grey dots.
  Line length follows the panel size, not world units. The tick number is drawn in each corner.
- **`report.html`** — one self-contained page (no external URLs or scripts, light/dark, phone-width):
  engine, seed, ticks, agents, final kinematic hash, git commit, the filmstrip inline, and one
  sparkline per `metrics.csv` column with first/last/min/max. Undefined (empty) cells are skipped,
  leaving a gap rather than a zero.

A visual sweep gives every cell those two files and adds `index.html` at the sweep root: one row
per cell with its parameter values, terminal metric values and a thumbnail linking to that cell's
`report.html` by relative path. Without `--visual` the output files are exactly as before.

Rendering reads the metric sampler's capture buffer only, copies just the 8 chosen frames, and
never touches the simulation, so `metrics.csv`, the hashes and the RNG are unchanged.
PNG bytes are repeatable on one machine. Pictures are written through the same staging directory,
so a failed render publishes nothing. The renderer needs only positions, velocities and world
extent, so any foundation that implements `PreparedSimulation.Capture` gets it.

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

## Parameter sweeps

`--sweep` runs a Cartesian product of seeds and numeric configuration values in the same process.
Each cell gets an ordinary replayable manifest, metric CSV and log; the sweep adds one stable
`summary.csv`. Execution is sequential. Plotting, statistical tests and parallel scheduling are
outside this mode.

The checked-in example runs **3 seeds × 4 values = 12 cells**, with 12 agents for 8 ticks per cell:

```sh
dotnet run --project SwarmSim.Cli -c Release -- --sweep configs/sweep-example.json --out sweeps/
```

```json
{
  "base": "canonical-balanced.json",
  "engine": "canonical",
  "agents": 12,
  "seeds": [13, 7, 11],
  "grid": { "SeparationWeight": [0, 1, 2, 3] },
  "ticks": 8,
  "metrics": ["meanSpeed", "polarization"]
}
```

`base`, `seeds`, `grid`, `ticks` and `metrics` are required. `engine` defaults to `canonical` and
`agents` defaults to `400`. The base path is resolved relative to the sweep JSON file, so the
example above reads `configs/canonical-balanced.json` regardless of the launch directory.
The native base configuration overlays engine defaults. Each cell then applies its grid values
and seed. There is no implicit renderer preset or scenario layer. Use `"engine": "legacy"` with
a legacy native base file when that engine is intended.

The sweep document uses strict JSON: unknown top-level names, duplicate object fields, comments
and trailing commas are rejected. Its field names are exactly the lowercase names shown above.
Base files retain native configuration support for comments and trailing commas, but unknown
and duplicate base properties are rejected. The base must be valid by itself, even if a later
grid value could repair an invalid base value.

Grid keys are validated against the selected engine's native configuration schema. They are
case-insensitive and normalized to the schema's property spelling. Version 1 supports numeric
scalar settings; structured or string settings belong in the base file. `Seed` is reserved for
the seeds array. Each grid axis needs at least one finite numeric value, and values must satisfy
the target property's type and validation rules. A misspelled key fails loudly. An empty grid
object is allowed for a seed-only sweep. Repeated numeric choices are allowed and remain separate
cells; seeds must be unique integers in `0..2147483647`.

Before creating any output directory, the runner validates the complete document, the base,
every resolved configuration, population capacity, simulation construction and initial metric
sampling for every cell. The product is bounded at **10,000 cells**, checked before expansion
with overflow-safe multiplication. This is a cell-count limit, not a runtime budget: exact
nearest-neighbor channels still have their documented O(N²) analysis cost in every cell.
`--sweep` can be combined only with `--out`, `--visual` and `--help`; engine, ticks, population and metrics
belong in the sweep file rather than competing command-line overrides.

### Cell order and output

The seeds retain their JSON array order. Normalized grid property names are sorted ordinally;
each value array retains its given order, and the last sorted grid axis varies fastest. Cells
are assigned `cell-000000`, `cell-000001`, and so on in that order. Reordering JSON grid
properties or changing their casing does not change cell order or summary bytes. Changing the
seed/value array order does change the plan and its cell IDs. The summary writer orders results
by planned cell index, independently of the order supplied to it.

A successful invocation prints one new `sweeps/sweep-<random-id>/` path. That directory contains
`summary.csv` plus one directory per cell. Each cell contains exactly the same three files as a
single run (five with `--visual`): `manifest.json`, `metrics.csv` and `log.txt`. The manifest keeps its ordinary random
run ID; replay does not depend on the containing cell directory name. Complete native settings
are available in each manifest, while summary configuration columns contain the swept numeric
properties, along with engine, seed, ticks and agent count.

The runner stages the entire sweep in a fresh temporary child, then publishes it by rename.
If any cell, CSV aggregation or final write fails, it removes only that attempt's staging
directory. Previously published sweeps and single runs are untouched. A failed attempt does
not publish a partial summary or a partial sweep directory.

### Summary statistics and replay

The example's summary header is:

```csv
cellId,engine,seed,ticks,agents,SeparationWeight,meanSpeed.terminal,meanSpeed.mean,meanSpeed.samples,polarization.terminal,polarization.mean,polarization.samples
```

Each requested metric contributes its terminal value, arithmetic mean and count of defined
samples. The terminal value is the metric at the requested final tick. The mean includes tick
0 whenever the channel is defined there, plus all defined completed-tick values. Thus an
always-defined channel in an 8-tick run has 9 samples; in a zero-tick run it has one initial
sample. Missing CSV fields do not contribute to either the sum or count. If the final sample
is undefined, the terminal field is blank. If every sample is undefined, both terminal and mean
are blank and the sample count is zero. Canonical `boids.*` channels have no tick-0 observation;
a singleton's nearest-neighbor distance is undefined at every tick.

Only deterministic channels are accepted. `runtime.wallMilliseconds` and
`runtime.allocatedBytes` are excluded because they describe observed execution costs and cannot
produce a byte-repeatable summary or exact cell replay. Use a separate single run for those
operational measurements.

The summary uses invariant round-trip number formatting, UTF-8 without a byte-order mark and
LF line endings. Random IDs, creation times, runtime timings and output paths are excluded.
Repeating the same plan with a matching binary, .NET runtime/platform and initialization
contract produces byte-identical summary and cell metric CSV files. Manifests and logs contain
per-attempt metadata and are not expected to be byte-identical. This remains the same scoped
determinism promise as single-run replay, rather than a cross-platform guarantee.

Replay any cell using its manifest alone:

```sh
dotnet run --project SwarmSim.Cli -c Release -- --manifest sweeps/sweep-<id>/cell-000005/manifest.json --out replays/
```

The normal replay path verifies both the final state hash and complete metric CSV hash. Schema
1 and schema 2 single-run replay remain available; newly produced sweep cells use schema 2.
To check a sweep, run the example twice and compare the two `summary.csv` files, then replay
one cell and compare its `metrics.csv`. The hosted headless smoke performs that bounded proof
on each CI operating system.

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

That earlier run exposed avoidable default-plan work: the full motion probe took 390 milliseconds
in Euclidean mode and 3,974 milliseconds in periodic mode for 1,000 samples of the same captured
50k frame. Both paths computed all five motion outputs, excluding stepping, capture and I/O.

The CLI now selects `MotionMetricProbe(includeCentroids: false)` whenever neither centroid channel
is requested. Its default three columns therefore skip coordinate sums and circular-centroid
trigonometry. Asking for either centroid enables the full probe; discovery still lists both.
The original parameterless library constructor remains a five-channel probe, and the first three
reductions preserve exact output bits and CSV bytes. Manifest schema and metric contract versions
are unchanged; actual schema 1 canonical and legacy manifests still replay their full CSV exactly.

Two separate opt-in checks cover the optimization and the core-only observer path:

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter FullyQualifiedName~BasicAndFullPeriodicPlans50k_ReuseScratchAndReportSamplingCost --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter FullyQualifiedName~SparseCanonical50k_CoreChannelsOnly_SeparatesPhysicsObserverAndBufferedFileAllocations --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true
```

The first compares basic and full motion probes in plans requesting the same three columns on one
prevalidated 50k periodic frame, after 100 warm samples and across 1,000 measured samples. It
excludes initialization, frame validation, simulation, capture and I/O. The second advances the
same sparse 50k simulation workload as the earlier pipeline check, with only the seven O(N) core
channels: five motion outputs plus both runtime costs. It excludes boids-specific probes and
quadratic nearest neighbors. Allocation assertions remain scoped to the calling thread; reported
times are observations, not FPS or throughput gates.

In one Linux x64 / .NET 8.0.21 Release run, the same-frame basic plan took 301.411 milliseconds
and the full plan took 2,191.723 milliseconds for those 1,000 samples. Both allocated zero
current-thread bytes and produced identical first-three output bits. The separate seven-channel
pipeline observed zero current-thread bytes in physics, capture/frame/plan, buffered CSV writes,
and the final flush. Its 1,000 measured steps took 26.290 seconds in physics and 30.402 seconds
for the full measured loop; the file was 109,069 bytes including warm-up rows. These two checks
ran on the shared development host, so their timings are workload-specific observations rather
than a controlled cross-machine performance comparison.

## Migrating the renderer benchmark flag

`SwarmSim.Render --benchmark` prints a deprecation notice and exits with code `0`; it no longer
executes the old legacy-only 600-tick loop. Use this runner for recorded experiments, selecting
`--engine legacy` to retain the legacy engine. For performance measurements, use
`dotnet run --project SwarmSim.Benchmarks -c Release -- --canonical`.
