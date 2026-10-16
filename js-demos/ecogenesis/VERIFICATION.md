# PR verification: Ecogenesis

Checked 16 October 2026 in `C:/Users/jekyt/source/slm-73`, with file edits over HEAD `0577c887301c9bf45bd43a9422430637661ecad5`. No Git writes performed.

- Reviewed HTML SHA-256: `36ae6bef4f6c84b77fe7c30f46806f65f519c9bcb74bb625002fe0c5f2127cfc` (computed from the packaged bytes).
- `node js-demos/ecogenesis/tests/static_check.cjs`: inline scripts and smoke script parse; standalone dependency checks pass.
- `py -3 js-demos/ecogenesis/tests/browser_smoke.py`: **47 passed checks**, Chromium `151.0.7922.34`, installed Python Playwright **1.62.0**.
- The workflow pins Playwright **1.57.0**. Installation in an isolated environment was attempted three ways, stalled without output, and was cancelled; that pinned version was **not verified** locally.
- Red before green: with only the new hostile archive test added, the browser command failed at `hostile archive import is rejected without creating executable elements` against the original HTML. It passes after the fix.
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
- hostile archive import rejects without live mutation or executable ancestry elements
- valid archive cause containing HTML displays literally, with no image, script or onerror element
- malformed archive identities, parents, ticks, generation, cause and traits reject
- archive (12,000), history (260), species (12,000), and species-signature bounds reject oversized imports
- selected id must be null or an existing organism; null selection round trips
- unknown organism fields and prototype keys are ignored
- Float32 environment and recurrent-memory overflow reject before live mutation
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

The C# Release suite and hosted CI were not run for these standalone JavaScript edits. The workflow's pinned Playwright 1.57.0 run remains unverified locally. Checkpoints with the former unselected sentinel `selectedId: 0` are now rejected; exports use `null`. No native model was compiled and no cross-runtime exactness, scale target, scientific calibration or whole-model security certification is claimed. Original design documents may quote earlier tests or experiments; those are not substituted for the checks above. This repo declares `human_todo: null`; no human-action file applies.
