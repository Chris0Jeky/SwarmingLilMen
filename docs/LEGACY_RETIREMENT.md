# Legacy engine retirement criteria

The legacy and canonical engines remain supported side by side. The owner confirmed these
criteria and the **at most 2× legacy** performance factor in [decision D3](https://github.com/Chris0Jeky/SwarmingLilMen/issues/31)
and [the parity issue's owner comment](https://github.com/Chris0Jeky/SwarmingLilMen/issues/28#issuecomment-5076016834).
The owner personally signs the eventual deletion PR. Completing this checklist is evidence for
that decision, not deletion authorization.

- [ ] **Group semantics:** canonical implements and verifies the group behavior needed by the
  supported legacy scenarios. A stored `Boid.Group` byte or a neighbor field alone is insufficient.
  Document same-group and cross-group interactions and their configuration mapping.
- [ ] **Agent lifecycle:** canonical supports verified spawn/add and removal, with stable identity,
  deterministic ordering, and correct telemetry/observer behavior across population changes.
  A fixed-capacity add method alone does not satisfy this requirement.
- [ ] **Behavioral comparison:** all five prescribed parity scenarios have repeatable artifacts;
  the same-build repeat and spatial-index exact checks pass; S2/S4/S5 classic/reference envelopes
  pass using the fixed seed roster and final-window method. The alignment-off mutation must fail
  the polarization gate. Explain measured legacy differences rather than demanding bitwise
  equivalence between different models.
- [ ] **Matched headless performance:** at both 10,000 and 50,000 agents, canonical's measured
  complete-tick cost is no more than twice legacy's, on the same machine/runtime with matched
  density, timestep, query capacity, warmup/iteration schedule and capture settings. Preserve
  both raw benchmark results, allocations, variability and exact source identities. Do not
  compare old machine figures or describe headless timing as renderer FPS.
- [ ] **User migration:** document supported presets/configuration mappings, CLI and renderer
  entry points, replay/version limits, missing features, and how existing experiments are retained.
  Old manifests remain tied to compatible historical binaries rather than being silently rewritten.
- [ ] **Owner deletion review:** the owner reviews the above evidence and personally signs the
  dedicated deletion PR, with an explicit migration/rollback plan.

At creation, none of these checklist items is asserted complete. Named composition and public
ClassicReynolds configuration make the parity comparison possible; they do not add group or
removal semantics. The legacy spawn-growth work is a supported legacy feature, not a reason to
remove it. Track verification in [issue #28](https://github.com/Chris0Jeky/SwarmingLilMen/issues/28)
and the authoritative epic [#10](https://github.com/Chris0Jeky/SwarmingLilMen/issues/10).

