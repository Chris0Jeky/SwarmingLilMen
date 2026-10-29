# Behavioral parity: bounded comparison contract

This work implements the five-case analysis surface in [issue #28](https://github.com/Chris0Jeky/SwarmingLilMen/issues/28).
It compares Enhanced canonical, ClassicReynolds canonical, legacy, and an independent Reynolds
reference. The browser demonstration remains a frozen qualitative reference. This document's
model, scenario, seed and metric choices were written before the first calibration run.

## Reference semantics frozen before calibration

The source is the repository's [boids-basic demonstration at the reviewed starting commit](https://github.com/Chris0Jeky/SwarmingLilMen/blob/6efc644cdf49f3ffd4e3ac0bfcea4f2c66599c82/js-demos/boids-basic/index.html).
Its animation loop computes every boid's force before moving any boid. Thus **this boids demo is
synchronous**; the separate Vicsek demo's sequential update must not be attributed to it.

The C# reference keeps the demonstration's three independent steering calculations: separation
uses a linearly fading inverse-distance influence; alignment uses the mean neighbor velocity;
cohesion steers toward the neighbor center. Each desired velocity is weighted, current velocity
is subtracted, each force is capped independently, and the three forces are summed. The summed
force has no additional cap. A nonzero integrated velocity is normalized to the configured speed.
Force calculations read the old population; movement writes a separate next buffer.

It deliberately adapts the demonstration in four explicit ways:

1. All state and steering arithmetic use C# `float`/`MathF`, rather than JavaScript `Number`.
2. Force and movement use a recorded fixed timestep, rather than one implicit step per frame.
3. Distances and cohesion use periodic minimum-image displacements. The demonstration senses in
   ordinary screen coordinates even though it wraps motion; that is a different edge model.
4. Position wrapping preserves overshoot using modulo. The demonstration snaps an overshoot to
   the opposite boundary.

The reference owns its vector arithmetic, neighbor loops, force formulas and integration. It does
not call canonical rules, resolvers, `Vec2`, spatial indexes, or world stepping. Ascending all-pairs
neighbor iteration replaces a spatial index. It has no FOV, truncation, whiskers, priority,
shaping, wander or turn limiter. Outer sensing uses inclusive `distance <= SenseRadius` as an
explicit adaptation; separation retains the demonstration's strict `distance < SeparationRadius`.
Coincident neighbors are excluded. A zero summed separation/alignment direction with positive
neighbor count retains the demonstration's braking behavior; zero cohesion direction emits no
force. Direct analytical cases pin these distinctions.
This is a specified adapted reference, not numeric reproduction of the browser executable.

## Five recipes and fixed seeds

The data files under `scenarios/parity/` are the complete recipe inputs. Each run emits the exact
generated initial states and their SHA-256 identity. Generation uses the named stream contract
`sha256-pcg32-v1`, consumer `parity.initial.v1.<scenario>`, and stable insertion identities.
The seed roster is **7, 19, 42, 73, 101**. No seeds are selected or discarded after observing output.

All recipes use a 256 by 256 periodic world, timestep 1/60 s, speed 30 units/s, per-rule reference
force cap 18 units/s², sensing radius 32, separation radius 8, and desired-speed weights
1.5/1/1 for separation/alignment/cohesion. Every canonical query can hold the whole population.
Enhanced retains its named default enhancement stages; legacy keeps its distinct priority and
speed-ceiling semantics. Their differences are measured, not disguised as classic parity.
Legacy also retains its internal **256-candidate** sensing buffer. Setting its `MaxNeighbors` to
the population does not enlarge that buffer: a sufficiently dense S4 population can still be
truncated. S5's 256 slots fit the candidate buffer, including self. Legacy is a descriptive
comparison, not a full-neighborhood oracle. The geometric neighbor-count metric independently
counts all agents in the analysis radius; it is not the number consumed by legacy steering or
by canonical FOV filtering.

| ID | Layout | Population | Ticks |
| --- | --- | ---: | ---: |
| S1_TWO_BOID_HEAD_ON | Two opposing headings, 20 units apart at the center | 2 | 240 |
| S2_RING_16 | Tangential ring, radius 24; seed rotates the whole configuration | 16 | 600 |
| S3_TWO_LINE_CROSSING | Two perpendicular lines, 16 boids each, spacing 4, offset 48 from center | 32 | 400 |
| S4_UNIFORM_RANDOM_512 | Uniform periodic positions and headings | 512 | 1800 |
| S5_DENSE_CLUMP_256 | Uniform-area disk of radius 10 with uniform headings | 256 | 900 |

The analytical layouts share their exact generated kinematic samples across every engine.
No engine gets a separately drawn initialization. Canonical ingest normalizes velocity; the
analysis adapter records its resulting initial-state identity so any normalization difference is
visible rather than silently claimed identical.

## Metrics and comparison tiers

The shared `MetricRegistry`, `MotionMetricProbe`, and `ExactNearestNeighborProbe` supply the
existing motion and nearest-neighbor channels. Analysis probes supply heading circular variance,
absolute turn rate (rad/s, mean/p50/p95), RMS flock radius around a periodic circular centroid,
geometric neighbor counts (mean/p50/p95), and unordered collision-pair count within one unit.
Percentiles linearly interpolate rank `q * (N - 1)`. Neighbor analysis is all-pairs, independent of
steering FOV/caps. Turns compare matching insertion identities before and after one tick; no
births, deaths or compaction occur in these recipes. Circular variance is `1 - polarization` for
these nonzero-speed populations. A circular centroid with no distinguished resultant makes
flock radius undefined (`NaN`), not zero. Baseline checks distinguish unavailable and finite values.

The **EXACT** tier compares repeated C# state/metric artifacts and canonical grid versus naive
spatial indexes within one build/runtime/platform. It is not a cross-OS bit-identity promise.
Every newly recorded or compared row regenerates and checks its requested and actual post-ingest
initial identities from that recipe, seed and model. Historical calibration input identities are
also regenerated when OS description, runtime and process architecture match. On another
platform, those stored hashes remain historical provenance; only its metric envelopes are reused,
while the new runs must pass their own regenerated identities and current-build exact checks.
The **ENVELOPE** tier compares ClassicReynolds and the independent reference on S2/S4/S5,
averaging the final 20% of completed ticks within each seed, then averaging all five seeds.
Bands must be fixed from the complete observed calibration distribution, with the inputs and
method retained. Alignment-off must exceed a polarization envelope to demonstrate sensitivity;
a failed witness is an open finding, not grounds to widen or move the gate.
Browser behavior is **QUALITATIVE** and never a numerical CI gate.

The initial envelope set is polarization and mean/median nearest-neighbor distance on each of
S2/S4/S5. For each channel, the absolute band is 1.25 times the largest absolute paired-seed
classic/reference difference, rounded upward to six decimals, with a minimum band of 0.02 for
polarization and 0.25 world units for distances. Before calibration, a tightness ceiling is set at
0.20 polarization and 10% of the sensing radius (3.2 units here) for distance. A calibration above
that ceiling is a reported model-comparison finding and cannot create a passing baseline. All
five seed means must have complete finite coverage for these channels. Other channels remain
reported diagnostics, including explicit finite-sample counts for potentially undefined radius.
Canonical-classic/reference speed bounds are checked separately from statistical parity.

Recording and verifying baselines are separate explicit commands. Verification never generates
or replaces a missing baseline. Baselines bind the full schema, model/seed/scenario inventories,
recipe identities, and metric ordering. The compact checked-in record is distinct from local raw
per-tick CSVs. Operational timing/provenance do not enter deterministic CSV identities.

See [legacy retirement criteria](LEGACY_RETIREMENT.md). No result in this harness by itself
authorizes engine deletion or proves renderer performance.

## Run, record, and verify

Build before using these `--no-build` commands:

```sh
dotnet build SwarmingLilMen.sln -c Release
dotnet test SwarmSim.Tests -c Release --no-build --filter FullyQualifiedName~Parity -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project SwarmSim.Parity -c Release --no-build -- run --out runs/parity-report
dotnet run --project SwarmSim.Parity -c Release --no-build -- record --out runs/parity-calibration --baseline runs/parity-candidate.json
dotnet run --project SwarmSim.Parity -c Release --no-build -- verify --out runs/parity-verification --baseline baselines/parity-v1.json
dotnet run --project SwarmSim.Parity -c Release --no-build -- verify --out runs/parity-alignment-off --baseline baselines/parity-v1.json --alignment-off
```

Every output directory must be new. `record` also requires a new baseline path and never replaces
prior evidence. `run` supports optional `--scenario`, `--seed`, and `--model` filters for exploration;
recording and verification reject partial matrices. These filters do not alter the prescribed
suite or its fixed calibration roster. Unknown/duplicate arguments and malformed recipes/baselines
reject before output creation.
The normal regression commands above use the checked-in locked baseline. Recording a candidate
is a separate evidence workflow; it never substitutes that candidate for the reviewed baseline.

Each model run writes `metrics.csv`, the exact requested `requested-initial.csv`, the actual
post-ingest `initial.csv`, and resolved `configuration.json`. The experiment root contains
`experiment.json` (recipes, contracts, platform/runtime/architecture and binary hashes),
`summary.json` (state/CSV identities and final-window means with finite counts), and an operational
`log.txt`. Initial, final and every-tick state identities hash little-endian float32
`x,y,vx,vy` in stable insertion order. Initial states may differ from their requested input due to
normalization; the two identities and files make that visible.

`record` runs all 100 scenario/seed/model combinations once. It writes the complete report before
checking calibration tightness, so an insufficient model comparison retains its observations
while refusing to publish a baseline. Passing record is not a repeat, index or mutation proof.

Normal `verify` repeats every model run and additionally runs both canonical modes through the
naive spatial index. It checks complete metric CSV and every-tick state hashes, then compares the
fixed metric envelopes. `verify --alignment-off` instead runs one full matrix with only the
classic alignment policy disabled and compares current references; it omits the duplicate
repeat/index work. Its expected rejection must specifically name **polarization** to establish
issue #28's witness; a nearest-distance failure alone is insufficient. Exit 1 reports comparison
failures, exit 2 rejects invalid input or a failed execution/recording contract, and exit 3 reports
an alignment-off run that did not violate a polarization envelope, including a distance-only
rejection. A distance-only rejection never counts as the required alignment witness.

The ordinary solution CI builds this project and runs the small analytical, real-engine,
repeat/index, input and comparator regressions. Full scientific verification is an explicit
command because the per-tick quadratic analysis and dense all-neighbor workloads exceed the
original under-60-second target on the measured host. This is partial issue #28; no expensive
full-matrix workload is silently added to every pull request.

## Recorded calibration and remaining gates — 2026-10-29

The [locked calibration](../baselines/parity-v1.json) contains all **100** prescribed
scenario/seed/model rows. Its SHA-256 is
`08e2e20ffff4cad7d5891095162f045fd5f4cba5a428c619619e981e77e5305d`.
It was recorded on Ubuntu 24.04.3 LTS, .NET 8.0.21, X64 using SDK 8.0.415. The complete primary
pass took **293.754 seconds**, including simulation, every-tick analysis, hashing and artifact
writing, and excluding compilation. This exceeds the issue's 60-second target. It is an observed
headless analysis workload, not a BenchmarkDotNet result, renderer FPS or a portable speed claim.

All nine calibration bands stayed below the limits declared before sampling:

| Scenario | Channel | Largest absolute paired-seed difference | Locked absolute band |
| --- | --- | ---: | ---: |
| S2 ring | Polarization | 0.000290966 | 0.020000 |
| S2 ring | Mean nearest distance | 0.002105865 | 0.250000 |
| S2 ring | Median nearest distance | 0.004963632 | 0.250000 |
| S4 uniform | Polarization | 0.031903344 | 0.039880 |
| S4 uniform | Mean nearest distance | 0.576730055 | 0.720913 |
| S4 uniform | Median nearest distance | 0.500650628 | 0.625814 |
| S5 clump | Polarization | 0.037745928 | 0.047183 |
| S5 clump | Mean nearest distance | 0.297613487 | 0.372017 |
| S5 clump | Median nearest distance | 0.372324968 | 0.465407 |

An independent Python recomputation from the actual generated files agreed exactly with all
100 CSV hashes, 78,800 tick rows, 1,600 sequentially accumulated window means and finite counts,
200 requested/actual initial-state CSV identities, and all nine bands. The four models received
matching requested state identities for each recipe/seed; actual post-ingest identities are
recorded separately. The independent source review also exercised the reference formulas,
analytical metrics, malformed/incomplete baselines and forged initial identities.

The modes make materially different predictions. For S4, five-seed final-window polarization
was 0.066568 (Enhanced), 0.979280 (classic), 0.421279 (legacy), and 0.969507 (reference).
Mean nearest distance was 7.315275, 2.808172, 8.429486, and 2.970562 world units respectively.
The classic/reference bands are therefore comparison of an explicitly chosen model; they do not
assert that Enhanced or legacy should behave like classic.

Calibration used Core binary SHA-256
`63D5E550854D44CC33DBBCE327F3825043B3DF2DE8B226D24F2B4A03C63F92E3`
and parity binary
`CB0CFB6F4731031D3E83957F56E63EA9281FBDF6F18C8EB5D246FD22670CB8E8`.
Its source and binary snapshot was retained before the later command-reporting correction,
[neighbor-statistics reuse (#186)](https://github.com/Chris0Jeky/SwarmingLilMen/pull/186), and
[representable-neighbor-distance correction (#188)](https://github.com/Chris0Jeky/SwarmingLilMen/pull/188).
Full verification uses the reconciled Core source; it does
not regenerate or adjust this calibration to accommodate the changes.

**Verification status while this change is under review:** the complete current-source
repeat/index run and the complete alignment-off matrix are pending. Their results must be
recorded here before claiming those scientific gates. The small default CI checks establish
their covered mechanisms, not completion of these full matrices. The 60-second target remains
unmet regardless of those outcomes, and issue #28 remains open for that measured limitation.

