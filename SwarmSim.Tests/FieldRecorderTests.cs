using SwarmSim.Cli;
using SwarmSim.Cli.Visual;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Tests;

public sealed class FieldRecorderTests
{
    [Fact]
    public void FieldSnapshotsAreIndependentCopiesTakenOnlyAtSelectedTicksWithNoAgents()
    {
        var source = new FieldSource();
        var recorder = new FrameRecorder(100, 2f, 2f, fieldSource: source);
        recorder.Capture(0, ReadOnlySpan<KinematicSample>.Empty);
        source.Value = 0.75f;
        recorder.Capture(1, ReadOnlySpan<KinematicSample>.Empty);
        Assert.Equal(1, source.Calls);
        recorder.Capture(recorder.Ticks[1], ReadOnlySpan<KinematicSample>.Empty);

        Assert.Equal(2, source.Calls);
        Assert.Empty(recorder.Frames[0]);
        Assert.Equal(new[] { 0.25f, 0.25f, 0.25f, 0.25f }, recorder.FieldFrames![0]);
        Assert.Equal(new[] { 0.75f, 0.75f, 0.75f, 0.75f }, recorder.FieldFrames[1]);
        Assert.Equal(new FieldProjection(2, 2, "V concentration", 0f, 1f), recorder.Field);
        Assert.Null(recorder.ColorAngles);
    }

    [Theory]
    [InlineData(0, 2, 0f, 1f)]
    [InlineData(int.MaxValue, 2, 0f, 1f)]
    [InlineData(2, 2, 1f, 1f)]
    [InlineData(2, 2, 2f, 1f)]
    [InlineData(2, 2, float.NaN, 1f)]
    [InlineData(2, 2, 0f, float.PositiveInfinity)]
    public void InvalidFieldMetadataFailsBeforeSnapshotAllocation(int columns, int rows, float minimum, float maximum)
    {
        var source = new FieldSource { FieldColumns = columns, FieldRows = rows, ColorMinimum = minimum, ColorMaximum = maximum };
        Assert.Throws<ArgumentException>(() => new FrameRecorder(10, 1f, 1f, fieldSource: source));
        Assert.Equal(0, source.Calls);
    }

    private sealed class FieldSource : IFieldSnapshotSource
    {
        public int FieldColumns { get; init; } = 2;
        public int FieldRows { get; init; } = 2;
        public string FieldLabel => "V concentration";
        public float ColorMinimum { get; init; }
        public float ColorMaximum { get; init; } = 1f;
        internal float Value { get; set; } = 0.25f;
        internal int Calls { get; private set; }
        public void CaptureField(Span<float> destination)
        {
            Calls++;
            destination.Fill(Value);
        }
    }
}
