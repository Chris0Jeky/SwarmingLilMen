# Firefly Synchrony

Phase-coupled oscillators: global, spatial and ring coupling.

This is the working browser implementation developed in the conversation, now packaged as repository source. It is not a native C# port. Open [index.html](index.html) in a desktop browser, or serve this folder locally:

```sh
python -m http.server 8000 --bind 127.0.0.1
```

There are no runtime package installations, external assets, server APIs or network calls. GitHub renders the HTML as source; download/checkout the branch and open the file, or use a static host.

## What is included

- `index.html`: complete offline application, with model, controls, canvas rendering and checkpoint tools.
- [Implementation plan](docs/IMPLEMENTATION_PLAN.md): staged native integration, model-specific architecture, hazards and acceptance gates.
- [Original integration notes](docs/INTEGRATION_NOTES.md): fuller design ideas and expansion directions.
- [Verification](VERIFICATION.md): checks run for this PR, distinct from historical evidence.
- `tests/`: dependency-free script parsing checks and real-browser deterministic/functional smoke checks.
- `source-provenance.json`: original artifact digest and reviewed repository base.

## Verification commands

Run from this directory:

```sh
node tests/static_check.cjs
python -m venv .venv
# Linux/macOS: .venv/bin/python; Windows: .venv\Scripts\python
.venv/bin/python -m pip install -r requirements-test.txt
.venv/bin/python -m playwright install chromium
.venv/bin/python tests/browser_smoke.py
```

The Python browser runner uses `CHROMIUM_PATH` when supplied, otherwise an installed Chromium or Playwright's downloaded browser. It loads the exact HTML content without external navigation. Test-only Playwright is not a runtime dependency. Set `SMOKE_REPORT` to an ignored path to retain JSON evidence.

## Checkpoint cache repair

Exports now preserve the Box–Muller cached Gaussian sample and flash state. Imports reject non-finite arrays, excessive populations and invalid settings before replacing live state. Legacy v1 exports without rngSpare remain readable, but exact continuation of an old odd-population noisy checkpoint cannot be guaranteed. The displayed 8-hex hash samples phases; the regression suite compares full numeric arrays and RNG state instead. Global mean-field coupling uses N including self, while metric and ring coupling use their local neighbour convention.

## Native integration and research status

Oscillator phases and intrinsic frequencies belong in a separate model, not a boids steering rule. Specify self-coupling, degree normalization, synchronous update ordering and noise units before porting. Implement a headless scalar baseline; add topology equivalence and zero-coupling analytical tests; only then add optimized global/grid paths. Use versioned streams and a normal-sampler cache in checkpoints.

The standalone demo does not satisfy the shared-kernel proof in epic #10. Native integration still depends on the repository's experiment work. No existing golden traces are replaced and no renderer, SDK, global hook, licence or merge-policy settings are changed by this package.

The model is synthetic. Same-runtime replay does not establish cross-browser bitwise identity or empirical validity. Performance targets and historical experiment results must not be inferred from a passing smoke suite.

## Licence

The repository's [GPL-3.0-only licence](../../LICENSE) and [relicensing record](../../RELICENSING.md) apply. No separately licensed runtime asset or library is bundled here.
