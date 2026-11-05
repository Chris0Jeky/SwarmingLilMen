using System.Reflection;
using System.Security.Cryptography;
using SwarmSim.Core;
using SwarmSim.Core.Aco;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Utils;
using SwarmSim.Core.Vicsek;

namespace SwarmSim.Tests;

public sealed class ModelStateHashTests
{
    [Fact]
    public void WriterMatchesAnIndependentLittleEndianBinaryFixture()
    {
        // Independently encoded with Python struct.pack and hashlib, without this writer.
        const string bytes = "537761726D53696D2E4D6F64656C5374617465000D000000746573742D73746174652D7631" +
            "7929EDFFEFCDAB89F8F8F9FAFBFCFDFE1032547698BADCFE00000080000000000000F83F0100";
        const string expected = "75221D42CFD790BB51AAB8AAA43AD08CDFC736332CB3439B43E41CAC5CCA991D";
        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(Convert.FromHexString(bytes))));

        using var writer = new StateHashWriter("test-state-v1");
        writer.WriteInt32(-1234567);
        writer.WriteUInt32(0x89ABCDEFu);
        writer.WriteInt64(-0x0102030405060708L);
        writer.WriteUInt64(0xFEDCBA9876543210ul);
        writer.WriteSingle(BitConverter.UInt32BitsToSingle(0x80000000u));
        writer.WriteDouble(1.5d);
        writer.WriteBoolean(true);
        writer.WriteBoolean(false);

        Assert.Equal(new ModelStateHash("test-state-v1", expected), writer.Finish());
        Assert.Throws<InvalidOperationException>(() => writer.Finish());
        Assert.Throws<InvalidOperationException>(() => writer.WriteInt32(0));
    }

    [Fact]
    public void ProfilesAndSignedZeroRemainDistinct()
    {
        static string Hash(string profile, uint bits)
        {
            using var writer = new StateHashWriter(profile);
            writer.WriteSingle(BitConverter.UInt32BitsToSingle(bits));
            return writer.Finish().Hash;
        }

        Assert.NotEqual(Hash("test-state-v1", 0), Hash("test-state-v2", 0));
        Assert.NotEqual(Hash("test-state-v1", 0), Hash("test-state-v1", 0x80000000u));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Model-v1")]
    [InlineData("model_1")]
    [InlineData("modél-v1")]
    public void WriterRejectsAmbiguousProfileIdentifiers(string profile) =>
        Assert.Throws<ArgumentException>(() => new StateHashWriter(profile));

    [Fact]
    public void RandomStateObservationDoesNotConsumeDraws()
    {
        var observed = new Pcg32Rng(42, 54);
        var control = new Pcg32Rng(42, 54);
        using var first = new StateHashWriter("rng-state-v1");
        first.WriteRandomState(observed);
        ModelStateHash before = first.Finish();
        Assert.Equal(control.NextUInt32(), observed.NextUInt32());
        using var second = new StateHashWriter("rng-state-v1");
        second.WriteRandomState(observed);
        Assert.NotEqual(before, second.Finish());
        Assert.Equal(control.NextUInt32(), observed.NextUInt32());
    }

    [Fact]
    public void AcoFieldMutationChangesOnlyTheModelStateFingerprint()
    {
        var world = new AntColonyWorld(new AntColonySettings(), 2);
        string motion = SimulationKinematicHash.Compute(world);
        ModelStateHash before = world.StateHash();

        world.Field.Deposit(10f, 20f, 0.5f);

        Assert.Equal(motion, SimulationKinematicHash.Compute(world));
        Assert.NotEqual(before, world.StateHash());
    }

    [Fact]
    public void AcoLoadAndFoodAreVisibleWhenKinematicsMatch()
    {
        var settings = new AntColonySettings { FoodAmount = 1 };
        Ant ant = new(new Vec2(60f, 60f), 0.5f, Carrying: false);
        var empty = new AntColonyWorld(settings, new[] { ant });
        var loaded = new AntColonyWorld(settings, new[] { ant with { Carrying = true } });

        Assert.Equal(SimulationKinematicHash.Compute(empty), SimulationKinematicHash.Compute(loaded));
        Assert.NotEqual(empty.StateHash(), loaded.StateHash());
        Assert.Equal(1, empty.FoodRemaining);
        Assert.Equal(0, loaded.FoodRemaining);
    }

    [Fact]
    public void PendingDepositPositionsAreVisibleBeforeTheFieldPhase()
    {
        var world = new AntColonyWorld(new AntColonySettings(),
            new[] { new Ant(new Vec2(60f, 60f), 0f, Carrying: true) });
        world.AgentPhase();
        Assert.Equal(1, world.PendingDeposits);
        Assert.Equal(0d, world.Field.Total());
        string motion = SimulationKinematicHash.Compute(world);
        ModelStateHash before = world.StateHash();

        // Fault injection changes only hidden, already-buffered state, without advancing a tick.
        Vec2[] deposits = PrivateField<Vec2[]>(world, "_deposits");
        deposits[0] = new Vec2(deposits[0].X + 1f, deposits[0].Y);

        Assert.Equal(motion, SimulationKinematicHash.Compute(world));
        Assert.NotEqual(before, world.StateHash());
        Assert.Equal(0, world.TickCount);
    }

    [Fact]
    public void AcoForageDrawPositionIsVisibleWithoutMotionOrClockChanges()
    {
        var world = new AntColonyWorld(new AntColonySettings(), 2);
        ModelStateHash before = world.StateHash();
        string motion = SimulationKinematicHash.Compute(world);

        PrivateField<Pcg32Rng[]>(world, "_forage")[1].NextUInt32();

        Assert.Equal(motion, SimulationKinematicHash.Compute(world));
        Assert.Equal(0, world.TickCount);
        Assert.NotEqual(before, world.StateHash());
    }

    [Fact]
    public void AcoPhaseProgressIsVisibleWithoutChangingAnyOtherState()
    {
        var world = new AntColonyWorld(new AntColonySettings(), 2);
        ModelStateHash before = world.StateHash();
        string motion = SimulationKinematicHash.Compute(world);

        // Isolate the phase flag: it changes which operation may legally execute next.
        typeof(AntColonyWorld).GetField("_agentPhaseDone", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world, true);

        Assert.Equal(motion, SimulationKinematicHash.Compute(world));
        Assert.Equal(0, world.TickCount);
        Assert.NotEqual(before, world.StateHash());
        Assert.Throws<InvalidOperationException>(() => world.AgentPhase());
    }

    [Fact]
    public void VicsekNoiseDrawPositionIsVisibleWithoutMotionOrClockChanges()
    {
        var world = new VicsekWorld(new VicsekSettings(), 2);
        ModelStateHash before = world.StateHash();
        string motion = SimulationKinematicHash.Compute(world);

        PrivateField<Pcg32Rng>(world, "_noise").NextUInt32();

        Assert.Equal(motion, SimulationKinematicHash.Compute(world));
        Assert.Equal(0, world.TickCount);
        Assert.NotEqual(before, world.StateHash());
    }

    [Fact]
    public void VicsekCompletedTickIsVisibleEvenWhenMotionReturnsToTheSameBits()
    {
        var settings = new VicsekSettings { Noise = 0f, Speed = 1f, WorldWidth = 1f, WorldHeight = 1f };
        var world = new VicsekWorld(settings, new[] { new Vec2(0f, 0f) }, new[] { 0f });
        ModelStateHash before = world.StateHash();
        string motion = SimulationKinematicHash.Compute(world);

        world.Step();

        Assert.Equal(motion, SimulationKinematicHash.Compute(world));
        Assert.Equal(1, world.TickCount);
        Assert.NotEqual(before, world.StateHash());
    }

    [Fact]
    public void ModelSettingsAreCoveredBeforeTheyAffectMotion()
    {
        var firstAco = new AntColonyWorld(new AntColonySettings { Evaporation = 0f }, 2);
        var secondAco = new AntColonyWorld(new AntColonySettings { Evaporation = 0.5f }, 2);
        Assert.Equal(SimulationKinematicHash.Compute(firstAco), SimulationKinematicHash.Compute(secondAco));
        Assert.NotEqual(firstAco.StateHash(), secondAco.StateHash());

        var firstVicsek = new VicsekWorld(new VicsekSettings { Noise = 0f }, 2);
        var secondVicsek = new VicsekWorld(new VicsekSettings { Noise = 0.5f }, 2);
        Assert.Equal(SimulationKinematicHash.Compute(firstVicsek), SimulationKinematicHash.Compute(secondVicsek));
        Assert.NotEqual(firstVicsek.StateHash(), secondVicsek.StateHash());
    }

    [Fact]
    public void RepeatedObservationsNeverChangeNativeTrajectories()
    {
        var observedAco = new AntColonyWorld(new AntColonySettings { Seed = 7 }, 8);
        var controlAco = new AntColonyWorld(new AntColonySettings { Seed = 7 }, 8);
        var observedVicsek = new VicsekWorld(new VicsekSettings { Seed = 7 }, 8);
        var controlVicsek = new VicsekWorld(new VicsekSettings { Seed = 7 }, 8);
        for (int tick = 0; tick < 32; tick++)
        {
            Assert.Equal(observedAco.StateHash(), observedAco.StateHash());
            Assert.Equal(observedVicsek.StateHash(), observedVicsek.StateHash());
            observedAco.Step();
            controlAco.Step();
            observedVicsek.Step();
            controlVicsek.Step();
        }

        Assert.Equal(controlAco.StateHash(), observedAco.StateHash());
        Assert.Equal(controlVicsek.StateHash(), observedVicsek.StateHash());
        Assert.Equal(SimulationKinematicHash.Compute(controlAco), SimulationKinematicHash.Compute(observedAco));
        Assert.Equal(SimulationKinematicHash.Compute(controlVicsek), SimulationKinematicHash.Compute(observedVicsek));
    }

    private static T PrivateField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
}
