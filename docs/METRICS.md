# Analysis metrics contract

`SwarmSim.Core.Metrics` provides explicit, reusable probes over a finite 2D kinematic snapshot.
It separates physical analysis from the neighbors selected by a steering algorithm. A registry
selects columns in a caller-supplied order; invariant CSV and JSONL writers use that same schema.

The headless CLI consumes this registry through `--metrics` and `--list-metrics`, with reusable
engine capture and schema-versioned manifests. It preserves the original three default columns
and verifies the complete selected deterministic CSV during replay. See
[headless experiments](HEADLESS_EXPERIMENTS.md) for selection, compatibility, operational metrics
and the separately measured 50,000-agent sparse pipeline.

## A reusable sampling plan

```csharp
using SwarmSim.Core.Metrics;

var registry = new MetricRegistry();
registry.Register(new MotionMetricProbe());

// Explicit opt-in: this probe uses all pairs, not a bounded steering query.
var exactNeighbors = new ExactNearestNeighborProbe(capacity: 2048);
registry.Register(exactNeighbors);

string[] requested = ["population", "meanSpeed", "nearestNeighborP95"];
MetricPlan plan = registry.CreatePlan(requested);
double[] values = new double[plan.ChannelCount];
KinematicSample[] population = new KinematicSample[2048];

// Populate the caller-owned buffer with live members before each sample.
// liveCount identifies the meaningful prefix; retain stable agent ordering.
int liveCount = 0;
var frame = new MetricFrame(population.AsSpan(0, liveCount), MetricDomain.Toroidal(1920, 1080));
plan.Sample(frame, values);

using var output = new StreamWriter("metrics.csv");
var csv = new MetricCsvWriter(output, plan); // Writes tick plus requested columns.
csv.WriteRow(0, values);
```

Construct probes, plans, population storage, result storage, and writers once, then reuse them.
The frame borrows its input span and validates that every position and velocity component is
finite. The caller chooses the analysis population, including whether to exclude dead agents or
filter a group. No engine-specific lifecycle or group semantics are hidden in the metric layer.
Keep that population stable while a sample runs, and create a fresh frame after updating it.

Channel names are case-sensitive. Unknown or duplicate requested names are errors. Registrations
are validated completely before they are published; channel collisions do not leave partially
registered probes behind. Names contain only ASCII letters/digits, `_`, or `.`, and `tick` is
reserved. A plan snapshots the requested order, so mutating the caller's name array afterward does
not change its schema. Each needed probe runs once per sample, even when several of its channels
are selected. Selecting one channel still computes that probe's complete set of outputs.

Plans, probes, and writers are reusable but not thread-safe. Independent concurrent consumers
need separate instances. `Sample` writes into a caller-owned result span; an undersized span is
rejected before probes run.

## Motion and centroid channels

`MotionMetricProbe` computes these five channels in O(N) work with double-precision accumulation.
The input components remain floats, matching the engines' state representation.
Selecting any of its channels currently computes all five, including the circular-centroid
trigonometry for a periodic frame; selecting only the original three columns does not skip that work.

| Channel | Definition |
| --- | --- |
| `population` | Number N of supplied members |
| `meanSpeed` | Arithmetic mean of `sqrt(vx² + vy²)`; zero when N is zero |
| `polarization` | Magnitude of `sum(unitVelocity) / N`, clamped to `[0, 1]` for rounding; zero when N is zero |
| `centroidX` | Arithmetic mean x in Euclidean space; circular mean on a periodic x axis |
| `centroidY` | Arithmetic mean y in Euclidean space; circular mean on a periodic y axis |

**Stationary members remain in the polarization denominator.** They contribute a zero unit
heading. For example, velocities `(3, 0)`, `(0, 4)`, and `(0, 0)` have mean speed `7/3` and
polarization `sqrt(2)/3`. The denominator is the full supplied population, not just moving members.
Every nonzero finite velocity is normalized; there is no implicit small-speed cutoff.

`MetricDomain.Euclidean` leaves coordinates unwrapped. `MetricDomain.Toroidal(width, height)`
requires positive finite extents and reduces each coordinate to a centered signed remainder in
`[-extent/2, extent/2]` before periodic analysis. Keeping tiny negative coordinates near zero avoids
losing their significant bits by first adding a much larger extent. For an axis of length L, the
circular mean uses angles `2*pi*remainder/L`, then
the angle of the summed sine/cosine vector. It maps that angle back into `[0, L)`.

A circular mean is undefined when the mean resultant magnitude is at most `1e-12`. That axis
reports NaN as a missing value. This applies independently to each axis: two agents at x values
`0` and `5` in a width-10 world have no distinguished x centroid, while an identical y coordinate
can still have a valid y centroid. An empty population has undefined x and y centroids. Floating
rounding can place a seam-centered result just below the world extent; it denotes the same
periodic location as a result near zero.

## Exact nearest-neighbor analysis

**Analysis neighborhoods are not steering neighborhoods.** `ExactNearestNeighborProbe` sees all
members in the supplied frame. It never consults a simulation spatial index, sense radius,
field of view, `MaxNeighbors`, group identity, insertion-order candidate buffer, or truncation flag.

For each member i it computes the minimum distance to every other member j. Only the same index
is excluded. Distinct coincident members therefore have a valid nearest-neighbor distance of zero.
Euclidean distances use raw coordinate differences; periodic distances use the shortest wrapped
displacement on each axis. Arithmetic uses doubles so subtracting and squaring extreme finite
float coordinates does not overflow float intermediates.

| Channel | Definition |
| --- | --- |
| `nearestNeighborCount` | N distances when N is at least two; otherwise zero |
| `nearestNeighborMean` | Arithmetic mean of those distances |
| `nearestNeighborMin` | Minimum distance |
| `nearestNeighborP50` | Median using the interpolation rule below |
| `nearestNeighborP95` | 95th percentile using the same rule |
| `nearestNeighborMax` | Maximum distance |

For a sorted distribution `d[0..n-1]` and quantile q, let `r = (n-1)*q`, `a = floor(r)`, and
`b = min(a+1, n-1)`. The percentile is `d[a] + (r-a)*(d[b]-d[a])`. Thus `[2, 2, 3, 4]` has
mean `2.75`, p50 `2.5`, and p95 `3.85`. Fewer than two members produce an empty distribution and
missing values for all five distance statistics, rather than a misleading zero distance.

`NearestDistances` exposes the last distribution in agent order, and `SortedNearestDistances`
exposes it in sorted order. These borrowed spans are overwritten by the next sample. Copy them
only when a caller needs to retain a distribution across samples.

### Cost and capacity

The implementation evaluates every unordered pair: **O(N²) distance work plus O(N log N)
sorting**. Selecting any nearest-neighbor channel opts into that cost. It is intended for
affordable populations or sparse offline analysis samples, not an implied real-time 50,000-agent
path. No sampling, approximation, hidden neighbor cap, or truncated fallback is used.

The configured capacity allocates four reusable double arrays, approximately `32 * capacity`
bytes plus array overhead. A population above capacity throws before sampling; the probe never
resizes itself or silently drops members. Capacity zero is allowed for an empty-only consumer.

Tests compare hand-computed distributions while the same positions are held in grid indexes with
cell sizes 1 and 4 and in the naive index. Their deliberately one-entry steering query buffers
truncate, while analysis still reports every member's exact nearest-neighbor distance.

## Foundation-specific and operational probes

The registry and frame have no boids channel names. A new foundation adds a separate
`IMetricProbe` implementation and registers it; it does not edit `World`, `CanonicalWorld`, or
the registry kernel.

### Canonical boids instrumentation

Register `new BoidsRuleMetricProbe(world.Instrumentation)` when those observations are needed.
The channels are:

| Channel | Definition |
| --- | --- |
| `boids.separationMagnitudeMean` | Mean of the recorded per-agent separation magnitudes |
| `boids.alignmentMagnitudeMean` | Mean of the recorded per-agent alignment magnitudes |
| `boids.cohesionMagnitudeMean` | Mean of the recorded per-agent cohesion magnitudes |
| `boids.steeringMagnitudeMean` | Mean of the square roots of recorded composed-steering squared magnitudes |
| `boids.truncatedQueryFraction` | Fraction of recorded per-agent steering queries marked truncated |

These channels preserve the existing instrumentation's meaning. They do not reinterpret a
steering-neighbor count or distance as an exact spatial statistic. Sample after the corresponding
completed canonical step and before the next step overwrites instrumentation. The probe checks
that instrumentation and frame counts agree; the caller remains responsible for associating them
with the same tick. Empty instrumentation/frame pairs report zero means and fraction.

### Runtime costs

`RuntimeCostMetricProbe` exposes `runtime.wallMilliseconds` and `runtime.allocatedBytes`. The
caller records an explicitly chosen interval with `Record(wallMilliseconds, allocatedBytes)`;
the probe never reads a clock or allocation counter itself. Both values must be non-negative,
and duration must be finite. Before the first record they are missing; afterward reads retain the
last supplied interval until it is replaced.

These channels declare `IsDeterministic == false`, and any plan containing them does too. Keep
them separate from deterministic replay defaults. Define the measurement boundary in the
experiment record: simulation-only and simulation-plus-observer/I/O timings are different
quantities. A `GC.GetAllocatedBytesForCurrentThread()` delta covers only that thread and must be
measured on the same thread before and after the interval. Outputs use the contract's double
representation; very large integer byte counts have the usual double precision limit.

## CSV and JSONL output

Both writers emit `tick` first, then only the requested names in their supplied order. They use
invariant round-trip numeric formatting and explicit LF line endings. They do not own, close,
or select the encoding of the supplied `TextWriter`; use a UTF-8 `StreamWriter` for files.

NaN means missing/undefined and becomes an empty CSV field or JSON `null`. Infinity is rejected,
as are negative ticks and rows whose value count differs from the schema. Numeric validity is
checked before writing a row. Formatting uses stack buffers and span-based writes rather than
allocating one string per number. The chosen underlying writer can still have its own allocation
or I/O behavior.

## Verification scope and headless integration

The 50,000-agent allocation test measures **only** `population`, `meanSpeed`, `polarization`,
`centroidX`, and `centroidY` in a Euclidean domain. It includes fresh-frame validation, sampling,
and CSV/JSONL formatting to `TextWriter.Null` for 1,000 samples after warm-up, using current-thread
allocation deltas. It observes zero allocated bytes. A separate small-population test verifies
reuse of exact-neighbor scratch across 1,000 samples.

This library-only evidence does not exercise 50,000-agent quadratic nearest-neighbor analysis,
engine snapshot copying or real disk writers, and is not a throughput claim. The separate CLI
consumer now uses the same registry and CSV sink with a reusable capture buffer. It supports
`--metrics` selection and discovery, schema 1 compatibility, and schema 2 exact replay across
the complete selected deterministic CSV. Operational runtime channels are classified explicitly
and disable exact replay. See [HEADLESS_EXPERIMENTS.md](HEADLESS_EXPERIMENTS.md) for its separate
50k sparse full-pipeline verification and precise allocation boundaries.

Runtime/platform and ordered input snapshots remain part of reproducibility; these formulas do
not create a cross-platform bitwise guarantee. JSONL remains available through the core writer
API; the headless run-directory contract uses CSV.
