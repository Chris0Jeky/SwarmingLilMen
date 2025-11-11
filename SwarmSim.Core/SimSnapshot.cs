namespace SwarmSim.Core;

/// <summary>
/// Immutable snapshot of a world's visible state at a specific tick.
/// Copies the minimal data needed for rendering or analytics so that
/// readers can safely inspect positions without racing the simulation.
/// </summary>
public sealed class SimSnapshot
{
    /// <summary>Simulation tick the snapshot was captured on.</summary>
    public ulong TickCount { get; }

    /// <summary>Simulation time in seconds at capture.</summary>
    public float SimulationTime { get; }

    /// <summary>Total active agents captured.</summary>
    public int AgentCount { get; }

    /// <summary>Copied X positions for active agents.</summary>
    public float[] PositionsX { get; }

    /// <summary>Copied Y positions for active agents.</summary>
    public float[] PositionsY { get; }

    /// <summary>Copied X velocities for active agents.</summary>
    public float[] VelocitiesX { get; }

    /// <summary>Copied Y velocities for active agents.</summary>
    public float[] VelocitiesY { get; }

    /// <summary>Copied group identifiers.</summary>
    public byte[] Groups { get; }

    private SimSnapshot(
        ulong tickCount,
        float simulationTime,
        int agentCount,
        float[] positionsX,
        float[] positionsY,
        float[] velocitiesX,
        float[] velocitiesY,
        byte[] groups)
    {
        TickCount = tickCount;
        SimulationTime = simulationTime;
        AgentCount = agentCount;
        PositionsX = positionsX;
        PositionsY = positionsY;
        VelocitiesX = velocitiesX;
        VelocitiesY = velocitiesY;
        Groups = groups;
    }

    /// <summary>
    /// Creates an immutable snapshot by copying the leading <paramref name="world.Count"/>
    /// entries from the world's SoA arrays.
    /// </summary>
    public static SimSnapshot FromWorld(World world)
    {
        int agentCount = world.Count;

        var posX = new float[agentCount];
        var posY = new float[agentCount];
        var velX = new float[agentCount];
        var velY = new float[agentCount];
        var groups = new byte[agentCount];

        Array.Copy(world.X, posX, agentCount);
        Array.Copy(world.Y, posY, agentCount);
        Array.Copy(world.Vx, velX, agentCount);
        Array.Copy(world.Vy, velY, agentCount);
        Array.Copy(world.Group, groups, agentCount);

        return new SimSnapshot(
            world.TickCount,
            world.SimulationTime,
            agentCount,
            posX,
            posY,
            velX,
            velY,
            groups);
    }
}
