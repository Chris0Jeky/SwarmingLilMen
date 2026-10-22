# PR verification: Avalanche Lab

## Issue #77 follow-up (22 October 2026)

Both defects reproduced with new browser assertions before changing the implementation:
direct sink input left both accounting totals at zero, and a null history entry caused an
import exception after changing the live grid and history.

Checkpoint import now stages and validates the lattice, seed/RNG, history, counters and controls
before applying them. Rejected imports preserve the exported state, snapshot, summary and UI
settings; an active avalanche also retains its queued work and deterministic continuation.
Direct sink input counts as both added and immediately dissipated, without starting an avalanche.
During an active avalanche it also contributes to that event's added/dissipated totals.

Commands run from the repository root:

- `node js-demos/avalanche-lab/tests/static_check.cjs`: passed.
- `python js-demos/avalanche-lab/tests/browser_smoke.py`: 35 passed checks in Chromium
  `151.0.7922.34`, including malformed-field rejection, active-import rollback, sink accounting,
  checkpoint replay, and no browser errors, warnings or network requests.
- `PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s .hosting -p "test_*.py"`:
  46 tests run, 45 passed, one skipped (symlink creation unavailable).
- `git diff --check`: passed.

PowerShell set `$env:PYTHONDONTWRITEBYTECODE='1'` before Python commands and set `TMP`/`TEMP` to
the worktree-local `.test-tmp` for the final browser and scanner runs. No packages were installed.
HTML SHA-256: `e7d0e5f7477bb2096866e4dd35d6d494c852048d335b26b7600b81d5f165cf70`.
Hosted CI, other browsers and deployment were not run. No commit, push or Git index write was made.

## Historical PR #72 verification

Checked 10 September 2026 against the reviewed HTML. Repository baseline: `9afdb571bab88f87ad8415241668513c58708c90`.

- Reviewed HTML SHA-256: `f573e8cf7baa23a54ef83b4b1afd2c2f67e17d719638cc4d0d73c517782faf20`.
- `node tests/static_check.cjs`: inline scripts and smoke script parse; standalone dependency checks pass.
- `python tests/browser_smoke.py`: **11 passed checks**, Chromium `143.0.7499.4`, Python Playwright 1.57.0.
- HTML loaded by its exact text in a real browser, not by replacing its model with test doubles.
- No unexpected page errors, console errors/warnings or runtime network requests.

## Checks exercised

- same seed produces identical settled states
- different seeds diverge
- grain conservation after repeated avalanches
- active-avalanche export is rejected
- large perturbation fully relaxes with exact mass accounting
- perturbation changes checkpoint state
- stable checkpoint round-trip
- checkpoint restores future random drive
- explicit sink removes incoming grains without accounting drift
- canvas is present
- no browser errors, warnings or network requests

## Not established here

The local environment has no .NET SDK; the existing C# Release suite was not run locally. Hosted CI results belong to the PR checks, not this historical file. No native model was compiled and no cross-runtime exactness, scale target, scientific calibration or whole-model security certification is claimed. Original design documents may quote earlier tests or experiments; those are not substituted for the checks above.
