# Canonical behavioral scenario coverage

[Issue #41](https://github.com/Chris0Jeky/SwarmingLilMen/issues/41) remains **partially complete**.
The scenarios below exercise the current canonical pipeline over fixed logical ticks. They do
not establish named rule isolation, classic-Reynolds composition, cross-engine parity, or a
general-purpose kernel. Those contracts depend on [#27](https://github.com/Chris0Jeky/SwarmingLilMen/issues/27)
and [#28](https://github.com/Chris0Jeky/SwarmingLilMen/issues/28).

The executable inventory is [CanonicalScenarioTests](../SwarmSim.Tests/CanonicalScenarioTests.cs).
Its eight test methods produce eleven cases: three isolated headings and two cluster placements
are separate cases. Initial states are explicit; every case disables wander. The suite depends
on no particular RNG sequence, wall clock, frame rate, or renderer.

## Acceptance inventory

| Roadmap acceptance | Scenario method | Horizon | Observable assertion |
| --- | --- | --- | --- |
| [Milestone 3: head-on separation](archive/NewImplementation.md#milestone-3--separation) | `Separation_HeadOnClosestApproachStaysAbovePassThrough` | 16 × 0.25 s = 4 s | Two initially approaching boids begin 7 units apart; closest approach stays above 4.8 and final distance exceeds 13.5. The separation-disabled witness fails these bounds despite the remaining shaped avoidance. |
| [Milestone 4: three headings converge](archive/NewImplementation.md#milestone-4--alignment) | `Alignment_TripletReducesCircularHeadingVariance` | 300 × 1/60 s = 5 s | Circular heading variance, defined as one minus mean resultant length, falls by at least 90% from a nontrivial initial spread. |
| [Milestone 4: no-neighbor response](archive/NewImplementation.md#milestone-4--alignment) | `Alignment_IsolatedBoidKeepsVelocity` | 600 × 1/60 s = 10 s | Every tick has exactly zero recorded steering and preserves the initial velocity within an absolute component tolerance of 1e-4 at speed 2. East, north and oblique headings cover both axis-aligned and trigonometric normalization paths. |
| [Milestone 5: approach one neighbor](archive/NewImplementation.md#milestone-5--cohesion) | `Cohesion_LoneBoidMovesCloserToNeighbor` | 180 × 1/60 s = 3 s | An initially parallel pair within sense range closes at least 20% of its initial 18-unit separation. |
| [Milestone 5: cluster contraction](archive/NewImplementation.md#milestone-5--cohesion) | `Cohesion_ScatteredClusterRadiusContracts` | 360 × 1/60 s = 6 s | A same-heading five-boid rank contracts its maximum radius about the centroid by at least 25%. |
| [Milestone 5: two clusters approach](archive/NewImplementation.md#milestone-5--cohesion) | `Cohesion_TwoCompactClustersApproachAcrossTheShortestImage` | 180 × 1/60 s = 3 s | Two three-boid clusters begin 30 units apart and close at least 20% of that center distance. Interior and wrap-seam placements use the same minimum-image distance; group centers are computed in a local unwrapped image. |
| [Milestone 6: finite, mobile combined behavior](archive/NewImplementation.md#milestone-6--rule-composition--stability), current priority mode only | `CombinedRules_ExplicitInteractingFlockStaysWithinPrioritySpeedBounds` | 400 × 1/60 s | A fixed 16-boid layout exercises all three recorded rule contributions and intentional speed droop. Every boid remains finite, with speed between `TargetSpeed * (1 - SeparationSpeedDroop)` and `TargetSpeed`, allowing 1e-4 rounding tolerance. |
| Default priority-mode speed semantics, distinct from classic constant speed | `PriorityMode_DefaultDroopHasAFloorAndRecoversWhenThePairSeparates` | 360 × 1/60 s = 6 s | An outgoing close pair engages priority, reaches the default 3% reduction, remains above its speed floor, then leaves the exit threshold and returns to target speed. |

The convergence margins are finite-scenario acceptance bounds, not universal convergence rates.
The isolated-velocity tolerance accounts for repeated `atan2`/`sin`/`cos` normalization; it does
not relax the exact zero-steering requirement. The dense scene checks the configured default
priority semantics rather than an arbitrary near-zero threshold: with target speed 2 and droop
0.03, the speed floor is 1.94. A user-selected droop of 1 permits zero speed and is outside that
default non-stuck claim.

## Current model boundaries

- **Literal faster/slower overtaking is still unproved.** `CanonicalWorld.TryAddBoid` and
  `SetVelocity` normalize each supplied velocity to the same world-wide `TargetSpeed`, and each
  step renormalizes to the priority-adjusted speed. Supplying unequal velocity magnitudes does
  not create a persistent faster follower and slower leader. This suite does not relabel an
  oblique head-on pass as the missing overtaking acceptance. A supported experiment definition
  must be settled before that case can be claimed.
- **A neighbor must be sensed before cohesion can react.** The pair begins within `SenseRadius`
  and outside `SeparationRadius`. The roadmap wording about approaching a neighbor until it is
  within `R` cannot mean attraction to a neighbor initially outside perception with no other
  observation or steering source.
- **A weight of zero is not a named off switch.** In the current rule implementations, scaling
  the desired velocity to zero can still yield `-self.Velocity`. Whisker/shaped avoidance also
  remains part of the inline pipeline. These tests do not edit `_rules`, assign positional test
  rules, or claim that zero weights establish isolated rule composition. In particular, the
  two-cluster cases prove integrated cohesion, not the roadmap's alignment-off isolation.
- **Classic constant speed remains a separate acceptance.** The default priority mode
  intentionally reduces speed near neighbors. The complete milestone-6 isolation and classic
  composition cases must use #27's named contract when it exists; weakening default droop or
  changing trajectories to imitate that future mode would be a behavior change.

No production implementation or golden fixture changes accompany this coverage expansion.
The existing six-case inventory already covered several issue-41 behaviors; this work strengthens
their observable margins, extends no-neighbor coverage, adds the two-cluster placements, and makes
default speed semantics explicit. The issue remains open for the listed model/composition
acceptance. Publication requires the Ubuntu and Windows gates on the reviewed revision.

## Run the scenarios

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release \
  --filter "FullyQualifiedName~CanonicalScenarioTests" \
  --logger "console;verbosity=detailed" \
  -- RunConfiguration.TreatNoTestsAsError=true
```

These cases run in the ordinary non-performance gate. Detailed output records the distances,
heading variance and droop floor used by the assertions. Neither a passing scenario suite nor its
mutation witnesses establish visual renderer acceptance or cross-platform bitwise equality.

## Local evidence and sensitivity

The initial Linux x64 / .NET 8.0.21 Release run observed:

| Quantity | Observed value |
| --- | --- |
| Head-on closest / final distance | 5.102513 / 14.051277 |
| Triplet circular variance, initial → final | 0.3333333 → 0.001920402 |
| Pair distance, initial → final | 18 → 14.237244 |
| Rank radius after six seconds | 16 → 10.921145 |
| Cluster-center distance, interior | 30 → 23.79292 |
| Cluster-center distance, across seam | 30 → 23.792923 |
| Default droop minimum speed | 1.9399999, expected floor 1.94 |

An exploratory quarter-radius requirement after four seconds failed: the rank reached 12.787768
from 16, a 20.1% contraction. The recorded six-second case retains the 25% objective and gives the
boids time to turn and translate. There was no prior published 25%-in-four-seconds contract and
no production defect or fix is inferred from that calibration.

Six deliberate mutations were applied one at a time in a separate checkout, with fresh Release
compilation and structured test results for each. They were never part of the implementation
patch. All eleven scenarios passed before mutation and again after restoring the original source.

| Isolated mutation | Failed cases | Required behavior detected |
| --- | ---: | --- |
| Replace separation's returned steering with zero | 2 | Head-on clearance/final separation; the interacting scene also detects its missing contribution |
| Replace alignment's returned steering with zero | 2 | Triplet heading convergence; missing contribution in the interacting scene |
| Replace cohesion's returned steering with zero | 5 | Pair, rank, both cluster placements, and the interacting scene |
| Add alignment steering when the neighbor list is empty | 3 | All three no-neighbor heading cases |
| Remove priority speed droop | 2 | Default speed reduction/recovery and the interacting scene's non-vacuous droop witness |
| Force integrated velocity to zero | 11 | Finite-horizon motion and the non-stuck speed floor |

The no-neighbor case is a negative-response contract: disabling alignment is expected to leave
it unchanged. Its appropriate sabotage therefore introduces an unwanted empty-neighborhood force.
The convergence cases, by contrast, must fail when their corresponding positive contribution is
removed. This distinction avoids claiming that an isolated boid needs a neighbor-driven rule to
maintain its velocity.
