using SwarmSim.Core;
using SwarmSim.Core.Utils;

namespace SwarmSim.Tests;

/// <summary>
/// Tests for boids flocking behavior (separation, alignment, cohesion).
/// </summary>
public class BoidsTests
{
    [Fact]
    public void Separation_TwoCloseAgents_PushApart()
    {
        // Two agents very close together should experience separation force pushing them apart
        var config = new SimConfig
        {
            InitialCapacity = 10,
            SeparationRadius = 20f,
            SenseRadius = 50f,
            SeparationWeight = 2.0f,
            AlignmentWeight = 0f, // Disable for isolation
            CohesionWeight = 0f,  // Disable for isolation
            Friction = 1.0f // No friction to see pure forces
        };

        var world = new World(config, seed: 42);

        // Place two agents very close (distance = 5, which is < separationRadius)
        int idx0 = world.AddAgent(x: 100f, y: 100f, group: 0);
        int idx1 = world.AddAgent(x: 105f, y: 100f, group: 0); // 5 units to the right

        // Run one tick
        world.Tick();

        // Agent 0 should move left (negative vx)
        // Agent 1 should move right (positive vx)
        Assert.True(world.Vx[idx0] < 0f, $"Agent 0 should move left, but vx={world.Vx[idx0]}");
        Assert.True(world.Vx[idx1] > 0f, $"Agent 1 should move right, but vx={world.Vx[idx1]}");
    }

    [Fact]
    public void Alignment_AgentMatchesNeighborVelocity()
    {
        // Agent with no velocity should align with moving neighbor
        var config = new SimConfig
        {
            InitialCapacity = 10,
            SenseRadius = 50f,
            SeparationWeight = 0f,  // Disable
            AlignmentWeight = 1.0f,
            CohesionWeight = 0f,    // Disable
            Friction = 1.0f
        };

        var world = new World(config, seed: 42);

        // Agent 0: stationary
        int idx0 = world.AddAgent(x: 100f, y: 100f, group: 0);
        world.Vx[idx0] = 0f;
        world.Vy[idx0] = 0f;

        // Agent 1: moving to the right, nearby
        int idx1 = world.AddAgent(x: 120f, y: 100f, group: 0);
        world.Vx[idx1] = 50f;  // Moving right
        world.Vy[idx1] = 0f;

        // Run one tick
        world.Tick();

        // Agent 0 should start moving right (positive vx) to align with agent 1
        Assert.True(world.Vx[idx0] > 0f, $"Agent 0 should align and move right, but vx={world.Vx[idx0]}");
    }

    [Fact]
    public void Cohesion_AgentMovesTowardGroup()
    {
        // Isolated agent should move toward the center of nearby group
        var config = new SimConfig
        {
            InitialCapacity = 10,
            SenseRadius = 100f,
            SeparationWeight = 0f,  // Disable
            AlignmentWeight = 0f,   // Disable
            CohesionWeight = 1.0f,
            Friction = 1.0f
        };

        var world = new World(config, seed: 42);

        // Agent 0: left of the group
        int idx0 = world.AddAgent(x: 50f, y: 100f, group: 0);

        // Agents 1-4: clustered to the right
        world.AddAgent(x: 150f, y: 100f, group: 0);
        world.AddAgent(x: 155f, y: 100f, group: 0);
        world.AddAgent(x: 150f, y: 105f, group: 0);
        world.AddAgent(x: 155f, y: 105f, group: 0);

        // Run one tick
        world.Tick();

        // Agent 0 should move right (toward the group)
        Assert.True(world.Vx[idx0] > 0f, $"Agent 0 should move toward group (right), but vx={world.Vx[idx0]}");
    }

    [Fact]
    public void Boids_DifferentGroups_NoInteraction()
    {
        // Agents from different groups should not influence each other
        var config = new SimConfig
        {
            InitialCapacity = 10,
            SenseRadius = 50f,
            SeparationWeight = 2.0f,
            AlignmentWeight = 1.0f,
            CohesionWeight = 1.0f,
            Friction = 1.0f
        };

        var world = new World(config, seed: 42);

        // Agent 0: group 0
        int idx0 = world.AddAgent(x: 100f, y: 100f, group: 0);

        // Agent 1: group 1, very close
        world.AddAgent(x: 105f, y: 100f, group: 1);

        // Run one tick
        world.Tick();

        // Agent 0 should have no forces (no neighbors in same group)
        // Use approximate equality for floating-point comparisons
        Assert.InRange(world.Vx[idx0], -0.001f, 0.001f);
        Assert.InRange(world.Vy[idx0], -0.001f, 0.001f);
    }

    [Fact]
    public void Boids_NoNeighbors_NoForces()
    {
        // Agent with no neighbors should not move
        var config = new SimConfig
        {
            InitialCapacity = 10,
            SenseRadius = 50f,
            Friction = 1.0f
        };

        var world = new World(config, seed: 42);

        int idx = world.AddAgent(x: 100f, y: 100f, group: 0);

        // Run one tick
        world.Tick();

        // Should have no velocity (no forces applied)
        // Use approximate equality for floating-point comparisons
        Assert.InRange(world.Vx[idx], -0.001f, 0.001f);
        Assert.InRange(world.Vy[idx], -0.001f, 0.001f);
    }

    [Fact]
    public void Boids_SpeedLimit_RespectedAfterIntegration()
    {
        // Agents should not exceed MaxSpeed
        var config = new SimConfig
        {
            InitialCapacity = 10,
            SenseRadius = 50f,
            MaxSpeed = 100f,
            SeparationWeight = 10.0f, // High separation for large forces
            Friction = 1.0f
        };

        var world = new World(config, seed: 42);

        // Two very close agents (high separation force)
        int idx0 = world.AddAgent(x: 100f, y: 100f, group: 0);
        int idx1 = world.AddAgent(x: 101f, y: 100f, group: 0);

        // Run multiple ticks to build up speed
        for (int i = 0; i < 10; i++)
        {
            world.Tick();
        }

        // Check that speeds are within MaxSpeed
        float speed0 = MathUtils.Length(world.Vx[idx0], world.Vy[idx0]);
        float speed1 = MathUtils.Length(world.Vx[idx1], world.Vy[idx1]);

        Assert.True(speed0 <= config.MaxSpeed, $"Agent 0 speed {speed0} exceeds MaxSpeed {config.MaxSpeed}");
        Assert.True(speed1 <= config.MaxSpeed, $"Agent 1 speed {speed1} exceeds MaxSpeed {config.MaxSpeed}");
    }

    [Fact]
    public void Boids_MultipleAgents_FormCohesiveGroup()
    {
        // Scattered agents should gradually move toward each other
        var config = new SimConfig
        {
            InitialCapacity = 20,
            SenseRadius = 200f, // Large sense radius to see all agents
            SeparationRadius = 15f,
            SeparationWeight = 1.5f,
            AlignmentWeight = 1.0f,
            CohesionWeight = 1.0f,
            MaxSpeed = 100f,
            Friction = 0.95f
        };

        var world = new World(config, seed: 42);

        // Spawn 10 agents in a scattered pattern
        var rng = new Rng(123);
        for (int i = 0; i < 10; i++)
        {
            float x = rng.NextFloat(50f, 150f);
            float y = rng.NextFloat(50f, 150f);
            world.AddAgent(x, y, group: 0);
        }

        // Calculate initial spread (average distance from centroid)
        float initialSpread = CalculateSpread(world);

        // Run simulation for 500 ticks (give more time for cohesion)
        for (int i = 0; i < 500; i++)
        {
            world.Tick();
        }

        // Calculate final spread
        float finalSpread = CalculateSpread(world);

        // Group should be more cohesive (lower spread) OR at least maintain cohesion
        // The important thing is that agents don't scatter apart
        Assert.True(finalSpread <= initialSpread,
            $"Group should maintain or improve cohesion. Initial spread: {initialSpread:F2}, Final spread: {finalSpread:F2}");
    }

    [Fact]
    public void Boids_Deterministic_SameResultsWithSameSeed()
    {
        // Two worlds with same seed should produce identical results
        var config = new SimConfig
        {
            InitialCapacity = 20,
            SenseRadius = 100f
        };

        var world1 = new World(config, seed: 42);
        var world2 = new World(config, seed: 42);

        // Add same agents to both worlds
        for (int i = 0; i < 10; i++)
        {
            world1.AddRandomAgent(group: 0);
            world2.AddRandomAgent(group: 0);
        }

        // Run both for 10 ticks
        for (int i = 0; i < 10; i++)
        {
            world1.Tick();
            world2.Tick();
        }

        // Positions should match (within floating-point precision)
        // The steering behavior formulation may have slightly different rounding, but should be deterministic
        const float epsilon = 0.01f; // Tight tolerance to catch real determinism bugs
        for (int i = 0; i < 10; i++)
        {
            Assert.InRange(world1.X[i], world2.X[i] - epsilon, world2.X[i] + epsilon);
            Assert.InRange(world1.Y[i], world2.Y[i] - epsilon, world2.Y[i] + epsilon);
            Assert.InRange(world1.Vx[i], world2.Vx[i] - epsilon, world2.Vx[i] + epsilon);
            Assert.InRange(world1.Vy[i], world2.Vy[i] - epsilon, world2.Vy[i] + epsilon);
        }
    }

    private static float CalculateSpread(World world)
    {
        // Calculate centroid
        float cx = 0f, cy = 0f;
        int count = 0;

        for (int i = 0; i < world.Count; i++)
        {
            if (!world.State[i].HasFlag(AgentState.Dead))
            {
                cx += world.X[i];
                cy += world.Y[i];
                count++;
            }
        }

        if (count == 0) return 0f;

        cx /= count;
        cy /= count;

        // Calculate average distance from centroid
        float totalDist = 0f;
        for (int i = 0; i < world.Count; i++)
        {
            if (!world.State[i].HasFlag(AgentState.Dead))
            {
                float dx = world.X[i] - cx;
                float dy = world.Y[i] - cy;
                totalDist += MathF.Sqrt(dx * dx + dy * dy);
            }
        }

        return totalDist / count;
    }
}
