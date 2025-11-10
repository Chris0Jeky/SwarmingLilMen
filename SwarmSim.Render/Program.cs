using Raylib_cs;
using SwarmSim.Core;
using System.Numerics;

namespace SwarmSim.Render;

internal static class Program
{
    // Window settings
    private const int WindowWidth = 1920;
    private const int WindowHeight = 1080;
    private const string WindowTitle = "SwarmingLilMen - Phase 0 Demo";

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

        // Create world with custom config
        var config = new SimConfig
        {
            WorldWidth = WindowWidth,
            WorldHeight = WindowHeight,
            BoundaryMode = BoundaryMode.Wrap,
            FixedDeltaTime = 1f / 120f,  // 120 Hz simulation, 60 Hz render
            // Peaceful flocking settings
            SeparationWeight = 2.0f,
            AlignmentWeight = 1.5f,
            CohesionWeight = 1.5f,
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
        // Spawn agents in multiple groups
        world.SpawnAgentsInCircle(WindowWidth * 0.25f, WindowHeight * 0.25f, 100f, 250, group: 0);
        world.SpawnAgentsInCircle(WindowWidth * 0.75f, WindowHeight * 0.25f, 100f, 250, group: 1);
        world.SpawnAgentsInCircle(WindowWidth * 0.25f, WindowHeight * 0.75f, 100f, 250, group: 2);
        world.SpawnAgentsInCircle(WindowWidth * 0.75f, WindowHeight * 0.75f, 100f, 250, group: 3);

        Console.WriteLine($"Spawned {world.Count} agents in 4 groups");
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

        // Draw each agent as a colored circle
        for (int i = 0; i < world.Count; i++)
        {
            // Skip dead agents
            if (world.State[i].HasFlag(AgentState.Dead))
                continue;

            // Get color based on group
            byte group = groups[i];
            Color color = GroupColors[group % GroupColors.Length];

            // Draw agent as a small circle (2 pixel radius)
            Raylib.DrawCircle((int)posX[i], (int)posY[i], 2f, color);
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
        Raylib.DrawRectangle(0, 0, 300, 200, new Color(0, 0, 0, 180));

        // Draw stats
        DrawText($"SwarmingLilMen - Phase 0 Demo", padding, y, 20, Color.White);
        y += lineHeight + 5;

        DrawText($"FPS: {fps}", padding, y, 18, fps >= 60 ? Color.Green : Color.Yellow);
        y += lineHeight;

        DrawText($"Agents: {stats.AliveAgents} / {stats.TotalAgents}", padding, y, 18, Color.White);
        y += lineHeight;

        DrawText($"Tick: {stats.TickCount}", padding, y, 18, Color.Gray);
        y += lineHeight;

        DrawText($"Sim Time: {stats.SimulationTime:F2}s", padding, y, 18, Color.Gray);
        y += lineHeight;

        DrawText($"Avg Energy: {stats.AverageEnergy:F1}", padding, y, 18, Color.SkyBlue);
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
