# PR verification: Turing Morphogenesis

Checked 9 September 2026 against the packaged HTML. Repository baseline: `9afdb571bab88f87ad8415241668513c58708c90`.

- Packaged HTML SHA-256: `e78cae81518046080e1281dab2b15c4c6aa76cefbe2c90c5e4921d9f530dd1f2`.
- `node tests/static_check.cjs`: inline scripts and smoke script parse; standalone dependency checks pass.
- `python tests/browser_smoke.py`: **13 passed checks**, Chromium `144.0.7559.96`, Python Playwright 1.57.0.
- HTML loaded by its exact text in a real browser, not by replacing its model with test doubles.
- No unexpected page errors, console errors/warnings or runtime network requests.
- One deliberate malformed-checkpoint case produces the expected validation message. It is checked separately from unexpected errors.

## Checks exercised

- same seeded reset and execution chunking reproduce field
- concentrations are bounded and metrics finite
- preset mitosis evolves finite fields
- preset coral evolves finite fields
- preset maze evolves finite fields
- preset worms evolves finite fields
- preset solitons evolves finite fields
- sealed boundary remains finite
- real downloaded checkpoint restores through file-input control
- restored field checkpoint reproduces future integration
- null diffusion parameter rejected without replacing field or configuration
- canvas is present
- no browser errors, warnings or network requests

## Not established here

The local environment has no .NET SDK; the existing C# Release suite was not run locally. Hosted CI results belong to the PR checks, not this historical file. No native model was compiled and no cross-runtime exactness, scale target, scientific calibration or whole-model security certification is claimed. Original design documents may quote earlier tests or experiments; those are not substituted for the checks above.
