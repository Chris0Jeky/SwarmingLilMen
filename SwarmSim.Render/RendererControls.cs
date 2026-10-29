using Raylib_cs;
using SwarmSim.Core;

namespace SwarmSim.Render;

internal enum RendererMode { Legacy, Canonical }

internal enum RendererControlId
{
    SeparationWeight, AlignmentWeight, CohesionWeight, SeparationRadius, SenseRadius, Speed, MaxForce, Friction,
    Increase, Decrease, FineAdjustment,
    Preset1, Preset2, Preset3, Preset4, Preset5,
    PrintConfiguration, Reset, Help, Quit,
    SpawnGroup0, SpawnGroup1, SpawnRandom, VelocityVectors, SenseCircles, NeighborLinks, DebugOverlay, ExportCsv, Shake,
    Interactions, NextSubject
}

/// <summary>The only platform-specific input boundary; tests supply key/mouse states directly.</summary>
internal interface IRendererInput
{
    bool IsPressed(KeyboardKey key);
    bool IsDown(KeyboardKey key);
    bool IsPressed(MouseButton button);
}

internal sealed class RaylibRendererInput : IRendererInput
{
    internal static readonly RaylibRendererInput Instance = new();
    private RaylibRendererInput() { }
    public bool IsPressed(KeyboardKey key) => Raylib.IsKeyPressed(key);
    public bool IsDown(KeyboardKey key) => Raylib.IsKeyDown(key);
    public bool IsPressed(MouseButton button) => Raylib.IsMouseButtonPressed(button);
}

internal readonly struct RendererInputSnapshot(RendererMode mode, ulong active)
{
    internal RendererMode Mode { get; } = mode;
    internal bool IsActive(RendererControlId action) => (active & (1UL << (int)action)) != 0;
}

internal sealed class RendererControlBinding(
    RendererControlId id, string section, string keyLabel, string description, string shortDescription,
    KeyboardKey[]? pressedKeys = null, KeyboardKey[]? heldKeys = null, MouseButton? mouse = null)
{
    internal RendererControlId Id { get; } = id;
    internal string Section { get; } = section;
    internal string KeyLabel { get; } = keyLabel;
    internal string Description { get; } = description;
    internal string ShortDescription { get; } = shortDescription;
    internal IReadOnlyList<KeyboardKey> PressedKeys { get; } = Array.AsReadOnly(pressedKeys ?? []);

    internal bool IsActive(IRendererInput input)
    {
        for (int i = 0; i < PressedKeys.Count; i++)
            if (input.IsPressed(PressedKeys[i])) return true;
        if (heldKeys is not null)
            foreach (KeyboardKey key in heldKeys)
                if (input.IsDown(key)) return true;
        return mouse is not null && input.IsPressed(mouse.Value);
    }
}

internal readonly record struct RendererParameter(
    int Index, RendererControlId Control, string Name, float MinValue, float MaxValue, float StepSize, float FineStepSize)
{
    internal string DisplayName(RendererMode mode) => Control switch
    {
        RendererControlId.Speed when mode == RendererMode.Canonical => "Target speed",
        RendererControlId.Friction => "Friction (Damped only)",
        _ => Name
    };
}

internal readonly record struct RendererHelpLine(string Text, bool IsHeading = false);

/// <summary>
/// One engine-scoped inventory feeds input capture, parameter dispatch, and every control label.
/// Bindings describe accepted input; friction's effective legacy speed model is stated separately.
/// </summary>
internal sealed class RendererControls
{
    internal const int SenseCircleAgentLimit = 100;
    private static readonly RendererParameter[] Parameters =
    [
        new(0, RendererControlId.SeparationWeight, "Separation weight", 0f, 500f, 10f, 0.01f),
        new(1, RendererControlId.AlignmentWeight, "Alignment weight", 0f, 500f, 10f, 0.01f),
        new(2, RendererControlId.CohesionWeight, "Cohesion weight", 0f, 10f, 0.1f, 0.0001f),
        new(3, RendererControlId.SeparationRadius, "Separation radius", 5f, 200f, 5f, 1f),
        new(4, RendererControlId.SenseRadius, "Sense radius", 10f, 300f, 10f, 2f),
        new(5, RendererControlId.Speed, "Max speed", 1f, 500f, 10f, 1f),
        new(6, RendererControlId.MaxForce, "Max force", 0.1f, 20f, 0.1f, 0.01f),
        new(7, RendererControlId.Friction, "Friction", 0.8f, 1f, 0.01f, 0.001f)
    ];
    private static readonly IReadOnlyList<RendererParameter> LegacyParameters = Array.AsReadOnly(Parameters);
    private static readonly IReadOnlyList<RendererParameter> CanonicalParameters = Array.AsReadOnly(Parameters[..7]);
    private readonly IReadOnlyList<RendererControlBinding> _legacy;
    private readonly IReadOnlyList<RendererControlBinding> _canonical;
    private readonly RendererControlBinding[] _presets;
    private readonly IReadOnlyList<RendererHelpLine> _legacyConstantHelp;
    private readonly IReadOnlyList<RendererHelpLine> _legacyDampedHelp;
    private readonly IReadOnlyList<RendererHelpLine> _canonicalHelp;

    internal RendererControls(IReadOnlyList<string> presetNames)
    {
        if (presetNames.Count != 5)
            throw new ArgumentException("The renderer binds exactly five F-key presets.", nameof(presetNames));
        _presets = new RendererControlBinding[presetNames.Count];
        for (int i = 0; i < _presets.Length; i++)
            _presets[i] = new(RendererControlId.Preset1 + i, "Presets", $"F{i + 1}",
                $"Load {presetNames[i]}", presetNames[i], [KeyboardKey.F1 + i]);
        _legacy = BuildBindings(RendererMode.Legacy);
        _canonical = BuildBindings(RendererMode.Canonical);
        _legacyConstantHelp = BuildHelp(RendererMode.Legacy, SpeedModel.ConstantSpeed);
        _legacyDampedHelp = BuildHelp(RendererMode.Legacy, SpeedModel.Damped);
        _canonicalHelp = BuildHelp(RendererMode.Canonical, SpeedModel.ConstantSpeed);
    }

    internal IReadOnlyList<RendererControlBinding> GetBindings(RendererMode mode) =>
        mode == RendererMode.Canonical ? _canonical : _legacy;

    internal IReadOnlyList<RendererParameter> GetParameters(RendererMode mode) =>
        mode == RendererMode.Canonical ? CanonicalParameters : LegacyParameters;

    internal RendererControlBinding GetPreset(int index) => _presets[index];

    internal RendererControlBinding GetBinding(RendererMode mode, RendererControlId id)
    {
        foreach (RendererControlBinding binding in GetBindings(mode))
            if (binding.Id == id) return binding;
        throw new ArgumentException($"{id} is not a {mode} renderer control.", nameof(id));
    }

    internal KeyboardKey ExitKey(RendererMode mode) => GetBinding(mode, RendererControlId.Quit).PressedKeys[0];

    internal RendererInputSnapshot Capture(RendererMode mode, IRendererInput input)
    {
        ulong active = 0;
        IReadOnlyList<RendererControlBinding> bindings = GetBindings(mode);
        for (int i = 0; i < bindings.Count; i++)
        {
            RendererControlBinding binding = bindings[i];
            if (binding.IsActive(input)) active |= 1UL << (int)binding.Id;
        }
        return new(mode, active);
    }

    /// <summary>Shared effective parameter dispatch; canonical cannot select the legacy friction slot.</summary>
    internal bool ApplyParameters(RendererInputSnapshot input, ref int selected,
        Func<int, float> getValue, Action<int, float> setValue, TextWriter output,
        SpeedModel speedModel = SpeedModel.ConstantSpeed)
    {
        IReadOnlyList<RendererParameter> parameters = GetParameters(input.Mode);
        if ((uint)selected >= (uint)parameters.Count) selected = 0;
        for (int i = 0; i < parameters.Count; i++)
        {
            RendererParameter parameter = parameters[i];
            if (!input.IsActive(parameter.Control)) continue;
            selected = parameter.Index;
            output.WriteLine($"Selected: {parameter.DisplayName(input.Mode)} = {getValue(selected):F4}");
            if (parameter.Control == RendererControlId.Friction) output.WriteLine(FrictionStatus(speedModel));
        }

        bool increase = input.IsActive(RendererControlId.Increase);
        if (!increase && !input.IsActive(RendererControlId.Decrease)) return false;
        RendererParameter active = parameters[selected];
        float step = input.IsActive(RendererControlId.FineAdjustment) ? active.FineStepSize : active.StepSize;
        float value = getValue(selected);
        value = increase ? Math.Min(value + step, active.MaxValue) : Math.Max(value - step, active.MinValue);
        setValue(selected, value);
        output.WriteLine($"{active.DisplayName(input.Mode)} = {value:F4} (Step: {step:F4})");
        if (active.Control == RendererControlId.Friction) output.WriteLine(FrictionStatus(speedModel));
        return true;
    }

    internal void PrintParameters(RendererMode mode, Func<int, float> getValue, TextWriter output, SpeedModel speedModel)
    {
        output.WriteLine("\n=== Current Configuration ===");
        foreach (RendererParameter parameter in GetParameters(mode))
            output.WriteLine($"{parameter.DisplayName(mode)}: {getValue(parameter.Index):F4}");
        if (mode == RendererMode.Legacy) output.WriteLine(FrictionStatus(speedModel));
        output.WriteLine("=============================\n");
    }

    internal string FrictionStatus(SpeedModel speedModel) => speedModel == SpeedModel.Damped
        ? "Friction is active with Damped."
        : "Friction is inactive with ConstantSpeed (Damped only).";

    internal string ParameterHeading(RendererMode mode)
    {
        IReadOnlyList<RendererParameter> parameters = GetParameters(mode);
        string first = GetBinding(mode, parameters[0].Control).KeyLabel;
        string last = GetBinding(mode, parameters[^1].Control).KeyLabel;
        return $"PARAMETERS ({first}-{last} to select)";
    }

    internal string ParameterHint(RendererMode mode) =>
        $"{GetBinding(mode, RendererControlId.Increase).KeyLabel} | {GetBinding(mode, RendererControlId.Decrease).KeyLabel}: adjust | {GetBinding(mode, RendererControlId.FineAdjustment).KeyLabel}: fine";

    internal string ShortLabel(RendererMode mode, RendererControlId id)
    {
        RendererControlBinding binding = GetBinding(mode, id);
        return $"{binding.KeyLabel}: {binding.ShortDescription}";
    }

    internal IReadOnlyList<RendererHelpLine> GetHelpLines(RendererMode mode, SpeedModel speedModel) =>
        mode == RendererMode.Canonical ? _canonicalHelp :
        speedModel == SpeedModel.Damped ? _legacyDampedHelp : _legacyConstantHelp;

    internal string FormatHelp(RendererMode mode, SpeedModel speedModel) =>
        string.Join(Environment.NewLine, GetHelpLines(mode, speedModel).Select(line => line.Text));

    private IReadOnlyList<RendererHelpLine> BuildHelp(RendererMode mode, SpeedModel speedModel)
    {
        var lines = new List<RendererHelpLine>();
        foreach (string section in new[] { "Parameters", "Presets", "World", "Visualization" })
        {
            lines.Add(new(section == "Parameters" ? ParameterHeading(mode) : section.ToUpperInvariant(), true));
            foreach (RendererControlBinding binding in GetBindings(mode))
                if (binding.Section == section) lines.Add(new($"  {binding.KeyLabel}: {binding.Description}"));
            if (section == "Parameters" && mode == RendererMode.Legacy)
                lines.Add(new("  " + FrictionStatus(speedModel)));
        }
        return lines.AsReadOnly();
    }

    private IReadOnlyList<RendererControlBinding> BuildBindings(RendererMode mode)
    {
        var bindings = new List<RendererControlBinding>();
        foreach (RendererParameter parameter in GetParameters(mode))
            bindings.Add(new(parameter.Control, "Parameters", (parameter.Index + 1).ToString(),
                $"Select {parameter.DisplayName(mode)}", parameter.DisplayName(mode), [KeyboardKey.One + parameter.Index]));
        bindings.Add(new(RendererControlId.Increase, "Parameters", "Up/+/KP+", "Increase selected parameter", "Increase", [KeyboardKey.Up, KeyboardKey.Equal, KeyboardKey.KpAdd]));
        bindings.Add(new(RendererControlId.Decrease, "Parameters", "Down/-/KP-", "Decrease selected parameter", "Decrease", [KeyboardKey.Down, KeyboardKey.Minus, KeyboardKey.KpSubtract]));
        bindings.Add(new(RendererControlId.FineAdjustment, "Parameters", "Shift", "Hold for fine adjustment with any adjust key", "Fine adjustment", heldKeys: [KeyboardKey.LeftShift, KeyboardKey.RightShift]));
        bindings.AddRange(_presets);
        bindings.Add(new(RendererControlId.PrintConfiguration, "World", "P", "Print current parameters to the console", "Print config", [KeyboardKey.P]));
        bindings.Add(new(RendererControlId.Reset, "World", "R", "Reset/respawn the world", "Reset", [KeyboardKey.R]));
        bindings.Add(new(RendererControlId.Quit, "World", "Esc", "Quit", "Quit", [KeyboardKey.Escape]));
        bindings.Add(new(RendererControlId.Help, "Visualization", "H", "Toggle this help overlay", "Help", [KeyboardKey.H]));
        if (mode == RendererMode.Canonical)
        {
            bindings.Add(new(RendererControlId.Interactions, "Visualization", "O", "Toggle interaction overlay", "Interactions", [KeyboardKey.O]));
            bindings.Add(new(RendererControlId.NextSubject, "Visualization", "Tab", "Cycle inspected boid", "Next boid", [KeyboardKey.Tab]));
        }
        else
        {
            bindings.Add(new(RendererControlId.SpawnGroup0, "World", "Left click", "Spawn up to 50 agents at cursor (group 0)", "Spawn group 0", mouse: MouseButton.Left));
            bindings.Add(new(RendererControlId.SpawnGroup1, "World", "Right click", "Spawn up to 50 agents at cursor (group 1)", "Spawn group 1", mouse: MouseButton.Right));
            bindings.Add(new(RendererControlId.SpawnRandom, "World", "Space", "Spawn up to 100 random agents across groups", "Random spawn", [KeyboardKey.Space]));
            bindings.Add(new(RendererControlId.Shake, "World", "X", "Shake (add random velocity)", "Shake", [KeyboardKey.X]));
            bindings.Add(new(RendererControlId.ExportCsv, "World", "C", "Export CSV snapshot", "CSV", [KeyboardKey.C]));
            bindings.Add(new(RendererControlId.VelocityVectors, "Visualization", "V", "Toggle velocity vectors", "Velocity", [KeyboardKey.V]));
            bindings.Add(new(RendererControlId.SenseCircles, "Visualization", "S", $"Toggle sense radius circles (below {SenseCircleAgentLimit} agents)", "Sense radius", [KeyboardKey.S]));
            bindings.Add(new(RendererControlId.NeighborLinks, "Visualization", "N", "Toggle tracked-agent neighbor connections", "Neighbors", [KeyboardKey.N]));
            bindings.Add(new(RendererControlId.DebugOverlay, "Visualization", "F12", "Toggle snapshot/debug overlay", "Debug overlay", [KeyboardKey.F12]));
        }
        return bindings.AsReadOnly();
    }
}
