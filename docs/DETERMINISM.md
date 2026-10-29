# Determinism and golden traces

The replay contract is exact equality of ordered agent kinematics for a fixed experiment:
the same initial agents, configuration, seed, timestep sequence, implementation, and recorded
OS/architecture/.NET band. The committed golden traces make changes to that contract visible.
They compare a new run with fixed reference data, so a runtime or implementation change cannot
pass merely because two fresh runs changed together.

Cross-platform agreement is measured by the Ubuntu and Windows CI matrix. A passing pair of
runs establishes agreement for those tested environments and experiments; it does not establish
bit-portable transcendental math or a permanent `System.Random` sequence across future .NET
releases. See the verified-state section of [PROJECT_STATUS.md](../PROJECT_STATUS.md) for the
latest platform evidence. The initial reference was authored and checked on Ubuntu; Windows
agreement must come from its own CI execution.

## Committed experiment

[GoldenTraceScenarios.cs](../SwarmSim.Tests/GoldenTraceScenarios.cs) fixes the complete settings
used by each engine and eight ordered initial agents. Positions and initial directions use exact
integer values, including a pair across both wrap seams. The trajectories exercise sensing,
separation, alignment, cohesion, boundary wrapping, and seeded wander. The canonical trace also
exercises its turn limit and priority behavior. The legacy and canonical engines have separate
reference traces because their dynamics differ.

| Property | Reference |
| --- | --- |
| Seed | `424242` |
| World extents | `128 × 96` |
| Agents | Eight, in a fixed order |
| Fixed timestep | `1 / 32` seconds |
| Checkpoints | Ticks `0`, `1`, `16`, `64`, and `256` |
| Grid cell size | `8` |
| Sense radius | `24` |
| Reference date | 2026-10-29 |
| Reference environment | Ubuntu 24.04.3 LTS, x64, .NET 8.0.21, SDK 8.0.415 |

The reference follows the corrected legacy wrap geometry and active `GridCellSize` behavior
(#133 and #61), and the canonical radius-comparison fixes (#67). It captures the intended
semantics after those fixes.

[GoldenTraceFixtures.cs](../SwarmSim.Tests/GoldenTraceFixtures.cs) contains every checkpoint's
SHA-256 hash and all 32 raw IEEE-754 component words. There is no file-copy step or generated
fixture at test time: these values compile directly into the test assembly.

The [version-1 kinematic hash](../SwarmSim.Core/Diagnostics/SimulationKinematicHash.cs) encodes
the agent count as a little-endian 32-bit integer, followed by each agent's `X`, `Y`, `Vx`, and
`Vy` float bits in order. Resource state, genomes, groups, clocks, configuration, private RNG
state, and scratch buffers are outside this hash. The fixed scenario controls their relevant
inputs, and later kinematic checkpoints detect their effect on motion. Signed zero remains
distinct in the raw bits and hash.

## RNG sequence reference

The same fixture pins the first 16 results of each of these APIs, each starting with a fresh
`Rng(424242)`:

- `Next()`: exact integers.
- `NextFloat()`: exact 32-bit float words.
- `NextDouble()`: exact 64-bit double words.

These assertions detect a change to the seeded generator or its floating-point adapters.
They also make an intentional future PRNG migration an explicit reference change. The tests
do not replace the separate RNG bounds, validation, and stream-independence tests.

## Running and reading the checks

The seven [GoldenTraceTests](../SwarmSim.Tests/GoldenTraceTests.cs) run in the normal
non-performance test suite and carry the `Determinism` category. To inspect their records:

```sh
dotnet build SwarmingLilMen.sln -c Release
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release --no-build \
  --filter "FullyQualifiedName~GoldenTraceTests" \
  --logger "console;verbosity=detailed" \
  -- RunConfiguration.TreatNoTestsAsError=true
```

The detailed logger exposes output for successful cases as well as failures. `GOLDEN_RUNTIME`
records the actual framework, OS description, and process architecture. `GOLDEN_TRACE` prints
the expected and actual hash at every checkpoint. `GOLDEN_STATE` prints every actual component
word, and `GOLDEN_RNG` prints expected and actual sequence values.

A mismatch fails the test. `GOLDEN_DRIFT` identifies the first differing agent/component and
its two bit patterns, the maximum absolute component difference, and the maximum ULP distance
at that checkpoint. The trace test continues collecting checkpoints before asserting, so one
early difference does not hide its later magnitude. These measurements are diagnostics;
neither absolute differences nor ULP distances are used as an acceptance tolerance.

## One-ULP sensitivity

The automated witness increases `MaxForce` by one representable float value while keeping the
initial state identical:

| Engine | Original force bits | Changed force bits | First differing checkpoint on the reference environment |
| --- | --- | --- | --- |
| Legacy | `40400000` (`3`) | `40400001` | Tick `1` |
| Canonical | `3F400000` (`0.75`) | `3F400001` | Tick `1` |

Both changed trajectories differ at every subsequent recorded checkpoint. `GOLDEN_ONE_ULP`
logs their expected and actual hashes. During fixture authorship, an isolated copy of the
ordinary checkpoint tests was also run with those constants changed; both golden assertions
failed. The shared implementation and committed scenario constants remain unchanged.

## Investigating drift and changing a reference

First compare the actual runtime/OS/architecture records and the earliest differing component.
Check the seed, configuration, timestep, initial ordering, and code revision before attributing
a difference to platform math. Keep the complete expected/actual records and measured drift.

If a supported platform differs, record the concrete environment band and discrepancy magnitude
before introducing a separately named fixture for that band. Its values require independent
review and an actual run on that platform. Keep exact comparisons; a failing platform is never
handled by silently increasing a tolerance, skipping the test, or regenerating expected data
during test execution.

An intentional dynamics, PRNG, or math change requires a reviewed reference update tied to the
behavior change. Preserve the old data in version history, explain the first affected checkpoint,
and repeat both platform checks and the one-ULP witness. These tests provide no environment
variable, command-line switch, or default action that rewrites their fixtures.
