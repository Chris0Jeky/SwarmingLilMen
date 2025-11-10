namespace SwarmSim.Core.Systems;

/// <summary>
/// Adds random forces to agents for testing movement and spatial grid.
/// This is a temporary system used for Phase 1 testing and will be replaced
/// by BehaviorSystem (boids) in Phase 2.
///
/// INVARIANTS:
/// - Reads from: State[], Count
/// - Writes to: Fx[], Fy[]
/// - Must not allocate memory
/// - Uses World.Rng for determinism
///
/// ALGORITHM:
/// - For each active agent, add a random force with magnitude ForceStrength
/// - Forces are applied in random directions using unit vectors
/// </summary>
public sealed class RandomWalkSystem : ISimSystem
{
    /// <summary>
    /// Strength of the random force applied to each agent.
    /// Can be tuned via configuration or constructor.
    /// </summary>
    public float ForceStrength { get; set; }

    /// <summary>
    /// Creates a new RandomWalkSystem with the specified force strength.
    /// </summary>
    /// <param name="forceStrength">Magnitude of random forces (default: 100)</param>
    public RandomWalkSystem(float forceStrength = 100f)
    {
        ForceStrength = forceStrength;
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

            // Add random force in a random direction
            (float dx, float dy) = rng.NextUnitVector();

            fx[i] += dx * ForceStrength;
            fy[i] += dy * ForceStrength;
        }
    }
}
