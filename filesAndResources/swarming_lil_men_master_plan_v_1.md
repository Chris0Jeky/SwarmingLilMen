# SwarmingLilMen – Master Plan (v1.0)

*A 2D, bacteria‑like emergent world built from first principles in C#/.NET with a data‑oriented core.*

---

## 1) Vision & North Star
**Goal:** Create a fast, from‑scratch 2D world where thousands to hundreds of thousands of agents (“lil men”) move, swarm, fight, forage, reproduce, evolve, and self‑organize. Priorities:
- **Emergence over scripts**: few simple, composable rules → rich macro patterns.
- **Performance**: 50k–100k interactive @ 60 FPS on a mid PC; 1M+ headless.
- **Determinism**: fixed‑timestep; reproducible with seeded RNG; record/replay.
- **Observability**: metrics, snapshots, profiling, property tests, benchmarks.
- **Extensibility**: small public API, SoA internals, systems modularity.

**Non‑Goals (MVP):** fancy art/audio, network multiplayer, complicated UIs.

---

## 2) Product Scope (initial + optional)
- **Must‑have (MVP):**
  - Continuous 2D world with wrap or reflect borders.
  - Agents with velocity, energy, group tag, simple genome, and states.
  - Spatial partitioning (uniform grid / spatial hash) for fast neighbours.
  - Boids core (separation, alignment, cohesion) + group aggression matrix.
  - Metabolism (drain), foraging (simple food field), reproduction (split), death.
  - Fixed‑timestep loop, raylib‑cs rendering, live parameter tweaking.
  - Metrics HUD + event log; zero‑alloc hot loop.
- **Nice‑to‑have (P1/P2):**
  - Obstacles; diffusion fields (food/scent) with simple PDE/discrete blur.
  - Tile/stripe parallelism; SIMD math; headless runner + AOT publish.
  - Replay (deterministic inputs); snapshot export for Python analysis.
  - Basic scenario presets (Peaceful Flocks, Warbands, Rapid Evolution).

---

## 3) Architecture (high level)
```
SwarmSim.sln
  Sim.Core        // data + systems, deterministic, allocation‑free tick
  Sim.Render      // raylib‑cs window, input, overlay, draws from spans
  Sim.Tests       // xUnit property/unit tests (grid, invariants, rules)
  Sim.Benchmarks  // BenchmarkDotNet microbenches for hot loops
```

**Core design principles**
- **SoA data layout** for cache/SIMD: `X[], Y[], Vx[], Vy[], Energy[], Group[], ...`.
- **Systems pipeline (ECS‑ish)**: stateless `ISimSystem.Run(w, dt)` composed in order.
- **Read current, write scratch** (forces/decisions), then integrate in a separate pass.
- **Spatial Hash/Grid**: `Head[cell]` + `Next[agent]` linked lists, rebuild per tick; cell size ≈ interaction radius.
- **Narrow Facade**: `World` exposes `Tick`, `AddAgent`, `Configure`, `GetReadonlySpans` and DTOs.
- **Event/Metrics Bus**: non‑blocking `Channel<T>` consumers (logger, snapshotter).

---

## 4) Data Model (SoA) & Memory Strategy
**Per‑agent arrays** (length = Capacity)
- `float[] X, Y, Vx, Vy, Energy, Health, Age`
- `byte[] Group, State` (bitflags: Fleeing, Hunting, Reproducing, etc.)
- `Genome[]` (packed traits: speedFactor, senseFactor, aggression, colorIdx) – `readonly record struct`.

**Scratch/derived**
- `Fx[], Fy[]` (forces); optional `NextVx[], NextVy[]` if double‑buffering velocities.
- Avoid `List<T>`/LINQ in hot paths; use `ArrayPool<T>` for temporary buffers.

**Capacity & lifecycle**
- Fixed `Capacity`; `Count` active.
- Deaths mark slots into a **free‑list**; compaction runs every N ticks or on demand.

**RNG & determinism**
- `Rng` wrapper with explicit `uint` seed; no time‑based randomness in core.
- Iteration order is stable; event bus consumers are decoupled from tick order.

---

## 5) Systems & Update Order
> Stateless system structs/classes; no allocations inside `Run`.
1. **SenseSystem** – build per‑agent local aggregates via grid scan (ally count, enemy count, centroids, avg vel).
2. **BehaviorSystem** – Boids + group matrix → target velocity/forces into `Fx/Fy`.
3. **CombatSystem** – within `AttackRadius` resolve hits/energy transfer; mark deaths.
4. **ForageSystem** – sample food field; add energy; optional diffusion update.
5. **ReproductionSystem** – energy ≥ threshold → spawn child with mutated genome (use free‑list).
6. **MetabolismSystem** – base drain + move cost; age increment; mark deaths.
7. **IntegrateSystem** – apply forces, clamp to `MaxSpeed`, update `V*` and `X/Y`; wrap/reflect.
8. **LifecycleSystem** – compact or recycle dead slots periodically; emit events.

**Environment**: optional fields updated at lower frequency (e.g., every 4th tick) to amortize cost.

---

## 6) Spatial Partitioning
- **Uniform Grid**: `Cols = ceil(Width/CellSize)`, `Rows = ceil(Height/CellSize)`; choose `CellSize ≈ SenseRadius`.
- Arrays: `int[] Head` (init -1 for each cell), `int[] Next` per agent.
- **RebuildGrid** each tick: O(n) push‑front into buckets.
- **Query**: for agent `i`, scan its 3×3 neighbourhood cells; skip `i`.
- **Experiments**: sweep `CellSize` vs density; test hashed variant; explore Morton‑sorted indices later.

---

## 7) Rendering & UX (Sim.Render)
- **Loop**: fixed‑timestep sim (e.g., 120 Hz); render as fast as possible; show FPS.
- **Primitives**: pixels/2×2 rects/triangles with heading; colour by group or trait.
- **Overlay**: counts, avg energy/speed, cell grid toggle, allocs (from metrics).
- **Controls**: pause/step, spawn (circle/rect region), kill, change wrap mode, adjust weights/radii with keys, cycle presets.
- **Hot‑reload config**: reload `SimConfig.json` on change (outside the tick).

---

## 8) Observability & Tooling
- **Metrics** (per tick): `Count`, `Births/Deaths`, `AvgEnergy`, `AvgSpeed`, CPU ms, neighbour stats.
- **Events**: `Birth`, `Death`, `Attack`, `Mutation`, `ConfigChanged` (DTOs only).
- **Snapshots**: every N ticks, export sample or full arrays to CSV/binary on a background `Channel` consumer.
- **Profiling**: Rider + **dotTrace** (CPU) + **dotMemory** (allocs) profiles cached as artifacts.
- **Benchmarks**: **BenchmarkDotNet** suite in Sim.Benchmarks for `RebuildGrid`, `Sense`, `Integrate`, full `Tick`.
- **Coverage**: **dotCover** runs on Sim.Tests; keep behaviour & grid tests green.

---

## 9) Testing Strategy
- **Unit**: deterministic micro‑scenarios (3–10 agents) for each rule; velocity clamp; bounds wrapping; genome mutation ranges.
- **Property tests**: grid correctness vs brute‑force neighbours; speed ≤ MaxSpeed; no NaNs/Infs.
- **Regression**: snapshot state hash after K ticks given seed; compare across runs.
- **Performance gates**: BDN baselines (fail on >X% regression); dotMemory zero‑alloc assertion in Tick.

---

## 10) Implementation Notes & Algorithms
- **Boids math**: accumulate separation (1/r^2 repel), alignment (avg vel), cohesion (avg pos − self). Use `Vector2` where helpful; branch‑reduce inner loops.
- **Group matrix**: `Aggression[g1,g2] ∈ [-1,1]` scales attraction/repulsion. Negative acts like fear; positive like hunt.
- **Metabolism**: `Energy -= BaseDrain + MoveCost*|v|`; death at ≤ 0; carcass → optional food deposit.
- **Reproduction**: split energy; child gets `genome' = mutate(genome)` with clamped normal noise.
- **Diffusion** (optional field): separable blur or Jacobi steps on a downsampled grid; update every N ticks.
- **Parallelization path**: after single‑thread is zero‑alloc and profiled, partition by **rows/tiles**; each worker uses private accumulators; integrate in a synchronized pass. Avoid shared writes; use `Interlocked` only for global counters.

---

## 11) Configuration & Presets
`SimConfig` (JSON‑serializable) includes: physics (dt, maxSpeed, friction), radii/weights, energy costs, aggression matrix, reproduction thresholds, mutation rate/std, world size, wrap mode.

**Presets**
- *Peaceful Flocks*: high cohesion/alignment; no combat; low drain.
- *Warbands*: two+ groups; positive aggression; combat on.
- *Rapid Evolution*: high mutation; reproduction cheap; observe trait drift.

---

## 12) Performance Plan & Checklists
**Hot path rules**
- No allocations; no LINQ/delegates; no boxing; avoid exceptions.
- Tight `for` loops; hoist invariant math; prefer SoA; consider `readonly ref` accessors.
- Keep neighbour `k` small by tuning `CellSize` and densities.

**Weekly perf ritual**
- Run BDN suite (Release). Record medians.
- dotTrace with 50k/100k agents; top‑3 hotspots tracked.
- dotMemory before/after 3s run → bytes allocated in Tick must be 0.

**SIMD**
- Introduce `System.Numerics.Vector2`/`Vector<T>` after scalar correctness; measure.

**AOT**
- Add Release‑AOT publish; compare startup, RAM, and FPS vs JIT and R2R; keep fastest for headless.

---

## 13) Dev Workflow & Tooling
- IDE: **Rider**. Run configs: Normal, CPU Profile, Memory Profile, Benchmarks.
- Packages: raylib‑cs, BenchmarkDotNet, xUnit (+ runner).
- Git hooks or CI: build + test + BDN + coverage; upload artifacts (profiles, reports).
- Branching: `main` (stable), short‑lived feature branches, PR with perf check.

---

## 14) Roadmap & Milestones
**P0 – Skeleton (2–3 days)**
- 4‑project layout; window; moving dots; tests pass; BDN runs; profiling configs ready.

**P1 – Boids & Grid (1 week)**
- Uniform grid; Boids systems; zero‑alloc Tick; 50k @ 60 FPS; metrics HUD.

**P2 – Groups & Interactions (1 week)**
- Group matrix; combat; deaths; births; channels for events; CSV snapshots.

**P3 – Evolution & Fields (1–2 weeks)**
- Genome, mutation, reproduction; optional diffusion food; presets & live tuning.

**P4 – Scale & Parallel (1–2 weeks)**
- Row/tiling `Parallel.For`; SIMD pass; headless runner; AOT publish; 200k+ target.

**P5 – Polish & Experiments (ongoing)**
- Replay; richer metrics; scenario scripts; writeups/graphs from Python notebooks.

---

## 15) Experimentation Plan (R&D)
- **E‑01 Cell size sweep**: measure neighbours/agent, FPS; pick optimal per density.
- **E‑02 Grid vs hashed bins**: compare rebuild + query costs.
- **E‑03 Double vs single buffer velocities**: numerical stability vs cost.
- **E‑04 SIMD vs scalar**: force accumulation variants.
- **E‑05 Parallel partitions**: rows vs tiles; false‑sharing analysis.
- **E‑06 Mutation regimes**: uniform vs Gaussian; clamping strategies; diversity over time.
- **E‑07 Aggression matrix**: map of regimes (stable coexistence vs domination vs oscillation).
- **E‑08 Diffusion cadence**: update frequencies vs visual fidelity/perf.

Each experiment: hypothesis → setup → metrics → acceptance criteria → notes.

---

## 16) Risks & Mitigations
- **GC churn** → enforce zero‑alloc in Tick; ArrayPool; snapshot tests.
- **False sharing** → partition by rows/tiles; pad shared counters.
- **Non‑determinism** → stable loops, seeded RNG, no time‑based randomness.
- **Unbounded populations** → cap Capacity; reproduction cost; compaction schedule.
- **Complexity creep** → keep systems small & testable; feature flags; presets.

---

## 17) Integration & Future Directions
- **Analytics**: Python notebooks reading snapshots (Parquet/CSV) to plot phase diagrams, diversity, aggression outcomes.
- **Ecosystem**: obstacles, resources with regeneration; predator/prey specialisations.
- **Economy**: simple goods exchange between agents; market price emergence.
- **Learning**: plug a bandit/RL policy per group and study strategy emergence.
- **Visuals**: triangle agents with heading; trails; density heatmaps.
- **Packaging**: self‑contained builds; NativeAOT headless server; presets folder.
- **Open sourcing**: MIT/Apache‑2 license; contribution guide; perf issues templates.

---

## 18) Definition of Done (per phase)
- **P1** DoD: 50k @ 60 FPS, zero allocs/step, Boids visible, grid property tests green.
- **P2** DoD: combat births/deaths visible; events/metrics emitted; snapshots saved.
- **P3** DoD: trait histograms change over time; at least two stable evolutionary regimes documented.
- **P4** DoD: 200k+ interactive or 1M headless; AOT artifact validated on target OS.

---

## 19) Appendix – Coding Guidelines (Core)
- `PascalCase` for types/methods; `_camelCase` private fields; file‑scoped namespaces.
- `<Nullable>enable</Nullable>`; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in Core/Tests.
- No exceptions in hot path; return codes/flags; guard once at edges.
- Avoid virtual/interface calls in inner loops; prefer direct/static.
- Document invariants atop each system (inputs, writes, side effects).

---

**This is the living source of truth.** Update as subsystems land (Systems, Config, Perf baselines, Experiments).

