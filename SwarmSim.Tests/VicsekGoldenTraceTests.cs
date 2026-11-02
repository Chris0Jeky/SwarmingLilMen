using System.Globalization;
using System.Runtime.InteropServices;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Vicsek;
using Xunit.Abstractions;

namespace SwarmSim.Tests;

[Trait("Category", "Determinism")]
public sealed class VicsekGoldenTraceTests
{
    private const float Noise = 0.75f;
    private static readonly int[] CheckpointTicks = [0, 1, 16, 64, 256];
    private readonly ITestOutputHelper _output;

    public VicsekGoldenTraceTests(ITestOutputHelper output)
    {
        _output = output;
        _output.WriteLine($"GOLDEN_REFERENCE engine=vicsek {VicsekGoldenTraceFixtures.Provenance}");
        _output.WriteLine($"GOLDEN_RUNTIME engine=vicsek {RuntimeInformation.FrameworkDescription} / {RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture}");
        Assert.True(Environment.Version.Major == 8 && RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
            (OperatingSystem.IsLinux() || OperatingSystem.IsWindows()),
            "Vicsek golden references require .NET 8 / x64 / Linux or Windows. Record and review a separate reference before extending this band; never skip or regenerate fixtures at test time.");
    }

    [Fact]
    public void CheckpointsMatchTheCommittedTrace()
    {
        var world = CreateWorld();
        GoldenCheckpoint[]? expected = VicsekGoldenTraceFixtures.Trace;
        var actual = new List<GoldenCheckpoint>();
        foreach (int tick in CheckpointTicks)
        {
            while (world.TickCount < tick)
                world.Step();

            string hash = SimulationKinematicHash.Compute(world);
            uint[] bits = KinematicBits(world);
            actual.Add(new GoldenCheckpoint(tick, hash, bits));
            _output.WriteLine($"GOLDEN_TRACE engine=vicsek tick={tick} actual={hash}");
            _output.WriteLine($"GOLDEN_STATE engine=vicsek tick={tick} bits={Hex(bits)}");
        }

        // Always emit every checkpoint before failing, including on a platform whose reference
        // is being recorded for review. There is no switch that accepts or writes new values.
        Assert.NotNull(expected);
        Assert.Equal(CheckpointTicks, expected.Select(checkpoint => checkpoint.Tick));
        var failures = new List<string>();
        for (int i = 0; i < expected.Length; i++)
        {
            string? failure = CompareCheckpoint(expected[i], actual[i]);
            if (failure is not null)
                failures.Add(failure);
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void IncreasingNoiseByOneUlpChangesTheGoldenTrace()
    {
        GoldenCheckpoint[]? expected = VicsekGoldenTraceFixtures.Trace;
        Assert.NotNull(expected);
        float changedNoise = float.BitIncrement(Noise);
        uint originalBits = BitConverter.SingleToUInt32Bits(Noise);
        uint changedBits = BitConverter.SingleToUInt32Bits(changedNoise);
        Assert.Equal(originalBits + 1u, changedBits);
        var world = CreateWorld(changedNoise);
        Assert.Equal(expected[0].Hash, SimulationKinematicHash.Compute(world));

        bool driftObserved = false;
        foreach (GoldenCheckpoint checkpoint in expected)
        {
            while (world.TickCount < checkpoint.Tick)
                world.Step();
            string actualHash = SimulationKinematicHash.Compute(world);
            if (actualHash != checkpoint.Hash)
            {
                driftObserved = true;
                _output.WriteLine($"GOLDEN_ONE_ULP engine=vicsek field=Noise beforeBits={originalBits:X8} afterBits={changedBits:X8} tick={checkpoint.Tick} expected={checkpoint.Hash} actual={actualHash}");
            }
        }
        Assert.True(driftObserved, "The Vicsek fixture must detect a one-ULP noise-amplitude change.");
    }

    private static VicsekWorld CreateWorld(float noise = Noise)
    {
        var settings = new VicsekSettings
        {
            Seed = 424242u,
            Noise = noise,
            Speed = 0.125f,
            InteractionRadius = 2f,
            WorldWidth = 16f,
            WorldHeight = 12f,
            FixedDeltaTime = 1f
        };
        // Exact binary positions and zero headings avoid random spawning. Noise introduces
        // distinct headings after tick 1; the trace includes a wrap-seam pair, a three-agent
        // chain, an inclusive-radius pair, and an initially isolated agent, in a fixed order.
        Vec2[] positions =
        [
            new(0.25f, 0.25f), new(15.75f, 11.75f),
            new(4f, 4f), new(5.5f, 4f), new(7f, 4f),
            new(8f, 9f), new(10f, 9f), new(13f, 6f)
        ];
        float[] headings = [0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f];
        return new VicsekWorld(settings, positions, headings);
    }

    private static uint[] KinematicBits(VicsekWorld world)
    {
        var bits = new uint[world.Agents.Length * 4];
        for (int i = 0; i < world.Agents.Length; i++)
        {
            Boid agent = world.Agents[i];
            bits[i * 4] = BitConverter.SingleToUInt32Bits(agent.Position.X);
            bits[i * 4 + 1] = BitConverter.SingleToUInt32Bits(agent.Position.Y);
            bits[i * 4 + 2] = BitConverter.SingleToUInt32Bits(agent.Velocity.X);
            bits[i * 4 + 3] = BitConverter.SingleToUInt32Bits(agent.Velocity.Y);
        }
        return bits;
    }

    private string? CompareCheckpoint(GoldenCheckpoint expected, GoldenCheckpoint actual)
    {
        _output.WriteLine($"GOLDEN_COMPARE engine=vicsek tick={expected.Tick} expected={expected.Hash} actual={actual.Hash}");
        if (expected.Hash == actual.Hash && expected.Bits.AsSpan().SequenceEqual(actual.Bits))
            return null;

        int firstDifference = -1;
        double maxAbsoluteDifference = 0d;
        long maxUlpDifference = 0;
        for (int i = 0; i < Math.Min(expected.Bits.Length, actual.Bits.Length); i++)
        {
            uint before = expected.Bits[i];
            uint after = actual.Bits[i];
            if (before != after && firstDifference < 0)
                firstDifference = i;
            maxAbsoluteDifference = Math.Max(maxAbsoluteDifference,
                Math.Abs((double)BitConverter.UInt32BitsToSingle(before) - BitConverter.UInt32BitsToSingle(after)));
            maxUlpDifference = Math.Max(maxUlpDifference, Math.Abs(OrderedBits(before) - OrderedBits(after)));
        }

        string first = firstDifference < 0 ? "none" :
            $"agent={firstDifference / 4},component={new[] { "X", "Y", "Vx", "Vy" }[firstDifference % 4]},expectedBits={expected.Bits[firstDifference]:X8},actualBits={actual.Bits[firstDifference]:X8}";
        string failure = $"GOLDEN_DRIFT engine=vicsek tick={expected.Tick} expected={expected.Hash} actual={actual.Hash} expectedComponents={expected.Bits.Length} actualComponents={actual.Bits.Length} first={first} maxAbsolute={maxAbsoluteDifference.ToString("R", CultureInfo.InvariantCulture)} maxUlp={maxUlpDifference}";
        _output.WriteLine(failure);
        return failure;
    }

    private static string Hex(uint[] values) => string.Join(",", values.Select(value => value.ToString("X8")));

    // Numerical ULP distance treats signed zeros equally; the raw comparisons above do not.
    private static long OrderedBits(uint bits) => (bits & 0x80000000u) == 0
        ? bits
        : -(long)(bits & 0x7FFFFFFFu);
}
