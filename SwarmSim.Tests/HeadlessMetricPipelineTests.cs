using System.Diagnostics;
using System.Text;
using SwarmSim.Cli;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Metrics;
using Xunit.Abstractions;

namespace SwarmSim.Tests;

public sealed class HeadlessMetricPipelineTests(ITestOutputHelper output)
{
    private static readonly string[] LinearChannels =
    [
        "population", "meanSpeed", "polarization", "centroidX", "centroidY",
        "boids.separationMagnitudeMean", "boids.alignmentMagnitudeMean", "boids.cohesionMagnitudeMean",
        "boids.steeringMagnitudeMean", "boids.truncatedQueryFraction",
        "runtime.wallMilliseconds", "runtime.allocatedBytes"
    ];

    [Fact]
    public void CompletedRuleWrapper_DoesNotInventInitialObservations_OrHideLaterMismatches()
    {
        RunRequest request = CanonicalRequest(2, 100f, 10f, []);
        var simulation = new PreparedSimulation(request);
        var probe = new CompletedStepBoidsMetricProbe(simulation.Canonical!);
        var agents = new KinematicSample[2];
        Assert.Equal(2, simulation.Capture(agents));
        var values = new double[probe.Channels.Count];

        probe.Sample(new MetricFrame(agents, simulation.Domain), values);

        Assert.All(values, value => Assert.True(double.IsNaN(value)));
        Assert.Empty(simulation.Canonical!.Instrumentation.SteeringMagnitudesSquared.ToArray());
        simulation.Step();
        Assert.Throws<ArgumentException>(() => probe.Sample(new MetricFrame(agents.AsSpan(0, 1), simulation.Domain), values));
        Assert.Equal(2, simulation.Capture(agents));
        probe.Sample(new MetricFrame(agents, simulation.Domain), values);
        Assert.All(values, value => Assert.True(double.IsFinite(value)));
    }

    [Theory]
    [InlineData(BoundaryMode.Wrap, true)]
    [InlineData(BoundaryMode.Reflect, false)]
    [InlineData(BoundaryMode.Clamp, false)]
    public void LegacyCapture_UsesConfiguredTopologyAndRejectsTruncation(BoundaryMode boundary, bool periodic)
    {
        var config = new SimConfig { BoundaryMode = boundary, InitialCapacity = 4 };
        var simulation = new PreparedSimulation(new RunRequest("legacy", 1, 4, config, null, null));
        var agents = new KinematicSample[4];

        Assert.Equal(periodic, simulation.Domain.IsPeriodic);
        Assert.Equal(4, simulation.Capture(agents));
        Assert.Throws<ArgumentException>(() => simulation.Capture(new KinematicSample[3]));
        if (periodic)
        {
            Assert.Equal(config.WorldWidth, simulation.Domain.Width);
            Assert.Equal(config.WorldHeight, simulation.Domain.Height);
        }
    }

    [Fact]
    public void CapturePlanAndCsv_ReuseBuffersWithEveryRegisteredCanonicalChannel()
    {
        string[] channels = [.. LinearChannels, "nearestNeighborCount", "nearestNeighborMean", "nearestNeighborMin",
            "nearestNeighborP50", "nearestNeighborP95", "nearestNeighborMax"];
        RunRequest request = CanonicalRequest(32, 1000f, 100f, channels);
        var simulation = new PreparedSimulation(request);
        var observer = new ExperimentMetrics(request, simulation);
        var csv = new MetricCsvWriter(TextWriter.Null, observer.Plan);
        for (int i = 0; i < 100; i++)
        {
            observer.StepAndSample();
            csv.WriteRow(i, observer.Values);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            observer.StepAndSample();
            csv.WriteRow(i, observer.Values);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocated);
        Assert.Equal(32d, observer.Values[0]);
        Assert.Equal(32d, observer.Values[12]);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void SparseCanonical50k_SeparatesPhysicsObserverAndBufferedFileAllocations()
    {
        const int count = 50_000;
        const int warmup = 100;
        const int samples = 1000;
        RunRequest request = CanonicalRequest(count, 100_000f, 100f, LinearChannels);
        var simulation = new PreparedSimulation(request);
        var observer = new ExperimentMetrics(request, simulation);
        string path = Path.Combine(Path.GetTempPath(), $"swarm-metric-pipeline-{Guid.NewGuid():N}.csv");
        long pipelineBytes = 0L, physicsBytes = 0L, writerBytes = 0L;
        double physicsMilliseconds = 0d;
        try
        {
            using (var text = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None),
                new UTF8Encoding(false)))
            {
                var csv = new MetricCsvWriter(text, observer.Plan);
                for (int i = 0; i < warmup; i++)
                {
                    observer.StepAndSample();
                    csv.WriteRow(i + 1, observer.Values);
                }
                long started = Stopwatch.GetTimestamp();
                for (int i = 0; i < samples; i++)
                {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    observer.StepAndSample();
                    long sampled = GC.GetAllocatedBytesForCurrentThread();
                    pipelineBytes += sampled - before;
                    physicsMilliseconds += observer.Values[10];
                    physicsBytes += (long)observer.Values[11];
                    csv.WriteRow(warmup + i + 1, observer.Values);
                    writerBytes += GC.GetAllocatedBytesForCurrentThread() - sampled;
                }
                double pipelineMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                output.WriteLine($"50k sparse canonical; warmup={warmup}; measuredSteps={samples}; world=100000x100000; senseRadius=100; MaxNeighbors=32; channels={string.Join(',', LinearChannels)}");
                output.WriteLine($"Physics current-thread bytes={physicsBytes}; capture/frame/plan bytes={pipelineBytes - physicsBytes}; buffered CSV write bytes={writerBytes}");
                output.WriteLine($"Physics milliseconds={physicsMilliseconds:F3}; total measured pipeline milliseconds={pipelineMilliseconds:F3} (shared-host observation, not a throughput gate)");
                long beforeFlush = GC.GetAllocatedBytesForCurrentThread();
                text.Flush();
                long flushBytes = GC.GetAllocatedBytesForCurrentThread() - beforeFlush;
                output.WriteLine($"Final flush bytes={flushBytes}; file bytes={new FileInfo(path).Length}");
            }
            Assert.Equal(0L, pipelineBytes - physicsBytes);
            Assert.Equal(0L, writerBytes);
            Assert.Equal(count, observer.Values[0]);

            // Isolate the current motion probe's circular-centroid overhead on one real
            // captured frame. Both paths compute all five channels; neither advances physics.
            var captured = new KinematicSample[count];
            Assert.Equal(count, simulation.Capture(captured));
            var motion = new MotionMetricProbe();
            var values = new double[5];
            var euclidean = new MetricFrame(captured, MetricDomain.Euclidean);
            var periodic = new MetricFrame(captured, simulation.Domain);
            for (int i = 0; i < 100; i++)
            {
                motion.Sample(euclidean, values);
                motion.Sample(periodic, values);
            }
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < samples; i++) motion.Sample(euclidean, values);
            double euclideanMs = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            start = Stopwatch.GetTimestamp();
            for (int i = 0; i < samples; i++) motion.Sample(periodic, values);
            double periodicMs = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            output.WriteLine($"Motion-only 50k x {samples}: Euclidean={euclideanMs:F3}ms; periodic={periodicMs:F3}ms. Periodic trigonometry is currently paid even when only the three default columns are requested.");
        }
        finally { File.Delete(path); }
    }

    private static RunRequest CanonicalRequest(int count, float extent, float senseRadius, string[] metrics) =>
        new("canonical", 1, count, null, new CanonicalWorldSettings
        {
            InitialCapacity = count, WorldWidth = extent, WorldHeight = extent,
            SenseRadius = senseRadius, MaxNeighbors = 32, Seed = 7
        }, null) { RequestedMetrics = metrics };
}
