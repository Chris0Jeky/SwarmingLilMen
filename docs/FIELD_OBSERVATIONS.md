# Field-only experiments and observations

The experiment runner supports both agent models and models whose complete physical state is
an evolving field. A field cell is not counted as an agent. The first field-only consumer is
the [Gray–Scott reaction–diffusion foundation](REACTION_DIFFUSION.md), which stores typed U/V
concentrations and uses the same phase schedule, double buffer, metric registry, replay and
publication path as other native models.

## Population and metric capabilities

Engine registration declares whether a model provides kinematic agents. Existing agent models
keep their positive population requirement and default of 400. The `reaction-diffusion` engine
defaults to zero agents and accepts only zero; its native `Columns` and `Rows` settings define
the field size. The same contract applies to single runs, manifests, sweep cells and gallery
entries. Discovery exposes the capability and defaults before construction.

Field-only engines register field metrics and the existing opt-in runtime metrics. Motion,
population, centroid and nearest-neighbor channels are unavailable for them: there is no
particle population on which to compute those quantities. This avoids returning a motion
statistic that could be mistaken for a description of the field.

The current adapter still satisfies the shared capture interface by returning an empty
`KinematicSample` sequence and the unchanged v1 hash of that empty sequence. This compatibility
hash is constant; it is not evidence about field contents. Required schema-3 model-state
fingerprints cover the complete typed field, settings and clock. Empty kinematic capture does
not replace field-state replay verification.

## Optional scalar projection

The internal `IFieldSnapshotSource` capability supplies one row-major scalar projection for
reports: dimensions, a channel label, a fixed finite color range and a pure copy operation.
It does not expose mutable arrays, step the model or draw randomness. A model may retain many
typed fields while choosing one meaningful report projection; Gray–Scott reports V concentration
and hashes both U and V.

`FrameRecorder` copies that projection only at the selected ticks, including the initial and
final states, with at most eight frames. Previous frames own their samples and cannot change
as the simulation evolves. Dimensions, labels and color range are captured once and validated
before output. A report chooses either an agent projection or a field projection.

The Gray–Scott grid is bounded by `ReactionDiffusionSettings.MaxCells` (1,048,576 cells).
Eight single-precision field snapshots use at most 32 MiB for their scalar sample arrays, plus
array metadata, the model buffers and report raster. This is a model admission bound, not a
process-isolation or resource-quota mechanism. Field rasterization samples grids finer than
the panel at deterministic pixel centers; it does not promise to display every fine-grid cell.

## Heatmap meaning

All panels use the same declared range, from purple at the lower bound to yellow at the upper
bound. A field value therefore keeps the same color across time. Report HTML states the channel,
range and grid dimensions. Finite values outside the display range use the nearest endpoint
color, and nonfinite display samples are gray. **Color mapping never modifies model state.**
Gray–Scott uses a display range of 0 to 1, while its numerical contract may retain valid
concentrations above 1; those values remain visible in its raw metrics and full-state hash.

Images retain the existing eight-panel layout, tick labels and final-panel thumbnail format.
They are written through the run's staging directory with the report and metrics. A failed
observation or render publishes no completed run. Plain and visual runs must produce identical
deterministic CSV and model-state hashes.

## Boundaries

This is a bounded scalar observation seam, not a dynamic universal state schema or checkpoint
format. Multiple simultaneous heatmaps, vector/tensor fields, irregular meshes, 3D volumes,
brush interventions, cross-model field exchange and native interactive field controls remain
separate extensions. The model owns its boundary rules and units; the renderer only projects
the samples it receives.

`FieldVisualTests` checks actual pixels, fixed-scale behavior, invalid dimensions/ranges,
nonfinite display semantics and unchanged input bits. `FieldRecorderTests` checks selected-tick
ownership with no invented agents. Reaction–diffusion CLI tests cover zero-agent preflight,
field metrics, full-state replay and plain/visual equality through real run publication.
