using System.Text;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public sealed class NamedRandomStreamsRngTests
{
    [Fact]
    public void NamedAgentStreamMatchesAnIndependentCommittedReference()
    {
        var factory = new RandomStreams(424242);
        uint[] expected = { 0x37866102u, 0x9A783A27u, 0x039415F6u, 0x8A05EE6Du, 0x0E80ACC1u, 0xB2F2969Du, 0xEDBDDE8Cu, 0xC70F189Bu };
        Assert.Equal(expected, Take(factory.CreateStream("canonical.wander", 0), expected.Length));
        uint[] worldExpected = { 0x5FA9E541u, 0x6D06A519u, 0xEADA0C03u, 0xA290A695u, 0x2443CFE2u, 0x213531E0u, 0x35467947u, 0x47C737E1u };
        Assert.Equal(worldExpected, Take(factory.CreateStream("canonical.wander"), worldExpected.Length));
    }

    [Fact]
    public void FullWidthSeedsAndAgentIdsHaveACommittedReference()
    {
        var factory = new RandomStreams(ulong.MaxValue);
        uint[] expected = { 0xC7036F94u, 0x66AA7367u, 0xEDF3FE6Du, 0x6CD178FAu, 0x2916DA36u, 0x4E8C2BF1u, 0x04C1B274u, 0x63DC33D4u };
        Assert.Equal(expected, Take(factory.CreateStream("vicsek.noise", ulong.MaxValue), expected.Length));
    }

    [Fact]
    public void CanonicalAgentStateIsCompactAndContainsNoManagedReferences()
    {
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Pcg32State>());
        Assert.Equal(16, System.Runtime.CompilerServices.Unsafe.SizeOf<Pcg32State>());
        var factory = new RandomStreams(424242);
        Pcg32State state = factory.CreateAgentState("canonical.wander", 0);
        Pcg32State copied = state;
        Pcg32Rng reference = factory.CreateStream("canonical.wander", 0);
        for (int i = 0; i < 64; i++)
        {
            uint expected = reference.NextUInt32();
            Assert.Equal(expected, state.NextUInt32());
            Assert.Equal(expected, copied.NextUInt32());
        }
    }

    [Fact]
    public void OldSignedSystemRandomAliasPairHasDistinctPinnedStreams()
    {
        // Issue #26: old uint seeds for these insertion IDs were +/-539281416 after
        // conversion, and System.Random treated the positive/negative seed as the same.
        var factory = new RandomStreams(42);
        uint[] first = { 0x6797284Eu, 0x9DA53427u, 0x32127612u, 0x7B368FE7u, 0xAD66A926u, 0x8A266EEDu, 0x3055076Cu, 0x4700602Fu };
        uint[] second = { 0x5B47F306u, 0xE55A5237u, 0x989F47CEu, 0x01B04027u, 0x1E7F822Du, 0xCAB2978Bu, 0x52B9E820u, 0x5034A7E6u };
        Assert.Equal(first, Take(factory.CreateStream("canonical.wander", 108142), first.Length));
        Assert.Equal(second, Take(factory.CreateStream("canonical.wander", 108358), second.Length));
        Assert.False(first.AsSpan().SequenceEqual(second));
    }

    [Fact]
    public void NamesUseStrictUtf8WithNoCultureOrNormalization()
    {
        var factory = new RandomStreams(0);
        uint[] expected = { 0x10647A64u, 0x17550DABu, 0xADE0EBD2u, 0x8CB17E90u, 0xCA6D17D8u, 0x779CB36Cu, 0xCA5B5737u, 0x14197609u };
        Assert.Equal(expected, Take(factory.CreateStream("é/👣", 42), expected.Length));
        Assert.NotEqual(Take(factory.CreateStream("é/👣", 42), 16), Take(factory.CreateStream("e\u0301/👣", 42), 16));
        Assert.Throws<EncoderFallbackException>(() => factory.CreateStream("bad\uD800"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingNamesAreRejected(string? name)
    {
        var factory = new RandomStreams(42);
        Assert.ThrowsAny<ArgumentException>(() => factory.CreateStream(name!));
    }

    [Fact]
    public void CreationOrderAndDrawsFromOtherStreamsDoNotChangeASequence()
    {
        var first = new RandomStreams(42);
        var second = new RandomStreams(42);
        var a = first.CreateStream("wander", 7);
        var unrelated = second.CreateStream("exploration", 7);
        var otherAgent = second.CreateStream("wander", 8);
        var b = second.CreateStream("wander", 7);
        for (int i = 0; i < 1024; i++)
        {
            for (int j = 0; j < i % 13; j++)
            {
                unrelated.NextUInt32();
                otherAgent.NextDouble();
            }
            Assert.Equal(a.NextUInt32(), b.NextUInt32());
        }
    }

    [Fact]
    public void RecreatingAStreamRestartsItAndDoesNotShareMutableState()
    {
        var streams = new RandomStreams(42);
        var a = streams.CreateStream("test", 9);
        uint[] expected = Take(a, 32);
        var b = streams.CreateStream("test", 9);
        a.NextUInt32();
        Assert.Equal(expected, Take(b, 32));
    }

    [Fact]
    public void NameAgentAndSeedAreAllPartOfTheIdentity()
    {
        var streams = new RandomStreams(42);
        uint[] reference = Take(streams.CreateStream("noise", 0), 32);
        Assert.NotEqual(reference, Take(streams.CreateStream("Noise", 0), 32));
        Assert.NotEqual(reference, Take(streams.CreateStream("noise", 1), 32));
        Assert.NotEqual(reference, Take(streams.CreateStream("noise"), 32));
        Assert.NotEqual(reference, Take(new RandomStreams(43).CreateStream("noise", 0), 32));
    }

    [Fact]
    [Trait("Category", "Determinism")]
    public void NewStochasticConsumerDoesNotPerturbExistingCanonicalTrajectories()
    {
        var reference = GoldenTraceScenarios.CreateCanonical();
        var extended = GoldenTraceScenarios.CreateCanonical();
        var consumer = new SamplingRule(extended.RandomStreams, extended.Count);
        extended.AddRule(consumer);
        for (int tick = 0; tick < 256; tick++)
        {
            reference.Step(reference.Settings.FixedDeltaTime);
            extended.Step(extended.Settings.FixedDeltaTime);
            Assert.Equal(SimulationKinematicHash.Compute(reference), SimulationKinematicHash.Compute(extended));
        }
        Assert.Equal(256 * extended.Count, consumer.DrawCount);
    }

    private static uint[] Take(Pcg32Rng rng, int count)
    {
        var values = new uint[count];
        for (int i = 0; i < count; i++) values[i] = rng.NextUInt32();
        return values;
    }

    private sealed class SamplingRule : IRule
    {
        private readonly Pcg32Rng[] _noise;
        public int DrawCount { get; private set; }

        internal SamplingRule(RandomStreams streams, int count)
        {
            _noise = new Pcg32Rng[count];
            for (int i = 0; i < count; i++)
                _noise[i] = streams.CreateStream("test.new-consumer", (ulong)i);
        }

        public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids,
            ReadOnlySpan<int> neighborIndices, ReadOnlySpan<float> neighborWeights, RuleContext context)
        {
            _noise[selfIndex].NextUInt32();
            DrawCount++;
            return Vec2.Zero;
        }
    }
}
