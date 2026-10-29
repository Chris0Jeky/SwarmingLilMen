# Behavioral Parity Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task by task; implementation is authorized
> by the ongoing repository task. Root performs independent review and connected-GitHub publishing.

**Goal:** Run the five prescribed micro-scenarios through four models and make honest deterministic
and ensemble comparisons, retaining the legacy retirement bar.

**Architecture:** A separate `SwarmSim.Parity` executable consumes Core without changing physics.
It owns strict scenario data, an independent float32 reference, metric adapters, stable artifacts
and an explicit record/verify baseline protocol.

**Tech Stack:** C#/.NET 8, existing Core metrics and xUnit; no new packages.

**Spec:** [BEHAVIORAL_PARITY.md](../../BEHAVIORAL_PARITY.md), frozen before calibration.

## Global constraints

- Preserve existing goldens and engine semantics; no renderer/browser-demo changes.
- Five scenarios only, seeds 7/19/42/73/101, final 20% window, strict input inventories.
- Source/baseline summaries only in git; retain raw calibration CSVs locally.
- Same-build/platform exact repeats and index comparisons; no cross-OS exact claim.
- Do not move bands/seeds to hide a failing alignment witness or timing target.

## Review focus

Input omissions, duplicate/unknown properties, mismatched baseline inventories, undefined circular
centroids, and engine initialization differences must be observable and fail closed where required.

### Task 1: Scenario data and independent reference

- [x] Write analytical initial-state/reference tests, observe missing implementation fail.
- [x] Add strict `ScenarioRecipe` JSON loading and named-stream generation for five data recipes.
- [x] Implement independent float32 all-pairs reference and engine adapters.
- [x] Run focused tests; measure one S4 pass early to bound the full workload (49.242 seconds for one seed/four models).

### Task 2: Metrics and deterministic artifacts

- [x] Write hand-calculated periodic geometry/turn/count/percentile tests.
- [x] Register shared motion/nearest probes plus analysis-only geometry/turn probes.
- [x] Emit stable per-tick CSV, initial samples, and compact result identities.
- [ ] Prove repeated artifacts and grid/naive state equality with focused tests and real runs.

### Task 3: Baseline record/verify and mutation witness

- [x] Add malformed/missing/incomplete baseline rejection tests before verifier implementation.
- [x] Record every seed/model/scenario result, then calculate documented fixed absolute bands (100 rows, nine bands, 293.754 seconds).
- [ ] Verify the locked baseline; run alignment-off through the same gate and retain red evidence.
- [ ] Run the full workload twice; document measured elapsed time without CI timing assertions.

### Task 4: Integration and evidence

- [x] Coordinate solution/test-project/CI edits with root; retain exact owned-file inventory (existing CI only, full matrices explicit).
- [ ] Finish harness instructions and measured limitations; link retirement checklist from status.
- [ ] Run full Release suite and request independent review before publication.

