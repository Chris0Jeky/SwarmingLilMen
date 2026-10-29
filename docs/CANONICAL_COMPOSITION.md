# Canonical steering composition

`CanonicalWorld` supports an explicit policy/resolver/stage composition. Its ordinary two-argument
constructor selects a built-in pipeline from `CanonicalWorldSettings.Mode`. Missing mode means
**Enhanced**, preserving the established behavior. The three-argument constructor installs only
the supplied pipeline; it does not append default rules or hidden velocity stages.

This is the bounded **force-policy** API from issue #27. It does not yet implement heading/velocity
replacement, Vicsek update ordering, a general per-agent state registry, or reward/reset APIs.

## Built-in modes and force semantics

| Composition | Force resolution | Velocity stages | Neighbor observation |
| --- | --- | --- | --- |
| `Enhanced` (default) | One shared `MaxForce` budget, in stable priority order; separation reserves the remainder when it spends | Shaped avoidance, turn-rate direction, target-speed normalization with priority speed droop | Configured FOV and bounded query |
| `ClassicReynolds` | Clamp each of separation, alignment and cohesion to `MaxForce`, then sum; **no shared post-clamp** | Target-speed normalization only | Requires `FieldOfView = 360`; bounded query remains explicit |
| `CanonicalPipeline.Empty()` (injected API) | No policy output | None | No neighbor query |

The default's total-force invariant remains unchanged, including its small float-rounding band.
Classic deliberately gives `MaxForce` a **per-contribution** meaning: the sum can exceed it, up to
the sum of the three clamped magnitudes. This is a distinct model choice, not a relaxation of the
enhanced resolver's budget. `RuleInstrumentation.SteeringMagnitudesSquared` records the composed
force before velocity stages in either mode; compare it with the invariant for the selected
resolver. A custom injected resolver defines its own composition semantics.

The classic factory installs exactly three policies and a target-speed stage. Whiskers, wander,
priority hysteresis/hold/ramp, speed droop, shaped avoidance, and turn-rate limiting are absent.
Nonzero values in their serialized settings do not activate those missing stages. The target-speed
stage normalizes a nonzero integrated velocity; a zero vector remains zero.

An empty pipeline preserves the stored velocity bit for bit, including a negative-zero component,
and moves by `position += velocity * dt` with the world's normal periodic wrapping. Ingest still
uses the existing `TryAddBoid` velocity normalization. Empty composition does not run trigonometric
heading normalization after ingest.

## Public construction and headless selection

```csharp
using SwarmSim.Core.Canonical;

var settings = CanonicalWorldSettings.ClassicReynolds();
var index = new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight);
var classic = new CanonicalWorld(settings, index);

var emptySettings = new CanonicalWorldSettings();
var empty = new CanonicalWorld(emptySettings,
    new GridSpatialIndex(emptySettings.SenseRadius, emptySettings.WorldWidth, emptySettings.WorldHeight),
    CanonicalPipeline.Empty());
```

An explicitly injected pipeline takes precedence over the settings factory choice. To reproduce
a built-in choice through configuration and the CLI, use the mode field instead:

```sh
dotnet run --project SwarmSim.Cli -c Release -- --engine canonical --config configs/canonical-classic.json --agents 400 --ticks 600 --seed 7 --out runs/
```

The [classic recipe](../configs/canonical-classic.json) sets full-circle perception and a candidate
capacity of 1024. It otherwise inherits canonical defaults. Native JSON must supply both
`"Mode": "ClassicReynolds"` and `"FieldOfView": 360`; selecting classic with another FOV is an error.
The public settings factory supplies both. A named scenario can set another FOV, so a later
classic file must replace it. Undefined enum values and unknown mode strings fail validation.

`Mode` belongs in a sweep's base configuration. Sweep v1 axes remain numeric scalar settings;
the string-valued mode is not an axis. The renderer's `SimConfig` bridge continues to select
Enhanced and does not accept native canonical mode files. No new renderer control is claimed here.

### Old configuration and manifest compatibility

Enhanced mode is omitted when serializing settings. Consequently, default resolved configuration
JSON keeps its pre-composition field set. Classic explicitly serializes `"Mode": "ClassicReynolds"`.
The strict resolver recognizes this one optional field while continuing to reject unknown names.

For both schema 1 and schema 2 manifests, a missing canonical `Mode` is explicitly pinned to
Enhanced. It is the single optional-field exception in the complete-configuration check; removing
an existing required field such as `WanderRate` still rejects replay. Other replay contracts,
metric ordering, complete CSV hashes, and final kinematic hashes remain unchanged. Custom injected
pipelines are an in-process API and are not serialized as arbitrary policy registries.

## Bounded observations and tagged output

An `ILocalSteeringPolicy` receives a borrowed `LocalObservation` and scalar `LocalPolicyContext`.
The observation contains self kinematics and a `ReadOnlySpan<NeighborView>` for only the neighbors
that survived the world's candidate cap and FOV filter. It has no world array or global neighbor
indices. Each neighbor provides:

- Minimum-image relative position in both directions.
- Relative velocity and the original neighbor velocity.
- Group, FOV weight, and distance.

The two displacement directions preserve half-world tie and signed-zero behavior. Original
neighbor velocity preserves alignment's float arithmetic; subtracting self velocity and adding it
back can lose bits. The ported three rules retain the legacy expressions and accumulation order,
and tests compare the local and compatibility paths bit for bit across seams and degenerate cases.

Observation is bounded, not a guarantee that every geometric neighbor was included. The existing
minimum candidate capacity of four and lowest-index truncation in ascending order remain. FOV filtering occurs
after the bounded candidate query. `QueryTruncated` and the CLI's `boids.truncatedQueryFraction`
report that loss. For an all-neighbor comparison, reserve at least the population's candidate
capacity and verify no truncation; a small cap is not a nearest-K model.

A policy declares immutable `PolicyMetadata` and `MaxIntents`, then calls `IntentWriter.AddForce`.
Every output carries its kind, vector payload, policy ID, priority, weight, and arbitration metadata.
The writer stamps that metadata; a policy cannot replace its borrowed buffer or forge another
policy's identity. Capacity overflow and nonfinite payloads fail explicitly instead of silently
dropping output. The resolver also rejects nonfinite weighted forces. Bounds, metadata and policy
ownership are established during registration; normal steps reuse the same observation and intent
scratch buffers.

Only the `Force` intent kind exists in this version. Policies do not emit unsupported heading,
velocity, or position intents that the world might silently ignore.

```csharp
sealed class UpwardForce : ILocalSteeringPolicy
{
    public PolicyMetadata Metadata { get; } = new("upward", priority: 50, weight: 1f);
    public int MaxIntents => 1;

    public void Emit(in LocalObservation observation, in LocalPolicyContext context,
        ref IntentWriter intents) => intents.AddForce(new Vec2(0f, 0.1f));
}
```

`world.AddPolicy(new UpwardForce())` adds a real contribution. `Metadata.Weight` scales emitted
force and is separate from the boid rules' historical desired-speed weights. Weights of zero are
not a rule-enable contract: a classic rule with desired-speed weight zero can still return
`-self.Velocity`. Use `world.SetPolicyEnabled("separation", false)` to stop that policy's emission.
IDs are case-sensitive; duplicate or unknown IDs fail explicitly. Disabled classic contributions
are zero while the other independently resolved contributions remain unchanged on the same input.

## Explicit enhanced order and state ownership

Lower priority numbers execute first. Equal-priority policies retain registration order.
The built-in composition is:

| Order | Component | Effect |
| --- | --- | --- |
| 0 | `whisker` policy | Predictive lateral avoidance spends from the shared remainder |
| 10 | `separation-priority` stage | Updates per-agent hysteresis, hold timer and blend |
| 20 | `separation` policy | Uses the current boost; any applied force reserves the remaining budget |
| 30 | `alignment` policy | Multiplies desired force by `1 - blend * 0.7` before arbitration |
| 40 | `cohesion` policy | Uses the same attenuation and remainder |
| 50 by default | Added policies / trusted legacy adapters | Output participates in arbitration |
| 100 | `wander` policy | Draws only when enabled, positive-strength and remaining force permits |
| After force integration | `shaped-avoidance`, `turn-rate`, `target-speed` stages | Preserve the enhanced velocity update order |

The priority stage runs before a policy with an equal stage priority. Policies normally still
execute when the force budget is exhausted, so observations and custom policy state do not depend
on an implicit skipped call. `RequiresRemainingForce` is an explicit exception used by whisker and
wander. Multiple intents from one policy retain emission order. Per-role instrumentation sums
the applied magnitudes of all intents carrying that role; the original rules each emit one force.

Pipeline, policy, resolver, and stage instances belong to one world. Reusing a mutable component
in a second world is rejected; create fresh instances, including for the built-in factories.
Registration/addition may allocate. Stepping does not allocate ownership tables or policy buffers.
World mutation, policy changes, and recursive `Step` calls are rejected during a step. These APIs
are synchronous and are not thread-safe.

Built-in wander retains compact 16-byte PCG state per agent in a world-owned array and the
`canonical.wander` stream identity. An explicitly injected positive-strength wander policy also
initializes that state, including when added after boids exist or when the settings factory's
`WanderStrength` is zero. Its calls do not share a mutable random generator with other policies.

The public local context currently has **no stable self identity or consumer-scoped RNG handle**.
It supports stateless policies and world-owned policy state, but it does not yet offer arbitrary
per-agent state/noise. External per-agent stochastic policies need the explicitly trusted legacy
adapter or a future bounded identity/noise contract. Vicsek work must add that concrete seam and
its update-order tests; exposing the private wander generator would break consumer independence.

### Trusted legacy compatibility

`IRule.Compute` remains source/binary compatible. `CanonicalWorld.AddRule` deliberately wraps it
in `TrustedLegacyRuleAdapter`; that adapter retains full-world and global-index access. It cannot
honestly be called a bounded local policy. New `ILocalSteeringPolicy` implementations are never
silently routed through it. An added legacy rule's returned force now participates in arbitration;
the previous fourth-and-later output-discard behavior was a bug and is covered by a regression.

Policy failures are fail-fast, not transactional simulation rollback. The active boid buffer and
tick are committed only after a successful step, but policy-private state, diagnostic buffers or
RNG draws may already have advanced when an exception is raised. Discard/reconstruct that world
before retrying an experiment after a policy contract error.

## Verification and cost

The committed tests cover default exact golden traces, bounded observations, original/local rule
bit equivalence, stable ordering, named disabling, writer limits/ownership, reentrant mutation,
empty straight-line motion including signed zero, independent classic force sums, absent enhanced
stages, configuration reachability, and both manifest schemas. Allocation checks measure 128 agents
for 1,000 ticks after 100 warm ticks in each of Enhanced, ClassicReynolds and Empty. They exclude
construction, registration, and the allocating compatibility telemetry snapshot.

An actual pre-composition binary produced a 400-agent, 600-tick balanced run with all ten selected
deterministic motion/boids channels. The composed implementation replayed the complete CSV and
final state hash exactly. A pre-change default three-channel run represented as schema 1 also
replayed exactly. These are measured Linux/.NET results, not a promise of cross-platform identity;
the repository's separate Linux and Windows golden gates remain required.

The controlled BenchmarkDotNet harness is [tools/CompositionCost](../tools/CompositionCost/Program.cs).
It forwards the existing `CanonicalWorldStepBenchmarks.Setup` and complete `Step` unchanged,
without inheriting its default job attribute and accidentally adding a second job:

```sh
dotnet run --project tools/CompositionCost/CompositionCost.csproj -c Release -- --filter '*' --warmupCount 3 --iterationCount 5 --launchCount 1 --invocationCount 64 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/composition
```

For before/after comparison, use this same harness against both source checkouts and the same SDK.
The fixed invocation count keeps the number of evolving-world steps equal. The measured scope is
enhanced physics including grid rebuild, with telemetry capture and rendering off, at 1k/10k/50k
agents in the unchanged 1920×1080 scenario. It does not measure full renderer FPS or classic-mode
throughput. See [project status](../PROJECT_STATUS.md) for the current integrated measurements and limits.

### Measured composition cost (2026-10-29)

The before source is [the reviewed compact-RNG commit](https://github.com/Chris0Jeky/SwarmingLilMen/commit/c00ad1e9620e03447fd1139d03220fb77201f08a).
All 54 core/benchmark/build-setting files used by the before checkout matched that commit's Git
blobs. The before and after harness `Program.cs` files are byte-identical. The after source uses
the composition in this change; its `CanonicalWorld.cs` SHA-256 is
`871835b9f2acfabb1aeb3164636e29cf21009bb773a9da7e7a2cc101c5867881`.
The earlier three-size measurement preceded only the final `AddRule` entry guard; that change
does not execute in the benchmark's setup or step.

Both runs used BenchmarkDotNet 0.15.6, SDK 8.0.415/runtime 8.0.21, x64 RyuJIT,
Concurrent Workstation GC, and Ubuntu 24.04.3 on an AMD EPYC 9V74 2.60 GHz cloud host.
BenchmarkDotNet reported one CPU and nine logical/physical cores. Raising process priority was
denied in both runs. This is one host observation with evolving worlds, not a performance SLA.

The initial matched matrix used the command above, one launch, three warmup iterations, five
measurement iterations and 64 fixed invocations per iteration. Values below are milliseconds
per complete tick. Error is BenchmarkDotNet's half-width of the 99.9% confidence interval.

| Agents | Before mean ± error (ms) | After mean ± error (ms) | Observed mean delta | Allocated before / after |
| --- | ---: | ---: | ---: | ---: |
| 1,000 | 0.478583 ± 0.078777 | 0.653493 ± 0.4915 | +36.55% | 0 B / 0 B |
| 10,000 | 7.4782 ± 0.69179 | 8.1232 ± 2.4928 | +8.63% | 0 B / 0 B |
| 50,000 | 114.8636 ± 20.16435 | 95.8358 ± 16.2676 | −16.57% | 0 B / 0 B |

The short 1k iterations triggered BenchmarkDotNet's minimum-iteration warning. A targeted
confirmation increased only the invocation count to 1024 while keeping the workload, seed,
SDK/runtime, three warmups, five measurements and one launch fixed:

```sh
dotnet run --project tools/CompositionCost/CompositionCost.csproj -c Release -- --filter '*AgentCount: 1000)' --warmupCount 3 --iterationCount 5 --launchCount 1 --invocationCount 1024 --unrollFactor 1 --artifacts BenchmarkDotNet.Artifacts/composition-1k
```

The measured environment selected the same SDK executable with `--cli <dotnet-path>` in both
runs; the after command also explicitly supplied `--runtimes net8.0`. Both logs identify the same
.NET 8.0.21 JIT/GC environment. Each selected exactly one benchmark, with the same number of
evolving-world steps. The confirmation no longer produced the short-iteration warning.

| 1k confirmation | Before | After |
| --- | ---: | ---: |
| Mean (ms/tick) | 0.481998 | 0.561671 |
| 99.9% interval (ms/tick) | [0.415449, 0.548546] | [0.484608, 0.638734] |
| Standard deviation (ms/tick) | 0.010298 | 0.020013 |
| Retained measurements | 4 of 5 | 5 of 5 |
| Managed allocation | 0 B/tick | 0 B/tick |

The 1k result is a **+16.53% measured cost**. The intervals overlap, but that does not establish
no regression. BenchmarkDotNet removed the before run's highest sample; retaining all five gives
approximately +14.74% instead. The repeated positive small-population delta remains performance
follow-up work. The single 50k decrease is likewise not a general speedup claim. These zero-allocation
rows do not explain or erase the earlier unrelated 10k 5 B/op observation tracked in issue #20.

For auditability, these are all five raw `WorkloadActual` iteration averages, including samples
that BenchmarkDotNet later excluded. Units are milliseconds per tick; each entry averages the
fixed invocation count, before the tiny measured harness-overhead subtraction.

| Run | Agents | Invocations | Five raw iteration averages (ms/tick) |
| --- | ---: | ---: | --- |
| Before | 1,000 | 64 | 0.4638632, 0.4836704, 0.5986189, 0.4922606, 0.4745563 |
| After | 1,000 | 64 | 0.7858386, 0.5790682, 0.5314794, 0.7977553, 0.5733495 |
| Before | 10,000 | 64 | 7.3510, 7.5511, 7.2833, 7.7417, 7.4636 |
| After | 10,000 | 64 | 7.5569, 8.3128, 8.2144, 8.4087, 9.4009 |
| Before | 50,000 | 64 | 118.0845, 106.9246, 112.0534, 118.4709, 118.7848 |
| After | 50,000 | 64 | 99.8900, 98.0784, 90.6821, 98.6339, 91.8945 |
| Before confirmation | 1,000 | 1024 | 0.4840260, 0.4668875, 0.4883572, 0.4887282, 0.5195632 |
| After confirmation | 1,000 | 1024 | 0.5348591, 0.5509982, 0.5605648, 0.5843682, 0.5775736 |

Every case's MemoryDiagnoser GC record reported zero collections and zero allocated bytes in its
measured allocation interval. Construction, policy registration, telemetry capture and rendering
are outside that interval.
