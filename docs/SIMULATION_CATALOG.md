# Simulation catalog and integration map

The browser collection contains more studies than the native engine currently runs. A shared
name does not promise identical algorithms, controls or trajectories. This map records actual
integration; it is a dependency map, not a second roadmap. [Epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10)
owns execution order.

## Discover the native engine

```sh
dotnet run --project SwarmSim.Cli -c Release -- --list-engines
dotnet run --project SwarmSim.Cli -c Release -- --engine aco --describe-engine
dotnet run --project SwarmSim.Cli -c Release -- --list-engines --json
dotnet run --project SwarmSim.Cli -c Release -- --engine aco --describe-engine --json
```

Discovery reads the same registry that creates experiments. It starts no simulation and creates
no run directory. Text is intended for quick use; JSON has discovery `schemaVersion: 1` and is
intended for scripts and agents. `--describe-engine` defaults to canonical unless `--engine` is
given. It reports principal state, native default configuration, spawn model, minimum manifest
schema, available metric names and their determinism/cost, and renderer availability.

The configuration object is a set of defaults, **not a JSON Schema or a promise that every
combination is valid**. Native validation remains authoritative. Metric names are case-sensitive.
Selecting runtime metrics disables exact replay; discovery makes that distinction available before
a run. Replay reruns a recorded initialization and verifies results; checkpoint restore is not
implemented. Both discovery commands accept `--json` and `--help`. The description also accepts
`--engine`. Run options are rejected so inspection cannot accidentally launch work.

For example, an agent can discover an engine, read its defaults and deterministic channels, write
a strict native overlay, then run:

```sh
dotnet run --project SwarmSim.Cli -c Release -- --engine aco --config scenarios/aco/base.json --seed 7 --agents 300 --ticks 2000 --metrics population,aco.foodReturned,aco.fieldTotal --visual --out runs
```

Use the emitted `manifest.json` with `--manifest` to replay. Use `--gallery scenarios/gallery.json`
for the existing short behavior tour. Reports are display-free; only legacy/canonical currently
have interactive Raylib renderers.

## Browser study → native model

| Browser study | Native integration at this checkpoint | Reusable capability still needed |
| --- | --- | --- |
| [Boids Basic](../js-demos/boids-basic/) | `legacy` and `canonical` are native boids implementations; no browser trace-equivalence claim. | Shared topology/state improvements must preserve canonical policy and historical replay contracts. |
| [Self-Propelled Particles](../js-demos/self-propelled-particles/) | `vicsek` runs through CLI/configuration, metrics, sweeps, manifests and reports. Native updates are synchronous; the browser reference uses sequential updates. | Broader topology can be evaluated against the existing fixed model and exact platform references. |
| [Ant Colony Optimization](../js-demos/ant-colony-optimization/) | `aco` is a bounded foraging model with a scalar pheromone field, carried food and separate agent/field phases. | Obstacles, multiple resources and optimization-quality criteria are absent; do not infer them from the demo title. |
| [Firefly Synchrony](../js-demos/firefly-synchrony/) | Browser-only at this checkpoint; selected as the next native integration. | Typed phase/frequency state, synchronous coupling, independent RNG and phase-order metrics. |
| [Turing Morphogenesis](../js-demos/turing-morphogenesis/) | Browser-only. | Multiple evolving field channels, stencil/boundary semantics, field-only experiment capture and field visualization. |
| [Avalanche Lab](../js-demos/avalanche-lab/) | Browser-only. | Integer fields, stable event queues, relaxation-until-stable semantics and exact mass accounting. |
| [Ecogenesis](../js-demos/ecogenesis/) | Browser-only. Existing legacy configuration names do not implement its ecology. | Stable lifecycle IDs, birth/death publication, typed traits/genomes, resources, lineage metrics and deterministic event order. |
| [Particle Swarm Optimization](../js-demos/particle-swarm-optimization/) | Browser-only. | Objective evaluation, per-agent best memory, shared best reduction, optimization bounds and fitness metrics. |

The demos remain independently usable. The goal of native integration is to extract mechanisms
that multiple models can share while retaining their different state and equations. Copying each
browser environment into a new isolated application would not establish that reuse.

## What counts as integrated

A native foundation needs an engine registration, strict configuration, named random streams,
deterministic stepping, honest capture/metrics, repeat and manifest replay, a sweep/example,
and analytical tests with explicit controls. It must document which common mechanisms it uses,
what representation it adds and where the abstraction still fails. Visual output must explain
its projection: an oscillator's phase is not a velocity, and a field is not a population of agents.

Models do not need the same agent struct or the same equations. Generalization should share
phase execution, state publication, topology, fields, interactions and observation where the
models provide concrete evidence for the contract. More foundation directories alone do not
prove a universal engine.
