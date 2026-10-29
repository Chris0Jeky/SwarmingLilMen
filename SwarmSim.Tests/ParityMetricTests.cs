using SwarmSim.Core.Metrics;
using SwarmSim.Parity;

namespace SwarmSim.Tests;

public sealed class ParityMetricTests
{
    [Fact]
    public void AnalysisUsesPeriodicDistancesAndDefinedTurnPercentiles()
    {
        var probe = new ParityGeometryProbe(3, 2, 0.5f);
        probe.SetPrevious(new[] { new KinematicSample(1,5,0,-1), new(9,5,1,0), new(5,5,0,1) });
        var frame = new MetricFrame(new[] { new KinematicSample(1,5,1,0), new(9,5,1,0), new(5,5,1,0) }, MetricDomain.Toroidal(10,10));
        double[] values = new double[probe.Channels.Count];
        probe.Sample(frame, values);
        Assert.Equal(0, values[0], 12);
        Assert.Equal(2 * Math.PI / 3, values[1], 12);
        Assert.Equal(Math.PI, values[2], 12);
        Assert.Equal(Math.PI, values[3], 12);
        Assert.Equal(3, values[4], 12);
        Assert.Equal(2d / 3, values[5], 12);
        Assert.Equal(1, values[6]);
        Assert.Equal(1, values[7]);
        Assert.Equal(0, values[8]);
        Assert.Equal(1, values[9]);
        Assert.Equal(1, values[10]);
    }

    [Fact]
    public void UndefinedCircularCenterIsNotReportedAsZeroRadius()
    {
        KinematicSample[] agents = { new(0,5,1,0), new(2.5f,5,1,0), new(5,5,1,0), new(7.5f,5,1,0) };
        var probe = new ParityGeometryProbe(4, 2, 1);
        probe.SetPrevious(agents);
        double[] output = new double[probe.Channels.Count];
        probe.Sample(new MetricFrame(agents, MetricDomain.Toroidal(10,10)), output);
        Assert.True(double.IsNaN(output[4]));
    }

    [Fact]
    public void UnpreparedOrChangedIdentityPopulationCannotProduceTurnStatistics()
    {
        var probe = new ParityGeometryProbe(2, 2, 1);
        double[] values = new double[probe.Channels.Count];
        Assert.Throws<InvalidOperationException>(() => probe.Sample(
            new MetricFrame(new[] { new KinematicSample(1,1,1,0) }, MetricDomain.Toroidal(10,10)), values));
        probe.SetPrevious(new[] { new KinematicSample(1,1,1,0), new(2,2,1,0) });
        Assert.Throws<InvalidOperationException>(() => probe.Sample(
            new MetricFrame(new[] { new KinematicSample(1,1,1,0) }, MetricDomain.Toroidal(10,10)), values));
    }
}
