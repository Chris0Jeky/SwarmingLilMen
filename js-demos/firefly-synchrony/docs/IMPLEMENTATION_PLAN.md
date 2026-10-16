# Firefly Synchrony: repository integration plan

## Scope of this PR

This PR adds the runnable browser implementation and a reproducible verification path. It does not add a native C# model, change CanonicalWorld or World, replace the RNG used by those engines, complete epic #10, or close its foundation-proof issues.

## Architecture and delivery sequence

Oscillator phases and intrinsic frequencies belong in a separate model, not a boids steering rule. Specify self-coupling, degree normalization, synchronous update ordering and noise units before porting. Implement a headless scalar baseline; add topology equivalence and zero-coupling analytical tests; only then add optimized global/grid paths. Use versioned streams and a normal-sampler cache in checkpoints.

1. Re-read the current issue #10 ordering and the configuration/runner/metrics work in #22, #23 and #24. These notes do not authorize skipping those gates.
2. Write a model-semantics document from the delivered source. Separate intended model rules from UI controls and implementation accidents.
3. Build a small headless C# reference model in an isolated model assembly after the experiment contracts are ready. Do not put every new model behind Boid/IRule/MaxForce.
4. Add deterministic state fixtures, independent invariants and boundary tests. A C# versus JavaScript comparison should use documented numerical/statistical tolerances, not a blanket bit-equality claim.
5. Connect manifest resolution, probes and artifact output. Keep scientific metrics separate from wall time, allocations and other nondeterministic operational measurements.
6. Port the inspector only after the reference implementation and regression suite are stable. Profile before optimizing storage, threading or SIMD.

## Current caveats and integration traps

Exports now preserve the Box–Muller cached Gaussian sample and flash state. Imports reject non-finite arrays, excessive populations and invalid settings before replacing live state. Legacy v1 exports without rngSpare remain readable, but exact continuation of an old odd-population noisy checkpoint cannot be guaranteed. The displayed 8-hex hash samples phases; the regression suite compares full numeric arrays and RNG state instead. Global mean-field coupling uses N including self, while metric and ring coupling use their local neighbour convention.

Checkpoint validation must complete before mutating a live model. Model time must not depend on animation frames. Population, work, input-size and output-history limits need explicit failure behaviour. Version schemas and random-stream semantics; do not silently regenerate golden fixtures.

## Agent handoff

Start with this directory's README and VERIFICATION.md, then the original INTEGRATION_NOTES.md. Treat proposed packages, interfaces and experiments as design input, not existing repository APIs. Refresh live issues and PRs before seeding work. Keep the existing browser examples intact and keep this model's native port separate from unrelated engine refactors. Use one small acceptance-driven PR per native slice; retain the owner gates around untrusted extensions and changes to authority.
