# Canonical neighbor distance reuse

The bounded composition builds each visible neighbor's minimum-image displacement and distance.
The initial composition then repeated both calculations in a second traversal to produce nearest
neighbor and aggregate distance telemetry. `BuildLocalNeighbors` now returns those statistics from
the values it already computes. Every policy still receives the same bounded observation and
passes through the same intent writer and resolver; no built-in behavior bypasses composition.

The change retains ascending candidate order, the original float summation order, strict `<`
selection of the first equally near neighbor, independent forward/reverse displacements, and the
existing empty/overflow sentinels. FOV filtering and candidate truncation are unchanged. This is
an arithmetic reuse change, not a new distance model or a nearest-K query.

## Correctness evidence

The six `CanonicalNeighborTelemetryTests` cases first passed against the unchanged composition,
then against reuse. Each case runs with both Grid and Naive indexes:

- Equal nearest distances on opposite sides of the periodic seam, including an exact half-world
  displacement, preserve nearest direction and every distance aggregate.
- Observed agents with no visible neighbors retain per-agent `float.MaxValue` distance and zero
  aggregate statistics.
- Valid finite coordinates at a very large world scale retain the existing float-distance
  overflow diagnostics. This optimization does not silently change those diagnostics to double.

Those bullets record the optimization's original equivalence check. The separate
[extreme-distance correction](CANONICAL_EXTREME_DISTANCES.md) subsequently fixes the last case's
incorrect zero/infinite distances and updates its expected values. The timing and identity
measurements below remain records of the earlier, behavior-preserving reuse change.

The full integrated Release suite passed **1,018 tests** locally, including the existing exact
trajectory fixtures and zero-allocation checks. No golden fixture changed. Cross-platform CI is
still required for the published change; a local result does not substitute for the Windows gate.

A separate before/after probe ran eight deterministic cases for 128 ticks each: sparse, dense
with a four-candidate cap, dense with complete neighborhoods, classic, empty pipeline,
coincident/periodic-seam positions, one agent, and an empty world. It serialized raw float bits
for every bounded neighbor-view field, each completed kinematic state, every captured telemetry
scalar, and per-agent rule instrumentation. All eight records were byte-identical before/after;
the eight-line digest inventory's SHA-256 was
`4a0d674b3ded0e409ceb6405c82245a196621141942a5658e67c297b4276f80f`.
These are differential checks against the pre-change implementation, in addition to the analytic
edge cases and repository golden tests.

## Matched timing experiment (2026-10-29)

Before is [PR #184's reviewed source](https://github.com/Chris0Jeky/SwarmingLilMen/commit/0cbeeb10f9c20223d9c4445e330a61940811d540).
All 64 core, benchmark, harness and build-setting Git blobs in the measured before checkout
matched that commit. After changes only the neighbor/statistics traversal; its
`CanonicalWorld.cs` SHA-256 is
`5cd814c17c0465d2c2b119c0c26047e16c4a6166621505454a3d713df7f5c90b`.
The harness and benchmark scenario are unchanged.

The paired runs used BenchmarkDotNet 0.15.6, SDK 8.0.415/runtime 8.0.21, x64 RyuJIT,
Concurrent Workstation GC, and Ubuntu 24.04.3 on the same AMD EPYC 9V74 2.60 GHz cloud host.
BenchmarkDotNet reported one CPU and nine logical/physical cores. Raising process priority was
denied. Other task-owned builds, tests and native applications were paused during both pairs.

Each case starts from the production benchmark's seed 123456 in its 1920×1080 world, with
`SenseRadius = 10`, `MaxNeighbors = 32` and enhanced composition. It measures the complete step,
including grid rebuilding. Worlds evolve through the same number of warmup and measured ticks
within each pair. Construction, telemetry capture and rendering are excluded.

Use the same source-identical [CompositionCost harness](../tools/CompositionCost/Program.cs)
against each source checkout with the same SDK:

```sh
dotnet run --project tools/CompositionCost/CompositionCost.csproj -c Release -- --filter '*AgentCount: 1000)' --warmupCount 3 --iterationCount 5 --launchCount 1 --invocationCount 1024 --unrollFactor 1 --runtimes net8.0 --artifacts BenchmarkDotNet.Artifacts/reuse-1k
dotnet run --project tools/CompositionCost/CompositionCost.csproj -c Release -- --filter '*AgentCount: 50000)' --warmupCount 3 --iterationCount 5 --launchCount 1 --invocationCount 64 --unrollFactor 1 --runtimes net8.0 --artifacts BenchmarkDotNet.Artifacts/reuse-50k
```

The measured commands additionally selected that SDK executable with `--cli <dotnet-path>`.
Every command selected exactly one case. Error below is half the 99.9% confidence interval.

| Agents | Before mean ± error (ms/tick) | After mean ± error (ms/tick) | Observed mean change | Allocation before / after |
| --- | ---: | ---: | ---: | ---: |
| 1,000 | 0.565134 ± 0.046953 | 0.527112 ± 0.126233 | −6.73% | 0 B / 0 B |
| 50,000 | 98.743237 ± 5.193 | 88.032047 ± 32.336 | −10.85% | 0 B / 0 B |

The 1k baseline retained four of five samples after BenchmarkDotNet excluded the highest one.
Retaining all five raw samples gives a −7.44% change instead. Both 50k runs retained all five.
The confidence intervals overlap and the evolving-world samples show trends; this one experiment
does not isolate host/JIT effects or establish a general speedup. It also does **not** establish
recovery of the original composition's +16.53% small-population cost. That performance acceptance
remains open in #27. There is no new 10k throughput result and no renderer FPS claim.

All raw `WorkloadActual` iteration averages are retained here, including the excluded sample.
Units are milliseconds per tick before the tiny measured harness-overhead subtraction.

| Run | Agents | Invocations per iteration | Five raw iteration averages (ms/tick) |
| --- | ---: | ---: | --- |
| Before | 1,000 | 1024 | 0.563785095, 0.570335806, 0.586730554, 0.555396625, 0.571023890 |
| After | 1,000 | 1024 | 0.481231424, 0.509116427, 0.530179365, 0.555204735, 0.559842331 |
| Before | 50,000 | 64 | 100.456175906, 99.837078234, 98.385118500, 97.617552797, 97.420291953 |
| After | 50,000 | 64 | 98.085056625, 95.239571609, 86.689115750, 79.539106219, 80.607424469 |

All four MemoryDiagnoser records reported zero collections and zero allocated managed bytes in
their measured allocation intervals. This does not explain or erase the historical 10k 5 B/op
observation in #20, or claim that construction, registration, full rendering or the compatibility
snapshot allocates nothing.

## Validation commands

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --filter FullyQualifiedName~CanonicalNeighborTelemetryTests -- RunConfiguration.TreatNoTestsAsError=true
dotnet test SwarmingLilMen.sln -c Release --filter 'Category!=Performance' -- RunConfiguration.TreatNoTestsAsError=true
```

The broader force-policy contract, initial composition measurements and remaining generality
limits are documented in [Canonical composition](CANONICAL_COMPOSITION.md).
