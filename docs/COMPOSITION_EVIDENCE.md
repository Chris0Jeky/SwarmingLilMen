# Controlled benchmark evidence

`tools/CompositionCost/collect.py` is an opt-in Python 3.9+ driver around the existing
`CompositionCost` BenchmarkDotNet harness. It preserves a measurement's inputs and raw output,
then checks whether the requested evidence is complete. **It never assigns a performance pass
or fail.** The simulation, benchmark setup, seed and complete `Step` workload are unchanged.

## Commands

Run from the repository root. Each output directory must be new, even if an existing directory
is empty. `--dotnet` selects the SDK executable for both the Release build and BDN's generated
child; it defaults to `dotnet` on `PATH`.

```sh
# Artifact plumbing only: one 1k Dry invocation, no warmup.
python tools/CompositionCost/collect.py --agents 1000 --smoke --out artifacts/composition/smoke

# The documented longer 1k measurement settings.
python tools/CompositionCost/collect.py --agents 1000 --invocations 1024 --out artifacts/composition/before-1k

# The documented three-size matrix, 64 invocations per measured iteration.
python tools/CompositionCost/collect.py --agents 1000 10000 50000 --out artifacts/composition/before-matrix
```

Measurement mode fixes three warmup iterations, five measurement iterations, one launch,
unroll factor one, the Throughput strategy and the .NET 8 runtime. `--invocations` defaults to 64 and must be positive.
Choose the same invocation count, populations, SDK and harness for both sides of a comparison:
the world evolves during warmup and measurement. The driver does not compare two runs or claim
their environments are equivalent.

`--smoke` accepts only `--agents 1000` and no `--invocations` override. It selects BDN's Dry job,
zero warmup iterations, one measurement iteration and one invocation. Its cold-start timing and
allocation values are **not steady-state measurements or performance qualification**.

## Bundle contract

| File or receipt field | Evidence retained |
| --- | --- |
| `dotnet-info.log` | Unabridged output of the selected SDK's `--info` command. |
| `dotnet-version.log` | The selected executable's SDK version, read in the same working directory as the build. This is authoritative; BDN's optional `DotNetCliVersion` field can be null when `dotnet` is absent from `PATH`. |
| `build.log` | Unabridged Release build output, including failures. |
| `console.log` | Unabridged merged stdout/stderr from the BDN command. Raw `WorkloadActual` and `// GC:` records are retained. |
| `bdn/` | BDN's logs, full JSON report, measurement CSV and other result files. |
| `source-before.json`, `source-after.json` | Relative source filenames, byte lengths and SHA-256 hashes, plus a deterministic hash of each manifest. |
| `receipt.json` | Exact driver and subprocess arguments, working directory, requested cases/job, command exit codes, child runtime/GC metadata, source identities, actual generated-build hashes, artifact hashes and completeness errors. |

The source manifests work without Git and include dirty or untracked source files. They cover
the repository tree except `.git`, `bin`, `obj` and `__pycache__` directories, root `artifacts`
and `BenchmarkDotNet.Artifacts`, and the requested output directory. They identify the observed
content; the driver does not invent a commit SHA for an archive. Compare these file hashes with
the reviewed source when linking a result to a commit. Source directory symlinks are rejected
so followed build inputs cannot be silently omitted. The before/after comparison detects
content differences at those two points; it is not continuous filesystem monitoring and cannot
prove that a transient edit was never made and restored.

BDN's `--keepFiles` retains generated projects and binaries under `tools/CompositionCost/bin`.
The receipt takes the actual child directory from its execution record and hashes that directory's
files, including the generated executable DLL, runtime/dependency configuration, `CompositionCost`,
`SwarmSim.Benchmarks` and `SwarmSim.Core`. These are the child build's files, not just the launcher
build. The bundle records their hashes and original directory; it does not copy all retained
binaries into the output directory. Preserve that generated directory too if the binaries are
needed for a later profiler run.

The artifact manifest hashes every retained bundle file except `receipt.json` itself, avoiding
a self-referential hash. Environment overrides are limited to an explicit list of SDK/runtime
selection, processor-count, GC and tiering settings. There is no environment-variable dump.

## Completeness and failure behavior

The parser is deliberately scoped to the repository's BenchmarkDotNet **0.15.6** output and
this one benchmark. A complete bundle requires:

- Exactly one `CompositionCostBenchmarks.Step` result and corresponding raw log section for
  each requested population, with the fixed requested job characteristics.
- A successful child exit and actual workload records for every requested measurement
  iteration, with unique indices, the requested operation count, and matching JSON/log values.
- A raw allocation record containing GC collection counts, total allocated bytes and the
  requested invocation count, consistent with the JSON memory record and .NET 8 rounding.
  Counts must fit BDN's integer types; missing or unknown (`?`) raw bytes are incomplete.
  Nonzero allocation is accepted as evidence.
- SDK and child runtime/GC metadata, the actual retained child build, and source content that
  matches before and after collection.

For this pinned **BDN 0.15.6 / .NET 8** job, `BytesAllocatedPerOperation` is an integer. BDN
converts total bytes and operations to doubles, divides them, and rounds to the nearest integer
with ties to even. Its older-runtime allocation-quantum exclusion does **not** apply to .NET 8.
The collector checks that exact relationship, including double conversion for large integers.
For example, raw `336 / 64` exports `5`, while raw `32 / 64` legitimately exports `0`.
Multiplying the rounded `5` by 64 would recover only 320 bytes, so the receipt retains the
original 336 bytes separately. A JSON value of `0` or `5.25` alongside raw `336 / 64` is
inconsistent evidence for this job. These rules come from the pinned
[`GcStats`](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.6/src/BenchmarkDotNet/Engines/GcStats.cs)
and [`JsonExporterBase`](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.6/src/BenchmarkDotNet/Exporters/Json/JsonExporterBase.cs),
and are not a claim about every BDN version or runtime.

The .NET 8 allocation counter is **process-wide**: BDN calls
`GC.GetTotalAllocatedBytes(precise: true)`. The
[`Engine`](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.6/src/BenchmarkDotNet/Engines/Engine.cs)
collects those bytes in a separate workload interval after timing, with
`invocations * operationsPerInvoke` operations. This benchmark has one operation per invocation,
so that interval must contain the requested invocation count; it is not the sum of the five
timing iterations. Tests using `GC.GetAllocatedBytesForCurrentThread()` observe a different
scope and cannot by themselves explain a process-wide allocation record. Neither counter
identifies the allocating method. Raw samples and warnings remain available even if BDN
excludes a timing sample from its summary.

Exit code **0** means `evidence_complete: true`. The receipt always says
`performance_verdict: "not_evaluated"`. Exit code **1** means incomplete evidence; logs and an
incomplete receipt remain for diagnosis. Empty selections, missing/duplicate/failed cases,
missing raw records or binaries, and changed source all fail completeness. Invalid arguments
or an existing output path return **2**; an existing output directory is left untouched.

The quick fixture tests run in both normal and exact-head CI:

```sh
python -m unittest discover -s tools/CompositionCost -p "test_*.py"
```

Both CI jobs also run the real `--agents 1000 --smoke` collector on Ubuntu and Windows. This
checks SDK selection, the generated child, native path handling, JSON/raw-log agreement and
bundle completeness. Each job retains `artifacts/composition/ci/` for seven days under a
`composition-smoke-integration-*` or `composition-smoke-exact-head-*` artifact name, including
failure logs when available. These cold-start artifacts supply plumbing evidence only; CI does
not apply a timing or allocation threshold or claim performance qualification.

## Continuing limits

This command supplies evidence for a future investigation. It does not resolve
[issue #20](https://github.com/Chris0Jeky/SwarmingLilMen/issues/20): the historical 10k allocation
record of 336 bytes over 64 operations remains unexplained. Later zero-allocation observations
do not identify its cause, and a smoke allocation record does not reproduce that historical job.

[Issue #27](https://github.com/Chris0Jeky/SwarmingLilMen/issues/27) still needs performance
qualification against the original pre-composition baseline. The existing composition and
neighbor-reuse results retain their documented uncertainty; a complete bundle alone does not
establish recovery, absence of regression, or the 50k/60 FPS target. See
[composition measurements](CANONICAL_COMPOSITION.md#measured-composition-cost-2026-10-29) and
[neighbor-reuse measurements](CANONICAL_NEIGHBOR_REUSE.md).
