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
            FixedDeltaTime = 1f / 120f,  // 120 Hz simulation, 60 Hz render

            // Physics tuned for visible, dynamic movement
            MaxSpeed = 300f,             // Fast movement
            Friction = 0.995f,           // Much less friction (was killing velocity too fast)

            // Boids settings - MASSIVE forces to overcome tiny dt
            SenseRadius = 120f,          // Larger interaction range
            SeparationRadius = 30f,      // Slightly increased
            SeparationWeight = 300.0f,   // 100x increase to overcome dt=1/120
            AlignmentWeight = 200.0f,    // 100x increase
            CohesionWeight = 150.0f,     // 100x increase

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

            // Update simulation (run 2 ticks per frame for 120 Hz sim @ 60 FPS render)
            world.Tick();
            world.Tick();

            // Periodic diagnostic output (every 2 seconds)
            frameCount++;
            if (frameCount % 120 == 0) // Every 2 seconds at 60 FPS
            {
                var stats = world.GetStats();
                Console.WriteLine($"[T={world.TickCount:D5}] Agents: {stats.AliveAgents}, Avg Speed: {stats.AverageSpeed:F1}/{config.MaxSpeed:F0}");

                // Sample a few agents to see their state
                if (world.Count > 0)
                {
                    // Check first agent's velocity and force
                    float speed0 = MathF.Sqrt(world.Vx[0] * world.Vx[0] + world.Vy[0] * world.Vy[0]);
                    float force0 = MathF.Sqrt(world.Fx[0] * world.Fx[0] + world.Fy[0] * world.Fy[0]);
                    Console.WriteLine($"  Agent 0: Speed={speed0:F1}, Force={force0:F1}, Pos=({world.X[0]:F0},{world.Y[0]:F0})");

                    // Check if agents are finding neighbors
                    int neighborsInRange = 0;
                    float x0 = world.X[0];
                    float y0 = world.Y[0];
                    byte group0 = world.Group[0];

                    for (int i = 1; i < Math.Min(world.Count, 50); i++) // Check first 50 agents
                    {
                        if (world.Group[i] != group0) continue;
                        float dx = world.X[i] - x0;
                        float dy = world.Y[i] - y0;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                        if (dist < config.SenseRadius)
                            neighborsInRange++;
                    }
                    Console.WriteLine($"  Agent 0 has {neighborsInRange} neighbors in range, dt={config.FixedDeltaTime:F4}");
                Console.WriteLine();
            }

            // Render
            Render(world);
        }

        Raylib.CloseWindow();
    }

    private static void SpawnInitialAgents(World world)
    {
        // Spawn agents closer together in the center for immediate interaction
        // Use smaller clusters that are close enough to interact (within SenseRadius)
        float centerX = WindowWidth * 0.5f;
        float centerY = WindowHeight * 0.5f;
        float clusterSpacing = 150f; // Close enough to interact (within 2x SenseRadius)

        // Spawn 4 groups in a tighter formation around center
        world.SpawnAgentsInCircle(centerX - clusterSpacing, centerY - clusterSpacing, 80f, 200, group: 0);
        world.SpawnAgentsInCircle(centerX + clusterSpacing, centerY - clusterSpacing, 80f, 200, group: 1);
        world.SpawnAgentsInCircle(centerX - clusterSpacing, centerY + clusterSpacing, 80f, 200, group: 2);
        world.SpawnAgentsInCircle(centerX + clusterSpacing, centerY + clusterSpacing, 80f, 200, group: 3);

        // Give agents some initial random velocity so they start moving
        var rng = world.Rng;
        for (int i = 0; i < world.Count; i++)
        {
            (float vx, float vy) = rng.NextUnitVector();
            float speed = rng.NextFloat(100f, 200f);  // INCREASED from 50-100 to 100-200
            world.Vx[i] = vx * speed;
            world.Vy[i] = vy * speed;
        }

        Console.WriteLine($"Spawned {world.Count} agents in 4 groups (centered for interaction)");
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
}
