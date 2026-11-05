# Canonical separation distance overflow

The tests-only [CI run 42081928431](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/42081928431)
reproduced exactly 12 failures and 1,601 passes before the production correction. The inspected
Ubuntu exact-head and Windows integration logs fail only the predicted large-scale direction,
multi-neighbor and mixed-radius witnesses. Ordinary arithmetic and compatibility controls pass.

This is a bounded follow-up to [#187](https://github.com/Chris0Jeky/SwarmingLilMen/issues/187)
for `SeparationRule.Compute` and the same rule's local-policy `Emit` path.

## Corrected case

A finite displacement can have a float squared length of positive infinity. If the
finite separation radius also overflows when squared, the old radius comparison
accepts the neighbor but the subsequent infinite distance reduces its separation
contribution to zero.

Both entry points now share a distance helper. The ordinary float squared-distance
comparison and square root remain unchanged. When that squared length overflows
and the displacement components, radius, and neighbor weight are finite, the helper
compares squared distance and squared radius in double precision, then narrows the
accepted distance to float. Comparing first prevents a length beyond float range
from reaching the subsequent influence calculation.

The rule retains its inverse-distance weighting, linear radius falloff, neighbor
iteration order, float accumulation, desired-speed multiplication order, and velocity
subtraction. The distance cutoff at `1e-4f` and the accumulator's `1e-6f` near-zero
cutoff remain in place. Nonfinite inputs keep their existing handling, including
`IntentWriter` rejecting a nonfinite emitted force.

## Regression geometry and scope

The public direct and local APIs accept supplied finite neighbor weights greater
than one. Those weights make the distance-overflow defect observable above the
existing accumulator cutoff:

| Case | Reverse displacement | Radius | Neighbor weight | Expected steering at speed 5 |
| --- | --- | --- | --- | --- |
| Ordinary control | `(3, 4)` | `10` | `10` | `(3, 4)` |
| Overflow regression, `s = 2^80` | `(3s, 4s)` | `10s` | `10s` | `(3, 4)` |
| Ordinary world-style weight, `s = 2^80` | `(3s, 4s)` | `10s` | `1` | `(0, 0)` due to the retained accumulator cutoff |

The previous implementation returns zero for the overflow regression. The tests
also use two axis-aligned neighbors whose independently derived contributions are
`(3, 2)` before normalization, so they check both inverse-distance influence and
radius falloff. Other controls cover radius boundaries, lengths beyond float range,
ordinary output bits, near-zero boundaries, ignored weights, and nonfinite inputs.

This does not claim an observable change for the world's usual field-of-view weights
in `[0, 1]` at those enormous distances: their individual contributions remain below
the preserved accumulator cutoff. It does not change derived radius overflow,
subnormal geometry, accumulator overflow, or validation of other rule controls.
Existing exact golden fixtures and physics ordering are unchanged. Historical
extreme-input runs using supplied weights can produce different results after this
correction; reproduce those results with their original binary and runtime.

## Validation

The focused regression class is `CanonicalSeparationDistanceTests`:

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter "FullyQualifiedName~CanonicalSeparationDistanceTests" -- RunConfiguration.TreatNoTestsAsError=true
```

Run the normal full test and CI gates on the exact candidate commit as well. No
performance claim is made for this change.
