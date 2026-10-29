using System.Diagnostics;
using System.Globalization;
using SwarmSim.Cli;
using SwarmSim.Core.Metrics;
using Xunit.Abstractions;

namespace SwarmSim.Tests;

public sealed class MotionMetricSelectionTests(ITestOutputHelper output)
{
    private static readonly string[] BasicChannels = ["population", "meanSpeed", "polarization"];

    [Fact]
    public void DefaultConstructorRetainsFiveChannels_AndBasicModeDeclaresOnlyItsThreeOutputs()
    {
        // Keep the real parameterless constructor, including for already compiled callers.
        Assert.NotNull(typeof(MotionMetricProbe).GetConstructor(Type.EmptyTypes));
        Assert.Equal(new[] { "population", "meanSpeed", "polarization", "centroidX", "centroidY" },
            new MotionMetricProbe().Channels);
        var basic = new MotionMetricProbe(includeCentroids: false);
        Assert.Equal(BasicChannels, basic.Channels);
        var values = new[] { -1d, -1d, -1d, 123d, 456d };

        basic.Sample(new MetricFrame([], MetricDomain.Euclidean), values);

        Assert.Equal(new[] { 0d, 0d, 0d, 123d, 456d }, values);
        Assert.Throws<ArgumentException>(() => basic.Sample(new MetricFrame([], MetricDomain.Euclidean), new double[2]));
        var registry = new MetricRegistry();
        registry.Register(basic);
        Assert.Throws<ArgumentException>(() => registry.CreatePlan("centroidX"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BasicOutputsMatchFullProbeBits_ForEmptyStationaryExtremeAndOrdinaryFrames(bool periodic)
    {
        var basic = new MotionMetricProbe(includeCentroids: false);
        var full = new MotionMetricProbe();
        var basicValues = new double[3];
        var fullValues = new double[5];
        MetricDomain domain = periodic ? MetricDomain.Toroidal(10f, 20f) : MetricDomain.Euclidean;
        KinematicSample[][] scenes =
        [
            [],
            [new(0f, 0f, 0f, 0f)],
            [new(1f, -2f, float.Epsilon, -float.Epsilon)],
            [new(0f, 0f, 3f, 0f), new(3f, 1f, 0f, 4f), new(6f, -7f, 0f, 0f)],
            [new(-0.1f, 19.75f, 0.3f, -0.2f), new(0.1f, 0.25f, -0.4f, 1f)],
            [new(float.MaxValue, -float.MaxValue, float.MaxValue, float.MaxValue),
             new(-float.MaxValue, float.MaxValue, -float.MaxValue, -float.MaxValue)]
        ];

        foreach (KinematicSample[] scene in scenes)
        {
            var frame = new MetricFrame(scene, domain);
            basic.Sample(frame, basicValues);
            full.Sample(frame, fullValues);
            for (int i = 0; i < basicValues.Length; i++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(fullValues[i]), BitConverter.DoubleToInt64Bits(basicValues[i]));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BasicAndFullPlans_KeepReorderedCsvBytesIdentical(bool periodic)
    {
        string[] requested = ["polarization", "population", "meanSpeed"];
        MetricPlan basic = MakePlan(includeCentroids: false, requested);
        MetricPlan full = MakePlan(includeCentroids: true, requested);
        var frame = new MetricFrame(
            [new(9.75f, -0.1f, 3f, 0f), new(0.25f, 0.1f, 0f, 4f), new(5f, 0f, 0f, 0f)],
            periodic ? MetricDomain.Toroidal(10f, 10f) : MetricDomain.Euclidean);
        using var basicText = new StringWriter(new CultureInfo("fr-FR"));
        using var fullText = new StringWriter(new CultureInfo("fr-FR"));
        var basicCsv = new MetricCsvWriter(basicText, basic);
        var fullCsv = new MetricCsvWriter(fullText, full);
        var values = new double[3];

        basic.Sample(frame, values);
        basicCsv.WriteRow(7, values);
        full.Sample(frame, values);
        fullCsv.WriteRow(7, values);

        Assert.Equal(fullText.ToString(), basicText.ToString());
        Assert.StartsWith("tick,polarization,population,meanSpeed\n7,", basicText.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("centroidX", 3)]
    [InlineData("centroidY", 4)]
    public void CatalogRetainsEitherIndividuallyRequestedCentroidAndColumnOrder(string channel, int fullIndex)
    {
        MetricPlan plan = MetricCatalog.CreatePlan("canonical", [channel, "population", "meanSpeed"], 0,
            null, new RuntimeCostMetricProbe());
        var frame = new MetricFrame([new(2f, 3f, 3f, 4f), new(4f, 7f, 0f, 0f)], MetricDomain.Toroidal(10f, 20f));
        var expected = new double[5];
        var actual = new double[3];
        new MotionMetricProbe().Sample(frame, expected);

        plan.Sample(frame, actual);

        Assert.Equal(new[] { channel, "population", "meanSpeed" }, plan.Channels);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[fullIndex]), BitConverter.DoubleToInt64Bits(actual[0]));
        Assert.Equal(expected[0], actual[1]);
        Assert.Equal(expected[1], actual[2]);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void BasicAndFullPeriodicPlans50k_ReuseScratchAndReportSamplingCost()
    {
        const int count = 50_000;
        const int warmup = 100;
        const int samples = 1000;
        var agents = new KinematicSample[count];
        for (int i = 0; i < agents.Length; i++)
            agents[i] = new((i * 17) % 100_000, (i * 31) % 100_000, (i % 7) - 3f, (i % 11) - 5f);
        var frame = new MetricFrame(agents, MetricDomain.Toroidal(100_000f, 100_000f));
        MetricPlan basic = MakePlan(includeCentroids: false, BasicChannels);
        MetricPlan full = MakePlan(includeCentroids: true, BasicChannels);
        var basicValues = new double[3];
        var fullValues = new double[3];
        for (int i = 0; i < warmup; i++)
        {
            basic.Sample(frame, basicValues);
            full.Sample(frame, fullValues);
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int i = 0; i < samples; i++) basic.Sample(frame, basicValues);
        double basicMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
        long basicAllocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        started = Stopwatch.GetTimestamp();
        for (int i = 0; i < samples; i++) full.Sample(frame, fullValues);
        double fullMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
        long fullAllocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(0L, basicAllocated);
        Assert.Equal(0L, fullAllocated);
        for (int i = 0; i < basicValues.Length; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(fullValues[i]), BitConverter.DoubleToInt64Bits(basicValues[i]));
        output.WriteLine($"Same periodic frame; agents={count}; warmup={warmup}; samples={samples}; selected=population,meanSpeed,polarization");
        output.WriteLine($"Basic probe plan: {basicMilliseconds:F3}ms, {basicAllocated} current-thread bytes; full probe plan: {fullMilliseconds:F3}ms, {fullAllocated} bytes.");
        output.WriteLine("Sampling only: setup, frame validation, engine stepping, capture and I/O excluded. Timing is an observation, not a throughput assertion.");
    }

    private static MetricPlan MakePlan(bool includeCentroids, string[] requested)
    {
        var registry = new MetricRegistry();
        registry.Register(new MotionMetricProbe(includeCentroids));
        return registry.CreatePlan(requested);
    }
}
