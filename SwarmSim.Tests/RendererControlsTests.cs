using Raylib_cs;
using SwarmSim.Core;
using SwarmSim.Core.Systems;
using SwarmSim.Render;
using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

public sealed class RendererControlsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryPressedKeyRoutesToTheDocumentedActionAndOtherKeysAreIgnored(bool canonical)
    {
        RendererMode mode = canonical ? RendererMode.Canonical : RendererMode.Legacy;
        var expected = new Dictionary<KeyboardKey, RendererControlId>
        {
            [KeyboardKey.One] = RendererControlId.SeparationWeight,
            [KeyboardKey.Two] = RendererControlId.AlignmentWeight,
            [KeyboardKey.Three] = RendererControlId.CohesionWeight,
            [KeyboardKey.Four] = RendererControlId.SeparationRadius,
            [KeyboardKey.Five] = RendererControlId.SenseRadius,
            [KeyboardKey.Six] = RendererControlId.Speed,
            [KeyboardKey.Seven] = RendererControlId.MaxForce,
            [KeyboardKey.Up] = RendererControlId.Increase,
            [KeyboardKey.Equal] = RendererControlId.Increase,
            [KeyboardKey.KpAdd] = RendererControlId.Increase,
            [KeyboardKey.Down] = RendererControlId.Decrease,
            [KeyboardKey.Minus] = RendererControlId.Decrease,
            [KeyboardKey.KpSubtract] = RendererControlId.Decrease,
            [KeyboardKey.F1] = RendererControlId.Preset1,
            [KeyboardKey.F2] = RendererControlId.Preset2,
            [KeyboardKey.F3] = RendererControlId.Preset3,
            [KeyboardKey.F4] = RendererControlId.Preset4,
            [KeyboardKey.F5] = RendererControlId.Preset5,
            [KeyboardKey.P] = RendererControlId.PrintConfiguration,
            [KeyboardKey.R] = RendererControlId.Reset,
            [KeyboardKey.H] = RendererControlId.Help,
            [KeyboardKey.Escape] = RendererControlId.Quit
        };
        if (canonical)
        {
            expected.Add(KeyboardKey.O, RendererControlId.Interactions);
            expected.Add(KeyboardKey.Tab, RendererControlId.NextSubject);
        }
        else
        {
            expected.Add(KeyboardKey.Eight, RendererControlId.Friction);
            expected.Add(KeyboardKey.Space, RendererControlId.SpawnRandom);
            expected.Add(KeyboardKey.V, RendererControlId.VelocityVectors);
            expected.Add(KeyboardKey.S, RendererControlId.SenseCircles);
            expected.Add(KeyboardKey.N, RendererControlId.NeighborLinks);
            expected.Add(KeyboardKey.F12, RendererControlId.DebugOverlay);
            expected.Add(KeyboardKey.C, RendererControlId.ExportCsv);
            expected.Add(KeyboardKey.X, RendererControlId.Shake);
        }

        foreach (KeyboardKey key in Enum.GetValues<KeyboardKey>())
        {
            var input = new FakeInput { Pressed = key };
            RendererInputSnapshot snapshot = RenderProgram.Controls.Capture(mode, input);
            foreach (RendererControlId action in Enum.GetValues<RendererControlId>())
                Assert.Equal(expected.TryGetValue(key, out var wanted) && wanted == action, snapshot.IsActive(action));
        }
        Assert.Equal(KeyboardKey.Escape, RenderProgram.Controls.ExitKey(mode));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShiftIsHeldAndMouseSpawnsAreLegacyOnly(bool canonical)
    {
        RendererMode mode = canonical ? RendererMode.Canonical : RendererMode.Legacy;
        foreach (KeyboardKey shift in new[] { KeyboardKey.LeftShift, KeyboardKey.RightShift })
        {
            Assert.True(RenderProgram.Controls.Capture(mode, new FakeInput { Held = shift })
                .IsActive(RendererControlId.FineAdjustment));
            Assert.False(RenderProgram.Controls.Capture(mode, new FakeInput { Pressed = shift })
                .IsActive(RendererControlId.FineAdjustment));
        }
        Assert.Equal(!canonical, RenderProgram.Controls.Capture(mode, new FakeInput { Mouse = MouseButton.Left })
            .IsActive(RendererControlId.SpawnGroup0));
        Assert.Equal(!canonical, RenderProgram.Controls.Capture(mode, new FakeInput { Mouse = MouseButton.Right })
            .IsActive(RendererControlId.SpawnGroup1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HelpAndCompactLabelsCoverEveryBindingWithoutAdvertisingTheOtherRenderer(bool canonical)
    {
        RendererMode mode = canonical ? RendererMode.Canonical : RendererMode.Legacy;
        var controls = RenderProgram.Controls;
        var bindings = controls.GetBindings(mode);
        Assert.Equal(bindings.Count, bindings.Select(binding => binding.Id).Distinct().Count());
        string help = controls.FormatHelp(mode, SpeedModel.ConstantSpeed);

        foreach (var binding in bindings)
        {
            Assert.Contains(binding.KeyLabel + ": " + binding.Description, help, StringComparison.Ordinal);
            Assert.StartsWith(binding.KeyLabel + ":", controls.ShortLabel(mode, binding.Id), StringComparison.Ordinal);
        }
        Assert.Contains(canonical ? "1-7" : "1-8", controls.ParameterHeading(mode), StringComparison.Ordinal);
        Assert.Contains("KP+", controls.ParameterHint(mode), StringComparison.Ordinal);
        Assert.Contains("KP-", controls.ParameterHint(mode), StringComparison.Ordinal);
        Assert.Contains("Shift", controls.ParameterHint(mode), StringComparison.Ordinal);
        if (canonical)
        {
            Assert.DoesNotContain("friction", help, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("F12", help, StringComparison.Ordinal);
            Assert.Contains("O: Toggle interaction overlay", help, StringComparison.Ordinal);
            Assert.Contains("Tab: Cycle inspected boid", help, StringComparison.Ordinal);
            Assert.Contains("Target speed", help, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("8: Select Friction (Damped only)", help, StringComparison.Ordinal);
            Assert.Contains("inactive with ConstantSpeed", help, StringComparison.Ordinal);
            Assert.Contains("S: Toggle sense radius circles (below 100 agents)", help, StringComparison.Ordinal);
            Assert.DoesNotContain("Tab:", help, StringComparison.Ordinal);
            Assert.DoesNotContain("O:", help, StringComparison.Ordinal);
        }
        for (int i = 0; i < RenderProgram.PresetIds.Count; i++)
            Assert.Contains(controls.GetPreset(i).Description, help, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsoleHelpContainsTheSameEngineScopedHelpAsBothOverlays()
    {
        string help = CommandLineOptions.GetHelpText();

        Assert.Contains(RenderProgram.Controls.FormatHelp(RendererMode.Legacy, SpeedModel.ConstantSpeed), help, StringComparison.Ordinal);
        Assert.Contains(RenderProgram.Controls.FormatHelp(RendererMode.Canonical, SpeedModel.ConstantSpeed), help, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalFrictionKeyCannotSelectOrModifyFrictionEvenAfterALegacySelection()
    {
        var values = new[] { 5f, 2.5f, 0.5f, 30f, 100f, 10f, 2.5f, 0.95f };
        int selected = 7;
        RendererInputSnapshot input = RenderProgram.Controls.Capture(RendererMode.Canonical,
            new FakeInput { Pressed = KeyboardKey.Eight });

        Assert.False(RenderProgram.Controls.ApplyParameters(input, ref selected, i => values[i], (i, value) => values[i] = value, TextWriter.Null));
        Assert.Equal(0, selected);
        Assert.Equal(0.95f, values[7]);
        input = RenderProgram.Controls.Capture(RendererMode.Canonical, new FakeInput { Pressed = KeyboardKey.Up });
        Assert.True(RenderProgram.Controls.ApplyParameters(input, ref selected, i => values[i], (i, value) => values[i] = value, TextWriter.Null));
        Assert.Equal(15f, values[0]);
        Assert.Equal(0.95f, values[7]);
    }

    [Fact]
    public void LegacyFrictionAdjustmentAndFineAliasesKeepTheirExistingValuesAndLimits()
    {
        var values = new[] { 5f, 2.5f, 0.5f, 30f, 100f, 10f, 2.5f, 0.95f };
        int selected = 0;
        RendererInputSnapshot input = RenderProgram.Controls.Capture(RendererMode.Legacy,
            new FakeInput { Pressed = KeyboardKey.Eight });
        Assert.False(RenderProgram.Controls.ApplyParameters(input, ref selected, i => values[i], (i, value) => values[i] = value, TextWriter.Null));
        Assert.Equal(7, selected);

        input = RenderProgram.Controls.Capture(RendererMode.Legacy, new FakeInput { Pressed = KeyboardKey.KpSubtract });
        Assert.True(RenderProgram.Controls.ApplyParameters(input, ref selected, i => values[i], (i, value) => values[i] = value, TextWriter.Null));
        Assert.Equal(0.95f - 0.01f, values[7]);
        input = RenderProgram.Controls.Capture(RendererMode.Legacy,
            new FakeInput { Pressed = KeyboardKey.Equal, Held = KeyboardKey.RightShift });
        RenderProgram.Controls.ApplyParameters(input, ref selected, i => values[i], (i, value) => values[i] = value, TextWriter.Null);
        Assert.Equal(0.95f - 0.01f + 0.001f, values[7]);
        values[7] = 1f;
        RenderProgram.Controls.ApplyParameters(input, ref selected, i => values[i], (i, value) => values[i] = value, TextWriter.Null);
        Assert.Equal(1f, values[7]);
    }

    [Fact]
    public void FrictionLabelsMatchActualLegacySpeedModelAndRegisteredPresetSemantics()
    {
        foreach (string preset in RenderProgram.PresetIds)
            Assert.Equal(SpeedModel.ConstantSpeed, RenderProgram.ResolveStartupConfiguration(preset, null).SpeedModel);
        Assert.Contains("inactive with ConstantSpeed", RenderProgram.Controls.FrictionStatus(SpeedModel.ConstantSpeed), StringComparison.Ordinal);
        Assert.DoesNotContain("inactive", RenderProgram.Controls.FrictionStatus(SpeedModel.Damped), StringComparison.OrdinalIgnoreCase);

        float constantLow = IntegrateWithFriction(SpeedModel.ConstantSpeed, 0.8f);
        float constantHigh = IntegrateWithFriction(SpeedModel.ConstantSpeed, 1f);
        float dampedLow = IntegrateWithFriction(SpeedModel.Damped, 0.8f);
        float dampedHigh = IntegrateWithFriction(SpeedModel.Damped, 1f);
        Assert.Equal(constantHigh, constantLow);
        Assert.Equal(4f, dampedLow);
        Assert.Equal(5f, dampedHigh);
    }

    [Fact]
    public void PrintedParametersUseTheEffectiveEngineInventoryAndFrictionNotice()
    {
        using var legacy = new StringWriter();
        using var canonical = new StringWriter();
        RenderProgram.Controls.PrintParameters(RendererMode.Legacy, _ => 1f, legacy, SpeedModel.ConstantSpeed);
        RenderProgram.Controls.PrintParameters(RendererMode.Canonical, _ => 1f, canonical, SpeedModel.ConstantSpeed);

        Assert.Contains("Friction (Damped only):", legacy.ToString(), StringComparison.Ordinal);
        Assert.Contains("inactive with ConstantSpeed", legacy.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("friction", canonical.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Target speed:", canonical.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SpaceSpawnReportsSuccessfulAddsAtTheCapacityCeiling()
    {
        var world = new World(new SimConfig { InitialCapacity = 3, MaxCapacity = 3 });
        world.AddAgent(100f, 100f);

        Assert.Equal(2, RenderProgram.SpawnRandomAgents(world));
        Assert.Equal(3, world.Count);
        Assert.Equal(0, RenderProgram.SpawnRandomAgents(world));
        Assert.Equal(3, world.Count);
    }

    private static float IntegrateWithFriction(SpeedModel speedModel, float friction)
    {
        var world = new World(new SimConfig { InitialCapacity = 1, SpeedModel = speedModel, Friction = friction, MaxSpeed = 10f });
        world.AddAgent(100f, 100f);
        world.Vx[0] = 5f;
        world.Vy[0] = 0f;
        new IntegrateSystem().Run(world, 1f / 60f);
        return world.Vx[0];
    }

    private sealed class FakeInput : IRendererInput
    {
        public KeyboardKey? Pressed { get; init; }
        public KeyboardKey? Held { get; init; }
        public MouseButton? Mouse { get; init; }
        public bool IsPressed(KeyboardKey key) => Pressed == key;
        public bool IsDown(KeyboardKey key) => Held == key;
        public bool IsPressed(MouseButton button) => Mouse == button;
    }
}
