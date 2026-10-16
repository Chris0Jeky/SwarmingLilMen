# PR verification: Avalanche Lab

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
