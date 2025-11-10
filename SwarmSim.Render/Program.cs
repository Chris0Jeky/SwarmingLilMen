using Raylib_cs;
using SwarmSim.Core;
using System.Numerics;

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
        // Initialize Raylib
        Raylib.InitWindow(WindowWidth, WindowHeight, WindowTitle);
        Raylib.SetTargetFPS(60);

        // Enable console output for debugging
        Console.WriteLine("=== SwarmingLilMen Renderer Starting ===");
        Console.WriteLine("Press V/S/N to toggle visualizations");
        Console.WriteLine("Watching for flocking behavior...\n");

        // Create world with config tuned for visible flocking
        var config = new SimConfig
        {
            WorldWidth = WindowWidth,
            WorldHeight = WindowHeight,
            BoundaryMode = BoundaryMode.Wrap,
            FixedDeltaTime = 1f / 60f,  // 60 Hz simulation to match render

            // Physics tuned for visible, dynamic movement
            MaxSpeed = 200f,             // Reduced from 300 for more controlled movement
            Friction = 0.98f,            // Increased friction to dampen oscillations

            // Boids settings - Rebalanced to prevent static equilibrium
            SenseRadius = 100f,          // Reduced from 120 (fewer neighbors = less clustering)
            SeparationRadius = 40f,      // INCREASED from 30 (push apart more)
            SeparationWeight = 250.0f,   // INCREASED - dominant force
            AlignmentWeight = 80.0f,     // Reduced - less "locking" to group velocity
            CohesionWeight = 30.0f,      // MUCH REDUCED - less attraction (prevents tight packing)

            // Disable combat for peaceful flocking demo
            AttackDamage = 0f,
            BaseDrain = 0.1f
        };

        var world = new World(config, seed: 42);

        // Spawn initial agents
        SpawnInitialAgents(world);

        // Diagnostic tracking
        int frameCount = 0;

        // Main loop
        while (!Raylib.WindowShouldClose())
        {
            // Handle input
            HandleInput(world);

            // Update simulation (1 tick per frame at 60 Hz)
            world.Tick();

            // Periodic diagnostic output (every 2 seconds)
            frameCount++;
            if (frameCount % 120 == 0) // Every 2 seconds at 60 FPS
            {
                PrintDiagnostics(world, config);
            }

            // Render
            Render(world);
        }

        Raylib.CloseWindow();
    }

    private static void SpawnInitialAgents(World world)
    {
        // Spawn agents SPREAD OUT to prevent initial clustering
        // The problem was: 200 agents in 80px radius = 147 neighbors each!
        // Solution: Much larger spawn areas, farther apart

        float centerX = WindowWidth * 0.5f;
        float centerY = WindowHeight * 0.5f;
        float clusterSpacing = 400f; // MUCH farther apart (was 150)
        float spawnRadius = 200f;     // MUCH larger circles (was 80)

        // Spawn 4 groups in corners, widely spread
        world.SpawnAgentsInCircle(centerX - clusterSpacing, centerY - clusterSpacing, spawnRadius, 200, group: 0);
        world.SpawnAgentsInCircle(centerX + clusterSpacing, centerY - clusterSpacing, spawnRadius, 200, group: 1);
        world.SpawnAgentsInCircle(centerX - clusterSpacing, centerY + clusterSpacing, spawnRadius, 200, group: 2);
        world.SpawnAgentsInCircle(centerX + clusterSpacing, centerY + clusterSpacing, spawnRadius, 200, group: 3);

        // Give agents strong initial velocity to prevent static start
        var rng = world.Rng;
        for (int i = 0; i < world.Count; i++)
        {
            (float vx, float vy) = rng.NextUnitVector();
            float speed = rng.NextFloat(80f, 150f);  // Higher initial speed
            world.Vx[i] = vx * speed;
            world.Vy[i] = vy * speed;
        }

        Console.WriteLine($"Spawned {world.Count} agents in 4 SPREAD OUT groups (spacing={clusterSpacing}, radius={spawnRadius})");
    }

    private static void HandleInput(World world)
    {
        // Left click: Spawn agents at mouse position
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = Raylib.GetMousePosition();
            int spawned = world.SpawnAgentsInCircle(mousePos.X, mousePos.Y, 50f, 50, group: 0);
            Console.WriteLine($"Spawned {spawned} agents at mouse position");
        }

        // Right click: Spawn different group
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var mousePos = Raylib.GetMousePosition();
            int spawned = world.SpawnAgentsInCircle(mousePos.X, mousePos.Y, 50f, 50, group: 1);
            Console.WriteLine($"Spawned {spawned} agents (group 1) at mouse position");
        }

        // Space: Spawn random agents
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            for (int i = 0; i < 100; i++)
            {
                world.AddRandomAgent(group: (byte)(i % 4));
            }
            Console.WriteLine("Spawned 100 random agents");
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

        // ESC: Quit (handled by WindowShouldClose)
    }

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
        const int lineHeight = 20;

        // Get stats
        var stats = world.GetStats();
        int fps = Raylib.GetFPS();

        // Background panel for readability
        Raylib.DrawRectangle(0, 0, 380, 320, new Color(0, 0, 0, 180));

        // Draw stats
        DrawText($"SwarmingLilMen - Phase 2: Boids", padding, y, 20, Color.White);
        y += lineHeight + 5;

        DrawText($"FPS: {fps}", padding, y, 18, fps >= 60 ? Color.Green : Color.Yellow);
        y += lineHeight;

        DrawText($"Agents: {stats.AliveAgents} / {stats.TotalAgents}", padding, y, 18, Color.White);
        y += lineHeight;

        DrawText($"Avg Speed: {stats.AverageSpeed:F1} / {world.Config.MaxSpeed:F0}", padding, y, 18, Color.SkyBlue);
        y += lineHeight;

        // Boids settings
        y += 5;
        DrawText($"Boids Settings:", padding, y, 16, Color.Yellow);
        y += lineHeight;
        DrawText($"  Separation: {world.Config.SeparationWeight:F1}", padding, y, 14, Color.Gray);
        y += lineHeight - 3;
        DrawText($"  Alignment:  {world.Config.AlignmentWeight:F1}", padding, y, 14, Color.Gray);
        y += lineHeight - 3;
        DrawText($"  Cohesion:   {world.Config.CohesionWeight:F1}", padding, y, 14, Color.Gray);
        y += lineHeight - 3;
        DrawText($"  Sense Radius: {world.Config.SenseRadius:F0}px", padding, y, 14, Color.Gray);
        y += lineHeight + 5;

        // Visualization options
        DrawText($"Visualization:", padding, y, 16, Color.Yellow);
        y += lineHeight;
        DrawText($"  Velocity: {(_showVelocityVectors ? "ON" : "OFF")} (V)", padding, y, 14,
            _showVelocityVectors ? Color.Green : Color.Gray);
        y += lineHeight - 3;
        DrawText($"  Sense Radius: {(_showSenseRadius ? "ON" : "OFF")} (S)", padding, y, 14,
            _showSenseRadius ? Color.Green : Color.Gray);
        y += lineHeight - 3;
        DrawText($"  Neighbors: {(_showNeighborConnections ? "ON" : "OFF")} (N)", padding, y, 14,
            _showNeighborConnections ? Color.Green : Color.Gray);
        y += lineHeight + 5;

        // Controls
        DrawText("Controls:", padding, y, 16, Color.Yellow);
        y += lineHeight;
        DrawText("  Left Click: Spawn agents", padding, y, 14, Color.LightGray);
        y += lineHeight - 3;
        DrawText("  Space: Spawn 100 random", padding, y, 14, Color.LightGray);
        y += lineHeight - 3;
        DrawText("  R: Reset  |  ESC: Quit", padding, y, 14, Color.LightGray);
    }

    private static void DrawText(string text, int x, int y, int fontSize, Color color)
    {
        Raylib.DrawText(text, x, y, fontSize, color);
    }

    private static void PrintDiagnostics(World world, SimConfig config)
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
                if (distSq < config.SenseRadius * config.SenseRadius)
                    neighbors++;
            }
            totalNeighbors += neighbors;

            if (neighbors < 5) agentsWithFewNeighbors++;
            if (neighbors > 30) agentsWithManyNeighbors++;
        }

        float avgNeighbors = stats.AliveAgents > 0 ? (float)totalNeighbors / stats.AliveAgents : 0f;

        Console.WriteLine($"═══ [T={world.TickCount:D5}] ═══");
        Console.WriteLine($"Agents: {stats.AliveAgents}  |  Avg Speed: {stats.AverageSpeed:F1}/{config.MaxSpeed:F0}  |  Range: [{minSpeed:F1}, {maxSpeed:F1}]");
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
                    if (distSq < config.SenseRadius * config.SenseRadius)
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
}
