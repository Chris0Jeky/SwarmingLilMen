# Scalar-field finite range

`ScalarField` stores non-negative binary32 values on a periodic grid. A finite deposit is valid
only when the resulting cell value is finite. `Deposit` performs the same float addition as
before, checks the result, then writes it. Overflow raises `OverflowException` without changing
any cell. It does not saturate the value, widen stored precision or discard the deposit silently.

Previously, two individually finite `float.MaxValue` deposits produced infinity. A subsequent
`Evaporate(1)` multiplied infinity by zero and left NaN. The finite-input check alone therefore
did not maintain finite field state. This matters when a scalar field becomes shared simulation
state: a poisoned cell can affect sensors, metrics and every later update.

The guard preserves every representable addition's exact float bits, including huge values,
zero deposits and subnormal additions. Evaporation retains its existing flush threshold and
ordinary arithmetic. A deposit rejected for overflow leaves a valid field that can still be
sampled, summed or cleared with full evaporation.

`ScalarFieldOverflowTests` supplies two before/after overflow regressions and four exact finite
addition controls. The tests-only revision `d59ca738bba541d77e57b0a1e88fefc4d8ad3118` in
[PR #228](https://github.com/Chris0Jeky/SwarmingLilMen/pull/228) reproduced both missing-exception
failures in [CI run 42079420419](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/42079420419);
the existing 1,532 tests and all four finite controls passed in the inspected Ubuntu integration
job. Final correction evidence is recorded on the PR.

This is atomicity of a **single deposit**, not rollback of an entire simulation phase. If an ACO
field phase has already applied earlier deposits before a later one fails, those writes remain.
The experiment runner treats overflow as a failed run and does not publish a completed result.
Recovering or retrying a partially completed model update is outside this field contract.
