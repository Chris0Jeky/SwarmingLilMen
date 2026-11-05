namespace SwarmSim.Cli;

/// <summary>
/// Optional observed circular state for report coloring, independent of physical kinematics.
/// Implementations copy one angle in radians per captured agent in the same stable order.
/// This observation must not step the model or consume randomness. It is sampled only at visual
/// frame ticks, and never changes metrics, replay state or physical velocity.
/// </summary>
internal interface IAgentColorSource
{
    string ColorAngleLabel { get; }
    void CaptureColorAngles(Span<float> destination);
}
