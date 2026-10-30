using SwarmSim.Core;
using SwarmSim.Core.Aco;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public sealed class AntColonyModelTests
{
    private const float NoNoise = 0f;

    /// <summary>Food and nest far from the point (50, 50) where the sensor tests stand.</summary>
    private static AntColonySettings Quiet(float deposit = 1f, float evaporation = 0f, uint seed = 1u) => new()
    {
        Deposit = deposit, Evaporation = evaporation, TurnNoise = NoNoise, SensorAngle = 0.5f, SensorDistance = 10f,
        Speed = 1f, WorldWidth = 100f, WorldHeight = 100f, FieldColumns = 100, FieldRows = 100,
        NestX = 5f, NestY = 5f, NestRadius = 2f, FoodX = 5f, FoodY = 30f, FoodRadius = 2f, Seed = seed
    };

    private static Ant Searching(float x, float y, float heading = 0f) => new(new Vec2(x, y), heading, Carrying: false);
    private static Ant Returning(float x, float y) => new(new Vec2(x, y), 0f, Carrying: true);

    private static uint[] Bits(AntColonyWorld world) =>
        world.Field.Cells.ToArray().Select(BitConverter.SingleToUInt32Bits).ToArray();

    private static (float X, float Y, uint Heading, bool Carrying)[] State(AntColonyWorld world) =>
        world.Ants.ToArray().Select(a => (a.Position.X, a.Position.Y, BitConverter.SingleToUInt32Bits(a.Heading), a.Carrying)).ToArray();

    [Fact]
    public void TheAgentPhaseNeverWritesTheField()
    {
        var world = new AntColonyWorld(new AntColonySettings { Seed = 3u }, 64);
        while (world.FoodReturned == 0) world.Step();
        while (world.CarryingCount == 0) world.Step();
        uint[] before = Bits(world);
        Assert.Contains(before, bits => bits != 0u);

        world.AgentPhase();

        Assert.Equal(before, Bits(world));
        Assert.True(world.PendingDeposits > 0);
        world.FieldPhase();
        Assert.NotEqual(before, Bits(world));
    }

    [Fact]
    public void AllAntsInATickReadTheSameFieldEvenWhenALowerIndexDeposits()
    {
        // Ant 0 lays pheromone exactly where ant 1's centre sensor reads. An inline deposit would
        // divert ant 1 this tick; a buffered one must leave it on its heading.
        AntColonySettings settings = Quiet();
        Ant[] ants = [Returning(61f, 50f), Searching(50f, 50f)];
        var world = new AntColonyWorld(settings, ants);

        world.AgentPhase();

        Assert.Equal(0f, world.Ants[1].Heading);
        Assert.Equal(0d, world.Field.Total());
        Assert.Equal(1, world.PendingDeposits);
    }

    [Fact]
    public void TheFieldPhaseDepositsThenEvaporates()
    {
        var world = new AntColonyWorld(Quiet(deposit: 8f, evaporation: 0.5f), [Returning(60f, 60f)]);

        world.Step();
        Assert.Equal(4d, world.Field.Total()); // Evaporating first would leave 8.
        Assert.Equal(0, world.PendingDeposits);

        world.Step();
        Assert.Equal(6d, world.Field.Total()); // (4 + 8) * 0.5.
        Assert.Equal(2L, world.TickCount);
    }

    [Fact]
    public void PhasesMustAlternate()
    {
        var world = new AntColonyWorld(Quiet(), 2);

        Assert.Throws<InvalidOperationException>(world.FieldPhase);
        world.AgentPhase();
        Assert.Throws<InvalidOperationException>(world.AgentPhase);
        world.FieldPhase();
        world.Step();
        Assert.Equal(2L, world.TickCount);
    }

    [Fact]
    public void ARunDependsOnNothingButSeedAndSettings()
    {
        var settings = new AntColonySettings { Seed = 5u };
        var first = new AntColonyWorld(settings, 64);
        var second = new AntColonyWorld(settings, 64);
        var other = new AntColonyWorld(new AntColonySettings { Seed = 6u }, 64);

        // Interleaved instances would expose any state shared between worlds.
        for (int tick = 0; tick < 600; tick++)
        {
            first.Step();
            other.Step();
            second.Step();
        }

        Assert.Equal(Bits(first), Bits(second));
        Assert.Equal(State(first), State(second));
        Assert.Equal(first.FoodReturned, second.FoodReturned);
        Assert.Equal(first.FoodRemaining, second.FoodRemaining);
        Assert.Equal(SimulationKinematicHash.Compute(first), SimulationKinematicHash.Compute(second));
        Assert.NotEqual(State(first), State(other));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void EachAntDrawsFromItsOwnNamedStream(int id)
    {
        var settings = new AntColonySettings { Deposit = 0f, TurnNoise = 1f, FoodAmount = 0, Seed = 9u };
        var world = new AntColonyWorld(settings, 4);
        float heading = world.Ants[id].Heading;
        Pcg32Rng stream = new RandomStreams(9u).CreateStream("aco.forage", (ulong)id);

        for (int tick = 0; tick < 200; tick++)
        {
            world.Step();
            heading += (stream.NextFloat() - 0.5f) * settings.TurnNoise;
            Assert.Equal(BitConverter.SingleToUInt32Bits(heading), BitConverter.SingleToUInt32Bits(world.Ants[id].Heading));
        }
    }

    [Fact]
    public void WithoutPheromoneAnAntIsIndependentOfThePopulation()
    {
        // Spawn draws in index order, so ant 0 starts alike in both worlds. Unlimited food means no
        // contention, and carrying changes only that ant's own draw schedule.
        var settings = new AntColonySettings { Deposit = 0f, Seed = 2u };
        var alone = new AntColonyWorld(settings, 1);
        var crowded = new AntColonyWorld(settings, 32);

        for (int tick = 0; tick < 2000; tick++)
        {
            alone.Step();
            crowded.Step();
            Assert.Equal(State(alone)[0], State(crowded)[0]);
        }
    }

    [Fact]
    public void DepositZeroLeavesTheFieldExactlyZeroAndSharesTheOnTrajectoryUntilPheromoneIsRead()
    {
        var off = new AntColonyWorld(new AntColonySettings { Deposit = 0f, Seed = 4u }, 64);
        var on = new AntColonyWorld(new AntColonySettings { Deposit = 1f, Seed = 4u }, 64);

        int tick = 0;
        while (on.Field.Total() == 0d)
        {
            Assert.Equal(State(off), State(on));
            off.Step();
            on.Step();
            tick++;
        }
        Assert.Equal(State(off), State(on)); // Ticks so far read a field that was still empty.

        for (; tick < 2000; tick++)
        {
            off.Step();
            on.Step();
        }
        Assert.Equal(0d, off.Field.Total());
        Assert.NotEqual(State(off), State(on));
    }

    [Theory]
    [InlineData(-0.5f)]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void OnePheromoneSensorTurnsTheAntExactlyToward(float sensor)
    {
        AntColonySettings settings = Quiet();
        var world = new AntColonyWorld(settings, [Searching(50f, 50f)]);
        (float sin, float cos) = MathF.SinCos(sensor);
        world.Field.Deposit(50f + cos * 10f, 50f + sin * 10f, 1f);

        world.Step();

        Assert.Equal(sensor, world.Ants[0].Heading);
    }

    [Fact]
    public void AnEmptyFieldLeavesAQuietAntOnItsHeading()
    {
        var world = new AntColonyWorld(Quiet(), [Searching(50f, 50f, 1f)]);

        for (int tick = 0; tick < 4; tick++) world.Step();

        Assert.Equal(1f, world.Ants[0].Heading);
    }

    [Fact]
    public void TheTurnIsProportionalToSensorStrength()
    {
        // 3:1 right:left in identical positions. 1600 independent streams give 1200 right with a
        // standard deviation of 17.3; the window is about five of those.
        const int ants = 1600;
        var world = new AntColonyWorld(Quiet(), Enumerable.Repeat(Searching(50f, 50f), ants).ToArray());
        (float sin, float cos) = MathF.SinCos(0.5f);
        world.Field.Deposit(50f + cos * 10f, 50f + sin * 10f, 3f);
        world.Field.Deposit(50f + cos * 10f, 50f - sin * 10f, 1f);

        world.Step();

        int right = 0, left = 0;
        foreach (Ant ant in world.Ants)
        {
            if (ant.Heading == 0.5f) right++;
            else if (ant.Heading == -0.5f) left++;
        }
        Assert.Equal(ants, right + left);
        Assert.InRange(right, 1115, 1285);
    }

    [Fact]
    public void AnAntReachingFoodPicksUpOneUnitAndHeadsHomeLayingPheromone()
    {
        AntColonySettings settings = Quiet();
        var world = new AntColonyWorld(settings, [Searching(5f, 29f, MathF.PI / 2f)]);

        world.Step();
        Assert.True(world.Ants[0].Carrying);
        Assert.Equal(settings.FoodAmount - 1, world.FoodRemaining);
        Assert.Equal(1, world.CarryingCount);
        Assert.Equal(0d, world.Field.Total());

        world.Step();
        Assert.Equal(-MathF.PI / 2f, world.Ants[0].Heading, 5);
        Assert.Equal(29f, world.Ants[0].Position.Y, 4);
        Assert.Equal(1f, world.Field.Sample(5f, 29f));
    }

    [Fact]
    public void ReachingTheNestDeliversExactlyOnceAndStartsANewSearch()
    {
        var world = new AntColonyWorld(Quiet(), [Returning(5f, 9f)]);

        for (int tick = 0; tick < 2; tick++)
        {
            Assert.Equal(0L, world.FoodReturned);
            world.Step();
        }

        Assert.Equal(1L, world.FoodReturned);
        Assert.Equal(0, world.CarryingCount);
        Assert.False(world.Ants[0].Carrying);
        Assert.Equal(2d, world.Field.Total()); // One deposit per carrying tick, the last included.
        world.Step();
        Assert.Equal(1L, world.FoodReturned);
    }

    [Fact]
    public void ADeliveredAntDrawsItsNewHeadingFromItsOwnStream()
    {
        var world = new AntColonyWorld(Quiet(), [Returning(5f, 6f)]);
        float expected = new RandomStreams(1u).CreateStream("aco.forage", 0ul).NextFloat() * MathF.Tau;

        world.Step();

        Assert.Equal(expected, world.Ants[0].Heading);
    }

    [Fact]
    public void AnAntHomesAcrossTheSeam()
    {
        AntColonySettings settings = Quiet();
        var world = new AntColonyWorld(settings, [Returning(98f, 5f)]);

        world.Step();

        // The nest at x = 5 is 7 away through the seam and 93 away across the world.
        Assert.Equal(99f, world.Ants[0].Position.X, 4);
        Assert.Equal(0f, world.Ants[0].Heading, 5);
    }

    [Fact]
    public void FoodIsFiniteAndContendedUnitsGoToTheLowestIndex()
    {
        var settings = new AntColonySettings
        {
            Deposit = 0f, TurnNoise = NoNoise, Speed = 0.01f, FoodAmount = 2, FoodX = 80f, FoodY = 60f,
            NestX = 40f, NestY = 60f
        };
        Ant[] ants = Enumerable.Repeat(Searching(80f, 60f), 5).ToArray();
        var world = new AntColonyWorld(settings, ants);

        world.Step();

        Assert.Equal([true, true, false, false, false], world.Ants.ToArray().Select(a => a.Carrying));
        Assert.Equal(0, world.FoodRemaining);
        world.Step();
        Assert.Equal(2, world.CarryingCount);
    }

    [Fact]
    public void FoodIsConservedOverARun()
    {
        var settings = new AntColonySettings { FoodAmount = 40, Seed = 3u };
        var world = new AntColonyWorld(settings, 128);

        for (int tick = 0; tick < 3000; tick++)
        {
            world.Step();
            Assert.Equal(settings.FoodAmount, world.FoodReturned + world.CarryingCount + world.FoodRemaining);
            Assert.Equal(world.Ants.ToArray().Count(a => a.Carrying), world.CarryingCount);
        }
        Assert.Equal(40L, world.FoodReturned);
    }

    [Fact]
    public void ATrailFadesOnceTheFoodIsGone()
    {
        var world = new AntColonyWorld(new AntColonySettings { FoodAmount = 40, Evaporation = 0.02f, Seed = 3u }, 128);
        while (world.FoodReturned < 40) world.Step();
        double peak = world.Field.Total();
        Assert.True(peak > 0d);

        for (int tick = 0; tick < 100; tick++) world.Step();

        // No carrying ants are left, so only evaporation acts: 0.98^100, within float rounding.
        Assert.Equal(peak * Math.Pow(0.98, 100), world.Field.Total(), 2);
    }

    [Fact]
    public void AntsSpawnAtTheNestCentreWithHeadingsFromTheSpawnStream()
    {
        var settings = new AntColonySettings { Seed = 8u };
        var world = new AntColonyWorld(settings, 16);
        Pcg32Rng stream = new RandomStreams(8u).CreateStream("aco.spawn");

        foreach (Ant ant in world.Ants)
        {
            Assert.Equal(new Vec2(settings.NestX, settings.NestY), ant.Position);
            Assert.Equal(stream.NextFloat() * MathF.Tau, ant.Heading);
            Assert.False(ant.Carrying);
        }
    }

    [Fact]
    public void KinematicsDeriveVelocityFromHeadingAndNeverTruncate()
    {
        var settings = new AntColonySettings { Speed = 2f };
        var world = new AntColonyWorld(settings, [Searching(3f, 4f, 0f), Searching(5f, 6f, MathF.PI / 2f)]);
        var samples = new KinematicSample[2];

        world.CopyKinematics(samples);

        Assert.Equal(new KinematicSample(3f, 4f, 2f, 0f), samples[0]);
        Assert.Equal(0f, samples[1].Vx, 5);
        Assert.Equal(2f, samples[1].Vy, 5);
        Assert.Throws<ArgumentException>(() => world.CopyKinematics(new KinematicSample[1]));
    }

    [Fact]
    public void TheKinematicHashIsBlindToTheField()
    {
        // This records a known gap, not a goal: see the seam inventory in docs/VICSEK.md.
        var world = new AntColonyWorld(new AntColonySettings(), 8);
        string before = SimulationKinematicHash.Compute(world);

        world.Field.Deposit(1f, 1f, 1f);

        Assert.Equal(before, SimulationKinematicHash.Compute(world));
    }

    [Fact]
    public void TheProbeReportsExactWorldCountersWithoutAllocating()
    {
        var world = new AntColonyWorld(new AntColonySettings { Seed = 2u }, 64);
        while (world.FoodReturned < 3 || world.CarryingCount == 0) world.Step();
        var probe = new AntColonyMetricProbe(world);
        var output = new double[3];

        long before = GC.GetAllocatedBytesForCurrentThread();
        probe.Sample(new MetricFrame([], MetricDomain.Euclidean), output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocated);
        Assert.Equal(["aco.foodReturned", "aco.carrying", "aco.fieldTotal"], probe.Channels);
        Assert.Equal(world.FoodReturned, output[0]);
        Assert.Equal(world.CarryingCount, output[1]);
        Assert.Equal(world.Field.Total(), output[2]);
        Assert.True(probe.IsDeterministic);
    }

    [Fact]
    public void AMetadataProbeRefusesToSample()
    {
        var probe = new AntColonyMetricProbe(null);

        Assert.Throws<InvalidOperationException>(() =>
            probe.Sample(new MetricFrame([], MetricDomain.Euclidean), new double[3]));
    }

    [Fact]
    public void WarmTicksAllocateNothing()
    {
        var world = new AntColonyWorld(new AntColonySettings { Seed = 2u }, 256);
        for (int tick = 0; tick < 512; tick++) world.Step();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int tick = 0; tick < 1000; tick++) world.Step();

        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(world.FoodReturned > 0);
    }

    [Theory]
    [InlineData("""{"FieldCadence":2}""", "FieldCadence")]
    [InlineData("""{"Deposit":-1}""", "Deposit")]
    [InlineData("""{"Evaporation":1.5}""", "Evaporation")]
    [InlineData("""{"Speed":0}""", "Speed")]
    [InlineData("""{"FieldColumns":0}""", "FieldColumns")]
    [InlineData("""{"NestRadius":0}""", "NestRadius")]
    [InlineData("""{"FoodAmount":-1}""", "FoodAmount")]
    [InlineData("""{"NestX":1000}""", "NestX")]
    [InlineData("""{"FoodY":-1}""", "FoodY")]
    public void InvalidSettingsAreRefusedByName(string json, string field)
    {
        var failure = Assert.ThrowsAny<Exception>(() => AntColonySettings.FromJson(json));
        Assert.Contains(field, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionRefusesEmptyOrNonFiniteState()
    {
        var settings = new AntColonySettings();
        Assert.Throws<ArgumentOutOfRangeException>(() => new AntColonyWorld(settings, 0));
        Assert.Throws<ArgumentException>(() => new AntColonyWorld(settings, Array.Empty<Ant>().AsSpan()));
        Assert.Throws<ArgumentException>(() => new AntColonyWorld(settings, [Searching(float.NaN, 1f)]));
        Assert.Throws<ArgumentException>(() => new AntColonyWorld(new AntColonySettings { Speed = -1f }, 1));
    }
}
