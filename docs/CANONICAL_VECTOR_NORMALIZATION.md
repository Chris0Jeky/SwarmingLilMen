# Normalizing finite vectors with overflowing squared lengths

This bounded follow-up to [issue #187](https://github.com/Chris0Jeky/SwarmingLilMen/issues/187)
corrects `Vec2.Normalized` and `Vec2.WithLength` when finite source components overflow their
squared float norm. Previously `(3 * 2^80, 4 * 2^80)` normalized to zero, even though its unit
direction is `(0.6, 0.8)` and its direction scaled to length five is `(3, 4)`.

The error reached public world ingestion. `TryAddBoid` and `SetVelocity` accept finite velocity
components and normalize them to the configured target speed. A huge finite input direction
previously produced a stationary boid. Correcting the vector operation retains that direction
and gives the requested finite speed.

## Bounded arithmetic change

Both methods keep their original float path whenever the norm is finite. Only finite source
components with a nonfinite float norm use double products, a double square root and double
division. The resulting components are converted back to float. Keeping the recovered norm in
double also handles a finite vector whose mathematical length itself exceeds `float.MaxValue`.

`Normalized` retains its existing length cutoff. `WithLength` retains its separate squared-length
nearly-zero check and its ordinary divide-then-multiply order. Its exceptional path additionally
requires a finite requested length: NaN/infinite targets keep their established results, including
the nearly-zero early return. Nonfinite source components retain their old behavior in both APIs.
No input guards, thresholds, defaults, `Length` or `LengthSquared` properties change.

## Analytical and consumer regressions

The tests cover ordinary and `2^80` 3-4-5 directions in every quadrant, finite positive/negative
requested lengths, tiny and huge output scales, a diagonal whose true norm exceeds float range,
and signed-zero axes scaled down to `float.Epsilon`. The ordinary `(1, 3)` case pins component
bits `0x3EA1E89B` and `0x3F72DCE8` for both methods. Boundary controls include the exact
`1e-6f` cutoff and its immediately adjacent float values. NaN/infinite source and target controls
pin the existing behavior.

The world regressions exercise both `TryAddBoid` and `SetVelocity` with ordinary and huge input
directions, then integrate a complete tick with an empty pipeline. They independently require
velocity `(3, 4)` or `(-3, -4)` at target speed five and the corresponding exact displacement.

Against the previous implementation, the focused matrix produced **18 failures and 45 passes**.
All failures were finite-source overflow cases; every ordinary, cutoff and nonfinite control
already passed. After the correction, all **63 cases pass**; a fresh local Release run including
the related clamp, ingestion and committed-golden checks passes **111 cases** with no skips.

## Compatibility and remaining limits

Extreme runs that depended on lost directions can intentionally change trajectories and metric
hashes; exact historical replay requires their original binary and matching runtime. Ordinary
golden fixtures and numeric tolerances are not rebaselined. This correction does not broaden the
steering resolvers' finite-squared-magnitude contract or qualify every accepted extreme setting.

Squared underflow outside the preserved nearly-zero policy, direct separation calculations,
overflow in derived controls and aggregate sample-count limits remain separately scoped work.
There is no throughput or renderer performance claim.

## Reproduce the checks

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter 'FullyQualifiedName~CanonicalVectorNormalizationTests|FullyQualifiedName~CanonicalVectorClampTests|FullyQualifiedName~CanonicalWorldIngestTests|FullyQualifiedName~GoldenTraceTests' -- RunConfiguration.TreatNoTestsAsError=true
dotnet test SwarmingLilMen.sln -c Release --filter 'Category!=Performance' -- RunConfiguration.TreatNoTestsAsError=true
```
