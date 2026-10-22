# SwarmingLilMen hosting reference

Reference-only preparation, 2026-09-10. `manifest.json` is inert agent-intake metadata. No engine, runtime, dataset, display name, domain, account or deployment trigger changes here. Existing workflows may still run after a future merge; inspect them first.

The repository already has standalone browser demonstrations under `js-demos/`, alongside its native .NET/Raylib and headless simulation paths. SL1 permits a later reviewed static demonstration surface using that distinction. Do not describe the browser demos as a port of the native engine, or propose an engine rewrite merely to host a website.

SL2 defines a public-artifact allowlist. Exclude raw private runs, local paths, credentials, unreviewed profiler output and test profiles. SL3 preserves the existing legacy/canonical engine boundaries and deterministic evidence. A green unit test does not establish renderer performance, measured frame rate or benchmark throughput. Do not publish the unmet 50,000-at-60-FPS target as an achievement.

Follow `AGENTS.md`, `CLAUDE.md` and the current project state. Preserve the fresh-clone fallback rules and existing inline-work convention; no hooks, harness or automatic merge authority is added. Syntax: `python -m json.tool .hosting/manifest.json`.

Future native changes require a fresh `dotnet build SwarmingLilMen.sln -c Release` before any `--no-build` test invocation. Use the existing test commands with `TreatNoTestsAsError=true`; browser demo changes need their own browser acceptance. JSON validation is not either test path. Keep the last reviewed build and stable demo/download links as rollback.

Publication, paid hosting and any product rename remain separate approvals. Private operational receipts and unregistered candidate names stay outside this public repository.

## Static publication (LIVE since 2026-10-11)

Status: the static demo publication is LIVE at https://chris0jeky.github.io/SwarmingLilMen/ since 2026-10-11. The owner approved go-live in chat ("Go live after review"), after previewing the built site. GitHub Pages is enabled with source GitHub Actions, and `pages-demos.yml` was deployed by manual `workflow_dispatch` from `main` (run 38128281974). `activation_authorized` is `true` and `public_origin` is set in `manifest.json`.

Origin of the decision (agent-hq hosting-public-demos, option b): agents prepared a static publication of the existing `js-demos/` behind an artifact allowlist and sent it to the owner before go-live.

- `publish-allowlist.txt` lists exactly the 7 files published: each demo's single self-contained `index.html` (ant-colony-optimization, avalanche-lab, boids-basic, firefly-synchrony, particle-swarm-optimization, self-propelled-particles, turing-morphogenesis). Every demo has inline CSS and JS and no external references, so no other runtime asset exists.
- Not published: demo READMEs, `VERIFICATION.md`, `docs/`, `tests/`, `source-provenance.json`, `requirements-test.txt`, `REPO_AGENT_PROMPT.md`, `IMPLEMENTATION_COMPARISON.md`, and everything outside `js-demos/` (the native .NET engine, benchmarks, configs, project docs). No demo was excluded for needing a build step or network.
- `build_site.py` (stdlib Python) copies the allowlisted files to `_site/` (gitignored) with a generated index page. It fails, writing nothing, if a listed file is missing or not a plain runtime asset, or if a listed page references a local file that is not listed, a remote URL, or a root-absolute path. Run `py -3 .hosting/build_site.py`; test with `py -3 -m unittest discover -s .hosting -p "test_*.py"`.
- Attributes are read with `html.parser`, including quoted/unquoted values, `srcset`/`imagesrcset` candidates and meta refresh URLs. `fetch`, imports and `serviceWorker.register` require a single unescaped literal URL; computed arguments, template substitutions and multi-line imports fail closed before output is written. `new Worker`/`SharedWorker`, `importScripts` (one URL) and any `.register(` follow the same rule; a bare `serviceWorker` that is not `serviceWorker.register(<literal>)`, and any use of `WebSocket`, `WebTransport`, `EventSource`, `sendBeacon` or `XMLHttpRequest` in code, are rejected. The JavaScript scan is a small lexer: comments, strings, regular-expression literals and template text are inert, `${...}` template parts are code, and an unterminated string, comment, regex or template, or an escaped identifier, is rejected. `<script>` blocks are scanned unless their `type` is clearly not JavaScript (shaders, JSON); `importmap` and `speculationrules` blocks are rejected. JavaScript checking is conservative, not a full language parser; the allowlist remains the publication boundary.
- `.github/workflows/pages-demos.yml` builds `_site/` and deploys it with `actions/deploy-pages`. It is `workflow_dispatch` only; redeploys are manual dispatches from `main`. Pages source is GitHub Actions. Do not add push, pull_request or schedule triggers without a new owner decision.

The index page labels the demos as separate from the native engine and makes no performance claim. Rollback: disable GitHub Pages for the repository. Any further publication change (new artifacts, new origin, paid hosting) remains a separate owner approval.
