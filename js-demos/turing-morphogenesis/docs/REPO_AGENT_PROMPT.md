> Imported design reference from the chat demo. Native C# work described below remains a proposal; see this package README for current commands and verification.

# In-repo agent prompt: evaluate the Turing Morphogenesis foundation

You are working inside `Chris0Jeky/SwarmingLilMen`.

A standalone Gray–Scott reaction–diffusion demo and an integration analysis have been supplied. Treat them as an implementation proposal and behavioural reference, not as repository truth or owner-ratified architecture.

## Read first

1. `CLAUDE.md`
2. `AGENTS.md` when running under Codex
3. the verified-state block at the top of `PROJECT_STATUS.md`
4. epic #10 and the current open issues
5. the supplied `INTEGRATION_NOTES.md`

Refresh the actual checkout, issue, pull-request, CI, licensing, and authority state before acting. Do not rely on dates or counts copied into this bundle.

## Objective

Determine whether a small, headless Gray–Scott foundation can be added without broad speculative abstractions and without interrupting the currently authorised epic ordering.

## First deliverable: read-only design note

Create a concise proposal that answers:

- whether this belongs before or after issue #30;
- which existing contracts can be reused unchanged;
- which contract, if any, must be widened;
- why boid `IRule`, steering `Intent`, and `ISpatialIndex` must not be forced onto a pure field model;
- the smallest file/project layout that fits current repository conventions;
- deterministic and behavioural acceptance tests;
- expected memory and per-step cost at 160×100, 220×140, 300×190, and one larger headless size;
- whether the work should be a new issue, a child of an existing issue, or deferred.

Do not create production scaffolding until that note establishes a concrete consumer and an authorised ordering.

## Implementation constraints if authorised

- Start with one concrete `GrayScottSimulation`; do not create a universal field framework.
- Use synchronous double-buffered binary32 fields.
- Validate all settings, dimensions, and multiplication overflow before allocation.
- Record integrator, stencil, boundary, concentration, RNG, and model version in any manifest.
- No allocation in `Step()` after construction.
- Scientific outputs must be deterministic; elapsed time and allocation telemetry must be stored separately.
- Add exact tests for replay, fixed point, stencil, boundary semantics, synchronous updating, and state round-trip.
- Add measured behavioural envelopes rather than asserting a visual label.
- Update `PROJECT_STATUS.md` only with facts proven at the exact head.
- Do not claim renderer behaviour without launching and inspecting it.
- Do not merge, rebaseline golden fixtures, or change roadmap ordering beyond the authority currently active in the checkout.

## Stop conditions

Stop and report rather than improvising when:

- the work would enter a wave not currently authorised;
- a new generic contract has no second real consumer;
- the implementation requires changing existing boid trajectories;
- a deterministic fixture would need an unbudgeted rebaseline;
- the renderer or artefact format requires an owner decision;
- the supplied demo conflicts with repository behaviour or policy.

## Expected report

Return:

- inspected head and live state;
- recommended disposition;
- proposed issue text or issue update;
- dependency graph;
- architecture delta;
- test plan;
- benchmark plan;
- human decisions, if any;
- exact next command for the authorised agent or human.
