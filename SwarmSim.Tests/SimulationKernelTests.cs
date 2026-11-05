using SwarmSim.Core.Simulation;
using SwarmSim.Core.Aco;
using SwarmSim.Core.Diagnostics;

namespace SwarmSim.Tests;

public sealed class SimulationKernelTests
{
    [Fact]
    public void EveryPhaseRunsOnceInDeclaredOrderAndReadsEarlierWrites()
    {
        var events = new List<string>();
        int value = 2;
        var schedule = new SimulationSchedule(
            new SimulationPhase("multiply", () => { value *= 3; events.Add("multiply"); }),
            new SimulationPhase("add", () => { value += 5; events.Add("add"); }));

        schedule.Run();
        schedule.Run();

        Assert.Equal(38, value);
        Assert.Equal(new[] { "multiply", "add", "multiply", "add" }, events);
        Assert.Equal(new[] { "multiply", "add" }, schedule.PhaseNames);
    }

    [Fact]
    public void RegistrationCannotBeChangedThroughTheSuppliedArray()
    {
        int value = 0;
        SimulationPhase[] phases = [new SimulationPhase("original", () => value++)];
        var schedule = new SimulationSchedule(phases);
        phases[0] = new SimulationPhase("replacement", () => value += 100);

        schedule.Run();

        Assert.Equal(1, value);
        Assert.Equal("original", schedule.PhaseNames[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)schedule.PhaseNames)[0] = "changed");
    }

    [Fact]
    public void InvalidAndDuplicatePhasesAreRejectedBeforeExecution()
    {
        Assert.Throws<ArgumentException>(() => new SimulationPhase(" ", () => { }));
        Assert.Throws<ArgumentNullException>(() => new SimulationPhase("missing", null!));
        Assert.Throws<ArgumentNullException>(() => new SimulationSchedule(null!));
        Assert.Throws<ArgumentException>(() => new SimulationSchedule());
        Assert.Throws<ArgumentException>(() => new SimulationSchedule([null!]));
        Assert.Throws<ArgumentException>(() => new SimulationSchedule(new SimulationPhase("same", () => { }), new SimulationPhase("same", () => { })));
    }

    [Fact]
    public void FailureStopsLaterPhasesAndDoesNotPretendToRollBackEarlierState()
    {
        int value = 0;
        bool fail = true;
        var failure = new InvalidOperationException("deliberate failure");
        var schedule = new SimulationSchedule(
            new SimulationPhase("write", () => value++),
            new SimulationPhase("may-fail", () => { if (fail) throw failure; }),
            new SimulationPhase("finish", () => value += 10));

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(schedule.Run));
        Assert.Equal(1, value);
        // Recovery is the model's responsibility. The runner has released its reentrancy guard.
        fail = false;
        schedule.Run();
        Assert.Equal(12, value);
    }

    [Fact]
    public void RecursiveRunsFailBeforeAnyNestedPhaseExecutes()
    {
        int calls = 0;
        SimulationSchedule? schedule = null;
        schedule = new SimulationSchedule(new SimulationPhase("recursive", () =>
        {
            calls++;
            schedule!.Run();
        }));

        Assert.Throws<InvalidOperationException>(schedule.Run);
        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(schedule.Run);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void NextGenerationStaysInvisibleUntilCommitAndTheOldGenerationIsReused()
    {
        Particle[] initial = [new(2f, 7), new(5f, 11)];
        var state = new DoubleBufferedState<Particle>(initial);
        initial[0] = new(999f, 999);
        Assert.Equal(new Particle(2f, 7), state.Current[0]);
        Assert.Equal(2, state.Count);

        state.Next[0] = new(state.Current[0].Phase + state.Current[1].Phase, 7);
        state.Next[1] = new(state.Current[1].Phase - state.Current[0].Phase, 11);
        Assert.Equal(new Particle(2f, 7), state.Current[0]);
        Assert.Equal(new Particle(5f, 11), state.Current[1]);
        state.Commit();

        Assert.Equal(new Particle(7f, 7), state.Current[0]);
        Assert.Equal(new Particle(3f, 11), state.Current[1]);
        Assert.Equal(new Particle(2f, 7), state.Next[0]);
        Assert.Equal(new Particle(5f, 11), state.Next[1]);
        state.Next[0] = new(13f, 7);
        state.Next[1] = new(17f, 11);
        state.Commit();
        Assert.Equal(new Particle(13f, 7), state.Current[0]);
    }

    [Fact]
    public void EmptyStateCanParticipateInAFieldOnlySchedule()
    {
        var state = new DoubleBufferedState<Particle>(ReadOnlySpan<Particle>.Empty);
        var schedule = new SimulationSchedule(new SimulationPhase("commit", state.Commit));
        schedule.Run();
        Assert.Equal(0, state.Count);
        Assert.True(state.Current.IsEmpty);
        Assert.True(state.Next.IsEmpty);
    }

    [Fact]
    public void WarmScheduleAndStateCommitsAllocateNoManagedMemory()
    {
        var state = new DoubleBufferedState<Particle>(new[] { new Particle(1f, 3) });
        var schedule = new SimulationSchedule(
            new SimulationPhase("compute", () => state.Next[0] = state.Current[0] with { Phase = state.Current[0].Phase + 1f }),
            new SimulationPhase("commit", state.Commit));
        for (int i = 0; i < 128; i++) schedule.Run();
        Action measure = () => { for (int i = 0; i < 1000; i++) schedule.Run(); };
        Assert.Equal(0L, AntColonyModelTests.LeastAllocated(measure));
        Assert.True(state.Current[0].Phase >= 1129f);
    }

    private readonly record struct Particle(float Phase, int Species);

    [Fact]
    public void ScheduledColonyMatchesExplicitAgentAndFieldPhasesIncludingFieldState()
    {
        var settings = new AntColonySettings { Seed = 31u };
        var scheduled = new AntColonyWorld(settings, 64);
        var explicitPhases = new AntColonyWorld(settings, 64);
        for (int tick = 0; tick < 512; tick++)
        {
            scheduled.Step();
            explicitPhases.AgentPhase();
            explicitPhases.FieldPhase();
        }
        Assert.Equal(explicitPhases.Ants.ToArray(), scheduled.Ants.ToArray());
        Assert.Equal(explicitPhases.Field.Cells.ToArray(), scheduled.Field.Cells.ToArray());
        Assert.Equal(explicitPhases.FoodRemaining, scheduled.FoodRemaining);
        Assert.Equal(explicitPhases.FoodReturned, scheduled.FoodReturned);
        Assert.Equal(512L, scheduled.TickCount);
        Assert.Equal(SimulationKinematicHash.Compute(explicitPhases), SimulationKinematicHash.Compute(scheduled));
    }
}
