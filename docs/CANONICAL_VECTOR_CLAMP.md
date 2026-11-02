# Finite-vector magnitude clamping

This is a bounded follow-up to [issue #187](https://github.com/Chris0Jeky/SwarmingLilMen/issues/187)
for the public canonical `Vec2.ClampMagnitude` operation.

## Failure and correction

A vector with finite components can have an infinite float squared magnitude even when its length
is representable. For example, `(3 * 2^80, 4 * 2^80)` has length `5 * 2^80`. Two earlier outcomes
were incorrect:

- With a large, smaller cap, both squared magnitudes become infinity. The comparison
  `Infinity <= Infinity` returns the original vector without clamping it.
- With an ordinary cap, the inverse of the infinite float norm becomes zero. Multiplying by that
  scale returns a zero vector instead of the capped direction.

Only when the float squared magnitude is nonfinite, both source components are finite and the cap
is finite does clamping compare squared magnitudes and calculate its scale in double. The final
components are converted back to float. This also keeps the scale representable when clamping an
axis-aligned `float.MaxValue` component to `float.Epsilon`.

The ordinary finite-square comparison, inverse square root, multiplication order and return path
remain unchanged. Nonpositive caps still return zero. NaN/infinite inputs and an infinite cap
retain their existing behavior; this patch does not introduce new input validation. Other `Vec2`
operations, squared underflow and finite-square scaling limits are outside this correction.

## Analytical and compatibility checks

The tests use exact 3-4-5 directions with power-of-two source and cap scales. Their expected
components are `3 * min(sourceScale, capScale)` and `4 * min(sourceScale, capScale)`, with each sign
combination covered. Cases include both overflow failure modes, equal/larger caps, an ordinary
control and a very small cap. Separate axis cases check the smallest positive output and signed
zero in the untouched component.

An ordinary `(1, 3)` vector capped to one pins the existing component bits `0x3EA1E89B` and
`0x3F72DCE8`. Computing every norm in double would change the latter to `0x3F72DCE9`, so a global
arithmetic replacement would violate this compatibility check. Additional cases preserve the
established nonfinite-input and nonpositive-cap behavior.

Against the previous implementation, the focused matrix produced **14 failures and 24 passes**.
After the correction, all **38 cases pass** in a fresh local Release build. The failures covered
both lost directions and missing clamps, including the smallest-positive-float axis cases.

This public-vector correction does not broaden the steering resolver contract. Both canonical
resolvers reject weighted forces with a nonfinite squared magnitude before they clamp or spend
their force budget. Tests retain that rejection and assert that a rejected force leaves the
accumulated total and remaining budget unchanged. No golden fixture, default or numeric tolerance
is updated, and this change makes no throughput claim.

## Reproduce the checks

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter 'FullyQualifiedName~CanonicalVectorClampTests|FullyQualifiedName~CanonicalBoidsTests|FullyQualifiedName~CanonicalCompositionTests' -- RunConfiguration.TreatNoTestsAsError=true
dotnet test SwarmingLilMen.sln -c Release --filter 'Category!=Performance' -- RunConfiguration.TreatNoTestsAsError=true
```
