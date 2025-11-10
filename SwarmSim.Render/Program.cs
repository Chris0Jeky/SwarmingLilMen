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

        // Create world with config tuned for visible flocking
        var config = new SimConfig
        {
            WorldWidth = WindowWidth,
            WorldHeight = WindowHeight,
            BoundaryMode = BoundaryMode.Wrap,
            FixedDeltaTime = 1f / 120f,  // 120 Hz simulation, 60 Hz render

            // Physics tuned for visible, dynamic movement
            MaxSpeed = 300f,             // Increased from 200 for faster movement
            Friction = 0.99f,            // Slightly higher from 0.98 for smoother motion

            // Boids settings for good flocking
            SenseRadius = 80f,           // Increased from 50 so agents can see each other
            SeparationRadius = 25f,      // Increased from 20
            SeparationWeight = 1.5f,     // Keep reasonable separation
            AlignmentWeight = 1.0f,      // Match neighbor velocities
            CohesionWeight = 0.8f,       // Attract to group center

            // Disable combat for peaceful flocking demo
            AttackDamage = 0f,
            BaseDrain = 0.1f
        };

        var world = new World(config, seed: 42);

        // Spawn initial agents
        SpawnInitialAgents(world);

        // Main loop
        while (!Raylib.WindowShouldClose())
        {
            // Handle input
            HandleInput(world);

            // Update simulation (run 2 ticks per frame for 120 Hz sim @ 60 FPS render)
            world.Tick();
            world.Tick();

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
            float speed = rng.NextFloat(50f, 100f);
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
                    new Color(color.R, color.G, color.B, 30));
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
                        new Color(color.R, color.G, color.B, 150)
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
                    new Color(color.R, color.G, color.B, 40)
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

        DrawText($"Avg Speed: {stats.AverageSpeed:F1} / {world.Config.MaxSpeed:F0}", padding, y, 18, Color.Cyan);
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
        y += lineHeight;

        DrawText($"Avg Speed: {stats.AverageSpeed:F1}", padding, y, 18, Color.SkyBlue);
        y += lineHeight + 10;

        // Controls
        DrawText("Controls:", padding, y, 16, Color.Yellow);
        y += lineHeight;
        DrawText("  Left Click: Spawn agents", padding, y, 14, Color.LightGray);
        y += lineHeight - 2;
        DrawText("  Right Click: Spawn group 2", padding, y, 14, Color.LightGray);
        y += lineHeight - 2;
        DrawText("  Space: Spawn 100 random", padding, y, 14, Color.LightGray);
        y += lineHeight - 2;
        DrawText("  R: Reset world", padding, y, 14, Color.LightGray);
        y += lineHeight - 2;
        DrawText("  ESC: Quit", padding, y, 14, Color.LightGray);
    }

    private static void DrawText(string text, int x, int y, int fontSize, Color color)
    {
        Raylib.DrawText(text, x, y, fontSize, color);
    }
}
