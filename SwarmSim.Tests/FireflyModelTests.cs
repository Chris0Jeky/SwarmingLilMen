using SwarmSim.Core.Canonical;
using SwarmSim.Core.Firefly;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public sealed class FireflyModelTests
{
    private static Oscillator Agent(float phase, float frequency = 0f, float x = 1f, float y = 2f) =>
        new(new Vec2(x, y), phase, frequency);

    private static double[] Metrics(FireflyWorld world)
    {
        var output = new double[3];
        new FireflyMetricProbe(world).Sample(new MetricFrame([], MetricDomain.Euclidean), output);
        return output;
    }

    private static void AssertAngle(double expected, double actual, double tolerance = 2e-6d)
    {
        double difference = Math.IEEERemainder(expected - actual, Math.Tau);
        Assert.InRange(Math.Abs(difference), 0d, tolerance);
    }

    [Fact]
    public void TwoOscillatorsReadTheSamePreviousPhasesAndNormalizeByTheWholePopulation()
    {
        var world = new FireflyWorld(new FireflySettings { Coupling = 2f, FixedDeltaTime = 0.1f },
            new[] { Agent(0f), Agent(MathF.PI / 2f) });

        world.Step();

        // K/N = 1, so the pair approach by 0.1 radians each. An in-place second read gives a different result.
        AssertAngle(0.1d, world.Agents[0].Phase);
        AssertAngle(MathF.PI / 2f - 0.1d, world.Agents[1].Phase);
        Assert.Equal(1L, world.TickCount);
        Assert.Equal(new[] { "mean-field", "oscillators", "commit" }, world.PhaseNames);
    }

    [Fact]
    public void MeanFieldMatchesAnIndependentPairwiseSineOracle()
    {
        var settings = new FireflySettings { Coupling = 1.7f, FixedDeltaTime = 0.037f };
        var world = new FireflyWorld(settings, new[]
        {
            Agent(0.2f, -0.5f), Agent(1.7f, 0.3f), Agent(3.2f, 1.2f), Agent(5.8f, -0.2f)
        });
        for (int tick = 0; tick < 20; tick++)
        {
            Oscillator[] previous = world.Agents.ToArray();
            world.Step();
            for (int i = 0; i < previous.Length; i++)
            {
                double sum = 0d;
                for (int j = 0; j < previous.Length; j++)
                    sum += Math.Sin((double)previous[j].Phase - previous[i].Phase);
                double expected = previous[i].Phase + settings.FixedDeltaTime *
                    (previous[i].NaturalFrequency + settings.Coupling * sum / previous.Length);
                AssertAngle(expected, world.Agents[i].Phase);
            }
        }
    }

    [Fact]
    public void AnIsolatedOscillatorHasNoSelfCoupling()
    {
        var uncoupled = new FireflyWorld(new FireflySettings { Coupling = 0f }, new[] { Agent(1.3f, 0.75f) });
        var coupled = new FireflyWorld(new FireflySettings { Coupling = 100f }, new[] { Agent(1.3f, 0.75f) });
        for (int tick = 0; tick < 200; tick++)
        {
            uncoupled.Step();
            coupled.Step();
        }
        Assert.Equal(uncoupled.Agents.ToArray(), coupled.Agents.ToArray());
    }

    [Fact]
    public void ZeroCouplingFollowsNaturalFrequenciesIncludingNegativeRotation()
    {
        var settings = new FireflySettings { Coupling = 0f, FixedDeltaTime = 0.01f };
        Oscillator[] initial = [Agent(0.25f, -1.5f), Agent(6f, 0.75f), Agent(1.2f, 0f)];
        var world = new FireflyWorld(settings, initial);
        for (int tick = 0; tick < 200; tick++) world.Step();

        for (int i = 0; i < initial.Length; i++)
        {
            AssertAngle(initial[i].Phase + initial[i].NaturalFrequency * settings.FixedDeltaTime * 200d,
                world.Agents[i].Phase, 1e-4d);
            Assert.InRange(world.Agents[i].Phase, 0f, MathF.BitDecrement(MathF.Tau));
        }
        Assert.Equal(initial[2].Phase, world.Agents[2].Phase);
    }

    [Fact]
    public void CommonPhaseAndFrequencyRemainSynchronized()
    {
        Oscillator[] initial = Enumerable.Range(0, 16).Select(i => Agent(1.25f, 1.8f, i, 0f)).ToArray();
        var world = new FireflyWorld(new FireflySettings { Coupling = 3f }, initial);
        for (int tick = 0; tick < 100; tick++) world.Step();

        foreach (Oscillator agent in world.Agents)
            Assert.Equal(world.Agents[0].Phase, agent.Phase);
        Assert.Equal(1d, Metrics(world)[0], 12);
    }

    [Fact]
    public void StrongCouplingSynchronizesAFiniteHeterogeneousExample()
    {
        const int count = 32;
        Oscillator[] initial = Enumerable.Range(0, count).Select(i => Agent(
            -1.5f + 3f * i / (count - 1),
            1.8f + 0.2f * (2f * ((i * 13) % count) / (count - 1) - 1f), i, 0f)).ToArray();
        var coupled = new FireflyWorld(new FireflySettings { Coupling = 3f }, initial);
        var control = new FireflyWorld(new FireflySettings { Coupling = 0f }, initial);
        for (int tick = 0; tick < 500; tick++)
        {
            coupled.Step();
            control.Step();
        }

        // One explicit finite example, not a claim about a critical coupling or a phase transition.
        Assert.InRange(Metrics(coupled)[0], 0.98d, 1d);
        Assert.InRange(Metrics(control)[0], 0d, 0.8d);
    }

    [Fact]
    public void ExplicitInitialStateIsCopiedNormalizedAndRetainsFrequency()
    {
        Oscillator[] input = [Agent(-0.25f, -0.6f, -1f, 61f)];
        var world = new FireflyWorld(new FireflySettings(), input);
        input[0] = Agent(3f, 4f, 5f, 6f);

        Assert.Equal(99f, world.Agents[0].Position.X);
        Assert.Equal(1f, world.Agents[0].Position.Y);
        AssertAngle(-0.25d, world.Agents[0].Phase);
        Assert.Equal(-0.6f, world.Agents[0].NaturalFrequency);
    }

    [Fact]
    public void PopulationOrderOnlyChangesFloatingPointReductionRoundoff()
    {
        Oscillator[] initial = [Agent(0.1f, 0.2f), Agent(2f, 0.3f), Agent(5f, -0.1f), Agent(1f, 0.8f)];
        var forward = new FireflyWorld(new FireflySettings(), initial);
        var reverse = new FireflyWorld(new FireflySettings(), initial.Reverse().ToArray());
        for (int tick = 0; tick < 100; tick++)
        {
            forward.Step();
            reverse.Step();
        }
        for (int i = 0; i < initial.Length; i++)
            AssertAngle(forward.Agents[i].Phase, reverse.Agents[initial.Length - i - 1].Phase, 1e-5d);
    }

    [Fact]
    public void SpawnUsesIndependentNamedPositionPhaseAndFrequencyStreams()
    {
        var settings = new FireflySettings { Seed = 7u };
        var world = new FireflyWorld(settings, 32);
        var streams = new RandomStreams(settings.Seed);
        Pcg32Rng positions = streams.CreateStream(FireflyWorld.PositionStream);
        Pcg32Rng phases = streams.CreateStream(FireflyWorld.PhaseStream);
        Pcg32Rng frequencies = streams.CreateStream(FireflyWorld.FrequencyStream);
        foreach (Oscillator agent in world.Agents)
        {
            Assert.Equal(positions.NextFloat() * settings.WorldWidth, agent.Position.X);
            Assert.Equal(positions.NextFloat() * settings.WorldHeight, agent.Position.Y);
            AssertAngle(phases.NextFloat() * Math.Tau, agent.Phase);
            float expected = (float)(settings.MeanFrequency + (frequencies.NextFloat() * 2d - 1d) * settings.FrequencySpread);
            Assert.Equal(expected, agent.NaturalFrequency);
        }
    }

    [Fact]
    public void CouplingControlsShareInitialStateAndGeometryDoesNotAffectPhaseEvolution()
    {
        var first = new FireflyWorld(new FireflySettings { Seed = 9u, Coupling = 0f }, 64);
        var coupled = new FireflyWorld(new FireflySettings { Seed = 9u, Coupling = 3f }, 64);
        Assert.Equal(first.Agents.ToArray(), coupled.Agents.ToArray());

        var wider = new FireflyWorld(new FireflySettings { Seed = 9u, Coupling = 3f, WorldWidth = 200f }, 64);
        for (int tick = 0; tick < 100; tick++)
        {
            coupled.Step();
            wider.Step();
        }
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(coupled.Agents[i].Phase, wider.Agents[i].Phase);
            Assert.Equal(coupled.Agents[i].NaturalFrequency, wider.Agents[i].NaturalFrequency);
        }
    }

    [Fact]
    public void RepeatedSeededRunsAndConfigurationRoundTripsAreBitIdentical()
    {
        var settings = new FireflySettings { Coupling = 2.5f, FrequencySpread = 0.2f, Seed = 17u };
        FireflySettings restored = FireflySettings.FromJson(settings.ToJson());
        Assert.Equal(settings.ToJson(), restored.ToJson());
        var first = new FireflyWorld(settings, 64);
        var second = new FireflyWorld(restored, 64);
        var otherSeed = new FireflyWorld(new FireflySettings { Coupling = 2.5f, FrequencySpread = 0.2f, Seed = 18u }, 64);
        for (int tick = 0; tick < 128; tick++)
        {
            first.Step();
            second.Step();
            otherSeed.Step();
        }
        Assert.Equal(first.Agents.ToArray(), second.Agents.ToArray());
        Assert.Equal(first.StateHash(), second.StateHash());
        Assert.NotEqual(first.Agents.ToArray(), otherSeed.Agents.ToArray());
    }

    [Fact]
    public void CaptureKeepsStationaryKinematicsWhilePhaseStateEvolves()
    {
        var world = new FireflyWorld(new FireflySettings(), 32);
        var before = new KinematicSample[world.Count];
        var after = new KinematicSample[world.Count];
        world.CopyKinematics(before);
        var hashBefore = world.StateHash();
        for (int tick = 0; tick < 64; tick++) world.Step();
        world.CopyKinematics(after);

        Assert.Equal(before, after);
        Assert.All(after, sample => { Assert.Equal(0f, sample.Vx); Assert.Equal(0f, sample.Vy); });
        Assert.NotEqual(hashBefore, world.StateHash());
        Assert.Throws<ArgumentException>(() => world.CopyKinematics(new KinematicSample[31]));
    }

    [Fact]
    public void ModelHashSeesPhaseFrequencySettingsAndClockBeyondKinematics()
    {
        var settings = new FireflySettings { Coupling = 0f };
        var original = new FireflyWorld(settings, new[] { Agent(1f) });
        var phase = new FireflyWorld(settings, new[] { Agent(1.1f) });
        var frequency = new FireflyWorld(settings, new[] { Agent(1f, 0.1f) });
        var config = new FireflyWorld(new FireflySettings { Coupling = 1f }, new[] { Agent(1f) });
        var hash = original.StateHash();
        Assert.Equal(FireflyWorld.ModelStateProfile, hash.Profile);
        Assert.Equal(64, hash.Hash.Length);
        Assert.NotEqual(hash, phase.StateHash());
        Assert.NotEqual(hash, frequency.StateHash());
        Assert.NotEqual(hash, config.StateHash());
        original.Step(); // Zero frequency and coupling: only the clock changes.
        Assert.Equal(1f, original.Agents[0].Phase);
        Assert.NotEqual(hash, original.StateHash());
    }

    [Fact]
    public void MetricsDescribeCircularPhaseRatherThanVelocity()
    {
        var aligned = new FireflyWorld(new FireflySettings(), new[] { Agent(1.25f, 1f), Agent(1.25f, 3f) });
        double[] coherent = Metrics(aligned);
        Assert.Equal(1d, coherent[0], 12);
        Assert.Equal(1.25d, coherent[1], 12);
        Assert.Equal(2d, coherent[2]);

        var opposed = new FireflyWorld(new FireflySettings(), new[] { Agent(0f), Agent(MathF.PI) });
        double[] incoherent = Metrics(opposed);
        Assert.InRange(incoherent[0], 0d, 1e-6d);
        Assert.True(double.IsNaN(incoherent[1]));
    }

    [Fact]
    public void ProbeMetadataRejectsSamplingAndSamplingDoesNotAdvanceState()
    {
        var metadata = new FireflyMetricProbe(null);
        Assert.Equal(new[] { "firefly.coherence", "firefly.meanPhase", "firefly.meanNaturalFrequency" }, metadata.Channels);
        Assert.True(metadata.IsDeterministic);
        Assert.Throws<InvalidOperationException>(() => metadata.Sample(new MetricFrame([], MetricDomain.Euclidean), new double[3]));
        var world = new FireflyWorld(new FireflySettings(), 8);
        var probe = new FireflyMetricProbe(world);
        Assert.Throws<ArgumentException>(() => probe.Sample(new MetricFrame([], MetricDomain.Euclidean), new double[2]));
        var before = world.StateHash();
        double[] first = Metrics(world);
        double[] second = Metrics(world);
        Assert.Equal(first, second);
        Assert.Equal(before, world.StateHash());
    }

    [Fact]
    public void WarmStepsCaptureAndMetricSamplingAllocateNothing()
    {
        var world = new FireflyWorld(new FireflySettings(), 64);
        var probe = new FireflyMetricProbe(world);
        var output = new double[3];
        var capture = new KinematicSample[world.Count];
        for (int tick = 0; tick < 128; tick++)
        {
            world.Step();
            world.CopyKinematics(capture);
            probe.Sample(new MetricFrame(capture, MetricDomain.Euclidean), output);
        }
        Assert.Equal(0L, AntColonyModelTests.LeastAllocated(() =>
        {
            for (int tick = 0; tick < 128; tick++)
            {
                world.Step();
                world.CopyKinematics(capture);
                probe.Sample(new MetricFrame(capture, MetricDomain.Euclidean), output);
            }
        }));
    }

    [Fact]
    public void InvalidSettingsPopulationsAndInitialStateFailBeforeStepping()
    {
        FireflySettings[] invalid =
        [
            new() { Coupling = -1f }, new() { Coupling = float.NaN },
            new() { MeanFrequency = float.PositiveInfinity }, new() { FrequencySpread = -1f },
            new() { MeanFrequency = float.MaxValue, FrequencySpread = float.MaxValue },
            new() { WorldWidth = 0f }, new() { WorldHeight = float.NaN },
            new() { FixedDeltaTime = 0f }, new() { FixedDeltaTime = float.PositiveInfinity },
            new() { Seed = uint.MaxValue }
        ];
        foreach (FireflySettings settings in invalid)
        {
            Assert.NotEmpty(settings.Validate());
            Assert.Throws<ArgumentException>(() => new FireflyWorld(settings, 1));
            Assert.Throws<ArgumentException>(() => settings.ToJson());
        }
        Assert.Throws<ArgumentNullException>(() => new FireflyWorld(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FireflyWorld(new FireflySettings(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FireflyWorld(new FireflySettings(), FireflyWorld.MaxAgents + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FireflyWorld(new FireflySettings(), Array.Empty<Oscillator>()));
        Assert.Throws<ArgumentException>(() => new FireflyWorld(new FireflySettings(), new[] { Agent(float.NaN) }));
        Assert.Throws<ArgumentException>(() => new FireflyWorld(new FireflySettings(), new[] { Agent(0f, float.NegativeInfinity) }));
        Assert.Throws<ArgumentException>(() => new FireflyWorld(new FireflySettings(), new[] { Agent(0f, x: float.PositiveInfinity) }));
    }

    [Fact]
    public void FiniteExtremeRatesStayInThePhaseDomainWithoutOverflow()
    {
        var world = new FireflyWorld(new FireflySettings
            { Coupling = float.MaxValue, FixedDeltaTime = float.MaxValue },
            new[] { Agent(1f, float.MaxValue), Agent(2f, -float.MaxValue) });
        for (int tick = 0; tick < 8; tick++) world.Step();
        foreach (Oscillator agent in world.Agents)
            Assert.InRange(agent.Phase, 0f, MathF.BitDecrement(MathF.Tau));
    }
}
