using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Systems;

/// <summary>
/// Integrates forces into velocity, then velocity into position using semi-implicit Euler.
/// Applies friction (if damped model), speed clamping, and boundary conditions.
///
/// INVARIANTS:
/// - Reads from: Fx[], Fy[], Vx[], Vy[], X[], Y[], State[], Config
/// - Writes to: Vx[], Vy[], X[], Y[]
/// - Must not allocate memory
/// - Respects Config.MaxSpeed, Config.MaxForce, Config.Friction, Config.SpeedModel, Config.BoundaryMode
///
/// ALGORITHM (Semi-Implicit Euler):
/// 1. For each active agent:
///    - Integrate steering into velocity: V += Steering * dt
///    - Apply friction if SpeedModel.Damped: V *= friction
///    - Clamp speed to MaxSpeed
///    - Integrate velocity into position: P += V * dt (uses UPDATED velocity)
///    - Apply boundary conditions (wrap/reflect/clamp)
///
/// Semi-implicit Euler (velocity first, then position) is more stable than explicit Euler
/// for game physics and is the recommended approach by Gaffer on Games.
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
        var speedModel = config.SpeedModel;
        float worldWidth = config.WorldWidth;
        float worldHeight = config.WorldHeight;
        var boundaryMode = config.BoundaryMode;

        // Determine if friction should be applied based on speed model
        bool applyFriction = speedModel == SpeedModel.Damped;

        for (int i = 0; i < count; i++)
        {
            // Skip dead agents
            if (state[i].HasFlag(AgentState.Dead))
                continue;

            // STEP 1: Integrate steering forces into velocity
            // (Semi-implicit Euler: update velocity first)
            vx[i] += fx[i] * dt;
            vy[i] += fy[i] * dt;

            // STEP 2: Apply friction if using damped model
            // ConstantSpeed model skips friction (agents maintain momentum)
            if (applyFriction)
            {
                vx[i] *= friction;
                vy[i] *= friction;
            }

            // STEP 3: Clamp speed to maxSpeed
            float speed = MathUtils.Length(vx[i], vy[i]);
            if (speed > maxSpeed)
            {
                float scale = maxSpeed / speed;
                vx[i] *= scale;
                vy[i] *= scale;
            }

            // STEP 4: Integrate velocity into position
            // (Semi-implicit: use UPDATED velocity from step 1-3)
            x[i] += vx[i] * dt;
            y[i] += vy[i] * dt;

            // STEP 5: Apply boundary conditions
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
