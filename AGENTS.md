# AGENTS.md — SwarmingLilMen (Codex adapter)

`CLAUDE.md` is the single home for this repo's tier, description, commands, per-seam proving
checks, and pitfalls. Read it first; it wins on any conflict. This file carries only the delta for
a non-Claude runtime.

## Codex delta

- **The estate's global laws are not auto-injected into Codex.** Read them at session start:
  `~/.claude/CLAUDE.md`; blueprint `C:/Users/jekyt/source/agent-harness/BLUEPRINT.md`; registry
  `~/.claude/ESTATE.md`.
- **Deny floor.** Claude receives the irreversible-command floor from the global PreToolUse hook.
  Codex has no global matcher, so `.codex/hooks.json` pins the same shared dispatcher
  (`~/.claude/hooks/dispatch.py --event pre --runtime codex`). That adapter is **inert until its
  exact definition is reviewed and trusted through `/hooks` in a fresh Codex session** — untrusted
  means no floor at all, not a quiet one. Never stack a second repo-level floor hook alongside it.
- `.codex/config.toml` declares `hooks = true`, `multi_agent = false` — work inline in this repo.
- House style: `rg` to search, `apply_patch` to edit, narrow diffs, conventional present-tense
  commit subjects, and the per-seam `dotnet test --filter` from `CLAUDE.md` before any claim.
- `.claude/agents/project-validator.md` is a read-only adversarial reviewer (Read/Grep/Glob only).
  It cannot run anything — route "what does this do at runtime" to a runtime that can execute.
