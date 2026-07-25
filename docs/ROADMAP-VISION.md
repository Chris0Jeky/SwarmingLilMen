> **Aspirational reference (2026-07-25): ~15% built.** The
> [epic #10 checklist](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10) is the executable plan
> and source of sequencing authority; this document is not the plan of record.

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

### Security non-goal

> **Security non-goal:** The product term **sandbox** is retired. A **modeling boundary** or
> **interaction surface** describes how entities, policies, environments, resources,
> communication, and institutions interact inside a simulation; it is not security isolation.
> The engine provides no process isolation, resource quotas, filesystem/network capability
> restrictions, or protection from a malicious or buggy extension. Scenario/config input is
> data-only today by design, not by an enforced security guarantee, and any in-process policy has
> the host process's full authority. Before untrusted policy code, community mods, or an untrusted
> external learner can run, [issue #44](https://github.com/Chris0Jeky/SwarmingLilMen/issues/44)
> requires a separate OS process, no ambient authority, CPU/memory/output/wall-clock limits,
> validated size-bounded schemas, authenticated local-only transport, a dedicated threat model,
> and adversarial fail-closed tests. Until that workstream passes, no product name, flag, package,
> API, or product-facing documentation may imply containment. The formal T1 authority label in
> agent-control files is outside product terminology and does not assert security isolation.

Issue #44 is an unscheduled prerequisite marker with no owner or timeline. It is a stop condition,
not queued implementation work.

### Deferral boundary

Every primitive below remains speculative until the shared kernel has carried boids, Vicsek, and
ACO without foundation-specific kernel edits beyond #30's declared generic field phase
([#29](https://github.com/Chris0Jeky/SwarmingLilMen/issues/29) and
[#30](https://github.com/Chris0Jeky/SwarmingLilMen/issues/30)); the independent browser demos do not
satisfy that gate. The global proof does not replace each primitive's direct prerequisite:

In this table, #27's bounded contract means per-agent observations and tagged policy intents. Its
kernel resolver arbitrates only steering-policy forces; institution, market, and payoff resolvers
remain module-owned.

| Deferred primitive | Kernel prerequisite before implementation |
| --- | --- |
| Resources | #30 adds the generic kernel field seam; the ACO module owns pheromone state and deterministic update cadence through stable space and Observation/Intent seams. ACO is the proof case, with no pheromone-specific kernel branch. |
| Communication | The bounded Observation/Intent contract from #27 plus deterministic, capacity-bounded message topology, delivery order, and cadence owned by a module. |
| Institutions | Module-owned durable state and a versioned event contract; institutional intents use #27's bounded contract, while the module owns deterministic arbitration. No institution-specific kernel branch. |
| Markets | Resource/inventory ownership, order intents using #27's bounded contract, a module-owned deterministic market resolver, and metrics registered through #24's probe contract; no market-specific kernel branch. |
| Networks | The query-contract discipline established by #18, widened to a pluggable graph topology/edge store only when a real post-#30 consumer proves the need, without kernel conditionals. |
| N-IPD | Deterministic scheduling/RNG, observations/intents using #27's bounded contract, a module-owned deterministic payoff resolver, and cooperation/payoff metrics registered through #24's probe contract; pairwise/neighborhood variants remain module configuration. |
| IPC learners | The Raylib-free runner (#23), versioned reset/episode/reward and Observation/Intent protocols, bounded DTOs, backpressure, replay, and authenticated loopback-only transport for every learner connection; untrusted execution additionally requires #44's separate-process, no-ambient-authority, resource-limit, threat-model, and adversarial-test gates. |
| Policy distillation | The Observation/Intent contract from #27, an explicitly designed versioned `(observation, intent, reward, next-observation)` trace schema and capture path, and round-trip validation through the experiment spine (#23-#24). The trace work remains unscheduled; section 10 is blocked on these contracts, not on distillation algorithms. |

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
  /Apps.Playground     // interactive playground app using a renderer
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
- Typed random-distribution declarations (for example, `speed ~ Normal(1,0.1)`) parsed as bounded
  data through a fixed allowlist; never evaluate a general-purpose expression or host-language code.
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
> **Speculative and deferred:** this section cannot begin until #27 supplies a stable
> Observation/Intent contract, #23-#24 supply the experiment runner and metrics spine, and an
> explicitly designed versioned trace schema and capture path exists. That trace work is not yet
> scheduled. Choosing a distillation algorithm is not the current blocker.

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

## 12) Extension Trust & Authoring
- Scenario authoring is data-only in shipped builds as a design choice, not an enforcement boundary.
- Future authoring syntax remains a fixed, size-bounded declarative grammar; general expression
  evaluators or dynamic code are untrusted extension paths blocked by #44.
- Development C# hooks are trusted in-process code with the host process's full authority; #44
  blocks any future untrusted extension path.
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
- Tweakable overlays + hot reload of scenario config in the Playground app.
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
