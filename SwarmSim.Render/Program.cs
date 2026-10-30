using Raylib_cs;
using SwarmSim.Core;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;
using SwarmSim.Core.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SwarmSim.Render;

internal static class Program
{
    private static readonly Version AppVersion = typeof(Program).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
    private static SimConfig _baseConfigTemplate = CreateDefaultBaseConfig();

    static Program()
    {
        SyncParametersWithConfig(_baseConfigTemplate);
    }
    // Window settings
    private const int WindowWidth = 1920;
    private const int WindowHeight = 1080;
    private const string WindowTitle = "SwarmingLilMen - Phase 2: Boids Flocking";

    // Visualization options
    private static bool _showVelocityVectors = true;
    private static bool _showSenseRadius = false;
    private static bool _showNeighborConnections = false;

    // Diagnostic tracking
    private static int[] _trackedAgents = Array.Empty<int>();
    private static readonly Random _random = new Random(42);

    // Dynamic parameter adjustment
    private static int _selectedParameter = 0;
    private static bool _fineAdjustment = false; // Hold shift for fine adjustment
    private static bool _showHelpOverlay = false;
    private static CommandLineOptions _cliOptions = new();
    private static World? _world = null;  // Store reference to world for recreation
    private static SimulationRunner? _runner = null;  // Fixed-timestep runner

    // Canonical mode helpers
    private const float CanonicalConsoleInterval = 5f;
    private const float CanonicalSelectionInterval = 30f;
    private static bool _canonicalOverlayVisible = false;
    private static int _overlaySubjectIndex = 0;
    private static readonly int[] _canonicalTrackedAgents = new int[5];
    private static readonly Random _canonicalRandom = new(314159);

    // Interpolation state for smooth rendering
    private static SimSnapshot? _prevSnapshot = null;
    private static SimSnapshot? _currSnapshot = null;
    private static bool _showDebugOverlay = false;
    private static long _lastSnapshotWarningVersion = 0;

    // Parameter values (mutable copies) - FIXED FOR PROPER EQUILIBRIUM
    private static float _separationWeight = 5.0f;    // Was 0.05 (100x stronger)
    private static float _alignmentWeight = 2.5f;     // Was 0.05 (50x stronger)
    private static float _cohesionWeight = 0.5f;      // Was 0.0005 (1000x stronger)
    private static float _separationRadius = 30f;     // Was 15
    private static float _senseRadius = 100f;         // Was 60
    private static float _maxSpeed = 10f;             // Was 6
    private static float _maxForce = 2.5f;
    private static float _friction = 1f;           // Was 0.99 (CRITICAL FIX!)
    private static float _fieldOfView = 270f;
    private static int _initialAgentCount = 400;

    // Values stay here; names, key bindings and adjustment ranges live in the shared inventory.
    private readonly record struct ParameterAccessor(Func<float> GetValue, Action<float> SetValue);
    private static readonly ParameterAccessor[] Parameters =
    [
        new(() => _separationWeight, v => _separationWeight = v),
        new(() => _alignmentWeight, v => _alignmentWeight = v),
        new(() => _cohesionWeight, v => _cohesionWeight = v),
        new(() => _separationRadius, v => _separationRadius = v),
        new(() => _senseRadius, v => _senseRadius = v),
        new(() => _maxSpeed, v => _maxSpeed = v),
        new(() => _maxForce, v => _maxForce = v),
        new(() => _friction, v => _friction = v),
    ];

    private static float GetParameterValue(int index) => Parameters[index].GetValue();
    private static void SetParameterValue(int index, float value) => Parameters[index].SetValue(value);
    private static float GetCanonicalParameterValue(int index) => index == 7 ? _fieldOfView : GetParameterValue(index);
    private static void SetCanonicalParameterValue(int index, float value)
    {
        if (index == 7) _fieldOfView = value;
        else SetParameterValue(index, value);
    }

    // Preset configurations
    private sealed class Preset
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required SimConfig Config { get; init; }
    }

    private static readonly Preset[] Presets =
    [
        new Preset
        {
            Id = "balanced",
            Name = "Balanced (Recommended)",
            Description = "Canonical boids tuning with smooth flocking",
            Config = new SimConfig
            {
                MaxSpeed = 10f,
                Friction = 0.95f,
                SenseRadius = 110f,
                SeparationRadius = 45f,
                SeparationWeight = 7.5f,
                AlignmentWeight = 2.2f,
                CohesionWeight = 0.35f,
                MaxForce = 2.5f,
                FieldOfView = 270f,
                WanderStrength = 0.45f,
                MaxNeighbors = 16,
                CollisionAvoidanceRadius = 12f,
                CollisionAvoidanceBoost = 4f,
                AttackDamage = 0f,
                BaseDrain = 0.1f,
                FixedDeltaTime = 1f / 60f
            }
        },
        new Preset
        {
            Id = "strong-separation",
            Name = "Strong Separation",
            Description = "Agents prioritize personal space, useful for dense scenes",
            Config = new SimConfig
            {
                MaxSpeed = 10f,
                Friction = 0.95f,
                SenseRadius = 100f,
                SeparationRadius = 40f,
                SeparationWeight = 10f,
                AlignmentWeight = 2f,
                CohesionWeight = 0.3f,
                MaxForce = 2f,
                FieldOfView = 270f,
                WanderStrength = 0.5f,
                FixedDeltaTime = 1f / 60f
            }
        },
        new Preset
        {
            Id = "tight-flocking",
            Name = "Tight Flocking",
            Description = "Emphasizes cohesion/alignment for ribbon-like formations",
            Config = new SimConfig
            {
                MaxSpeed = 10f,
                Friction = 0.95f,
                SenseRadius = 80f,
                SeparationRadius = 20f,
                SeparationWeight = 3f,
                AlignmentWeight = 5f,
                CohesionWeight = 2f,
                MaxForce = 2f,
                FieldOfView = 300f,
                WanderStrength = 0.25f,
                FixedDeltaTime = 1f / 60f
            }
        },
        new Preset
        {
            Id = "fast-loose",
            Name = "Fast & Loose",
            Description = "Higher speed ceiling with lighter cohesion",
            Config = new SimConfig
            {
                MaxSpeed = 15f,
                Friction = 0.9f,
                SenseRadius = 120f,
                SeparationRadius = 30f,
                SeparationWeight = 8f,
                AlignmentWeight = 4f,
                CohesionWeight = 0.5f,
                MaxForce = 3f,
                FieldOfView = 270f,
                WanderStrength = 0.75f,
                FixedDeltaTime = 1f / 60f
            }
        },
        new Preset
        {
            Id = "slow-cohesive",
            Name = "Slow & Cohesive",
            Description = "Lower speed with high cohesion for schooling behavior",
            Config = new SimConfig
            {
                MaxSpeed = 5f,
                Friction = 0.98f,
                SenseRadius = 80f,
                SeparationRadius = 25f,
                SeparationWeight = 2f,
                AlignmentWeight = 3f,
                CohesionWeight = 3f,
                MaxForce = 1.5f,
                FieldOfView = 270f,
                WanderStrength = 0.2f,
                FixedDeltaTime = 1f / 60f
            }
        }
    ];

    // Registered preset IDs, derived from Presets so help text and tests cannot drift from the
    // registry Main actually resolves against. Must stay declared AFTER Presets: static field
    // initializers run in declaration order, so an earlier declaration would read a null Presets.
    internal static readonly IReadOnlyList<string> PresetIds = Array.ConvertAll(Presets, p => p.Id);
    internal static readonly RendererControls Controls = new(Array.ConvertAll(Presets, p => p.Name));

    // Color palette for groups (16 colors)
    private static readonly Color[] GroupColors =
    [
        Color.White,      // Group 0
        Color.Red,        // Group 1
        Color.Green,      // Group 2
        Color.Blue,       // Group 3
        Color.Yellow,     // Group 4
        Color.Magenta,    // Group 5
        Color.Orange,     // Group 6
        Color.Purple,     // Group 7
        Color.Pink,       // Group 8
        Color.Lime,       // Group 9
        Color.SkyBlue,    // Group 10
        Color.Violet,     // Group 11
        Color.Beige,      // Group 12
        Color.Brown,      // Group 13
        Color.Gold,       // Group 14
        Color.Maroon      // Group 15
    ];

    internal static SimConfig CreateDefaultBaseConfig() => new()
    {
        Seed = 42u,
        WorldWidth = WindowWidth,
        WorldHeight = WindowHeight,
        BoundaryMode = BoundaryMode.Wrap,
        FixedDeltaTime = 1f / 60f,
        MaxSpeed = 10f,
        MaxForce = 2.5f,
        Friction = 0.95f,
        SpeedModel = SpeedModel.ConstantSpeed,
        GridCellSize = 50f,
        SenseRadius = 110f,
        SeparationRadius = 45f,
        FieldOfView = 270f,
        SeparationWeight = 7.5f,
        AlignmentWeight = 2.2f,
        CohesionWeight = 0.35f,
        WanderStrength = 0f,
        MaxNeighbors = 16,
        CollisionAvoidanceRadius = 12f,
        CollisionAvoidanceBoost = 4f,
        AttackDamage = 0f,
        AttackRadius = 15f,
        AttackCooldown = 0.5f,
        InitialEnergy = 100f,
        MaxEnergy = 200f,
        BaseDrain = 0.1f,
        MoveCost = 0.01f,
        DeathEnergyThreshold = 0f,
        InitialHealth = 100f,
        MaxHealth = 100f,
        HealthRegenRate = 0.5f,
        ReproductionEnergyThreshold = 150f,
        ReproductionEnergyCost = 50f,
        ChildEnergyStart = 50f,
        MutationRate = 0.1f,
        MutationStdDev = 0.2f,
        FoodEnergyGain = 20f,
        ForageRadius = 10f,
        AggressionMatrix = SimConfig.NeutralAggressionMatrix(4),
        InitialCapacity = 100_000,
        MaxCapacity = 200_000,
        CompactionInterval = 600
    };

    private static void ApplyBaseConfig(SimConfig config)
    {
        _baseConfigTemplate = CopyConfig(config);
        SyncParametersWithConfig(_baseConfigTemplate);
    }

    private static void SyncParametersWithConfig(SimConfig config)
    {
        _maxSpeed = config.MaxSpeed;
        _maxForce = config.MaxForce;
        _friction = config.Friction;
        _senseRadius = config.SenseRadius;
        _separationRadius = config.SeparationRadius;
        _separationWeight = config.SeparationWeight;
        _alignmentWeight = config.AlignmentWeight;
        _cohesionWeight = config.CohesionWeight;
        _fieldOfView = config.FieldOfView;
    }

    internal static SimConfig CopyConfig(SimConfig source)
    {
        return new SimConfig
        {
            Seed = source.Seed,
            WorldWidth = source.WorldWidth,
            WorldHeight = source.WorldHeight,
            BoundaryMode = source.BoundaryMode,
            FixedDeltaTime = source.FixedDeltaTime,
            MaxSpeed = source.MaxSpeed,
            MaxForce = source.MaxForce,
            Friction = source.Friction,
            SpeedModel = source.SpeedModel,
            GridCellSize = source.GridCellSize,
            SenseRadius = source.SenseRadius,
            SeparationRadius = source.SeparationRadius,
            FieldOfView = source.FieldOfView,
            SeparationWeight = source.SeparationWeight,
            AlignmentWeight = source.AlignmentWeight,
            CohesionWeight = source.CohesionWeight,
            WanderStrength = source.WanderStrength,
            WanderRate = source.WanderRate,
            MaxTurnRateDegPerSecond = source.MaxTurnRateDegPerSecond,
            WhiskerTimeHorizon = source.WhiskerTimeHorizon,
            WhiskerWeight = source.WhiskerWeight,
            SeparationPriorityRadiusFactor = source.SeparationPriorityRadiusFactor,
            SeparationPriorityExitFactor = source.SeparationPriorityExitFactor,
            SeparationPriorityBoost = source.SeparationPriorityBoost,
            SeparationPriorityHoldTime = source.SeparationPriorityHoldTime,
            SeparationPriorityRampInTime = source.SeparationPriorityRampInTime,
            SeparationPriorityRampOutTime = source.SeparationPriorityRampOutTime,
            SeparationSpeedDroop = source.SeparationSpeedDroop,
            SeparationCrowdingThreshold = source.SeparationCrowdingThreshold,
            SeparationCrowdingBoost = source.SeparationCrowdingBoost,
            MaxNeighbors = source.MaxNeighbors,
            CollisionAvoidanceRadius = source.CollisionAvoidanceRadius,
            CollisionAvoidanceBoost = source.CollisionAvoidanceBoost,
            AttackRadius = source.AttackRadius,
            AttackDamage = source.AttackDamage,
            AttackCooldown = source.AttackCooldown,
            InitialEnergy = source.InitialEnergy,
            MaxEnergy = source.MaxEnergy,
            BaseDrain = source.BaseDrain,
            MoveCost = source.MoveCost,
            DeathEnergyThreshold = source.DeathEnergyThreshold,
            InitialHealth = source.InitialHealth,
            MaxHealth = source.MaxHealth,
            HealthRegenRate = source.HealthRegenRate,
            ReproductionEnergyThreshold = source.ReproductionEnergyThreshold,
            ReproductionEnergyCost = source.ReproductionEnergyCost,
            ChildEnergyStart = source.ChildEnergyStart,
            MutationRate = source.MutationRate,
            MutationStdDev = source.MutationStdDev,
            FoodEnergyGain = source.FoodEnergyGain,
            ForageRadius = source.ForageRadius,
            AggressionMatrix = source.AggressionMatrix,
            InitialCapacity = source.InitialCapacity,
            MaxCapacity = source.MaxCapacity,
            CompactionInterval = source.CompactionInterval
        };
    }

    private static SimConfig BuildCurrentConfig() => BuildCurrentConfig(_baseConfigTemplate);

    internal static SimConfig BuildCurrentConfig(SimConfig template)
    {
        return new SimConfig
        {
            Seed = template.Seed,
            WorldWidth = WindowWidth,
            WorldHeight = WindowHeight,
            BoundaryMode = template.BoundaryMode,
            FixedDeltaTime = template.FixedDeltaTime,
            MaxSpeed = _maxSpeed,
            MaxForce = _maxForce,
            Friction = _friction,
            SpeedModel = template.SpeedModel,
            GridCellSize = template.GridCellSize,
            SenseRadius = _senseRadius,
            SeparationRadius = _separationRadius,
            FieldOfView = _fieldOfView,
            SeparationWeight = _separationWeight,
            AlignmentWeight = _alignmentWeight,
            CohesionWeight = _cohesionWeight,
            WanderStrength = template.WanderStrength,
            WanderRate = template.WanderRate,
            MaxTurnRateDegPerSecond = template.MaxTurnRateDegPerSecond,
            WhiskerTimeHorizon = template.WhiskerTimeHorizon,
            WhiskerWeight = template.WhiskerWeight,
            SeparationPriorityRadiusFactor = template.SeparationPriorityRadiusFactor,
            SeparationPriorityExitFactor = template.SeparationPriorityExitFactor,
            SeparationPriorityBoost = template.SeparationPriorityBoost,
            SeparationPriorityHoldTime = template.SeparationPriorityHoldTime,
            SeparationPriorityRampInTime = template.SeparationPriorityRampInTime,
            SeparationPriorityRampOutTime = template.SeparationPriorityRampOutTime,
            SeparationSpeedDroop = template.SeparationSpeedDroop,
            SeparationCrowdingThreshold = template.SeparationCrowdingThreshold,
            SeparationCrowdingBoost = template.SeparationCrowdingBoost,
            MaxNeighbors = template.MaxNeighbors,
            CollisionAvoidanceRadius = template.CollisionAvoidanceRadius,
            CollisionAvoidanceBoost = template.CollisionAvoidanceBoost,
            AttackRadius = template.AttackRadius,
            AttackDamage = template.AttackDamage,
            AttackCooldown = template.AttackCooldown,
            InitialEnergy = template.InitialEnergy,
            MaxEnergy = template.MaxEnergy,
            BaseDrain = template.BaseDrain,
            MoveCost = template.MoveCost,
            DeathEnergyThreshold = template.DeathEnergyThreshold,
            InitialHealth = template.InitialHealth,
            MaxHealth = template.MaxHealth,
            HealthRegenRate = template.HealthRegenRate,
            ReproductionEnergyThreshold = template.ReproductionEnergyThreshold,
            ReproductionEnergyCost = template.ReproductionEnergyCost,
            ChildEnergyStart = template.ChildEnergyStart,
            MutationRate = template.MutationRate,
            MutationStdDev = template.MutationStdDev,
            FoodEnergyGain = template.FoodEnergyGain,
            ForageRadius = template.ForageRadius,
            AggressionMatrix = template.AggressionMatrix,
            InitialCapacity = template.InitialCapacity,
            MaxCapacity = template.MaxCapacity,
            CompactionInterval = template.CompactionInterval
        };
    }
    /// <summary>
    /// Resolves authored inputs in shared scenario/file order. An unauthored launch retains
    /// the renderer profile; preset/file launches retain their existing SimConfig defaults.
    /// Display-name aliases normalize through the same registry used by the F-key UI.
    /// </summary>
    internal static SimConfig ResolveStartupConfiguration(string? presetName, string? configFile)
    {
        string? scenarioId = null;
        if (!string.IsNullOrWhiteSpace(presetName))
        {
            if (!TryGetPreset(presetName, out var preset))
                throw new ArgumentException($"Unknown preset '{presetName}'.", nameof(presetName));
            scenarioId = preset.Id;
        }

        SimConfig? defaults = scenarioId is null && string.IsNullOrWhiteSpace(configFile)
            ? CreateDefaultBaseConfig()
            : null;
        return SwarmSim.Core.Configuration.ConfigurationResolver.ResolveSimConfig(
            scenarioName: scenarioId, filePath: configFile, defaults: defaults);
    }

    internal static int Main(string[] args)
    {
        _cliOptions = CommandLineOptions.Parse(args);

        if (_cliOptions.Error != null)
        {
            Console.Error.WriteLine($"{_cliOptions.Error} Use --help for usage.");
            return 2;
        }

        if (_cliOptions.RunMinimalTest)
        {
            MinimalTest.Run();
            return 0;
        }

        if (_cliOptions.ShowVersion)
        {
            PrintVersionInfo();
            return 0;
        }

        if (_cliOptions.ListPresets)
        {
            PrintPresetList();
            return 0;
        }

        if (_cliOptions.ShowHelp)
        {
            Console.WriteLine(CommandLineOptions.GetHelpText());
            PrintPresetList();
            return 0;
        }

        Preset? selectedPreset = null;
        if (!string.IsNullOrWhiteSpace(_cliOptions.PresetName))
        {
            if (!TryGetPreset(_cliOptions.PresetName, out var preset))
            {
                Console.Error.WriteLine($"Unknown preset '{_cliOptions.PresetName}'. Use --list-presets to view options.");
                return 2;
            }
            selectedPreset = preset;
        }

        try
        {
            var config = ResolveStartupConfiguration(selectedPreset?.Id, _cliOptions.ConfigFile);
            ApplyBaseConfig(config);
            if (!string.IsNullOrWhiteSpace(_cliOptions.ConfigFile))
            {
                WarnIfInvalidConfig(config, _cliOptions.ConfigFile);
                Console.WriteLine($"Loaded configuration from {_cliOptions.ConfigFile}");
            }
            else if (selectedPreset is not null)
            {
                WarnIfInvalidConfig(config, selectedPreset.Name);
                Console.WriteLine($"Preset '{selectedPreset.Name}' loaded.");
            }
        }
        catch (Exception ex)
        {
            string source = _cliOptions.ConfigFile ?? selectedPreset?.Name ?? "renderer defaults";
            Console.WriteLine($"Failed to load configuration '{source}': {ex.Message}");
            return 1;
        }

        _initialAgentCount = _cliOptions.AgentCount ?? _initialAgentCount;

        if (_cliOptions.BenchmarkMode)
        {
            Console.WriteLine("SwarmSim.Render --benchmark is deprecated; this option now prints migration guidance only.");
            Console.WriteLine("Run experiments: dotnet run --project SwarmSim.Cli -c Release -- --engine canonical --seed 7 --ticks 600 --agents 400 --out runs/");
            Console.WriteLine("Select --engine legacy to use the legacy engine instead.");
            Console.WriteLine("Measure throughput and allocations: dotnet run --project SwarmSim.Benchmarks -c Release -- --canonical");
            return 0;
        }

        if (_cliOptions.UseCanonicalMode)
        {
            RunCanonicalMode();
            return 0;
        }

        PrintStartupBanner(_initialAgentCount);

        Raylib.InitWindow(WindowWidth, WindowHeight, WindowTitle);
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(Controls.ExitKey(RendererMode.Legacy));

        var world = CreateWorldWithCurrentParams();
        _world = world;
        SpawnInitialAgents(world, _initialAgentCount);

        _runner = new SimulationRunner(world);
        ForceSnapshotRefresh("initial world", notifyRunner: false);

        int frameCount = 0;

        while (!Raylib.WindowShouldClose())
        {
            Debug.Assert(_world != null, nameof(_world) + " != null");
            RendererInputSnapshot input = Controls.Capture(RendererMode.Legacy, RaylibRendererInput.Instance);
            if (input.IsActive(RendererControlId.Quit)) break;
            HandleInput(_world, input);

            float frameTime = Raylib.GetFrameTime();
            int stepsProcessed = _runner.Advance(frameTime);

            if (stepsProcessed > 0)
            {
                _prevSnapshot = _currSnapshot;
                _currSnapshot = _runner.CaptureSnapshot();
            }

            SimSnapshot? prevSnapshot = _prevSnapshot ?? _currSnapshot;
            SimSnapshot? currSnapshot = _currSnapshot ?? _prevSnapshot;

            if (currSnapshot == null)
            {
                continue;
            }

            bool canInterpolate = prevSnapshot != null &&
                                  prevSnapshot.CaptureVersion != currSnapshot.CaptureVersion &&
                                  prevSnapshot.MutationVersion == currSnapshot.MutationVersion;

            float alpha = 0f;
            if (canInterpolate)
            {
                alpha = (float)Math.Clamp(_runner.Accumulator / _runner.FixedDeltaTime, 0f, 1f);
            }
            else
            {
                prevSnapshot = currSnapshot;
            }

            frameCount++;
            if (frameCount % 120 == 0 && _world != null)
            {
                PrintDiagnostics(_world);
            }

            RenderInterpolated(prevSnapshot!, currSnapshot, alpha);
        }

        Raylib.CloseWindow();
        return 0;
    }

    private static World CreateWorldWithCurrentParams()
    {
        SimConfig config = BuildCurrentConfig();
        return new World(config);
    }

    private static void ForceSnapshotRefresh(string reason, bool notifyRunner = true)
    {
        if (_runner == null)
            return;

        if (notifyRunner)
        {
            _runner.NotifyWorldMutated();
        }

        var snapshot = _runner.CaptureSnapshot();
        snapshot.DebugAssertConsistent(reason);
        _currSnapshot = snapshot;
        _prevSnapshot = snapshot;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Console.WriteLine($"[Snapshots] Refresh ({reason}) → capture {snapshot.CaptureVersion}, mutation {snapshot.MutationVersion}");
        }
    }

    private static void SpawnInitialAgents(World world, int agentCount)
    {
        if (agentCount <= 0)
            return;

        float centerX = WindowWidth * 0.5f;
        float centerY = WindowHeight * 0.5f;
        float clusterSpacing = MathF.Max(150f, MathF.Min(WindowWidth, WindowHeight) * 0.2f);
        float spawnRadius = MathF.Max(80f, MathF.Sqrt(agentCount) * 1.2f);

        int groups = 4;
        int basePerGroup = agentCount / groups;
        int remainder = agentCount % groups;

        int[] groupCounts = new int[groups];
        for (int g = 0; g < groups; g++)
        {
            groupCounts[g] = basePerGroup + (g < remainder ? 1 : 0);
        }

        world.SpawnAgentsInCircle(centerX - clusterSpacing, centerY - clusterSpacing, spawnRadius, groupCounts[0], group: 0);
        world.SpawnAgentsInCircle(centerX + clusterSpacing, centerY - clusterSpacing, spawnRadius, groupCounts[1], group: 1);
        world.SpawnAgentsInCircle(centerX - clusterSpacing, centerY + clusterSpacing, spawnRadius, groupCounts[2], group: 2);
        world.SpawnAgentsInCircle(centerX + clusterSpacing, centerY + clusterSpacing, spawnRadius, groupCounts[3], group: 3);

        var rng = world.Rng;
        for (int i = 0; i < world.Count; i++)
        {
            (float vx, float vy) = rng.NextUnitVector();
            float speed = rng.NextFloat(2f, 4f);
            world.Vx[i] = vx * speed;
            world.Vy[i] = vy * speed;
        }

        Console.WriteLine($"Spawned {agentCount} agents across {groups} clusters.");
    }

    private static void HandleInput(World world, RendererInputSnapshot input)
    {
        _fineAdjustment = input.IsActive(RendererControlId.FineAdjustment);
        if (Controls.ApplyParameters(input, ref _selectedParameter, GetParameterValue, SetParameterValue, Console.Out, world.Config.SpeedModel))
        {
            RecreateWorldWithNewParams(world);
            world = _world!;
        }

        for (int i = 0; i < Presets.Length; i++)
        {
            if (!input.IsActive(Controls.GetPreset(i).Id)) continue;
            ApplyPreset(Presets[i]);
            RecreateWorldWithNewParams(world);
            world = _world!;
        }

        // Remaining actions in this captured frame must target the newly active world.

        if (input.IsActive(RendererControlId.PrintConfiguration))
            Controls.PrintParameters(RendererMode.Legacy, GetParameterValue, Console.Out, _baseConfigTemplate.SpeedModel);

        // === EXISTING CONTROLS ===
        // Left click: Spawn agents at mouse position
        if (input.IsActive(RendererControlId.SpawnGroup0))
        {
            var mousePos = Raylib.GetMousePosition();
            int spawned = world.SpawnAgentsInCircle(mousePos.X, mousePos.Y, 50f, 50, group: 0);
            Console.WriteLine($"Spawned {spawned} agents at mouse position");
            ForceSnapshotRefresh("mouse spawn", notifyRunner: true);
        }

        // Right click: Spawn different group
        if (input.IsActive(RendererControlId.SpawnGroup1))
        {
            var mousePos = Raylib.GetMousePosition();
            int spawned = world.SpawnAgentsInCircle(mousePos.X, mousePos.Y, 50f, 50, group: 1);
            Console.WriteLine($"Spawned {spawned} agents (group 1) at mouse position");
            ForceSnapshotRefresh("mouse spawn (group1)", notifyRunner: true);
        }

        // Space: Spawn random agents
        if (input.IsActive(RendererControlId.SpawnRandom))
        {
            int spawned = SpawnRandomAgents(world);
            Console.WriteLine($"Spawned {spawned} random agents");
            ForceSnapshotRefresh("space spawn", notifyRunner: true);
        }

        // R: Reset world
        if (input.IsActive(RendererControlId.Reset))
        {
            // Clear all agents (mark all as dead and compact)
            for (int i = 0; i < world.Count; i++)
            {
                world.MarkDead(i);
            }
            world.CompactDeadAgents();
            SpawnInitialAgents(world, _initialAgentCount);
            Console.WriteLine("World reset");
            ForceSnapshotRefresh("world reset", notifyRunner: true);
        }

        // V: Toggle velocity vectors
        if (input.IsActive(RendererControlId.VelocityVectors))
        {
            _showVelocityVectors = !_showVelocityVectors;
            Console.WriteLine($"Velocity vectors: {(_showVelocityVectors ? "ON" : "OFF")}");
        }

        // S: Toggle sense radius visualization
        if (input.IsActive(RendererControlId.SenseCircles))
        {
            _showSenseRadius = !_showSenseRadius;
            Console.WriteLine($"Sense radius: {(_showSenseRadius ? "ON" : "OFF")}");
        }

        // N: Toggle neighbor connections
        if (input.IsActive(RendererControlId.NeighborLinks))
        {
            _showNeighborConnections = !_showNeighborConnections;
            Console.WriteLine($"Neighbor connections: {(_showNeighborConnections ? "ON" : "OFF")}");
        }

        if (input.IsActive(RendererControlId.Help))
        {
            _showHelpOverlay = !_showHelpOverlay;
            Console.WriteLine($"Help overlay: {(_showHelpOverlay ? "ON" : "OFF")}");
        }

        if (input.IsActive(RendererControlId.DebugOverlay))
        {
            _showDebugOverlay = !_showDebugOverlay;
            Console.WriteLine($"Debug overlay: {(_showDebugOverlay ? "ON" : "OFF")}");
        }

        // C: Export CSV snapshot
        if (input.IsActive(RendererControlId.ExportCsv))
        {
            ExportCSV(world);
            Console.WriteLine("Exported CSV snapshot");
        }

        // X: "Shake" - add random velocity to break equilibrium
        if (input.IsActive(RendererControlId.Shake))
        {
            var rng = world.Rng;
            for (int i = 0; i < world.Count; i++)
            {
                if (world.State[i].HasFlag(AgentState.Dead)) continue;
                (float dvx, float dvy) = rng.NextUnitVector();
                float dSpeed = rng.NextFloat(1f, 3f); // Small shake for low max speed
                world.Vx[i] += dvx * dSpeed;
                world.Vy[i] += dvy * dSpeed;
            }
            Console.WriteLine("Shook agents to break equilibrium!");
        }
    }

    internal static int SpawnRandomAgents(World world)
    {
        int spawned = 0;
        for (int i = 0; i < 100; i++)
            if (world.AddRandomAgent(group: (byte)(i % 4)) >= 0) spawned++;
        return spawned;
    }

    private static void ApplyPreset(Preset preset)
    {
        ApplyBaseConfig(preset.Config);
        WarnIfInvalidConfig(preset.Config, preset.Name);
        Console.WriteLine($"Applied preset: {preset.Name}");
        Console.WriteLine($"  Description: {preset.Description}");
    }

    private static void RecreateWorldWithNewParams(World oldWorld)
    {
        // Store agent positions and velocities
        var positions = new List<(float x, float y, float vx, float vy, byte group)>();
        for (int i = 0; i < oldWorld.Count; i++)
        {
            if (!oldWorld.State[i].HasFlag(AgentState.Dead))
            {
                positions.Add((oldWorld.X[i], oldWorld.Y[i], oldWorld.Vx[i], oldWorld.Vy[i], oldWorld.Group[i]));
            }
        }

        // Create new world with updated parameters
        _world = CreateWorldWithCurrentParams();

        // Restore agents
        foreach (var (x, y, vx, vy, group) in positions)
        {
            int idx = _world.AddAgent(x, y, group);
            if (idx >= 0)
            {
                _world.Vx[idx] = vx;
                _world.Vy[idx] = vy;
            }
        }

        // Recreate simulation runner with new world
        _runner = new SimulationRunner(_world);

        // Reset snapshots
        ForceSnapshotRefresh("world recreated", notifyRunner: false);
    }

    /// <summary>
    /// Renders the simulation with interpolation between previous and current snapshots.
    /// </summary>
    private static void RenderInterpolated(SimSnapshot prevSnapshot, SimSnapshot currSnapshot, float alpha)
    {
        prevSnapshot.DebugAssertConsistent("render-prev");
        currSnapshot.DebugAssertConsistent("render-curr");
        bool interpolating = alpha > 0f && prevSnapshot.CaptureVersion != currSnapshot.CaptureVersion;

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        // Draw agents with interpolation
        DrawAgentsInterpolated(prevSnapshot, currSnapshot, alpha);

        // Draw UI (uses _world directly for stats)
        if (_world != null)
        {
            DrawUI(_world);
        }

        if (_showDebugOverlay && _runner != null)
        {
            DrawDebugOverlay(prevSnapshot, currSnapshot, alpha, interpolating);
        }

        if (_showHelpOverlay)
        {
            DrawHelpOverlay(RendererMode.Legacy, _world?.Config.SpeedModel ?? _baseConfigTemplate.SpeedModel);
        }

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Draws agents with linear interpolation between previous and current positions.
    /// Handles cases where agent count differs between snapshots (spawning/removal).
    /// </summary>
    private static void DrawAgentsInterpolated(SimSnapshot prevSnapshot, SimSnapshot currSnapshot, float alpha)
    {
        int rawCurrCount = currSnapshot.AgentCount;
        int rawPrevCount = prevSnapshot.AgentCount;
        int currCount = GetSafeAgentCount(currSnapshot);
        int prevCount = GetSafeAgentCount(prevSnapshot);

        // Defensive check: log significant snapshot mismatches for debugging (but avoid spamming)
        if (Math.Abs(rawCurrCount - rawPrevCount) >= 50 &&
            currSnapshot.CaptureVersion != _lastSnapshotWarningVersion)
        {
            Console.WriteLine(
                $"⚠️  Snapshot mismatch: prev={rawPrevCount}, curr={rawCurrCount}, " +
                $"delta={rawCurrCount - rawPrevCount}, prevMut={prevSnapshot.MutationVersion}, currMut={currSnapshot.MutationVersion}");
            _lastSnapshotWarningVersion = currSnapshot.CaptureVersion;
        }

        // Interpolate agents that exist in both snapshots
        int interpolateCount = Math.Min(prevCount, currCount);

        for (int i = 0; i < interpolateCount; i++)
        {
            // Interpolate position between prev and curr
            float x = Lerp(prevSnapshot.PositionsX[i], currSnapshot.PositionsX[i], alpha);
            float y = Lerp(prevSnapshot.PositionsY[i], currSnapshot.PositionsY[i], alpha);

            byte group = currSnapshot.Groups[i];
            Color color = GroupColors[group % GroupColors.Length];

            // Draw sense radius (if enabled and not too many agents)
            if (_showSenseRadius && currCount < RendererControls.SenseCircleAgentLimit && _world != null)
            {
                Raylib.DrawCircleLines((int)x, (int)y, _world.Config.SenseRadius,
                    new Color(color.R, color.G, color.B, (byte)30));
            }

            // Draw velocity vector (if enabled)
            if (_showVelocityVectors)
            {
                // Interpolate velocity for smooth vector display
                float vx = Lerp(prevSnapshot.VelocitiesX[i], currSnapshot.VelocitiesX[i], alpha);
                float vy = Lerp(prevSnapshot.VelocitiesY[i], currSnapshot.VelocitiesY[i], alpha);
                float speed = MathF.Sqrt(vx * vx + vy * vy);

                if (speed > 0.1f) // Only draw if moving
                {
                    float lineLength = MathF.Min(speed / 3f, 30f);
                    float dirX = vx / speed;
                    float dirY = vy / speed;

                    Raylib.DrawLineEx(
                        new Vector2(x, y),
                        new Vector2(x + dirX * lineLength, y + dirY * lineLength),
                        1.5f,
                        new Color(color.R, color.G, color.B, (byte)150)
                    );
                }
            }

            // Draw agent circle
            Raylib.DrawCircle((int)x, (int)y, 4f, color);

            // Draw neighbor connections (if enabled and agent is tracked)
            if (_showNeighborConnections && _world != null && i < _world.Count && _trackedAgents.Contains(i))
            {
                DrawNeighborConnections(_world, i, x, y, color);
            }
        }

        // Draw newly spawned agents (if currCount > prevCount) without interpolation
        // Use current snapshot positions directly since they didn't exist in previous snapshot
        for (int i = interpolateCount; i < currCount; i++)
        {
            float x = currSnapshot.PositionsX[i];
            float y = currSnapshot.PositionsY[i];

            byte group = currSnapshot.Groups[i];
            Color color = GroupColors[group % GroupColors.Length];

            // Draw sense radius (if enabled and not too many agents)
            if (_showSenseRadius && currCount < RendererControls.SenseCircleAgentLimit && _world != null)
            {
                Raylib.DrawCircleLines((int)x, (int)y, _world.Config.SenseRadius,
                    new Color(color.R, color.G, color.B, (byte)30));
            }

            // Draw velocity vector (if enabled)
            if (_showVelocityVectors)
            {
                float vx = currSnapshot.VelocitiesX[i];
                float vy = currSnapshot.VelocitiesY[i];
                float speed = MathF.Sqrt(vx * vx + vy * vy);

                if (speed > 0.1f)
                {
                    float lineLength = MathF.Min(speed / 3f, 30f);
                    float dirX = vx / speed;
                    float dirY = vy / speed;

                    Raylib.DrawLineEx(
                        new Vector2(x, y),
                        new Vector2(x + dirX * lineLength, y + dirY * lineLength),
                        1.5f,
                        new Color(color.R, color.G, color.B, (byte)150)
                    );
                }
            }

            // Draw agent circle (with slight visual indicator that it's new - brighter)
            Raylib.DrawCircle((int)x, (int)y, 4f, color);
        }
    }

    private static int GetSafeAgentCount(SimSnapshot snapshot)
    {
        return Math.Min(
            snapshot.AgentCount,
            Math.Min(
                Math.Min(snapshot.PositionsX.Length, snapshot.PositionsY.Length),
                Math.Min(
                    Math.Min(snapshot.VelocitiesX.Length, snapshot.VelocitiesY.Length),
                    snapshot.Groups.Length)));
    }

    /// <summary>
    /// Linear interpolation helper.
    /// </summary>
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static void Render(World world)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        // Draw agents
        DrawAgents(world);

        // Draw UI
        DrawUI(world);

        Raylib.EndDrawing();
    }

    private static void DrawAgents(World world)
    {
        // Get read-only spans for efficiency
        var posX = world.GetPositionsX();
        var posY = world.GetPositionsY();
        var groups = world.GetGroups();

        // Draw each agent with various visualizations
        for (int i = 0; i < world.Count; i++)
        {
            // Skip dead agents
            if (world.State[i].HasFlag(AgentState.Dead))
                continue;

            float x = posX[i];
            float y = posY[i];
            byte group = groups[i];
            Color color = GroupColors[group % GroupColors.Length];

            // Draw sense radius (if enabled and not too many agents)
            if (_showSenseRadius && world.Count < RendererControls.SenseCircleAgentLimit)
            {
                Raylib.DrawCircleLines((int)x, (int)y, world.Config.SenseRadius,
                    new Color(color.R, color.G, color.B, (byte)30));
            }

            // Draw velocity vector (if enabled)
            if (_showVelocityVectors)
            {
                float vx = world.Vx[i];
                float vy = world.Vy[i];
                float speed = MathF.Sqrt(vx * vx + vy * vy);

                if (speed > 0.1f) // Only draw if moving
                {
                    // Scale velocity for visibility (divide by 3 to make reasonable length)
                    float lineLength = MathF.Min(speed / 3f, 30f);
                    float dirX = vx / speed;
                    float dirY = vy / speed;

                    Raylib.DrawLineEx(
                        new Vector2(x, y),
                        new Vector2(x + dirX * lineLength, y + dirY * lineLength),
                        1.5f,
                        new Color(color.R, color.G, color.B, (byte)150)
                    );
                }
            }

            // Draw neighbor connections (if enabled and not too many agents)
            if (_showNeighborConnections && world.Count < 500)
            {
                DrawNeighborConnections(world, i, x, y, color);
            }

            // Draw agent as a larger, more visible circle
            Raylib.DrawCircle((int)x, (int)y, 3f, color); // Increased from 2 to 3
        }
    }

    private static void DrawNeighborConnections(World world, int agentIdx, float x, float y, Color color)
    {
        // Query neighbors and draw lines
        var grid = world.Grid;
        float senseRadiusSq = world.Config.SenseRadius * world.Config.SenseRadius;

        Span<int> neighbors = stackalloc int[64]; // Limit for performance
        int neighborCount = grid.Query3x3(x, y, neighbors, 64);

        for (int n = 0; n < neighborCount && n < 64; n++)
        {
            int j = neighbors[n];

            if (j == agentIdx || j >= world.Count) continue;
            if (world.State[j].HasFlag(AgentState.Dead)) continue;
            if (world.Group[j] != world.Group[agentIdx]) continue; // Same group only

            float dx = world.X[j] - x;
            float dy = world.Y[j] - y;
            float distSq = dx * dx + dy * dy;

            if (distSq > 0.01f && distSq < senseRadiusSq)
            {
                Raylib.DrawLineEx(
                    new Vector2(x, y),
                    new Vector2(world.X[j], world.Y[j]),
                    0.5f,
                    new Color(color.R, color.G, color.B, (byte)40)
                );
            }
        }
    }

    private static void DrawUI(World world)
    {
        const int padding = 10;
        int y = padding;
        const int lineHeight = 18;
        var config = world.Config;

        // Get stats
        var stats = world.GetStats();
        int fps = Raylib.GetFPS();

        // Larger background panel for parameters
        Raylib.DrawRectangle(0, 0, 450, 650, new Color(0, 0, 0, 200));

        // Title
        DrawText($"SwarmingLilMen - Dynamic Parameter Adjustment", padding, y, 20, Color.White);
        y += lineHeight + 5;

        // Stats
        DrawText($"FPS: {fps} | Agents: {stats.AliveAgents} | Speed: {stats.AverageSpeed:F1}/{config.MaxSpeed:F0}",
            padding, y, 14, fps >= 60 ? Color.Green : Color.Yellow);
        y += lineHeight + 5;

        // === ADJUSTABLE PARAMETERS ===
        DrawText(Controls.ParameterHeading(RendererMode.Legacy), padding, y, 14, Color.SkyBlue);
        y += lineHeight;
        DrawText(Controls.ParameterHint(RendererMode.Legacy) + (_fineAdjustment ? " [FINE]" : ""), padding, y, 11, Color.LightGray);
        y += lineHeight;

        var parameterDefinitions = Controls.GetParameters(RendererMode.Legacy);
        for (int i = 0; i < parameterDefinitions.Count; i++)
        {
            var param = parameterDefinitions[i];
            float value = GetParameterValue(i);
            bool isSelected = i == _selectedParameter;

            Color textColor = isSelected ? Color.Yellow : Color.White;
            Color valueColor = isSelected ? Color.Gold : Color.Gray;
            string marker = isSelected ? ">" : " ";

            string valueStr = param.Control is RendererControlId.SeparationWeight or RendererControlId.AlignmentWeight or RendererControlId.CohesionWeight or RendererControlId.Friction
                ? $"{value:F6}" : $"{value:F1}";

            string keyLabel = Controls.GetBinding(RendererMode.Legacy, param.Control).KeyLabel;
            DrawText($"{marker}[{keyLabel}] {param.DisplayName(RendererMode.Legacy)}:", padding, y, 14, textColor);
            DrawText($"{valueStr}", padding + 250, y, 14, valueColor);

            // Show adjustment hints for selected parameter
            if (isSelected)
            {
                float step = _fineAdjustment ? param.FineStepSize : param.StepSize;
                DrawText($"[{step:F6}]", padding + 360, y, 12, new Color(100, 100, 100, 255));
            }

            y += lineHeight - 2;
        }
        DrawText(Controls.FrictionStatus(config.SpeedModel), padding, y, 12,
            config.SpeedModel == SpeedModel.Damped ? Color.LightGray : Color.Orange);
        y += lineHeight;

        y += 8;

        // === PRESETS ===
        DrawText($"PRESETS ({Controls.GetPreset(0).KeyLabel}-{Controls.GetPreset(Presets.Length - 1).KeyLabel}):", padding, y, 14, Color.SkyBlue);
        y += lineHeight;

        for (int i = 0; i < Math.Min(5, Presets.Length); i++)
        {
            DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, Controls.GetPreset(i).Id)}", padding, y, 12, Color.LightGray);
            y += lineHeight - 4;
        }

        y += 8;

        // === VISUALIZATION ===
        DrawText("VISUALIZATION:", padding, y, 14, Color.SkyBlue);
        y += lineHeight;
        DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.VelocityVectors)}: {(_showVelocityVectors ? "ON" : "OFF")}", padding, y, 12,
            _showVelocityVectors ? Color.Green : Color.Gray);
        y += lineHeight - 4;
        DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.SenseCircles)}: {(_showSenseRadius ? "ON" : "OFF")}", padding, y, 12,
            _showSenseRadius ? Color.Green : Color.Gray);
        y += lineHeight - 4;
        DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.NeighborLinks)}: {(_showNeighborConnections ? "ON" : "OFF")}", padding, y, 12,
            _showNeighborConnections ? Color.Green : Color.Gray);
        y += lineHeight - 2;
        DrawText($"  Field of View: {_world?.Config.FieldOfView:F0}° | Sense Radius: {_world?.Config.SenseRadius:F0}", padding, y, 12, Color.LightGray);
        y += lineHeight - 4;
        DrawText($"  Max Neighbors: {_world?.Config.MaxNeighbors}", padding, y, 12, Color.LightGray);
        y += lineHeight - 2;

        y += 8;

        // === CONTROLS ===
        DrawText("CONTROLS:", padding, y, 14, Color.SkyBlue);
        y += lineHeight;
        DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.SpawnGroup0)} | {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.SpawnGroup1)}", padding, y, 12, Color.LightGray);
        y += lineHeight - 4;
        DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.SpawnRandom)}", padding, y, 12, Color.LightGray);
        y += lineHeight - 4;
        DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.Reset)} | {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.Shake)} | {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.ExportCsv)}", padding, y, 12, Color.LightGray);
        y += lineHeight - 4;
        DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.PrintConfiguration)} | {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.Quit)}", padding, y, 12, Color.LightGray);
        y += lineHeight - 4;
        DrawText($"  Hold {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.FineAdjustment)}", padding, y, 12, Color.Gold);
        y += lineHeight - 4;
        DrawText($"  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.Help)} [{(_showHelpOverlay ? "ON" : "OFF")}] | {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.DebugOverlay)}", padding, y, 12, Color.LightGray);

        // === CURRENT VALUES DISPLAY (right side panel) ===
        int rightX = WindowWidth - 300;
        int rightY = padding;
        Raylib.DrawRectangle(rightX - 10, 0, 310, 200, new Color(0, 0, 0, 180));

        DrawText("CURRENT VALUES", rightX, rightY, 14, Color.SkyBlue);
        rightY += lineHeight;

        // Show key metrics
        float maxForce = 0;
        for (int i = 0; i < Math.Min(world.Count, 100); i++)  // Sample first 100 for performance
        {
            if (world.State[i].HasFlag(AgentState.Dead)) continue;
            float force = MathF.Sqrt(world.Fx[i] * world.Fx[i] + world.Fy[i] * world.Fy[i]);
            maxForce = MathF.Max(maxForce, force);
        }

        DrawText($"Max Force: {maxForce:F1}", rightX, rightY, 12,
            maxForce > 500 ? Color.Red : Color.White);
        rightY += lineHeight - 4;

        DrawText($"Avg Speed: {stats.AverageSpeed:F2}", rightX, rightY, 12,
            stats.AverageSpeed < 1 ? Color.Orange : Color.White);
        rightY += lineHeight - 4;

        // Show selected parameter in detail
        if (_selectedParameter >= 0 && _selectedParameter < parameterDefinitions.Count)
        {
            var param = parameterDefinitions[_selectedParameter];
            rightY += 10;
            DrawText($"Selected: {param.DisplayName(RendererMode.Legacy)}", rightX, rightY, 12, Color.Yellow);
            rightY += lineHeight - 4;
            DrawText($"Range: [{param.MinValue:F2} - {param.MaxValue:F2}]", rightX, rightY, 12, Color.Gray);
        }
    }

    private static void DrawDebugOverlay(SimSnapshot prevSnapshot, SimSnapshot currSnapshot, float alpha, bool interpolating)
    {
        if (_runner == null)
            return;

        const int panelWidth = 360;
        const int panelHeight = 120;
        int x = 10;
        int y = WindowHeight - panelHeight - 10;

        Raylib.DrawRectangle(x, y, panelWidth, panelHeight, new Color(0, 0, 0, 180));

        int line = 0;
        void Write(string text, Color color)
        {
            DrawText(text, x + 8, y + 8 + line * 16, 14, color);
            line++;
        }

        Write($"Prev #{prevSnapshot.CaptureVersion} ({prevSnapshot.AgentCount} agents)", Color.LightGray);
        Write($"Curr #{currSnapshot.CaptureVersion} ({currSnapshot.AgentCount} agents)", Color.LightGray);
        Write($"Count delta: {currSnapshot.AgentCount - prevSnapshot.AgentCount}", Color.Yellow);
        Write($"Mutation: prev {prevSnapshot.MutationVersion}, curr {currSnapshot.MutationVersion}", Color.SkyBlue);
        Write($"Alpha: {alpha:F2} | Mode: {(interpolating ? "Interpolating" : "Direct")}", interpolating ? Color.Green : Color.Orange);
        Write($"Accumulator: {_runner.Accumulator:F4} / {_runner.FixedDeltaTime:F4}", Color.LightGray);
    }

    private static void DrawHelpOverlay(RendererMode mode, SpeedModel speedModel)
    {
        IReadOnlyList<RendererHelpLine> lines = Controls.GetHelpLines(mode, speedModel);
        int width = 900;
        int height = 54 + lines.Count * 18;
        int x = (WindowWidth - width) / 2;
        int y = 40;

        Raylib.DrawRectangle(x, y, width, height, new Color(0, 0, 0, 230));
        Raylib.DrawRectangleLines(x, y, width, height, Color.SkyBlue);

        string helpKey = Controls.GetBinding(mode, RendererControlId.Help).KeyLabel;
        DrawText($"{mode} controls (press {helpKey} to hide)", x + 16, y + 16, 18, Color.Yellow);
        for (int i = 0; i < lines.Count; i++)
            DrawText(lines[i].Text, x + 16, y + 44 + i * 18, 14,
                lines[i].IsHeading ? Color.SkyBlue : Color.LightGray);
    }

    private static void PrintStartupBanner(int agentCount)
    {
        var config = BuildCurrentConfig();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  SwarmingLilMen - High-Performance Swarm Simulation v{AppVersion}  ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  Starting with {agentCount} agents across 4 groups".PadRight(61) + "║");
        Console.WriteLine($"║  Fixed timestep: {config.FixedDeltaTime:F4}s | Interpolation: Enabled".PadRight(61) + "║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine(($"║  {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.Help)} | {Controls.ShortLabel(RendererMode.Legacy, RendererControlId.DebugOverlay)}").PadRight(61) + "║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    }

    private static void PrintVersionInfo()
    {
        Console.WriteLine($"SwarmingLilMen v{AppVersion}");
        Console.WriteLine(".NET Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
    }

    private static void PrintPresetList() => Console.Write(FormatPresetList());

    /// <summary>
    /// Renders the preset listing exactly as <c>--list-presets</c> prints it, so documentation
    /// checks can compare against the same text without capturing console output.
    /// </summary>
    internal static string FormatPresetList()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Available presets:");
        foreach (var preset in Presets)
        {
            sb.AppendLine($"  {preset.Id,-15} - {preset.Name} :: {preset.Description}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reports whether <paramref name="name"/> resolves through the same preset lookup
    /// <see cref="Main"/> uses, so tests assert the real matching rules rather than a copy.
    /// </summary>
    internal static bool IsRegisteredPreset(string? name) => TryGetPreset(name, out _);

    private static bool TryGetPreset(string? name, out Preset preset)
    {
        preset = null!;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var match = Presets.FirstOrDefault(p =>
            string.Equals(p.Id, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return false;

        preset = match;
        return true;
    }

    private static void RunCanonicalMode()
    {
        Console.WriteLine("Launching canonical single-group simulation.");
        Raylib.InitWindow(WindowWidth, WindowHeight, $"SwarmingLilMen - Canonical Mode");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(Controls.ExitKey(RendererMode.Canonical));

        var settings = BuildCanonicalWorldSettings();
        var canonicalWorld = CreateCanonicalWorld(settings, _initialAgentCount);
        // The canonical HUD explicitly opts into researcher telemetry and owns its buffers.
        var telemetry = CaptureCanonicalTelemetry(canonicalWorld, null);
        float accumulator = 0f;
        bool showHelp = true;
        float consoleTimer = 0f;
        float selectionTimer = 0f;
        SelectCanonicalTracked(canonicalWorld, _canonicalTrackedAgents);
        var neighborBuffer = new int[128];
        var neighborWeightBuffer = new float[128];

        while (!Raylib.WindowShouldClose())
        {
            RendererInputSnapshot input = Controls.Capture(RendererMode.Canonical, RaylibRendererInput.Instance);
            if (input.IsActive(RendererControlId.Quit))
            {
                break;
            }

            bool parametersChanged = ProcessCanonicalParameters(input);
            if (parametersChanged)
            {
                settings = BuildCanonicalWorldSettings();
                canonicalWorld = RecreateCanonicalWorld(canonicalWorld, settings);
                SelectCanonicalTracked(canonicalWorld, _canonicalTrackedAgents);
                _overlaySubjectIndex = 0;
                accumulator = 0f;
            }

            if (input.IsActive(RendererControlId.Reset))
            {
                var resetWorld = CreateCanonicalWorld(settings, _initialAgentCount);
                CanonicalInspectionOverlay.CopyRuleSwitches(canonicalWorld, resetWorld);
                canonicalWorld = resetWorld;
                SelectCanonicalTracked(canonicalWorld, _canonicalTrackedAgents);
                accumulator = 0f;
            }

            CanonicalInspectionOverlay.ApplyRuleSwitches(input, canonicalWorld);

            if (input.IsActive(RendererControlId.Help))
            {
                showHelp = !showHelp;
            }

            if (input.IsActive(RendererControlId.Interactions))
            {
                _canonicalOverlayVisible = !_canonicalOverlayVisible;
            }

            if (input.IsActive(RendererControlId.NextSubject) && canonicalWorld.Count > 0)
            {
                _overlaySubjectIndex = (_overlaySubjectIndex + 1) % canonicalWorld.Count;
            }

            float frameTime = Raylib.GetFrameTime();
            accumulator += frameTime;
            while (accumulator >= settings.FixedDeltaTime)
            {
                canonicalWorld.Step(settings.FixedDeltaTime);
                accumulator -= settings.FixedDeltaTime;
            }
            telemetry = CaptureCanonicalTelemetry(canonicalWorld, telemetry);

            float speedSum = 0f;
            foreach (var boid in canonicalWorld.Boids)
            {
                speedSum += boid.Velocity.Length;
            }

            int count = canonicalWorld.Count;
            float avgSpeed = count > 0 ? speedSum / count : 0f;

            selectionTimer += frameTime;
            if (selectionTimer >= CanonicalSelectionInterval)
            {
                selectionTimer -= CanonicalSelectionInterval;
                SelectCanonicalTracked(canonicalWorld, _canonicalTrackedAgents);
            }

            consoleTimer += frameTime;
            if (consoleTimer >= CanonicalConsoleInterval)
            {
                consoleTimer -= CanonicalConsoleInterval;
                LogCanonicalStatus(canonicalWorld, avgSpeed);
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            foreach (var boid in canonicalWorld.Boids)
            {
                var pos = new Vector2(boid.Position.X, boid.Position.Y);
                Raylib.DrawCircleV(pos, 3f, Color.Lime);
            }

            if (_canonicalOverlayVisible && canonicalWorld.Count > 0 && _overlaySubjectIndex >= 0)
            {
                CanonicalInspectionOverlay.Draw(canonicalWorld, _overlaySubjectIndex, neighborBuffer, neighborWeightBuffer);
            }

            Raylib.DrawText($"Canonical Mode - {count} agents (single group)", 10, 8, 18, Color.Lime);
            Raylib.DrawText($"Target: {settings.TargetSpeed:F1} | Avg speed: {avgSpeed:F1}", 10, 32, 14, Color.SkyBlue);
            Raylib.DrawText($"Sense radius: {settings.SenseRadius:F1} | Separation radius: {settings.SeparationRadius:F1}", 10, 52, 14, Color.SkyBlue);
            Raylib.DrawText($"Max force: {settings.MaxForce:F2} | FOV: {settings.FieldOfView:F0}°", 10, 70, 14, Color.SkyBlue);
            var neighborStats = telemetry.NeighborCountStats;
            Raylib.DrawText($"Neighbors avg {neighborStats.Avg:F1} (min {neighborStats.Min}, max {neighborStats.Max})", 10, 88, 14, Color.SkyBlue);
            Raylib.DrawText($"Weights avg {telemetry.AverageNeighborWeight:F2} | Sep avg {telemetry.AverageSeparationMagnitude:F2}", 10, 106, 14, Color.SkyBlue);
            Raylib.DrawText($"Align avg {telemetry.AverageAlignmentMagnitude:F2} | Coh avg {telemetry.AverageCohesionMagnitude:F2}", 10, 124, 14, Color.SkyBlue);
            Raylib.DrawText($"Distance avg {telemetry.AverageNeighborDistance:F1} (min {telemetry.MinNeighborDistance:F1}, max {telemetry.MaxNeighborDistance:F1})", 10, 142, 14, Color.SkyBlue);
            Raylib.DrawText($"Separation priority: {(telemetry.SeparationPriorityTriggered ? "ACTIVE" : "idle")}", 10, 160, 14, telemetry.SeparationPriorityTriggered ? Color.Orange : Color.Gray);
            CanonicalInspectionOverlay.DrawRuleSwitches(canonicalWorld, 10, 186);

            Raylib.DrawText($"{Controls.ShortLabel(RendererMode.Canonical, RendererControlId.Interactions)} [{(_canonicalOverlayVisible ? "ON" : "OFF")}] | {Controls.ShortLabel(RendererMode.Canonical, RendererControlId.NextSubject)}", 10, WindowHeight - 68, 14, Color.Gray);
            Raylib.DrawText(Controls.ParameterHeading(RendererMode.Canonical) + " | " + Controls.ParameterHint(RendererMode.Canonical), 10, WindowHeight - 48, 12, Color.Gray);
            Raylib.DrawText($"{Controls.ShortLabel(RendererMode.Canonical, RendererControlId.Reset)} | {Controls.ShortLabel(RendererMode.Canonical, RendererControlId.Help)} | {Controls.ShortLabel(RendererMode.Canonical, RendererControlId.Quit)}", 10, WindowHeight - 24, 14, Color.Gray);
            if (showHelp)
            {
                DrawHelpOverlay(RendererMode.Canonical, SpeedModel.ConstantSpeed);
            }

            Raylib.DrawFPS(WindowWidth - 90, 10);
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    /// <summary>
    /// Captures the completed tick into renderer-owned buffers. Respawns and parameter changes
    /// reuse the allocation when it fits; a larger replacement world grows it once. A new world
    /// is captured even before its first tick so the HUD cannot retain the previous world's data.
    /// </summary>
    internal static CanonicalTelemetry CaptureCanonicalTelemetry(
        CanonicalWorld world,
        CanonicalTelemetry? reusableTelemetry)
    {
        ArgumentNullException.ThrowIfNull(world);
        int capacity = Math.Max(1, world.Settings.InitialCapacity);
        if (reusableTelemetry is null || reusableTelemetry.Capacity < capacity)
            reusableTelemetry = new CanonicalTelemetry(capacity);
        world.CaptureTelemetryInto(reusableTelemetry);
        return reusableTelemetry;
    }


    /// <summary>
    /// Gets the query capacity the interaction overlay must use so it matches the simulation's
    /// per-boid candidate CAP. The overlay owns a fixed 128-entry buffer, but
    /// <see cref="CanonicalWorld.Step"/> caps candidates at
    /// <see cref="CanonicalWorld.EffectiveMaxNeighbors"/> (16 for the renderer's mapped config).
    /// Querying with the wider buffer would draw neighbours steering discarded and would report
    /// no truncation in exactly the cases where the simulation did truncate.
    /// </summary>
    /// <remarks>
    /// Inspection requeries the last completed tick's inputs, so its links and force diagnostics
    /// refer to the same pre-integration positions.
    /// </remarks>
    /// <param name="bufferLength">Entries available in the overlay's own buffers.</param>
    /// <param name="effectiveMaxNeighbors">The simulation's own per-boid candidate cap.</param>
    /// <returns>The smaller of the two, never negative.</returns>
    internal static int ComputeOverlayQueryCapacity(int bufferLength, int effectiveMaxNeighbors)
        => Math.Max(0, Math.Min(bufferLength, effectiveMaxNeighbors));


    private static bool ProcessCanonicalParameters(RendererInputSnapshot input)
    {
        _fineAdjustment = input.IsActive(RendererControlId.FineAdjustment);
        bool changed = Controls.ApplyParameters(input, ref _selectedParameter, GetCanonicalParameterValue, SetCanonicalParameterValue, Console.Out);

        if (input.IsActive(RendererControlId.PrintConfiguration))
            Controls.PrintParameters(RendererMode.Canonical, GetCanonicalParameterValue, Console.Out, SpeedModel.ConstantSpeed);

        for (int i = 0; i < Presets.Length; i++)
            if (input.IsActive(Controls.GetPreset(i).Id)) { ApplyPreset(Presets[i]); changed = true; }

        return changed;
    }

    private static void SelectCanonicalTracked(CanonicalWorld world, int[] tracked)
    {
        if (tracked.Length == 0)
            return;

        int count = world.Count;
        if (count == 0)
        {
            for (int i = 0; i < tracked.Length; i++)
                tracked[i] = -1;
            return;
        }

        var used = new HashSet<int>();
        for (int i = 0; i < tracked.Length; i++)
        {
            if (used.Count >= count)
            {
                tracked[i] = -1;
                continue;
            }

            int candidate;
            int attempts = 0;
            do
            {
                candidate = _canonicalRandom.Next(count);
                attempts++;
            } while (used.Contains(candidate) && attempts < 16);

            used.Add(candidate);
            tracked[i] = candidate;
        }
    }

    private static void LogCanonicalStatus(CanonicalWorld world, float avgSpeed)
    {
        var instrumentation = world.Instrumentation;
        var neighborStats = instrumentation.NeighborCountStats;
        Console.WriteLine($"[Canonical] Tick {world.TickCount}: AvgSpeed={avgSpeed:F2}, Neighbors avg={neighborStats.Avg:F1} (min {neighborStats.Min}, max {neighborStats.Max}), WeightAvg={instrumentation.AverageNeighborWeight:F2}");

        foreach (int idx in _canonicalTrackedAgents)
        {
            if (idx < 0 || idx >= world.Count)
                continue;

            var boid = world.Boids[idx];
            if (!instrumentation.TryGetMetrics(idx, out var metrics))
                continue;

            Console.WriteLine($"  Agent #{idx}: Pos=({boid.Position.X:F1},{boid.Position.Y:F1}) Speed={boid.Velocity.Length:F2} Neighbors={metrics.NeighborCount} WeightSum={metrics.NeighborWeightSum:F2} Sep={metrics.SeparationMagnitude:F2} Align={metrics.AlignmentMagnitude:F2} Coh={metrics.CohesionMagnitude:F2}");
        }
    }

    internal static CanonicalWorld RecreateCanonicalWorld(CanonicalWorld oldWorld, CanonicalWorldSettings settings)
    {
        var snapshots = new List<(Vec2 position, Vec2 velocity)>(oldWorld.Count);
        foreach (var boid in oldWorld.Boids)
        {
            snapshots.Add((boid.Position, boid.Velocity));
        }

        var newWorld = new CanonicalWorld(settings, new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        CanonicalInspectionOverlay.CopyRuleSwitches(oldWorld, newWorld);
        foreach (var (position, velocity) in snapshots)
        {
            if (newWorld.TryAddBoid(position, velocity))
            {
                newWorld.SetVelocity(newWorld.Count - 1, velocity);
            }
        }

        return newWorld;
    }

    private static CanonicalWorldSettings BuildCanonicalWorldSettings()
    {
        return BuildCanonicalWorldSettings(BuildCurrentConfig(), _initialAgentCount);
    }

    internal static CanonicalWorldSettings BuildCanonicalWorldSettings(
        SimConfig config,
        int initialAgentCount)
        => CanonicalWorldSettings.FromSimConfig(config, Math.Max(initialAgentCount * 2, 1024));

    internal static CanonicalWorld CreateCanonicalWorld(
        CanonicalWorldSettings settings,
        int agentCount)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var canonicalWorld = new CanonicalWorld(
            settings,
            new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight));
        var rng = new Rng(settings.Seed);
        float twoPi = 2f * MathF.PI;

        for (int i = 0; i < agentCount; i++)
        {
            float x = rng.NextFloat(0f, settings.WorldWidth);
            float y = rng.NextFloat(0f, settings.WorldHeight);
            float angle = rng.NextFloat(0f, twoPi);
            var direction = new Vec2(MathF.Cos(angle), MathF.Sin(angle));
            var velocity = direction.WithLength(settings.TargetSpeed);
            canonicalWorld.TryAddBoid(new Vec2(x, y), velocity);
        }

        return canonicalWorld;
    }

    private static void WarnIfInvalidConfig(SimConfig config, string source)
    {
        var errors = config.Validate();
        if (errors.Count == 0)
            return;

        Console.WriteLine($"[Config Warning] {source}: {string.Join("; ", errors)}");
    }

    private static void DrawText(string text, int x, int y, int fontSize, Color color)
    {
        Raylib.DrawText(text, x, y, fontSize, color);
    }

    private static void PrintDiagnostics(World world)
    {
        var stats = world.GetStats();

        // Compute aggregate statistics
        float minSpeed = float.MaxValue;
        float maxSpeed = 0f;
        float minForce = float.MaxValue;
        float maxForce = 0f;
        int totalNeighbors = 0;
        int agentsWithFewNeighbors = 0; // < 5 neighbors
        int agentsWithManyNeighbors = 0; // > 30 neighbors

        for (int i = 0; i < world.Count; i++)
        {
            if (world.State[i].HasFlag(AgentState.Dead)) continue;

            float speed = MathF.Sqrt(world.Vx[i] * world.Vx[i] + world.Vy[i] * world.Vy[i]);
            float force = MathF.Sqrt(world.Fx[i] * world.Fx[i] + world.Fy[i] * world.Fy[i]);

            minSpeed = MathF.Min(minSpeed, speed);
            maxSpeed = MathF.Max(maxSpeed, speed);
            minForce = MathF.Min(minForce, force);
            maxForce = MathF.Max(maxForce, force);

            // Count neighbors
            int neighbors = 0;
            for (int j = 0; j < world.Count; j++)
            {
                if (i == j || world.State[j].HasFlag(AgentState.Dead)) continue;
                if (world.Group[i] != world.Group[j]) continue;

                float dx = world.X[j] - world.X[i];
                float dy = world.Y[j] - world.Y[i];
                float distSq = dx * dx + dy * dy;
                if (distSq < world.Config.SenseRadius * world.Config.SenseRadius)
                    neighbors++;
            }
            totalNeighbors += neighbors;

            if (neighbors < 5) agentsWithFewNeighbors++;
            if (neighbors > 30) agentsWithManyNeighbors++;
        }

        float avgNeighbors = stats.AliveAgents > 0 ? (float)totalNeighbors / stats.AliveAgents : 0f;

        Console.WriteLine($"═══ [T={world.TickCount:D5}] ═══");
        Console.WriteLine($"Agents: {stats.AliveAgents}  |  Avg Speed: {stats.AverageSpeed:F1}/{world.Config.MaxSpeed:F0}  |  Range: [{minSpeed:F1}, {maxSpeed:F1}]");
        Console.WriteLine($"Forces: Avg N/A  |  Range: [{minForce:F1}, {maxForce:F1}]");
        Console.WriteLine($"Neighbors: Avg {avgNeighbors:F1}  |  Few(<5): {agentsWithFewNeighbors}  |  Many(>30): {agentsWithManyNeighbors}");

        // Pick 5 random agents to track if we haven't yet
        if (_trackedAgents.Length == 0 && world.Count > 0)
        {
            int numToTrack = Math.Min(5, world.Count);
            _trackedAgents = new int[numToTrack];
            for (int i = 0; i < numToTrack; i++)
            {
                _trackedAgents[i] = _random.Next(0, world.Count);
            }
            Console.WriteLine($"Tracking agents: {string.Join(", ", _trackedAgents)}");
        }

        // Show tracked agent details
        if (_trackedAgents.Length > 0)
        {
            Console.WriteLine("Tracked Agents:");
            foreach (int idx in _trackedAgents)
            {
                if (idx >= world.Count) continue;

                float speed = MathF.Sqrt(world.Vx[idx] * world.Vx[idx] + world.Vy[idx] * world.Vy[idx]);
                float force = MathF.Sqrt(world.Fx[idx] * world.Fx[idx] + world.Fy[idx] * world.Fy[idx]);

                // Count neighbors for this agent
                int neighbors = 0;
                for (int j = 0; j < world.Count; j++)
                {
                    if (idx == j || world.State[j].HasFlag(AgentState.Dead)) continue;
                    if (world.Group[idx] != world.Group[j]) continue;

                    float dx = world.X[j] - world.X[idx];
                    float dy = world.Y[j] - world.Y[idx];
                    float distSq = dx * dx + dy * dy;
                    if (distSq < world.Config.SenseRadius * world.Config.SenseRadius)
                        neighbors++;
                }

                Console.WriteLine($"  #{idx:D4}: Spd={speed:F1} Frc={force:F1} Nbr={neighbors:D2} Pos=({world.X[idx]:F0},{world.Y[idx]:F0}) Grp={world.Group[idx]}");
            }
        }

        // Detect anomalies
        if (maxForce > 500f)
            Console.WriteLine($"⚠️  HIGH FORCE DETECTED: {maxForce:F0} (agents too close!)");
        if (stats.AverageSpeed < 5f && stats.AliveAgents > 100)
            Console.WriteLine($"⚠️  LOW ACTIVITY: Avg speed {stats.AverageSpeed:F1} (agents in static equilibrium)");
        if (agentsWithManyNeighbors > stats.AliveAgents * 0.5)
            Console.WriteLine($"⚠️  OVER-CLUSTERING: {agentsWithManyNeighbors}/{stats.AliveAgents} agents have >30 neighbors");

        Console.WriteLine();
    }

    private static void ExportCSV(World world)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"swarm_snapshot_{timestamp}_T{world.TickCount:D6}.csv";

        using (var writer = new StreamWriter(filename))
        {
            // Header
            writer.WriteLine("AgentID,Group,X,Y,Vx,Vy,Speed,Energy,Health,Age,State");

            // Data rows
            for (int i = 0; i < world.Count; i++)
            {
                if (world.State[i].HasFlag(AgentState.Dead)) continue;

                float speed = MathF.Sqrt(world.Vx[i] * world.Vx[i] + world.Vy[i] * world.Vy[i]);

                writer.WriteLine($"{i}," +
                    $"{world.Group[i]}," +
                    $"{world.X[i]:F2}," +
                    $"{world.Y[i]:F2}," +
                    $"{world.Vx[i]:F2}," +
                    $"{world.Vy[i]:F2}," +
                    $"{speed:F2}," +
                    $"{world.Energy[i]:F2}," +
                    $"{world.Health[i]:F2}," +
                    $"{world.Age[i]:F2}," +
                    $"{world.State[i]}");
            }
        }

        Console.WriteLine($"📊 Exported {world.Count} agents to {filename}");
    }
}
