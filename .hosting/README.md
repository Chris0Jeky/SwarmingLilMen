# SwarmingLilMen hosting reference

Reference-only preparation, 2026-09-10. `manifest.json` is inert agent-intake metadata. No engine, runtime, dataset, display name, domain, account or deployment trigger changes here. Existing workflows may still run after a future merge; inspect them first.

The repository already has standalone browser demonstrations under `js-demos/`, alongside its native .NET/Raylib and headless simulation paths. SL1 permits a later reviewed static demonstration surface using that distinction. Do not describe the browser demos as a port of the native engine, or propose an engine rewrite merely to host a website.

SL2 defines a public-artifact allowlist. Exclude raw private runs, local paths, credentials, unreviewed profiler output and test profiles. SL3 preserves the existing legacy/canonical engine boundaries and deterministic evidence. A green unit test does not establish renderer performance, measured frame rate or benchmark throughput. Do not publish the unmet 50,000-at-60-FPS target as an achievement.

Follow `AGENTS.md`, `CLAUDE.md` and the current project state. Preserve the fresh-clone fallback rules and existing inline-work convention; no hooks, harness or automatic merge authority is added. Syntax: `python -m json.tool .hosting/manifest.json`.

Future native changes require a fresh `dotnet build SwarmingLilMen.sln -c Release` before any `--no-build` test invocation. Use the existing test commands with `TreatNoTestsAsError=true`; browser demo changes need their own browser acceptance. JSON validation is not either test path. Keep the last reviewed build and stable demo/download links as rollback.

Publication, paid hosting and any product rename remain separate approvals. Private operational receipts and unregistered candidate names stay outside this public repository.
