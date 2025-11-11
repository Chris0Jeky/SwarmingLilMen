using SwarmSim.Core;

namespace SwarmSim.Tests;

public class WorldTests
{
    [Fact]
    public void Constructor_CreatesEmptyWorld()
    {
        // Arrange & Act
        var world = new World(new SimConfig(), seed: 42u);

        // Assert
        Assert.Equal(0, world.Count);
        Assert.True(world.Capacity > 0);
        Assert.Equal(0ul, world.TickCount);
        Assert.Equal(0f, world.SimulationTime);
    }

    [Fact]
    public void AddAgent_IncreasesCount()
    {
        // Arrange
        var world = new World(new SimConfig(), seed: 42u);

        // Act
        int idx = world.AddAgent(100f, 100f, group: 0);

        // Assert
        Assert.Equal(0, idx); // First agent gets index 0
        Assert.Equal(1, world.Count);
    }

    [Fact]
    public void AddAgent_StoresPositionCorrectly()
    {
        // Arrange
        var world = new World(new SimConfig(), seed: 42u);
        const float expectedX = 123.45f;
        const float expectedY = 678.90f;

        // Act
        int idx = world.AddAgent(expectedX, expectedY);

        // Assert
        Assert.Equal(expectedX, world.X[idx]);
        Assert.Equal(expectedY, world.Y[idx]);
    }

    [Fact]
    public void AddAgent_InitializesWithDefaultValues()
    {
        // Arrange
        var config = new SimConfig();
        var world = new World(config, seed: 42u);

        // Act
        int idx = world.AddAgent(0f, 0f);

        // Assert
        Assert.Equal(0f, world.Vx[idx]);
        Assert.Equal(0f, world.Vy[idx]);
        Assert.Equal(config.InitialEnergy, world.Energy[idx]);
        Assert.Equal(config.InitialHealth, world.Health[idx]);
        Assert.Equal(0f, world.Age[idx]);
        Assert.Equal(AgentState.None, world.State[idx]);
    }

    [Fact]
    public void AddRandomAgent_AddsAgentWithinBounds()
    {
        // Arrange
        var config = new SimConfig { WorldWidth = 1000f, WorldHeight = 1000f };
        var world = new World(config, seed: 42u);

        // Act
        int idx = world.AddRandomAgent(group: 1);

        // Assert
        Assert.InRange(world.X[idx], 0f, config.WorldWidth);
        Assert.InRange(world.Y[idx], 0f, config.WorldHeight);
        Assert.Equal(1, world.Group[idx]);
    }

    [Fact]
    public void SpawnAgentsInCircle_CreatesMultipleAgents()
    {
        // Arrange
        var world = new World(new SimConfig(), seed: 42u);

        // Act
        int spawned = world.SpawnAgentsInCircle(500f, 500f, radius: 100f, count: 50, group: 2);

        // Assert
        Assert.Equal(50, spawned);
        Assert.Equal(50, world.Count);

        // All agents should be in group 2
        for (int i = 0; i < world.Count; i++)
        {
            Assert.Equal(2, world.Group[i]);
        }
    }

    [Fact]
    public void MarkDead_SetsDeadFlag()
    {
        // Arrange
        var world = new World(new SimConfig(), seed: 42u);
        int idx = world.AddAgent(0f, 0f);

        // Act
        world.MarkDead(idx);

        // Assert
        Assert.True(world.State[idx].HasFlag(AgentState.Dead));
    }

    [Fact]
    public void CompactDeadAgents_RemovesDeadAgents()
    {
        // Arrange
        var world = new World(new SimConfig(), seed: 42u);
        world.AddAgent(0f, 0f); // idx 0
        world.AddAgent(10f, 10f); // idx 1
        world.AddAgent(20f, 20f); // idx 2

        world.MarkDead(1); // Mark middle agent as dead

        // Act
        int removed = world.CompactDeadAgents();

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(2, world.Count);

        // Remaining agents should be at indices 0 and 1 (compacted)
        Assert.Equal(0f, world.X[0]);
        Assert.Equal(20f, world.X[1]); // This was at idx 2, now at idx 1
    }

    [Fact]
    public void Tick_AdvancesSimulationTime()
    {
        // Arrange
        var config = new SimConfig { FixedDeltaTime = 1f / 60f };
        var world = new World(config, seed: 42u);
        world.AddAgent(100f, 100f);

        // Act
        world.Tick();
        world.Tick();
        world.Tick();

        // Assert
        Assert.Equal(3ul, world.TickCount);
        Assert.InRange(world.SimulationTime, 0.049f, 0.051f); // ~0.05 seconds (3 * 1/60)
    }

    [Fact]
    public void Tick_AppliesFriction()
    {
        // Arrange
        var config = new SimConfig
        {
            Friction = 0.9f,
            SpeedModel = SpeedModel.Damped, // Explicitly use damped model for friction test
            FixedDeltaTime = 1f / 60f
        };
        var world = new World(config, seed: 42u);
        int idx = world.AddAgent(100f, 100f);

        // Set initial velocity
        world.Vx[idx] = 100f;
        world.Vy[idx] = 100f;

        // Act
        world.Tick();

        // Assert - Velocity should be reduced by friction
        Assert.InRange(world.Vx[idx], 85f, 95f); // Should be around 90
        Assert.InRange(world.Vy[idx], 85f, 95f);
    }

    [Fact]
    public void BoundaryMode_Wrap_WrapsPosition()
    {
        // Arrange
        var config = new SimConfig
        {
            WorldWidth = 100f,
            WorldHeight = 100f,
            BoundaryMode = BoundaryMode.Wrap,
            FixedDeltaTime = 0.1f, // Larger timestep for easier testing
            Friction = 1.0f // No friction
        };
        var world = new World(config, seed: 42u);
        int idx = world.AddAgent(95f, 95f);

        // Set velocity to move beyond boundary in one tick
        world.Vx[idx] = 100f; // Will move 10 units per tick (100 * 0.1)
        world.Vy[idx] = 100f;

        // Act
        world.Tick();

        // Assert - Position should wrap around (started at 95, moved 10, wrapped to ~5)
        Assert.InRange(world.X[idx], 4f, 6f); // Wrapped around
        Assert.InRange(world.Y[idx], 4f, 6f); // Wrapped around
    }

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        // Arrange
        var world = new World(new SimConfig(), seed: 42u);
        world.AddAgent(0f, 0f);
        world.AddAgent(10f, 10f);
        world.AddAgent(20f, 20f);

        world.MarkDead(1);

        // Act
        var stats = world.GetStats();

        // Assert
        Assert.Equal(3, stats.TotalAgents);
        Assert.Equal(2, stats.AliveAgents); // One is dead
    }

    [Fact]
    public void Determinism_SameSeed_ProducesSameResults()
    {
        // Arrange
        const uint seed = 12345u;
        var config = new SimConfig();

        var world1 = new World(config, seed);
        var world2 = new World(config, seed);

        // Act - Add random agents and run ticks
        for (int i = 0; i < 10; i++)
        {
            world1.AddRandomAgent();
            world2.AddRandomAgent();
        }

        for (int i = 0; i < 100; i++)
        {
            world1.Tick();
            world2.Tick();
        }

        // Assert - Positions should be identical
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(world1.X[i], world2.X[i]);
            Assert.Equal(world1.Y[i], world2.Y[i]);
        }
    }
}
