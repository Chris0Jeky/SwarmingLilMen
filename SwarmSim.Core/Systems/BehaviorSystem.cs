using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Systems;

/// <summary>
/// Applies boids rules (separation, alignment, cohesion) to generate forces.
/// Uses aggregates computed by SenseSystem to determine desired behavior.
///
/// INVARIANTS:
/// - Reads from: X[], Y[], Vx[], Vy[], State[], SenseSystem aggregates
/// - Writes to: Fx[], Fy[]
/// - Must not allocate memory
/// - Runs after SenseSystem
///
/// ALGORITHM:
/// For each agent with neighbors:
/// 1. Separation: Apply accumulated repulsion force (weighted by SeparationWeight)
/// 2. Alignment: Steer toward average neighbor velocity (weighted by AlignmentWeight)
/// 3. Cohesion: Steer toward average neighbor position (weighted by CohesionWeight)
/// 4. Combine forces and add to Fx[], Fy[]
/// </summary>
public sealed class BehaviorSystem : ISimSystem
{
    private readonly SenseSystem _senseSystem;

    /// <summary>
    /// Creates a BehaviorSystem that depends on SenseSystem for neighbor data.
    /// </summary>
    public BehaviorSystem(SenseSystem senseSystem)
    {
        _senseSystem = senseSystem ?? throw new ArgumentNullException(nameof(senseSystem));
    }

    public void Run(World world, float dt)
    {
        var config = world.Config;
        int count = world.Count;

        var x = world.X;
        var y = world.Y;
        var vx = world.Vx;
        var vy = world.Vy;
        var fx = world.Fx;
        var fy = world.Fy;
        var state = world.State;

        // Get aggregates from SenseSystem
        var neighborCounts = _senseSystem.NeighborCounts;
        var separationX = _senseSystem.SeparationX;
        var separationY = _senseSystem.SeparationY;
        var alignmentVx = _senseSystem.AlignmentVx;
        var alignmentVy = _senseSystem.AlignmentVy;
        var cohesionX = _senseSystem.CohesionX;
        var cohesionY = _senseSystem.CohesionY;

        // Weights from config
        float separationWeight = config.SeparationWeight;
        float alignmentWeight = config.AlignmentWeight;
        float cohesionWeight = config.CohesionWeight;
        float maxSpeed = config.MaxSpeed;

        for (int i = 0; i < count; i++)
        {
            // Skip dead agents
            if (state[i].HasFlag(AgentState.Dead))
                continue;

            int neighborCount = neighborCounts[i];

            // Skip agents with no neighbors
            if (neighborCount == 0)
                continue;

            float invNeighbors = 1f / neighborCount;

            // === Separation Force ===
            // Already accumulated with 1/r^2 weighting by SenseSystem
            float separationForceX = separationX[i] * separationWeight;
            float separationForceY = separationY[i] * separationWeight;

            // === Alignment Force ===
            // Steer toward average neighbor velocity
            float avgVx = alignmentVx[i] * invNeighbors;
            float avgVy = alignmentVy[i] * invNeighbors;
            float alignmentForceX = (avgVx - vx[i]) * alignmentWeight;
            float alignmentForceY = (avgVy - vy[i]) * alignmentWeight;

            // === Cohesion Force ===
            // Steer toward average neighbor position (center of mass)
            float centerX = cohesionX[i] * invNeighbors;
            float centerY = cohesionY[i] * invNeighbors;
            float toCenterX = centerX - x[i];
            float toCenterY = centerY - y[i];

            // Normalize and scale cohesion force
            float cohesionDist = MathUtils.Length(toCenterX, toCenterY);
            float cohesionForceX = 0f;
            float cohesionForceY = 0f;

            if (cohesionDist > 0.001f) // Avoid division by zero
            {
                float cohesionScale = cohesionWeight / cohesionDist;
                cohesionForceX = toCenterX * cohesionScale;
                cohesionForceY = toCenterY * cohesionScale;
            }

            // === Combine Forces ===
            float totalForceX = separationForceX + alignmentForceX + cohesionForceX;
            float totalForceY = separationForceY + alignmentForceY + cohesionForceY;

            // NO FORCE CLAMPING - Traditional Boids doesn't clamp forces, only speeds
            // The small weights (0.05 instead of 300) naturally limit force magnitudes

            // Add to force accumulators
            fx[i] += totalForceX;
            fy[i] += totalForceY;
        }
    }
}
