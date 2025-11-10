using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Systems;

/// <summary>
/// Integrates forces into velocity, then velocity into position.
/// Applies friction, speed clamping, and boundary conditions.
///
/// INVARIANTS:
/// - Reads from: Fx[], Fy[], Vx[], Vy[], X[], Y[], State[]
/// - Writes to: Vx[], Vy[], X[], Y[]
/// - Must not allocate memory
/// - Respects Config.MaxSpeed, Config.Friction, Config.BoundaryMode
///
/// ALGORITHM:
/// 1. For each active agent:
///    - Integrate forces into velocity: V += F * dt
///    - Apply friction: V *= friction
///    - Clamp speed to MaxSpeed
///    - Integrate velocity into position: P += V * dt
///    - Apply boundary conditions (wrap/reflect/clamp)
/// </summary>
public sealed class IntegrateSystem : ISimSystem
{
    public void Run(World world, float dt)
    {
        var config = world.Config;
        int count = world.Count;

        // Direct array access for performance
        var x = world.X;
        var y = world.Y;
        var vx = world.Vx;
        var vy = world.Vy;
        var fx = world.Fx;
        var fy = world.Fy;
        var state = world.State;

        float maxSpeed = config.MaxSpeed;
        float friction = config.Friction;
        float worldWidth = config.WorldWidth;
        float worldHeight = config.WorldHeight;
        var boundaryMode = config.BoundaryMode;

        for (int i = 0; i < count; i++)
        {
            // Skip dead agents
            if (state[i].HasFlag(AgentState.Dead))
                continue;

            // Integrate forces into velocity
            vx[i] += fx[i] * dt;
            vy[i] += fy[i] * dt;

            // Apply friction
            vx[i] *= friction;
            vy[i] *= friction;

            // Clamp to max speed
            float speed = MathUtils.Length(vx[i], vy[i]);
            if (speed > maxSpeed)
            {
                float scale = maxSpeed / speed;
                vx[i] *= scale;
                vy[i] *= scale;
            }

            // Integrate velocity into position
            x[i] += vx[i] * dt;
            y[i] += vy[i] * dt;

            // Apply boundary conditions
            ApplyBoundary(ref x[i], ref y[i], ref vx[i], ref vy[i], boundaryMode, worldWidth, worldHeight);
        }
    }

    /// <summary>
    /// Applies boundary conditions based on the configured mode.
    /// </summary>
    private static void ApplyBoundary(
        ref float x, ref float y,
        ref float vx, ref float vy,
        BoundaryMode mode,
        float worldWidth, float worldHeight)
    {
        switch (mode)
        {
            case BoundaryMode.Wrap:
                (x, y) = MathUtils.WrapPosition(x, y, worldWidth, worldHeight);
                break;

            case BoundaryMode.Reflect:
                MathUtils.ReflectPosition(ref x, ref vx, worldWidth);
                MathUtils.ReflectPosition(ref y, ref vy, worldHeight);
                break;

            case BoundaryMode.Clamp:
                x = MathUtils.Clamp(x, 0f, worldWidth);
                y = MathUtils.Clamp(y, 0f, worldHeight);
                // Stop at boundary
                if (x == 0f || x == worldWidth) vx = 0f;
                if (y == 0f || y == worldHeight) vy = 0f;
                break;
        }
    }
}
