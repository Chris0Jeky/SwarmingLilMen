# Named RNG streams implementation plan

> **For agentic workers:** Use independent review and exact-head CI before merging.

**Goal:** Give each stochastic consumer a reproducible, independent source of draws, and migrate
canonical wander in the one explicit reference update budgeted by issue #26.

**Architecture:** A stateless `RandomStreams` factory derives PCG32 initial state and sequence from
an unambiguous SHA-256 encoding of seed, exact UTF-8 name and optional stable agent ID. Consumers
retain their own mutable `Pcg32Rng`. Canonical wander owns `canonical.wander` per insertion index.
The legacy `Rng` and all its consumers remain unchanged.

**Tech stack:** C#/.NET 8; BCL hashing/binary primitives; no new packages.

**Spec:** [Issue #26](https://github.com/Chris0Jeky/SwarmingLilMen/issues/26), within the existing
owner-authorized autonomous implementation and merge workflow.

## Decisions and constraints

- PCG32 XSH-RR with the published two-step seeding protocol provides a small specified generator
  with an external test vector. A counter-based generator was considered; independent retained
  consumer state is sufficient here, and this change does not introduce parallel scheduling.
- SHA-256 at stream creation avoids process-randomized string hashes and a custom string mixer.
  Construction may allocate; drawing and simulation stepping may not allocate steady-state.
- Names are ordinal, strict UTF-8, without normalization. Missing/blank names fail. Optional agent
  identity has an explicit tag; a world stream and agent zero are distinct.
- Recreating an identity restarts it. The factory is not a cache and never shares mutable RNG
  instances. Identical identities intentionally give common random numbers across experiments.
- Float/double adapters use exactly 24/53 random bits scaled by powers of two. They exclude one.
  Bounded unsigned draws use rejection; invalid bounds fail before consuming state.
- Canonical per-agent identity remains insertion index because canonical has no removal/reordering.
  Future lifecycle support must preserve stable identity before changing that assumption.
- This fixes generator portability and inter-consumer coupling, not float transcendental math,
  cross-platform trajectory identity, or consumption differences within a conditional consumer.
- Keep Apache-2.0 attribution/license for the adapted PCG core; repository license is unchanged.

## Tasks and proving checks

- [x] Pin external PCG words, independently calculated float/double/bounded words, named identity
  vectors, validation and interleaved-consumer independence in ordinary tests.
- [x] Implement the generator/factory and route canonical wander. Keep legacy bytes/fixtures intact.
- [x] Add an actually invoked fourth stochastic observer and prove 256 ticks of existing motion
  are unchanged when it draws. Retain capacity/extra-agent isolation and cross-process checks.
- [x] Capture old-vs-new canonical checkpoint drift on Linux and Windows; commit reviewed exact
  references only after independently checking component words and hashes. Keep one-ULP witnesses.
- [x] Update determinism, source guidance and project status with the measured new scope/limits.
- [ ] Run full Release non-performance gate, allocation gate, and exact-head/merge-result CI on
  Ubuntu and Windows; inspect logs and review the final published diff before merging.

## Review focus

The highest-risk inputs are maximum-width unsigned seeds/IDs (wraparound), ambiguous names or
absent-vs-zero IDs (encoding), bounds above 2^31 (rejection), malformed Unicode (silent replacement),
and consumers that draw conditionally (isolation between streams versus within one stream).
Committed vectors and dedicated tests cover each; trajectory fixtures remain exact.
