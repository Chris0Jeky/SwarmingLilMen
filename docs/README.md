# Documentation Index

[`PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified implementation
state. [Epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10) is the executable plan and
ordering authority. Historical documents are context, not proof.

## Live Guides

- [`EXPERIMENT_FOUNDATION.md`](EXPERIMENT_FOUNDATION.md) - reusable configuration, spatial semantics and explicit telemetry
- [`HEADLESS_EXPERIMENTS.md`](HEADLESS_EXPERIMENTS.md) - run artifacts, native configurations and exact manifest replay
- [`METRICS.md`](METRICS.md) - reusable analysis probes, exact neighbor distributions and deterministic versus runtime channels
- [`DETERMINISM.md`](DETERMINISM.md) - measured platform scope, exact golden traces and fixture policy
- [`RANDOM_STREAMS.md`](RANDOM_STREAMS.md) - named PCG32 streams, independent ownership and the byte contract
- [`CANONICAL_COMPOSITION.md`](CANONICAL_COMPOSITION.md) - bounded force policies, classic mode and measured cost
- [`CANONICAL_NEIGHBOR_REUSE.md`](CANONICAL_NEIGHBOR_REUSE.md) - equivalent statistics reuse and matched timing evidence
- [`CANONICAL_EXTREME_DISTANCES.md`](CANONICAL_EXTREME_DISTANCES.md) - representable-distance correction and wider numeric limits
- [`BEHAVIORAL_PARITY.md`](BEHAVIORAL_PARITY.md) - four-model scenario runner, fixed seed calibration and explicit verification gates
- [`LEGACY_RETIREMENT.md`](LEGACY_RETIREMENT.md) - group, lifecycle, behavioral, performance and migration criteria
- [`CANONICAL_BEHAVIOR_SCENARIOS.md`](CANONICAL_BEHAVIOR_SCENARIOS.md) - quantitative scenarios and mutation witnesses
- [`RENDERER_CONFIGURATION.md`](RENDERER_CONFIGURATION.md) - shared startup layering and preserved renderer profiles
- [`CANONICAL_RENDER_TELEMETRY.md`](CANONICAL_RENDER_TELEMETRY.md) - reusable capture and recorded versus current query diagnostics
- [`NATIVE_UI_VERIFICATION.md`](NATIVE_UI_VERIFICATION.md) - native Linux interactions, screenshots and exact evidence scope
- [`QUICKSTART.md`](QUICKSTART.md) - five-minute setup and first run
- [`CONTROLS.md`](CONTROLS.md) - renderer controls and runtime parameter editor
- [`PARAMETER_GUIDE.md`](PARAMETER_GUIDE.md) - configuration field effects and ranges
- [`CONFIGURATION_COOKBOOK.md`](CONFIGURATION_COOKBOOK.md) - scenario recipes
- [`RENDERER_GUIDE.md`](RENDERER_GUIDE.md) - legacy and canonical renderer behavior
- [`SIMULATION_MECHANICS_EXPLAINED.md`](SIMULATION_MECHANICS_EXPLAINED.md) - simulation mechanics
- [`ROADMAP-VISION.md`](ROADMAP-VISION.md) - aspirational reference and explicit deferral boundary

## Security non-goal

> **Security non-goal:** The product term **sandbox** is retired. A **modeling boundary** or
> **interaction surface** describes simulation behavior, not security isolation. The engine
> provides no process isolation, resource quotas, filesystem/network capability restrictions, or
> protection from a malicious or buggy extension. Scenario/config input is data-only today by
> design, not by an enforced security guarantee, and in-process policies have the host process's
> full authority. Before any untrusted code, community mod, or untrusted IPC learner can run, the
> dedicated containment workstream in
> [issue #44](https://github.com/Chris0Jeky/SwarmingLilMen/issues/44) must pass its separate-process,
> no-ambient-authority, resource-limit, size-bounded-schema, authenticated-loopback-transport,
> threat-model, and adversarial-test gates. Until those gates pass, no product name, flag, package,
> API, or product-facing documentation may imply containment. The formal T1 authority label in
> agent-control files is outside product terminology and does not assert security isolation.
> Issue #44 is an unscheduled prerequisite marker with no owner or timeline.

Any future IPC endpoint, including metrics/events, notebook tooling, and learner bridges, must bind
to loopback and authenticate peers by default. Remote exposure requires a separate threat model and
explicit design decision.

The direct prerequisites for deferred resources, communication, institutions, markets, networks,
N-IPD, IPC learners, and policy distillation are listed in the
[`ROADMAP-VISION.md` deferral boundary](ROADMAP-VISION.md#deferral-boundary).

## Historical Archive

[`PHASE_3_READINESS_CHECKLIST.md`](PHASE_3_READINESS_CHECKLIST.md) preserves earlier readiness
criteria and assessments. Its checkboxes are historical context; current scheduling belongs to
epic #10 and its open dependencies.

Files under [`archive/`](archive/) open with dated banners. They preserve provenance and may contain
superseded plans, paths, or implementation claims; use the live sources above for current guidance.
