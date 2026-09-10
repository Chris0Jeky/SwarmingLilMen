# PR verification: Firefly Synchrony

Checked 9 September 2026 against the packaged HTML. Repository baseline: `9afdb571bab88f87ad8415241668513c58708c90`.

- Packaged HTML SHA-256: `b1982d1b0ab8c780c358a63f45b3a6312cf7ad4f9d65a1947e06c1d8d824a0b7`.
- `node tests/static_check.cjs`: inline scripts and smoke script parse; standalone dependency checks pass.
- `python tests/browser_smoke.py`: **22 passed checks**, Chromium `144.0.7559.96`, Python Playwright 1.57.0.
- HTML loaded by its exact text in a real browser, not by replacing its model with test doubles.
- No unexpected page errors, console errors/warnings or runtime network requests.

## Checks exercised

- built-in deterministic self-check
- same seed and different chunk sizes match full numeric state
- different seed diverges
- odd population exercises cached Gaussian state
- checkpoint restores future noisy evolution, including Gaussian cache
- invalid checkpoint rejected before live-state mutation
- invalid checkpoint rejected before live-state mutation
- invalid checkpoint rejected before live-state mutation
- invalid checkpoint rejected before live-state mutation
- global remains finite with bounded coherence
- spatial remains finite with bounded coherence
- full-radius metric query includes every other oscillator
- ring remains finite with bounded coherence
- preset critical runs
- preset disorder runs
- preset chorus runs
- preset localWaves runs
- preset chimera runs
- preset fireflies runs
- interventions preserve finite phases
- canvas is present
- no browser errors, warnings or network requests

## Not established here

The local environment has no .NET SDK; the existing C# Release suite was not run locally. Hosted CI results belong to the PR checks, not this historical file. No native model was compiled and no cross-runtime exactness, scale target, scientific calibration or whole-model security certification is claimed. Original design documents may quote earlier tests or experiments; those are not substituted for the checks above.
