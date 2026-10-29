using System.Globalization;
using System.Text.Json;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Metrics;

namespace SwarmSim.Tests;

public sealed class MetricsContractTests
{
    [Fact]
    public void MotionChannels_UseAllAgentsForThePolarizationDenominator()
    {
        KinematicSample[] agents = [new(0f, 0f, 3f, 0f), new(3f, 0f, 0f, 4f), new(0f, 3f, 0f, 0f)];
        var registry = new MetricRegistry();
        registry.Register(new MotionMetricProbe());
        MetricPlan plan = registry.CreatePlan("population", "meanSpeed", "polarization", "centroidX", "centroidY");
        var values = new double[plan.ChannelCount];

        plan.Sample(new MetricFrame(agents, MetricDomain.Euclidean), values);

        Assert.Equal(3d, values[0]);
        Assert.Equal(7d / 3d, values[1], precision: 12);
        Assert.Equal(Math.Sqrt(2d) / 3d, values[2], precision: 12);
        Assert.Equal(1d, values[3]);
        Assert.Equal(1d, values[4]);
        Assert.True(plan.IsDeterministic);
    }

    [Fact]
    public void EmptyFrame_HasZeroMotionAndUndefinedCentroid()
    {
        var probe = new MotionMetricProbe();
        var values = new double[5];
        probe.Sample(new MetricFrame([], MetricDomain.Euclidean), values);

        Assert.Equal(new[] { 0d, 0d, 0d }, values.Take(3));
        Assert.True(double.IsNaN(values[3]));
        Assert.True(double.IsNaN(values[4]));
    }

    [Fact]
    public void ToroidalCentroid_UsesCircularMeansAndMarksAntipodalAmbiguity()
    {
        var probe = new MotionMetricProbe();
        var values = new double[5];
        MetricDomain domain = MetricDomain.Toroidal(10f, 10f);
        probe.Sample(new MetricFrame([new(0.25f, 2f, 0f, 0f), new(9.75f, 2f, 0f, 0f)], domain), values);

        Assert.InRange(Math.Min(values[3], 10d - values[3]), 0d, 1e-12);
        Assert.Equal(2d, values[4], precision: 12);
        Assert.Equal(0d, values[2]);

        probe.Sample(new MetricFrame([new(0f, 2f, 1f, 0f), new(5f, 2f, -1f, 0f)], domain), values);
        Assert.True(double.IsNaN(values[3]));
        Assert.Equal(2d, values[4], precision: 12);
        Assert.Equal(0d, values[2]);
    }

    [Fact]
    public void ExactNeighborDistribution_HasDocumentedInterpolatedPercentiles()
    {
        var probe = new ExactNearestNeighborProbe(capacity: 4);
        var values = new double[6];
        KinematicSample[] agents = [new(0, 0, 0, 0), new(2, 0, 0, 0), new(5, 0, 0, 0), new(9, 0, 0, 0)];

        probe.Sample(new MetricFrame(agents, MetricDomain.Euclidean), values);

        Assert.Equal(new[] { 2d, 2d, 3d, 4d }, probe.NearestDistances.ToArray());
        Assert.Equal(new[] { 2d, 2d, 3d, 4d }, probe.SortedNearestDistances.ToArray());
        Assert.Equal(4d, values[0]);
        Assert.Equal(2.75d, values[1]);
        Assert.Equal(2d, values[2]);
        Assert.Equal(2.5d, values[3]);
        Assert.Equal(3.85d, values[4], precision: 12);
        Assert.Equal(4d, values[5]);
    }

    [Fact]
    public void ExactNeighbors_NormalizePeriodicCoordinatesAndIncludeCoincidentPeers()
    {
        var probe = new ExactNearestNeighborProbe(3);
        var values = new double[6];
        probe.Sample(new MetricFrame([new(20.25f, 0, 0, 0), new(-0.25f, 0, 0, 0), new(5, 0, 0, 0)], MetricDomain.Toroidal(10, 10)), values);

        Assert.Equal(new[] { 0.5d, 0.5d, 4.75d }, probe.NearestDistances.ToArray());
        Assert.Equal(4.325d, values[4], precision: 12);

        probe.Sample(new MetricFrame([new(1, 0, 0, 0), new(1, 0, 0, 0), new(4, 0, 0, 0)], MetricDomain.Euclidean), values);
        Assert.Equal(new[] { 0d, 0d, 3d }, probe.NearestDistances.ToArray());
        Assert.Equal(1d, values[1]);
        Assert.Equal(0d, values[3]);
    }

    [Theory]
    [InlineData(1e15f, 0.1f)]
    [InlineData(10f, float.Epsilon)]
    public void PeriodicNearestNeighbors_PreserveSmallDistancesAcrossTheOrigin(float extent, float offset)
    {
        var probe = new ExactNearestNeighborProbe(2);
        var values = new double[6];
        MetricDomain domain = MetricDomain.Toroidal(extent, extent);
        double expected = 2d * offset;

        probe.Sample(new MetricFrame([new(-offset, 0, 0, 0), new(offset, 0, 0, 0)], domain), values);
        Assert.Equal(expected, values[1]);
        probe.Sample(new MetricFrame([new(0, -offset, 0, 0), new(0, offset, 0, 0)], domain), values);
        Assert.Equal(expected, values[1]);
    }

    [Fact]
    public void PeriodicNearestNeighbors_ReduceHugeOffsetsBeforeSubtractingSmallCoordinates()
    {
        // float.MaxValue = 2^128 - 2^104 is an exact multiple of ten. Subtracting
        // the small coordinate before taking the remainder would lose that coordinate.
        var probe = new ExactNearestNeighborProbe(2);
        var values = new double[6];
        MetricDomain domain = MetricDomain.Toroidal(10f, 10f);

        probe.Sample(new MetricFrame([new(float.MaxValue, 0, 0, 0), new(-0.1f, 0, 0, 0)], domain), values);
        Assert.Equal((double)0.1f, values[1]);
        probe.Sample(new MetricFrame([new(0, -float.MaxValue, 0, 0), new(0, 0.1f, 0, 0)], domain), values);
        Assert.Equal((double)0.1f, values[1]);
        probe.Sample(new MetricFrame([new(4.75f, 0, 0, 0), new(-4.75f, 0, 0, 0)], domain), values);
        Assert.Equal(0.5d, values[1]);
    }

    [Fact]
    public void PeriodicCentroid_PreservesAsymmetricTinyClustersAcrossTheOrigin()
    {
        var probe = new MotionMetricProbe();
        var values = new double[5];
        const float extent = 1e15f;
        const float offset = 0.1f;

        probe.Sample(new MetricFrame(
            [new(-offset, -offset, 0, 0), new(offset, offset, 0, 0), new(offset, offset, 0, 0)],
            MetricDomain.Toroidal(extent, extent)), values);

        // At this angular scale the circular-vs-arithmetic mean correction is < 1e-32.
        Assert.Equal((double)offset / 3d, values[3], precision: 15);
        Assert.Equal((double)offset / 3d, values[4], precision: 15);
    }

    [Fact]
    public void ExactNeighbors_EmptyAndSingletonHaveNoDistances_AndCapacityNeverTruncates()
    {
        var probe = new ExactNearestNeighborProbe(1);
        var values = new double[6];
        probe.Sample(new MetricFrame([], MetricDomain.Euclidean), values);
        Assert.Equal(0d, values[0]);
        Assert.All(values.Skip(1), value => Assert.True(double.IsNaN(value)));
        Assert.Empty(probe.NearestDistances.ToArray());
        probe.Sample(new MetricFrame([new(1, 1, 0, 0)], MetricDomain.Euclidean), values);
        Assert.Equal(0d, values[0]);
        Assert.Empty(probe.NearestDistances.ToArray());
        Assert.Throws<ArgumentException>(() => probe.Sample(new MetricFrame([new(1, 1, 0, 0), new(2, 2, 0, 0)], MetricDomain.Euclidean), values));
    }

    [Fact]
    public void ExactAnalysis_IsIdenticalAcrossGridCellSizesAndNaiveIndex_DespiteSteeringTruncation()
    {
        ISpatialIndex[] indexes = [new GridSpatialIndex(1f, 10f, 10f), new GridSpatialIndex(4f, 10f, 10f), new NaiveSpatialIndex(10f, 10f)];
        float[] positions = [0.25f, 0.5f, 1f, 2f, 4f, 5f, 8f, 9.75f];
        double[] expected = [0.25d, 0.25d, 0.5d, 1d, 1d, 1d, 1.75d, 0.5d];
        var probe = new ExactNearestNeighborProbe(positions.Length);
        var values = new double[6];
        var queryBuffer = new int[1];
        foreach (ISpatialIndex index in indexes)
        {
            var world = new CanonicalWorld(new CanonicalWorldSettings
            {
                InitialCapacity = positions.Length, WorldWidth = 10f, WorldHeight = 10f,
                SenseRadius = 5f, MaxNeighbors = 4
            }, index);
            foreach (float x in positions) Assert.True(world.TryAddBoid(new Vec2(x, 1f), new Vec2(1f, 0f)));
            index.Rebuild(world.Boids);
            SpatialQueryResult truncated = index.QueryNeighbors(world.Boids, 0, 5f, queryBuffer);
            Assert.True(truncated.IsTruncated);
            Assert.Equal(1, truncated.Count);
            var agents = new KinematicSample[world.Count];
            for (int i = 0; i < world.Count; i++)
            {
                Boid boid = world.Boids[i];
                agents[i] = new(boid.Position.X, boid.Position.Y, boid.Velocity.X, boid.Velocity.Y);
            }

            probe.Sample(new MetricFrame(agents, MetricDomain.Toroidal(10f, 10f)), values);

            Assert.Equal(8d, values[0]);
            Assert.Equal(expected, probe.NearestDistances.ToArray());
        }
    }

    [Fact]
    public void ExactNeighborScratch_IsReusedWithoutAllocationsAtSmallOfflinePopulation()
    {
        var agents = new KinematicSample[32];
        for (int i = 0; i < agents.Length; i++) agents[i] = new(i, (i * 3) % 11, 1f, 0f);
        var probe = new ExactNearestNeighborProbe(agents.Length);
        var values = new double[6];
        for (int i = 0; i < 100; i++) probe.Sample(new MetricFrame(agents, MetricDomain.Toroidal(32f, 32f)), values);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) probe.Sample(new MetricFrame(agents, MetricDomain.Toroidal(32f, 32f)), values);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocated);
        Assert.Equal(32d, values[0]);
    }

    [Theory]
    [InlineData(0f, 10f)]
    [InlineData(10f, -1f)]
    [InlineData(float.NaN, 10f)]
    [InlineData(10f, float.PositiveInfinity)]
    public void InvalidPeriodicDomainIsRejected(float width, float height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MetricDomain.Toroidal(width, height));
    }

    [Fact]
    public void RegistrationRejectsUnsafeColumnsAtomically_AndShortOutputsDoNotSample()
    {
        var registry = new MetricRegistry();
        registry.Register(new MotionMetricProbe());
        Assert.Throws<ArgumentException>(() => registry.Register(new NamedProbe("new.channel", "population")));
        Assert.Throws<ArgumentException>(() => registry.CreatePlan("new.channel"));
        foreach (string name in new[] { "tick", "", "bad,name", "bad\nname", "bad\"name" })
            Assert.Throws<ArgumentException>(() => registry.Register(new NamedProbe(name)));
        registry.Register(new SequenceProbe());
        MetricPlan plan = registry.CreatePlan("custom.a", "custom.b");
        Assert.Throws<ArgumentException>(() => plan.Sample(new MetricFrame([], MetricDomain.Euclidean), new double[1]));
        var values = new double[2];
        plan.Sample(new MetricFrame([], MetricDomain.Euclidean), values);
        Assert.Equal(new[] { 10d, 20d }, values);
    }

    [Fact]
    public void Registry_PreservesRequestedColumnsAndSamplesEachProbeOnce()
    {
        var registry = new MetricRegistry();
        registry.Register(new SequenceProbe());
        string[] requested = ["custom.b", "custom.a"];
        MetricPlan plan = registry.CreatePlan(requested);
        requested[0] = "changed-after-plan";
        var values = new double[2];

        plan.Sample(new MetricFrame([], MetricDomain.Euclidean), values);

        Assert.Equal(new[] { "custom.b", "custom.a" }, plan.Channels);
        Assert.Equal(new[] { 20d, 10d }, values);
        Assert.Throws<ArgumentException>(() => registry.CreatePlan("custom.missing"));
        Assert.Throws<ArgumentException>(() => registry.CreatePlan("custom.a", "custom.a"));
        Assert.Throws<ArgumentException>(() => registry.Register(new SequenceProbe()));
    }

    [Theory]
    [InlineData(float.NaN, 0f, 1f, 0f)]
    [InlineData(0f, float.PositiveInfinity, 1f, 0f)]
    [InlineData(0f, 0f, float.NaN, 0f)]
    [InlineData(0f, 0f, 1f, float.NegativeInfinity)]
    public void NonFiniteFramesAreRejected(float x, float y, float vx, float vy)
    {
        Assert.Throws<ArgumentException>(() => { _ = new MetricFrame([new(x, y, vx, vy)], MetricDomain.Euclidean); });
    }

    [Fact]
    public void ExtremeFiniteCoordinatesAndVelocitiesStayFinite()
    {
        var motion = new MotionMetricProbe();
        var nearest = new ExactNearestNeighborProbe(2);
        var motionValues = new double[5];
        var nearestValues = new double[6];
        KinematicSample[] agents = [new(float.MaxValue, 0, float.MaxValue, float.MaxValue), new(-float.MaxValue, 0, -float.MaxValue, -float.MaxValue)];
        var frame = new MetricFrame(agents, MetricDomain.Euclidean);

        motion.Sample(frame, motionValues);
        nearest.Sample(frame, nearestValues);

        Assert.All(motionValues, value => Assert.True(double.IsFinite(value)));
        Assert.All(nearestValues, value => Assert.True(double.IsFinite(value)));
        Assert.Equal(0d, motionValues[2]);
        Assert.Equal(2d * float.MaxValue, nearestValues[1]);
    }

    [Fact]
    public void RuntimeCostsAreRecordedExplicitlyAndMarkedNondeterministic()
    {
        var runtime = new RuntimeCostMetricProbe();
        var registry = new MetricRegistry();
        registry.Register(runtime);
        MetricPlan plan = registry.CreatePlan("runtime.wallMilliseconds", "runtime.allocatedBytes");
        var values = new double[2];
        plan.Sample(new MetricFrame([], MetricDomain.Euclidean), values);
        Assert.All(values, value => Assert.True(double.IsNaN(value)));

        runtime.Record(12.5d, 32L);
        plan.Sample(new MetricFrame([], MetricDomain.Euclidean), values);

        Assert.False(plan.IsDeterministic);
        Assert.Equal(new[] { 12.5d, 32d }, values);
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Record(double.NaN, 0L));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Record(0d, -1L));
    }

    [Fact]
    public void BoidsProbeReadsExistingInstrumentationWithoutWorldChanges()
    {
        var instrumentation = new RuleInstrumentation(2);
        instrumentation.Prepare(2);
        instrumentation.RecordSeparation(0, 2f);
        instrumentation.RecordSeparation(1, 4f);
        instrumentation.RecordAlignment(0, 1f);
        instrumentation.RecordAlignment(1, 3f);
        instrumentation.RecordCohesion(0, 4f);
        instrumentation.RecordCohesion(1, 6f);
        instrumentation.RecordSteering(0, 9f);
        instrumentation.RecordSteering(1, 25f);
        instrumentation.RecordTruncation(1, true);
        var probe = new BoidsRuleMetricProbe(instrumentation);
        var values = new double[5];

        probe.Sample(new MetricFrame([new(0, 0, 1, 0), new(1, 0, 1, 0)], MetricDomain.Euclidean), values);

        Assert.Equal(new[] { 3d, 2d, 5d, 4d, 0.5d }, values);
        Assert.Throws<ArgumentException>(() => probe.Sample(new MetricFrame([], MetricDomain.Euclidean), values));
    }

    [Fact]
    public void WritersUseRequestedOrderInvariantNumbersAndExplicitMissingValues()
    {
        var registry = new MetricRegistry();
        registry.Register(new MotionMetricProbe());
        MetricPlan plan = registry.CreatePlan("centroidX", "meanSpeed");
        using var csvText = new StringWriter();
        using var jsonText = new StringWriter();
        var csv = new MetricCsvWriter(csvText, plan);
        var json = new MetricJsonlWriter(jsonText, plan);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            csv.WriteRow(7, [double.NaN, 1.25d]);
            json.WriteRow(7, [double.NaN, 1.25d]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Equal("tick,centroidX,meanSpeed\n7,,1.25\n", csvText.ToString());
        Assert.Equal("{\"tick\":7,\"centroidX\":null,\"meanSpeed\":1.25}\n", jsonText.ToString());
        using JsonDocument parsed = JsonDocument.Parse(jsonText.ToString());
        Assert.Equal(JsonValueKind.Null, parsed.RootElement.GetProperty("centroidX").ValueKind);
        Assert.Throws<ArgumentException>(() => csv.WriteRow(8, [1d, double.PositiveInfinity]));
        Assert.Equal("tick,centroidX,meanSpeed\n7,,1.25\n", csvText.ToString());
    }

    [Fact]
    public void FiftyThousandAgents_LinearMotionChannelsAndBothSinks_AllocateZeroAcrossThousandSamples()
    {
        var agents = new KinematicSample[50_000];
        for (int i = 0; i < agents.Length; i++) agents[i] = new(i % 100, i / 100, 3f, 4f);
        var registry = new MetricRegistry();
        registry.Register(new MotionMetricProbe());
        MetricPlan plan = registry.CreatePlan("population", "meanSpeed", "polarization", "centroidX", "centroidY");
        var values = new double[5];
        var csv = new MetricCsvWriter(TextWriter.Null, plan);
        var json = new MetricJsonlWriter(TextWriter.Null, plan);
        for (int tick = 0; tick < 100; tick++)
        {
            plan.Sample(new MetricFrame(agents, MetricDomain.Euclidean), values);
            csv.WriteRow(tick, values);
            json.WriteRow(tick, values);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int tick = 0; tick < 1000; tick++)
        {
            plan.Sample(new MetricFrame(agents, MetricDomain.Euclidean), values);
            csv.WriteRow(tick, values);
            json.WriteRow(tick, values);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocated);
        Assert.Equal(50_000d, values[0]);
        Assert.Equal(5d, values[1]);
    }

    private sealed class SequenceProbe : IMetricProbe
    {
        private int _sample;
        public IReadOnlyList<string> Channels { get; } = new[] { "custom.a", "custom.b" };
        public bool IsDeterministic => true;
        public void Sample(in MetricFrame frame, Span<double> output)
        {
            _sample++;
            output[0] = _sample * 10d;
            output[1] = _sample * 20d;
        }
    }

    private sealed class NamedProbe(params string[] names) : IMetricProbe
    {
        public IReadOnlyList<string> Channels => names;
        public bool IsDeterministic => true;
        public void Sample(in MetricFrame frame, Span<double> output) => output.Clear();
    }
}
