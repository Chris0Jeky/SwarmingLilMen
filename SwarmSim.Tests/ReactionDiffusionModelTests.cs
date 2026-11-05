using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Metrics;
using SwarmSim.Core.ReactionDiffusion;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

public sealed class ReactionDiffusionModelTests
{
    private static ReactionDiffusionSettings DiffusionOnly(int columns, int rows,
        float du = 0.125f, float dv = 0.0625f, float dt = 0.5f) => new()
    {
        Columns = columns, Rows = rows, DiffusionU = du, DiffusionV = dv,
        ReactionRate = 0f, Feed = 0f, Kill = 0f, FixedDeltaTime = dt, PatchFraction = 0f
    };

    private static void Close(double expected, double actual, double tolerance = 2e-7d) =>
        Assert.InRange(Math.Abs(expected - actual), 0d, tolerance);

    [Fact]
    public void TheUniformUOneVZeroEquilibriumIsExactlyPreserved()
    {
        var world = new ReactionDiffusionWorld(new ReactionDiffusionSettings
            { Columns = 7, Rows = 5, PatchFraction = 0f });
        CellState[] initial = world.Cells.ToArray();
        ModelStateHash before = world.StateHash();
        for (int tick = 0; tick < 128; tick++) world.Step();

        Assert.Equal(initial, world.Cells.ToArray());
        Assert.All(world.Cells.ToArray(), cell => Assert.Equal(new CellState(1f, 0f), cell));
        Assert.Equal(128L, world.TickCount);
        Assert.NotEqual(before, world.StateHash()); // The clock is continuation state even at equilibrium.
        Assert.Equal(new[] { "stability", "reaction-diffusion", "commit" }, world.PhaseNames);
    }

    [Fact]
    public void ACornerImpulseDiffusesAcrossBothPeriodicSeamsWithoutDiagonalOrInPlacePropagation()
    {
        var initial = new CellState[20];
        initial[0] = new CellState(1f, 2f);
        var world = new ReactionDiffusionWorld(DiffusionOnly(5, 4, du: 0.25f, dv: 0.125f, dt: 1f), initial);

        world.Step();

        int[] neighbors = [1, 4, 5, 15];
        for (int i = 0; i < initial.Length; i++)
        {
            Assert.Equal(neighbors.Contains(i) ? 0.25f : 0f, world.Cells[i].U);
            Assert.Equal(i == 0 ? 1f : neighbors.Contains(i) ? 0.25f : 0f, world.Cells[i].V);
        }
        // Cell (2,0) must not see this tick's newly populated (1,0).
        Assert.Equal(new CellState(0f, 0f), world.Cells[2]);
    }

    [Fact]
    public void ThinPeriodicGridsCountEveryDirectedStencilLink()
    {
        var oneColumn = new ReactionDiffusionWorld(DiffusionOnly(1, 3, du: 0.25f, dt: 1f),
            new[] { new CellState(1f, 0f), new CellState(), new CellState() });
        oneColumn.Step();
        Assert.Equal(new[] { 0.5f, 0.25f, 0.25f }, oneColumn.Cells.ToArray().Select(cell => cell.U));

        var twoColumns = new ReactionDiffusionWorld(DiffusionOnly(2, 3, du: 0.25f, dt: 1f),
            new[] { new CellState(1f, 0f), new CellState(), new CellState(), new CellState(), new CellState(), new CellState() });
        twoColumns.Step();
        // Left and right both refer to the other column and contribute separately.
        Assert.Equal(new[] { 0f, 0.5f, 0.25f, 0f, 0.25f, 0f }, twoColumns.Cells.ToArray().Select(cell => cell.U));

        var singleton = new ReactionDiffusionWorld(DiffusionOnly(1, 1, du: 0.25f, dv: 0.25f, dt: 1f),
            new[] { new CellState(0.75f, 0.25f) });
        singleton.Step();
        Assert.Equal(new CellState(0.75f, 0.25f), singleton.Cells[0]);
    }

    [Fact]
    public void ARectangularGridMatchesAnIndependentLaplacianAndReactionEulerOracle()
    {
        var settings = new ReactionDiffusionSettings
        {
            Columns = 3, Rows = 4, DiffusionU = 0.125f, DiffusionV = 0.0625f,
            ReactionRate = 0.75f, Feed = 0.04f, Kill = 0.07f, FixedDeltaTime = 0.125f
        };
        CellState[] initial = Enumerable.Range(0, 12).Select(i =>
            new CellState(0.2f + ((i * 5) % 7) * 0.1f, 0.03f + ((i * 3) % 5) * 0.05f)).ToArray();
        var world = new ReactionDiffusionWorld(settings, initial);
        for (int tick = 0; tick < 8; tick++)
        {
            CellState[] previous = world.Cells.ToArray();
            world.Step();
            for (int y = 0; y < settings.Rows; y++)
                for (int x = 0; x < settings.Columns; x++)
                {
                    CellState At(int px, int py) => previous[
                        ((py + settings.Rows) % settings.Rows) * settings.Columns
                        + (px + settings.Columns) % settings.Columns];
                    CellState self = At(x, y);
                    double lapU = (double)At(x - 1, y).U + At(x + 1, y).U + At(x, y - 1).U + At(x, y + 1).U - 4d * self.U;
                    double lapV = (double)At(x - 1, y).V + At(x + 1, y).V + At(x, y - 1).V + At(x, y + 1).V - 4d * self.V;
                    double reaction = (double)settings.ReactionRate * self.U * self.V * self.V;
                    double u = self.U + settings.FixedDeltaTime *
                        (settings.DiffusionU * lapU - reaction + settings.Feed * (1d - self.U));
                    double v = self.V + settings.FixedDeltaTime *
                        (settings.DiffusionV * lapV + reaction - ((double)settings.Feed + settings.Kill) * self.V);
                    Close(u, world.Cells[y * settings.Columns + x].U);
                    Close(v, world.Cells[y * settings.Columns + x].V);
                }
        }
    }

    [Fact]
    public void PureDiffusionConservesBothFieldTotalsOnTheTorusWithinStorageRounding()
    {
        CellState[] initial = Enumerable.Range(0, 35).Select(i =>
            new CellState((i % 7) / 8f, (i % 5) / 16f)).ToArray();
        var world = new ReactionDiffusionWorld(DiffusionOnly(7, 5), initial);
        double sumU = initial.Sum(cell => (double)cell.U), sumV = initial.Sum(cell => (double)cell.V);

        for (int tick = 0; tick < 128; tick++) world.Step();

        Close(sumU, world.Cells.ToArray().Sum(cell => (double)cell.U), 2e-5d);
        Close(sumV, world.Cells.ToArray().Sum(cell => (double)cell.V), 2e-5d);
        Assert.All(world.Cells.ToArray(), cell =>
        {
            Assert.InRange(cell.U, 0f, 0.75f);
            Assert.InRange(cell.V, 0f, 0.25f);
        });
    }

    [Fact]
    public void TheLocalReactionTransfersUToVWithoutCreatingTotalConcentration()
    {
        var settings = new ReactionDiffusionSettings
        {
            Columns = 1, Rows = 1, DiffusionU = 0f, DiffusionV = 0f,
            ReactionRate = 1f, Feed = 0f, Kill = 0f, FixedDeltaTime = 0.5f
        };
        var world = new ReactionDiffusionWorld(settings, new[] { new CellState(0.8f, 0.25f) });
        world.Step();
        Close(0.775d, world.Cells[0].U);
        Close(0.275d, world.Cells[0].V);
        Close((double)0.8f + 0.25f, (double)world.Cells[0].U + world.Cells[0].V);
    }

    [Fact]
    public void FeedAddsUAndBothFeedAndKillRemoveV()
    {
        var settings = new ReactionDiffusionSettings
        {
            Columns = 1, Rows = 1, DiffusionU = 0f, DiffusionV = 0f,
            ReactionRate = 0f, Feed = 0.25f, Kill = 0.125f, FixedDeltaTime = 0.5f
        };
        var world = new ReactionDiffusionWorld(settings, new[] { new CellState(0.4f, 0.8f) });
        world.Step();
        Close(0.475d, world.Cells[0].U);
        Close(0.65d, world.Cells[0].V);
    }

    [Fact]
    public void ConcentrationsAboveOneAreRetainedWithoutClamping()
    {
        var world = new ReactionDiffusionWorld(DiffusionOnly(1, 1, du: 0f, dv: 0f, dt: 1f),
            new[] { new CellState(2f, 3f) });
        world.Step();
        Assert.Equal(new CellState(2f, 3f), world.Cells[0]);
    }

    [Fact]
    public void AStationaryMaximumConcentrationUsesOrdinaryFloatRounding()
    {
        // Regrouped double arithmetic can land one double ULP above float.MaxValue;
        // conversion still rounds to the same finite binary32 value, with zero Laplacian.
        var world = new ReactionDiffusionWorld(DiffusionOnly(1, 1, du: 0.1f, dv: 0f, dt: 0.01f),
            new[] { new CellState(float.MaxValue, 0f) });

        world.Step();

        Assert.Equal(new CellState(float.MaxValue, 0f), world.Cells[0]);
        Assert.Equal(1L, world.TickCount);
    }

    [Fact]
    public void TrueStoredConcentrationOverflowDoesNotPublishAPartialField()
    {
        var settings = new ReactionDiffusionSettings
        {
            Columns = 2, Rows = 1, DiffusionU = 0f, DiffusionV = 0f,
            ReactionRate = float.Epsilon, Feed = 0f, Kill = 0f,
            FixedDeltaTime = 1e-33f, PatchFraction = 0f
        };
        // The first candidate is written to scratch. The second has safe depletion (~0.1623),
        // but its V gain exceeds binary32 range and must invalidate the complete next field.
        CellState[] initial = [new(0.75f, 0.25f), new(float.MaxValue, float.MaxValue)];
        var world = new ReactionDiffusionWorld(settings, initial);
        ModelStateHash before = world.StateHash();

        Assert.Throws<InvalidOperationException>(world.Step);

        Assert.Equal(initial, world.Cells.ToArray());
        Assert.Equal(0L, world.TickCount);
        Assert.Equal(before, world.StateHash());
    }

    [Fact]
    public void AReactionDepletionViolationStopsBeforeCommitAndPreservesTheLastCompleteField()
    {
        var settings = new ReactionDiffusionSettings
        {
            Columns = 1, Rows = 1, DiffusionU = 0f, DiffusionV = 0f,
            ReactionRate = 1f, Feed = 0f, Kill = 0f, FixedDeltaTime = 1f
        };
        var world = new ReactionDiffusionWorld(settings, new[] { new CellState(1f, 0.5f) });
        world.Step();
        Assert.Equal(new CellState(0.75f, 0.75f), world.Cells[0]);
        world.Step();
        Assert.Equal(new CellState(0.328125f, 1.171875f), world.Cells[0]);
        ModelStateHash before = world.StateHash();
        CellState[] cells = world.Cells.ToArray();

        var error = Assert.Throws<InvalidOperationException>(world.Step);

        Assert.Contains("No new field was committed", error.Message, StringComparison.Ordinal);
        Assert.Equal(2L, world.TickCount);
        Assert.Equal(cells, world.Cells.ToArray());
        Assert.Equal(before, world.StateHash());
        Assert.Throws<InvalidOperationException>(world.Step); // The schedule released its recursion guard.
        Assert.Equal(before, world.StateHash());
    }

    [Fact]
    public void InitialReactionDepletionIsValidatedBeforeAWorldIsPublished()
    {
        var settings = new ReactionDiffusionSettings
        {
            Columns = 1, Rows = 1, DiffusionU = 0f, DiffusionV = 0f,
            Feed = 0f, Kill = 0f, FixedDeltaTime = 1f
        };
        Assert.Empty(settings.Validate()); // Linear terms alone are safe; V makes this state unsafe.
        var error = Assert.Throws<ArgumentException>(() =>
            new ReactionDiffusionWorld(settings, new[] { new CellState(1f, 2f) }));
        Assert.Contains("Initial V", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SeededPatchUsesTheNamedStreamAndLeavesTheBackgroundAtEquilibrium()
    {
        var settings = new ReactionDiffusionSettings
            { Columns = 10, Rows = 6, PatchFraction = 0.5f, PatchNoise = 0.02f, Seed = 17u };
        var world = new ReactionDiffusionWorld(settings);
        Pcg32Rng expected = new RandomStreams(settings.Seed).CreateStream(ReactionDiffusionWorld.PatchStream);
        int patched = 0;
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 10; x++)
            {
                CellState actual = world.Cells[y * 10 + x];
                if (x >= 2 && x < 7 && y >= 1 && y < 4)
                {
                    float u = (float)(0.5d + (expected.NextFloat() * 2d - 1d) * settings.PatchNoise);
                    float v = (float)(0.25d + (expected.NextFloat() * 2d - 1d) * settings.PatchNoise);
                    Assert.Equal(new CellState(u, v), actual);
                    patched++;
                }
                else Assert.Equal(new CellState(1f, 0f), actual);
            }
        Assert.Equal(15, patched);
    }

    [Fact]
    public void ExplicitInitialFieldsAreCopiedAndTheModelHasNoKinematicAgents()
    {
        CellState[] initial = [new(0.5f, 0.25f), new(1f, 0f)];
        var world = new ReactionDiffusionWorld(DiffusionOnly(2, 1), initial);
        initial[0] = new CellState(99f, 99f);
        Assert.Equal(new CellState(0.5f, 0.25f), world.Cells[0]);
        Assert.Equal(2, world.Columns);
        Assert.Equal(1, world.Rows);
        Assert.Equal(2, world.CellCount);
        Assert.Equal(0, world.AgentCount);
        Assert.Equal(0, world.CopyKinematics(Span<KinematicSample>.Empty));
        KinematicSample[] untouched = [new(1f, 2f, 3f, 4f)];
        Assert.Equal(0, world.CopyKinematics(untouched));
        Assert.Equal(new KinematicSample(1f, 2f, 3f, 4f), untouched[0]);
    }

    [Fact]
    public void ConfigurationRoundTripsAndRepeatedSeededFieldsAreBitIdentical()
    {
        var settings = new ReactionDiffusionSettings { Columns = 12, Rows = 9, Seed = 19u };
        ReactionDiffusionSettings restored = ReactionDiffusionSettings.FromJson(settings.ToJson());
        Assert.Equal(settings.ToJson(), restored.ToJson());
        var first = new ReactionDiffusionWorld(settings);
        var second = new ReactionDiffusionWorld(restored);
        var other = new ReactionDiffusionWorld(new ReactionDiffusionSettings { Columns = 12, Rows = 9, Seed = 20u });
        for (int tick = 0; tick < 128; tick++)
        {
            first.Step();
            second.Step();
            other.Step();
        }
        Assert.Equal(first.Cells.ToArray(), second.Cells.ToArray());
        Assert.Equal(first.StateHash(), second.StateHash());
        Assert.NotEqual(first.Cells.ToArray(), other.Cells.ToArray());
    }

    [Fact]
    public void ParameterControlsShareTheSameSeededInitialField()
    {
        var baseline = new ReactionDiffusionWorld(new ReactionDiffusionSettings { Columns = 8, Rows = 8, Seed = 7u });
        var changed = new ReactionDiffusionWorld(new ReactionDiffusionSettings
            { Columns = 8, Rows = 8, Seed = 7u, Feed = 0.04f, Kill = 0.06f, ReactionRate = 0.5f });
        Assert.Equal(baseline.Cells.ToArray(), changed.Cells.ToArray());
        Assert.NotEqual(baseline.StateHash(), changed.StateHash());
    }

    [Fact]
    public void ModelStateProfileMatchesAnIndependentBinaryFixtureAtTwoAnalyticalStates()
    {
        // Independently encoded with Python struct.pack('<ii8fI') and hashlib, then
        // '<qi' for clock/count and '<ff' per cell. Both states use exact binary fractions.
        var settings = new ReactionDiffusionSettings
        {
            Columns = 2, Rows = 1, DiffusionU = 0.125f, DiffusionV = 0.0625f,
            ReactionRate = 0.5f, Feed = 0.03125f, Kill = 0.0625f,
            FixedDeltaTime = 0.25f, PatchFraction = 0f, PatchNoise = 0f, Seed = 17u
        };
        var world = new ReactionDiffusionWorld(settings, new[] { new CellState(1f, 0.25f), new CellState(0.5f, 0.125f) });
        Assert.Equal(new ModelStateHash(ReactionDiffusionWorld.ModelStateProfile,
            "A4A270232A94C83A45CE9464F9D5C0765532F421D24183FD1D471E07044DE161"), world.StateHash());

        world.Step();

        Assert.Equal(new[] { new CellState(0.9609375f, 0.248046875f), new CellState(0.5341796875f, 0.126953125f) }, world.Cells.ToArray());
        Assert.Equal(new ModelStateHash(ReactionDiffusionWorld.ModelStateProfile,
            "0F7CD155FAEA9CD944AE3FF7FFF9BC79A9B12D2627A7A1AA48C3BD0C3E068EDE"), world.StateHash());
    }

    [Fact]
    public void ModelHashSeesOrderedFieldBitsEvenWhenAggregateMetricsMatch()
    {
        var settings = DiffusionOnly(2, 1);
        var first = new ReactionDiffusionWorld(settings, new[] { new CellState(1f, 0.25f), new CellState(0.5f, 0.125f) });
        var reversed = new ReactionDiffusionWorld(settings, new[] { new CellState(0.5f, 0.125f), new CellState(1f, 0.25f) });
        var firstValues = new double[6];
        var reversedValues = new double[6];
        new ReactionDiffusionMetricProbe(first).Sample(new MetricFrame([], MetricDomain.Euclidean), firstValues);
        new ReactionDiffusionMetricProbe(reversed).Sample(new MetricFrame([], MetricDomain.Euclidean), reversedValues);
        Assert.Equal(firstValues, reversedValues);
        Assert.NotEqual(first.StateHash(), reversed.StateHash());
    }

    [Fact]
    public void FieldMetricsReadBothConcentrationsAndDoNotAdvanceTheWorld()
    {
        var world = new ReactionDiffusionWorld(DiffusionOnly(2, 2), new[]
        {
            new CellState(0.25f, 0.5f), new CellState(0.75f, 0.25f),
            new CellState(1f, 0f), new CellState(0f, 1f)
        });
        ModelStateHash before = world.StateHash();
        var probe = new ReactionDiffusionMetricProbe(world);
        var values = new double[6];
        probe.Sample(new MetricFrame([], MetricDomain.Euclidean), values);
        Assert.Equal(new[] { 0.5d, 0d, 1d, 0.4375d, 0d, 1d }, values);
        Assert.Equal(before, world.StateHash());
        Assert.True(probe.IsDeterministic);
        Assert.Throws<ArgumentException>(() => probe.Sample(new MetricFrame([], MetricDomain.Euclidean), new double[5]));

        var metadata = new ReactionDiffusionMetricProbe(null);
        Assert.Equal(new[] { "reactionDiffusion.uMean", "reactionDiffusion.uMin", "reactionDiffusion.uMax",
            "reactionDiffusion.vMean", "reactionDiffusion.vMin", "reactionDiffusion.vMax" }, metadata.Channels);
        Assert.Throws<InvalidOperationException>(() => metadata.Sample(new MetricFrame([], MetricDomain.Euclidean), values));
        var registry = new MetricRegistry();
        registry.Register(probe);
        MetricPlan plan = registry.CreatePlan("reactionDiffusion.vMax", "reactionDiffusion.uMean");
        var selected = new double[2];
        plan.Sample(new MetricFrame([], MetricDomain.Euclidean), selected);
        Assert.Equal(new[] { 1d, 0.5d }, selected);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void DefaultSeededGridsRemainFiniteAndNonnegativeForABoundedRun(int side)
    {
        var world = new ReactionDiffusionWorld(new ReactionDiffusionSettings { Columns = side, Rows = side, Seed = 7u });
        for (int tick = 0; tick < 256; tick++) world.Step();
        Assert.Equal(256L, world.TickCount);
        foreach (CellState cell in world.Cells)
        {
            Assert.True(float.IsFinite(cell.U) && float.IsFinite(cell.V));
            Assert.True(cell.U >= 0f && cell.V >= 0f);
        }
    }

    [Fact]
    public void WarmStepsAndFieldMetricSamplingAllocateNoManagedMemory()
    {
        var world = new ReactionDiffusionWorld(new ReactionDiffusionSettings { Columns = 16, Rows = 16 });
        var probe = new ReactionDiffusionMetricProbe(world);
        var output = new double[6];
        for (int tick = 0; tick < 128; tick++)
        {
            world.Step();
            probe.Sample(new MetricFrame([], MetricDomain.Euclidean), output);
        }
        Assert.Equal(0L, AntColonyModelTests.LeastAllocated(() =>
        {
            for (int tick = 0; tick < 128; tick++)
            {
                world.Step();
                probe.Sample(new MetricFrame([], MetricDomain.Euclidean), output);
            }
        }));
    }

    [Fact]
    public void InvalidSettingsRejectUnsafeShapesRatesAndLinearTimesteps()
    {
        ReactionDiffusionSettings[] invalid =
        [
            new() { Columns = 0 }, new() { Rows = -1 },
            new() { Columns = int.MaxValue, Rows = int.MaxValue },
            new() { Columns = ReactionDiffusionSettings.MaxCells + 1, Rows = 1 },
            new() { DiffusionU = -1f }, new() { DiffusionV = float.NaN },
            new() { ReactionRate = float.PositiveInfinity }, new() { ReactionRate = -1f },
            new() { Feed = -1f }, new() { Kill = -1f },
            new() { FixedDeltaTime = 0f }, new() { FixedDeltaTime = float.PositiveInfinity },
            new() { DiffusionU = 0.5f, FixedDeltaTime = 1f },
            new() { DiffusionV = 0.5f, FixedDeltaTime = 1f },
            new() { Feed = 3f, FixedDeltaTime = 1f }, new() { Kill = 3f, FixedDeltaTime = 1f },
            new() { PatchFraction = -0.1f }, new() { PatchFraction = 1.1f },
            new() { PatchNoise = 0.3f }, new() { PatchNoise = float.NaN }, new() { Seed = uint.MaxValue }
        ];
        foreach (ReactionDiffusionSettings settings in invalid)
        {
            Assert.NotEmpty(settings.Validate());
            Assert.Throws<ArgumentException>(() => new ReactionDiffusionWorld(settings));
            Assert.Throws<ArgumentException>(() => settings.ToJson());
        }
        Assert.Throws<ArgumentNullException>(() => new ReactionDiffusionWorld(null!));
    }

    [Fact]
    public void InitialFieldLengthAndConcentrationsMustBeValid()
    {
        var settings = DiffusionOnly(1, 1);
        Assert.Throws<ArgumentException>(() => new ReactionDiffusionWorld(settings, Array.Empty<CellState>()));
        Assert.Throws<ArgumentException>(() => new ReactionDiffusionWorld(settings, new CellState[2]));
        foreach (CellState invalid in new[]
            { new CellState(-1f, 0f), new CellState(0f, -1f), new CellState(float.NaN, 0f), new CellState(0f, float.PositiveInfinity) })
            Assert.Throws<ArgumentException>(() => new ReactionDiffusionWorld(settings, new[] { invalid }));
    }
}
