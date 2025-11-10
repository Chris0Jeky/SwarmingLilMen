using SwarmSim.Core.Utils;

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
    private float _wanderStrength;
    private Rng _rng;

    public WanderSystem(float wanderStrength = 20f)
    {
        _wanderStrength = wanderStrength;
        _rng = new Rng((uint)System.DateTime.Now.Ticks);
    }

    public void Run(World world, float dt)
    {
        int count = world.Count;
        var fx = world.Fx;
        var fy = world.Fy;
        var state = world.State;

        for (int i = 0; i < count; i++)
        {
            // Skip dead agents
            if (state[i].HasFlag(AgentState.Dead))
                continue;

            // Add small random force
            (float wx, float wy) = _rng.NextUnitVector();
            float wanderMag = _rng.NextFloat(0, _wanderStrength);

            fx[i] += wx * wanderMag;
            fy[i] += wy * wanderMag;
        }
    }
}