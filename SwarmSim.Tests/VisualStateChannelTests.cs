using SwarmSim.Cli;
using SwarmSim.Cli.Visual;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Tests;

public sealed class VisualStateChannelTests
{
    [Fact]
    public void StationaryAgentsCanShowCircularStateWithoutInventingVelocity()
    {
        KinematicSample[] agents = [new(0.5f, 0.5f, 0f, 0f)];
        Raster first = Filmstrip.Render([0], [agents], 1f, 1f, [new[] { 0f }]);
        Raster second = Filmstrip.Render([0], [agents], 1f, 1f, [new[] { MathF.PI }]);
        Raster ordinary = Filmstrip.Render([0], [agents], 1f, 1f);
        int x = Filmstrip.Gap + (Filmstrip.PanelWidth - 1) / 2;
        int y = Filmstrip.Gap + (Filmstrip.PanelHeight(1f, 1f) - 1) / 2;

        Assert.Equal(Filmstrip.HeadingColor(0d), Pixel(first, x, y));
        Assert.Equal(Filmstrip.HeadingColor(MathF.PI), Pixel(second, x, y));
        Assert.Equal(Filmstrip.Stationary, Pixel(ordinary, x, y));
        Assert.NotEqual(first.Rgba, second.Rgba);
        Assert.Equal(0f, agents[0].Vx);
        Assert.Equal(0f, agents[0].Vy);
    }

    [Fact]
    public void ColorStateMustMatchEveryFrameAndAgent()
    {
        KinematicSample[] agents = [new(0.5f, 0.5f, 0f, 0f)];
        Assert.Throws<ArgumentException>(() => Filmstrip.Render([0], [agents], 1f, 1f, []));
        Assert.Throws<ArgumentException>(() => Filmstrip.Render([0], [agents], 1f, 1f, [Array.Empty<float>()]));
    }

    [Fact]
    public void NonfiniteColorValuesRemainVisibleAsStationaryMarkers()
    {
        Raster raster = Filmstrip.Render([0], [new[] { new KinematicSample(0.5f, 0.5f, 0f, 0f) }], 1f, 1f,
            [new[] { float.NaN }]);
        int middle = Filmstrip.Gap + (Filmstrip.PanelWidth - 1) / 2;
        Assert.Equal(Filmstrip.Stationary, Pixel(raster, middle, middle));
    }

    [Fact]
    public void ColorCaptureUsesOnlySelectedTicksAndCopiesTheObservedValues()
    {
        var source = new ColorSource { Value = 1f };
        var recorder = new FrameRecorder(100, 1f, 1f, source);
        KinematicSample[] agents = [new(0.5f, 0.5f, 0f, 0f)];
        recorder.Capture(0, agents);
        source.Value = 2f;
        recorder.Capture(1, agents);
        Assert.Equal(1, source.Calls);
        recorder.Capture(recorder.Ticks[1], agents);

        Assert.Equal(2, source.Calls);
        Assert.Equal(1f, recorder.ColorAngles![0][0]);
        Assert.Equal(2f, recorder.ColorAngles[1][0]);
        Assert.Equal("phase", recorder.ColorAngleLabel);
    }

    private static (byte, byte, byte) Pixel(Raster raster, int x, int y)
    {
        int offset = (y * raster.Width + x) * 4;
        return (raster.Rgba[offset], raster.Rgba[offset + 1], raster.Rgba[offset + 2]);
    }

    private sealed class ColorSource : IAgentColorSource
    {
        internal float Value { get; set; }
        internal int Calls { get; private set; }
        public string ColorAngleLabel => "phase";
        public void CaptureColorAngles(Span<float> destination)
        {
            Calls++;
            destination.Fill(Value);
        }
    }
}
