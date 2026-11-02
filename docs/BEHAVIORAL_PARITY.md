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

Its main arithmetic and domain adaptations are:

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
`summary.json` (state/CSV identities and final-window means with finite counts), an operational
`timing.json`, and `log.txt`. Timing version 1 separates completed primary, repeat and naive-index
run counts and their elapsed seconds. The matrix clock includes those runs, exact repeat/index
checks and per-run console output; it excludes command input validation, baseline loading,
experiment metadata, final summary/timing/log writes and baseline calibration/comparison. Component
timers surround completed runner calls, including artifact writes when requested. The wrapper's
elapsed time covers its broader command execution and validation. These are operational scopes,
not additions to the deterministic metric schema. Initial, final and every-tick state identities hash little-endian float32
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
repeat/index, input and comparator regressions, including validation of the actual checked-in
calibration against all five recipes. Full scientific verification is a separate, explicit gate
because the per-tick quadratic analysis and dense all-neighbor workloads exceed the original
under-60-second target on the measured host. It is not part of every routine pull request.

### Full scientific Actions workflow

Run **Scientific parity** from the Actions tab with `workflow_dispatch` when an engine, metric,
scenario or parity contract change needs complete qualification. Pull requests changing that
workflow, its Python wrapper, or the command's gate/timing orchestration also run it automatically,
so changes to the gate prove themselves.
The path filter deliberately does not make the expensive matrix run on every engine or docs PR.

Four independent jobs cover Ubuntu/Windows and normal/alignment-off verification. Each checks out
the exact PR head (or the selected dispatch revision), records its Git SHA and builds the harness.
Normal mode executes 100 primary runs, 100 repeats and 50 canonical naive-index comparisons;
alignment-off executes its separate 100-run witness. A witness passes the CI wrapper only when
the harness returns comparison exit 1 **and** identifies a polarization rejection. Crashes,
invalid input (exit 2), an ineffective/distance-only witness (exit 3), or a partial/duplicated
scenario/seed/model matrix fail the job. The locked baseline's SHA-256 must remain unchanged.

Each job retains the full matrix, console output, runtime and binary provenance, plus a
`receipt.json` with the checked source SHA, baseline hash, elapsed time, exit code, verdict and
validated phase timings. The wrapper checks the actual completed counts (100/100/50 for normal
verification; 100/0/0 for the witness) and rejects missing, nonfinite, negative or inconsistent
timing records. Phase sums cannot exceed their containing matrix interval beyond rounding noise.
Artifacts are retained for 14 days. The job timeout is 45 minutes; this is an execution allowance,
not a relaxed performance acceptance threshold. Run the same wrapper after a Release build from
a Git checkout with:

```powershell
python .github/scripts/scientific_parity.py --mode verify --out artifacts/parity-verify
python .github/scripts/scientific_parity.py --mode alignment-off --out artifacts/parity-alignment-off
```

Use new output directories for each run. The wrapper never records a new calibration or changes
seeds, recipes or bands. The original under-60-second requirement remains an open part of #28.

## Recorded calibration and verification — 2026-10-29

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
recorded separately. An independent source review passed 73 probes covering the reference
formulas, analytical metrics, malformed/incomplete baselines, forged initial identities,
mutation-exit classification and the actual locked baseline.

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
Full verification used the reconciled Core source without regenerating or adjusting the
calibration. The measured verification binaries had Core SHA-256
`A522C2A6D9D1596EF1913F0DCCC19ED69F3E4631FD9F193EB3186C115D851E2B`
and parity SHA-256
`39CB017359D4E1788546EE2E8EC381E85D25E12400B333F8CF51243A4674E3F3`.

The complete normal verification **passed with exit 0**: 100 primary runs, 100 repeated runs,
and 50 canonical runs through the naive index. Every current-build state-trace and CSV exact
check passed, as did all nine locked metric envelopes. A separate Python artifact audit also
confirmed that all 100 current primary rows exactly matched the original calibration, including
requested/actual initial identities, final and every-tick state hashes, CSV hashes, window means
and finite counts. Its raw-file audit checked all 78,800 emitted tick rows, 1,600 window means and
200 initial-state CSV identities. This is additional observed preservation across the reconciled
Core changes on this host; it does not expand the cross-platform contract.

The complete alignment-off matrix **returned the required exit 1**. It reran all 100 primary
combinations, including current references, while disabling only classic alignment. There were
eight envelope failures, including both of these polarization violations:

| Scenario | Alignment-off polarization | Reference polarization | Absolute difference | Locked band |
| --- | ---: | ---: | ---: | ---: |
| S4 uniform | 0.031647 | 0.969507 | 0.937860 | 0.039880 |
| S5 clump | 0.110003 | 0.733099 | 0.623095 | 0.047183 |

Independent review reproduced all eight failures and confirmed unchanged requested/actual
initial identities for all 100 rows. The 75 unmodified-model rows retained their final-state,
state-trace and CSV hashes. Twenty classic trajectories changed; the five symmetric head-on
trajectories remained unchanged. The baseline bytes and bands were not changed for this witness.

Normal verification took 856.440 seconds for its 250 runs; the separate mutation matrix took
236.910 seconds for 100 runs. These elapsed values include overlapping execution and are retained
as operational evidence, not a matched performance comparison. At this 2026-10-29 revision, the
full scientific gates had been demonstrated on the recorded Ubuntu/.NET/X64 host. Ordinary
cross-platform CI covered the small regression suite; full Windows qualification is recorded in
the 2026-11-02 follow-up below.

**Remaining gate:** the 60-second target is still unmet at this revision. Issue #28 remains open
for that measured cost, and full scientific verification remains an explicit command or dedicated
Actions workflow. The passed
bounded behavioral comparison does not establish the other legacy retirement criteria.


## Cross-platform scientific qualification — 2026-11-02

All four full scientific jobs passed at `9dfe5319dcef48b000eefb72934508932b5fbe3e` in
[Actions run 41384869576](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/41384869576).
Each Ubuntu/Windows normal job completed 100 primary runs, 100 exact repeats and 50 canonical
naive-index comparisons, with all nine locked envelopes passing. Each separate alignment-off job
completed 100 primary runs and returned the required comparison rejection with polarization
failures. The wrapper verified complete scenario/seed/model identities and unchanged baseline bytes;
no seeds, recipes, calibration rows or metric bands were changed. Within-platform exactness remains
the contract: this is not cross-OS bit identity.

| Recorded workload | Ubuntu (seconds) | Windows (seconds) | Scope |
| --- | ---: | ---: | --- |
| Normal primary work | 360.269 | 346.421 | Sum of 100 rounded per-primary log intervals |
| Normal verification | 1059.943 | 1040.620 | 250 runs, interleaved exact checks and final summary write |
| Alignment-off witness | 318.325 | 344.325 | Separate 100-run mutated matrix |

These are observed CI timings, excluding build/startup and the later envelope comparison; they
are not a controlled host comparison or an independent 100-run normal benchmark. At this initial
revision the final normal log said “100 primary runs” while its stopwatch included all 250
executions. The primary sums above use the individual three-decimal log intervals, not that final
stopwatch. The separate version-1 timing record now removes this reporting ambiguity. It does not
make the under-60-second target pass.

The required polarization failures remained decisive on both platforms:

| Scenario | Ubuntu absolute difference | Windows absolute difference | Locked band |
| --- | ---: | ---: | ---: |
| S4 uniform | 0.937859819 | 0.927651284 | 0.039880 |
| S5 clump | 0.623095388 | 0.621027708 | 0.047183 |

### Remaining runtime investigation

S4 accounted for 310.294 of the 360.269 logged Ubuntu primary seconds and 296.317 of the 346.421
Windows seconds. S5 accounted for another 48.431 / 48.687 seconds. Source inspection identifies
two independent exact all-pairs metric scans on each tick, as well as model-specific steering work;
these are cost hypotheses to profile, not measured CPU attribution. The primary matrix requires
10,604,617,600 pair-distance evaluations across the two metric probes, calculated from the committed
populations, ticks, seeds and models.

The subsequent [runtime investigation](PARITY_RUNTIME.md) ran that unchanged S4 / seed 7 / classic
workload with a managed sampled-thread-time profile and bracketed same-binary controls. It identified
the spatial query, geometry probe and per-agent hash updates as bounded optimization targets. Strict
guards now skip identity remainders, and fixed-size hash batches retain the exact byte stream. An
unprofiled A–B–B–A comparison observed 42.286 / 34.042-second baseline primary intervals and
19.857 / 20.314-second optimized intervals, with every scientific file byte-identical. These are
one-case observations on a variable shared host, not a qualification of the complete matrix target.

Nearest-neighbor analysis still normalizes coordinates before subtraction while the geometry probe
subtracts before periodic remainder; their calculations remain separate. Reusing steering neighbors,
dropping per-tick metrics or reducing repeats would change the scientific contract. The dedicated
scientific workflow now also runs for changes under `SwarmSim.Parity/` and to `MathUtils.cs`.
Issue #28 stays open for runtime work, and the other legacy retirement requirements remain open.

The integrated optimization passed all four full scientific jobs at
`8dd2a734b27767b6e529ae2e323993dcfc16d7d5` in
[run 41449136980](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/41449136980).
Each normal job completed 100 primary, 100 repeat and 50 naive-index runs, with all nine envelopes
passing; both 100-run alignment-off jobs produced the required S4/S5 polarization rejections.
Normal primary intervals were 258.550 / 171.189 seconds on Ubuntu / Windows, and complete normal
matrix intervals were 733.770 / 497.876 seconds. These are separate CI-host observations, not a
controlled before/after comparison. The under-60-second target remains unmet. The
[runtime evidence](PARITY_RUNTIME.md#full-integrated-matrix-timings) retains phase timings, artifact
digests and independent checks of the 100 CSV hashes and 75 unchanged witness rows on each OS.
