# Gray–Scott reaction diffusion

`ReactionDiffusionWorld` is a field-only foundation. Each grid cell contains two concentrations,
`CellState(U, V)`, and the world contains **zero kinematic agents**. It reuses the same typed
double buffer, ordered phase schedule, named initialization streams, metric probe interface
and model-state hash writer as the agent foundations.

The governing equations follow equation (2) in John E. Pearson's primary paper,
[Complex Patterns in a Simple System](https://arxiv.org/pdf/patt-sol/9304003)
([Science, 1993, DOI](https://doi.org/10.1126/science.261.5118.189)). The implementation adds an
explicit reaction-rate multiplier `R`; its default of one gives the cited equations. Setting
`R = F = k = 0` isolates diffusion for numerical controls.

```text
∂U/∂t = Du ∇²U - R U V² + F (1 - U)
∂V/∂t = Dv ∇²V + R U V² - (F + k) V
```

This is a small numerical simulation with a specified discretization. It makes no claim of
biological validation, reproduction of the paper's parameter study, a measured Turing
instability or parity with the browser morphogenesis demo. The source paper motivates the
equations and a localized perturbation; the defaults and numerical guards below are this
implementation's contract.

## Field and update contract

Cells are stored in row-major order: index `y * Columns + x`. Grid spacing is one in both
directions. The boundary is periodic on both axes. The five-point discrete Laplacian is:

```text
lap(A)[x,y] = A[x-1,y] + A[x+1,y] + A[x,y-1] + A[x,y+1] - 4 A[x,y]
```

Every directed link counts. For a two-column grid, left and right refer to the same cell and
contribute twice. For a one-column grid they both refer to self. The same rule applies to rows.
This makes the definition valid for rectangular and thin grids without changing the stencil.

One fixed timestep applies synchronous forward Euler: every derivative reads the complete
previous field, and every next cell is written before the generation is committed. State uses
binary32 storage; neighborhood sums, reaction terms, coefficients and integration use binary64
intermediates. Casts back to binary32 occur once per concentration per cell per tick.
Finite nonnegative candidates use ordinary binary32 rounding, then the stored results are checked
for finiteness. A double just above `float.MaxValue` may round back to that finite value; a true
conversion overflow rejects the proposed field. No saturation is applied.

| Scheduled phase | Work |
| --- | --- |
| `stability` | Check the current maximum V against the explicit reaction/depletion condition. |
| `reaction-diffusion` | Compute every next U/V value from the old field and validate each candidate. |
| `commit` | Swap the complete generations and increment `TickCount`. |

The update is algebraically grouped into nonnegative terms. With `S_U` and `S_V` denoting the
four-neighbor sums, the actual evaluation is:

```text
q = R V²
U(next) = U [1 - dt (4 Du + F + q)] + dt Du S_U + dt F
V(next) = V [1 - dt (4 Dv + F + k)] + dt Dv S_V + dt U q
```

That grouping avoids subtractive negative roundoff at an admitted coefficient boundary. It is
the same forward-Euler equation, not a split reaction/diffusion solver. Neither field is
silently clamped, including to `[0,1]`; finite concentrations greater than one are supported
when the timestep conditions hold.

## Timestep and failure behavior

Rates must be finite and nonnegative. Timestep must be finite and positive. The settings
validator requires:

```text
dt (4 Du + F) <= 1
dt (4 Dv + F + k) <= 1
```

At construction and before every step, the world additionally requires:

```text
dt (4 Du + F + R max(V)²) <= 1
```

These are conservative nonnegative-coefficient conditions for this explicit discretization;
they include the usual diffusion restriction for a unit-spaced two-dimensional five-point
stencil. They do not establish convergence or accuracy of the nonlinear solution. A small
enough initial timestep can become insufficient as V grows, and the runtime guard detects
that case. A smaller timestep requires a new run/configuration; settings are immutable.

Invalid initial shapes or concentrations are rejected. A failed runtime guard or a nonfinite,
negative or unrepresentable proposed concentration throws before commit. The last complete
`Cells` generation, `TickCount` and state fingerprint remain unchanged. The partially written
next buffer, if any, is private scratch and never becomes visible. There is no automatic
timestep reduction, hidden clipping or checkpoint-recovery protocol.

## Settings and seeded patch

| Setting | Default | Meaning |
| --- | ---: | --- |
| `Columns` | 64 | Grid columns |
| `Rows` | 64 | Grid rows |
| `DiffusionU` | 0.16 | U diffusion coefficient with unit grid spacing |
| `DiffusionV` | 0.08 | V diffusion coefficient with unit grid spacing |
| `ReactionRate` | 1 | Multiplier of the U V² reaction |
| `Feed` | 0.035 | Supply of U and removal of both species |
| `Kill` | 0.065 | Additional V removal |
| `FixedDeltaTime` | 0.5 | Simulation time per tick |
| `PatchFraction` | 0.2 | Fraction of each dimension occupied by the centered patch |
| `PatchNoise` | 0.02 | Additive uniform perturbation half-width in the patch |
| `Seed` | 123456 | Named initialization-stream seed |

The grid has at most 1,048,576 cells. `PatchFraction` lies in `[0,1]`; zero initializes the exact
uniform equilibrium U = 1, V = 0. A positive fraction creates a rectangle of
`ceil(Columns * PatchFraction)` by `ceil(Rows * PatchFraction)` cells, centered using integer
division. The patch starts around U = 0.5 and V = 0.25. Background cells remain exactly U = 1,
V = 0.

The `reaction-diffusion.patch` stream supplies two draws per patch cell in row-major order,
first U and then V. Each draw adds `(2u - 1) * PatchNoise` for uniform `u` in `[0,1)`.
`PatchNoise` is restricted to `[0,0.25]`, so initialization stays nonnegative. Results are
rounded to binary32. No stream survives initialization, and stepping draws no random values.
Changing only reaction or diffusion rates preserves the seeded initial field.

The explicit-state constructor accepts exactly `Columns * Rows` finite nonnegative cells,
copies them and applies the same initial depletion guard. It bypasses the patch initializer.

## Experiment observations

`AgentCount` is zero. `CopyKinematics` returns zero and leaves its destination untouched.
Cells do not masquerade as boids, particles or stationary kinematic agents. A field view can
read `Cells`, `Columns` and `Rows` directly; display normalization must not modify the field.
Agent motion, neighbor distance and polarization are not field observables.

The reusable metric probe reads the world directly and exposes six channels:

| Channel | Definition |
| --- | --- |
| `reactionDiffusion.uMean` | Ordered double-precision sum of U divided by cell count |
| `reactionDiffusion.uMin` | Minimum U |
| `reactionDiffusion.uMax` | Maximum U |
| `reactionDiffusion.vMean` | Ordered double-precision sum of V divided by cell count |
| `reactionDiffusion.vMin` | Minimum V |
| `reactionDiffusion.vMax` | Maximum V |

Sampling allocates no managed memory and does not advance the model. The null-world probe
supports channel discovery and refuses sampling. Pure diffusion with `ReactionRate = Feed =
Kill = 0` conserves each field total up to binary32 storage rounding; the full fed/removed
reaction model has no such separate conservation law.

The committed [base settings](../scenarios/reaction-diffusion/base.json) are suitable for small
32 × 32 or 64 × 64 experiments. The core also supports direct construction:

```csharp
var world = new ReactionDiffusionWorld(new ReactionDiffusionSettings
{
    Columns = 32,
    Rows = 32,
    Seed = 7
});
for (int tick = 0; tick < 256; tick++) world.Step();
ReadOnlySpan<CellState> field = world.Cells;
```

## Run, replay and compare

```sh
dotnet run --project SwarmSim.Cli -c Release -- --engine reaction-diffusion --describe-engine --json
dotnet run --project SwarmSim.Cli -c Release -- --engine reaction-diffusion --config scenarios/reaction-diffusion/base.json --ticks 1000 --seed 7 --visual --out runs
dotnet run --project SwarmSim.Cli -c Release -- --sweep scenarios/reaction-diffusion/feed-sweep.json --visual --out runs
dotnet run --project SwarmSim.Cli -c Release -- --manifest runs/run-<id>/manifest.json --visual --out replays
```

Population defaults to zero and an explicit `--agents` must also be zero. Grid size comes from
the native configuration. Default metrics are U mean and V mean; motion/neighbor metrics are
unavailable. Every completed run requires schema 3 with the full U/V field fingerprint.
The heatmap shows V concentration on one fixed 0-to-1 display scale across all frames; raw
values above one are retained in state and metrics. See [field observations](FIELD_OBSERVATIONS.md).

The six-cell feed sweep uses two fixed seeds and three feed values while retaining the same
seeded initial field for each comparison. It is exploratory; no morphology classification or
phase boundary is inferred. The shared [gallery](../scenarios/gallery.json) includes a small
seeded-patch run and exact U=1,V=0 equilibrium control. Its broad concentration bands are
finite implementation checks, separate from the analytical model tests below.

## Versioned state fingerprint

`reaction-diffusion-state-v1` uses `StateHashWriter` and covers the entire retained native model
state. After the writer's domain and profile prefix, fields appear in this exact order:

1. `Columns`, `Rows` as signed 32-bit integers.
2. `DiffusionU`, `DiffusionV`, `ReactionRate`, `Feed`, `Kill`, `FixedDeltaTime`, `PatchFraction`,
   `PatchNoise` as exact binary32 bits.
3. `Seed` as an unsigned 32-bit integer.
4. `TickCount` as a signed 64-bit integer and `CellCount` as a signed 32-bit integer.
5. U then V for every cell in row-major order, as exact binary32 bits.

Every primitive is little-endian. There is no retained RNG cursor. The alternate buffer is
overwritten before use and excluded. A change to field order, meaning or coverage requires a
new profile version. The fingerprint is a comparison witness, not a serialized checkpoint.
Exact repeatability is scoped to the recorded implementation, platform and runtime.

`ModelStateProfileMatchesAnIndependentBinaryFixtureAtTwoAnalyticalStates` pins an independently
encoded fixture: a 2 × 1 grid, Du = 1/8, Dv = 1/16, R = 1/2, F = 1/32, k = 1/16, dt = 1/4,
patch fraction/noise zero, seed 17, and initial cells `(1, 1/4), (1/2, 1/8)`. All arithmetic in
the checked step is exactly representable in binary32. Python `struct.pack` and `hashlib`
independently give:

| Tick | Model-state SHA-256 |
| ---: | --- |
| 0 | `A4A270232A94C83A45CE9464F9D5C0765532F421D24183FD1D471E07044DE161` |
| 1 | `0F7CD155FAEA9CD944AE3FF7FFF9BC79A9B12D2627A7A1AA48C3BD0C3E068EDE` |

This fixture verifies encoding and one analytical step; it is not a measured long-trajectory
reference for a pattern regime.

## Verification scope

The model tests cover the exact uniform equilibrium, corner impulses crossing both periodic
seams, directed-link behavior for thin grids, a separate Laplacian/reaction Euler oracle,
pure-diffusion conservation, reaction transfer, feed/kill effects, values above one, seeded
initialization, settings round trips, repeated fields, explicit input ownership, field-only
capture, ordered state hashes, field metrics, invalid inputs, dynamic guard failure, bounded
32 × 32 and 64 × 64 runs, maximum-value rounding, stored overflow without partial publication,
and warmed allocation behavior.

No grid-refinement study, timestep-convergence study, long-horizon pattern classification,
biological interpretation or large-grid performance target is claimed. Those require their
own experiments; a finite heatmap alone cannot establish them.
