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
