# SwarmingLilMen - Project Status & Implementation Tracker

**Last Updated**: 2026-08-08 (documentation sweep; Wave 1 landed the toroidal spatial-index
contract, the source-comment reconciliation, CLI preset help, and the shared `MaxForce` budget)
**Current Phase**: Canonical readiness before Phase 3 - migration, parity, and performance evidence incomplete

> **READ ORDER**: This verified-state section is the live source of truth. The phase checklists and
> session log below are retained as historical planning context and contain stale counts/claims.
> Git, executable checks, and current code override them.

## Verified Live State (2026-08-08)

- Git: Wave 0 began from clean `main` at `8108254`, matching `origin/main`, after a dormant period
  whose last product commits landed on 2025-11-19. Wave 0 and Wave 1 have since merged product
  changes on 2026-08-07 and 2026-08-08, and #65 adopted GPL-3.0-only on 2026-09-02; `main` is at
  `f005277` as this block is written.
- GitHub: public repository; Wave 0 and Wave 1 work is tracked by epic #10. The repository CI
  workflow supplies Release build/test checks (excluding the `Performance` category) on ubuntu
  and windows for pushes and pull requests targeting `main`. No branch-protection ruleset exists,
  so merge gating remains process-enforced. Pull-request jobs test GitHub's head/base merge ref;
  `workflow_dispatch` is available for an exact branch-head rerun.
- Repository hygiene: the advertised GPL-3.0-only license is present; local debug captures, generated run
  outputs, and the unused vendored SDK installer are excluded from the tracked root set. The
  previously committed debug capture remains in Git history; no history rewrite was performed.
- Documentation entrypoints now point back to this verified-state section and distinguish the
  unmet performance objective from the dated legacy-core sample. Older phase/session prose remains
  under dated banners in `docs/archive/`. The root contains the five named Markdown entrypoints plus
  `LICENSE`, and — since the GPLv3 adoption in #65 — `RELICENSING.md` and `LICENSES/`; live guides
  are under `docs/`, and `docs/ROADMAP-VISION.md` is explicitly aspirational while epic #10 remains
  the executable plan.
- Toolchain: .NET SDK `8.0.415` satisfies `global.json` (`8.0.0`, latest-minor roll-forward).
- NuGet audit: no known vulnerable direct/transitive packages from nuget.org. Available top-level
  updates include Raylib-cs 8.0.0, coverlet.collector 10.0.1, Microsoft.NET.Test.Sdk 18.8.1, and
  BenchmarkDotNet 0.15.8; compatibility has not been tested.
- CI-filtered Release solution test (`Category!=Performance`): **114 passed, 0 failed, 0 skipped**
  in 2 seconds of test execution on 2026-08-08.
- Unfiltered Release solution test: **118 passed, 0 failed, 0 skipped** in 23 seconds of test
  execution, confirming the full suite remains under one minute.
- Explicit Release `Performance` category: **4 passed, 0 failed, 0 skipped** on 2026-08-08.
  Command after the Release build:
  `dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj --configuration Release --no-build --filter "Category=Performance" --logger "console;verbosity=detailed" -- RunConfiguration.TreatNoTestsAsError=true`.
  This one local fully optimized-JIT sample measured:
  - 1k legacy agents: 0.177 ms/tick (5,641 operations/second)
  - 10k legacy agents: 9.498 ms/tick (105.3 operations/second)
  - 50k legacy agents: 261.077 ms/tick (3.83 operations/second; reported target not met)
  - 50k grid rebuild: 0.104 ms
- The `Performance` category exercises the **legacy** world tick and the uniform-grid **rebuild**.
  It does not touch `UniformGrid.QueryRadiusToroidal`, so it cannot speak to the query-reach change
  in this wave. That path was measured separately and directly: a temporary Release harness ran one
  million grid queries over 4,000 boids at radius 12.5 against both this head and its pre-fix
  parent (`48d5c06`), in a world whose extent is a whole multiple of the cell size and in one that
  leaves a partial terminal cell. One local sample each, measured at this head: exact-multiple
  6,380.7 ns/query before versus 6,146.4 ns/query after, partial-terminal 6,689.1 ns/query before
  versus 6,243.6 ns/query after, with identical neighbor-count checksums in both worlds. The
  `double` accumulator costs nothing measurable because the walk is O(cells) per query rather than
  per neighbor, and every current caller walks at most two cells. The harness was removed. This is
  one sample per configuration, not a benchmark distribution.
- Performance-category tests are excluded from the default CI suite. When run explicitly, all four
  measurements compare matching operation horizons after warmup, gate generous machine-relative
  scaling envelopes, and emit JSON records. Release test hosts disable tiered compilation so the
  ratios do not depend on test-order-specific tier promotion; absolute throughput targets remain
  reported-only. The dated figures are one local sample, not a stable benchmark distribution. A
  green default or performance-category run therefore does **not** prove the 50k/60 FPS headline
  objective.
- A separate reported-only Release diagnostic initialized identical 1,000-agent canonical Grid and
  Naive worlds, warmed each for 50 ticks, then alternated 100 measured ticks. This one sample
  measured 1.213 ms/tick for Grid and 27.155 ms/tick for Naive (22.40x); the temporary harness was
  removed. This is not a benchmark distribution, scale curve, or replacement for #20.
- CI-filtered coverage report (`XPlat Code Coverage`, Release, `Category!=Performance`): **60.47%
  line / 44.93% branch overall**; `SwarmSim.Core` is 86.58% line / 77.03% branch and
  `SwarmSim.Render` is 29.74% line / 12.70% branch. The instrumented timing tests are intentionally
  excluded from this coverage sample; renderer automation remains the dominant gap.
- Active implementation: legacy SoA `World`/`Systems` remains the default renderer and benchmark
  target. Canonical boids is opt-in through `--canonical` and remains the intended future path.
  Core scaffolding and the three steering-rule implementations exist. Canonical `ISpatialIndex` now
  specifies initialized/rebuilt, self-excluding, inclusive circular toroidal queries with ascending
  lowest-index truncation and caller-visible status; Grid and Naive matched in 200 deterministic
  randomized scenarios plus seam/corner/dense/one-cell, partial-terminal-cell, and unwrapped-input
  cases and a 200-tick no-wander trajectory.
  Two equivalence gaps found in review were reproduced as failing tests and then closed. The grid
  derived its scan reach from the wrapped query endpoints' cell IDs, so when the world extent is not
  a whole multiple of the cell size a circular interval could cross the whole short terminal cell
  while both endpoints wrapped back into the centre cell, silently dropping qualifying neighbors
  (width 18 / cell size 10, query at x=0.5 r=8.6 missed a neighbor 7.1 units away); the reach now
  walks outward accumulating each cell's actual extent. Separately, `NaiveSpatialIndex` subtracted
  raw coordinates and wrapped afterwards while `GridSpatialIndex` subtracts already-wrapped stored
  positions, so the two rounded differently in float32 and disagreed about neighbors on the
  inclusive radius boundary for far-out-of-range inputs; both now normalize first.
  A third defect lived in the replacement reach itself: accumulating each cell's extent in `float`
  rounds in both directions, so a deep walk could stop one cell early and drop a neighbor strictly
  inside the radius. It needed a cell size not exactly representable in binary32 plus a walk of
  hundreds of cells, so no current caller could reach it -- every construction site passes
  `cellSize == SenseRadius` -- but the public constructor invites `cellSize < radius`. The running
  total is now a `double`; two measured reproducers are pinned as regression tests. Separately,
  `MathUtils.Wrap` could return exactly `max` for tiny negative inputs (`Wrap(-1e-9f, 100f)` was
  `100f`), which broke the `[0, extent)` invariant the hot path relies on to skip re-normalizing;
  it now folds to the origin. Grid/Naive equivalence is therefore exact by construction rather than
  approximate -- an adversarial differential sweep of roughly 1.5 million generated cases (random,
  dense, exact-boundary radii, and structural) found only the drift defect above, now fixed.
  Normalizing both indexes then exposed a third disagreement one level up: `Vec2.MinimumImageDelta`,
  which field-of-view filtering and every steering rule use, subtracts the stored coordinates
  directly, and `TryAddBoid` stored seeded positions raw. A boid spawned outside the world could
  therefore be accepted by the spatial query and simultaneously rejected by `SeparationRule` at the
  radius boundary, until the first `Step` happened to wrap it. `TryAddBoid` now applies the same
  normalization `Step` does, so stored positions are in `[0, extent)` by invariant; the per-neighbor
  hot path keeps its direct subtraction and `Vec2.MinimumImageDelta` documents the precondition.
  Minimum-image deltas now cover FOV, whiskers, neighbor statistics, separation, and cohesion.
  The renderer overlay uses the same deltas for links/hit classification and labels capped queries.
  `CanonicalWorld.EffectiveMaxNeighbors` exposes the per-boid candidate cap `Step` actually applies,
  and the interaction overlay sizes its query to it; it previously queried with its own 128-entry
  buffer while steering used 16, drawing discarded neighbors and suppressing the truncation notice
  in exactly the cases where the simulation had truncated. Renderer overlay drawing itself is still
  only verified by the extracted capacity seam, not by running the window.
  The full Release suite passes after correcting a priority-hysteresis test setup that had depended
  on the pre-contract seam behavior. Composition now honours its total `MaxForce` bound (#19):
  separation draws from the same per-tick remainder as whisker avoidance, alignment, cohesion, and
  wander instead of clamping to a fresh `MaxForce` and adding on top. Measured on a 200-agent
  100x100 dense crowd over 300 ticks with `MaxForce = 2.5`: before the fix the worst composed
  steering was 5.000000 (ratio exactly 2.0000) with 43,588 of 60,000 agent-ticks over budget;
  after it, 2.500000 (ratio 1.0000) with 0 over budget. The composed magnitude is observable
  through `RuleInstrumentation.SteeringMagnitudesSquared`, recorded just before integration.
  This intentionally changed canonical trajectories; the seed-pinned kinematic hash for that
  scenario moved from `37EAE868...` to `FE8295A7...` while the initial-state hash is unchanged.
  Those two values come from a temporary probe over the dense-crowd scenario defined in
  `CanonicalSteeringBudgetTests.CanonicalWorld_DenseCrowd_NeverExceedsMaxForceBudget` (200 agents,
  seed 20260807, 300 ticks); no committed test emits them, so reproducing them means rebuilding
  that probe. They are a before/after record, not a golden fixture.
  Rule dispatch remains positional: later rules are discarded and qualifying separation starves
  alignment, cohesion, and wander, including wander-angle updates (#27).
  Issue #27 owns the replacement: named composition plus bounded Observation/Intent contracts and
  kernel-resolved arbitration.
  Canonical construction now rejects unsafe turn, wander, whisker, and separation-priority control
  values mapped by this wave; comprehensive validation of the older base geometry/time/rule fields
  remains tracked in #48.
  Instrumentation UX remains partial (#40). Full prescribed milestone 3-6 scenario acceptance is
  unverified (#41). Milestones 8-10 and multi-group semantics remain incomplete.
- Reproducibility: legacy wander consumes the world's configured RNG; canonical construction maps
  `SimConfig.Seed` and the canonical steering settings, and each wander-enabled boid receives an
  index-derived stream at successful spawn. The supported `--minimal` legacy harness also samples
  velocities and staged spawn positions from `World.Rng`, not process-global randomness. External
  seeds are limited to `0..2147483647`; config-only legacy construction is preferred, while the
  established explicit-seed overload remains authoritative and normalizes `World.Config` without
  mutating its caller. The established internal full-width derived-stream mapping
  remains unchanged pending #26. Exact
  ordered kinematic hashes cover both paths for 500 ticks, and two fresh processes running
  `configs/balanced.json` produced the same 600-tick SHA-256 kinematic hash
  (`DAF60518...5BB770F6`). The version-1 hash covers agent count plus ordered X/Y/Vx/Vy bits; it
  intentionally excludes configuration, clocks, RNG/wander state, groups, lifecycle/resources,
  and genomes. This verifies same-.NET-8-binary/same-platform kinematics for a matching
  configuration, timestep, and ordered input sequence; it does not claim broader world-state,
  reset-event, live-reconfiguration, cross-runtime, or cross-OS identity. Cross-OS measurement
  remains #21.
- Intentional trajectory change: the shared unauthored renderer default now follows the
  `SimConfig` contract (`WanderStrength: 0`, previously `0.45`) for both legacy and canonical paths;
  authored presets/JSON values still opt into wander. In a comparable 64-agent canonical diagnostic,
  the initial 64-bit diagnostic remained `E4DF43EE92B466D4`, while the 500-tick diagnostic changed
  from `559B9F9DDD9A26DC` to `1902D36DF3FE793A`. **These six 16-hex values are not reproducible from
  this repository and were previously mislabelled "FNV-1a".** `SimulationKinematicHash` computes
  SHA-256 (64 hex) and no FNV implementation exists anywhere in the tree; the 16-hex figures came
  from a temporary word-wise diagnostic that has since been removed. They are retained only as a
  historical before/after record, and no session should expect to reproduce them.
  The reproducible evidence is the SHA-256 emitted by
  `DeterminismTests.RendererDefaultCanonicalTrajectory_EmitsIntentionalAfterHash`. Measured on
  2026-08-07 against both checkouts: the initial hash is
  `A638454B49E23E2595060531CD2E64D65ABFD7140B61E283F02170E446A2BCC6`
  on `main` and unchanged at this head, while the 500-tick hash moves from
  `2F9351E2E39BBACE0B71E6A488841A63471056965E4C05FE5BAB7AA9C7091781` on `main` to
  `29F3DC6129BF88265DF4E78C88AA01C328CDE7C47DDD715D5C869D8367201FE1` here, as seam-adjacent
  neighbors now influence steering. No behavior retuning was included; these remain diagnostics
  rather than golden fixtures.
  Wander-enabled canonical construction currently retains one `Rng`/`System.Random` object per
  boid; it adds no tick-time allocations in the measured probe but is a setup/GC scale risk owned by
  the broader RNG-stream work in #26.
- CLI help preset advertising is corrected (#38, 2026-08-07). `--help` no longer names the JSON
  configuration files `peaceful` and `warbands` as `--preset` values; the option parenthetical and
  the usage example are now derived from `Program.PresetIds`, and the JSON names moved to the
  `--config` line where they are valid. The class of drift is closed rather than the instance:
  `Program.PresetIds` and `Program.IsRegisteredPreset` expose the runtime registry and the same
  `TryGetPreset` lookup `Main` uses, and tests assert every advertised name resolves through it.
  A further test compares the README's verbatim help transcript against the live help text, so the
  README cannot drift again; it was mutation-checked rather than assumed. Note the unknown-preset
  path still exits **0**, so a mistyped preset remains indistinguishable from success to a script —
  that is tracked separately in
  [issue #53](https://github.com/Chris0Jeky/SwarmingLilMen/issues/53).
- Runtime parameter/help labels are partial and still advertise keys `1-7` while input accepts
  **1-8**; the canonical **H** panel also omits several active controls. Friction key 8 is a no-op
  for registered legacy presets because they inherit `ConstantSpeed`, and canonical settings do
  not consume friction; only an explicit custom legacy `Damped` configuration uses it. Runtime
  synchronization and regression coverage are tracked in
  [issue #39](https://github.com/Chris0Jeky/SwarmingLilMen/issues/39).
- Source comment accuracy in the legacy path is reconciled (#42, 2026-08-07). `SenseSystem` no
  longer claims inverse-distance or inverse-square separation: the implementation scales a unit
  away-vector by the bounded linear falloff `(separationRadius - distance) / separationRadius` and
  nothing else, `BehaviorSystem` re-normalizes the aggregate so that falloff acts as a per-neighbor
  direction blend weight rather than a force gain, and neighbors nearer than 0.01 units are skipped
  outright rather than pushed at maximum strength. The `SpeedModel.ConstantSpeed` doc no longer
  claims `friction = 1.0`, direction-only steering, or that agents hold `MaxSpeed`; the integrator
  skips friction entirely in that mode, adds steering straight into velocity, and applies an upper
  clamp only. A characterization test pins both corrected claims. The canonical `SeparationRule`
  genuinely does combine linear falloff with `1/d`, so the canonical descriptions elsewhere in this
  file remain correct and were deliberately left alone. No executable line changed.
- Test inventory: 118 executed test cases across 12 test files — 114 `[Fact]`/`[Theory]` attributes,
  with `[Theory]` `InlineData` expanding the remainder. Includes four explicitly categorized
  performance measurements and a zero-allocation steady-state assertion for both canonical spatial
  query implementations. No canonical BenchmarkDotNet comparison, renderer automation, coverage
  gate, or absolute-throughput gate currently exists.
- Complexity hotspots: `SwarmSim.Render/Program.cs` is 2,012 lines and
  `SwarmSim.Core/Canonical/CanonicalWorld.cs` is 755 lines. Whole tracked C# tree: 10,665 lines.
- Agent controls were refreshed in this audit: shared repo rules and per-seam proving checks in
  `CLAUDE.md`, with `AGENTS.md` reduced to a thin Codex adapter over it that also carries the
  runtime-neutral fail-safe floor for clones with no estate profile; plus the T1 declaration, safe
  committed Claude settings, read-only validator, and Codex project adapter/settings. The adapter audit marker matches released floor 1.6.5. In a fresh
  exact-repository Codex session the owner reviewed and accepted the adapter, `/hooks` reported
  `PreToolUse` as 1 installed / 1 active, `git status` was allowed, and an inert force-push dry-run
  was denied before Git executed. Runtime byte pinning remains an agent-harness #18 limitation.
- Documentation sweep (2026-08-08). Every tracked Markdown surface was audited against the code by
  eight independent readers, each finding adversarially re-checked; 72 defects were confirmed and 14
  claims were rejected as unreproducible. Fixed here: stale counts (`CanonicalWorld.cs` 732 -> 755,
  "11 test files" -> 12, QUICKSTART's expected 64 passing tests against a suite reporting 114);
  closed issues still listed as open (#17 in the readiness checklist, #18/#19 in RENDERER_GUIDE and
  README); `CONTRIBUTING`'s test-file tree naming a `GenomeTests.cs` that has never existed; three of
  the four legacy force descriptions in `SIMULATION_MECHANICS_EXPLAINED`, each of which claimed a
  weight or a distance scales the force where the implementation normalizes it away, plus that
  file's complete silence about the shared `MaxForce` budget; and the duplicated 2026-07-25
  performance sample in README and RENDERER_GUIDE, which contradicted the newer sample in this
  block. **That cleanup is incomplete and the remaining copies are still live**: the 162.815
  ms/tick sample survives in `js-demos/README.md`, `js-demos/IMPLEMENTATION_COMPARISON.md`,
  `docs/PHASE_3_READINESS_CHECKLIST.md`, and the historical sections of this file. Only the
  entry-point docs were cleared; treat this block, not those copies, as current.
  Two behavioural facts were measured and documented for the first time: `LoadFromJson` registers no
  string-enum converter, so `{"SpeedModel":"Damped"}` fails the entire load while `{"SpeedModel":1}`
  succeeds; and every bundled config authors a `Friction` value while none sets `SpeedModel`, so all
  three inherit `ConstantSpeed` and the integrator skips friction outright.
  Two dead configuration knobs were found and filed rather than fixed: `GridCellSize` (#61) is
  validated, defaulted, and copied but never read, because `World` hard-codes `cellSize:
  Config.SenseRadius`; and `MaxCapacity` (#62) is validated `>= InitialCapacity` while the arrays
  never grow past `InitialCapacity`. The remaining unfixed findings are tracked in #63.
  **Not verified: the renderer window still has not been launched.** No claim in this sweep rests on
  observing the running UI.
- Extension trust: the product term **sandbox** is retired. **Modeling boundary** / **interaction
  surface** are reserved for simulation concepts, the aspirational package sketch uses
  **Playground**, and the shipped app remains `SwarmSim.Render`. Data-only scenario input is a
  current design fact, not a security guarantee; in-process extensions have full host authority.
  Future untrusted code is blocked on the dedicated containment prerequisite in
  [issue #44](https://github.com/Chris0Jeky/SwarmingLilMen/issues/44), an unscheduled marker with no
  owner or timeline. Until its gates pass, no product name, flag, package, API, or product-facing
  documentation may imply containment. The formal T1 authority label in agent-control files is an
  intentional non-product exception and does not claim containment. Any future IPC endpoint must
  bind to loopback and authenticate peers by default; remote exposure requires a separate threat
  model and explicit design decision.

---

## Quick Context for Future Sessions

> **HISTORICAL CONTEXT:** Numeric performance and test-count entries below preserve prior session
> reports whose exact commands/hardware were not recorded; they are unmeasured for the current
> state. Only the verified block above is current evidence.

### What This Project Is
A 2D swarm simulation in C#/.NET 8.0 with an unmet target of 50k-100k interactive agents at
60 FPS. Its data-oriented design emphasizes determinism and allocation-conscious hot paths; the
verified block above records what is actually measured.

### What's Been Done

**Session 1 - Foundation (originally P0)** ✅
- Solution structure, build system, core data structures
- World class with SoA layout, Rng, MathUtils
- Original test snapshot (21 facts), Raylib rendering, full documentation
- **Foundation Complete**

**Session 2 - Phase 1 (Spatial Grid & Basic Movement)** ✅
- ✅ **Spatial Grid**: UniformGrid with Head[]/Next[] linked lists, O(1) insertion, 3x3 queries
- ✅ **Systems**: ISimSystem interface, IntegrateSystem, RandomWalkSystem
- ✅ **World Integration**: Systems pipeline, grid rebuild each tick
- ✅ **Tests**: 14 new UniformGrid tests + 4 performance tests (39 total, all passing)
- ✅ **Benchmarks**: WorldTickBenchmarks and GridBenchmarks (BenchmarkDotNet)
- ✅ **Performance Results** (Release mode):
  - **50k agents: 1.92ms/tick (521 FPS)** - 8.7x better than 60 FPS target! 🚀
  - 10k agents: 0.38ms/tick (2,612 FPS)
  - 1k agents: 0.069ms/tick (14,522 FPS)
  - Grid rebuild (50k): 0.093ms
- ✅ **PHASE 1 COMPLETE** - Ready for Phase 2 (Boids)

**Session 2 - Phase 2 (Boids & Flocking)** ✅
- ✅ **SenseSystem**: Queries spatial grid, computes neighbor aggregates (separation, alignment, cohesion)
- ✅ **BehaviorSystem**: Applies boids rules to generate forces from aggregates
- ✅ **World Integration**: Updated systems pipeline (Sense → Behavior → Integrate)
- ✅ **Tests**: 8 new boids behavior tests (43 total, all passing)
  - Separation, alignment, cohesion verified individually
  - Cross-group non-interaction, speed limits, determinism confirmed
- ✅ **Performance Results** (Release mode):
  - **10k agents: 129 FPS** ✅ (exceeds 60 FPS target)
  - **1k agents: 8,555 FPS** ✅
  - 50k agents: 6.5 FPS (below target, optimization opportunity for Phase 5)
- ✅ **Emergent Behavior**: Visible flocking with separation, alignment, and cohesion
- ✅ **PHASE 2 COMPLETE** - Ready for Phase 3 (Combat & Metabolism)

**Session 3 - Architecture Improvements (Post-P2)** ✅
- ✅ **Part A - Canonical Boids**: Refactored to Reynolds steering behaviors, added FOV filtering, MaxForce parameter, SpeedModel enum
- ✅ **Part B - Fixed Timestep & Decoupling**: SimulationRunner with accumulator loop, SimSnapshot for safe state sharing, interpolated rendering
- ✅ **Tests**: 49/51 passing (96.1%), deterministic physics across framerates
- ✅ **ARCHITECTURE IMPROVEMENTS COMPLETE** - Ready for Phase 3 (Combat & Metabolism)

### What to Work On Next
**Phase 3**: Groups, Combat, Energy & Metabolism
- Implement CombatSystem for inter-agent attacks
- Implement MetabolismSystem for energy drain and death
- Implement LifecycleSystem for death cleanup
- Add ForageSystem for energy replenishment
- Use aggression matrix for inter-group behavior
- Target: Multiple groups with combat and survival mechanics

### Key Files to Reference
- `docs/PHASE_3_READINESS_CHECKLIST.md` - Current canonical-readiness checklist
- `docs/ROADMAP-VISION.md` - Aspirational engine vision; epic #10 is the executable plan
- `docs/archive/IMPLEMENTATION_EVOLUTION.md` - Historical explanation of the implementation pivot
- `docs/archive/NewImplementation.md` - Historical TDD roadmap for canonical boids (milestones 0-10)
- `docs/archive/swarming_lil_men_master_plan_v_1.md` - Original detailed design document
- `CLAUDE.md` - Development guidelines and commands
- This file (`PROJECT_STATUS.md`) - Current status and next steps

### Critical Context: Implementation Pivot

**⚠️ IMPORTANT**: The project underwent a significant architectural transition starting around commit `f5d9dca`. We are migrating from a systems-based SoA approach to a canonical boids implementation. **Two implementations currently coexist**:

1. **Legacy Implementation** (`SwarmSim.Core/World.cs`, `Systems/`):
   - Structure-of-Arrays (SoA) with systems pipeline
   - Bounded steering from `BehaviorSystem` is integrated with selectable speed models; the
     current renderer/presets use `ConstantSpeed` (no damping, upper cap only), while `Damped`
     applies friction
   - Two-pass: SenseSystem → BehaviorSystem
   - **Status**: Working but deprecated, hard to debug
   - **Run with**: Default renderer (no `--canonical` flag)

2. **Canonical Implementation** (`SwarmSim.Core/Canonical/`):
   - Immutable `Boid` structs and independent `IRule` implementations
   - Reynolds-style steering rules with positional, incomplete world-level composition (#27)
   - Single-pass with per-agent decision-making
   - **Status**: core and rule implementations exist; perception, composition enforcement,
     prescribed scenario acceptance, and instrumentation UX are partial; milestones 8-10 and
     multi-group semantics incomplete
   - **Run with**: `dotnet run --project SwarmSim.Render -- --canonical`

**Before Phase 3**: We must resolve seeded reproducibility, perception, and composition defects;
complete milestone 7's UX/test acceptance and milestones 8-10; add multi-group support; and
validate performance. See `docs/archive/IMPLEMENTATION_EVOLUTION.md` for the preserved pivot
narrative and what needs to happen next.

**Recommendation**: All Phase 3+ features should be built on the canonical implementation, not the legacy one.

---

## Architecture Quick Reference

### Project Structure
```
SwarmSim.Core/          - Core library (SoA data, systems, deterministic logic)
SwarmSim.Render/        - Raylib-cs visualization (references Core)
SwarmSim.Tests/         - xUnit tests (references Core AND Render)
SwarmSim.Benchmarks/    - BenchmarkDotNet suite (references Core)
```

### Systems Pipeline Order
1. `SenseSystem` → 2. `BehaviorSystem` → 3. optional `WanderSystem` → 4. `IntegrateSystem`

Combat, forage, reproduction, metabolism, and lifecycle systems are future Phase 3+ work
(`SwarmSim.Core/World.cs:119-144`).

### Data Layout (SoA)
Agent arrays: `X[]`, `Y[]`, `Vx[]`, `Vy[]`, `Fx[]`, `Fy[]`, `Energy[]`, `Health[]`, `Age[]`,
`Group[]`, `State[]`, `Genomes[]`, `LastAttackTime[]`

---

## Implementation Priority Queue

### Foundation Milestone (P0) - ✅ COMPLETE
**Goal**: Basic data structures, World skeleton, can compile and run minimal simulation

- [x] **Core Data Structures** (SwarmSim.Core/)
  - [x] `Genome.cs` - readonly record struct with traits, mutation logic
  - [x] `AgentState.cs` - byte flags enum with extension methods
  - [x] `SimConfig.cs` - Complete configuration with presets (Peaceful, Warbands, Evolution)

- [x] **Utilities** (SwarmSim.Core/Utils/)
  - [x] `Rng.cs` - Full deterministic RNG with Gaussian, unit vectors, shuffle, etc.
  - [x] `MathUtils.cs` - Complete vector math, distance, normalization, wrapping, reflection

- [x] **World Class** (SwarmSim.Core/)
  - [x] `World.cs` - Complete SoA arrays (13 arrays), agent management
  - [x] Add/Remove/Spawn methods with multiple overloads
  - [x] Basic integration loop with boundary handling
  - [x] Compaction for dead agents
  - [x] Stats retrieval and readonly span accessors

- [x] **Basic Tests** (SwarmSim.Tests/)
  - [x] `RngTests.cs` - 8 tests covering determinism, distributions, shuffling
  - [x] `WorldTests.cs` - 13 tests covering agents, boundaries, determinism
  - [x] **Original suite passing (21 facts)**

- [x] **Minimal Render** (SwarmSim.Render/) ✅ COMPLETE
  - [x] Program.cs - Full Raylib implementation with window, world, render loop
  - [x] Draw agents as colored circles (16-color palette for groups)
  - [x] Interactive controls (mouse spawn, keyboard commands)
  - [x] HUD with FPS, stats, controls
  - [x] Starts with 1000 agents in 4 groups

- [x] **Documentation**
  - [x] README.md - Project overview
  - [x] CONTRIBUTING.md - Developer guide with IDE setup
  - [x] `docs/QUICKSTART.md` - 5-minute guide

**Exit Criteria**: ✅ Can create World with 1000 agents, render them as dots, window runs at 60 FPS

**Status**: ✅ **FOUNDATION COMPLETE** - All planned foundation features were recorded as complete.

---

### Phase 1: Spatial Grid & Basic Movement (P1) - ✅ COMPLETE
**Goal**: Agents move randomly, spatial grid works, 50k agents @ 60 FPS

- [x] **Spatial Grid** (SwarmSim.Core/Spatial/)
  - [x] `UniformGrid.cs` - Head[]/Next[] arrays, Rebuild(), Query3x3()
  - [x] Grid tests - compare with brute force for correctness (14 tests)

- [x] **Basic Systems** (SwarmSim.Core/Systems/)
  - [x] `ISimSystem.cs` - Interface: `void Run(World world, float dt)`
  - [x] `IntegrateSystem.cs` - Apply velocity, update position, wrap/reflect bounds
  - [x] `RandomWalkSystem.cs` - Add random forces (temporary, for testing)

- [x] **World Integration**
  - [x] Updated World.cs to use systems pipeline
  - [x] Grid rebuild called each tick
  - [x] ClearForces() at tick start
  - [x] All systems run in sequence

- [x] **Performance**
  - [x] Performance tests with 1k, 10k, 50k agents
  - [x] Benchmark suite with WorldTickBenchmarks and GridBenchmarks

**Exit Criteria**: ✅ **ALL MET** - 50k agents @ 521 FPS (8.7x target!), grid working perfectly, 39 tests passing

**Status**: ✅ **PHASE 1 COMPLETE** - Performance far exceeds goals. Ready for Phase 2.

---

### Phase 2: Boids & Flocking (P2) - ✅ COMPLETE
**Goal**: Implement separation, alignment, cohesion - see emergent flocking

- [x] **Boids Systems** (SwarmSim.Core/Systems/)
  - [x] `SenseSystem.cs` - Query neighbors via grid, compute local aggregates
  - [x] `BehaviorSystem.cs` - Boids rules → forces into Fx[]/Fy[]
  - [x] Replaced RandomWalkSystem with boids behavior

- [x] **Tests**
  - [x] 8 boids behavior tests (separation, alignment, cohesion)
  - [x] Property test: agents stay within max speed
  - [x] Determinism verified

- [x] **Configuration**
  - [x] SimConfig already has boids weights and radii
  - [x] Parameters tunable via config

**Exit Criteria**: ✅ **ALL MET** - Visible flocking, agents form cohesive groups, parameters in SimConfig

**Status**: ✅ **PHASE 2 COMPLETE** - Boids behavior fully functional. 10k agents @ 129 FPS exceeds target.

---

### Canonical Boids Implementation (Post-P2 Rewrite)
> **See `docs/archive/IMPLEMENTATION_EVOLUTION.md` and `docs/archive/CanonicalBoids_SmoothingPlan.md` for full technical details**

- **Status**: In active development; core scaffolding and rule implementations exist, but
  perception, composition, instrumentation UX, milestones 8-10, multi-group semantics, and
  canonical performance evidence remain incomplete
- **Why the rewrite**: The legacy two-pass aggregate `BehaviorSystem` architecture had fundamental
  issues even after it adopted bounded Reynolds-style steering:
  - Debugging was nearly impossible (two-pass architecture, opaque aggregates)
  - Earlier `Damped`-mode force/friction equilibrium created unpredictable parameter sensitivity
  - Early inverse-square separation was replaced by the current bounded linear radial falloff;
    separation still participates in the opaque aggregate/force pipeline
  - Aggregate-coupled rule calculations made standard tuning guidance and single-rule diagnosis hard
- **New approach**: Complete rewrite in `SwarmSim.Core.Canonical` namespace following Reynolds' canonical steering behaviors:
  - **Immutable data**: `readonly struct Boid`, functional transformations
  - **Independent steering rules**: rules return `desired - current`; the caller clamps and arbitrates
    contributions against one shared per-tick `MaxForce` budget (#19), with the remaining
    composition defects tracked in #27
  - **Direct speed control**: velocity is normalized without friction to `TargetSpeed` or the
    priority-adjusted allowed speed (up to 3% lower at the current default)
  - **Single-pass**: All decision-making in one place per agent
  - **Rule interface**: `IRule` implementations exist, but composition is positional and results
    after slot 2 are discarded; #27 owns a real named composition surface
  - **FOV weighting**: Neighbors weighted by position in vision cone
  - **Prioritized separation**: priority enters at 20% of sense radius by default and boosts
    separation/reduces allowed speed; independently, a separation vector whose squared magnitude
    exceeds the current `1e-6` cutoff spends from the shared per-tick budget and then exhausts
    whatever remains (#27)
  - **World perception snapshot**: new `PerceptionSnapshot` carries avg/min/max neighbor distances plus rule magnitudes so you can reason about the scene without rendering
  - **Rich instrumentation**: Per-agent neighbor counts, weights, rule contributions
- **Implemented components**: core scaffolding, steering-rule classes, and Phase C smoothing
  - Core infrastructure (Vec2, Boid, CanonicalWorld, RuleContext)
  - Separation with 1/d weighting and linear falloff; alignment averages velocity and cohesion
    targets the weighted center of neighbors
  - Spatial indexing (NaiveSpatialIndex, GridSpatialIndex)
  - FOV filtering with linear weight falloff
  - **Smoothing System** (Phase A-C from `docs/archive/CanonicalBoids_SmoothingPlan.md`):
    - Angular rate limiter (MaxTurnRateDegPerSecond) - prevents instant snaps
    - Priority hysteresis (enter/exit/hold thresholds) - prevents ping-pong
    - Shaped separation (lateral+away blending with smoothstep) - smooth collision avoidance
    - Gradual avoidance falloff - steering sharpness increases with proximity
    - Smooth wander angle changes while force budget remains; qualifying separation pauses the
      angle update as well as its contribution (#27)
    - Alignment/cohesion attenuation is calculated during priority, but qualifying separation
      spends from and then exhausts the remaining budget before those contributions are applied
      (#27)
    - Whisker lookahead visualization (blue circle in overlay)
  - Enhanced PerceptionSnapshot with per-agent nearest angles and whisker counts
  - 12 unit tests passing (including new angular limiter and hysteresis tests)
- **Milestone 2 complete**: canonical grid and naive queries enforce radius/self-exclusion and
  minimum-image deltas are used through the perception and built-in rule paths, with deterministic
  equivalence, bounded-result, trajectory, and steady-state allocation evidence.
- **Milestone 6 partial**: the composition path exists and its total `MaxForce` budget is now
  enforced and test-guarded (#19 - whisker plus separation share one per-tick remainder; measured
  worst-case ratio 1.0000 across 60,000 dense agent-ticks, down from exactly 2.0000); positional
  slots, discarded later rules, and separation starvation of alignment, cohesion, and wander remain
  tracked in #27
- **Milestone 7 partial**: backend instrumentation and a basic selected-boid inspection overlay
  exist; an FOV arc, rule-colored links, a steering-vector arrow, rule/FOV controls, and
  rule-toggle acceptance tests remain open in #40
- **Milestones 3-6 acceptance partial**: direct rule tests exist, but the roadmap's multi-tick
  behavioral/single-rule/stability scenarios remain open in #41
- **In Progress**: Milestones 8-10
  - Boundary testing (wrapping, reflection)
  - Spatial index equivalence tests
  - Property tests at scale (polarization, clustering)
  - Multi-group support
  - Visualization tools
- **Not Started**: Full migration, performance validation, Phase 3 features
- **How to run**:
  - Tests: `dotnet test --filter CanonicalBoidsTests`
  - Renderer: `dotnet run --project SwarmSim.Render -- --canonical` (single-group)
  - Legacy: `dotnet run --project SwarmSim.Render` (multi-group, deprecated)
- **Before Phase 3**: #17, #18 and #19 are closed — seeded reproducibility, the perception/
  spatial-index contract, and the per-tick force budget are all enforced and test-guarded. What
  remains is milestone 7's UX/test acceptance (#40), milestones 8-10, multi-group support, the
  prescribed milestone 3-6 scenarios (#41), and performance validation

---

### PRIORITY: Architecture Improvements (Post-P2, Pre-P3) 🔥
**Goal**: Fix boids implementation to be canonical, decouple simulation from rendering

These improvements address fundamental architecture issues discovered during Phase 2 debugging and should be completed before continuing to Phase 3. They will provide a solid foundation for all future features.

#### Part A: Canonical Boids Implementation (PRIORITY 1) - ✅ COMPLETE
**Historical rationale (before the completed refactor)**: The implementation used raw forces,
which caused parameter-tuning issues. `BehaviorSystem` now computes bounded `desired - current`
steering; the separate canonical path further separates rules.

- [x] **Refactor to Steering Behaviors** (SwarmSim.Core/Systems/)
  - [x] Changed BehaviorSystem to compute desired velocities (not raw forces)
  - [x] Implemented steering: `steer = clamp(desired - current, maxForce)`
  - [x] Separation already uses bounded linear radial falloff (corrected during Phase 2)
  - [x] Added MaxForce parameter to SimConfig (default 5.0)
  - [x] Semi-implicit Euler: `v += steer*dt; x += v*dt` in IntegrateSystem

- [x] **Speed Model Choice**
  - [x] Added SpeedModel enum to SimConfig (ConstantSpeed vs Damped)
  - [x] ConstantSpeed: friction=1.0, agents maintain momentum (default)
  - [x] Damped: friction < 1.0, equilibrium speeds based on forces
  - [x] IntegrateSystem respects SpeedModel flag

- [x] **Perception Improvements**
  - [x] Added FieldOfView parameter to SimConfig (default 270°)
  - [x] Filter neighbors by angular visibility in SenseSystem
  - [x] Zero-velocity agents treated as omnidirectional (can see all directions)
  - [x] Added MathUtils.IsWithinFieldOfView() helper using dot product

- [x] **Configuration**
  - [x] Added WanderStrength parameter (default 0 = disabled)
  - [x] Updated PeacefulFlocks preset with new parameters
  - [x] Updated validation to check new parameters

- [x] **Tests**
  - [x] 45/47 tests passing (95.7% pass rate)
  - [x] All boids behavior tests pass (separation, alignment, cohesion)
  - [x] Determinism tests pass (with appropriate floating-point tolerance)
  - [x] Updated tests to use SpeedModel.Damped when testing friction
  - [x] 2 performance tests fail (expected - steering does more work, will optimize in Phase 5)

**References**: `docs/archive/MakingBoidsBetter.md`, Reynolds' steering behaviors paper

**Status**: ✅ **PART A COMPLETE** - Canonical steering behaviors implemented and tested. Performance optimization deferred to Phase 5.

**Exit Criteria**: ✅ **ALL MET** - Boids use steering behaviors, parameters easy to tune, no force/friction pathologies

---

#### Part B: Fixed Timestep & Decoupling (PRIORITY 2) - ✅ COMPLETE
**Rationale**: Current implementation couples simulation rate to render rate. Fixed timestep ensures determinism and stability.

- [x] **Fixed Timestep Loop** (SwarmSim.Core/)
  - [x] Add accumulator-based runner (`SimulationRunner`) that steps worlds off the render loop
  - [x] Build-in spiral-of-death guard + accumulator carry-over (maxStepsPerAdvance=8)
  - [x] Wire renderer/input loop to runner to make framerate-independent
  - [ ] Add clock utilities for headless determinism (deferred - not needed for current use cases)

- [x] **Snapshot Architecture** (SwarmSim.Core/)
  - [x] Create `SimSnapshot` struct with read-only SoA data
  - [x] Provide helpers for runner (`CaptureSnapshot`, per-tick callback)
  - [ ] Thread-safe channel + renderer consumption (deferred - single-threaded approach works well)

- [x] **Interpolation Support** (SwarmSim.Render/)
  - [x] Store previous and current snapshots (_prevSnapshot, _currSnapshot fields)
  - [x] Calculate alpha = accumulator / dt
  - [x] Render with `lerp(prev, curr, alpha)` for smooth motion
  - [x] Implemented linear interpolation for positions and velocities

- [ ] **Optional: Threading** (Future optimization - Phase 5)
  - [ ] Create bounded channel for snapshots (capacity 2-3)
  - [ ] Simulation thread publishes snapshots
  - [ ] Render thread consumes latest snapshots
  - [ ] Non-blocking, frame dropping when renderer is slow

**References**: `docs/archive/DecouplingPlan.md`, Gaffer on Games "Fix Your Timestep"

**Status**: ✅ **PART B COMPLETE** - Fixed timestep with interpolation fully integrated. Simulation now runs independently of render rate.

**Exit Criteria**: ✅ **ALL MET** - Simulation runs at fixed dt (configurable), rendering interpolates smoothly, deterministic across framerates

---

### Active TODO – Snapshot & Runner Hardening
Recent testing surfaced blinking agents and `IndexOutOfRangeException` crashes when snapshot sizes diverge. Current status:

1. **Runner Mutation Hooks** ✅ (2025‑11‑12)
   - Added `SimulationRunner.ResetAccumulator()` / `NotifyWorldMutated()` plus capture & mutation version tracking.
   - All spawn/reset/preset paths now route through `ForceSnapshotRefresh`, which triggers these hooks automatically.

2. **Snapshot Contracts & Versioning** ✅
   - `SimSnapshot` carries `CaptureVersion`, `MutationVersion`, and debug-only consistency guards; interpolation is skipped when versions differ.

3. **Renderer Safety & Debug HUD** ✅
   - Draw loops clamp to actual array lengths, neighbor overlays guard `_world` indices, and a togglable `F12` overlay surfaces prev/curr counts, versions, alpha, and accumulator metrics.

4. **Automated Regression Tests** ⏳
   - `SimulationRunnerTests` now cover version monotonicity and mutation resets. Still need renderer-focused tests that feed mismatched synthetic snapshots into the interpolation routine to guarantee it never throws.

5. **Logging & Telemetry** ⏳
   - Structured `[Snapshots]` logs describe refresh reasons and large deltas; extend this with an optional snapshot dump (CSV/JSON) for deep dives.

Finishing the remaining tests/logging work will make the snapshot pipeline fully resilient and turn the new debugging workflow into standard practice.

---

### Active TODO – Developer Experience & Documentation Improvements
The current project lacks clear onboarding and runtime discoverability. Developers need better visibility into:

1. **Runtime Help System** ✅
   - [x] Add `--help` flag to SwarmSim.Render showing all command-line options
   - [x] Add in-app help overlay (H key); current text is partial/stale and tracked in issue #39
   - [x] Add configuration file examples for common use cases (`configs/` folder)
   - [x] Document all keyboard shortcuts with grouping (`docs/CONTROLS.md` is the complete reference)

2. **Command-Line Interface** ✅
   - [x] Add command-line preset selection; stale help examples are tracked in issue #38
   - [x] Add `--config <path>` to load custom configuration from JSON file
   - [x] Add `--agent-count <n>` to override initial agent count
   - [x] Add `--benchmark` mode for headless performance testing
   - [x] Add `--version` and `--list-presets` commands

3. **Documentation Consolidation** ⏳
   - [x] Update README.md with current capabilities and feature status (CLI, help overlay, configs)
   - [x] Consolidate `docs/QUICKSTART.md` with practical examples (running presets, tweaking parameters)
   - [x] Archive outdated documentation (`docs/archive/DecouplingPlan.md`, `docs/archive/MakingBoidsBetter.md`)
   - [x] Create `docs/CONTROLS.md` reference guide with all keyboard/mouse interactions
   - [x] Update CLAUDE.md with recent architectural decisions (snapshots, versioning, CLI)

4. **Configuration Discovery** ⏳
   - [x] Create `configs/` directory with example JSON files for each preset
   - [x] Document SimConfig parameter ranges and their effects (`docs/CONFIGURATION_COOKBOOK.md`)
   - [x] Add validation messages that explain why a config is invalid (warnings on load)
   - [x] Create "Configuration Cookbook" with recipes for common scenarios

5. **Testing Improvements** ✅
   - [x] Historical warning-only timing tests (superseded on 2026-07-25 by machine-relative
     `Performance`-category assertions)
   - [x] Document that Phase 5 performance targets are deferred until optimization phase
   - [x] Add integration tests for command-line argument parsing
   - [x] Add tests for configuration loading from JSON

**Goal**: Any developer (or future Claude instance) should be able to:
- Discover all program capabilities without reading source code
- Run the simulation with different configurations via command line
- Understand what each parameter does and how to tune it
- Access comprehensive help both in-app and via documentation

**Priority**: Complete before Phase 3 to establish good DX patterns early.

---

### Phase 3: Groups, Combat, Energy (P3)
**Goal**: Multiple groups, aggression matrix, combat interactions, metabolism

- [ ] **Combat & Metabolism** (SwarmSim.Core/Systems/)
  - [ ] `CombatSystem.cs` - Resolve attacks within radius
  - [ ] `MetabolismSystem.cs` - Energy drain, age increment, mark deaths
  - [ ] `LifecycleSystem.cs` - Compact dead slots, free-list management

- [ ] **Group Aggression**
  - [ ] Aggression matrix in SimConfig
  - [ ] Modify BehaviorSystem to use aggression values

- [ ] **Tests**
  - [ ] Combat scenarios: verify energy transfer
  - [ ] Death and cleanup tests

**Exit Criteria**: Two groups fight/flee based on aggression matrix, deaths occur, population stable

---

### Phase 4: Reproduction & Evolution (P4)
**Goal**: Agents reproduce, genomes mutate, observe trait drift

- [ ] **Reproduction** (SwarmSim.Core/Systems/)
  - [ ] `ReproductionSystem.cs` - Energy threshold → spawn child with mutated genome
  - [ ] Genome mutation logic with clamped normal noise

- [ ] **Foraging**
  - [ ] `ForageSystem.cs` - Simple uniform food field or point sources
  - [ ] Add energy to agents

- [ ] **Metrics & Events**
  - [ ] Event DTOs (Birth, Death, Mutation)
  - [ ] Metrics: births/deaths per tick, avg traits

**Exit Criteria**: Population self-sustaining, trait histograms change over time, 2+ evolutionary regimes

---

### Phase 5: Performance & Parallel (P5)
**Goal**: Optimize for 200k+ interactive or 1M+ headless

- [ ] **SIMD**
  - [ ] Replace scalar math with System.Numerics.Vector2
  - [ ] Benchmark improvements

- [ ] **Parallelization**
  - [ ] Row/tile partitioning with Parallel.For
  - [ ] Private worker accumulators
  - [ ] Measure speedup vs single-threaded

- [ ] **NativeAOT**
  - [ ] Publish profiles (see `docs/archive/PublishScript.md`)
  - [ ] Test startup time and memory

**Exit Criteria**: 200k+ agents interactive OR 1M+ headless, documented perf baselines

---

### Phase 6: Polish & Tooling (P6)
**Goal**: Presets, replay, snapshots, better UX

- [ ] **Presets**
  - [ ] Peaceful Flocks, Warbands, Rapid Evolution configs
  - [ ] Load from JSON files

- [ ] **Observability**
  - [ ] Snapshot export (CSV/binary) every N ticks
  - [ ] HUD with detailed metrics

- [ ] **Replay**
  - [ ] Record inputs for deterministic replay
  - [ ] Replay viewer

**Exit Criteria**: Presets work, can export data for Python analysis, replay system functional

---

## Implementation Guidelines for Future Claude

### Before Starting Work
1. **Read this file completely** - Understand current phase and status
2. **Check CLAUDE.md** - Refresh on build commands and architecture
3. **Review roadmap vision** - Reference `docs/ROADMAP-VISION.md` as aspirational context; use epic #10 for execution
4. **Use TodoWrite** - Create task list for your session
5. **Build and test** - Ensure solution builds before making changes

### While Working
1. **Follow SoA principles** - No allocations in hot paths, use arrays not lists
2. **Determinism first** - Stable iteration order, seeded RNG, no time-based randomness
3. **Test as you go** - Write tests for data structures and systems
4. **Document invariants** - Comment at top of each system class
5. **Profile regularly** - Check allocations with dotMemory, CPU with dotTrace
6. **Update todos** - Mark tasks complete as you finish them

### After Completing Work
1. **Verify build** - `dotnet build` and `dotnet test` must pass
2. **Update this file** - Check off completed items, note any blockers
3. **Update phase status** - If phase complete, update "Current Phase" at top
4. **Commit frequently** - Small, atomic commits with clear messages
5. **Note for next session** - Add any "TODO" or "FIXME" comments for future work

### Performance Rules (CRITICAL)
- **Zero allocations** in Tick() - Use stackalloc or ArrayPool for temp buffers
- **No LINQ** in hot paths - Use for loops
- **No boxing** - Avoid object, dynamic, delegates in systems
- **No exceptions** for control flow - Use return codes
- **Hoist invariants** - Move calculations outside loops when possible
- **Prefer structs** - Especially for small DTOs like Genome
- **readonly ref** - Pass large structs by ref when reading

### Common Pitfalls to Avoid
- ❌ Don't use List<T> for agent data - Use arrays
- ❌ Don't allocate in Tick() - Pre-allocate everything
- ❌ Don't use virtual/interface calls in inner loops - Direct/static only
- ❌ Don't iterate agents with foreach - Use for loop with index
- ❌ Don't modify collection while iterating - Use two-pass (mark, then act)
- ❌ Don't forget to update Count when adding/removing agents
- ❌ Don't use Time.Now or Random() - Use injected Rng with seed

---

## Current Blockers & Questions

1. **Canonical readiness**: seeded reproducibility (#17), the perception/spatial-index contract
   (#18), and the per-tick force budget (#19) are all closed, enforced and test-guarded. Milestone
   7 UX/test acceptance (#40) and milestones 8-10 remain incomplete. Prescribed milestone 3-6
   scenarios remain unverified (#41), while boundary/reflection coverage and scale
   properties/metrics are also open.
2. **Feature parity**: canonical multi-group semantics and aggression support are not complete, so
   Phase 3 combat/metabolism work has no settled target model.
3. **Performance evidence**: the legacy 50k simulation tick took 162.815 ms in the 2026-07-25
   sample, well above a 16.67 ms single-step budget; renderer FPS, canonical throughput, and
   allocations remain unmeasured.
4. **Test-gate honesty**: timing facts now assert generous machine-relative scaling envelopes
   outside the default hosted CI gate; absolute throughput remains reported-only evidence.
5. **Maintainability**: renderer and canonical-world monoliths increase change and review cost;
   legacy/canonical duplication creates ongoing drift risk.
6. **Documentation drift**: high-traffic entrypoints are reconciled. The explicitly historical
   sections below intentionally preserve obsolete phase, performance, and next-session claims; the
   historical-context banner above them is the liveness boundary.
7. **Untrusted-extension prerequisite**: no untrusted extension code may run until
   [issue #44](https://github.com/Chris0Jeky/SwarmingLilMen/issues/44) delivers a separate process,
   least-authority filesystem/network access, resource and wall-clock limits, bounded validated
   schemas, authenticated local-only transport, a dedicated threat model, and adversarial
   fail-closed evidence. This marker is unscheduled and has no owner or timeline.

---

## Recent Changes Log

### 2025-11-12 (Session 3.3 - Canonical Smoothing System COMPLETE ✅)
- **Implemented Complete Smoothing System** (Phase A-C from CanonicalBoids_SmoothingPlan.md):
  - **Smooth Wander**: Replaced per-tick direction reseeding with a persistent, budget-gated angle
    - Added `WanderRate` parameter (1.5 rad/s) for eligible angle changes
    - Each agent maintains persistent `_wanderAngles[]` that evolve smoothly over time
    - Creates natural, flowing movement instead of jittery random directions
  - **Gradual Avoidance Falloff**: Replaced sharp threshold activation with smooth ramp
    - Avoidance influence starts at `SeparationRadius * 2.0` (gentle steering)
    - Increases quadratically as agents approach `separationEnterThreshold`
    - Uses distance-based influence: `(rGradualStart - dist) / (rGradualStart - rHard)`
    - Combined with priority blend for emergency response at close range
  - **Shaped Separation Blending**: Lateral + away components with smoothstep transition
    - At medium distance (rSoft): 100% lateral "shoulder past" deflection
    - At close range (rHard): 100% away direct repulsion
    - Smoothstep blend between the two zones prevents sharp transitions
    - Influence capped at 70% to preserve angular rate limiter effectiveness
  - **Fixed Collision Issues**: Previous version removed snap-away but had no replacement
    - New shaped avoidance provides strong collision prevention
    - Maintains smooth turning while ensuring agents don't overlap
  - **Added Per-Agent Perception Data**: Extended PerceptionSnapshot
    - `NearestDistances[]` - distance to each agent's nearest neighbor
    - `NearestAngles[]` - angle to nearest neighbor (degrees, relative to forward)
    - `WhiskerCounts[]` - neighbors detected in whisker lookahead capsule
  - **Enhanced Tests**: 12 canonical boids tests passing
    - Angular rate limiter verification
    - Priority hysteresis enter/exit logic
    - Whisker capsule detection
    - Per-agent perception snapshot data
- **Identified Blue Circle**: The blue circle in overlay is the **whisker lookahead capsule**
  - Shows predictive collision detection zone ahead of tracked boid
  - Radius = `SeparationRadius`, lookahead = `TargetSpeed * WhiskerTimeHorizon`
- Solution builds with 0 warnings, 0 errors
- **✅ CANONICAL SMOOTHING COMPLETE** - Agents now have smooth, natural movement with robust collision avoidance

### 2025-11-12 (Session 3.2 - Part B: Fixed Timestep COMPLETE ✅)
- **Fixed SimulationRunner Tests**:
  - Fixed compilation error: `SimConfig.FixedDeltaTime` is init-only, can't modify after construction
  - Fixed floating-point precision issues by using power-of-2 fractions (0.125, 0.0625) for exact binary representation
  - All SimulationRunner tests now passing (3/3)
- **Integrated Fixed Timestep with Raylib Renderer**:
  - Modified `Program.cs` main loop to use `SimulationRunner.Advance(frameTime)` instead of direct `World.Tick()`
  - Added `_runner`, `_prevSnapshot`, `_currSnapshot` fields for state management
  - Created `RenderInterpolated()` method with alpha calculation: `alpha = accumulator / dt`
  - Created `DrawAgentsInterpolated()` method with linear interpolation for smooth rendering
  - Added `Lerp()` helper for position and velocity interpolation
  - Updated `RecreateWorldWithNewParams()` to recreate runner and reset snapshots
  - Fixed compilation error in `DrawNeighborConnections()` (missing color parameter)
- **Test Results**:
  - **49/51 tests passing (96.1%)**
  - All boids behavior tests pass
  - All SimulationRunner tests pass
  - 2 performance tests fail (expected - steering behaviors do more work, will optimize in Phase 5)
- **Architecture Achievement**: Simulation now runs at fixed timestep, completely decoupled from render rate
  - Deterministic physics regardless of frame rate
  - Smooth rendering via interpolation between snapshots
  - Spiral-of-death protection with maxStepsPerAdvance guard
- Solution builds with 0 warnings, 0 errors
- **✅ PART B COMPLETE** - Ready for Phase 3 (Combat & Metabolism)

### 2025-11-12 (Session 3.1 - Simulation Harness & Tests)
- Introduced `SimulationRunner` (fixed-step accumulator with spiral-of-death guard) to decouple the simulation rate from rendering/event loops.
- Added immutable `SimSnapshot` DTO and helper APIs so renderers/tests can read consistent SoA data without racing the World arrays.
- Created `SimulationRunnerTests` to validate accumulator math, snapshot immutability, and safety caps—laying the groundwork for CI coverage of timestep bugs.
- Updated PROJECT_STATUS Part B checklist to reflect the new infrastructure; next step is to wire the Raylib front-end to the runner and add interpolation.

### 2025-11-13 (Session 4 - Canonical Boids & UX Polish)
- Refined boids steering to match classic Reynolds behaviour: limited neighbor samples, explicit collision-avoidance radius/boost, and prioritized steering budgets so separation always fires first.
- Added new `SimConfig` knobs (`MaxNeighbors`, `CollisionAvoidanceRadius`, `CollisionAvoidanceBoost`) plus crowding-aware separation defaults; updated all presets/config JSON to use these values.
- Enhanced the renderer overlays (H/F12) to display vision radius, FOV, max neighbors, and documented every parameter in `docs/PARAMETER_GUIDE.md`.

### 2025-11-11 (Session 3 - Part A: Canonical Boids COMPLETE ✅)
- **Implemented Canonical Steering Behaviors** (Reynolds model):
  - Refactored BehaviorSystem to compute desired velocities, then `steer = clamp(desired - current, maxForce)`
  - Each force (separation, alignment, cohesion) now calculates a desired velocity at `maxSpeed * weight`
  - Steering forces are clamped to `MaxForce` for predictable behavior
  - No more force/friction equilibrium pathologies!
- **Added New SimConfig Parameters**:
  - `MaxForce` (default 5.0) - limits steering force magnitude
  - `SpeedModel` enum (ConstantSpeed vs Damped) - control friction behavior
  - `FieldOfView` (default 270°) - perception cone for neighbor filtering
  - `WanderStrength` (default 0) - optional random exploration forces
- **Updated IntegrateSystem**:
  - Semi-implicit Euler integration (v first, then x)
  - Respects SpeedModel: ConstantSpeed skips friction, Damped applies it
  - Better stability and predictability
- **Enhanced Perception**:
  - FOV filtering in SenseSystem using dot product check
  - Zero-velocity agents treated as omnidirectional (can see all directions)
  - Added `MathUtils.IsWithinFieldOfView()` helper
- **Test Results**:
  - **45/47 tests passing (95.7%)**
  - All boids behavior tests pass
  - Determinism tests pass (updated for floating-point tolerance)
  - 2 performance tests fail (steering does ~40% more work, expected, will optimize in Phase 5)
- **Documentation**: Created `docs/SIMULATION_MECHANICS_EXPLAINED.md` with detailed parameter guide
- Solution builds with 0 warnings, 0 errors
- **✅ PART A COMPLETE** - Ready for Part B (Fixed Timestep) or Phase 3 (Combat)

### 2025-11-11 (Session 3 - Architecture Improvements Added)
- **Added High-Priority Architecture Improvements** (Post-P2, Pre-P3):
  - Part A: Canonical Boids Implementation (steering behaviors, 1/d separation, FOV)
  - Part B: Fixed Timestep & Decoupling (accumulator loop, snapshots, interpolation)
- **Rationale**: Issues discovered during P2 debugging revealed fundamental architecture gaps
  - The pre-refactor raw-force/damped approach caused parameter-tuning pathologies
  - Simulation coupled to render rate (not deterministic across framerates)
  - Industry-standard approaches (Reynolds steering, Gaffer fixed timestep) will provide solid foundation
- **References**: Added `docs/archive/DecouplingPlan.md` and `docs/archive/MakingBoidsBetter.md` as design documents
- **Next Steps**: Implement Part A (Canonical Boids) first, then Part B (Fixed Timestep)
- Updated PROJECT_STATUS.md to reflect new priority tasks

### 2025-11-10 (Session 2 - Phase 2 COMPLETE ✅)
- **Implemented Phase 2: Boids & Flocking**:
  - Created SenseSystem: neighbor queries, computes separation/alignment/cohesion aggregates
  - Created BehaviorSystem: applies boids rules (separation 1/r², alignment avg velocity, cohesion center of mass)
  - Replaced RandomWalkSystem with boids behavior
  - Updated World.cs systems pipeline: Sense → Behavior → Integrate
- **Tests & Verification**:
  - 8 new boids behavior tests (separation, alignment, cohesion, groups, determinism)
  - **43 tests total, all passing** (35 from P0/P1 + 8 new)
  - Verified individual boids rules work correctly
- **Performance Results** (Release mode):
  - 10k agents: 129 FPS ✅ (exceeds 60 FPS target)
  - 1k agents: 8,555 FPS ✅
  - 50k agents: 6.5 FPS (optimization opportunity for Phase 5)
- **Emergent Behavior**: Visible flocking confirmed in tests
- Solution builds with 0 warnings, 0 errors
- **✅ PHASE 2 COMPLETE** - Ready for Phase 3 (Combat & Metabolism)

### 2025-11-10 (Session 2 - Phase 1 COMPLETE ✅)
- **Implemented Phase 1: Spatial Grid & Basic Movement**:
  - Created ISimSystem interface for all simulation systems
  - Implemented UniformGrid with Head[]/Next[] linked list structure
  - Created IntegrateSystem (velocity → position, boundary conditions)
  - Created RandomWalkSystem (temporary, for testing movement)
  - Updated World.cs to use systems pipeline
- **Tests & Benchmarks**:
  - 14 new UniformGrid tests (brute force comparison, boundary cases, stats)
  - 4 performance tests (1k, 10k, 50k agents)
  - WorldTickBenchmarks and GridBenchmarks (BenchmarkDotNet)
  - **39 tests total, all passing** (21 from P0 + 18 new)
- **Performance Results** (Release mode, exceeds all targets):
  - 50k agents: 1.92ms/tick (**521 FPS**) - 8.7x better than 60 FPS goal!
  - 10k agents: 0.38ms/tick (2,612 FPS)
  - 1k agents: 0.069ms/tick (14,522 FPS)
  - Grid rebuild: 0.093ms (50k agents)
- Solution builds with 0 warnings, 0 errors
- **✅ PHASE 1 COMPLETE** - Ready for Phase 2 (Boids)

### 2025-11-10 (Session 1 - FOUNDATION COMPLETE ✅)
- Created PROJECT_STATUS.md as persistent memory document
- Solution reorganized: removed root project, added Directory.Build.props
- **Implemented complete P0 foundation**:
  - Genome.cs with Random() and Mutate() methods
  - AgentState.cs enum with HasFlag/SetFlag/ClearFlag extensions
  - SimConfig.cs with validation and 3 presets
  - Rng.cs with Gaussian, unit vectors, circle sampling, shuffle
  - MathUtils.cs with full 2D vector operations
  - World.cs with 13 SoA arrays, agent lifecycle, boundary modes
- **Created a passing 21-fact suite** verifying determinism and core functionality
- **Implemented Raylib rendering**:
  - Full visualization with 1920x1080 window
  - 16-color palette for agent groups
  - Interactive controls (mouse spawn, keyboard, reset)
  - Live stats HUD (FPS, agent count, energy, speed)
  - Starts with 1000 agents in 4 colored groups
- **Created comprehensive documentation**:
  - README.md - Project overview, quick start, features, usage examples
  - CONTRIBUTING.md - Developer setup, IDE configuration, workflow, performance guidelines
  - `docs/QUICKSTART.md` - 5-minute getting started guide
  - PublishScript.md analysis and explanation
- Solution builds with 0 warnings, 0 errors
- **✅ FOUNDATION COMPLETE** - Ready to move to Phase 1

---

## Performance Baselines

| Phase | Agents | Tick Time | FPS | Allocs/Tick | Notes |
|-------|--------|-----------|-----|-------------|-------|
| P0    | 1k     | 0.069ms   | 14,522 | N/A | Baseline (no systems) |
| P1    | 1k     | 0.069ms   | 14,522 | TBD | Grid + RandomWalk + Integrate ✅ |
| P1    | 10k    | 0.38ms    | 2,612  | TBD | Grid + RandomWalk + Integrate ✅ |
| P1    | 50k    | 1.92ms    | 521    | TBD | Grid + RandomWalk + Integrate ✅ |
| **P2**| **1k** | **0.117ms** | **8,555** | **TBD** | **Grid + Boids + Integrate** ✅ |
| **P2**| **10k** | **7.75ms** | **129** | **TBD** | **Grid + Boids + Integrate** ✅ |
| **P2**| **50k** | **154ms** | **6.5** | **TBD** | **Grid + Boids + Integrate** ⚠️ |
| P3    | 50k    | TBD       | 60+ (target) | 0 | With combat/metabolism |
| P4    | 50k    | TBD       | 60+ (target) | 0 | With reproduction |
| P5    | 200k+  | TBD       | 60+ (target) | 0 | With SIMD/parallel |

**Notes**:
- All results measured in Release mode on development machine
- Grid rebuild time (50k agents): 0.093ms
- P1 (RandomWalk): 521 FPS @ 50k agents - far exceeds target
- P2 (Boids): 129 FPS @ 10k agents - exceeds target; 6.5 FPS @ 50k needs optimization
- Boids calculations are O(n*k) where k = avg neighbors, explaining performance drop at high density
- Optimization opportunities for P5: SIMD, parallelization, neighbor limits

---

## External Resources

### Documentation
- Roadmap vision: `docs/ROADMAP-VISION.md`
- Historical master plan: `docs/archive/swarming_lil_men_master_plan_v_1.md`
- Historical publish scripts: `docs/archive/PublishScript.md`
- Claude Guidelines: `CLAUDE.md`

### Dependencies
- Raylib-cs 7.0.2 (SwarmSim.Render)
- BenchmarkDotNet 0.15.6 (SwarmSim.Benchmarks)
- xUnit 2.9.3 (SwarmSim.Tests)

### Useful Commands
```bash
# Build
dotnet build

# Test
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj

# Run renderer
dotnet run --project SwarmSim.Render/SwarmSim.Render.csproj

# Benchmark (always Release)
dotnet run --project SwarmSim.Benchmarks/SwarmSim.Benchmarks.csproj -c Release

# Profile in Rider
# Use CPU Profile / Memory Profile run configurations
```

---

## Notes for Next Session

**Where We Left Off**: Phases 0-2 and fixed-timestep/smoothing work exist; the project is paused at
canonical readiness before Phase 3. The repository has been dormant since November 2025 and was
audited on 2026-07-25.

**Next Steps**:
1. Add canonical boundary and grid-vs-naive equivalence tests (milestones 8-9).
2. Define and test canonical multi-group semantics before adding combat/metabolism.
3. Add canonical BenchmarkDotNet cases and a real allocation measurement; decide whether timing
   thresholds should gate CI or remain explicitly observational.
4. Keep pull-request CI current with the latest `main`; use the manual workflow trigger when exact
   branch-head evidence is required. Performance remains separate and observational.
5. Consolidate README and historical status claims against the verified-state section.

**Remember**: Do not start Phase 3 on the legacy path by default, and do not claim the headline
performance target from a passing timing test alone.
