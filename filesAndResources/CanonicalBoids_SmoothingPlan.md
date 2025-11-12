# Canonical Boids – Smoothing & Flocking Plan

Audience: engineers iterating on `SwarmSim.Core.Canonical` to move from collision-safe behavior toward natural flocking with smooth trajectories and clear diagnostics.

Status summary (today)
- Collision avoidance: strong. Predictive whiskers + separation priority prevents overlaps well.
- Problem: when separation priority triggers, agents can “ping‑pong” (bounce‑ball feel). The snap‑away heading plus full acceleration budget produces aggressive, discrete turns and group turbulence.
- Visibility: HUD shows neighbor counts, distances, rule magnitudes, and a global priority flag; overlay shows FOV cone and neighbors. We lack explicit whisker visualization and a speed projection readout.

Design goals
- Preserve robust collision avoidance but make turns continuous, anticipatory, and flock-like (cohesion/alignment recover smoothly after clearing space).
- Keep constant-speed envelope (TargetSpeed) and determinism.
- Provide headless, data-only visibility to reason about flock health and turning smoothness.

Root causes of “bouncy” behavior
1) Discrete priority switch: once inside the close-range threshold, separation becomes exclusive for the tick and snaps heading directly away → large angular jumps and oscillations when neighbors hover near the threshold.
2) Full-budget impulses: using the entire `MaxForce` instantaneously and normalizing to TargetSpeed produces high curvature with no angular rate limit.
3) Tangential steering deficit: separation is primarily “away” (radial); insufficient lateral (tangential) bias leads to head-on reflections rather than graceful lane changes.
4) No temporal hysteresis: agents can enter/exit priority rapidly around the threshold → on/off flicker.

Smoothing strategy (high level)
- Introduce an angular-rate-limited steering stage: cap heading change per tick (max turn rate) regardless of linear acceleration decisions.
- Replace “snap-away” with prioritized but blended steering: shape separation into lateral+away components and blend using smooth ramps as distance crosses thresholds (no discontinuity).
- Add temporal hysteresis on the priority state and budget ramps to avoid flicker.
- Allow small speed droop while in hard-avoid mode to further damp overshoot; recover TargetSpeed smoothly after clearance.

Proposed changes (detailed)
1) Angular-rate limiter (max turn rate)
   - Compute desired heading `h_des = normalize(velocity + steering)`.
   - Clamp the signed angular difference `Δθ = clamp(Δθ, ±ω_max*dt)` before rotating the current heading.
   - New velocity `v' = rotate(v, Δθ)` and re-normalize to TargetSpeed.
   - Adds a single parameter `MaxTurnRateDegPerSec` (typ. 180–540°/s depending on dt and speed).

2) Separation shaping (away + lateral)
   - Decompose the nearest-neighbor vector into forward and lateral components.
   - Emphasize lateral deflection early (outside hard threshold) to “shoulder past” rather than bounce.
   - Inside the hard threshold, ramp the away component up with a smooth curve, e.g., smoothstep:
     `w = smoothstep(r_hard, r_soft, d)`; `steer = w * lateral + (1-w) * away`.
   - Keep per-tick acceleration budget but avoid instant full impulses.

3) Temporal hysteresis + grace period
   - Enter priority at `d <= r_enter` and exit only after `d >= r_exit` for `holdTicks` (with `r_exit > r_enter`).
   - Budget ramp: when entering priority, increase separation weight/boost over `τ_in` ticks; when exiting, decrease over `τ_out` ticks.
   - Prevents flicker and ping‑pong.

4) Optional micro speed droop under hard separation
   - While in priority, reduce speed envelope to `TargetSpeed*(1 - speedDroop)` (e.g., 2–5%), then recover with exponential smoothing after exit.
   - Further damps overshoot while keeping constant-speed semantics outside priority.

5) Predictive whiskers – make visible and tunable
   - Draw whisker capsule (lookahead and lateral radius) and color neighbors that fall inside.
   - Expose `WhiskerTimeHorizon` and `WhiskerWeight` in the UI; log counts of neighbors in the capsule to correlate with early deflections.

6) Projection readout and flock metrics
   - Compute mean flock heading `H = normalize(sum(v_i))` per frame.
   - For selected agents (or tracked five), report `proj = dot(v_i, H)` and lateral component `lat = |v_i - proj*H|`.
   - Show min/mean/max of `proj` to prove no one exceeds TargetSpeed and diagnose “diagonal drift.”
   - Report flock polarization `|mean(normalize(v_i))|`, milling (circulation), and average nearest-neighbor distance for flock quality checks.

7) Perception snapshot extensions (data-only mode)
   - Per-agent: nearest distance, nearest angle (relative to forward), number inside whisker capsule.
   - World snapshot: distribution histograms (buckets) for nearest distance and neighbor count.
   - Optional: rolling windows to track convergence of polarization and spacing.

Implementation plan (phased)

Phase A – Turning and separation smoothing
- Completed: `MaxTurnRateDegPerSec` limiter, priority hysteresis, and lateral/away blending with ramped separation boosts. Hard snaps were replaced with smooth steering, and the optional speed droop is already enabled when the priority state is active.

Phase B – Diagnostics and overlays
- Completed: Whisker capsule overlay, projection readout, and console logging of the new perception snapshot (distance stats plus `SeparationPriorityTriggered`). Whisker hits and mean-heading projection now appear on-screen.

Phase C – Perception data & tests
- Extend `PerceptionSnapshot` with per-agent nearest angle and whisker counts.
- Add tests for angular-rate limiter (max heading change per step), priority hysteresis (enter/exit only on the right thresholds), and whisker counts vs. brute‐force check.

Phase D – Flock‑quality tuning
- Reduce alignment/cohesion influence while in or near priority state (soft gating, not hard off).
- Weight alignment by angular error (agents that already point with group get less “pull”).
- Cohesion falloff by distance to the centroid (avoid too-strong pull when agents are very close).

Suggested starting parameter ranges
- `MaxTurnRateDegPerSec`: 180–540
- `PriorityEnterFactor`: 0.33; `PriorityExitFactor`: 0.40
- `PriorityHoldTicks`: 5–12 (at 60–120 Hz)
- `PriorityRampInTicks`: 3–8; `PriorityRampOutTicks`: 6–12
- `SpeedDroopInPriority`: 0.0–0.05
- `WhiskerTimeHorizon`: 0.3–0.6 s; `WhiskerWeight`: 1.0–2.0

Data‑driven evaluation (no rendering)
- At the canonical step loop, record snapshots for 60–120 s of sim time and compute:
  - Polarization vs. time, nearest-neighbor distance vs. time (expect upward jump after enabling smoothing).
  - Fraction of agents in priority per tick and average dwell time.
  - Projection stats (prove no TargetSpeed exceedances, and that lateral residuals decay after avoidance).

Risks & mitigations
- Too much smoothing → slow reactions: keep whiskers predictive and enforce angular-rate cap that still allows ~90° in 0.2–0.4 s.
- Hysteresis that traps agents in priority: use reasonable exit margin and hold ticks; verify with logging.
- Excess speed droop → lagging flock: keep droop small (≤ 5%) and only while in hard separation.

Next concrete tasks (for the upcoming implementation pass)
1) Implement angular-rate limiter and priority hysteresis (enter/exit/hold + ramps).
2) Replace snap-away with shaped separation (away+lateral; smoothstep blend); remove forced velocity snap.
3) Add whisker capsule overlay and console counts; expose time horizon and weight in the UI.
4) Add projection readout and polarization metrics to HUD/console.
5) Extend PerceptionSnapshot with nearest angle and whisker counts; add tests for limiter + hysteresis.
6) Tune defaults; provide two presets: “Smooth Avoid” (turning focus) and “Flock Cohesion” (alignment/cohesion emphasis) for quick A/B.

Appendix – Smoothstep and parameterization
- `smoothstep(a, b, x) = t^2 * (3 - 2t)` where `t = clamp((x-a)/(b-a), 0, 1)`.
- Use `r_soft` = 0.6*SeparationRadius, `r_hard` = PriorityEnterFactor*SenseRadius; blend lateral→away as d decreases from `r_soft` to `r_hard`.
