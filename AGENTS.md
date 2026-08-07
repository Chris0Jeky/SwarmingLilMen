# AGENTS.md — SwarmingLilMen (Codex adapter)

`CLAUDE.md` is the single home for this repo's tier, description, commands, per-seam proving
checks, and pitfalls. Read it first; it wins on any conflict. This file carries only the delta for
a non-Claude runtime.

## Codex delta

- **The estate's global laws are not auto-injected into Codex.** Read them at session start:
  `~/.claude/CLAUDE.md`, the registry `~/.claude/ESTATE.md`, and `BLUEPRINT.md` in the active
  `agent-harness` checkout (its path is recorded in `ESTATE.md`, not hardcoded here).
- **Deny floor.** Claude receives the irreversible-command floor from the global PreToolUse hook.
  Codex has no global matcher, so `.codex/hooks.json` pins the same shared dispatcher
  (`~/.claude/hooks/dispatch.py --event pre --runtime codex`). That adapter is **inert until its
  exact definition is reviewed and trusted through `/hooks` in a fresh Codex session** — untrusted
  means no floor at all, not a quiet one. Never stack a second repo-level floor hook alongside it —
  the same rule holds Claude-side: this repo deliberately declares no `.claude` hooks; keep it so.
- `.codex/config.toml` declares `hooks = true`, `multi_agent = false` — work inline in this repo.
- House style: `rg` to search, `apply_patch` to edit, narrow diffs, conventional present-tense
  commit subjects, and the per-seam `dotnet test --filter` from `CLAUDE.md` before any claim.
- `.claude/agents/project-validator.md` is a read-only adversarial reviewer (Read/Grep/Glob only).
  It cannot run anything — route "what does this do at runtime" to a runtime that can execute.

## Fail-safe floor — binds when no estate profile is installed

`~/.claude/CLAUDE.md` and `~/.claude/ESTATE.md` are **not tracked in this repository**. In a fresh
public clone, in CI, or in any Codex installation outside the author's machine they simply do not
exist — and an untrusted `.codex/hooks.json` enforces nothing either. A session there has neither
injected policy nor a deny floor, so the rules below are restated in full rather than referenced.
They are the entire policy in that case, and they still bind when the estate profile *is* present:

- **Nothing irreversible without explicit human instruction.** No force-push, no history rewrite,
  no branch or tag deletion, no `reset --hard` / `clean -fd` / `stash` / `checkout --` across work
  you did not write, and no deleting files you did not create.
- **Preserve unrelated work.** Never destroy an unclean tree merely to obtain a clean one. Ask.
- **Never commit secrets**, tokens, credentials, private data, generated profiler or test output,
  or agent-attribution trailers. This repository is public and its history is permanent.
- **Publishing is scoped, and merging is not in it.** Local edits, builds, tests, commits, branch
  pushes, and pull requests are in scope. Merging to `main`, repository settings, releases, and
  anything that leaves this repository are not — `authority.merge` in `.agent-harness/tier.json`
  presumes the estate laws that are missing here, so without them merge is human-only.
- **No unproven claims.** Run the check that proves the claim, and state plainly what you did not
  verify — a green unit suite is not renderer, benchmark, or throughput evidence here.
