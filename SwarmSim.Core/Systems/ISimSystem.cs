namespace SwarmSim.Core.Systems;

/// <summary>
/// Interface for all simulation systems.
/// Systems are stateless and operate on World data during each tick.
///
/// INVARIANTS:
/// - Must not allocate memory during Run() in hot paths
/// - Must not modify World.Count directly (only through World methods)
/// - Should read from World arrays and write to scratch buffers (Fx, Fy)
/// - Must be deterministic (no time-based randomness, use World.Rng)
/// </summary>
public interface ISimSystem
{
    /// <summary>
    /// Executes the system logic for one simulation tick.
    /// </summary>
    /// <param name="world">The world containing all agent data</param>
    /// <param name="dt">Fixed timestep in seconds</param>
    void Run(World world, float dt);
}
