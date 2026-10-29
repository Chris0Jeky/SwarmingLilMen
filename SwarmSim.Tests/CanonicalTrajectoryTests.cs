using SwarmSim.Benchmarks;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

public sealed class CanonicalTrajectoryTests
{
    [Fact]
    public void ExperimentRejectsExtraArgumentsBeforeLaunchingAnyBenchmarks()
    {
        Assert.Throws<ArgumentException>(() => Program.Main(new[] { "--canonical-experiment", "--extra" }));
    }

    [Theory]
    [InlineData(0, 1701u)]
    [InlineData(1, 1701u)]
    [InlineData(2, 1701u)]
    [InlineData(3, 7919u)]
    public void EveryCanonicalFrameMatchesNaiveAndRepeats(int workload, uint seed)
    {
        var scenario = (CanonicalExperiment.Workload)workload;
        var first = CanonicalExperiment.CompareTrajectory(scenario, seed, 96, 24);
        var second = CanonicalExperiment.CompareTrajectory(scenario, seed, 96, 24, naiveFirst: true);
        Assert.Equal(first.Hash, second.Hash);
        Assert.True(first.VisibleNeighbors > 0);
        if (scenario == CanonicalExperiment.Workload.Capped) Assert.Equal(96 * 24, first.TruncatedQueries);
    }

    [Fact]
    public void OracleRejectsAnIntentionallyPerturbedTrajectory()
    {
        var left = CanonicalExperiment.CreateWorld(CanonicalExperiment.Workload.Seam, 1701, 16, grid: true);
        var right = CanonicalExperiment.CreateWorld(CanonicalExperiment.Workload.Seam, 1701, 16, grid: false);
        right.SetVelocity(0, new Vec2(0, 1));
        Assert.Throws<InvalidOperationException>(() => CanonicalExperiment.AssertSameState(left, right));
    }

    [Fact]
    public void MovingCapAcrossFovChangesNeighborsAndSteering()
    {
        Assert.Equal((0, 1), CanonicalExperiment.CapBeforeFovWitness());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    public void DescendingDenseCandidatesReturnLowestIndicesAndExactTruncation(int capacity)
    {
        var boids = new Boid[33];
        Array.Fill(boids, new Boid(new Vec2(10, 10), new Vec2(1, 0)));
        var grid = new GridSpatialIndex(10, 100, 100);
        grid.Initialize(boids.Length);
        grid.Rebuild(boids);
        var ids = new int[capacity];
        var result = grid.QueryNeighbors(boids, 0, 0, ids);
        Assert.Equal(Math.Min(capacity, 32), result.Count);
        Assert.Equal(capacity < 32, result.IsTruncated);
        Assert.Equal(Enumerable.Range(1, result.Count), ids.Take(result.Count));
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    public void DenseSelectionReducesWritesWithoutAllocating(int count)
    {
        var result = CanonicalSelectionExperiment.MeasureDenseSelection(count, 32);
        Assert.True(result.HeapWrites * 2 < result.SortedWrites);
        Assert.Equal(0, result.GridQueryBytes);
        Assert.Equal(0, result.NaiveQueryBytes);
    }
}
