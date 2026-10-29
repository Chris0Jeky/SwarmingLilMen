# Determinism and golden traces

The replay contract is exact equality of ordered agent kinematics for a fixed experiment:
the same initial agents, configuration, seed, timestep sequence, implementation, and recorded
OS/architecture/.NET band. The committed golden traces make changes to that contract visible.
They compare a new run with fixed reference data, so a runtime or implementation change cannot
pass merely because two fresh runs changed together.

The Ubuntu and Windows CI matrix verifies each platform against its own reviewed reference.
Comparing the recorded states measures agreement or difference between those environments; two
passing platform-specific fixtures do not imply equal trajectories. The checks do not establish
bit-portable transcendental math or a permanent `System.Random` sequence across future .NET
releases. See the verified-state section of [PROJECT_STATUS.md](../PROJECT_STATUS.md) for the
latest platform evidence. The initial reference was authored and checked on Ubuntu. Windows CI
measured a different canonical trace, so the canonical fixture is explicitly selected by platform;
the legacy and RNG references are shared. The golden suite currently requires .NET 8, x64, and
Linux or Windows. Other environments need measured, reviewed references before entering this band.

## Named-stream migration (issue #26)

Canonical wander now uses the specified named PCG32 contract in [RANDOM_STREAMS.md](RANDOM_STREAMS.md).
This is the single deliberate reference update budgeted by epic #10. The eight initial agents,
settings, timestep and float dynamics are unchanged; the canonical wander sequences change.
Legacy trajectories and the legacy `Rng` sequence fixtures remain unchanged. Older canonical
wander-enabled manifests require their original binary/revision; mismatches fail replay hashes.

The first Linux difference from the previous canonical reference is agent 1's Y at tick 1:
`42BDFFE9` → `42BE0059`. The maximum absolute component difference at that tick is
`0.02755446219816804`. Subsequent trajectories diverge, including wrap seams; raw coordinate
differences across a seam are not geometric displacement. The new Linux tick-256 hash is
`F99E0084F72492206AA98A10C6AF6BAFE2B0C2C794144B2CF38924A1491C1BEE`.

[CI run 40811616741](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/40811616741), head
`fc45d58e9b75cab4cf832d98fde9b5fc42d3ccf1`, measured the Windows migration with the old exact
fixture still active. Both independent Windows jobs reported identical hashes and component words;
rehashing those words reproduced every checkpoint hash. Ubuntu .NET 8.0.33 matched the locally
measured Linux .NET 8.0.21 reference. The current tick-256 references are:

```text
Linux:   F99E0084F72492206AA98A10C6AF6BAFE2B0C2C794144B2CF38924A1491C1BEE
Windows: 5D5005CC773F66E87340BA972CE71F3BB9A08DA937249782658DA6333326E1C7
```

| Checkpoint | Maximum absolute component difference, Windows versus Linux | Maximum ULP difference |
| ---: | ---: | ---: |
| 0 | 0 | 0 |
| 1 | 0.0000000009313225746154785 | 1 |
| 16 | 0.0000016689300537109375 | 47 |
| 64 | 0.0000069141387939453125 | 145 |
| 256 | 0.00009918212890625 | 112 |

The first platform difference remains agent 0's `Vy`, `3C671F23` versus `3C671F24`, at tick 1.
The PCG word/adaptor/identity tests also run in the ordinary Ubuntu/Windows suite. The two
platform trajectory fixtures are exact; generator portability has not removed float-math drift.

## Original pre-migration platform difference

[CI run 40804377401](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/40804377401)
tested head `75e788e4f9a43499a307150373cd0c3b1b1fa750` with SDK 8.0.427 and .NET 8.0.33.
Ubuntu 24.04.5 x64 matched the original Ubuntu 24.04.3 / .NET 8.0.21 reference exactly.
Windows 10.0.26100 x64 matched the legacy trace and all three RNG sequences, but differed in
the canonical trace. Its merge-result and exact-head jobs independently reported identical
Windows hashes and component words. Rehashing those words also reproduced every logged hash.

| Canonical checkpoint | Maximum absolute component difference, Windows versus Linux | Maximum ULP difference |
| ---: | ---: | ---: |
| 0 | 0 | 0 |
| 1 | 0.0000000009313225746154785 | 1 |
| 16 | 0.0000016689300537109375 | 25 |
| 64 | 0.0000069141387939453125 | 96 |
| 256 | 0.00012969970703125 | 322 |

The first difference is agent 0's `Vy`: Linux bits `3C671F23`, Windows bits `3C671F24`.
This measured a platform-dependent trajectory; it did not isolate a particular math operation
as the cause. These pre-PCG traces remain in [PR #171](https://github.com/Chris0Jeky/SwarmingLilMen/pull/171)
and version history. Their tick-256 hashes were respectively:

```text
Linux:   A8395F4CE8A49D82D3F95AA14A8E7E7C2C0400E8729205F97D09EF93B85BE0C8
Windows: 920102DA689C45E1939E82DCDC14A3F6EA65B966866A5C7E3A2E471D5EE7CFA4
```

**The enforced contract is exact replay within the recorded platform/runtime band, not exact
canonical replay between Windows and Linux.** No tolerance was widened, and neither platform
is skipped. A new runtime patch can still fail the reference and requires investigation.

## Committed experiment

The shared legacy reference below is not a general cross-platform equality claim. The standalone
runner's 400-agent, 600-tick, seed-7 workload uses uniform seeded positions and headings rather
than these eight fixed initial agents. In [CI run 40806403511](https://github.com/Chris0Jeky/SwarmingLilMen/actions/runs/40806403511)
both its legacy and canonical final hashes differed between Windows and Linux, while every
same-platform repeat and manifest replay produced an identical CSV and state hash. Thus a passing
fixed legacy fixture does not extend the guarantee to arbitrary initial states or workloads.

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

| Engine | Original force bits | Changed force bits | First differing checkpoint, Ubuntu | First differing checkpoint, Windows |
| --- | --- | --- | --- | --- |
| Legacy | `40400000` (`3`) | `40400001` | Tick `1` | Tick `1` |
| Canonical | `3F400000` (`0.75`) | `3F400001` | Tick `1` | Tick `16` |

Each changed trajectory differs at every recorded checkpoint from its first difference onward.
Windows canonical's tick-1 result remains equal to its own baseline, then differs at tick 16;
the witness compares against the selected platform fixture. `GOLDEN_ONE_ULP` logs expected and
actual hashes. During fixture authorship on Ubuntu, an isolated copy of the
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
