namespace SwarmSim.Cli;

/// <summary>
/// Optional model-chosen scalar projection for field reports, independent of kinematic agents.
/// Dimensions and the finite, increasing display range remain fixed during a recording. Capture
/// copies FieldColumns * FieldRows values in row-major order into the destination prefix, throws
/// before writing if the buffer is too short, and leaves any remaining destination values alone.
/// It must not step the model, consume randomness, normalize values, or mutate simulation state.
/// Values outside the display range and nonfinite values are retained in the captured data;
/// the renderer alone chooses how to display them.
/// </summary>
internal interface IFieldSnapshotSource
{
    int FieldColumns { get; }
    int FieldRows { get; }
    string FieldLabel { get; }
    float ColorMinimum { get; }
    float ColorMaximum { get; }
    void CaptureField(Span<float> destination);
}
