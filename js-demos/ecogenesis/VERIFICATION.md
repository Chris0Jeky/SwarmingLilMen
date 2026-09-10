# PR verification: Ecogenesis

Checked 10 September 2026 against the reviewed HTML. Repository baseline: `9afdb571bab88f87ad8415241668513c58708c90`.

- Reviewed HTML SHA-256: `6c242fa72870931a5d09e6920963edd88c33696b88aa37ccafddd58952150f43`.
- `node tests/static_check.cjs`: inline scripts and smoke script parse; standalone dependency checks pass.
- `python tests/browser_smoke.py`: **18 passed checks**, Chromium `143.0.7499.4`, Python Playwright 1.57.0.
- HTML loaded by its exact text in a real browser, not by replacing its model with test doubles.
- No unexpected page errors, console errors/warnings or runtime network requests.

## Checks exercised

- same seed reproduces full-state fingerprint
- different seeds diverge
- checkpoint restores numeric state
- restored RNG and recurrent state reproduce future
- malformed genome rejected before live mutation
- incomplete runtime parameters rejected before live mutation
- imported event text renders as text rather than executable HTML
- bounded endurance run remains finite
- reproduction actually executes
- population remains inside configured ceiling
- intervention bloom stays finite
- intervention drought stays finite
- intervention winter stays finite
- intervention plague stays finite
- intervention mutation stays finite
- intervention meteor stays finite
- canvas is present
- no browser errors, warnings or network requests

## Not established here

The local environment has no .NET SDK; the existing C# Release suite was not run locally. Hosted CI results belong to the PR checks, not this historical file. No native model was compiled and no cross-runtime exactness, scale target, scientific calibration or whole-model security certification is claimed. Original design documents may quote earlier tests or experiments; those are not substituted for the checks above.
