# Snapshot & Runner Hardening Notes

This short guide captures the work done to harden the snapshot pipeline and the recommended workflow for investigating future rendering anomalies (e.g., blinking agents, interpolation crashes).

## What Changed
- **SimulationRunner extensions** – added snapshot/ mutation versioning plus `ResetAccumulator()` and `NotifyWorldMutated()` helpers so any out-of-band world mutation can atomically reset interpolation state.
- **SimSnapshot contracts** – snapshots now carry `CaptureVersion` and `MutationVersion`, include a consistency check, and expose helper methods for debug assertions.
- **Renderer safeguards** – every spawn/reset path now calls `ForceSnapshotRefresh`, interpolation clamps to actual array lengths, and we skip lerp when mutation versions diverge. Additional structured logs fire when snapshot deltas exceed 50 agents.
- **Debug overlay** – toggle with `F12` to see prev/curr capture IDs, mutation versions, alpha, and accumulator. This replaces guesswork with concrete telemetry directly on screen.

## Debug Workflow
1. **Reproduce** with the overlay on. Watch the delta and mutation lines—if they spike, we know interpolation was skipped intentionally.
2. **Check logs** for `[Snapshots]` entries (refreshes, mismatches). Each entry includes capture/mutation versions so you can correlate with user actions (spawn/reset/etc.).
3. **Capture data**: if you still see oddities, trigger a manual `Export CSV` (key `C`) or add a temporary dump guarded by the new helper to persist the offending snapshot pair.
4. **Automated coverage**: `SimulationRunnerTests` now verify version monotonicity and mutation resets. When adding new mutation paths, write a small unit test to ensure `NotifyWorldMutated` gets called (look at the new `NotifyWorldMutated_BumpsMutationVersion_AndResetsAccumulator` test for a template).

## Next Steps
- Finish wiring structured logs into an optional CSV/JSON dump mode for deep dives.
- Add renderer-side tests that feed synthetic mismatched snapshots into `DrawAgentsInterpolated` to guarantee we never regress on bounds safety.
- Consider wrapping spawn/reset actions in explicit commands (queue) so rendering never runs concurrently with world mutations, keeping the pipeline even simpler to reason about.
