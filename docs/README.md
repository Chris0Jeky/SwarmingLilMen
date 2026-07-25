# Documentation Index

[`PROJECT_STATUS.md`](../PROJECT_STATUS.md) is the live source of truth for verified implementation
state. [Epic #10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10) is the executable plan and
ordering authority. Historical documents are context, not proof.

## Live Guides

- [`QUICKSTART.md`](QUICKSTART.md) - five-minute setup and first run
- [`CONTROLS.md`](CONTROLS.md) - renderer controls and runtime parameter editor
- [`PARAMETER_GUIDE.md`](PARAMETER_GUIDE.md) - configuration field effects and ranges
- [`CONFIGURATION_COOKBOOK.md`](CONFIGURATION_COOKBOOK.md) - scenario recipes
- [`RENDERER_GUIDE.md`](RENDERER_GUIDE.md) - legacy and canonical renderer behavior
- [`SIMULATION_MECHANICS_EXPLAINED.md`](SIMULATION_MECHANICS_EXPLAINED.md) - simulation mechanics
- [`PHASE_3_READINESS_CHECKLIST.md`](PHASE_3_READINESS_CHECKLIST.md) - canonical-readiness evidence
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

Files under [`archive/`](archive/) open with dated banners. They preserve provenance and may contain
superseded plans, paths, or implementation claims; use the live sources above for current guidance.
