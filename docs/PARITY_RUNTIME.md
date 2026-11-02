# Parity runtime investigation — 2026-11-02

This bounded experiment advances [#28](https://github.com/Chris0Jeky/SwarmingLilMen/issues/28).
It preserves the five recipes, five seeds, four models, every-tick sampling, exact repeat/index
checks and locked metric bands. The under-60-second target for the complete 100-run primary
matrix remains open; the measurements below concern one unchanged S4/seed7/classic case.

## Baseline profile and controls

The baseline is commit `e512f984b743b93455bd7bf7a0bf23dfaa7b30f4`, tree
`3489216efae5a15ffd66aab845d20a54a07adb26`. Its complete 391-file snapshot was checked against
Git blob hashes and file modes. Release used SDK 8.0.427 and runtime 8.0.33 on Ubuntu 24.04.3/X64.
The workload is S4_UNIFORM_RANDOM_512, seed 7, ClassicReynolds, 512 agents and 1,800 ticks.

The unchanged binary was run unprofiled, profiled, then unprofiled again. The profiler was
Microsoft dotnet-trace 9.0.661903 with `dotnet-sampled-thread-time,dotnet-common`, launching the
built parity DLL directly rather than tracing the SDK build/run command:

```sh
dotnet-trace collect --profile dotnet-sampled-thread-time,dotnet-common --output baseline.nettrace --show-child-io -- dotnet SwarmSim.Parity/bin/Release/net8.0/SwarmSim.Parity.dll run --scenario S4_UNIFORM_RANDOM_512 --seed 7 --model classic --out runs/profile-s4
dotnet-trace report baseline.nettrace topN -n 20 --verbose
```

This is **managed sampled thread time**, not kernel CPU attribution. Microsoft documents the
[sampling profile and its limits](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).
The method names below guide source inspection; inclusive and exclusive samples must not be
summed as independent costs. Native work and sampling attribution also limit interpretation.

| Baseline launch | Primary runner interval (s) | Outer process interval (s) |
| --- | ---: | ---: |
| control-before | 29.264223 | 29.850803 |
| profile | 25.881709 | 26.564143 |
| control-after | 46.347277 | 46.774836 |

Every scientific file and experiment metadata file was byte-identical across those three runs;
only operational timing/log files were excluded. The wide control variation prevents a useful
profiler-overhead estimate. The profiled run being faster is not evidence that profiling sped up
simulation.

| Reported method | Exclusive sampled thread time |
| --- | ---: |
| `UniformGrid.QueryRadiusToroidal` | 37.91% |
| `ParityGeometryProbe.Sample` | 16.96% |
| `Buffer._Memmove` | 11.00% |
| `CanonicalWorld.StepCore` | 9.06% |
| `CanonicalWorld.BuildLocalNeighbors` | 8.65% |
| `Cryptography!Interop+Crypto.EvpDigestUpdate` (hash-provider interop) | 7.22% |

## Bounded implementation changes

1. `MathUtils.MinimumImageDelta` bypasses `%` only when `abs(delta) < extent`. Remainder is
   exactly the original float there, including signed zero. Exact multiples, out-of-range deltas,
   nonfinite deltas, and NaN/nonpositive extents retain the preceding remainder path and folding
   order. Finite deltas with a positive infinite extent take the identity path and preserve their
   previous results.
2. `ParityGeometryProbe.Distance` similarly bypasses remainder for a nonnegative difference
   smaller than the validated extent. Finite input positions and finite circular centers make
   that condition safe. Euclidean distance and the inclusive neighbor/collision tests are unchanged.
3. `ParityRunner.AppendState` encodes at most 64 agents in a fixed 1 KiB stack buffer before each
   hash-provider call, instead of calling the provider once per agent. It writes every included
   record and hashes only the populated tail. Little-endian float component order, agent order,
   tick order and all state identities remain the same; no population-sized buffer is allocated.

The two exact all-pairs metric scans remain independent. They use different coordinate arithmetic,
so this change does not fuse them or substitute a steering neighborhood for a scientific metric.
No spatial acceptance test, traversal order, recipe, seed or baseline tolerance changes.

## Unprofiled comparison

After inspection and implementation, preserved baseline binaries and the changed binaries ran
in A1–B1–B2–A2 order on the same host. All other task-owned benchmarks and tests were paused.
The host remained shared; these four fresh process launches do not estimate a universal effect
size. Each launch used the same S4/seed7/classic command above without dotnet-trace.

| Launch | Binary | Primary runner interval (s) | Outer process interval (s) | Child user CPU (s) |
| --- | --- | ---: | ---: | ---: |
| A1 | baseline | 42.285950 | 42.874023 | 42.053801 |
| B1 | optimized | 19.857453 | 20.355050 | 20.551258 |
| B2 | optimized | 20.313733 | 20.593870 | 19.484666 |
| A2 | baseline | 34.041754 | 34.410902 | 32.771737 |

Both optimized launches were faster than either baseline launch in this observation, while the
baseline intervals themselves varied substantially. The result supports advancing this bounded
optimization; it does not qualify the whole matrix's 60-second target, renderer throughput or
large-population engine performance. Timing was measured on the base plus these three production
edits only. Other numeric/renderer/collector fixes merged in the same maintenance round are not
part of this timed comparison; their integrated correctness is qualified separately by CI.

All five scientific files match byte for byte across A1/B1/B2/A2: per-tick metrics, requested and
post-ingest initial states, resolved configuration and summary (including every-tick/final state
hashes and all window statistics). Experiment metadata matches after excluding and separately
recording the changed implementation binary hashes. Operational timing/logs are intentionally
outside scientific identity. Binary and source hashes were rechecked after every run.

## Regression and integration gates

Twenty-five new cases cover analytical minimum images over binary scales from subnormal to huge,
signed zero, exact wrap multiples, adjacent half/whole-extent bit patterns, nonfinite compatibility,
metric neighbor/collision edges with out-of-domain coordinates, and complete hash streams at
1/63/64/65/129 agents. The hash oracle independently encodes individual float words with
`BinaryWriter` and hashes the complete stream once, including initial and subsequent ticks.

The focused suite passed 36/36; the base plus performance patch passed all 1,371 ordinary C# tests
with no skips. No existing golden fixture or numeric tolerance was updated. These functional
compatibility cases pass on the original arithmetic too; the pre-change evidence for this
performance work is the measured runtime/profile, not an invented functional failure.

The Scientific parity workflow now also triggers on the whole `SwarmSim.Parity` directory and
`MathUtils.cs`, in addition to its own orchestration files. The integrated reviewed head
`8dd2a734b27767b6e529ae2e323993dcfc16d7d5` passed all four ordinary Ubuntu/Windows exact-head and
merge-result jobs in [CI run 41449137035](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/41449137035):
1,532 ordinary C# tests each, deterministic references, 40 collector tests, actual benchmark
evidence smoke, manifest replay, the behavior gallery and the Linux native renderer probes.

All four full jobs passed in
[scientific run 41449136980](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/41449136980)
at that same head. Each normal receipt proves 100 primary + 100 repeat + 50 naive-index runs,
with current-build exact checks and all nine locked metric envelopes passing. Each separate
alignment-off receipt proves 100 primary runs and the required S4/S5 polarization rejections.
The locked baseline was unchanged. The actual merge of [PR #221](https://github.com/Chris0Jeky/SwarmingLilMen/pull/221),
`c1cfdb8bca6ac994a978dfec7740decedaac439d`, has the same tree as the reviewed head and tested
integration: `9c260f630ec3acdf56e7abd0ea6156a92eb5dc34`.

### Full integrated matrix timings

These receipts measure separate CI hosts and launches. They do not constitute a controlled
platform comparison or a controlled before/after experiment. The isolated matched experiment
above remains the evidence for advancing the optimization.

| Platform | Primary, 100 (s) | Repeat, 100 (s) | Naive-index, 50 (s) | Normal matrix (s) |
| --- | ---: | ---: | ---: | ---: |
| Ubuntu | 258.550 | 258.405 | 216.811 | 733.770 |
| Windows | 171.189 | 171.185 | 155.497 | 497.876 |

The separate 100-run alignment-off primary intervals were 239.725 s on Ubuntu and 243.242 s on
Windows; their matrix intervals were 239.728 / 243.247 s. The matrix clock includes the completed
runs and exact verification work but excludes build/startup, baseline comparison and final
summary/metadata writes, as specified by timing version 1. The wrapper's broader command
interval is recorded separately in each receipt. Both normal primary observations remain above
the 60-second target, so #28 stays open.

All four downloaded CI archives match their advertised SHA-256 digests. An independent artifact
audit checked each normal matrix's 100 CSV hashes and 78,800 tick rows. The 75 unmodified-model
summary rows (including all state/CSV identities and window values) match exactly between normal
and alignment-off jobs on each platform. This does not assert cross-platform bit identity.
Baseline receipt hashes differ only because the Windows checkout uses CRLF and Ubuntu uses LF;
each matches the unchanged committed baseline with its checkout line endings.

| Platform / mode | Artifact ID | ZIP SHA-256 |
| --- | --- | --- |
| Ubuntu normal | `13143493802` | `4df86e9b88fbba6491a55d90b1256a6337e76f0536b0a009ba0d11f40ada8a00` |
| Windows normal | `13144278243` | `af087ecb1103c7234015e386e21fabe133c95f7a98941b1dfc009aefa0adb261` |
| Ubuntu alignment-off | `13144641592` | `25e504a5655aef8dbb02752a8689f5c01ef2f279c31f230bcd952c0934d88517` |
| Windows alignment-off | `13144660467` | `c66d2a17da57cda81cf3f7fcb3f6697c665bcb2ea669bae4a49bc77ad179d4b0` |

## Evidence identities

The following binary hashes identify the isolated before/after comparison. Later integrated
builds naturally have different hashes.

| Binary | Baseline SHA-256 | Optimized SHA-256 |
| --- | --- | --- |
| `SwarmSim.Core.dll` | `7cc2b02238435b53cd0a9c6906847208c4eb981aa58095d924f78061b19d50b4` | `f0b42c5531ac3c5f596ce7fc9f2502a73e871855edbf06568eafa614acefbffe` |
| `SwarmSim.Parity.dll` | `7795e6959093afa8a14b4e236e2cb0c08d3757c1367ac390768df319c9471786` | `4a89db802f9954b39fc989e3fa355df8be79256433b07cc359d4841e129dba2c` |

The unchanged scientific artifact hashes are:

| File | SHA-256 |
| --- | --- |
| `S4_UNIFORM_RANDOM_512/7/classic/configuration.json` | `e403eedd3a0d83e0fd2a598f63904e3a1b1828dcb6bdce4471271751e35c8f84` |
| `S4_UNIFORM_RANDOM_512/7/classic/initial.csv` | `0fe9e36c888df06627b22867d44730dbf798a458fc7303fb4aee7be1ac121309` |
| `S4_UNIFORM_RANDOM_512/7/classic/metrics.csv` | `6d4707d442345a5c3f0118eef63f1e24803cb99eb44e2656da6c4ee46f7777a4` |
| `S4_UNIFORM_RANDOM_512/7/classic/requested-initial.csv` | `311e38235fc54529ac2703b6044f90f6f916f97a8cc64c4624ffe238913e419d` |
| `summary.json` | `a7d8eecb44bd0e33b46db44681eb8401dc8b27fd05f39fe80cbd118eda01a9a3` |

Changed production source SHA-256 values:

| File | SHA-256 |
| --- | --- |
| `SwarmSim.Core/Utils/MathUtils.cs` | `dd41d61096f686889cc95a1fc871b662b3adf720116115076cabb0ec351f05bc` |
| `SwarmSim.Parity/ParityGeometryProbe.cs` | `24a1cc062463a49018bb374d4f5fa86f63242ab77c1a8d6ec20ecd673639810f` |
| `SwarmSim.Parity/ParityRunner.cs` | `287feb4b977bb313774be89cf55bdf5a95bcc9c90c9cc115733d97b76258fa4c` |

Raw trace, reports, stdout, timings, cgroup observations and artifact receipts were retained outside
Git. Generated profiler/benchmark output is not committed. The remaining runtime investigation
must preserve the full scientific contract and use repeated matched observations when comparing
further optimizations. See [the parity guide](BEHAVIORAL_PARITY.md) for workload/timing scopes.
