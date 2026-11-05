# SwarmingLilMen — verified project state

**Updated: 2026-11-05.** This file describes current capabilities and the evidence behind them.
[The earlier tracker](docs/archive/PROJECT_STATUS_BEFORE_2026-10-29.md) preserves historical logs,
measurements and superseded plans. [Epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10)
is the roadmap and ordering authority; historical phase checklists are not a second work queue.

## What the engine is today

SwarmingLilMen is a C#/.NET 8 simulation research foundation. Six registered engines represent five
model families through one headless experiment workflow:

| Engine | Represented state and update | Observation |
| --- | --- | --- |
| `legacy` | Existing boids SoA pipeline, bounded spawn-time growth | Agent kinematics |
| `canonical` | Composable boids policies, resolvers and velocity stages | Agent kinematics and optional rule telemetry |
| `vicsek` | Synchronous heading replacement with angular noise | Agent kinematics and polarization |
| `aco` | Ants, resources, carrying state, pheromone field and retained per-ant RNG | Agent kinematics and native foraging/field metrics |
| `firefly` | Fixed-position oscillators with phase and natural frequency | Phase-coloured agents and coherence |
| `reaction-diffusion` | Synchronous Gray–Scott U/V concentration fields; zero agents | Native field metrics and a fixed-scale V heatmap |

New boids behavior belongs in `CanonicalWorld`; a different state/update model has its own
`Core/<Name>/` foundation and one registration in `SwarmSim.Cli/Engines.cs`. Discovery comes from
that registry: `--list-engines` and `--engine NAME --describe-engine`, optionally `--json`, expose supported
configuration, counts, observations and metrics without running a simulation. See the
[eight-study integration map](docs/SIMULATION_CATALOG.md).

The common layer now includes actual simulation mechanisms: immutable ordered synchronous
`SimulationSchedule` phases are used by Vicsek, ACO, Firefly and reaction–diffusion; typed
`DoubleBufferedState<T>` generations are used by Vicsek, Firefly and reaction–diffusion. Named RNG
streams, native configuration, metrics, manifests, replay, sweeps and reports remain shared.
Versioned model-state fingerprints cover the complete native state of those four models, while
historical kinematic hashes remain unchanged. The kernel does not yet own heterogeneous coupled
worlds, dynamic lifecycle/event semantics, checkpoint restore or a universal state schema. The
[kernel contract](docs/SIMULATION_KERNEL.md) and [fingerprint contract](docs/MODEL_STATE_HASHES.md)
state these limits explicitly.

The eight browser demos remain separate implementations; five study families now have native
counterparts with documented differences. Avalanche, PSO and Ecogenesis remain browser-only.
The public Pages site is unchanged. Combat, metabolism, reproduction and cross-group interactions
are not made active merely because `SimConfig` contains related settings.

### Historical scientific qualification — 2026-11-02

`SwarmSim.Parity` now compares Enhanced, ClassicReynolds, legacy and an independent float32
Reynolds reference through five explicit data recipes and five fixed seeds. It records requested
and post-ingest initial states, per-tick metrics, source/runtime provenance and a locked calibration.
The 100-run calibration took 293.754 seconds on the recorded host, so the 60-second target remains
unmet. All nine predeclared comparison bands fit their ceilings; an independent Python audit
agrees exactly with all 100 CSV hashes, 78,800 tick rows, 1,600 window means and 200 initial-state
hashes. The reconciled source then passed the complete 250-run same-build verification: 100
primary runs, 100 repeats and 50 canonical Naive-index runs. A separate 100-run alignment-off
matrix fails the S4/S5 polarization bands while all 75 unmodified-model traces and CSVs remain
exact. The dedicated Scientific parity workflow has now passed the complete normal and
alignment-off matrices on both Ubuntu and Windows at a recorded exact head, with all locked
bands and baseline bytes unchanged. Each job retains the matrix, runtime/binary provenance and a
source-bound receipt. Normal CI keeps the small analytical/real-engine suite; the expensive gate
runs on explicit dispatch or changes to its orchestration, `SwarmSim.Parity/`, or the shared
minimum-image helper. Operational timing now records
actual primary/repeat/naive counts and separate phase durations, so 250-run verification cost is
not labeled as a 100-run primary measurement. After the bounded runtime changes, the complete
normal primary intervals were 258.550 / 171.189 seconds on separate Ubuntu / Windows CI hosts;
the under-60-second target remains open. These host observations are not a controlled performance
comparison. The [runtime evidence](docs/PARITY_RUNTIME.md#full-integrated-matrix-timings) keeps
the workload, timing and comparison scopes explicit.
See [behavioral parity](docs/BEHAVIORAL_PARITY.md) and the
[legacy retirement criteria](docs/LEGACY_RETIREMENT.md). Legacy remains supported.

## Verified live state

The integrated Release gate passes **1,780 non-Performance tests**, with zero failures or skips
in all four Ubuntu/Windows exact-head/generated-merge jobs in
[CI run 42085048980](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/42085048980).
The reviewed head is `9245c9c89db75550c0e85fbbfeba2005329782eb`; merged commit
`8ae2cadcb5232027d98b8e90a9572cf1d4aac60c` has the same tree,
`e1632c401b4978b080b118da5b402c6737419346`. Both exact-head logs report clean Release builds,
all nine existing golden checks, unchanged old-engine repeat/replay identities, collector tests
and smoke, and all ten gallery entries. Linux native renderer probes also pass. The separate
hosting scanner gate is green.

The working environment for this round had no .NET SDK; C# execution evidence is from Actions.
The scanner, benchmark collector and scientific-wrapper suites separately passed **96**, **40**
and **14** Python tests locally. The complete normal/witness scientific matrices were last qualified
in [run 41449136980](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/41449136980) on
2026-11-02; those expensive matrices were not rerun for the new model integrations. Their prior
measurements and limitations remain historical evidence, not new performance results.

### Completed in this round

- **Shared phases and typed generations** ([#224](https://github.com/Chris0Jeky/SwarmingLilMen/pull/224)):
  Vicsek and ACO first exercised the common synchronous phase runner, preserving their existing
  operation/RNG order. Firefly and reaction–diffusion subsequently reused it without changing the
  scheduler. Phase exceptions stop later phases; the scheduler itself does not roll back model
  mutations. Generation publication is explicit. See [simulation kernel](docs/SIMULATION_KERNEL.md).
- **Registry-derived discovery** ([#226](https://github.com/Chris0Jeky/SwarmingLilMen/pull/226)):
  CLI text/JSON discovery exposes actual native support and metric costs; the catalogue maps all
  eight studies and separates model subsets from browser equivalence.
- **Finite scalar deposits** ([#228](https://github.com/Chris0Jeky/SwarmingLilMen/pull/228)):
  an overflowing finite sum now fails before changing the destination cell. Two tests failed on
  the preceding implementation; all six focused cases pass. Ordinary float rounding is unchanged,
  and this is single-deposit atomicity, not transactional rollback of an entire ACO phase. See
  [scalar-field range](docs/SCALAR_FIELD_RANGE.md).
- **Complete native state identity** ([#230](https://github.com/Chris0Jeky/SwarmingLilMen/pull/230)):
  schema 3 adds a profile-qualified hash of native settings, clock, ordered state and retained RNG
  cursors. Fields, resources, pending deposits, phases and frequencies cannot hide behind unchanged
  kinematics or a sparse metric selection. Schema 1/2 compatibility and their original checks are
  retained. Hashing is observational and does not consume random draws or provide checkpoint restore.
  See [model-state identity](docs/MODEL_STATE_HASHES.md).
- **Direct separation distance overflow** ([#231](https://github.com/Chris0Jeky/SwarmingLilMen/pull/231)):
  both `Compute` and `Emit` recover overflowing squared finite distances through the same bounded
  helper. Twelve tests failed before the fix; all 58 focused cases pass. Ordinary arithmetic,
  weighting, cutoffs and nonfinite behavior remain intact. The observable witness uses large finite
  caller weights; it is not an ordinary-world trajectory change. Wider #187 work remains open. See
  [separation scope](docs/CANONICAL_SEPARATION.md).
- **Native Firefly synchrony** ([#232](https://github.com/Chris0Jeky/SwarmingLilMen/pull/232)):
  global synchronous Kuramoto phases reuse the shared schedule/buffers and now have configuration,
  metrics, full-state replay, sweeps and phase-coloured reports. The committed finite gallery control
  measures approximately 0.998 coherence with coupling and 0.0233 without it on both CI platforms;
  actual generated filmstrips were inspected. This is not spatial/noisy browser-feature parity or a
  phase-transition measurement. See [Firefly contract](docs/FIREFLY.md).
- **Field-only Gray–Scott experiments** ([#234](https://github.com/Chris0Jeky/SwarmingLilMen/pull/234)):
  zero-agent engine capabilities, strict native metrics, synchronous U/V state, complete fingerprints
  and optional field observation now use the same run/replay/sweep/gallery pipeline. Conservative
  depletion bounds and finite stored-value checks reject unsafe steps before committing the field;
  concentrations are never clipped. Independent review found and corrected one maximum-float
  rounding boundary. The patch evolves while the equilibrium stays at U = 1, V = 0 in the inspected
  exact-head gallery artifact. The 86 added cases include independent stencils, conservation,
  atomicity, hash fixtures, CLI behavior and real pixels. No convergence or Turing-instability claim
  is made. See [model](docs/REACTION_DIFFUSION.md) and [field observations](docs/FIELD_OBSERVATIONS.md).

### Earlier verified additions

The records below preserve the counts and measurements at their original checkpoints. They do
not imply that every historical benchmark or scientific matrix was rerun in this round.

- **Profile-guided parity runtime:** strict guards skip identity remainder operations in the
  spatial minimum-image helper and geometry probe, while 64-agent hash batches retain the exact
  serialized byte stream. An unchanged S4/seed7/classic A–B–B–A experiment observed baseline
  primary intervals of 42.286/34.042 s and optimized intervals of 19.857/20.314 s. All five
  scientific files match byte for byte. This isolated case supports the bounded optimization;
  it does not qualify the complete 100-run under-60-second target. The two exact all-pairs metric
  scans, full recipes/seeds/cadence and locked bands are unchanged. See the
  [profile, controls, raw observations and identities](docs/PARITY_RUNTIME.md).
- **Benchmark evidence consistency:** the collector now checks BDN 0.15.6/.NET 8 integer
  allocation output against raw process-wide bytes, including double conversion and ties-to-even
  rounding. Raw 336 bytes / 64 operations must export 5 B/op, while the original raw total stays
  in the receipt. Forty Python tests pass after 17 failing subcases before the correction; CI now
  runs and retains the actual smoke bundle on Ubuntu/Windows at integration and exact head.
  A matched 1k comparison against the original pre-composition baseline produced reversing pair
  deltas and substantial launch drift, so it does not establish recovery or a regression magnitude.
  [#20](https://github.com/Chris0Jeky/SwarmingLilMen/issues/20) and
  [#27](https://github.com/Chris0Jeky/SwarmingLilMen/issues/27) remain open; see
  [the collector contract](docs/COMPOSITION_EVIDENCE.md) and
  [the full matched observations](docs/CANONICAL_COMPOSITION.md).
- **Immediate renderer shake feedback:** legacy X/shake now refreshes both interpolation
  snapshots and notifies the simulation runner after changing velocities. Three real-input-path
  regressions cover shake alone and same-frame parameter/preset recreation; they fail on stale
  snapshots before the fix and pass afterward, including mutation-version/accumulator checks.
  The renderer guide now describes canonical key 8/FOV and S/A/C switches accurately. This does
  not complete the physical keyboard, focus, DPI or human visual acceptance in #40.
- **Static-demo scanner admission:** visible function/key restrictions now span script blocks,
  inline handlers and `javascript:` URLs within each artifact, with explicit parse-unit boundaries.
  Visible reflection/prototype APIs are outside the admitted language. The 96 scanner tests pass,
  including rejection before output writes; eight actual publication files remain byte-identical.
  Arbitrary computed keys, coercions, returned/member values and cross-file bindings remain
  unproved. [#164](https://github.com/Chris0Jeky/SwarmingLilMen/issues/164) stays open; the
  [grammar limits](.hosting/README.md#scanner-grammar-limits-2026-11-02) retain artifact review and
  the allowlist as the publication boundary.
- **Finite-vector normalization and ingestion:** huge finite velocity inputs no longer lose
  their direction when `Normalized` or `WithLength` overflows a squared float norm. The fallback
  retains a double norm, while ordinary rounding, each method's existing near-zero cutoff and
  nonfinite target/source behavior stay unchanged. Sixty-three new cases pass after 18 failed
  before, including real `TryAddBoid`/`SetVelocity` plus complete-tick displacement. The integrated
  four-fix numeric lane passes 1,504 ordinary tests and six gallery entries. See
  [the normalization contract](docs/CANONICAL_VECTOR_NORMALIZATION.md).
- **Shaped avoidance at large finite distances:** an in-range neighbor no longer disappears
  from the avoidance blend when its squared float norm overflows. The exceptional path recovers
  both distance and away direction; ordinary arithmetic, tiny-distance cutoffs and the exclusive
  gradual-radius edge remain unchanged. All 37 direct-stage and real-world Grid/Naive seam/interior
  cases pass after 15 failed before. The three numeric follow-ups together pass 1,441 ordinary
  tests and all six gallery entries on the recorded local build. See
  [the shaped-avoidance scope](docs/CANONICAL_SHAPED_AVOIDANCE.md).
- **Finite-vector magnitude clamps:** `Vec2.ClampMagnitude` now recovers the direction and cap
  when finite components overflow their squared float norm. Its ordinary rounding and nonfinite
  input behavior are unchanged, and both steering resolvers still reject nonfinite weighted
  squared magnitudes before clamping. All 38 analytical/compatibility cases pass after 14 failed
  before the fix, including signed zero and the smallest positive float. See
  [the bounded clamp contract](docs/CANONICAL_VECTOR_CLAMP.md).
- **Finite neighbor-distance averages:** the local or world float sum can overflow while every
  distance and the mean remain finite. A double fallback now activates only after that overflow,
  retaining the original float prefix, ordinary accumulation order and division. Twenty analytical
  cases cover local/global overflow, both indexes and both capture APIs, tick reset and an exact
  ordinary-rounding witness; all pass after eight failed before the correction. No hot-path
  allocation or extra ordinary per-neighbor work was added. Other extreme-scale consumers remain
  in [#187](https://github.com/Chris0Jeky/SwarmingLilMen/issues/187); see
  [the distance scope and evidence](docs/CANONICAL_EXTREME_DISTANCES.md#distance-average-follow-up--2026-11-02).
- **ACO foundation and scalar field:** `ScalarField` is the first non-agent state, and
  `AntColonyWorld` runs a minimal foraging colony with its own ant shape, one retained named stream
  per ant, and distinct agent and field phases: the agent phase reads field(t) and buffers deposits,
  and the field phase applies them in ant order and evaporates. Sixty-two analytical and CLI tests
  pin the phases, stream use, conditional draws, sensors, food conservation, repeat and replay
  bytes, exact field bits and zero warm-tick allocation. At 300 ants, five locked seeds return
  2300-2450 units against 490-519 for `Deposit = 0`, ratios 4.49-4.90 and ensemble 4.67, inside bands
  committed in `61748c7` before those seeds ran. All 13 source mutations fail, nine of them only
  through analytic tests. Filmstrips show a two-way trail with pheromone and none without. This is a
  foraging comparison with no optimization-quality claim. Engine dispatch moved from seven CLI files
  to one registry with all three earlier engines byte-identical. At that checkpoint, the shared
  claim covered the experiment spine, with world-owned phases and a kinematic hash unable to see
  the field. The current shared schedule and schema 3 state profiles close those specific gaps;
  the broader kernel limits remain explicit above. See [the model and evidence](docs/ACO.md) and
  [the seam inventory](docs/VICSEK.md#seam-inventory-update-aco).
- **Vicsek foundation:** `VicsekWorld` runs the original synchronous model through the unchanged
  `ISpatialIndex`, named streams and `polarization` probe, with no change to `Canonical/` or legacy
  code. Twenty-seven analytical and CLI tests pin old-state reads, old-velocity positions, self
  counted once, radian noise, seams, the zero-resultant rule, untruncated neighborhoods, grid/naive
  equivalence, repeat/replay bytes and zero warm-tick allocation. Five locked seeds (N=100, density
  4) measure order 0.988 at η=0.5 and 0.126-0.136 at η=5, inside bands committed before those seeds
  ran. Seven source mutations fail. This is a finite noise-response result, not a phase-transition
  measurement. The original seam inventory found engine dispatch spread across seven CLI files;
  the later registry removed that duplication. Boids force composition still does not express
  heading replacement; Vicsek uses its native update semantics. See
  [the model, evidence and seam inventory](docs/VICSEK.md).
- **Vicsek exact traces and manifest guards:** five new regressions directly exercise schema-1
  rejection, spawn-model mismatch, shared-name configuration overlays and an eight-agent exact
  trajectory at ticks 0/1/16/64/256. Linux and Windows references were captured independently on
  .NET 8.0.33/X64 and every component word was rehashed. The first platform difference is one ULP
  at tick 1; separate exact references preserve that measured distinction. The one-ULP noise
  witness diverges at tick 1 on both platforms, and a separate source mutation fails the ordinary
  Linux golden assertion; existing boids/legacy fixtures are unchanged. See
  [the scenario, reference provenance and mutation evidence](docs/VICSEK.md#exact-trajectory-regression).
- **Behaviour gallery:** `SwarmSim.Cli --gallery scenarios/gallery.json` now runs ten entries:
  tuned canonical flock, legacy balanced, Vicsek low/high noise, ACO with/without pheromone,
  Firefly coupled/control and reaction–diffusion patch/equilibrium. It prints one PASS/FAIL line per
  entry and publishes `index.html` plus `summary.json` over ordinary replayable run directories.
  The original six-entry gallery took 1.8 s of run time on its recorded host; that timing is not a
  current ten-entry measurement. Bounds are coarse final-quarter sanity checks. Existing Vicsek/ACO
  scientific evidence and the new analytical/controlled model tests have separate scopes. At the
  original gallery addition, 52 `GalleryTests` passed; inverting a bound, widening the quarter
  window and reversing the failure sort each failed tests. Actual Firefly and field artifacts
  from this round's exact-head Linux CI were also inspected. See
  [behaviour gallery](docs/HEADLESS_EXPERIMENTS.md#behaviour-gallery).
- **Visual reports:** `SwarmSim.Cli --visual` adds `filmstrip.png` (up to eight selected ticks,
  heading-coloured agents, Firefly phases or a fixed-scale scalar field) and a self-contained
  `report.html` to any run, replay or sweep cell, plus an `index.html`
  for sweeps, using only the standard library and no display. Tests prove it leaves `metrics.csv`
  and hashes unchanged and decode the PNG independently (43 cases in `VisualReportTests`). The
  filmstrips were checked by eye for canonical and legacy 400-agent runs; final-panel thumbnails
  were also inspected for canonical and high-noise Vicsek. No render-time or large-population cost
  was measured. Each sparkline is capped at 480 vertices while retaining endpoint/bucket extrema
  and original-data gaps; statistics still use every row. Final-panel `thumbnail.png` files reduce
  sweep/gallery image payload, failed bands display round-trip values and bounds, extreme aspect
  heights clamp before integer conversion, and five actual write failures prove staging cleanup
  without disturbing earlier output. Long CSV parsing still scales with row count; no phone-browser
  acceptance or large-sweep timing is claimed. See [visual reports](docs/HEADLESS_EXPERIMENTS.md#visual-reports).
- **Canonical inspection:** S/A/C switch the named separation/alignment/cohesion policies;
  key 8 adjusts FOV and switch state survives recreation/reset. The overlay now draws a gold
  FOV arc, rule-colored input links and the actual white resolved-force arrow, all using the last
  completed tick's inputs. The existing double buffer supplies positions; force instrumentation
  adds 8 bytes per capacity slot and retains its squared-magnitude API. Headless tests cover
  independent rule effects, caps, timing, geometry and zero warm query allocation; exact goldens
  remain unchanged. Tick status, zero-input pipelines, late-spawn membership and selected-agent
  persistence through the real recreation path have direct regressions. The reusable
  `tools/RendererProbe` passes 107 native behavior checks and 18 separate artifact checks in nine
  captures, including a seam scene, with an HTML gallery and JSON/build-hash report. It checks
  missing, extra, shortened and overlong geometry with explicit raster/occlusion tolerances;
  deleting the force shaft or halving primary links fails while unmodified images stay byte-identical.
  Linux CI runs it under Mesa/Xvfb for integration and exact-head builds and retains feedback
  artifacts; the earlier Windows local GPU execution remains separate evidence. Windows OS-input
  injection and direct framebuffer/DPI export did not qualify, so OS input and human visual
  acceptance remain open in #40. See [inspection semantics and evidence](docs/CANONICAL_INSPECTION.md).

- **Named random streams:** `RandomStreams` derives PCG32 state from a specified seed/name/optional
  agent encoding; consumers retain independent generators. Canonical wander now uses a stream per
  stable insertion index. Legacy RNG behavior is unchanged. External PCG words and independently
  calculated adapter/identity vectors are committed, and a fourth stochastic observer leaves every
  kinematic hash unchanged for 256 ticks. The deliberate canonical wander reference migration is
  tracked in [determinism](docs/DETERMINISM.md); cross-platform motion identity is not promised.
  The local Release gate passes 943 tests. Both Windows jobs independently captured the new exact
  trajectory in [run 40811616741](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/40811616741),
  with all logged component words rehashed; Ubuntu CI matched the Linux reference. Retained RNG
  draws allocate zero bytes in the 100,000-batch regression. Canonical agents retain 16-byte value
  states; a 100,000-agent whole-world probe measured retained managed memory dropping from
  45,241,032 B to 12,441,520 B, with zero bytes across 1,000 measured ticks. The known old signed-seed
  alias pair is pinned to independent output vectors. See the
  [stream ownership and byte contract](docs/RANDOM_STREAMS.md).

- **Canonical composition:** injected pipelines now own bounded relative-neighbor observations,
  tagged force intents, a named resolver and explicit velocity stages. The default Enhanced
  composition preserves exact platform fixtures and complete pre-change experiment replay.
  Added local policies and trusted legacy-rule adapters now affect trajectories. Empty pipelines
  preserve straight-line velocity bits; public ClassicReynolds configuration installs only the
  three independently clamped rules and target-speed normalization. Named disabling stops emission;
  a zero desired-speed weight is not a disable switch. Registration prevents shared mutable
  components, forged output writers and world mutation during a tick, without retaining a world
  through a saved component. Old schema 1/2 manifests without Mode retain Enhanced semantics.
  This remains a force-policy API: public per-agent identity/noise, heading replacement and
  Vicsek update ordering are not implemented. Warm allocation checks cover Enhanced, Classic and
  Empty; the controlled 1k benchmark nevertheless shows a +16.53% cost, recorded below and kept as
  follow-up work. See [contracts, replay and measured cost](docs/CANONICAL_COMPOSITION.md).
- **CI and regression gates:** PR #168 retains Ubuntu/Windows merge-result builds/tests and adds
  independent jobs for the exact PR head, logging its SHA. All four jobs passed 536 tests at
  `f342bc9695e2707e930e0e5e0fac85db84a2c449`. Genome validation tests reject every covered invalid
  rate/deviation without consuming RNG draws. Removing the guards in an isolated copy produced
  eight expected failures. The main branch received this change through a merge commit.
- **Legacy population capacity:** `InitialCapacity` controls initial storage and `MaxCapacity`
  bounds occupied slots. Spawn calls grow geometrically while preserving agent data/indices,
  sensing scratch, grid links, simulation time and RNG state; reaching the ceiling consumes no
  random draws. Borrowed arrays/spans must be reacquired after growth; copied snapshots remain
  valid and mutation notification remains explicit. The CLI accepts populations up to the legacy
  maximum. Fourteen new regressions plus an independent Wrap/Reflect/Clamp probe compare grown
  and preallocated worlds through spawn, compaction, refill and subsequent ticks. Existing exact
  traces remain unchanged. Equivalent bit masks replace seven legacy `Enum.HasFlag` calls that
  boxed under Tier0: fresh-process grown/preallocated probes now allocate zero bytes for 1,000
  ticks with normal tiering and with tiering disabled. This finding is separate from the unresolved
  canonical BenchmarkDotNet 5 B/op observation below. See [population limits](docs/PARAMETER_GUIDE.md).
- **Behavioral scenarios:** eleven explicit-state, fixed-tick cases now pin head-on clearance,
  circular heading convergence, empty-neighborhood response at three headings, cohesion
  contraction, interior/seam cluster approach and the default priority speed floor/recovery.
  All three flocking contributions must occur in the interacting stability scene. Six isolated
  implementation mutations make the relevant scenarios fail; restored source passes. No
  production behavior or exact golden fixture changes accompany this coverage. Eight additional named-composition cases now prove isolated rule-level integration over
  120 ticks, single-rule avoidance/alignment/cohesion behavior and classic constant speed across
  1,200 ticks. Four more mutations make those cases fail. A valid faster/slower overtaking model
  remains issue #41 work. See the
  [acceptance inventory and measured margins](docs/CANONICAL_BEHAVIOR_SCENARIOS.md).
- **Foundation gate:** PR #170 passed all 697 Release non-performance tests on Ubuntu and Windows,
  against both the merge result and exact head `5f08a9f331944f65c7665ab91fd86dd3c7ea6614`.
- **Configuration:** both config types load and save JSON, including the legacy rectangular
  aggression matrix via a compatible converter. `ConfigurationResolver` provides defaults →
  named scenario → file → explicit overrides. New resolver file/override typos fail; existing raw
  loaders retain permissive unknown-property handling. The constructor/JSON validation boundary
  rejects invalid active values while preserving documented finite clamps, zero disable modes
  and legacy unlimited-neighbor configuration. Renderer startup uses the same preset-before-file
  layering while preserving unauthored defaults and the five-entry F-key registry. Five previously
  dropped neighbor/crowding/collision settings now survive its copy paths. See
  [renderer configuration](docs/RENDERER_CONFIGURATION.md),
  [configuration cookbook](docs/CONFIGURATION_COOKBOOK.md) and
  [parameter guide](docs/PARAMETER_GUIDE.md).
- **Renderer controls and native acceptance:** one engine-scoped inventory now drives both input
  paths, H panels, HUD/footer labels, printed parameters and console help. Canonical accepts 1–8
  (FOV is on 8);
  legacy accepts 1–8 and labels friction active only with Damped. Sense-circle help states the
  existing below-100-agent limit. Parameter/preset recreation now directs later same-frame actions
  to the active world: native F1+Space and Up+Space trials changed from a false 100-spawn report
  with 64 exported agents to 164 exported agents. At that #182 revision, the integrated Release
  suite passed 976 tests.
  Seven real native X11 launches verified both complete H panels, all five presets, sparse
  preset/file startup, both friction modes, O/Tab, pending/completed/reset telemetry, F12 and
  spawn/reset/export. Direct captures and exact source/binary hashes are committed in the
  [native verification record](docs/NATIVE_UI_VERIFICATION.md). This is Linux software-rendered
  input/visual evidence, not human sign-off, cross-platform GUI coverage or an FPS benchmark.
- **Legacy spatial correctness:** Wrap now queries across periodic seams and computes nearest-image
  distances/FOV/separation/cohesion. `GridCellSize` is honored, with radius-complete candidate-cell
  traversal and a matching preallocation budget. One/two-cell axes are visited without duplicates;
  valid positive worlds retain at least one cell if a dimension ratio underflows. Reflect/Clamp
  keep bounded-world geometry. These are intentional legacy behavior corrections.
- **Canonical spatial precision:** both indexes keep normal binary32 query semantics and use a
  double comparison only when squared radii are subnormal or overflow. Tiny distinct offsets no
  longer count as coincident; large finite radii no longer admit farther points through infinity
  comparisons. Lowest-index ordering, inclusive boundaries and truncation behavior are tested.
- **Representable neighbor distances:** bounded observations and nearest-distance telemetry now
  recover valid lengths when squared float intermediates overflow, become subnormal or underflow.
  Ordinary float arithmetic remains unchanged. Analytical 3–4–5 cases cover four scales, both
  indexes, periodic seams and first-candidate ties; four regressions show distant neighbors no
  longer falsely activate separation priority. Ordinary traces and an actual pre-fix manifest
  remain exact. A separate 26-case partial-FOV matrix now covers large-scale norm overflow with
  ordinary/large 3–4–5 geometry, both indexes, interior/seam layouts, three apertures and tiny-direction
  cutoff controls. Eight cases fail before the exceptional-norm correction; queries, actual policy
  observations, instrumentation and last-tick inspection pass after. Ordinary normalization, direction
  cutoffs and exact goldens remain unchanged. The later bounded average, clamp, shaping and normalization corrections are listed above.
  Direct rule calculations, derived extreme controls and aggregate sample-count limits still
  need the wider numeric audit in #187. See the
  [correction, compatibility scope and remaining consumers](docs/CANONICAL_EXTREME_DISTANCES.md).
- **Canonical telemetry:** `Step` no longer allocates snapshots. Explicit caller-owned
  `CanonicalTelemetry` captures last-tick diagnostics and truncation without a spatial re-query.
  A 256-boid/1,000-warm-tick regression fell from 3,144,000 allocated bytes to zero, including a
  capture-every-tick variant. The old `CapturePerceptionSnapshot()` is an allocating compatibility
  adapter. The canonical renderer now explicitly reuses telemetry once per frame. Its overlay
  status line reports the recorded tick's inputs and truncation separately from the displayed
  links and 128-entry display cap. Headless tests
  cover HUD value parity and recreation; actual native Linux input and inspected screenshots are
  now recorded in [native verification](docs/NATIVE_UI_VERIFICATION.md). Human sign-off and other
  platforms' GUI behavior are not inferred from that evidence.
  See [renderer telemetry](docs/CANONICAL_RENDER_TELEMETRY.md).
- **Trajectory preservation:** all four bounded canonical workloads retained their pre-change
  Linux hashes for all three repeats. Grid and naive state matches at every observed frame;
  capture on/off also matches every boid float bit for 128 ticks. This does not establish
  cross-platform identity. [Trajectory experiment](docs/CANONICAL_EXPERIMENT.md).

- **Determinism contract:** exact replay is scoped to the recorded implementation, configuration,
  seed, timestep, OS/architecture and .NET band. Committed eight-agent traces pin ticks
  0/1/16/64/256 and RNG sequences. Ubuntu x64 .NET 8.0.21 and 8.0.33 match; Windows x64 .NET 8.0.33
  matches legacy/RNG but has a separate exact canonical reference. The first canonical difference
  is 1 ULP at tick 1. The original pre-PCG trace had a tick-256 maximum component difference of
  0.00012969970703125 (322 ULP); after the explicit named-stream migration it is
  0.00009918212890625 (112 ULP), measured in both Windows jobs against Linux. The determinism guide
  retains both experiments and their CI references.
  One-ULP force changes fail the ordinary golden assertions. The golden suite enforces the
  reviewed .NET 8 / x64 / Linux-or-Windows band; new bands require reviewed measured references.
  See [determinism and fixture policy](docs/DETERMINISM.md).
- **Headless experiments:** `SwarmSim.Cli` references only Core and runs all six engines without a
  display. A completed run contains a versioned manifest with the full resolved native config,
  deterministic invariant-culture CSV, and an operational log. Seeded initialization and manifest
  replay verify the compatibility kinematic and CSV hashes, plus the required complete native
  state hash for schema 3 models. Field-only runs use zero agents and native metrics instead of
  fabricated motion channels. Duplicate manifest fields and unknown fields are
  rejected. Git provenance comes from the built assembly when available; unknown build dirtiness
  remains null and the launch directory cannot invent source identity. Output is published into
  a fresh run directory after completion. See [headless experiments](docs/HEADLESS_EXPERIMENTS.md).

[Experiment foundation](docs/EXPERIMENT_FOUNDATION.md) describes contracts, ownership, examples
and limits. Configuration/spatial/telemetry checks run in the normal non-Performance test gate;
allocation regressions are not hidden behind timing-only test categories.

The analysis library now exposes a foundation-neutral metric frame, registered reusable probes,
ordered selection, and invariant CSV/JSONL writers. Motion and periodic centroids are separate
from exact all-population nearest-neighbor distributions; canonical boids instrumentation is a
registered foundation-specific probe. Runtime costs explicitly declare nondeterminism. The
nearest-neighbor implementation is O(N²), opt-in, and not a 50k-agent throughput claim. Periodic
analysis preserves tiny signed offsets even in very large worlds. See [metrics](docs/METRICS.md).
The CLI now selects registered channels with `--metrics`, records their order and determinism in
schema 2/3 manifests, and verifies the complete deterministic CSV on replay. Original schema 1
manifests remain replayable with byte-identical default CSV. Runtime-cost channels measure only
Step and explicitly disable exact replay. The suite passed 853 tests at the metrics addition; a
separate 50k sparse/1,000-step proof observes zero current-thread allocation in physics, reused
capture/sampling, buffered CSV writing and the final flush. It excludes setup, hashing and
quadratic nearest-neighbor analysis. Exact measurement boundaries and the optional-centroid
optimization are documented in [headless experiments](docs/HEADLESS_EXPERIMENTS.md).

Sequential parameter sweeps now resolve a native base configuration, unique seeds and numeric
grid axes in-process. Every cell is validated before output and receives its own replayable
manifest/CSV/log; the sweep adds a deterministic summary of terminal values, defined-sample means
and sample counts. The 3-seed × 4-value example produces 12 cells, byte-identical repeated
summaries, and an independently replayable cell. Its integrated gate passed 911 tests, including
58 sweep cases. Runtime metrics are excluded from sweeps, which are capped at 10,000 cells and
publish only after the complete attempt succeeds. This does not add parallel scheduling,
statistics or plotting. See [parameter sweeps](docs/HEADLESS_EXPERIMENTS.md#parameter-sweeps).

The CLI now avoids centroid work unless either centroid column is requested. The public
parameterless motion probe still exposes all five channels; its explicit basic mode exposes only
the three values it computes. Its isolated integrated Release gate passed **918 tests**. Original
schema 1 CSVs and selected schema 2 centroid/neighbor CSVs remain byte-identical on replay, and
metric discovery is unchanged. One same-frame 50k/1,000-sample comparison measured 301.411 ms for
the basic plan versus 2,191.723 ms for the full probe, both with zero current-thread allocation
and identical first-three output bits. This excludes physics, capture, frame validation and I/O.
A separate 50k/1,000-step sparse run selecting only seven linear core channels observed zero
allocation in physics, capture/sampling, real buffered CSV writing and final flush. These are
scoped host observations; they do not establish dense-scene speed or interactive FPS.

The isolated foundation Release build (SDK 8.0.415, Linux x64) completed with no warnings/errors;
all **697 non-Performance tests passed** before publication. The isolated golden-trace addition
passes **704 tests** locally. The separately developed headless CLI is excluded from those counts.
Hosted exact-head checks remain the merge gate; platform-specific golden fixtures are verified
against their own platform, not by widening tolerances.

The isolated CLI addition passes **763 non-Performance tests** locally with a clean Release build.
The display-free smoke step runs 400 agents for 600 ticks in both engines, repeats each run, and
replays each manifest: all three CSVs are byte-identical per engine and contain 602 lines.
The CLI dependency graph contains only Cli and Core. CI runs that same smoke on both platforms
and additionally checks the embedded build commit against the tested checkout.

The renderer follow-up adds 39 regression cases and passes **802 tests** in its isolated Release
gate. The default canonical initial/500-tick hashes are unchanged, actual --list-presets output
matches, and preset definitions plus both F1–F5 dispatch blocks are byte-identical to the prior
source. This verifies configuration and diagnostic behavior without claiming window interaction.

## Reproduce validation

```sh
dotnet build SwarmingLilMen.sln -c Release
dotnet test SwarmingLilMen.sln -c Release --no-build --filter "Category!=Performance" -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project SwarmSim.Benchmarks -c Release --no-build -- --canonical-experiment
dotnet run --project SwarmSim.Benchmarks -c Release --no-build -- --canonical
dotnet run --project SwarmSim.Cli -c Release --no-build -- --scenario balanced --seed 7 --ticks 600 --out runs/
dotnet run --project SwarmSim.Cli -c Release --no-build -- --list-engines
dotnet run --project SwarmSim.Cli -c Release --no-build -- --engine reaction-diffusion --agents 0 --seed 7 --ticks 1000 --visual --out runs/
dotnet run --project SwarmSim.Cli -c Release --no-build -- --gallery scenarios/gallery.json --out runs/
```

Rebuild after source changes before using `--no-build`. The canonical benchmark route accepts
BenchmarkDotNet arguments, such as `--canonical --list flat`. Render's older `--benchmark` now
prints migration guidance and exits successfully; experiments use the standalone CLI.

## Performance evidence and its limits

Canonical BenchmarkDotNet measures complete ticks with capture off at 1k/10k/50k agents, seeded
uniform starts, fixed 1920×1080 extents, radius 10, cap 32 and dt=1/60. Worlds evolve during warmup
and measurement. Zero steady-state allocation is separately asserted in the ordinary regression
gate. This is not renderer FPS and does not cover arbitrary densities or active third-party rules.

The target of 50k agents at 60 interactive FPS is **unmet**. Prior legacy measurements and older
canonical experiments are retained with their original dates in the historical tracker; they
must not be presented as measurements of the current revision.

Final foundation-worktree run (2026-10-29, BenchmarkDotNet 0.15.6, SDK 8.0.415/runtime 8.0.21,
Ubuntu 24.04.3 x64, AMD EPYC 9V74 2.60 GHz):

| Agents | Mean ms/tick | Error (99.9% CI) | Standard deviation | BDN allocated/op |
|---:|---:|---:|---:|---:|
| 1,000 | 0.678402 | 0.018490 | 0.054229 | 0 B |
| 10,000 | 10.0191 | 0.47451 | 1.39910 | 5 B |
| 50,000 | 120.7328 | 5.29927 | 15.45822 | 0 B |

The host denied high-priority scheduling and BDN warned of bimodal distributions at 10k/50k.
The 10k allocation row came from 336 bytes across 64 operations; a previous run
reported zero for all cases and the direct current-thread allocation regressions report zero.
A targeted follow-up using the same 10k settings measured 1,320 ticks after warmup with zero
current-thread and process-total allocated-byte deltas and zero collections. This does not
identify the cause of the BDN reading. The nonzero observation is retained rather than rounded
away. These noisy cloud measurements do not establish a performance improvement or a universal
zero-allocation promise.

An opt-in [CompositionCost evidence collector](docs/COMPOSITION_EVIDENCE.md) now retains raw
workload/allocation records, exact commands, selected SDK and child runtime/GC metadata, source
content fingerprints, and hashes of the actual executed child build and bundle artifacts. Its
40-test Python suite and real frozen Linux captures pass; integration/exact-head CI now exercises
the actual smoke on Ubuntu and Windows. The collector checks raw bytes against BDN 0.15.6/.NET 8
integer B/op rounding and records its process-wide scope. A complete receipt proves that the
requested evidence was collected; it does not qualify performance or explain the earlier allocation
sample. Raw bytes and operations remain separate from rounded B/op. The original-baseline recheck
retains all 20 raw 1k measurements, observes reversing pair deltas and leaves #20/#27 open; see
[the matched results and their limits](docs/CANONICAL_COMPOSITION.md#original-baseline-recheck-2026-11-02).

The composition change uses a committed [controlled benchmark wrapper](tools/CompositionCost/Program.cs)
around the same canonical benchmark. On the host/runtime above, matching 64-invocation runs
measured the following complete-tick means (all rows measured zero allocated bytes):

| Agents | Before composition (ms) | With composition (ms) | Observed mean delta |
|---:|---:|---:|---:|
| 1,000 | 0.478583 | 0.653493 | +36.55% |
| 10,000 | 7.4782 | 8.1232 | +8.63% |
| 50,000 | 114.8636 | 95.8358 | −16.57% |

The short 1k iterations warranted a matched confirmation at 1,024 invocations per iteration,
three warmups and five measurements. That measured **0.481998 → 0.561671 ms/tick (+16.53%)**,
again with zero allocated bytes. Including the before sample excluded by BenchmarkDotNet gives
+14.74%. These results retain a real observed small-population cost for follow-up; overlapping
confidence intervals do not prove no regression, and the single 50k decrease is not a general
speedup claim. Source identities, commands, uncertainty and all raw iteration averages are in
the [composition cost record](docs/CANONICAL_COMPOSITION.md#measured-composition-cost-2026-10-29).
The new zero rows do not establish the cause of the historical 10k nonzero observation above.

A separate neighbor-reuse follow-up removes the second visible-neighbor traversal and repeated
minimum-image/square-root calculations. Every policy still uses the same bounded observation,
writer and resolver. Independent before/after checks compare all state, neighbor views, telemetry
and instrumentation over 16 Grid/Naive traces of 128 ticks; every byte matches. Six analytic
cases pin periodic ties and existing empty/overflow diagnostics. In paired measurements against
the initial composition, observed means were 0.565134 → 0.527112 ms/tick at 1k (−6.73%) and
98.743237 → 88.032047 ms/tick at 50k (−10.85%), with zero measured allocation. The intervals
overlap and the samples trend; this does not establish a general speedup or recovery of the
original small-population cost. See [raw samples, source identities and limits](docs/CANONICAL_NEIGHBOR_REUSE.md).

The issue #67 query probe used 512 boids, seed 8181, radius/cell size 50, a 64-slot buffer, 4,096
warmup queries and seven batches of 32,768 queries with tiering disabled. Adjacent baseline/final
median ns/query were: grid sparse 439.5/453.2, grid dense 14,708.8/14,415.6, naive sparse
23,638.8/19,196.0 and naive dense 18,903.0/17,951.3. All measured query allocations were zero.
The earlier grid-sparse baseline was 370.1 ns, demonstrating substantial host variation. These
measurements check the cold-path precision fix for a gross regression; they do not establish a
speedup or machine-independent performance bound.

## Current limitations and next work

The owner-directed continuation in [epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10)
allows richer-state work to proceed alongside the remaining boids qualifications. The next native
integrations are tracked under [#225](https://github.com/Chris0Jeky/SwarmingLilMen/issues/225):

1. **Avalanche (#235):** integer heights, exact mass accounting and deterministic relaxation with
   an explicit work budget and unfinished-event state. Reuse field observation without silently
   rounding authoritative integers or treating partial relaxation as a stable system.
2. **PSO (#236):** one defined objective, personal/shared best memory, stable tie rules, named RNG
   and explicit synchronous-versus-sequential best publication. Hash the memory that affects the
   next step, not only current positions.
3. **Ecogenesis (#237):** stable lifecycle identities, deterministic birth/death publication,
   resource accounting and survivor RNG ownership before adding a broad ecology/genome model.

Each integration must preserve old goldens/replay, supply analytical and controlled behavior
evidence, and reuse or justify a concrete shared mechanism. The shared scheduler and full-state
hashes are implemented; heterogeneous coupling, generic event/lifecycle semantics, reusable
stencil operators, richer field/vector observations and checkpoint restore remain separate work.
An in-process common scheduler does not itself provide those contracts.

Other active work retains its existing acceptance criteria:

- #20/#27: investigate the historical canonical 10k BDN allocation observation and qualify the
  measured composition cost. Direct zero-allocation probes and inconsistent short benchmark pairs
  do not explain the old sample or prove performance recovery.
- #28: reduce the complete primary scientific matrix below 60 seconds without changing recipes,
  seeds, cadence, metrics or locked bands. The 2026-11-02 qualification is recorded above; its
  runtime target remains unmet.
- #40: physical input, full-window/DPI and human visual acceptance. Recorded Linux software-rendered
  captures and automated probes do not substitute for that scope.
- #41: define and implement a meaningful persistent faster/slower overtaking case.
- #187: derived-control overflow, subnormal/scaling behavior, accumulation and sample-count limits
  beyond the bounded numerical fixes already merged.
- #164: publication-scanner indirection outside the admitted grammar. #44 remains the future
  prerequisite before executing untrusted extensions.

Exact replay remains scoped to its implementation/runtime band (#21). Runtime channels declare
nondeterminism, exact nearest-neighbor analysis is opt-in O(N²), and sweeps remain sequential.
Legacy retirement still requires the owner's eventual migration sign-off.

The documentation residue audit (#60/#63) now uses stable source symbols, current headless and
benchmark commands, explicit historical measurement labels and the actual sequential/stochastic
browser-demo semantics. The old readiness checklist points scheduling back to the epic instead
of directing new work to closed issues.

A green engine suite does not validate every renderer interaction, browser demo or deployment.

## Trust and publication boundary

In-process simulation rules have the host process's authority. The engine is a modeling boundary,
not a security isolation mechanism. Untrusted code requires the separate containment workstream
(#44). Static-demo publication remains allowlisted and manually dispatched; this research work
does not change repository settings, publish releases, or deploy Pages.
