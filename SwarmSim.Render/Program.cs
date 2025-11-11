using Raylib_cs;
using SwarmSim.Core;
using System.Numerics;
using System.Linq;

namespace SwarmSim.Render;

internal static class Program
{
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
    private static World? _world = null;  // Store reference to world for recreation
    private static SimulationRunner? _runner = null;  // Fixed-timestep runner

    // Interpolation state for smooth rendering
    private static SimSnapshot? _prevSnapshot = null;
    private static SimSnapshot? _currSnapshot = null;
    private static bool _showDebugOverlay = false;
    private static ulong _lastSnapshotWarningVersion = 0;

    // Parameter values (mutable copies) - FIXED FOR PROPER EQUILIBRIUM
    private static float _separationWeight = 5.0f;    // Was 0.05 (100x stronger)
    private static float _alignmentWeight = 2.5f;     // Was 0.05 (50x stronger)
    private static float _cohesionWeight = 0.5f;      // Was 0.0005 (1000x stronger)
    private static float _separationRadius = 30f;     // Was 15
    private static float _senseRadius = 100f;         // Was 60
    private static float _maxSpeed = 10f;             // Was 6
    private static float _friction = 1f;           // Was 0.99 (CRITICAL FIX!)

    // Parameter info for display and adjustment
    private struct AdjustableParameter
    {
        public string Name;
        public string Key;
        public float MinValue;
        public float MaxValue;
        public float StepSize;
        public float FineStepSize;
        public Func<float> GetValue;
        public Action<float> SetValue;
    }

    private static readonly AdjustableParameter[] Parameters =
    [
        new() { Name = "Separation Weight", Key = "1", MinValue = 0f, MaxValue = 500f, StepSize = 10f, FineStepSize = 0.01f,
            GetValue = () => _separationWeight, SetValue = v => _separationWeight = v },
        new() { Name = "Alignment Weight", Key = "2", MinValue = 0f, MaxValue = 500f, StepSize = 10f, FineStepSize = 0.01f,
            GetValue = () => _alignmentWeight, SetValue = v => _alignmentWeight = v },
        new() { Name = "Cohesion Weight", Key = "3", MinValue = 0f, MaxValue = 10f, StepSize = 0.1f, FineStepSize = 0.0001f,
            GetValue = () => _cohesionWeight, SetValue = v => _cohesionWeight = v },
        new() { Name = "Separation Radius", Key = "4", MinValue = 5f, MaxValue = 200f, StepSize = 5f, FineStepSize = 1f,
            GetValue = () => _separationRadius, SetValue = v => _separationRadius = v },
        new() { Name = "Sense Radius", Key = "5", MinValue = 10f, MaxValue = 300f, StepSize = 10f, FineStepSize = 2f,
            GetValue = () => _senseRadius, SetValue = v => _senseRadius = v },
        new() { Name = "Max Speed", Key = "6", MinValue = 1f, MaxValue = 500f, StepSize = 10f, FineStepSize = 1f,
            GetValue = () => _maxSpeed, SetValue = v => _maxSpeed = v },
        new() { Name = "Friction", Key = "7", MinValue = 0.8f, MaxValue = 1.0f, StepSize = 0.01f, FineStepSize = 0.001f,
            GetValue = () => _friction, SetValue = v => _friction = v },
    ];

    // Preset configurations
    private struct Preset
    {
        public string Name;
        public float SeparationWeight;
        public float AlignmentWeight;
        public float CohesionWeight;
        public float SeparationRadius;
        public float SenseRadius;
        public float MaxSpeed;
        public float Friction;
    }

    private static readonly Preset[] Presets =
    [
        new() { Name = "Balanced (Recommended)",
            SeparationWeight = 5.0f, AlignmentWeight = 2.5f, CohesionWeight = 0.5f,
            SeparationRadius = 30f, SenseRadius = 100f, MaxSpeed = 10f, Friction = 0.95f },
        new() { Name = "Strong Separation",
            SeparationWeight = 10f, AlignmentWeight = 2.0f, CohesionWeight = 0.3f,
            SeparationRadius = 40f, SenseRadius = 100f, MaxSpeed = 10f, Friction = 0.95f },
        new() { Name = "Tight Flocking",
            SeparationWeight = 3f, AlignmentWeight = 5f, CohesionWeight = 2f,
            SeparationRadius = 20f, SenseRadius = 80f, MaxSpeed = 10f, Friction = 0.95f },
        new() { Name = "Fast & Loose",
            SeparationWeight = 8f, AlignmentWeight = 4f, CohesionWeight = 0.5f,
            SeparationRadius = 30f, SenseRadius = 120f, MaxSpeed = 15f, Friction = 0.90f },
        new() { Name = "Slow & Cohesive",
            SeparationWeight = 2f, AlignmentWeight = 3f, CohesionWeight = 3f,
            SeparationRadius = 25f, SenseRadius = 80f, MaxSpeed = 5f, Friction = 0.98f },
    ];

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

    private static void Main(string[] args)
    {
        // Check if we should run minimal test
        if (args.Length > 0 && args[0] == "--minimal")
        {
            MinimalTest.Run();
            return;
        }

        // Initialize Raylib
        Raylib.InitWindow(WindowWidth, WindowHeight, WindowTitle);
        Raylib.SetTargetFPS(60);

        // Enable console output for debugging
        Console.WriteLine("=== SwarmingLilMen Renderer Starting ===");
        Console.WriteLine("Dynamic Parameter Adjustment Enabled!");
        Console.WriteLine("Use number keys 1-7 to select parameters");
        Console.WriteLine("Use ↑↓/+- to adjust, hold SHIFT for fine tuning");
        Console.WriteLine("Press F1-F5 for presets\n");

        // Create initial world
        var world = CreateWorldWithCurrentParams();
        _world = world;

        // Spawn initial agents
        SpawnInitialAgents(world);

        // Create fixed-timestep simulation runner
        _runner = new SimulationRunner(world);

        // Initialize snapshots for interpolation
        _currSnapshot = _runner.CaptureSnapshot();
        _prevSnapshot = _currSnapshot;

        // Diagnostic tracking
        int frameCount = 0;

        // Main loop
        while (!Raylib.WindowShouldClose())
        {
            // Handle input
            HandleInput(_world);

            // Get elapsed time since last frame
            float frameTime = Raylib.GetFrameTime();

            // Advance simulation using fixed timestep
            int stepsProcessed = _runner.Advance(frameTime);

            // Update snapshots if simulation stepped
            if (stepsProcessed > 0)
            {
                _prevSnapshot = _currSnapshot;
                _currSnapshot = _runner.CaptureSnapshot();
            }

            // Calculate interpolation alpha (how far between prev and curr snapshot)
            // alpha = (time remaining in accumulator) / (fixed timestep)
            float alpha = (float)(_runner.Accumulator / _runner.FixedDeltaTime);

            // Periodic diagnostic output (every 2 seconds)
            frameCount++;
            if (frameCount % 120 == 0) // Every 2 seconds at 60 FPS
            {
                PrintDiagnostics(_world);
            }

            // Render with interpolation
            RenderInterpolated(_prevSnapshot!, _currSnapshot!, alpha);
        }

        Raylib.CloseWindow();
    }

    private static World CreateWorldWithCurrentParams()
    {
        var config = new SimConfig
        {
            WorldWidth = WindowWidth,
            WorldHeight = WindowHeight,
            BoundaryMode = BoundaryMode.Wrap,
            FixedDeltaTime = 1f / 60f,

            // Use current parameter values
            MaxSpeed = _maxSpeed,
            Friction = _friction,
            SenseRadius = _senseRadius,
            SeparationRadius = _separationRadius,
            SeparationWeight = _separationWeight,
            AlignmentWeight = _alignmentWeight,
            CohesionWeight = _cohesionWeight,

            // Disable combat for flocking demo
            AttackDamage = 0f,
            BaseDrain = 0.1f
        };

        return new World(config, seed: 42);
    }

    private static void SpawnInitialAgents(World world)
    {
        // Spawn agents with moderate spacing for traditional Boids
        // With SenseRadius=60px and slower speeds, groups need to be closer

        float centerX = WindowWidth * 0.5f;
        float centerY = WindowHeight * 0.5f;
        float clusterSpacing = 300f; // Moderate spacing for interaction
        float spawnRadius = 150f;     // Reasonable spawn circles

        // Spawn 100 agents per group (400 total)
        world.SpawnAgentsInCircle(centerX - clusterSpacing, centerY - clusterSpacing, spawnRadius, 100, group: 0);
        world.SpawnAgentsInCircle(centerX + clusterSpacing, centerY - clusterSpacing, spawnRadius, 100, group: 1);
        world.SpawnAgentsInCircle(centerX - clusterSpacing, centerY + clusterSpacing, spawnRadius, 100, group: 2);
        world.SpawnAgentsInCircle(centerX + clusterSpacing, centerY + clusterSpacing, spawnRadius, 100, group: 3);

        // Give agents moderate initial velocity (matching new low max speed)
        var rng = world.Rng;
        for (int i = 0; i < world.Count; i++)
        {
            (float vx, float vy) = rng.NextUnitVector();
            float speed = rng.NextFloat(2f, 4f);  // Small initial speeds for traditional Boids
            world.Vx[i] = vx * speed;
            world.Vy[i] = vy * speed;
        }

        Console.WriteLine($"Spawned {world.Count} agents (100 per group, 400 total)");
        Console.WriteLine($"TRADITIONAL BOIDS: MaxSpeed=6, Friction=0.99");
        Console.WriteLine($"Weights: Sep=0.05, Ali=0.05, Coh=0.0005 (vs old 300/150/10)");
        Console.WriteLine($"Expected: Smooth, continuous flocking without equilibrium");
    }

    private static void HandleInput(World world)
    {
        var config = world.Config;

        // Check for shift key (fine adjustment)
        _fineAdjustment = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);

        // === PARAMETER ADJUSTMENT ===
        // Number keys 1-7: Select parameter to adjust
        for (int i = 0; i < Parameters.Length; i++)
        {
            var keyCode = KeyboardKey.One + i;
            if (Raylib.IsKeyPressed(keyCode))
            {
                _selectedParameter = i;
                var param = Parameters[i];
                float currentValue = param.GetValue();
                Console.WriteLine($"Selected: {param.Name} = {currentValue:F4}");
            }
        }

        // Up/Down arrows or +/- : Adjust selected parameter
        bool increase = Raylib.IsKeyPressed(KeyboardKey.Up) ||
                       Raylib.IsKeyPressed(KeyboardKey.Equal) ||
                       Raylib.IsKeyPressed(KeyboardKey.KpAdd);
        bool decrease = Raylib.IsKeyPressed(KeyboardKey.Down) ||
                       Raylib.IsKeyPressed(KeyboardKey.Minus) ||
                       Raylib.IsKeyPressed(KeyboardKey.KpSubtract);

        if (increase || decrease)
        {
            var param = Parameters[_selectedParameter];
            float currentValue = param.GetValue();
            float step = _fineAdjustment ? param.FineStepSize : param.StepSize;

            if (increase)
                currentValue = Math.Min(currentValue + step, param.MaxValue);
            else
                currentValue = Math.Max(currentValue - step, param.MinValue);

            param.SetValue(currentValue);
            Console.WriteLine($"{param.Name} = {currentValue:F4} (Step: {step:F4})");

            // Recreate world with new parameters
            RecreateWorldWithNewParams(world);
        }

        // === PRESETS (F1-F5) ===
        if (Raylib.IsKeyPressed(KeyboardKey.F1)) { ApplyPreset(Presets[0]); RecreateWorldWithNewParams(world); }
        if (Raylib.IsKeyPressed(KeyboardKey.F2)) { ApplyPreset(Presets[1]); RecreateWorldWithNewParams(world); }
        if (Raylib.IsKeyPressed(KeyboardKey.F3)) { ApplyPreset(Presets[2]); RecreateWorldWithNewParams(world); }
        if (Raylib.IsKeyPressed(KeyboardKey.F4)) { ApplyPreset(Presets[3]); RecreateWorldWithNewParams(world); }
        if (Raylib.IsKeyPressed(KeyboardKey.F5)) { ApplyPreset(Presets[4]); RecreateWorldWithNewParams(world); }

        // P: Print current configuration
        if (Raylib.IsKeyPressed(KeyboardKey.P))
        {
            Console.WriteLine("\n=== Current Configuration ===");
            foreach (var param in Parameters)
            {
                Console.WriteLine($"{param.Name}: {param.GetValue():F4}");
            }
            Console.WriteLine("=============================\n");
        }

        // === EXISTING CONTROLS ===
        // Left click: Spawn agents at mouse position
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = Raylib.GetMousePosition();
            int spawned = world.SpawnAgentsInCircle(mousePos.X, mousePos.Y, 50f, 50, group: 0);
            Console.WriteLine($"Spawned {spawned} agents at mouse position");

            // Immediately update snapshots to prevent blinking/out-of-bounds issues
            if (_runner != null)
            {
                _currSnapshot = _runner.CaptureSnapshot();
                _prevSnapshot = _currSnapshot; // No interpolation for newly spawned agents
            }
        }

        // Right click: Spawn different group
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var mousePos = Raylib.GetMousePosition();
            int spawned = world.SpawnAgentsInCircle(mousePos.X, mousePos.Y, 50f, 50, group: 1);
            Console.WriteLine($"Spawned {spawned} agents (group 1) at mouse position");

            // Immediately update snapshots to prevent blinking/out-of-bounds issues
            if (_runner != null)
            {
                _currSnapshot = _runner.CaptureSnapshot();
                _prevSnapshot = _currSnapshot; // No interpolation for newly spawned agents
            }
        }

        // Space: Spawn random agents
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            for (int i = 0; i < 100; i++)
            {
                world.AddRandomAgent(group: (byte)(i % 4));
            }
            Console.WriteLine("Spawned 100 random agents");

            // Immediately update snapshots to prevent blinking/out-of-bounds issues
            if (_runner != null)
            {
                _currSnapshot = _runner.CaptureSnapshot();
                _prevSnapshot = _currSnapshot; // No interpolation for newly spawned agents
            }
        }

        // R: Reset world
        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            // Clear all agents (mark all as dead and compact)
            for (int i = 0; i < world.Count; i++)
            {
                world.MarkDead(i);
            }
            world.CompactDeadAgents();
            SpawnInitialAgents(world);
            Console.WriteLine("World reset");

            // Immediately update snapshots to prevent blinking/out-of-bounds issues
            if (_runner != null)
            {
                _currSnapshot = _runner.CaptureSnapshot();
                _prevSnapshot = _currSnapshot;
            }
        }

        // V: Toggle velocity vectors
        if (Raylib.IsKeyPressed(KeyboardKey.V))
        {
            _showVelocityVectors = !_showVelocityVectors;
            Console.WriteLine($"Velocity vectors: {(_showVelocityVectors ? "ON" : "OFF")}");
        }

        // S: Toggle sense radius visualization
        if (Raylib.IsKeyPressed(KeyboardKey.S))
        {
            _showSenseRadius = !_showSenseRadius;
            Console.WriteLine($"Sense radius: {(_showSenseRadius ? "ON" : "OFF")}");
        }

        // N: Toggle neighbor connections
        if (Raylib.IsKeyPressed(KeyboardKey.N))
        {
            _showNeighborConnections = !_showNeighborConnections;
            Console.WriteLine($"Neighbor connections: {(_showNeighborConnections ? "ON" : "OFF")}");
        }

        // C: Export CSV snapshot
        if (Raylib.IsKeyPressed(KeyboardKey.C))
        {
            ExportCSV(world);
            Console.WriteLine("Exported CSV snapshot");
        }

        // X: "Shake" - add random velocity to break equilibrium
        if (Raylib.IsKeyPressed(KeyboardKey.X))
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

    private static void ApplyPreset(Preset preset)
    {
        _separationWeight = preset.SeparationWeight;
        _alignmentWeight = preset.AlignmentWeight;
        _cohesionWeight = preset.CohesionWeight;
        _separationRadius = preset.SeparationRadius;
        _senseRadius = preset.SenseRadius;
        _maxSpeed = preset.MaxSpeed;
        _friction = preset.Friction;
        Console.WriteLine($"Applied preset: {preset.Name}");
        Console.WriteLine($"  Weights: Sep={preset.SeparationWeight:F4}, Ali={preset.AlignmentWeight:F4}, Coh={preset.CohesionWeight:F6}");
        Console.WriteLine($"  Radii: Sep={preset.SeparationRadius}, Sense={preset.SenseRadius}");
        Console.WriteLine($"  Physics: Speed={preset.MaxSpeed}, Friction={preset.Friction:F3}");
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
        _currSnapshot = _runner.CaptureSnapshot();
        _prevSnapshot = _currSnapshot;
    }

    /// <summary>
    /// Renders the simulation with interpolation between previous and current snapshots.
    /// </summary>
    private static void RenderInterpolated(SimSnapshot prevSnapshot, SimSnapshot currSnapshot, float alpha)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        // Draw agents with interpolation
        DrawAgentsInterpolated(prevSnapshot, currSnapshot, alpha);

        // Draw UI (uses _world directly for stats)
        if (_world != null)
        {
            DrawUI(_world);
        }

        Raylib.EndDrawing();
    }

    /// <summary>
    /// Draws agents with linear interpolation between previous and current positions.
    /// Handles cases where agent count differs between snapshots (spawning/removal).
    /// </summary>
    private static void DrawAgentsInterpolated(SimSnapshot prevSnapshot, SimSnapshot currSnapshot, float alpha)
    {
        int currCount = currSnapshot.AgentCount;
        int prevCount = prevSnapshot.AgentCount;

        // Defensive check: log significant snapshot mismatches for debugging
        if (Math.Abs(currCount - prevCount) > 100)
        {
            // Large agent count change - log for debugging
            Console.WriteLine($"⚠️  Large snapshot mismatch: prev={prevCount}, curr={currCount}, delta={currCount - prevCount}");
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
            if (_showSenseRadius && currCount < 100 && _world != null)
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
            if (_showNeighborConnections && _trackedAgents.Contains(i) && _world != null)
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
            if (_showSenseRadius && currCount < 100 && _world != null)
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
            if (_showSenseRadius && world.Count < 100)
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
        Raylib.DrawRectangle(0, 0, 450, 580, new Color(0, 0, 0, 200));

        // Title
        DrawText($"SwarmingLilMen - Dynamic Parameter Adjustment", padding, y, 20, Color.White);
        y += lineHeight + 5;

        // Stats
        DrawText($"FPS: {fps} | Agents: {stats.AliveAgents} | Speed: {stats.AverageSpeed:F1}/{config.MaxSpeed:F0}",
            padding, y, 14, fps >= 60 ? Color.Green : Color.Yellow);
        y += lineHeight + 5;

        // === ADJUSTABLE PARAMETERS ===
        DrawText($"PARAMETERS (1-7 to select, ↑↓/+- to adjust{(_fineAdjustment ? " [FINE]" : "")})", padding, y, 14, Color.SkyBlue);
        y += lineHeight;

        for (int i = 0; i < Parameters.Length; i++)
        {
            var param = Parameters[i];
            float value = param.GetValue();
            bool isSelected = i == _selectedParameter;

            Color textColor = isSelected ? Color.Yellow : Color.White;
            Color valueColor = isSelected ? Color.Gold : Color.Gray;
            string marker = isSelected ? "►" : " ";

            string valueStr = param.Name.Contains("Weight") || param.Name.Contains("Friction")
                ? $"{value:F6}" : $"{value:F1}";

            DrawText($"{marker}[{param.Key}] {param.Name}:", padding, y, 14, textColor);
            DrawText($"{valueStr}", padding + 250, y, 14, valueColor);

            // Show adjustment hints for selected parameter
            if (isSelected)
            {
                float step = _fineAdjustment ? param.FineStepSize : param.StepSize;
                DrawText($"[{step:F6}]", padding + 360, y, 12, new Color(100, 100, 100, 255));
            }

            y += lineHeight - 2;
        }

        y += 8;

        // === PRESETS ===
        DrawText("PRESETS (F1-F5):", padding, y, 14, Color.SkyBlue);
        y += lineHeight;

        for (int i = 0; i < Math.Min(5, Presets.Length); i++)
        {
            DrawText($"  F{i + 1}: {Presets[i].Name}", padding, y, 12, Color.LightGray);
            y += lineHeight - 4;
        }

        y += 8;

        // === VISUALIZATION ===
        DrawText("VISUALIZATION:", padding, y, 14, Color.SkyBlue);
        y += lineHeight;
        DrawText($"  [V] Velocity: {(_showVelocityVectors ? "ON" : "OFF")}", padding, y, 12,
            _showVelocityVectors ? Color.Green : Color.Gray);
        y += lineHeight - 4;
        DrawText($"  [S] Sense Radius: {(_showSenseRadius ? "ON" : "OFF")}", padding, y, 12,
            _showSenseRadius ? Color.Green : Color.Gray);
        y += lineHeight - 4;
        DrawText($"  [N] Neighbors: {(_showNeighborConnections ? "ON" : "OFF")}", padding, y, 12,
            _showNeighborConnections ? Color.Green : Color.Gray);
        y += lineHeight - 2;

        y += 8;

        // === CONTROLS ===
        DrawText("CONTROLS:", padding, y, 14, Color.SkyBlue);
        y += lineHeight;
        DrawText("  Click: Spawn | Space: Random", padding, y, 12, Color.LightGray);
        y += lineHeight - 4;
        DrawText("  [R] Reset | [X] Shake | [C] CSV", padding, y, 12, Color.LightGray);
        y += lineHeight - 4;
        DrawText("  [P] Print Config | ESC: Quit", padding, y, 12, Color.LightGray);
        y += lineHeight - 4;
        DrawText("  Hold SHIFT for fine adjustment", padding, y, 12, Color.Gold);

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
        if (_selectedParameter >= 0 && _selectedParameter < Parameters.Length)
        {
            var param = Parameters[_selectedParameter];
            rightY += 10;
            DrawText($"Selected: {param.Name}", rightX, rightY, 12, Color.Yellow);
            rightY += lineHeight - 4;
            DrawText($"Range: [{param.MinValue:F2} - {param.MaxValue:F2}]", rightX, rightY, 12, Color.Gray);
        }
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
