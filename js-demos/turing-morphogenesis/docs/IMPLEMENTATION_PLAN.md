# Turing Morphogenesis: repository integration plan

## Scope of this PR

This PR adds the runnable browser implementation and a reproducible verification path. It does not add a native C# model, change CanonicalWorld or World, replace the RNG used by those engines, complete epic #10, or close its foundation-proof issues.

## Architecture and delivery sequence

Use double-buffered A/B fields with an explicitly specified stencil, boundary sampler and iteration clock. Fix the update equation and clipping policy before optimization. Test a uniform field, boundary indexing, finite concentration bounds, replay and stencil equivalence. Treat the diffusion timestep and discretization as numerical-model choices; a visually plausible pattern is not a convergence proof.

1. Re-read the current issue #10 ordering and the configuration/runner/metrics work in #22, #23 and #24. These notes do not authorize skipping those gates.
2. Write a model-semantics document from the delivered source. Separate intended model rules from UI controls and implementation accidents.
3. Build a small headless C# reference model in an isolated model assembly after the experiment contracts are ready. Do not put every new model behind Boid/IRule/MaxForce.
4. Add deterministic state fixtures, independent invariants and boundary tests. A C# versus JavaScript comparison should use documented numerical/statistical tolerances, not a blanket bit-equality claim.
5. Connect manifest resolution, probes and artifact output. Keep scientific metrics separate from wall time, allocations and other nondeterministic operational measurements.
6. Port the inspector only after the reference implementation and regression suite are stable. Profile before optimizing storage, threading or SIMD.

## Current caveats and integration traps

Imported diffusion settings, clock, seed, boundaries, view choices and concentration buffers are validated before the current field is replaced. Existing valid numerical trajectories are unchanged. The suite exercises real downloads, file-input restoration and continuation; its deliberate invalid-input case emits one expected console rejection. Morphology labels remain heuristic, not scientific classifiers.

Checkpoint validation must complete before mutating a live model. Model time must not depend on animation frames. Population, work, input-size and output-history limits need explicit failure behaviour. Version schemas and random-stream semantics; do not silently regenerate golden fixtures.

## Agent handoff

Start with this directory's README and VERIFICATION.md, then the original INTEGRATION_NOTES.md. Treat proposed packages, interfaces and experiments as design input, not existing repository APIs. Refresh live issues and PRs before seeding work. Keep the existing browser examples intact and keep this model's native port separate from unrelated engine refactors. Use one small acceptance-driven PR per native slice; retain the owner gates around untrusted extensions and changes to authority.
