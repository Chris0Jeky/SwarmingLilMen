namespace SwarmSim.Core.Systems;

/// <summary>
/// Adds random "wander" forces to prevent static equilibrium.
/// This ensures agents keep moving even when boids forces balance out.
///
/// INVARIANTS:
/// - Reads from: State[], Rng
/// - Writes to: Fx[], Fy[]
/// - Adds small random forces to keep agents moving
/// </summary>
public sealed class WanderSystem : ISimSystem
{
    private readonly float _wanderStrength;

    public WanderSystem(float wanderStrength = 20f)
    {
        _wanderStrength = wanderStrength;
    }

    public void Run(World world, float dt)
    {
        int count = world.Count;
        var fx = world.Fx;
        var fy = world.Fy;
        var state = world.State;
        var rng = world.Rng;

        for (int i = 0; i < count; i++)
        {
            // Skip dead agents
            if (state[i].HasFlag(AgentState.Dead))
                continue;

            // Add small random force
            (float wx, float wy) = rng.NextUnitVector();
            float wanderMag = rng.NextFloat(0, _wanderStrength);

            fx[i] += wx * wanderMag;
            fy[i] += wy * wanderMag;
        }
    }
}
