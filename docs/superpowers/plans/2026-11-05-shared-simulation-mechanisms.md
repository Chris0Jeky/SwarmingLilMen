# Shared simulation mechanisms implementation plan

> For agentic workers: use the repository's implementation and independent-review workflow. The owner has authorized implementation and safe merges; epic #10 remains the scheduling authority.

**Goal:** Turn repeated simulation mechanisms into reusable core code and connect a richer-state model to the existing experiment tools.

**Architecture:** Keep scientific state and rules in their foundation. Extract only ordered synchronous phase execution, typed state buffering and explicitly versioned model fingerprints. Prove reuse with Vicsek, ACO and a bounded firefly model; expose the registry to humans and agents.

**Tech stack:** Existing C#/.NET 8, xUnit, Python repository checks, GitHub Actions on Ubuntu/Windows. No new dependencies.

**Spec:** [Shared simulation mechanisms](../../SIMULATION_KERNEL.md); model-specific contracts travel with their individual changes.

## Constraints and decisions

- Preserve all existing ordinary trajectories, platform-specific goldens, random stream names and historical replay contracts.
- Keep legacy retirement, performance qualification and native human acceptance open until their own gates are met.
- Prefer typed state and explicit phases over a dynamic ECS or a universal force interface: the former has two existing consumers and permits incremental verification.
- Model fingerprints must state their coverage; kinematics alone must never be called full state.
- The browser demos are study references, not interchangeable implementations or automatically native engine features.
- Local .NET execution is unavailable in this session. Publish draft branches through GitHub and inspect exact-head plus integration CI; report that limitation accurately.

## Review focus

Phase exceptions/recursion; accidental next-generation reads; silently incomplete state fingerprints;
old-manifest compatibility; discovery commands accidentally allocating or launching a simulation.
Each is covered in its owning change's tests. No broad scheduler or checkpoint API is promised.

## Independently reviewable changes

- [ ] Add `SimulationSchedule` and `DoubleBufferedState<T>` plus lifecycle tests. Migrate Vicsek's existing buffer/phase order and ACO's existing public phases. Preserve mathematical expressions and ordering. Gate with model suites, unchanged goldens and gallery.
- [ ] Add a versioned state-fingerprint contract with explicit ACO field, resource, agent and RNG coverage. Retain kinematic version 1 and read old manifests. Test hidden-state mutations, malformed/mismatched profiles and publication cleanup.
- [ ] Integrate a bounded firefly/oscillator foundation using typed generations and phases. Add analytical synchronous-update, control, deterministic-stream and CLI repeat/replay/sweep tests plus model-specific metrics.
- [ ] Add registry-derived engine discovery and native defaults/metric metadata, update the browser-to-engine map, and record the remaining field-only/topology/lifecycle work as issues.
- [ ] Independently review each PR, inspect exact-head and generated-merge CI, reconcile current main before merge, and update PROJECT_STATUS and issues with measured evidence.

The numeric separation correction under #187 is a separate, independently reviewed change.
