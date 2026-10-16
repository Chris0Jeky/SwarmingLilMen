> Imported design reference from the chat demo. Native C# work described below remains a proposal; see this package README for current commands and verification.

# In-repository agent prompt — Ecogenesis adoption and architecture spike

You are working inside `Chris0Jeky/SwarmingLilMen`.

This bundle contains an independently built, standalone browser simulation named **Ecogenesis — Artificial Life Observatory**. It is a proposed new emergence foundation, not evidence that the current C# kernel already supports ecology/evolution.

## Inputs

Read these bundle files first:

1. `ecogenesis_lab.html`
2. `README.md`
3. `INTEGRATION_NOTES.md`
4. `VERIFICATION.md`
5. `VALIDATION_REPORT.json`

Then refresh live repository truth before acting:

- current branch, HEAD and worktree;
- `AGENTS.md` and `CLAUDE.md`;
- `.agent-harness/tier.json` and active runtime policy;
- `PROJECT_STATUS.md` verified block;
- epic #10 and all open issues/PRs;
- current JS-demo structure and source-of-truth banners;
- current status of experiment-spine issues #22–#25;
- current status of composition/RNG issues #26–#28;
- current licensing and attribution requirements.

The bundle was prepared against the 2026-09-04 conversation context. Live repository state wins.

## Goal

Produce a reviewable adoption plan and, where current authority permits, a bounded demo-only pull request that adds Ecogenesis as an explicitly independent browser reference.

Do **not** silently turn the demo into a claim that the C# emergence kernel supports it.

## Stage A — Evidence-preserving demo adoption

Preferred first deliverable:

```text
js-demos/ecogenesis/
  index.html
  README.md
```

Actions:

- copy the standalone HTML without adding external dependencies;
- preserve its deterministic self-check and scriptable API;
- add a concise README with model scope and limitations;
- add the same live-source-of-truth banner used by maintained JS demo documentation;
- link it from `js-demos/README.md` and the root README only if that matches current repository convention;
- describe it as a qualitative/prototyping reference, not a C# parity target;
- preserve GPL and third-party-notice requirements;
- do not add generated validation JSON to the repository unless current artifact policy explicitly allows it.

## Stage B — Architecture decision record, not implementation sprawl

Create or propose a short architecture note that answers:

1. Which experiment/session contracts Ecogenesis could reuse unchanged?
2. Which boids contracts it must not inherit?
3. Which field/lifecycle/intent seams are foundation-owned?
4. What does ACO already prove about mutable fields?
5. What would Ecogenesis prove that Vicsek and ACO do not?
6. What prerequisites must exist before a C# vertical slice begins?

Use the seam-inventory categories:

```text
reused unchanged
widened
foundation-owned
bypassed
proved unnecessary
```

Do not introduce a universal `World<TAgent>` or package explosion from this document alone.

## Stage C — Issue shaping

Do not seed a large issue fleet automatically.

First search for overlap with existing issues. Prefer comments/subtasks under current roadmap authority when the work is already implied.

If a new issue is justified, propose at most three:

1. demo adoption;
2. Ecogenesis headless vertical-slice design after experiment-spine prerequisites;
3. a future ecology/evolution research proof.

Each proposed issue must include:

- motivation;
- blockers;
- exact scope and non-goals;
- acceptance criteria;
- deterministic proving checks;
- artifact/metric contract;
- documentation update;
- explicit statement that the browser demo is not C# kernel evidence.

## Stage D — C# implementation boundary

Do not begin a C# Ecogenesis implementation unless all of the following are true in live state:

- the owner explicitly selects it as a current foundation;
- the Raylib-free runner exists or an equivalent current contract is available;
- versioned experiment configuration and checkpoint conventions exist;
- named deterministic RNG streams exist or the owner authorizes a bounded pre-stream prototype;
- metric registration exists or a bounded foundation-local substitute is approved;
- the work does not bypass the current epic ordering authority.

If implementation is authorized, begin with the smallest headless slice:

```text
procedural fields
+ fixed-capacity organism store
+ metabolism and plant consumption
+ deterministic checkpoint
+ population/biomass metrics
```

Exclude predation, disease, neural controllers, reproduction and species clustering from the first PR. Add those through separately falsifiable vertical slices.

## Required proving checks for the demo PR

At minimum:

- parse the inline JavaScript;
- open the exact HTML in a browser runtime;
- confirm no uncaught console/page errors;
- run the embedded replay check;
- exercise at least one preset and one intervention;
- confirm the page has no network dependency;
- run the repository’s exact current Release gate if any tracked repository file changes outside static demo assets/docs;
- state explicitly that no C# trajectory, renderer or performance evidence was produced.

## Safety and authority

- Preserve unrelated work and unclean worktrees.
- Do not rewrite history, delete branches, change repository settings or publish releases.
- Do not merge when the runtime’s effective authority requires human merge.
- Do not change the roadmap ordering authority merely to make Ecogenesis current.
- Do not relax checkpoint, determinism or numerical-validation claims.
- Treat any manual renderer/browser evidence required by current policy as human-owned unless the runtime can genuinely produce and inspect it.

## Final report format

Return:

1. refreshed live-state summary;
2. overlap analysis against current issues;
3. files changed or proposed;
4. exact checks run and their results;
5. what was not verified;
6. decisions requiring the owner;
7. recommended next action;
8. any generated issue drafts or ADR text in copy-ready form.
