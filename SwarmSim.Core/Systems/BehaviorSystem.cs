using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Systems;

/// <summary>
/// Applies canonical steering behaviors (Reynolds model) to generate steering forces.
/// Uses aggregates computed by SenseSystem to determine desired velocities.
///
/// INVARIANTS:
/// - Reads from: X[], Y[], Vx[], Vy[], State[], SenseSystem aggregates, Config
/// - Writes to: Fx[], Fy[]
/// - Must not allocate memory
/// - Runs after SenseSystem
///
/// ALGORITHM (Canonical Steering Behaviors):
/// For each agent with neighbors:
/// 1. Separation: Compute desired velocity away from neighbors, steer = clamp(desired - current, maxForce)
/// 2. Alignment: Compute desired velocity matching neighbors, steer = clamp(desired - current, maxForce)
/// 3. Cohesion: Compute desired velocity toward center, steer = clamp(desired - current, maxForce)
/// 4. Sum steering forces and add to Fx[], Fy[]
///
/// Steering formulation advantages:
/// - Predictable force magnitudes (capped by maxForce)
/// - No force/friction equilibrium pathologies
/// - Easy to tune (weights scale desired velocity, not raw forces)
/// - Industry-standard approach from Reynolds' "Steering Behaviors for Autonomous Characters"
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

        // Parameters from config
        float maxSpeed = config.MaxSpeed;
        float maxForce = config.MaxForce;
        float separationWeight = config.SeparationWeight;
        float alignmentWeight = config.AlignmentWeight;
        float cohesionWeight = config.CohesionWeight;

        for (int i = 0; i < count; i++)
        {
            // Skip dead agents
            if (state[i].HasFlag(AgentState.Dead))
                continue;

            int neighborCount = neighborCounts[i];

            // Skip agents with no neighbors
            if (neighborCount == 0)
                continue;

            // Current velocity
            float currentVx = vx[i];
            float currentVy = vy[i];

            // Accumulators for total steering force (prioritized budget)
            float totalSteeringX = 0f;
            float totalSteeringY = 0f;
            float remainingForce = maxForce;

            // === Separation Steering ===
            // Desired: Move away from neighbors at maxSpeed
            float sepX = separationX[i];
            float sepY = separationY[i];
            float sepMag = MathUtils.Length(sepX, sepY);

            if (sepMag > 0.001f)
            {
                float crowdingBoost = 1f;
                if (neighborCount > config.SeparationCrowdingThreshold)
                {
                    float excess = neighborCount - config.SeparationCrowdingThreshold;
                    float normalized = MathF.Min(1f, excess / Math.Max(1, config.SeparationCrowdingThreshold));
                    crowdingBoost = MathUtils.Lerp(1f, config.SeparationCrowdingBoost, normalized);
                }

                // Desired velocity: away from neighbors at (maxSpeed * weight)
                float desiredSpeed = maxSpeed * separationWeight * crowdingBoost;
                float desiredVx = (sepX / sepMag) * desiredSpeed;
                float desiredVy = (sepY / sepMag) * desiredSpeed;

                // Steering: desired - current, clamped to maxForce
                float steerX = desiredVx - currentVx;
                float steerY = desiredVy - currentVy;
                (steerX, steerY) = ClampMagnitude(steerX, steerY, remainingForce);
                AddPrioritizedSteer(ref totalSteeringX, ref totalSteeringY, ref remainingForce, steerX, steerY);
            }

            if (remainingForce <= 0f)
            {
                fx[i] += totalSteeringX;
                fy[i] += totalSteeringY;
                continue;
            }

            // === Alignment Steering ===
            // Desired: Match average neighbor velocity
            float invNeighbors = 1f / neighborCount;
            float avgVx = alignmentVx[i] * invNeighbors;
            float avgVy = alignmentVy[i] * invNeighbors;
            float avgMag = MathUtils.Length(avgVx, avgVy);

            if (avgMag > 0.001f)
            {
                // Desired velocity: match neighbors at (maxSpeed * weight)
                float desiredSpeed = maxSpeed * alignmentWeight;
                float desiredVx = (avgVx / avgMag) * desiredSpeed;
                float desiredVy = (avgVy / avgMag) * desiredSpeed;

                // Steering: desired - current, clamped to maxForce
                float steerX = desiredVx - currentVx;
                float steerY = desiredVy - currentVy;
                (steerX, steerY) = ClampMagnitude(steerX, steerY, remainingForce);
                AddPrioritizedSteer(ref totalSteeringX, ref totalSteeringY, ref remainingForce, steerX, steerY);
            }

            if (remainingForce <= 0f)
            {
                fx[i] += totalSteeringX;
                fy[i] += totalSteeringY;
                continue;
            }

            // === Cohesion Steering ===
            // Desired: Move toward center of mass
            float centerX = cohesionX[i] * invNeighbors;
            float centerY = cohesionY[i] * invNeighbors;
            float toCenterX = centerX - x[i];
            float toCenterY = centerY - y[i];
            float centerMag = MathUtils.Length(toCenterX, toCenterY);

            if (centerMag > 0.001f)
            {
                // Desired velocity: toward center at (maxSpeed * weight)
                float desiredSpeed = maxSpeed * cohesionWeight;
                float desiredVx = (toCenterX / centerMag) * desiredSpeed;
                float desiredVy = (toCenterY / centerMag) * desiredSpeed;

                // Steering: desired - current, clamped to maxForce
                float steerX = desiredVx - currentVx;
                float steerY = desiredVy - currentVy;
                (steerX, steerY) = ClampMagnitude(steerX, steerY, remainingForce);
                AddPrioritizedSteer(ref totalSteeringX, ref totalSteeringY, ref remainingForce, steerX, steerY);
            }

            // Add combined steering force to accumulators
            fx[i] += totalSteeringX;
            fy[i] += totalSteeringY;
        }
    }

    /// <summary>
    /// Clamps a vector to a maximum magnitude.
    /// Returns (x, y) with magnitude capped at maxMagnitude.
    /// </summary>
    private static (float x, float y) ClampMagnitude(float x, float y, float maxMagnitude)
    {
        float mag = MathUtils.Length(x, y);
        if (mag > maxMagnitude)
        {
            float scale = maxMagnitude / mag;
            return (x * scale, y * scale);
        }
        return (x, y);
    }

    private static void AddPrioritizedSteer(ref float totalX, ref float totalY, ref float remainingForce, float steerX, float steerY)
    {
        if (remainingForce <= 0f)
            return;

        float mag = MathUtils.Length(steerX, steerY);
        if (mag < 0.0001f)
            return;

        float allowed = MathF.Min(mag, remainingForce);
        float scale = allowed / mag;
        totalX += steerX * scale;
        totalY += steerY * scale;
        remainingForce -= allowed;
    }
}
