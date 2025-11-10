# Emergence Engine – Masterplan & Expansion Roadmap (v1)

*A modular, data‑oriented engine for massive agent systems, social dilemmas (incl. N‑IPD), economy & ecology sims, and exportable “reasoning systems”; built in C#/.NET, headless‑first with pluggable renderers (2D→3D), deterministic core, and research‑grade observability.*

---

## 0) North Star & Design Tenets
**North Star:** A general‑purpose, moddable engine where you can *define*, *run*, and *study* complex agent worlds—from bacteria‑like swarms to N‑person social dilemmas and market/ecosystem models—then **extract policies/behaviours** for reuse (e.g., NPC AI).

**Tenets**
1. **Emergence > scripts**: few composable primitives, rich macro behaviour.
2. **Deterministic & reproducible**: fixed timestep, seeded RNG, record/replay.
3. **Headless‑first**: pure sim kernel; renderers are adapters (2D now, 3D later).
4. **Data‑oriented performance**: SoA, zero‑alloc ticks, spatial partitioning, SIMD/parallel.
5. **Observability**: metrics, events, snapshots, experiments, baselines.
6. **Extensibility**: plug new spaces (2D/3D/networks), interaction rules, learners, economies.
7. **Interoperability**: clean DTOs + IPC (CSV/Parquet, gRPC/WebSocket) for notebooks/ML.
8. **Policy extraction**: distil learned behaviour to **interpretable** forms (BT/FSM/DT).

---

## 1) Scope & Capabilities
**Core**
- Large agent counts (50k–200k interactive; 1M+ headless); continuous 2D → 3D.
- Multi‑layer world: **spatial field(s)** + **networks** (social, trade, kinship, comms).
- Interaction patterns: local (neighbourhood), pairwise, k‑nearest, field‑mediated, market clearing.
- Social dilemmas: **N‑IPD** variants (pairwise vs neighbourhood voting), public goods, volunteer’s dilemma.
- Biology/economy primitives: needs & satisfaction, metabolism/utility, resources, markets, reproduction & mutation.
- Learning: scripted strategies, bandits, tabular RL (Q/HC/WOlf‑PHC), hooks for external learners.
- Policy export: trace → distilled policy artefacts (FSM/BT/tree) + ONNX for NN‑based.

**Tooling**
- Experiment runner, scenario DSL/JSON, snapshot export, replay, dashboards.
- Profile/bench harness; coverage & property tests; CI publishing (JIT/R2R/AOT).

---

## 2) High‑Level Architecture (packages)
```
SwarmSim.sln
  /Engine.Core         // headless kernel: world, schedulers, systems, spaces, networks
  /Engine.Modules      // optional modules: SocialDilemmas, Economy, Ecology, Learning
  /Engine.IO           // config/DSL, snapshots, metrics/events, IPC (CSV/Parquet/gRPC)
  /Engine.Inference    // policy distillation, BT/FSM export, ONNX runtime adapters
  /Render.Raylib2D     // 2D renderer (raylib‑cs), HUD, tweakables
  /Render.ImGui        // (optional) Dear ImGui overlay for inspectors
  /Render.Godot3D      // (later) 3D renderer adapter
  /Apps.SimRunner      // CLI/headless runner + batch/experiments
  /Apps.Sandbox        // interactive sandbox app using a renderer
  /Tests               // unit, property, integration
  /Benchmarks          // BDN microbenches for hot loops & regressions
```

**Core layers**
- **Kernel**: deterministic scheduler (fixed Δt), phase pipeline (sense→decide→interact→integrate).
- **Spaces**: `Continuous2D`, `Grid2D`, later `Continuous3D` (voxel grid), `GraphSpace` (networks).
- **Data**: SoA storages for agents & edges; pools; free‑lists; compaction passes.
- **Systems**: stateless ECS‑ish components; no alloc; explicit reads/writes.
- **Facades**: narrow API for renderers/clients; DTOs (snapshots/events/metrics).

**Extensibility points**
- New **Space** types, **System**s, **Interaction** rules, **Payoff** models, **Learners**, **Renderers**.
- Strategy catalog: pure functions + pluggable policies; registry is code‑first to remain AOT‑friendly.

---

## 3) Data & Memory Model
**Agents (SoA arrays)**
- kinematics: `X[], Y[], Z?[], Vx[], Vy[], Vz?[]`
- life/economy: `Energy[], Health[], Wealth[], InventoryHandles[]`
- social: `Group[], Role[], Persona[]`, `Flags[]` (bitfield: Fleeing/Hunting/Trading…)
- control: `PolicyId[]`, `Cooldown[]`, `Age[]`
- genome/traits: packed `Genome[]` (speed, sense, aggression, honesty, risk, colorIdx, etc.)

**Networks**
- Edge lists per relation (friendship/trust/trade); SoA edges: `A[], B[], Weight[]`, adjacency index.

**Fields**
- Scalar/vector fields (food, scent, price, pheromones) as tiles; multi‑rate updates (every N ticks).

**Scratch**
- Force/intent buffers `Fx/Fy/Fz`, micro‑stats (neighbour counts/centroids), market orders.

**Lifecycle**
- Fixed `Capacity`; `Count` active; deaths → free‑list; periodic compaction; stable iteration order.

---

## 4) Time, Scheduling & Determinism
- Fixed Δt (e.g., 1/120s); substepping for stiff rules; render decoupled.
- Phase order (per tick):
  1) **Rebuild spatial index** (uniform grid / voxel grid; cell≈sense radius)
  2) **Sense**: local aggregates from index/networks/fields
  3) **Decide**: policies/strategies compute intents (forces/votes/orders)
  4) **Interact/Resolve**: combat, trades, payoffs, reproduction/mutation
  5) **Integrate**: apply intents, clamp, wrap/reflect, update ages/energy
  6) **Emit**: events/metrics; schedule compaction every K ticks
- All randomness through a seeded `Rng` wrapper; no wall‑clock/time‑based noise.

---

## 5) Spatial & Network Mechanics
**Spatial Indexing**
- Uniform grid/voxel with `Head[cell]` + `Next[agent]` lists; amortized O(n) rebuild.
- Query neighbourhood: 3×3 (2D) / 3×3×3 (3D) cells; optional k‑NN via buckets.
- Experiments: cell size sweep; density control; Morton sort (later) for cache wins.

**Networks**
- Multi‑layer graphs (trust, kin, comms, trade) with per‑edge decay/update rules.
- Influence flows: message passing steps separate from physics step (lower frequency).

---

## 6) Modules (initial set)
### 6.1 Social Dilemmas Module
- **N‑IPD**: both *pairwise* (N−1 simultaneous dyads) and *neighbourhood* (single group vote). Configurable payoffs, discount γ, exploration ε, memory length H, noisy channels.
- Other games: Public Goods, Volunteer’s dilemma, Stag Hunt; composable **meta‑mechanics** (reputation, ostracism, voting rules, punishment costs).
- Render overlays: cooperation heatmap, payoff charts, reputation graph.

### 6.2 Economy Module
- Needs/utility vectors; production/consumption; resource fields & deposits.
- Price discovery: posted‑offer markets or double auction; budget constraints; inventories.
- Policies: heuristics (satisficing), myopic utility, bandit purchase, RL‑based shoppers.

### 6.3 Ecology/Biology Module
- Boids (sep/align/cohere), predation zones, metabolism, reproduction, mutation; diffusion fields.
- Evolution: heritable traits; selection via survival/utility, mutation kernels; lineage tracking.

### 6.4 Learning Module
- Built‑ins: ε‑greedy bandit, tabular Q, hysteretic‑Q, WoLF‑PHC; configurable state abstraction.
- **External learner bridge**: gRPC/WebSocket to Python (Gym‑like step/reset/obs/reward). Batch/async rollouts.
- **Policy distillation**: behaviour cloning (supervised) + tree induction to yield interpretable BT/FSM.

---

## 7) Scenario Definition & DSL
- **JSON/DSL** describing: world size/space, agent archetypes, initializers, payoff/game rules, schedules, metrics to log.
- Inline expressions for randomization (e.g., `speed ~ Normal(1,0.1)`).
- Reusable **presets** (Warbands, Rapid Evolution, Pairwise vs Neighbourhood N‑IPD, Market Shock, Resource Scarcity).
- Headless **SimRunner** loads scenario → runs batches → dumps artefacts.

---

## 8) Renderers & Tools
**2D (now)**: `Render.Raylib2D`
- Points/triangles; per‑agent colouring by group/trait/state; density heatmaps.
- HUD: FPS; counts; mean/variance of cooperation/wealth/energy; allocs; Δt.
- Tweakables: keys/sliders for weights, γ/ε/H, aggression matrices, market toggles.

**3D (later)**: `Render.Godot3D` (adapter)
- Same DTO stream; agents as billboards/meshes; navmesh/obstacles; camera rigs.

**Inspectors**
- ImGui panels: select agent → show needs/utility, policy summary, network egonet, history sparkline.

---

## 9) Observability & Data
- **Metrics** per tick: population, births/deaths, means/quantiles of cooperation/wealth/energy/speeds; CPU ms; neighbour stats.
- **Events**: Birth, Death, Attack/Trade, Vote, Payoff, Mutation, PolicySwitch.
- **Snapshots**: periodic raw arrays (binary/Parquet) + small samples for quick plots.
- **Replay**: input/config/seed log → deterministic re‑run for papers/demos.

---

## 10) Policy Extraction (“Reasoning Systems”)
1) **Trace capture**: (obs, action, reward, next‑obs) + derived features (last k votes, opponent fingerprints).
2) **Distillation**:
   - *Interpretable*: decision trees, rule lists, **Finite State Machines** (from discretised memory), **Behaviour Trees** (hand‑tuned nodes auto‑param’d).
   - *Compact ML*: small MLP → ONNX; or linear rules.
3) **Validation**: re‑embed distilled policy in engine; compare payoffs & cooperation; stability under noise.
4) **Export**: emit C#/JSON/BT XML; ready to drop into games as NPC controllers.

---

## 11) Performance Blueprint
- SoA arrays; `Span<T>`/`ref` locals; `ArrayPool<T>` for temps; no LINQ/delegates in hot loops.
- Spatial index rebuild O(n) w/ tight loops; neighbour scans branch‑reduced.
- SIMD via `System.Numerics` after scalar baseline; then **chunky parallelism** (rows/tiles) with `Parallel.For`.
- Periodic compaction; stable order for determinism; atomics only for global counters.
- Bench kit (BDN): grid rebuild, sense, integrate, full tick; density & cell size sweeps.

---

## 12) Security & Modding Considerations
- Sandbox scenarios: no dynamic code by default in shipped build; DSL only. Dev build supports C# hooks.
- AOT‑friendly plugin model: registration via attributes + source generators; fallback reflection in dev.

---

## 13) Migration Path: 2D → 3D
- Abstract `ISpace` + `ISpatialIndex` (2D grid → 3D voxel grid; 3×3×3 neighbourhood).
- Kinematics/promotions (Vec2→Vec3; add gravity/ground plane).
- Renderer swap: keep DTOs stable; Godot3D adapter first; later Unity/Stride if desired.

---

## 14) Risks & Mitigations
- **GC churn** → zero‑alloc tick; memory audits; pools; snapshot writers on background channels.
- **False sharing** → partition ownership; per‑thread accumulators; cache‑line padding.
- **Non‑determinism** → stable iteration orders; seeded RNG; no async in kernel.
- **Model bloat** → feature flags; module boundaries; scenario‑level toggles.
- **AOT friction** → minimize reflection; compile‑time registries; pre‑link policies.

---

## 15) Roadmap & Milestones
**P0 – Engine Skeleton (week 1)**
- Kernel (Δt, phases), SoA agents, uniform grid, Raylib2D renderer, metrics HUD.
- SimRunner + Scenario DSL v0; tests for grid/invariants; BDN baselines; CI.

**P1 – Social Dilemmas (week 2)**
- N‑IPD pairwise & neighbourhood; configurable payoffs γ, ε, H; reputation switch.
- Presets & overlays; experiment batcher; CSV/Parquet exports.

**P2 – Policy & Economy (weeks 3–4)**
- Bandit/Q‑learning (tabular) + state abstraction; needs/utility; simple market loop.
- Policy distillation MVP → decision trees; round‑trip validation.

**P3 – Biology/Ecology (weeks 5–6)**
- Boids + predation; reproduction/mutation; diffusion fields.
- Evolution scenarios; lineage viewer; mutation kernels sweeps.

**P4 – Scale & 3D (weeks 7–8)**
- Parallel tiles; SIMD; 200k+ target / 1M headless.
- 3D voxel index; Godot3D adapter PoC.

**P5 – Polish & Release (ongoing)**
- Replay; dashboards; docs; samples; publish JIT/R2R/AOT artefacts.

---

## 16) Experiments Backlog (with outcomes)
1. **Pairwise vs Neighbourhood N‑IPD**: cooperation regimes map over γ, ε, H, noise; locate phase boundaries.
2. **Punishment cost & reputation**: when does cheap punishment yield stable cooperation? Sensitivity to observation noise.
3. **Needs aggregation**: linear vs **non‑linear** (max/softmax/Choquet) prioritisation; impact on behaviour realism.
4. **Market shocks**: price volatility & wealth Gini under inventory/budget constraints.
5. **Mutation kernels**: Gaussian vs Cauchy vs log‑normal; diversity vs fitness trade‑offs.
6. **3D promotion cost**: perf delta 2D→3D; tile size & neighbourhood radius sweep.
7. **Policy distillation**: RL→DT/BT fidelity vs size; adversarial robustness.
8. **External learner latency**: throughput via gRPC batching; on‑policy vs off‑policy bridges.

---

## 17) Deliverables & To‑Dos (actionable)
**Engine.Core**
- [ ] `World` + schedulers; Δt fixed; seeded RNG
- [ ] SoA agent store; free‑list; compaction pass
- [ ] `UniformGrid2D` (head/next); neighbour API; tests & property checks
- [ ] Phases: Sense/Decide/Interact/Integrate; zero‑alloc contracts

**Modules.SocialDilemmas**
- [ ] N‑IPD model: pairwise & neighbourhood; payoff tables; γ, ε, H
- [ ] Reputation & punishment toggles; noisy observation channel
- [ ] Metrics: cooperation rate, payoff distributions; overlays

**Modules.Economy**
- [ ] Needs/utility vectors; metabolism; activities & costs
- [ ] Market microstructure (posted offer); inventory & budget

**Modules.Ecology**
- [ ] Boids systems; predation; reproduction/mutation
- [ ] Diffusion field (downsampled blur); cadence control

**Modules.Learning**
- [ ] Bandit + Q‑learning (tabular); state encoders; ε/α/γ schedulers
- [ ] External learner bridge (gRPC); batched rollouts; sample collector

**Engine.IO**
- [ ] Scenario DSL/JSON; presets; SimRunner CLI
- [ ] Snapshots (Parquet/CSV); Events/Metrics channels; Replay log

**Inference**
- [ ] Trace → DecisionTree; export C#/JSON
- [ ] BT/FSM exporters; validation harness

**Render.Raylib2D**
- [ ] Minimal renderer; HUD; tweakables (keys/sliders)
- [ ] Inspectors (ImGui): agent panel; graph egonet

**Perf/QA**
- [ ] BDN benches (grid, sense, integrate, tick)
- [ ] dotTrace/dotMemory baselines; zero‑alloc enforcement
- [ ] CI: build/tests/benches + publish JIT/R2R/AOT

---

## 18) Example Scenarios (sketch)
**S01 – Collaborative Hill vs Tragic Valley**
- N=3..25; pairwise vs neighbourhood; TFT/TFT‑E/Q; map cooperation surfaces over (γ, ε, H).

**S02 – Reputation Saves the Commons**
- Public Goods game w/ reputation score; ostracise below threshold; measure stability & welfare.

**S03 – Needs‑Driven Urban Emergence**
- Needs & activities; jobs/shops resources; commute costs; emergent districts & markets.

**S04 – Predator/Prey with Evolution**
- Traits: speed/sense/aggro; mutation σ sweep; lineage tree; coexistence regimes.

**S05 – Market Shock**
- Supply shock → price dynamics; inventory buffers; wealth distribution tails.

---

## 19) Documentation & UX
- Docs site with: Concepts (Spaces, Systems, Modules), How‑tos (create scenario, add policy, external learner), API refs, Samples.
- Tweakable overlays + hot reload of scenario config in Sandbox.
- Experiment notebooks (Python) reading Parquet.

---

## 20) Definition of Done (phase‑wise)
- **P1**: Grid+Boids/N‑IPD stable, zero‑alloc, 50k@60FPS, experiment S01 runs.
- **P2**: Learning & economy minimal; decision‑tree export validated.
- **P3**: Ecology + evolution with lineage; replay & snapshots in notebooks.
- **P4**: Parallel tiles, SIMD; 200k+ interactive or 1M headless; 3D adapter PoC.

---

### Closing
This plan aims to balance **engineering discipline** (determinism, testing, perf) with **scientific flexibility** (scenario DSL, experiments, policy extraction). Build **headless first**, keep **interfaces narrow and stable**, and let renderers/tools evolve around a strong kernel. Iterate via the experiments backlog and lock in reproducible baselines as the system grows.

