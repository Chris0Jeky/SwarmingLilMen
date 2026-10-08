> Imported design reference from the chat demo. Native C# work described below remains a proposal; see this package README for current commands and verification.

# Ecogenesis research experiment pack

> These are proposed replicated protocols for the browser model. The interactive demo exposes the controls and deterministic scripting surface, but it does not yet provide an automatic batch runner. Use the exact seed sets and record the full configuration, build hash and intervention schedule for every result.

## General reporting rules

- Treat each seed as one replicate; never report a visually attractive trajectory as representative evidence.
- Separate survival probability, extinction time, transient dynamics and terminal state.
- Report operational species counts together with the clustering threshold that produced them.
- Preserve the full checkpoint and state fingerprint for every analysed run.
- Distinguish model time, browser wall time and rendering frame rate.
- Analyse treatment effects against matched-seed controls whenever an intervention is involved.

## ECO-01-baseline-turnover — Baseline trophic turnover

**Question.** Does the Genesis basin retain all three trophic niches across seeds for two simulated years?

**Preset:** `genesis`
**Ticks per run:** `3600`
**Replicate seeds:** `101, 211, 307, 419, 523, 631, 743, 857, 967, 1087, 1201, 1303`

### Fixed parameters

- `plantGrowth` = `1.0`
- `predationLethality` = `1.0`
- `mutationStrength` = `1.0`
- `speciationThreshold` = `0.18`

### Intervention schedule

- None; allow endogenous dynamics only.

### Metrics

- `population`
- `herbivores`
- `omnivores`
- `predators`
- `plantMean`
- `births`
- `deaths`
- `predationDeaths`
- `maxGeneration`

### Interpretation constraint

Report niche persistence probability and extinction-time distribution; do not promote a single surviving seed as stability evidence.

## ECO-02-island-radiation — Archipelago adaptive radiation

**Question.** How do habitat fragmentation and aquatic adaptation affect lineage branching and migration?

**Preset:** `archipelago`
**Ticks per run:** `5400`
**Replicate seeds:** `1409, 1523, 1627, 1741, 1847, 1951, 2063, 2179`

### Factorial sweep

- `speciationThreshold` ∈ `[0.1, 0.14, 0.18, 0.24]`
- `mutationStrength` ∈ `[0.75, 1.0, 1.5]`

### Intervention schedule

- None; allow endogenous dynamics only.

### Metrics

- `livingSpecies`
- `speciesOrigination`
- `speciesExtinction`
- `aquaticTraitMean`
- `aquaticTraitVariance`
- `islandOccupancy`
- `lineageDepth`
- `genomeDiversity`

### Interpretation constraint

Treat cluster count as algorithm-dependent; report sensitivity to the speciation threshold alongside every radiation result.

## ECO-03-arms-race — Predator–prey trait arms race

**Question.** Which combinations of lethality and mutation maintain coevolution rather than immediate prey or predator collapse?

**Preset:** `armsrace`
**Ticks per run:** `5400`
**Replicate seeds:** `2281, 2383, 2503, 2609, 2711, 2819, 2939, 3041`

### Factorial sweep

- `predationLethality` ∈ `[0.5, 0.8, 1.0, 1.3, 1.7]`
- `mutationStrength` ∈ `[0.5, 1.0, 1.5]`

### Intervention schedule

- None; allow endogenous dynamics only.

### Metrics

- `preyBodySizeMean`
- `preySpeedMean`
- `preyDefenseMean`
- `preyCamouflageMean`
- `predatorBodySizeMean`
- `predatorSpeedMean`
- `predationRate`
- `nichePersistence`

### Interpretation constraint

Use replicated trajectories and extinction probabilities; avoid inferring adaptation from one terminal trait mean.

## ECO-04-plague-selection — Plague, immunity and demographic cost

**Question.** Does repeated pathogen pressure select higher immunity, and what population cost follows?

**Preset:** `genesis`
**Ticks per run:** `5400`
**Replicate seeds:** `3163, 3271, 3389, 3499, 3613, 3727, 3847, 3967`

### Factorial sweep

- `diseaseTransmission` ∈ `[0.5, 1.0, 1.5, 2.0]`

### Intervention schedule

- `tick`=`1200`, `type`=`plague`
- `tick`=`3000`, `type`=`plague`

### Metrics

- `infectionPrevalence`
- `infectionIncidence`
- `recoveryRate`
- `immunityMean`
- `immunityVariance`
- `population`
- `birthRate`
- `deathRate`

### Interpretation constraint

Compare against intervention-free controls at identical seeds and report the built-in immunity metabolic cost.

## ECO-05-catastrophe-recovery — Mass-extinction recovery and hysteresis

**Question.** Does post-impact ecology return to its pre-impact trophic and trait state?

**Preset:** `genesis`
**Ticks per run:** `7200`
**Replicate seeds:** `4001, 4111, 4217, 4337, 4447, 4561, 4679, 4787, 4903, 5009`

### Intervention schedule

- `tick`=`2700`, `type`=`meteor`, `x`=`800`, `y`=`450`, `radius`=`280`

### Metrics

- `population`
- `plantMean`
- `trophicComposition`
- `genomeDiversity`
- `livingSpecies`
- `lineageSurvival`
- `recoveryTime`
- `terminalDistanceFromPreImpactState`

### Interpretation constraint

Define recovery before running: demographic, functional and genetic recovery are separate outcomes.

## ECO-06-cambrian-sensitivity — Mutation and operational speciation sensitivity

**Question.** How much apparent adaptive radiation is biological turnover versus a clustering-threshold artifact?

**Preset:** `cambrian`
**Ticks per run:** `3600`
**Replicate seeds:** `5119, 5231, 5347, 5471, 5581, 5701, 5813, 5927`

### Factorial sweep

- `mutationStrength` ∈ `[0.75, 1.0, 1.5, 2.15, 2.5]`
- `speciationThreshold` ∈ `[0.08, 0.1, 0.12, 0.16, 0.22, 0.3]`

### Intervention schedule

- None; allow endogenous dynamics only.

### Metrics

- `livingSpecies`
- `singletonSpeciesFraction`
- `speciesLifetimeDistribution`
- `genomeDiversity`
- `lineageDepth`
- `traitDispersion`
- `population`

### Interpretation constraint

Always publish the full mutation × threshold surface; never report one cluster count without its sensitivity context.

## Suggested output layout

```text
runs/<experiment-id>/<treatment-id>/<seed>/
  manifest.json
  checkpoint-final.json
  metrics.csv
  events.jsonl
  summary.json
```

For a future repository integration, the experiment runner should produce stable treatment ordering, fail on unknown sweep keys, and keep deterministic scientific measurements separate from wall-clock performance telemetry.
