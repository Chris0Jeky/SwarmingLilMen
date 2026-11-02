# Representable distances in shaped avoidance

This is a bounded consumer follow-up to
[issue #187](https://github.com/Chris0Jeky/SwarmingLilMen/issues/187). The neighbor observation
already recovers representable extreme distances, but `ShapedAvoidanceStage` previously recomputed
the selected displacement's norm using squared floats. Overflow made an in-range neighbor appear
infinitely far away and skipped the stage's avoidance blend.

## Correction and preserved behavior

The stage now recovers the norm from double products only when the squared float magnitude is
nonfinite and both displacement components are finite. If that recovered distance lies inside the
gradual radius, the same exceptional path divides by it to obtain the away direction. Recovering
the distance alone would still leave a zero direction from the old overflowing normalization.

Finite-square distances and ordinary normalization retain their original expressions. The
nearly-zero guard, exclusive gradual-radius boundary, hard/soft radius interpolation, priority
blend and velocity update order are unchanged. This does not change `Vec2` normalization globally,
nor does it widen the accepted settings domain or alter nonfinite-input behavior.

## Independent expected values

At scale `s`, the regression uses a neighbor at distance `5 * s`, a hard radius of `5 * s`, a soft
radius of `12 * s` and a gradual radius of `20 * s`. There is no active priority. At the hard radius,
the soft interpolation is zero and the distance ratio is one. The influence is therefore `0.5`,
and the final interpolation weight is `0.35`.

For incoming velocity `(1, 0)` and allowed speed one, these inputs give simple component oracles:

| Neighbor displacement | Unit away direction | Expected resulting velocity |
| --- | --- | --- |
| `(5 * s, 0)` | `(-1, 0)` | `(0.3, 0)` |
| `(0, 5 * s)` | `(0, -1)` | `(0.65, -0.35)` |
| `(0, -5 * s)` | `(0, 1)` | `(0.65, 0.35)` |

The matrix covers `s = 1` and `s = 2^80`. It exercises the public stage directly and installs the
real stage in a `CanonicalWorld`, using both Grid and Naive indexes with interior and periodic-seam
layouts. World cases also check the actual stage execution count, nearest-distance telemetry,
neighbor counts and zero pre-stage steering. Boundary and tiny-distance controls preserve the
existing exclusions.

Against the previous implementation, the focused matrix produced **15 failures and 22 passes**:
every large in-range case retained the unmodified incoming velocity. After the correction, all
**37 cases pass** in a fresh local Release build, including the ordinary and exclusion controls.

## Compatibility and remaining scope

No ordinary-scale expression, golden fixture, default or numeric tolerance changes. Extreme runs
that previously skipped avoidance can intentionally change trajectory and metric hashes; exact
historical replay requires their original binary and matching runtime. This is a correctness
correction with no throughput or renderer performance claim.

The wider numeric audit remains open. In particular, overflow in derived radius products or other
control formulas, direct separation-rule calculations and generic normalization still require
separate bounded review. This correction covers a representable nearest distance and representable
derived radii; it does not establish correctness for every extreme accepted configuration.

## Reproduce the checks

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter 'FullyQualifiedName~CanonicalShapedAvoidanceDistanceTests|FullyQualifiedName~CanonicalExtremeDistanceTests|FullyQualifiedName~CanonicalCompositionTests' -- RunConfiguration.TreatNoTestsAsError=true
dotnet test SwarmingLilMen.sln -c Release --filter 'Category!=Performance' -- RunConfiguration.TreatNoTestsAsError=true
```
