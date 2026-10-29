# Representable canonical neighbor distances

This change addresses the bounded-observation and nearest-neighbor part of
[issue #187](https://github.com/Chris0Jeky/SwarmingLilMen/issues/187). It corrects a real proximity
error while retaining ordinary-scale float arithmetic. The wider numeric audit remains open.

## Failure and correction

`sqrt(dx * dx + dy * dy)` can lose a representable distance when its intermediate products use
float. For example, two visible agents separated by `5e24` have a finite representable distance,
but its squared float overflows. The old observation reported infinity. Because infinity never
replaced the nearest-distance sentinel, the nearest statistic subsequently became zero.
Underflow can likewise turn a nonzero separation into zero; subnormal products can also lose
enough precision to distort the distance.

The consequence reaches behavior: `SeparationPriorityStage` consumes the nearest distance. A
neighbor outside its configured entry radius could activate separation priority, boost/attenuation
and speed droop because the reported nearest distance was zero. The correction makes the stage
use the representable distance without changing its thresholds, hysteresis or update order.

The local distance helper keeps the existing `MathF.Sqrt` result when the float squared magnitude
is normal. Only a non-normal squared magnitude uses double products and a double square root,
then converts the resulting distance to float. This recovers overflow, nonzero subnormal and
underflow-to-zero cases. Exact zero remains zero.

Nearest-angle telemetry also needs the recovered length: dividing huge components by an infinite
float norm would otherwise produce a zero direction. That exceptional path divides the selected
minimum-image displacement by the recovered distance. The ordinary normalization expression is
unchanged, as is the established nearly-zero direction cutoff. Tiny distances therefore remain
measurable even when their direction is intentionally treated as nearly zero.

Candidate order, strict first-neighbor tie selection, FOV weights, query truncation, both
minimum-image displacement directions, and ordinary float summation order are unchanged.

## Analytical regressions

The tests use displacements `(3 * scale, 4 * scale)` whose distance is exactly `5 * scale` when
`scale` is a power of two. This supplies analytical expected values without repeating the helper's
calculation in the assertion.

| Scale | Defect exercised | Old observed distance | Correct distance |
| --- | --- | ---: | ---: |
| `2^0` | Ordinary float control | 5 | 5 |
| `2^80` | Squared overflow | Infinity | approximately `6.0446291e24` |
| `2^-75` | Nonzero subnormal product rounding | approximately `1.29674912e-22` | approximately `1.32348898e-22` |
| `2^-90` | Squared underflow to zero | 0 | approximately `4.03896783e-27` |

Every scale runs through both Grid and Naive indexes, in interior and periodic-seam layouts.
Sixteen additional cases put two equally distant neighbors on opposite sides and check the actual
nearest displacement received by a velocity stage, proving first-candidate tie selection remains
intact. Four enhanced-pipeline cases at overflow/underflow scales prove a visible neighbor at
`5 * scale` does not enter the default `1.6 * scale` priority radius or cause speed droop.
The earlier two-agent `5e24` regression now asserts finite observations and correct 0/180-degree
nearest angles.

Before the production correction, the initial 26 focused cases had **18 failures and 8 passes**,
including all four false-priority cases. After the correction and the additional tie matrix,
all **42 focused cases** pass. The complete local Release suite passes **1,054 tests**, including
the existing exact golden traces and warm-step allocation checks. This lane predates the separate
eight named-scenario additions; the final integrated count is reported by the publishing PR.

## Compatibility and evidence

No golden fixture, random-stream contract, manifest schema or ordinary numeric expression was
re-baselined. A separate eight-scenario, 128-tick differential probe preserved every recorded
bounded neighbor view, completed state, telemetry scalar and per-agent instrumentation value
byte-for-byte at ordinary scales. Its digest-inventory SHA-256 remains
`4a0d674b3ded0e409ceb6405c82245a196621141942a5658e67c297b4276f80f`.

An actual pre-fix default run with 96 agents, 128 ticks and seed 7 replayed successfully in the
corrected binary. The complete CSV stayed byte-identical and the hashes remained:

- Final kinematic state: `CE8101501FAE47656D48B70928D2FFCE05DED3C3EC7CAEB1AD5F743FEAA086CC`.
- Metrics CSV: `5F190BF9F1648A8399AE716F2C41BCE70CA1B591CB3CB9EF0354D8E1D95C1816`.

These are same-platform/runtime verification records. Exact-head Ubuntu and Windows CI remain
required before merging. There is no new throughput or renderer performance claim in this fix.

Runs whose behavior depended on the faulty extreme distances can intentionally change trajectory
or metrics. Reproducing those historical results requires the original binary and matching runtime;
the existing replay verifier rejects changed state/CSV hashes and publishes no completed replay
directory. This is an explicit exceptional-scale correctness change, not a golden-fixture migration.

## Consumer inventory and remaining scope

| Consumer | Effect of this correction | Remaining limit |
| --- | --- | --- |
| `NeighborView.Distance` | Recovers representable lengths for selected visible neighbors | It does not change which candidates pass the earlier FOV filter |
| Nearest distance/minimum/maximum telemetry | Uses recovered distances with the existing order and tie rule | No-neighbor sentinels remain unchanged |
| Nearest-angle telemetry | Large selected displacements normalize with the recovered length | The existing nearly-zero cutoff still suppresses tiny direction angles |
| `SeparationPriorityStage` | Receives the corrected nearest distance | Threshold/hold/ramp formulas and their separate extreme-value limits are unchanged |
| Average neighbor distance | Sums the recovered per-neighbor values | Local/world float sums can still overflow even when each distance and the mathematical average are finite |
| Partial-FOV filtering | Unchanged | Its earlier `Vec2.Normalized` call can still overflow and exclude large displacements |
| Shaped avoidance and both separation-rule APIs | Unchanged | Their direct squared-distance calculations still require their own bounded numeric review |
| Cohesion and other generic vector operations | Unchanged | Normalization, magnitude clamping and extreme controls are outside this fix |

The verified extreme-distance cases use full-circle FOV. This evidence does not establish that
every accepted extreme configuration has correct perception or dynamics. The uncorrected consumers
and aggregate-overflow cases remain explicitly tracked in #187. The prior
[statistics-reuse measurements](CANONICAL_NEIGHBOR_REUSE.md) describe the preceding optimization,
whose exceptional diagnostics were intentionally preserved until this separate correction.

## Reproduce the checks

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter 'FullyQualifiedName~CanonicalExtremeDistanceTests|FullyQualifiedName~CanonicalNeighborTelemetryTests' -- RunConfiguration.TreatNoTestsAsError=true
dotnet test SwarmingLilMen.sln -c Release --filter 'Category!=Performance' -- RunConfiguration.TreatNoTestsAsError=true
```
