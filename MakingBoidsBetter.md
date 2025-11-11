> **Status (2025-11-12)**: Steering refactor completed; this document remains as a design reference.

Great prompt, and yes—setting friction to 1 “unblocked” things, but it’s a symptom fix, not the root. Here’s how your write-up stacks up against the consensus, plus a concrete path to make your boids kernel solid and extensible.

# 1) What the literature actually does (and why)

* **Three steering rules**: separation, alignment, cohesion. That’s the bedrock. ([red3d.com][1])
* **Steering, not raw forces**: most canonical implementations compute a **desired velocity** for each behavior, then apply **steering = desired − currentVelocity**, and **limit steering by maxForce**; final velocity is limited by **maxSpeed**. This is the “steering behaviors” pattern from Reynolds and is the dominant recipe in teaching materials and OpenSteer-like libs. ([red3d.com][2])
* **Constant-speed variants**: a very common simplification runs boids at (nearly) constant speed (turning changes heading, not speed). If you do this, explicit “friction” is unnecessary; the **speed clamp** provides the velocity envelope. ([pup-assets.imgix.net][3])
* **Time stepping**: use a **fixed timestep** for stability and determinism; if you render at a different rate, accumulate time and step physics in constant dt, optionally render with interpolation. ([Gaffer On Games][4])
* **Integrator**: use **semi-implicit (symplectic) Euler** (update v then x). It’s as cheap as explicit Euler but more stable for game physics. ([Gaffer On Games][5])

# 2) Where your explanation is strong, and where to tweak

Your doc matches consensus on: fixed dt; summing behaviors with weights; clamping maxSpeed; tuning radii; the “friction-force equilibrium” intuition. All good.

**Key tweaks to make it robust:**

1. **Prefer steering formulation over summing raw forces**
   Compute for each rule a **desired velocity** with a clear target magnitude (often `maxSpeed` scaled by a weight), then **steer = clamp(desired − v, maxForce)**. This avoids the “huge force → clamp → still underpowered after dt and damping” pathologies you hit earlier. ([natureofcode.com][6])

2. **Use 1/d separation weighting (not 1/d²) + caps**
   Classic separation weights by **inverse distance**; 1/r² explodes at close range and creates numerical headaches, forcing you to crank clamps. Cap per-neighbor contribution and the final steering. ([golancourses.net][7])

3. **Choose one of two speed models explicitly**

   * **Constant-speed boids**: set friction = 1, always clamp speed to `maxSpeed`. Very common and simple (and matches your working state). ([pup-assets.imgix.net][3])
   * **Damped boids**: use friction (0.95–0.99 per frame) and reasonable **maxForce** so equilibrium speeds make sense. This feels “birdy” but requires careful tuning. (Still use steering caps.) ([natureofcode.com][6])

4. **Fix your timestep + semi-implicit Euler**
   Keep `dt` constant (e.g., 1/60), accumulate real time, and step N times per frame as needed; do **v += steer*dt; x += v*dt**. This eliminates frame-rate coupling and improves stability. ([Gaffer On Games][4])

5. **Combine behaviors with either weighted blend or prioritized allocation**
   Start with a **weighted blend** (simple, what you’re doing). If you see “indecision” (forces cancel), switch to **prioritized acceleration**: spend a capped acceleration budget on Separation first, then Alignment, then Cohesion. It’s a known improvement. ([cs.auckland.ac.nz][8])

6. **Limit perception**
   Classic boids react only to neighbors within a **radius** and **field of view** (cone). This reduces noise and cost and produces nicer flocks. ([red3d.com][1])

# 3) A small, “canonical” kernel you can adopt

**Per boid per step** (semi-implicit Euler):

1. Gather neighbors within `senseRadius` (and FOV).
2. Compute three **desired velocities**:

   * **Separation**: sum of normalized “away” vectors, weight by `1/d`, normalize, *scale to* `maxSpeed * sepGain`. ([golancourses.net][7])
   * **Alignment**: average neighbor velocity, normalize, *scale to* `maxSpeed * alignGain`. ([natureofcode.com][6])
   * **Cohesion**: centerOfMass − myPos → normalize, *scale to* `maxSpeed * cohGain`. ([natureofcode.com][6])
3. For each: `steerX = clamp(desiredX − v, maxForceX)`.
4. Sum steers (or allocate by priority).
5. **v += steer * dt;** if using damping, **v *= friction**; **v = clampMag(v, maxSpeed)**; **x += v * dt**. ([Gaffer On Games][5])

**Good starter values** (constant-speed style):
`maxSpeed ~ 8–15`, `maxForce ~ 0.5–2.0`, `sepGain ~ 1.2–2.0`, `alignGain ~ 0.8–1.5`, `cohGain ~ 0.3–1.0`, `separationRadius ~ 0.3*senseRadius`, FOV ~ 270°. (These are typical ranges drawn from steering tutorials and OpenSteer examples—tune to taste.) ([opensteer.sourceforge.net][9])

# 4) Why friction = 1 “worked” for you

With friction < 1, your earlier **1/r² separation + heavy clamping** meant the effective acceleration after `dt` and damping was too small to escape clumps. Setting friction to 1 removed the continuous velocity bleed, so agents could finally build/retain speed. The **canonical** fix is not “friction=1”, it’s **(a)** steering formulation + caps that don’t explode, **(b)** sensible maxForce, **(c)** fixed dt + semi-implicit integrator, **(d)** 1/d separation. ([natureofcode.com][6])

# 5) Quick diagnostics to “lock it in”

* **Metrics to log** each second: mean speed / maxSpeed, % at speed cap, mean neighbor count, % with zero neighbors (loners), and average steering magnitude vs maxForce. If most boids are pegged at speed cap and steering is constantly clamped, weights/forces are too high; if speeds sit near 0 with friction<1, forces are too low or damping too strong. (This traces directly to the steering + clamp model. ) ([natureofcode.com][6])
* **Two-boid test**: place two boids 1–2 radii apart, see clean separation without oscillation; check that per-neighbor separation steer is capped. ([golancourses.net][7])
* **Fixed-dt harness**: run at 30, 60, 120 Hz physics with render unlocked—flock structure should be invariant modulo interpolation. ([Gaffer On Games][4])

# 6) Common extensions you can add safely later

* **Obstacle avoidance / walls** using additional steering behaviors (seek/arrive, avoid, path follow). ([red3d.com][2])
* **Prioritized allocation** (Separation > Avoidance > Alignment > Cohesion). ([cs.auckland.ac.nz][8])
* **Vision cones, leader following, wander with memory** (Reynolds’ wander uses correlated noise). ([natureofcodeunity.com][10])

---

## TL;DR recommended fixes for your code

1. Refactor rule outputs to **desired velocities** → **steer = desired − v**, **clamp to maxForce**; then **v += steer*dt; x += v*dt; clamp v to maxSpeed**. ([natureofcode.com][6])
2. Change separation weighting to **1/d** and **cap per-neighbor** and total steer. ([golancourses.net][7])
3. Keep **friction optional**:

   * Constant-speed mode: friction=1 (simple, canonical). ([pup-assets.imgix.net][3])
   * Damped mode: friction ~ 0.96–0.99 with appropriate **maxForce** (don’t rely on massive raw forces). ([natureofcode.com][6])
4. Enforce **fixed timestep** and **semi-implicit Euler**. ([Gaffer On Games][4])
5. Start with **weighted blend**, switch to **prioritized** if you see cancellation. ([cs.auckland.ac.nz][8])

Adopting that kernel will make the sim behave “by the book,” so you can safely tinker with weights/radii and layer in new behaviors without mysterious stalls or explosions.

[1]: https://www.red3d.com/cwr/boids/?utm_source=chatgpt.com "Boids (Flocks, Herds, and Schools: a Distributed Behavioral Model)"
[2]: https://www.red3d.com/cwr/papers/1999/gdc99steer.pdf?utm_source=chatgpt.com "Steering Behaviors For Autonomous Characters"
[3]: https://pup-assets.imgix.net/onix/images/9780691224138/9780691224138.pdf?utm_source=chatgpt.com "Brief Contents"
[4]: https://gafferongames.com/post/fix_your_timestep/?utm_source=chatgpt.com "Fix Your Timestep! | Gaffer On Games"
[5]: https://www.gafferongames.com/post/integration_basics/?utm_source=chatgpt.com "Integration Basics | Gaffer On Games"
[6]: https://natureofcode.com/autonomous-agents/?utm_source=chatgpt.com "5. Autonomous Agents / Nature of Code"
[7]: https://www.golancourses.net/2012spring/resources/natureofcode.shiffman.draft.1.16.2012.pdf?utm_source=chatgpt.com "Chapter 1.  Vectors"
[8]: https://www.cs.auckland.ac.nz/research/gameai/dissertations/Hurk_BSc_09.pdf?utm_source=chatgpt.com "A Multi-Layered Flocking"
[9]: https://opensteer.sourceforge.net/doc.html?utm_source=chatgpt.com "OpenSteer Preliminary Documentation"
[10]: https://natureofcodeunity.com/chaptersix.html?utm_source=chatgpt.com "The Nature of Code Unity Remix"
