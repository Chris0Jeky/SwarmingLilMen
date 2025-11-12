# high-level model (what we’re building)

* **Agent model:** each boid has `pos`, `vel` (constant speed magnitude), and computes a *steering acceleration* each tick from rules (separation, alignment, cohesion). This is classic Reynolds steering, designed to compose linearly and clamp safely. ([Red3D][1])
* **Perception:** each boid sees neighbors within a **radius R** and **field-of-view (FOV)**. Birds often have very wide FOV; a 270° arc is biologically plausible (real birds can be ~300°). We’ll use a 270° half-angle of **135°** for visibility checks. ([U of T Computer Science][2])
* **Update loop:** use a **fixed timestep** (e.g., `dt = 1/60s`) for determinism and stable physics. ([Gaffer On Games][3])
* **Neighbor search:** start naïve (O(n²)), then swap in a **uniform grid / spatial hash** with equivalence tests to keep behavior identical. ([redblobgames.com][4])
* **Determinism for tests:** inject a **seeded PRNG** so “random direction jitter” is reproducible in tests. ([Microsoft Learn][5])

---

# incremental TDD roadmap

> Use fixed-timestep simulation tests (no rendering). For each milestone, write tests first, then code.

## milestone 0 — core scaffolding

**Goal:** deterministic, minimal sim core.

* **Types:** `Vec2`, `Boid`, `World`, `Rng(seed)`, `IRule`, `ISpatialIndex`.
* **Loop:** `step(dt)` uses fixed `dt`; clamp `max_speed`, `max_force`; integrate with semi-implicit Euler.
* **Tests (unit):**

  * `Vec2` ops (normalize, dot, limiting).
  * Integration keeps `|vel| == target_speed` (within epsilon) after applying clamped steering.
  * Fixed-timestep invariants (same seed → same trajectory). ([Gaffer On Games][3])

## milestone 1 — single boid motion (+ optional jitter)

**Goal:** “one entity moves in a straight line at constant speed; optional small direction noise.”

* **Logic:** without steering rules, velocity heading stays constant; optionally add **heading jitter** each tick from PRNG, limited by `max_turn_rate`.
* **Tests (unit):**

  * With jitter off → straight line (`pos` collinear with initial heading).
  * With jitter on + seed → positions match snapshot (golden master). ([Microsoft Learn][5])

## milestone 2 — perception (radius + FOV)

**Goal:** neighbors = within `R` AND inside FOV cone.

* **Algorithm:** neighbor vector `d = other.pos - self.pos`; in-radius if `|d| ≤ R`; in-FOV if
  `dot( forward, normalize(d) ) ≥ cos(FOV/2)`. For **270°**, use `cos(135°) ≈ −0.7071`.
* **Tests (unit):**

  * Points exactly on radius are included (choose inclusive policy).
  * FOV edge case: on boundary (dot == threshold) counted.
  * “Behind me” excluded.
* **Debug hooks:** toggle **perception circle** and **FOV wedge** per selected boid.

> note: forward-weighted perception is realistic; you can later weight neighbors by angle if you want. ([U of T Computer Science][2])

## milestone 3 — separation

**Goal:** steer away from too-close neighbors (collision avoidance).

* **Algorithm:** for neighbors within `d_min` (usually `d_min < R`), accumulate
  `sep += (self.pos - n.pos) / |self.pos - n.pos|^2` → normalize → scale by `w_sep` and clamp to `max_force`.
* **Tests (behavioral):**

  * **Head-on two-boid:** distance after N steps is **increasing**.
  * **Overtaking:** faster follower veers to avoid entering `d_min`.

(Separation priority > alignment/cohesion; we’ll enforce via weights or “early exit” if very close.) ([Red3D][1])

## milestone 4 — alignment

**Goal:** match heading of visible neighbors.

* **Algorithm:** `avg_vel = average(neighbor.vel)`; `align = steer_towards(normalize(avg_vel)) * w_align`.
* **Tests (behavioral):**

  * Three boids with different headings → headings converge (variance ↓ over time).
  * No neighbors → align = 0.

## milestone 5 — cohesion

**Goal:** move toward neighbors’ center of mass.

* **Algorithm:** `center = average(neighbor.pos)`; `cohesion = steer_towards(center - self.pos) * w_coh`.
* **Tests:**

  * Two separated clusters drift toward each other (with alignment off).
  * One boid + distant single neighbor → approaches until within R.

> alignment + cohesion + separation are the canonical “boids trio.” ([Red3D][6])

## milestone 6 — rule composition & stability

**Goal:** combine forces safely; keep speed constant.

* **Algorithm:** `steer = sep * w_sep + align * w_align + cohesion * w_coh`; clamp to `max_force`; update vel; then **re-normalize to constant speed** (your requirement).
* **Tests:**

  * Speed magnitude stays within `[target_speed ± ε]`.
  * With only one rule enabled, behavior matches earlier milestones.
  * With all rules: no boid gets stuck (|vel| never ≈ 0), no NaNs.

## milestone 7 — instrumentation & UX toggles

**Goal:** make development observable.

* **Overlays:**

  * **Perception radius** circle; **FOV arc**;
  * **Neighbor links**: draw lines to neighbors; color by rule: red=separation, blue=alignment, green=cohesion;
  * Show **steering vector** arrow.
* **Controls:** checkboxes for rules, sliders for `R`, `d_min`, `w_*`, `max_force`, FOV.
* **Tests (snapshot/visual or asserts on neighbor counts):** toggling rules changes which neighbors contribute.

## milestone 8 — boundaries & worlds

**Goal:** predictable edges so tests don’t flake.

* Start with **toroidal wrap**; later add walls/obstacles (obstacle avoidance is a standard steering behavior).
* **Tests:** position wraps correctly; with walls, boids reflect/steer before collision. ([Red3D][1])

## milestone 9 — performance swap: spatial hashing

**Goal:** same behavior, faster.

* Implement `GridIndex(cell = R)`; when querying neighbors, check current + adjacent cells (9 in 2D).
* **Equivalence tests:** for random scenes, **hash index** neighbor set == **brute force** (within floating-point tolerance).
* **Perf test:** time per step vs n; expect ~linear for large n. ([redblobgames.com][4])

## milestone 10 — property tests & metrics

**Goal:** confidence at scale.

* **Metrics:** polarization (|mean normalized heading|), average nearest-neighbor distance, clustering coefficient. Expect sensible ranges with typical weights.
* **Property tests:** with separation on and dense init, *median* pairwise distance should rise over first T seconds; with only alignment, heading variance decreases. (Use seeded PRNG). ([Microsoft Learn][5])

---

# algorithms (concise formulas)

* **FOV check:** `visible(n) := (|d| ≤ R) ∧ (dot(forward, d̂) ≥ cos(FOV/2))`. For FOV=270°, threshold ≈ **−0.70710678**. ([U of T Computer Science][2])
* **Seek/steer helper (Reynolds):**
  `desired = normalize(target) * target_speed`
  `steer = clamp(desired − vel, max_force)` (then integrate & re-normalize speed). ([Red3D][1])
* **Rule forces:**

  * **Separation:** inverse-square sum away from close neighbors (only `|d| ≤ d_min`).
  * **Alignment:** steer towards `normalize(avg_vel)`.
  * **Cohesion:** steer towards `center − pos`.
  * **Combine:** `steer = w_sep*sep + w_align*align + w_coh*cohesion` (often `w_sep` slightly larger to avoid clumping). ([natureofcode.com][7])

---

# implementation plan (structure & tooling)

**project layout (language-agnostic)**

```
/core         Vec2, rng(seed), config, constants
/sim          Boid, World(step), integrator, time (fixed dt)
/rules        IRule, Separation, Alignment, Cohesion
/spatial      ISpatialIndex, NaiveIndex, GridIndex
/debug        Overlay, Toggles, Inspector
/tests        unit/, behavior/, property/, perf/
```

**time and determinism**

* Fixed `dt` accumulator loop; never use variable-`dt` physics. Seed the PRNG and inject it (DI) into systems that need randomness. ([Gaffer On Games][3])

**testing stack suggestions**

* **Python:** `pytest` + `hypothesis` (property tests).
* **JS/TS:** `vitest`/`jest` + fast-check.
* **Java:** JUnit + QuickTheories.
* **C++:** Catch2 + RapidCheck.
  Focus most tests on pure functions (`neighbors()`, `rule()`), then a handful of end-to-end “scenario” tests per milestone.

**scenario tests you can copy**

1. **Straight-line single boid** (no rules).
2. **Two-boid head-on separation** (distance ↑).
3. **Triplet heading convergence** (alignment).
4. **Join the party**: lone boid enters flock (cohesion pulls it in).
5. **Perception off-axis**: neighbor behind is ignored until it rotates into FOV.
6. **Spatial index parity**: hash vs brute-force neighbor sets equal for 100 random frames.

**typical starting params** (tune with sliders)

* `target_speed`: 1.0 units/tick
* `R`: 6–10 units; `d_min`: 2–3 units
* `w_sep`: 1.5–2.0, `w_align`: 1.0, `w_coh`: 1.0
* `max_force`: 0.05–0.2 *target_speed* per tick
* `FOV`: 270° (threshold −0.7071)

> for deeper background and friendly derivations of these three rules, see *The Nature of Code*’s boids chapter. ([natureofcode.com][7])

---

# quality-of-life & future-proofing

* **Debug UI:** rule toggles, radius/FOV sliders, neighbor-line switch, selected-boid inspector (counts per rule, net steer magnitude).
* **Config snapshots:** load/save JSON of weights/params to reproduce behaviors for bug reports.
* **Performance:** once stable, enable the spatial hash (cell size ≈ `R` or `2*d_min`); check only 9 cells in 2D. ([matthias-research.github.io][8])
* **Extensions (later):** obstacle avoidance, goal seeking, wander, leader following—all in Reynolds’ steering catalog and pluggable via `IRule`. ([Red3D][1])

---

## why this works

This mirrors Reynolds’ original decomposition (simple local rules → emergent flocking) and his later *steering behaviors* design (small, composable forces), while enforcing a fixed-timestep deterministic loop for testability. The wide, forward-biased perception you want (270°) is consistent with biological vision ranges; we wire it directly via dot-product thresholds. For scale, we keep neighbor search O(n) with spatial hashing and lock it down with parity tests. ([Red3D][6])
