# PR verification: Avalanche Lab

Checked 9 September 2026 against the packaged HTML. Repository baseline: `9afdb571bab88f87ad8415241668513c58708c90`.

- Packaged HTML SHA-256: `77df388c7d4a0a28286117721531f9133c7520d21aeb2670c3caca1f22d49feb`.
- `node tests/static_check.cjs`: inline scripts and smoke script parse; standalone dependency checks pass.
- `python tests/browser_smoke.py`: **11 passed checks**, Chromium `144.0.7559.96`, Python Playwright 1.57.0.
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
