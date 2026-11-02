using System.Reflection;
using Raylib_cs;
using SwarmSim.Core;
using SwarmSim.Render;
using RenderProgram = SwarmSim.Render.Program;

namespace SwarmSim.Tests;

[CollectionDefinition("Renderer mutable input state", DisableParallelization = true)]
public sealed class RendererMutableInputCollection;

[Collection("Renderer mutable input state")]
public sealed class RendererCombinedInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecreationAndSpawnInOneFrameMutateTheActiveWorld(bool preset)
    {
        using var saved = new RendererStateScope();
        var config = new SimConfig { InitialCapacity = 256, MaxCapacity = 256 };
        Invoke("ApplyBaseConfig", config);
        var original = new World(config);
        original.AddAgent(100f, 100f);
        original.AddAgent(200f, 100f);
        Set("_world", original);
        Set("_runner", new SimulationRunner(original));
        Set("_selectedParameter", 0);

        var input = RenderProgram.Controls.Capture(RendererMode.Legacy,
            new PressedKeys(preset ? KeyboardKey.F1 : KeyboardKey.Up, KeyboardKey.Space));
        Invoke("HandleInput", original, input);

        var active = Assert.IsType<World>(Get("_world"));
        Assert.NotSame(original, active);
        Assert.Equal(102, active.Count);
        Assert.Equal(2, original.Count);
        Assert.Equal(active.Count, Assert.IsType<SimSnapshot>(Get("_currSnapshot")).AgentCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(KeyboardKey.Up)]
    [InlineData(KeyboardKey.F1)]
    public void ShakePublishesChangedVelocitiesAndInvalidatesInterpolation(KeyboardKey? recreationKey)
    {
        using var saved = new RendererStateScope();
        var config = new SimConfig { InitialCapacity = 4, MaxCapacity = 4 };
        Invoke("ApplyBaseConfig", config);
        var original = new World(config);
        original.AddAgent(100f, 100f);
        original.AddAgent(200f, 100f);
        var runner = new SimulationRunner(original);
        var before = runner.CaptureSnapshot();
        Set("_world", original);
        Set("_runner", runner);
        Set("_prevSnapshot", before);
        Set("_currSnapshot", before);
        Set("_selectedParameter", 0);
        Assert.Equal(0, runner.Advance(runner.FixedDeltaTime * 0.5));
        Assert.True(runner.Accumulator > 0);

        KeyboardKey[] keys = recreationKey is { } key ? [key, KeyboardKey.X] : [KeyboardKey.X];
        var input = RenderProgram.Controls.Capture(RendererMode.Legacy, new PressedKeys(keys));
        Invoke("HandleInput", original, input);

        var active = Assert.IsType<World>(Get("_world"));
        var activeRunner = Assert.IsType<SimulationRunner>(Get("_runner"));
        var current = Assert.IsType<SimSnapshot>(Get("_currSnapshot"));
        Assert.Same(active, activeRunner.World);
        Assert.Equal(before.AgentCount, current.AgentCount);
        for (int i = 0; i < active.Count; i++)
        {
            Assert.True(active.Vx[i] != before.VelocitiesX[i] || active.Vy[i] != before.VelocitiesY[i]);
            Assert.Equal(active.Vx[i], current.VelocitiesX[i]);
            Assert.Equal(active.Vy[i], current.VelocitiesY[i]);
            Assert.Equal(before.PositionsX[i], current.PositionsX[i]);
            Assert.Equal(before.PositionsY[i], current.PositionsY[i]);
        }
        Assert.Same(current, Get("_prevSnapshot"));
        Assert.True(current.CaptureVersion > before.CaptureVersion);
        Assert.Equal(1L, activeRunner.MutationVersion);
        Assert.Equal(activeRunner.MutationVersion, current.MutationVersion);
        Assert.Equal(0d, activeRunner.Accumulator);
        Assert.Equal(before.TickCount, current.TickCount);
    }

    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
    private static object? Get(string name) => typeof(RenderProgram).GetField(name, Flags)!.GetValue(null);
    private static void Set(string name, object? value) => typeof(RenderProgram).GetField(name, Flags)!.SetValue(null, value);
    private static void Invoke(string name, params object[] args) => typeof(RenderProgram).GetMethod(name, Flags)!.Invoke(null, args);

    // The production input path is deliberately exercised without initializing a native window.
    // Serialize this collection and restore every mutable static field so other renderer tests
    // cannot observe the parameter, runner or snapshot changes made by this regression.
    private sealed class RendererStateScope : IDisposable
    {
        private readonly (FieldInfo Field, object? Value)[] _fields = typeof(RenderProgram)
            .GetFields(Flags).Where(field => !field.IsInitOnly && !field.IsLiteral)
            .Select(field => (field, field.GetValue(null))).ToArray();
        private readonly TextWriter _output = Console.Out;

        public RendererStateScope() => Console.SetOut(TextWriter.Null);

        public void Dispose()
        {
            foreach (var (field, value) in _fields) field.SetValue(null, value);
            Console.SetOut(_output);
        }
    }

    private sealed class PressedKeys(params KeyboardKey[] keys) : IRendererInput
    {
        public bool IsPressed(KeyboardKey key) => keys.Contains(key);
        public bool IsDown(KeyboardKey key) => false;
        public bool IsPressed(MouseButton button) => false;
    }
}
