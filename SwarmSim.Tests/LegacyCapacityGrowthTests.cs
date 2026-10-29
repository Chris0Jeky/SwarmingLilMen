using SwarmSim.Core;
using SwarmSim.Core.Diagnostics;

namespace SwarmSim.Tests;

public sealed class LegacyCapacityGrowthTests
{
    [Fact]
    public void AddAgent_GrowsGeometricallyToCeilingWithoutResettingAnyExistingState()
    {
        var world = new World(Configuration(2, 5));
        Assert.Equal(0, world.AddAgent(10f, 11f));
        Assert.Equal(1, world.AddAgent(12f, 13f));
        world.Tick();
        for (int i = 0; i < world.Count; i++)
        {
            world.X[i] = 10f + i;
            world.Y[i] = 20f + i;
            world.Vx[i] = 0.25f + i;
            world.Vy[i] = -0.5f - i;
            world.Fx[i] = 1.25f + i;
            world.Fy[i] = -1.5f - i;
            world.Energy[i] = 31f + i;
            world.Health[i] = 41f + i;
            world.Age[i] = 51f + i;
            world.Group[i] = (byte)(i + 1);
            world.State[i] = i == 0 ? AgentState.Dead : AgentState.Hunting | AgentState.Injured;
            world.Genomes[i] = new Genome(0.75f + i, 0.5f + i, -0.25f + i, (byte)(i + 4));
            world.LastAttackTime[i] = 61f + i;
        }
        Dictionary<string, Array> before = CopyArrays(world);
        float[] oldPositions = world.X;
        var grid = world.Grid;
        world.Grid.Rebuild(world.X, world.Y, world.State, world.Count);
        int[] oldQuery = QueryAll(world);
        ulong tick = world.TickCount;
        float time = world.SimulationTime;

        Assert.Equal(2, world.AddAgent(14f, 15f, group: 3));

        Assert.Equal(4, world.Capacity);
        Assert.Equal(3, world.Count); // Growing never compacts a dead slot or changes an existing index.
        Assert.NotSame(oldPositions, world.X);
        Assert.Same(grid, world.Grid);
        Assert.Equal(oldQuery, QueryAll(world)); // Existing index remains usable until the next rebuild.
        AssertPrefix(before, world, 2);
        Assert.Equal(tick, world.TickCount);
        Assert.Equal(time, world.SimulationTime);
        AssertArraysMatchCapacity(world);
        Assert.Equal(world.Config.InitialEnergy, world.Energy[2]);
        Assert.Equal(world.Config.InitialHealth, world.Health[2]);
        Assert.Equal(float.NegativeInfinity, world.LastAttackTime[2]);
        Assert.Equal(AgentState.None, world.State[2]);

        float[] afterFirstGrowth = world.X;
        Assert.Equal(3, world.AddAgent(16f, 17f));
        Assert.Same(afterFirstGrowth, world.X);
        Dictionary<string, Array> beforeSecondGrowth = CopyArrays(world);
        Assert.Equal(4, world.AddAgent(18f, 19f));
        Assert.Equal(5, world.Capacity); // The final geometric step is clamped to the exact ceiling.
        AssertPrefix(beforeSecondGrowth, world, 4);
        AssertArraysMatchCapacity(world);
        world.Grid.Rebuild(world.X, world.Y, world.State, world.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, QueryAll(world).Order().ToArray());
    }

    [Fact]
    public void GrowthAndCompactionMatchAPreallocatedWorldThroughLaterWanderingTicks()
    {
        var growing = new World(Configuration(2, 9));
        var preallocated = new World(Configuration(9, 9));
        for (int phase = 0; phase < 3; phase++)
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(preallocated.AddRandomAgent(), growing.AddRandomAgent());
                Assert.Equal(preallocated.Count, growing.Count);
            }
            for (int tick = 0; tick < 40; tick++)
            {
                preallocated.Tick();
                growing.Tick();
                Assert.Equal(SimulationKinematicHash.Compute(preallocated), SimulationKinematicHash.Compute(growing));
            }
            AssertPrefix(CopyArrays(preallocated), growing, growing.Count);
            Assert.Equal(QueryAll(preallocated), QueryAll(growing));
        }
        growing.MarkDead(2);
        preallocated.MarkDead(2);
        Assert.Equal(1, growing.CompactDeadAgents());
        Assert.Equal(1, preallocated.CompactDeadAgents());
        Assert.Equal(preallocated.AddRandomAgent(group: 2), growing.AddRandomAgent(group: 2));
        for (int tick = 0; tick < 40; tick++)
        {
            preallocated.Tick();
            growing.Tick();
        }
        AssertPrefix(CopyArrays(preallocated), growing, growing.Count);
        Assert.Equal(preallocated.Rng.Next(), growing.Rng.Next());
        Assert.Equal(9, growing.Capacity);
    }

    [Fact]
    public void CircleSpawnGrowsToMaximumAndStopsBeforeUnneededRandomDraws()
    {
        var growing = new World(Configuration(2, 5));
        var preallocated = new World(Configuration(5, 5));

        Assert.Equal(5, growing.SpawnAgentsInCircle(32f, 32f, 12f, int.MaxValue, group: 2));
        Assert.Equal(5, preallocated.SpawnAgentsInCircle(32f, 32f, 12f, 5, group: 2));

        Assert.Equal(5, growing.Count);
        Assert.Equal(5, growing.Capacity);
        AssertPrefix(CopyArrays(preallocated), growing, 5);
        Assert.Equal(preallocated.Rng.Next(), growing.Rng.Next());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CeilingFailuresLeaveArraysPopulationTimeGridAndRandomSequenceUnchanged(int initialCapacity)
    {
        var world = new World(Configuration(initialCapacity, 2));
        var control = new World(Configuration(2, 2));
        for (int i = 0; i < 2; i++)
        {
            world.AddRandomAgent();
            control.AddRandomAgent();
        }
        world.Tick();
        control.Tick();
        Dictionary<string, Array> references = Arrays(world);
        Dictionary<string, Array> contents = CopyArrays(world);
        var grid = world.Grid;
        int[] query = QueryAll(world);
        ulong tick = world.TickCount;
        float time = world.SimulationTime;

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        int directResult = world.AddAgent(1f, 2f);
        int randomResult = world.AddRandomAgent();
        int circleResult = world.SpawnAgentsInCircle(32f, 32f, 5f, 100);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(-1, directResult);
        Assert.Equal(-1, randomResult);
        Assert.Equal(0, circleResult);
        Assert.Equal(0L, allocated);

        Assert.Equal(2, world.Count);
        Assert.Equal(2, world.Capacity);
        Assert.Equal(tick, world.TickCount);
        Assert.Equal(time, world.SimulationTime);
        Assert.Same(grid, world.Grid);
        Assert.Equal(query, QueryAll(world));
        AssertPrefix(contents, world, 2);
        foreach ((string name, Array array) in Arrays(world)) Assert.Same(references[name], array);
        Assert.Equal(control.Rng.Next(), world.Rng.Next());
    }

    [Fact]
    public void NonFiniteAgentPositionDoesNotGrowFullInitialStorage()
    {
        var world = new World(Configuration(1, 4));
        world.AddAgent(10f, 20f);
        Dictionary<string, Array> references = Arrays(world);

        Assert.Equal(-1, world.AddAgent(float.NaN, 1f));
        Assert.Equal(-1, world.AddAgent(1f, float.PositiveInfinity));

        Assert.Equal(1, world.Count);
        Assert.Equal(1, world.Capacity);
        foreach ((string name, Array array) in Arrays(world)) Assert.Same(references[name], array);
    }

    [Fact]
    public void SnapshotsStayCopiedAndMutationNotificationSeparatesGrowthFromPriorCapture()
    {
        var world = new World(Configuration(1, 3));
        world.AddAgent(10f, 20f);
        var runner = new SimulationRunner(world);
        runner.Advance(world.Config.FixedDeltaTime * 0.5d);
        SimSnapshot before = runner.CaptureSnapshot();

        Assert.Equal(1, world.AddAgent(30f, 40f));
        runner.NotifyWorldMutated();
        SimSnapshot after = runner.CaptureSnapshot();

        Assert.True(before.IsConsistent);
        Assert.True(after.IsConsistent);
        Assert.Equal(1, before.AgentCount);
        Assert.Equal(new[] { 10f }, before.PositionsX);
        Assert.Equal(new[] { 10f, 30f }, after.PositionsX);
        Assert.Equal(2, after.AgentCount);
        Assert.True(after.CaptureVersion > before.CaptureVersion);
        Assert.Equal(before.MutationVersion + 1, after.MutationVersion);
        Assert.Equal(0d, runner.Accumulator);
        world.X[0] = 55f;
        Assert.Equal(10f, before.PositionsX[0]);
        Assert.Equal(10f, after.PositionsX[0]);
        SimSnapshot stepped = runner.Step();
        Assert.True(stepped.IsConsistent);
        Assert.Equal(2, stepped.AgentCount);
        Assert.Equal(after.MutationVersion, stepped.MutationVersion);
    }

    [Fact]
    public void OnlyTheGrowthSpawnAllocates_AndLaterTicksReuseAllStorage()
    {
        var world = new World(Configuration(2, 4));
        world.AddAgent(10f, 10f);
        world.AddAgent(11f, 10f);
        long beforeGrowth = GC.GetAllocatedBytesForCurrentThread();
        int growthIndex = world.AddAgent(12f, 10f);
        long growthBytes = GC.GetAllocatedBytesForCurrentThread() - beforeGrowth;
        Assert.Equal(2, growthIndex);
        Assert.True(growthBytes > 0);

        long beforeOrdinarySpawn = GC.GetAllocatedBytesForCurrentThread();
        int ordinaryIndex = world.AddAgent(13f, 10f);
        long ordinaryBytes = GC.GetAllocatedBytesForCurrentThread() - beforeOrdinarySpawn;
        Assert.Equal(3, ordinaryIndex);
        Assert.Equal(0L, ordinaryBytes);
        for (int tick = 0; tick < 100; tick++) world.Tick();
        Dictionary<string, Array> references = Arrays(world);

        long beforeTicks = GC.GetAllocatedBytesForCurrentThread();
        for (int tick = 0; tick < 1000; tick++) world.Tick();
        long tickBytes = GC.GetAllocatedBytesForCurrentThread() - beforeTicks;

        Assert.Equal(0L, tickBytes);
        foreach ((string name, Array array) in Arrays(world)) Assert.Same(references[name], array);
        Assert.Equal(4, world.Capacity);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapacityAboveManagedArrayLimitIsRejectedBeforeWorldAllocation(bool initialTooLarge)
    {
        int tooLarge = Array.MaxLength + 1;
        SimConfig config = Configuration(initialTooLarge ? tooLarge : 1, tooLarge);
        string property = initialTooLarge ? "InitialCapacity" : "MaxCapacity";

        Assert.Contains(config.Validate(), error => error.Contains(property, StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => new World(config));
    }

    [Fact]
    public void ManagedArrayLimitIsADeferredCeiling_NotAnEagerAllocationRequest()
    {
        SimConfig config = Configuration(1, Array.MaxLength);
        Assert.Empty(config.Validate());
        Assert.Empty(Configuration(Array.MaxLength, Array.MaxLength).Validate());
        var world = new World(config);

        Assert.Equal(1, world.Capacity);
        Assert.Equal(0, world.AddAgent(1f, 1f));
        Assert.Equal(1, world.AddAgent(2f, 2f));
        Assert.Equal(2, world.Capacity);
        Assert.Equal(Array.MaxLength, world.Config.MaxCapacity);
        Assert.Equal(1, world.Config.InitialCapacity);
    }

    private static SimConfig Configuration(int initial, int maximum) => new()
    {
        Seed = 7u, InitialCapacity = initial, MaxCapacity = maximum,
        WorldWidth = 64f, WorldHeight = 64f, GridCellSize = 8f,
        SenseRadius = 18f, SeparationRadius = 6f, FieldOfView = 360f,
        FixedDeltaTime = 1f / 32f, MaxSpeed = 3f, MaxForce = 0.4f,
        WanderStrength = 0.25f, CompactionInterval = 32
    };

    private static Dictionary<string, Array> Arrays(World world) => typeof(World).GetProperties()
        .Where(property => property.PropertyType.IsArray)
        .ToDictionary(property => property.Name, property => (Array)property.GetValue(world)!, StringComparer.Ordinal);

    private static Dictionary<string, Array> CopyArrays(World world) => Arrays(world)
        .ToDictionary(pair => pair.Key, pair => (Array)pair.Value.Clone(), StringComparer.Ordinal);

    private static void AssertPrefix(Dictionary<string, Array> expected, World world, int count)
    {
        foreach ((string name, Array actual) in Arrays(world))
            Assert.Equal(expected[name].Cast<object>().Take(count), actual.Cast<object>().Take(count));
    }

    private static void AssertArraysMatchCapacity(World world)
    {
        foreach (Array array in Arrays(world).Values) Assert.Equal(world.Capacity, array.Length);
    }

    private static int[] QueryAll(World world)
    {
        var result = new int[world.Count];
        int count = world.Grid.QueryNeighborhood(32f, 32f, 100f, true, result);
        return result.AsSpan(0, count).ToArray();
    }
}
