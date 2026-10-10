# SwarmingLilMen hosting reference

Reference-only preparation, 2026-09-10. `manifest.json` is inert agent-intake metadata. No engine, runtime, dataset, display name, domain, account or deployment trigger changes here. Existing workflows may still run after a future merge; inspect them first.

The repository already has standalone browser demonstrations under `js-demos/`, alongside its native .NET/Raylib and headless simulation paths. SL1 permits a later reviewed static demonstration surface using that distinction. Do not describe the browser demos as a port of the native engine, or propose an engine rewrite merely to host a website.

SL2 defines a public-artifact allowlist. Exclude raw private runs, local paths, credentials, unreviewed profiler output and test profiles. SL3 preserves the existing legacy/canonical engine boundaries and deterministic evidence. A green unit test does not establish renderer performance, measured frame rate or benchmark throughput. Do not publish the unmet 50,000-at-60-FPS target as an achievement.

Follow `AGENTS.md`, `CLAUDE.md` and the current project state. Preserve the fresh-clone fallback rules and existing inline-work convention; no hooks, harness or automatic merge authority is added. Syntax: `python -m json.tool .hosting/manifest.json`.

Future native changes require a fresh `dotnet build SwarmingLilMen.sln -c Release` before any `--no-build` test invocation. Use the existing test commands with `TreatNoTestsAsError=true`; browser demo changes need their own browser acceptance. JSON validation is not either test path. Keep the last reviewed build and stable demo/download links as rollback.

Publication, paid hosting and any product rename remain separate approvals. Private operational receipts and unregistered candidate names stay outside this public repository.

## Static publication (prepared 2026-10-11, NOT live)

Owner decision (agent-hq hosting-public-demos, option b): agents prepare a static publication of the existing `js-demos/` behind an artifact allowlist and send it to the owner before it goes live. Nothing is published, Pages is not enabled, and `activation_authorized` stays `false`.

- `publish-allowlist.txt` lists exactly the 7 files published: each demo's single self-contained `index.html` (ant-colony-optimization, avalanche-lab, boids-basic, firefly-synchrony, particle-swarm-optimization, self-propelled-particles, turing-morphogenesis). Every demo has inline CSS and JS and no external references, so no other runtime asset exists.
- Not published: demo READMEs, `VERIFICATION.md`, `docs/`, `tests/`, `source-provenance.json`, `requirements-test.txt`, `REPO_AGENT_PROMPT.md`, `IMPLEMENTATION_COMPARISON.md`, and everything outside `js-demos/` (the native .NET engine, benchmarks, configs, project docs). No demo was excluded for needing a build step or network.
- `build_site.py` (stdlib Python) copies the allowlisted files to `_site/` (gitignored) with a generated index page. It fails, writing nothing, if a listed file is missing or not a plain runtime asset, or if a listed page references a local file that is not listed, a remote URL, or a root-absolute path. Run `py -3 .hosting/build_site.py`; test with `py -3 -m unittest discover -s .hosting -p "test_*.py"`.
- `.github/workflows/pages-demos.yml` builds `_site/` and deploys it with `actions/deploy-pages`. It is `workflow_dispatch` only and inert until the owner approves go-live and enables Pages (source: GitHub Actions); until then a dispatch fails at the deploy step.

The index page labels the demos as separate from the native engine and makes no performance claim. Go-live is a separate step: owner approval, enabling Pages, then dispatching the workflow. Rollback is disabling Pages.
