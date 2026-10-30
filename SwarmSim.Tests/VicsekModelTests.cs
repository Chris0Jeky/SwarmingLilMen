using System.Text.Json;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Configuration;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.Utils;
using SwarmSim.Core.Vicsek;

namespace SwarmSim.Tests;

public sealed class VicsekModelTests
{
    private const float AngleTolerance = 2e-6f;
    private static readonly VicsekSettings NoNoise = new() { Noise = 0f };

    private static float Heading(in Boid agent) => MathF.Atan2(agent.Velocity.Y, agent.Velocity.X);

    private static void AssertAngle(float expected, float actual, float tolerance = AngleTolerance) =>
        Assert.InRange(MathF.Abs(MathUtils.AngleDifference(expected, actual)), 0f, tolerance);

    [Fact]
    public void HeadingsReadThePreviousPopulation_NotNeighborsUpdatedEarlierInTheTick()
    {
        // A-B and B-C interact; A-C do not. An in-place update would feed A's new heading to B.
        Vec2[] positions = [new(1f, 5f), new(1.8f, 5f), new(2.6f, 5f)];
        float[] headings = [0f, MathF.PI / 2f, MathF.PI];
        var world = new VicsekWorld(NoNoise, positions, headings);

        world.Step();

        AssertAngle(MathF.PI / 4f, Heading(world.Agents[0]));
        AssertAngle(MathF.PI / 2f, Heading(world.Agents[1]));
        AssertAngle(3f * MathF.PI / 4f, Heading(world.Agents[2]));
    }

    [Fact]
    public void AgentOrderDoesNotChangeTheNoiselessUpdate()
    {
        Vec2[] positions = [new(1f, 5f), new(1.8f, 5f), new(2.6f, 5f)];
        float[] headings = [0.3f, 2f, -2.5f];
        var forward = new VicsekWorld(NoNoise, positions, headings);
        var reversed = new VicsekWorld(NoNoise,
            positions.Reverse().ToArray(), headings.Reverse().ToArray());

        for (int tick = 0; tick < 4; tick++)
        {
            forward.Step();
            reversed.Step();
        }

        for (int i = 0; i < positions.Length; i++)
            AssertAngle(Heading(forward.Agents[i]), Heading(reversed.Agents[positions.Length - 1 - i]));
    }

    [Fact]
    public void PositionsAdvanceWithThePreviousVelocity()
    {
        var world = new VicsekWorld(NoNoise, [new Vec2(1f, 5f), new Vec2(1.8f, 5f)], [0f, MathF.PI / 2f]);

        world.Step();

        // Heading changed to pi/4, yet this tick's displacement used the old axis-aligned velocity.
        Assert.Equal(1f + NoNoise.Speed, world.Agents[0].Position.X, 5);
        Assert.Equal(5f, world.Agents[0].Position.Y, 5);
        Assert.Equal(5f + NoNoise.Speed, world.Agents[1].Position.Y, 5);
    }

    [Fact]
    public void AnAgentCountsItselfExactlyOnce()
    {
        var pair = new VicsekWorld(NoNoise, [new Vec2(1f, 5f), new Vec2(1.5f, 5f)], [0f, MathF.PI / 2f]);
        pair.Step();
        // Equal weights give pi/4. Self counted twice would give atan2(1, 2) for the first agent.
        AssertAngle(MathF.PI / 4f, Heading(pair.Agents[0]));

        var isolated = new VicsekWorld(NoNoise, [new Vec2(1f, 5f)], [1.25f]);
        isolated.Step();
        AssertAngle(1.25f, Heading(isolated.Agents[0]));

        // A coincident other agent is a neighbor, not a second copy of self.
        var coincident = new VicsekWorld(NoNoise,
            [new Vec2(1f, 5f), new Vec2(1f, 5f), new Vec2(1.5f, 5f)], [0f, 0f, MathF.PI / 2f]);
        coincident.Step();
        AssertAngle(MathF.Atan2(1f, 2f), Heading(coincident.Agents[0]));
    }

    [Fact]
    public void NoiseIsUniformInRadiansFromTheNamedStream()
    {
        var settings = new VicsekSettings { Noise = 1.2f, Seed = 77u };
        var world = new VicsekWorld(settings, [new Vec2(5f, 5f)], [0f]);
        Pcg32Rng expected = new RandomStreams(settings.Seed).CreateStream(VicsekWorld.NoiseStream);
        double heading = 0d;
        for (int tick = 0; tick < 256; tick++)
        {
            world.Step();
            heading += (expected.NextFloat() - 0.5f) * 1.2d;
            AssertAngle((float)heading, Heading(world.Agents[0]), 3e-4f);
        }

        var widest = new VicsekWorld(new VicsekSettings { Noise = 1.2f }, [new Vec2(5f, 5f)], [0f]);
        float largest = 0f;
        for (int tick = 0; tick < 4096; tick++)
        {
            float before = Heading(widest.Agents[0]);
            widest.Step();
            largest = MathF.Max(largest, MathF.Abs(MathUtils.AngleDifference(before, Heading(widest.Agents[0]))));
        }
        // The half-width is 0.6 radians, not 0.6 * 2*pi and not 1.2.
        Assert.InRange(largest, 0.59f, 0.6f + 1e-5f);
    }

    [Fact]
    public void EachAgentConsumesOneDrawPerTick_SoEtaDoesNotShiftTheNoiseSequence()
    {
        VicsekSettings quiet = new() { Noise = 0.1f, Seed = 5u };
        VicsekSettings loud = new() { Noise = 0.2f, Seed = 5u };
        Vec2[] positions = [new(1f, 1f), new(5f, 5f), new(8f, 2f)]; // Mutually out of range.
        float[] headings = [0f, 0f, 0f];
        var first = new VicsekWorld(quiet, positions, headings);
        var second = new VicsekWorld(loud, positions, headings);
        first.Step();
        second.Step();

        for (int i = 0; i < positions.Length; i++)
            Assert.Equal(Heading(first.Agents[i]) * 2f, Heading(second.Agents[i]), 5);
    }

    [Fact]
    public void NeighborhoodsCrossPeriodicSeams()
    {
        var x = new VicsekWorld(NoNoise, [new Vec2(0.1f, 5f), new Vec2(9.9f, 5f)], [0f, MathF.PI / 2f]);
        var y = new VicsekWorld(NoNoise, [new Vec2(5f, 0.1f), new Vec2(5f, 9.9f)], [0f, MathF.PI / 2f]);
        x.Step();
        y.Step();
        AssertAngle(MathF.PI / 4f, Heading(x.Agents[0]));
        AssertAngle(MathF.PI / 4f, Heading(y.Agents[0]));

        var wrapped = new VicsekWorld(NoNoise, [new Vec2(9.99f, 5f)], [0f]);
        wrapped.Step();
        Assert.Equal(9.99f + NoNoise.Speed - 10f, wrapped.Agents[0].Position.X, 4);
    }

    [Fact]
    public void InteractionRadiusIsInclusiveAndCircular()
    {
        var inside = new VicsekWorld(NoNoise, [new Vec2(4f, 5f), new Vec2(5f, 5f)], [0f, MathF.PI / 2f]);
        var outside = new VicsekWorld(NoNoise, [new Vec2(4f, 5f), new Vec2(5.001f, 5f)], [0f, MathF.PI / 2f]);
        var corner = new VicsekWorld(NoNoise, [new Vec2(4f, 4f), new Vec2(4.9f, 4.9f)], [0f, MathF.PI / 2f]);
        inside.Step();
        outside.Step();
        corner.Step();
        AssertAngle(MathF.PI / 4f, Heading(inside.Agents[0]));
        AssertAngle(0f, Heading(outside.Agents[0]));
        AssertAngle(0f, Heading(corner.Agents[0])); // Inside the bounding square, outside the circle.
    }

    [Fact]
    public void OpposedHeadingsKeepTheAgentsOwnHeading()
    {
        var world = new VicsekWorld(NoNoise, [new Vec2(4f, 5f), new Vec2(4.5f, 5f)], [0.7f, 0.7f + MathF.PI]);
        world.Step();
        AssertAngle(0.7f, Heading(world.Agents[0]), 1e-5f);
        AssertAngle(0.7f + MathF.PI, Heading(world.Agents[1]), 1e-5f);
    }

    [Fact]
    public void DenseNeighborhoodsAreNeverTruncated()
    {
        const int Count = 600; // Above the boids engines' 128/256 neighbor buffers.
        var positions = new Vec2[Count];
        var headings = new float[Count];
        var random = new Pcg32Rng(3ul, 5ul);
        double sumX = 0d, sumY = 0d;
        for (int i = 0; i < Count; i++)
        {
            positions[i] = new Vec2(5f + random.NextFloat() * 0.5f, 5f + random.NextFloat() * 0.5f);
            headings[i] = random.NextFloat() * MathF.Tau;
            sumX += MathF.Cos(headings[i]);
            sumY += MathF.Sin(headings[i]);
        }

        var world = new VicsekWorld(NoNoise, positions, headings);
        world.Step();

        float mean = (float)Math.Atan2(sumY, sumX);
        foreach (Boid agent in world.Agents)
            AssertAngle(mean, Heading(agent), 1e-4f);
    }

    [Fact]
    public void GridAndNaiveIndexesProduceIdenticalTrajectories()
    {
        var settings = new VicsekSettings { Noise = 1f, Seed = 9u };
        var grid = new VicsekWorld(settings, 300);
        var naive = new VicsekWorld(settings, 300, new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight));
        for (int tick = 0; tick < 128; tick++)
        {
            grid.Step();
            naive.Step();
        }
        Assert.Equal(SimulationKinematicHash.Compute(naive), SimulationKinematicHash.Compute(grid));
    }

    [Fact]
    public void RepeatedRunsAreBitIdentical_AndSeedsDiffer()
    {
        static string Run(uint seed)
        {
            var world = new VicsekWorld(new VicsekSettings { Noise = 2f, Seed = seed }, 256);
            for (int tick = 0; tick < 256; tick++) world.Step();
            return SimulationKinematicHash.Compute(world);
        }

        Assert.Equal(Run(4u), Run(4u));
        Assert.NotEqual(Run(4u), Run(5u));
    }

    [Fact]
    public void SpawnDoesNotDependOnNoise_SoEtaComparisonsShareInitialState()
    {
        var quiet = new VicsekWorld(new VicsekSettings { Noise = 0f, Seed = 8u }, 64);
        var loud = new VicsekWorld(new VicsekSettings { Noise = 6f, Seed = 8u }, 64);
        Assert.Equal(SimulationKinematicHash.Compute(quiet), SimulationKinematicHash.Compute(loud));
    }

    [Fact]
    public void WarmTicksAllocateNothing()
    {
        var world = new VicsekWorld(new VicsekSettings { Noise = 1f }, 256);
        for (int tick = 0; tick < 64; tick++) world.Step();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int tick = 0; tick < 1000; tick++) world.Step();
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ThePolarizationChannelIsTheVicsekOrderParameter()
    {
        var settings = new VicsekSettings { Noise = 3f, Seed = 2u };
        var world = new VicsekWorld(settings, 128);
        for (int tick = 0; tick < 32; tick++) world.Step();

        double sumX = 0d, sumY = 0d;
        var samples = new KinematicSample[world.Agents.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            Boid agent = world.Agents[i];
            samples[i] = new KinematicSample(agent.Position.X, agent.Position.Y, agent.Velocity.X, agent.Velocity.Y);
            sumX += agent.Velocity.X;
            sumY += agent.Velocity.Y;
        }
        var output = new double[3];
        new MotionMetricProbe(includeCentroids: false).Sample(
            new MetricFrame(samples, MetricDomain.Toroidal(settings.WorldWidth, settings.WorldHeight)), output);

        double expected = Math.Sqrt(sumX * sumX + sumY * sumY) / (samples.Length * settings.Speed);
        Assert.Equal(expected, output[2], 5);
        Assert.Equal(settings.Speed, output[1], 5);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(6.3f)]
    [InlineData(float.NaN)]
    public void NoiseMustBeAnAngleInRadians(float noise) =>
        Assert.Throws<ArgumentException>(() => new VicsekWorld(new VicsekSettings { Noise = noise }, 4));

    [Fact]
    public void ConfigurationRoundTripsAndRejectsAnotherEnginesFields()
    {
        var settings = new VicsekSettings { Noise = 1.5f, Speed = 0.5f, Seed = 12u };
        VicsekSettings restored = VicsekSettings.FromJson(settings.ToJson());
        Assert.Equal(settings.ToJson(), restored.ToJson());

        var overrides = new Dictionary<string, JsonElement> { ["TargetSpeed"] = JsonSerializer.SerializeToElement(1f) };
        Assert.Throws<ArgumentException>(() => ConfigurationResolver.ResolveVicsekSettings(overrides: overrides));
    }
}
