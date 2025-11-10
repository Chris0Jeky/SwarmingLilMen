using Raylib_cs;
using SwarmSim.Core;
using System.Numerics;

namespace SwarmSim.Render;

/// <summary>
/// Minimal test program to debug boids issues step by step.
/// Start with the absolute simplest case and build up.
/// </summary>
internal static class MinimalTest
{
    private const int WindowWidth = 1920;
    private const int WindowHeight = 1080;

    // Test progression stages
    private static int _testStage = 0;
    private static readonly string[] StageNames =
    [
        "Stage 0: Two agents, no forces",
        "Stage 1: Two agents, manual force",
        "Stage 2: Two agents, separation only",
        "Stage 3: Three agents, separation only",
        "Stage 4: Three agents, alignment only",
        "Stage 5: Three agents, cohesion only",
        "Stage 6: Three agents, all forces",
        "Stage 7: Ten agents, all forces",
        "Stage 8: Gradual spawn (add 10 every 2 seconds)"
    ];

    private static World? _world;
    private static int _frameCount = 0;
    private static int _lastSpawnFrame = 0;

    public static void Run()
    {
        Raylib.InitWindow(WindowWidth, WindowHeight, "Minimal Boids Test");
        Raylib.SetTargetFPS(60);

        Console.WriteLine("=== MINIMAL BOIDS TEST ===");
        Console.WriteLine("Starting with simplest possible scenario");
        Console.WriteLine("Press SPACE to advance to next test stage");
        Console.WriteLine("Press R to restart current stage");
        Console.WriteLine("Press D for detailed debug of current frame\n");

        InitializeStage(0);

        while (!Raylib.WindowShouldClose())
        {
            HandleInput();

            // Update simulation
            if (_world != null)
            {
                _world.Tick();
                _frameCount++;

                // Periodic diagnostics
                if (_frameCount % 60 == 0) // Every second
                {
                    PrintStageDiagnostics();
                }

                // Stage-specific updates
                UpdateStage();
            }

            Render();
        }

        Raylib.CloseWindow();
    }

    private static void InitializeStage(int stage)
    {
        _testStage = stage;
        _frameCount = 0;
        _lastSpawnFrame = 0;

        Console.WriteLine($"\n=== {StageNames[stage]} ===");

        // Create config based on stage
        var config = CreateConfigForStage(stage);
        _world = new World(config, seed: 42);

        // Spawn agents based on stage
        SpawnAgentsForStage(stage);
    }

    private static SimConfig CreateConfigForStage(int stage)
    {
        // Create config based on stage (must set all values in initializer due to init-only properties)
        return stage switch
        {
            0 or 1 => new SimConfig // No forces or manual force
            {
                WorldWidth = WindowWidth,
                WorldHeight = WindowHeight,
                BoundaryMode = BoundaryMode.Wrap,
                FixedDeltaTime = 1f / 60f,
                MaxSpeed = 10f,
                Friction = 0.99f,
                SeparationWeight = 0f,
                AlignmentWeight = 0f,
                CohesionWeight = 0f,
                SeparationRadius = 30f,
                SenseRadius = 100f,
                AttackDamage = 0f,
                BaseDrain = 0f
            },

            2 or 3 => new SimConfig // Separation only
            {
                WorldWidth = WindowWidth,
                WorldHeight = WindowHeight,
                BoundaryMode = BoundaryMode.Wrap,
                FixedDeltaTime = 1f / 60f,
                MaxSpeed = 10f,
                Friction = 0.99f,
                SeparationWeight = 1.0f,
                AlignmentWeight = 0f,
                CohesionWeight = 0f,
                SeparationRadius = 30f,
                SenseRadius = 100f,
                AttackDamage = 0f,
                BaseDrain = 0f
            },

            4 => new SimConfig // Alignment only
            {
                WorldWidth = WindowWidth,
                WorldHeight = WindowHeight,
                BoundaryMode = BoundaryMode.Wrap,
                FixedDeltaTime = 1f / 60f,
                MaxSpeed = 10f,
                Friction = 0.99f,
                SeparationWeight = 0f,
                AlignmentWeight = 1.0f,
                CohesionWeight = 0f,
                SeparationRadius = 30f,
                SenseRadius = 100f,
                AttackDamage = 0f,
                BaseDrain = 0f
            },

            5 => new SimConfig // Cohesion only
            {
                WorldWidth = WindowWidth,
                WorldHeight = WindowHeight,
                BoundaryMode = BoundaryMode.Wrap,
                FixedDeltaTime = 1f / 60f,
                MaxSpeed = 10f,
                Friction = 0.99f,
                SeparationWeight = 0f,
                AlignmentWeight = 0f,
                CohesionWeight = 0.1f,
                SeparationRadius = 30f,
                SenseRadius = 100f,
                AttackDamage = 0f,
                BaseDrain = 0f
            },

            _ => new SimConfig // All forces (stages 6-8)
            {
                WorldWidth = WindowWidth,
                WorldHeight = WindowHeight,
                BoundaryMode = BoundaryMode.Wrap,
                FixedDeltaTime = 1f / 60f,
                MaxSpeed = 10f,
                Friction = 0.99f,
                SeparationWeight = 1.0f,
                AlignmentWeight = 0.5f,
                CohesionWeight = 0.1f,
                SeparationRadius = 30f,
                SenseRadius = 100f,
                AttackDamage = 0f,
                BaseDrain = 0f
            }
        };
    }

    private static void SpawnAgentsForStage(int stage)
    {
        if (_world == null) return;

        switch (stage)
        {
            case 0: // Two agents, no movement
            case 1: // Two agents, manual force
            case 2: // Two agents, separation
                SpawnTwoAgents(500f); // Far apart
                break;

            case 3: // Three agents, separation
            case 4: // Three agents, alignment
            case 5: // Three agents, cohesion
            case 6: // Three agents, all forces
                SpawnThreeAgents();
                break;

            case 7: // Ten agents
                SpawnTenAgents();
                break;

            case 8: // Start with 3, add more gradually
                SpawnThreeAgents();
                break;
        }

        // For stage 1, add manual force to first agent
        if (stage == 1 && _world.Count > 0)
        {
            Console.WriteLine("Adding manual force of 5.0 to agent 0 (rightward)");
            _world.Fx[0] = 5.0f;
            _world.Fy[0] = 0.0f;
        }
    }

    private static void SpawnTwoAgents(float distance)
    {
        if (_world == null) return;

        float centerX = WindowWidth / 2f;
        float centerY = WindowHeight / 2f;

        int a1 = _world.AddAgent(centerX - distance/2, centerY, 0);
        int a2 = _world.AddAgent(centerX + distance/2, centerY, 0);

        // Give them opposing velocities
        if (a1 >= 0)
        {
            _world.Vx[a1] = 2f;
            _world.Vy[a1] = 0f;
        }
        if (a2 >= 0)
        {
            _world.Vx[a2] = -2f;
            _world.Vy[a2] = 0f;
        }

        Console.WriteLine($"Spawned 2 agents at distance {distance}");
    }

    private static void SpawnThreeAgents()
    {
        if (_world == null) return;

        float centerX = WindowWidth / 2f;
        float centerY = WindowHeight / 2f;
        float radius = 100f;

        // Triangle formation
        for (int i = 0; i < 3; i++)
        {
            float angle = i * (2f * MathF.PI / 3f);
            float x = centerX + MathF.Cos(angle) * radius;
            float y = centerY + MathF.Sin(angle) * radius;

            int idx = _world.AddAgent(x, y, 0);
            if (idx >= 0)
            {
                // Small random velocity
                _world.Vx[idx] = (float)(Random.Shared.NextDouble() - 0.5) * 2f;
                _world.Vy[idx] = (float)(Random.Shared.NextDouble() - 0.5) * 2f;
            }
        }

        Console.WriteLine("Spawned 3 agents in triangle formation");
    }

    private static void SpawnTenAgents()
    {
        if (_world == null) return;

        float centerX = WindowWidth / 2f;
        float centerY = WindowHeight / 2f;

        _world.SpawnAgentsInCircle(centerX, centerY, 150f, 10, 0);

        // Give random velocities
        for (int i = 0; i < _world.Count; i++)
        {
            _world.Vx[i] = (float)(Random.Shared.NextDouble() - 0.5) * 4f;
            _world.Vy[i] = (float)(Random.Shared.NextDouble() - 0.5) * 4f;
        }

        Console.WriteLine("Spawned 10 agents in circle");
    }

    private static void UpdateStage()
    {
        if (_world == null) return;

        // Stage-specific updates
        if (_testStage == 8)
        {
            // Gradual spawn
            if (_frameCount - _lastSpawnFrame > 120 && _world.Count < 100) // Every 2 seconds
            {
                _lastSpawnFrame = _frameCount;
                float x = WindowWidth / 2f + (float)(Random.Shared.NextDouble() - 0.5) * 400;
                float y = WindowHeight / 2f + (float)(Random.Shared.NextDouble() - 0.5) * 400;
                _world.SpawnAgentsInCircle(x, y, 50f, 10, 0);
                Console.WriteLine($"Added 10 more agents (total: {_world.Count})");
            }
        }

        // Keep adding manual force in stage 1
        if (_testStage == 1 && _world.Count > 0)
        {
            _world.Fx[0] += 5.0f;
        }
    }

    private static void PrintStageDiagnostics()
    {
        if (_world == null) return;

        Console.WriteLine($"[Frame {_frameCount}] Agents: {_world.Count}");

        // Print details for first few agents
        for (int i = 0; i < Math.Min(3, _world.Count); i++)
        {
            float speed = MathF.Sqrt(_world.Vx[i] * _world.Vx[i] + _world.Vy[i] * _world.Vy[i]);
            float force = MathF.Sqrt(_world.Fx[i] * _world.Fx[i] + _world.Fy[i] * _world.Fy[i]);

            Console.WriteLine($"  Agent {i}: Pos=({_world.X[i]:F1},{_world.Y[i]:F1}) " +
                            $"Vel=({_world.Vx[i]:F2},{_world.Vy[i]:F2}) Speed={speed:F2} " +
                            $"Force=({_world.Fx[i]:F2},{_world.Fy[i]:F2}) Mag={force:F2}");
        }
    }

    private static void PrintDetailedDebug()
    {
        if (_world == null) return;

        Console.WriteLine("\n=== DETAILED DEBUG ===");
        Console.WriteLine($"Stage: {StageNames[_testStage]}");
        Console.WriteLine($"Config: MaxSpeed={_world.Config.MaxSpeed}, Friction={_world.Config.Friction}");
        Console.WriteLine($"Weights: Sep={_world.Config.SeparationWeight}, Ali={_world.Config.AlignmentWeight}, Coh={_world.Config.CohesionWeight}");

        for (int i = 0; i < _world.Count; i++)
        {
            float speed = MathF.Sqrt(_world.Vx[i] * _world.Vx[i] + _world.Vy[i] * _world.Vy[i]);
            float force = MathF.Sqrt(_world.Fx[i] * _world.Fx[i] + _world.Fy[i] * _world.Fy[i]);

            Console.WriteLine($"\nAgent {i}:");
            Console.WriteLine($"  Position: ({_world.X[i]:F2}, {_world.Y[i]:F2})");
            Console.WriteLine($"  Velocity: ({_world.Vx[i]:F4}, {_world.Vy[i]:F4}) | Speed: {speed:F4}/{_world.Config.MaxSpeed}");
            Console.WriteLine($"  Force:    ({_world.Fx[i]:F4}, {_world.Fy[i]:F4}) | Magnitude: {force:F4}");

            // Count neighbors
            int neighbors = 0;
            for (int j = 0; j < _world.Count; j++)
            {
                if (i != j)
                {
                    float dx = _world.X[j] - _world.X[i];
                    float dy = _world.Y[j] - _world.Y[i];
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist < _world.Config.SenseRadius)
                    {
                        neighbors++;
                    }
                }
            }
            Console.WriteLine($"  Neighbors in sense radius: {neighbors}");
        }
        Console.WriteLine("======================\n");
    }

    private static void HandleInput()
    {
        // Space: Next stage
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            if (_testStage < StageNames.Length - 1)
            {
                InitializeStage(_testStage + 1);
            }
            else
            {
                Console.WriteLine("Already at final stage");
            }
        }

        // R: Restart stage
        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            Console.WriteLine("Restarting stage...");
            InitializeStage(_testStage);
        }

        // D: Detailed debug
        if (Raylib.IsKeyPressed(KeyboardKey.D))
        {
            PrintDetailedDebug();
        }

        // Number keys: Jump to stage
        for (int i = 0; i <= 8; i++)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Zero + i) && i < StageNames.Length)
            {
                InitializeStage(i);
            }
        }
    }

    private static void Render()
    {
        if (_world == null) return;

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        // Draw agents
        for (int i = 0; i < _world.Count; i++)
        {
            if (_world.State[i].HasFlag(AgentState.Dead)) continue;

            float x = _world.X[i];
            float y = _world.Y[i];

            // Draw velocity vector
            float vx = _world.Vx[i];
            float vy = _world.Vy[i];
            float speed = MathF.Sqrt(vx * vx + vy * vy);

            if (speed > 0.1f)
            {
                float scale = 20f; // Scale for visibility
                Raylib.DrawLineEx(
                    new Vector2(x, y),
                    new Vector2(x + vx * scale, y + vy * scale),
                    2f,
                    Color.Green
                );
            }

            // Draw force vector (different color)
            float fx = _world.Fx[i];
            float fy = _world.Fy[i];
            float forceMag = MathF.Sqrt(fx * fx + fy * fy);

            if (forceMag > 0.01f)
            {
                float scale = 5f; // Different scale for forces
                Raylib.DrawLineEx(
                    new Vector2(x, y),
                    new Vector2(x + fx * scale, y + fy * scale),
                    1f,
                    Color.Red
                );
            }

            // Draw agent
            Color agentColor = i == 0 ? Color.Yellow : Color.White; // Highlight first agent
            Raylib.DrawCircle((int)x, (int)y, 5f, agentColor);

            // Draw agent number
            Raylib.DrawText(i.ToString(), (int)x + 8, (int)y - 8, 12, Color.Gray);
        }

        // Draw UI
        DrawUI();

        Raylib.EndDrawing();
    }

    private static void DrawUI()
    {
        if (_world == null) return;

        // Background panel
        Raylib.DrawRectangle(0, 0, 500, 200, new Color(0, 0, 0, 200));

        int y = 10;
        Raylib.DrawText($"MINIMAL TEST - {StageNames[_testStage]}", 10, y, 20, Color.White);
        y += 25;

        Raylib.DrawText($"Frame: {_frameCount} | Agents: {_world.Count}", 10, y, 16, Color.Green);
        y += 20;

        Raylib.DrawText($"Config: Speed={_world.Config.MaxSpeed} Friction={_world.Config.Friction:F3}", 10, y, 14, Color.Gray);
        y += 18;

        Raylib.DrawText($"Weights: Sep={_world.Config.SeparationWeight:F2} Ali={_world.Config.AlignmentWeight:F2} Coh={_world.Config.CohesionWeight:F3}", 10, y, 14, Color.Gray);
        y += 18;

        // Show first agent details
        if (_world.Count > 0)
        {
            float speed = MathF.Sqrt(_world.Vx[0] * _world.Vx[0] + _world.Vy[0] * _world.Vy[0]);
            float force = MathF.Sqrt(_world.Fx[0] * _world.Fx[0] + _world.Fy[0] * _world.Fy[0]);

            Raylib.DrawText($"Agent 0: Speed={speed:F2}/{_world.Config.MaxSpeed} Force={force:F2}", 10, y, 14, Color.Yellow);
            y += 18;
        }

        y += 10;
        Raylib.DrawText("Controls: SPACE=Next Stage | R=Restart | D=Debug | 0-8=Jump to Stage", 10, y, 12, Color.LightGray);

        // Legend
        Raylib.DrawText("Green=Velocity | Red=Force | Yellow=Agent 0", 10, y + 20, 12, Color.LightGray);
    }
}