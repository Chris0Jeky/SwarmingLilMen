using System.Globalization;
using System.Runtime.InteropServices;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Utils;
using Xunit.Abstractions;

namespace SwarmSim.Tests;

[Trait("Category", "Determinism")]
public sealed class GoldenTraceTests
{
    private readonly ITestOutputHelper _output;

    public GoldenTraceTests(ITestOutputHelper output)
    {
        _output = output;
        _output.WriteLine($"GOLDEN_REFERENCE {GoldenTraceFixtures.Provenance}");
        _output.WriteLine($"GOLDEN_RUNTIME {RuntimeInformation.FrameworkDescription} / {RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture}");
        Assert.True(Environment.Version.Major == 8 && RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
            (OperatingSystem.IsLinux() || OperatingSystem.IsWindows()),
            "The reviewed golden-reference band is .NET 8 / x64 / Linux or Windows. Add measured, reviewed reference data before extending this band; never skip or regenerate fixtures at test time.");
    }

    [Fact]
    public void LegacyCheckpointsMatchTheCommittedTrace()
    {
        var world = GoldenTraceScenarios.CreateLegacy();
        AssertTrace("legacy", GoldenTraceFixtures.Legacy,
            () => world.TickCount,
            world.Tick,
            () => SimulationKinematicHash.Compute(world),
            () => GoldenTraceScenarios.KinematicBits(world));
    }

    [Fact]
    public void CanonicalCheckpointsMatchTheCommittedTrace()
    {
        var world = GoldenTraceScenarios.CreateCanonical();
        AssertTrace("canonical", GoldenTraceFixtures.Canonical,
            () => world.TickCount,
            () => world.Step(world.Settings.FixedDeltaTime),
            () => SimulationKinematicHash.Compute(world),
            () => GoldenTraceScenarios.KinematicBits(world));
    }

    [Fact]
    public void RngNextMatchesTheCommittedSequence()
    {
        var rng = new Rng(GoldenTraceScenarios.Seed);
        var actual = new int[GoldenTraceFixtures.NextValues.Length];
        for (int i = 0; i < actual.Length; i++)
            actual[i] = rng.Next();

        _output.WriteLine($"GOLDEN_RNG kind=Next seed={GoldenTraceScenarios.Seed} expected={string.Join(",", GoldenTraceFixtures.NextValues)} actual={string.Join(",", actual)}");
        Assert.Equal(GoldenTraceFixtures.NextValues, actual);
    }

    [Fact]
    public void RngNextFloatMatchesTheCommittedBits()
    {
        var rng = new Rng(GoldenTraceScenarios.Seed);
        var actual = new uint[GoldenTraceFixtures.NextFloatBits.Length];
        for (int i = 0; i < actual.Length; i++)
            actual[i] = BitConverter.SingleToUInt32Bits(rng.NextFloat());

        _output.WriteLine($"GOLDEN_RNG kind=NextFloat seed={GoldenTraceScenarios.Seed} expected={Hex(GoldenTraceFixtures.NextFloatBits)} actual={Hex(actual)}");
        Assert.Equal(GoldenTraceFixtures.NextFloatBits, actual);
    }

    [Fact]
    public void RngNextDoubleMatchesTheCommittedBits()
    {
        var rng = new Rng(GoldenTraceScenarios.Seed);
        var actual = new ulong[GoldenTraceFixtures.NextDoubleBits.Length];
        for (int i = 0; i < actual.Length; i++)
            actual[i] = BitConverter.DoubleToUInt64Bits(rng.NextDouble());

        string expectedText = string.Join(",", GoldenTraceFixtures.NextDoubleBits.Select(value => value.ToString("X16")));
        string actualText = string.Join(",", actual.Select(value => value.ToString("X16")));
        _output.WriteLine($"GOLDEN_RNG kind=NextDouble seed={GoldenTraceScenarios.Seed} expected={expectedText} actual={actualText}");
        Assert.Equal(GoldenTraceFixtures.NextDoubleBits, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncreasingTheForceBudgetByOneUlpChangesTheGoldenTrace(bool canonical)
    {
        if (canonical)
        {
            float changedForce = float.BitIncrement(GoldenTraceScenarios.CanonicalMaxForce);
            var world = GoldenTraceScenarios.CreateCanonical(changedForce);
            AssertPerturbation("canonical", GoldenTraceFixtures.Canonical,
                GoldenTraceScenarios.CanonicalMaxForce, changedForce,
                () => world.TickCount,
                () => world.Step(world.Settings.FixedDeltaTime),
                () => SimulationKinematicHash.Compute(world));
        }
        else
        {
            float changedForce = float.BitIncrement(GoldenTraceScenarios.LegacyMaxForce);
            var world = GoldenTraceScenarios.CreateLegacy(changedForce);
            AssertPerturbation("legacy", GoldenTraceFixtures.Legacy,
                GoldenTraceScenarios.LegacyMaxForce, changedForce,
                () => world.TickCount,
                world.Tick,
                () => SimulationKinematicHash.Compute(world));
        }
    }

    private void AssertTrace(
        string engine,
        GoldenCheckpoint[] expected,
        Func<ulong> tick,
        Action step,
        Func<string> hash,
        Func<uint[]> bits)
    {
        var failures = new List<string>();
        foreach (var checkpoint in expected)
        {
            while (tick() < (ulong)checkpoint.Tick)
                step();

            string? failure = CompareCheckpoint(engine, checkpoint, hash(), bits());
            if (failure is not null)
                failures.Add(failure);
        }

        // Collect all checkpoints first so cross-OS failures report both the first difference
        // and its later magnitude; an early mismatch must not hide the rest of the trace.
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private string? CompareCheckpoint(string engine, GoldenCheckpoint expected, string actualHash, uint[] actualBits)
    {
        _output.WriteLine($"GOLDEN_TRACE engine={engine} tick={expected.Tick} expected={expected.Hash} actual={actualHash}");
        _output.WriteLine($"GOLDEN_STATE engine={engine} tick={expected.Tick} bits={Hex(actualBits)}");
        if (expected.Hash == actualHash && expected.Bits.AsSpan().SequenceEqual(actualBits))
            return null;

        int firstDifference = -1;
        double maxAbsoluteDifference = 0d;
        long maxUlpDifference = 0;
        for (int i = 0; i < Math.Min(expected.Bits.Length, actualBits.Length); i++)
        {
            uint before = expected.Bits[i];
            uint after = actualBits[i];
            if (before != after && firstDifference < 0)
                firstDifference = i;
            maxAbsoluteDifference = Math.Max(maxAbsoluteDifference,
                Math.Abs((double)BitConverter.UInt32BitsToSingle(before) - BitConverter.UInt32BitsToSingle(after)));
            maxUlpDifference = Math.Max(maxUlpDifference, Math.Abs(OrderedBits(before) - OrderedBits(after)));
        }

        string first = firstDifference < 0 ? "none" :
            $"agent={firstDifference / 4},component={new[] { "X", "Y", "Vx", "Vy" }[firstDifference % 4]},expectedBits={expected.Bits[firstDifference]:X8},actualBits={actualBits[firstDifference]:X8}";
        string failure = $"GOLDEN_DRIFT engine={engine} tick={expected.Tick} expected={expected.Hash} actual={actualHash} expectedComponents={expected.Bits.Length} actualComponents={actualBits.Length} first={first} maxAbsolute={maxAbsoluteDifference.ToString("R", CultureInfo.InvariantCulture)} maxUlp={maxUlpDifference}";
        _output.WriteLine(failure);
        return failure;
    }

    private void AssertPerturbation(
        string engine,
        GoldenCheckpoint[] expected,
        float originalForce,
        float changedForce,
        Func<ulong> tick,
        Action step,
        Func<string> hash)
    {
        uint originalBits = BitConverter.SingleToUInt32Bits(originalForce);
        uint changedBits = BitConverter.SingleToUInt32Bits(changedForce);
        Assert.Equal(originalBits + 1u, changedBits);
        Assert.Equal(expected[0].Hash, hash()); // The changed setting has not altered initial state.

        bool driftObserved = false;
        foreach (var checkpoint in expected)
        {
            while (tick() < (ulong)checkpoint.Tick)
                step();
            string actualHash = hash();
            if (actualHash != checkpoint.Hash)
            {
                driftObserved = true;
                _output.WriteLine($"GOLDEN_ONE_ULP engine={engine} field=MaxForce beforeBits={originalBits:X8} afterBits={changedBits:X8} tick={checkpoint.Tick} expected={checkpoint.Hash} actual={actualHash}");
            }
        }
        Assert.True(driftObserved, $"The {engine} fixture must detect a one-ULP force-budget change.");
    }

    private static string Hex(uint[] values) => string.Join(",", values.Select(value => value.ToString("X8")));

    // Monotone IEEE-754 ordering for finite values; positive/negative zero share a numerical
    // ULP distance of zero, but the raw bit and hash comparisons still distinguish them.
    private static long OrderedBits(uint bits) => (bits & 0x80000000u) == 0
        ? bits
        : -(long)(bits & 0x7FFFFFFFu);
}
