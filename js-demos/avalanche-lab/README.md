# Avalanche Lab

Integer sandpiles with threshold cascades, dissipative sinks and exact mass accounting.

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

## Original implementation preserved

The HTML is imported byte-for-byte. Checkpoints are stable-state only, not mid-avalanche resumptions. Cascade measurements are exploratory; the primed lattice is not a stationary-distribution sample and the displayed tail-fit estimate is not proof of a power law. The new suite checks stable replay, explicit sinks, bounded relaxation and zero mass residual.

## Native integration and research status

Give this model a work-budget/relaxation contract rather than pretending a toppling is a physical fixed-dt tick. Preserve integer heights, legal toppling order, open-boundary dissipation and explicit unfinished work. Start with FIFO versus LIFO final-state equivalence, grain accounting and bounded run tests. Add mid-cascade checkpoints only when the queue and partial event accumulators are included.

The standalone demo does not satisfy the shared-kernel proof in epic #10. Native integration still depends on the repository's experiment work. No existing golden traces are replaced and no renderer, SDK, global hook, licence or merge-policy settings are changed by this package.

The model is synthetic. Same-runtime replay does not establish cross-browser bitwise identity or empirical validity. Performance targets and historical experiment results must not be inferred from a passing smoke suite.

## Licence

The repository's [GPL-3.0-only licence](../../LICENSE) and [relicensing record](../../RELICENSING.md) apply. No separately licensed runtime asset or library is bundled here.
