using System.Diagnostics;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Utils;
using MinimalTestProgram = SwarmSim.Render.MinimalTest;
using RenderProgram = SwarmSim.Render.Program;
using Xunit.Abstractions;

namespace SwarmSim.Tests;

public sealed class DeterminismTests
{
    private const int TickCount = 500;
    private readonly ITestOutputHelper _output;

    public DeterminismTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void LegacyWorld_WithWander_IsBitIdenticalAfter500Ticks()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "configs", "balanced.json");
        SimConfig config = SimConfig.LoadFromJson(configPath);
        var first = new World(config, seed: 42u);
        var second = new World(config, seed: 42u);

        for (int i = 0; i < 64; i++)
        {
            first.AddRandomAgent();
            second.AddRandomAgent();
        }

        Advance(first, TickCount);
        Advance(second, TickCount);

        string firstHash = SimulationKinematicHash.Compute(first);
        string secondHash = SimulationKinematicHash.Compute(second);
        _output.WriteLine($"LEGACY_KINEMATIC_HASH={firstHash}");
        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void CanonicalWorld_WithWander_IsBitIdenticalAfter500Ticks()
    {
        // The second stream derived from seed 42 is above the public range. Pinning that integer
        // mapping plus same-platform repetition proves the internal compatibility path is used.
        CanonicalWorldSettings settings = CreateCanonicalSettings(capacity: 8, seed: 42u);
        CanonicalWorld first = CreateCanonicalWorld(settings);
        CanonicalWorld second = CreateCanonicalWorld(settings);
        AddCanonicalAgents(first);
        AddCanonicalAgents(second);

        Advance(first, TickCount);
        Advance(second, TickCount);

        uint firstDerivedSeed = CanonicalWorld.DeriveWanderSeed(settings.Seed, agentIndex: 0);
        uint secondDerivedSeed = CanonicalWorld.DeriveWanderSeed(settings.Seed, agentIndex: 1);
        Assert.Equal(939_911_724u, firstDerivedSeed);
        Assert.Equal(3_948_730_756u, secondDerivedSeed);
        Assert.True(secondDerivedSeed > Rng.MaxSupportedSeed);
        string firstHash = SimulationKinematicHash.Compute(first);
        _output.WriteLine($"CANONICAL_WANDER_KINEMATIC_HASH={firstHash}");
        Assert.Equal(
            firstHash,
            SimulationKinematicHash.Compute(second));
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void WorldFactories_RejectInvalidDeterminismInputs()
    {
        uint unsupportedSeed = Rng.MaxSupportedSeed + 1u;

        Assert.Throws<ArgumentOutOfRangeException>(() => new World(new SimConfig(), unsupportedSeed));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new World(new SimConfig { Seed = unsupportedSeed }));

        var overriddenConfig = new SimConfig { Seed = 7u };
        var overriddenWorld = new World(overriddenConfig, seed: 8u);
        var configuredWorld = new World(new SimConfig { Seed = 8u });
        Assert.Equal(7u, overriddenConfig.Seed);
        Assert.NotSame(overriddenConfig, overriddenWorld.Config);
        Assert.Equal(8u, overriddenWorld.Config.Seed);
        Assert.Equal(configuredWorld.Rng.Next(), overriddenWorld.Rng.Next());

        CanonicalWorldSettings settings = CreateCanonicalSettings(capacity: 8, seed: unsupportedSeed);
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCanonicalWorld(settings));

        var negativeTurnRate = new CanonicalWorldSettings { MaxTurnRateDegPerSecond = -1f };
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCanonicalWorld(negativeTurnRate));

        var nonFiniteTurnRate = new CanonicalWorldSettings { MaxTurnRateDegPerSecond = float.NaN };
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCanonicalWorld(nonFiniteTurnRate));
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void KinematicHash_ExcludesNonKinematicAgentState()
    {
        var legacyFirst = new World(new SimConfig(), seed: 42u);
        var legacySecond = new World(new SimConfig(), seed: 42u);
        int firstIndex = legacyFirst.AddAgent(10f, 20f, group: 0);
        int secondIndex = legacySecond.AddAgent(10f, 20f, group: 3);
        legacyFirst.Energy[firstIndex] = 10f;
        legacySecond.Energy[secondIndex] = 90f;
        legacySecond.State[secondIndex] = AgentState.Hunting;

        Assert.Equal(
            SimulationKinematicHash.Compute(legacyFirst),
            SimulationKinematicHash.Compute(legacySecond));

        CanonicalWorldSettings settings = CreateCanonicalSettings(capacity: 2, seed: 42u);
        CanonicalWorld canonicalFirst = CreateCanonicalWorld(settings);
        CanonicalWorld canonicalSecond = CreateCanonicalWorld(settings);
        Assert.True(canonicalFirst.TryAddBoid(new Vec2(10f, 20f), new Vec2(1f, 0f), group: 0));
        Assert.True(canonicalSecond.TryAddBoid(new Vec2(10f, 20f), new Vec2(1f, 0f), group: 3));

        Assert.Equal(
            SimulationKinematicHash.Compute(canonicalFirst),
            SimulationKinematicHash.Compute(canonicalSecond));
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void CanonicalWorld_DifferentSeeds_ProduceDifferentTrajectoryHashes()
    {
        CanonicalWorld first = CreateCanonicalWorld(CreateCanonicalSettings(capacity: 8, seed: 123u));
        CanonicalWorld second = CreateCanonicalWorld(CreateCanonicalSettings(capacity: 8, seed: 456u));
        AddCanonicalAgents(first);
        AddCanonicalAgents(second);

        Advance(first, TickCount);
        Advance(second, TickCount);

        Assert.NotEqual(
            SimulationKinematicHash.Compute(first),
            SimulationKinematicHash.Compute(second));
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void CanonicalWorld_ExtraAgentDoesNotChangeExistingWanderTrajectory()
    {
        CanonicalWorld alone = CreateCanonicalWorld(CreateCanonicalSettings(capacity: 8, seed: 321u));
        CanonicalWorld withExtra = CreateCanonicalWorld(CreateCanonicalSettings(capacity: 8, seed: 321u));
        var sharedPosition = new Vec2(500f, 700f);
        var sharedVelocity = new Vec2(1f, 2f);
        Assert.True(alone.TryAddBoid(sharedPosition, sharedVelocity));
        Assert.True(withExtra.TryAddBoid(sharedPosition, sharedVelocity));
        Assert.True(withExtra.TryAddBoid(new Vec2(8_000f, 8_000f), new Vec2(-2f, 1f)));

        Advance(alone, TickCount);
        Advance(withExtra, TickCount);

        AssertBoidStateEqual(alone.Boids[0], withExtra.Boids[0]);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void CanonicalWorld_CapacityDoesNotChangeWanderTrajectory()
    {
        CanonicalWorld small = CreateCanonicalWorld(CreateCanonicalSettings(capacity: 4, seed: 789u));
        CanonicalWorld large = CreateCanonicalWorld(CreateCanonicalSettings(capacity: 1024, seed: 789u));
        AddCanonicalAgents(small);
        AddCanonicalAgents(large);

        Advance(small, TickCount);
        Advance(large, TickCount);

        Assert.Equal(
            SimulationKinematicHash.Compute(small),
            SimulationKinematicHash.Compute(large));
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void CanonicalWorld_DefaultWander_IsDisabled()
    {
        Assert.Equal(0f, new CanonicalWorldSettings().WanderStrength);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void RendererCanonicalSettings_MapsEveryTrajectorySetting()
    {
        var config = new SimConfig
        {
            Seed = 987u,
            WorldWidth = 1_111f,
            WorldHeight = 2_222f,
            FixedDeltaTime = 0.0125f,
            MaxSpeed = 12.25f,
            MaxForce = 3.5f,
            SenseRadius = 91f,
            SeparationRadius = 23f,
            FieldOfView = 222f,
            SeparationWeight = 4.1f,
            AlignmentWeight = 5.2f,
            CohesionWeight = 6.3f,
            MaxNeighbors = 27,
            WanderStrength = 0.73f,
            WanderRate = 2.4f,
            MaxTurnRateDegPerSecond = 321f,
            WhiskerTimeHorizon = 0.67f,
            WhiskerWeight = 1.9f,
            SeparationPriorityRadiusFactor = 0.17f,
            SeparationPriorityExitFactor = 0.38f,
            SeparationPriorityBoost = 3.4f,
            SeparationPriorityHoldTime = 0.14f,
            SeparationPriorityRampInTime = 0.15f,
            SeparationPriorityRampOutTime = 0.16f,
            SeparationSpeedDroop = 0.07f
        };

        CanonicalWorldSettings settings = RenderProgram.BuildCanonicalWorldSettings(config, 600);

        Assert.Equal(1_200, settings.InitialCapacity);
        Assert.Equal(config.Seed, settings.Seed);
        Assert.Equal(config.WorldWidth, settings.WorldWidth);
        Assert.Equal(config.WorldHeight, settings.WorldHeight);
        Assert.Equal(config.FixedDeltaTime, settings.FixedDeltaTime);
        Assert.Equal(config.MaxSpeed, settings.TargetSpeed);
        Assert.Equal(config.MaxForce, settings.MaxForce);
        Assert.Equal(config.SenseRadius, settings.SenseRadius);
        Assert.Equal(config.SeparationRadius, settings.SeparationRadius);
        Assert.Equal(config.FieldOfView, settings.FieldOfView);
        Assert.Equal(config.SeparationWeight, settings.SeparationWeight);
        Assert.Equal(config.AlignmentWeight, settings.AlignmentWeight);
        Assert.Equal(config.CohesionWeight, settings.CohesionWeight);
        Assert.Equal(config.MaxNeighbors, settings.MaxNeighbors);
        Assert.Equal(config.WanderStrength, settings.WanderStrength);
        Assert.Equal(config.WanderRate, settings.WanderRate);
        Assert.Equal(config.MaxTurnRateDegPerSecond, settings.MaxTurnRateDegPerSecond);
        Assert.Equal(config.WhiskerTimeHorizon, settings.WhiskerTimeHorizon);
        Assert.Equal(config.WhiskerWeight, settings.WhiskerWeight);
        Assert.Equal(config.SeparationPriorityRadiusFactor, settings.SeparationPriorityRadiusFactor);
        Assert.Equal(config.SeparationPriorityExitFactor, settings.SeparationPriorityExitFactor);
        Assert.Equal(config.SeparationPriorityBoost, settings.SeparationPriorityBoost);
        Assert.Equal(config.SeparationPriorityHoldTime, settings.SeparationPriorityHoldTime);
        Assert.Equal(config.SeparationPriorityRampInTime, settings.SeparationPriorityRampInTime);
        Assert.Equal(config.SeparationPriorityRampOutTime, settings.SeparationPriorityRampOutTime);
        Assert.Equal(config.SeparationSpeedDroop, settings.SeparationSpeedDroop);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void MinimalHarnessRandomSamples_AreWorldSeeded()
    {
        var first = new World(new SimConfig(), seed: 42u);
        var matching = new World(new SimConfig(), seed: 42u);
        var different = new World(new SimConfig { Seed = 43u }, seed: 43u);
        bool observedDifferentSeed = false;

        for (int i = 0; i < 8; i++)
        {
            float firstSample = MinimalTestProgram.NextCenteredSample(first, halfRange: 200f);
            float matchingSample = MinimalTestProgram.NextCenteredSample(matching, halfRange: 200f);
            float differentSample = MinimalTestProgram.NextCenteredSample(different, halfRange: 200f);

            Assert.Equal(
                BitConverter.SingleToUInt32Bits(firstSample),
                BitConverter.SingleToUInt32Bits(matchingSample));
            observedDifferentSeed |= BitConverter.SingleToUInt32Bits(firstSample)
                != BitConverter.SingleToUInt32Bits(differentSample);
            Assert.InRange(firstSample, -200f, 200f);
        }

        Assert.True(observedDifferentSeed);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void RendererCanonicalFactory_SeedControlsInitialStateAndTrajectory()
    {
        CanonicalWorldSettings firstSettings = RenderProgram.BuildCanonicalWorldSettings(
            new SimConfig { Seed = 123u, WanderStrength = 0.4f },
            initialAgentCount: 32);
        CanonicalWorldSettings matchingSettings = RenderProgram.BuildCanonicalWorldSettings(
            new SimConfig { Seed = 123u, WanderStrength = 0.4f },
            initialAgentCount: 32);
        CanonicalWorldSettings differentSettings = RenderProgram.BuildCanonicalWorldSettings(
            new SimConfig { Seed = 456u, WanderStrength = 0.4f },
            initialAgentCount: 32);

        CanonicalWorld first = RenderProgram.CreateCanonicalWorld(firstSettings, agentCount: 32);
        CanonicalWorld matching = RenderProgram.CreateCanonicalWorld(matchingSettings, agentCount: 32);
        CanonicalWorld different = RenderProgram.CreateCanonicalWorld(differentSettings, agentCount: 32);

        string initialHash = SimulationKinematicHash.Compute(first);
        Assert.Equal(initialHash, SimulationKinematicHash.Compute(matching));
        Assert.NotEqual(initialHash, SimulationKinematicHash.Compute(different));

        Advance(first, TickCount);
        Advance(matching, TickCount);
        Advance(different, TickCount);

        string trajectoryHash = SimulationKinematicHash.Compute(first);
        Assert.Equal(trajectoryHash, SimulationKinematicHash.Compute(matching));
        Assert.NotEqual(trajectoryHash, SimulationKinematicHash.Compute(different));
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void DefaultAndBalancedConfig_MapCanonicalWanderIntentionally()
    {
        CanonicalWorldSettings defaults = RenderProgram.BuildCanonicalWorldSettings(
            RenderProgram.CreateDefaultBaseConfig(),
            32);
        string configPath = Path.Combine(AppContext.BaseDirectory, "configs", "balanced.json");
        SimConfig balanced = SimConfig.LoadFromJson(configPath);
        CanonicalWorldSettings authored = RenderProgram.BuildCanonicalWorldSettings(balanced, 32);

        Assert.Equal(0f, defaults.WanderStrength);
        Assert.Equal(0.45f, authored.WanderStrength);
        Assert.Equal(42u, authored.Seed);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void RendererDefaultCanonicalTrajectory_EmitsIntentionalAfterHash()
    {
        CanonicalWorldSettings settings = RenderProgram.BuildCanonicalWorldSettings(
            RenderProgram.CreateDefaultBaseConfig(),
            initialAgentCount: 64);
        CanonicalWorld world = RenderProgram.CreateCanonicalWorld(settings, agentCount: 64);

        _output.WriteLine($"CANONICAL_DEFAULT_INITIAL_KINEMATIC_HASH_AFTER={SimulationKinematicHash.Compute(world)}");
        Advance(world, TickCount);
        _output.WriteLine($"CANONICAL_DEFAULT_FINAL_KINEMATIC_HASH_AFTER={SimulationKinematicHash.Compute(world)}");

        Assert.Equal(0f, settings.WanderStrength);
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public async Task BalancedConfig_IsBitIdenticalAcrossTwoHeadlessProcesses()
    {
        string firstHash = await RunHeadlessBalancedBenchmark();
        string secondHash = await RunHeadlessBalancedBenchmark();

        _output.WriteLine($"HEADLESS_KINEMATIC_HASH={firstHash}");
        Assert.Equal(firstHash, secondHash);
    }

    private static CanonicalWorldSettings CreateCanonicalSettings(int capacity, uint seed) => new()
    {
        InitialCapacity = capacity,
        TargetSpeed = 3f,
        MaxForce = 0.5f,
        SenseRadius = 5f,
        SeparationRadius = 2f,
        SeparationWeight = 0f,
        AlignmentWeight = 0f,
        CohesionWeight = 0f,
        FieldOfView = 360f,
        MaxNeighbors = 8,
        MaxTurnRateDegPerSecond = 720f,
        WanderStrength = 0.4f,
        WanderRate = 1.25f,
        WhiskerWeight = 0f,
        Seed = seed,
        WorldWidth = 10_000f,
        WorldHeight = 10_000f,
        FixedDeltaTime = 1f / 60f
    };

    private static CanonicalWorld CreateCanonicalWorld(CanonicalWorldSettings settings) => new(
        settings,
        new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));

    private static void AddCanonicalAgents(CanonicalWorld world)
    {
        for (int i = 0; i < 4; i++)
        {
            Vec2 position = new(500f + i * 1000f, 700f + i * 900f);
            Vec2 velocity = new(1f + i, 2f - i * 0.25f);
            Assert.True(world.TryAddBoid(position, velocity));
        }
    }

    private static void AssertBoidStateEqual(Boid expected, Boid actual)
    {
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(expected.Position.X),
            BitConverter.SingleToUInt32Bits(actual.Position.X));
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(expected.Position.Y),
            BitConverter.SingleToUInt32Bits(actual.Position.Y));
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(expected.Velocity.X),
            BitConverter.SingleToUInt32Bits(actual.Velocity.X));
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(expected.Velocity.Y),
            BitConverter.SingleToUInt32Bits(actual.Velocity.Y));
    }

    private static async Task<string> RunHeadlessBalancedBenchmark()
    {
        string rendererPath = Path.Combine(AppContext.BaseDirectory, "SwarmSim.Render.dll");
        string configPath = Path.Combine(AppContext.BaseDirectory, "configs", "balanced.json");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add(rendererPath);
        startInfo.ArgumentList.Add("--benchmark");
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(configPath);
        startInfo.ArgumentList.Add("--agent-count");
        startInfo.ArgumentList.Add("32");

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start());
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process exited after HasExited was sampled.
                }
                await process.WaitForExitAsync();
            }

            string timedOutOutput = await standardOutput;
            string timedOutError = await standardError;
            throw new TimeoutException(
                $"Headless renderer did not exit within 30 seconds.{Environment.NewLine}" +
                $"{timedOutOutput}{Environment.NewLine}{timedOutError}");
        }
        string output = await standardOutput;
        string error = await standardError;

        Assert.True(
            process.ExitCode == 0,
            $"Headless renderer exited {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        string[] hashLines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("KinematicHash: ", StringComparison.Ordinal))
            .ToArray();
        string hashLine = Assert.Single(hashLines);
        string hash = hashLine["KinematicHash: ".Length..];
        Assert.Matches("^[0-9A-F]{64}$", hash);
        return hash;
    }

    private static void Advance(World world, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            world.Tick();
        }
    }

    private static void Advance(CanonicalWorld world, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            world.Step(world.Settings.FixedDeltaTime);
        }
    }
}
