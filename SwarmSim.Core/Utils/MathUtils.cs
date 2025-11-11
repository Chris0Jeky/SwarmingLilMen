namespace SwarmSim.Core.Utils;

/// <summary>
/// High-performance math utilities for swarm simulation.
/// All methods designed for hot path usage - no allocations.
/// </summary>
public static class MathUtils
{
    public const float Pi = MathF.PI;
    public const float TwoPi = 2f * MathF.PI;
    public const float Epsilon = 1e-6f;

    /// <summary>
    /// Squared distance between two points (avoids sqrt for performance).
    /// </summary>
    public static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// Euclidean distance between two points.
    /// </summary>
    public static float Distance(float x1, float y1, float x2, float y2)
        => MathF.Sqrt(DistanceSquared(x1, y1, x2, y2));

    /// <summary>
    /// Squared magnitude of a 2D vector.
    /// </summary>
    public static float LengthSquared(float x, float y)
        => x * x + y * y;

    /// <summary>
    /// Magnitude of a 2D vector.
    /// </summary>
    public static float Length(float x, float y)
        => MathF.Sqrt(x * x + y * y);

    /// <summary>
    /// Normalizes a 2D vector in place. Returns true if successful.
    /// If magnitude is too small, returns false and sets to zero.
    /// </summary>
    public static bool TryNormalize(ref float x, ref float y)
    {
        float len = Length(x, y);
        if (len < Epsilon)
        {
            x = y = 0f;
            return false;
        }

        float invLen = 1f / len;
        x *= invLen;
        y *= invLen;
        return true;
    }

    /// <summary>
    /// Returns a normalized copy of the vector, or (0,0) if too small.
    /// </summary>
    public static (float x, float y) Normalize(float x, float y)
    {
        float len = Length(x, y);
        if (len < Epsilon)
            return (0f, 0f);

        float invLen = 1f / len;
        return (x * invLen, y * invLen);
    }

    /// <summary>
    /// Clamps a vector's magnitude to maxLength.
    /// </summary>
    public static (float x, float y) ClampMagnitude(float x, float y, float maxLength)
    {
        float lenSq = LengthSquared(x, y);
        if (lenSq <= maxLength * maxLength)
            return (x, y);

        float len = MathF.Sqrt(lenSq);
        float scale = maxLength / len;
        return (x * scale, y * scale);
    }

    /// <summary>
    /// Dot product of two 2D vectors.
    /// </summary>
    public static float Dot(float x1, float y1, float x2, float y2)
        => x1 * x2 + y1 * y2;

    /// <summary>
    /// 2D cross product (returns scalar, z-component of 3D cross).
    /// </summary>
    public static float Cross(float x1, float y1, float x2, float y2)
        => x1 * y2 - y1 * x2;

    /// <summary>
    /// Angle of a vector in radians (-PI to PI).
    /// </summary>
    public static float Angle(float x, float y)
        => MathF.Atan2(y, x);

    /// <summary>
    /// Calculates the shortest angular difference between two angles in radians.
    /// Returns a value in [-PI, PI].
    /// </summary>
    public static float AngleDifference(float angle1, float angle2)
    {
        float diff = angle2 - angle1;
        // Normalize to [-PI, PI]
        while (diff > Pi) diff -= TwoPi;
        while (diff < -Pi) diff += TwoPi;
        return diff;
    }

    /// <summary>
    /// Checks if a target direction is within the field of view cone.
    /// </summary>
    /// <param name="forwardX">Forward direction X (heading)</param>
    /// <param name="forwardY">Forward direction Y (heading)</param>
    /// <param name="targetX">Target direction X (to neighbor)</param>
    /// <param name="targetY">Target direction Y (to neighbor)</param>
    /// <param name="fovDegrees">Field of view in degrees (e.g., 270)</param>
    /// <returns>True if target is within FOV cone</returns>
    public static bool IsWithinFieldOfView(
        float forwardX, float forwardY,
        float targetX, float targetY,
        float fovDegrees)
    {
        // Full 360° FOV = omnidirectional (no filtering)
        if (fovDegrees >= 360f)
            return true;

        // Zero-length vectors can't have a direction
        if (LengthSquared(forwardX, forwardY) < Epsilon || LengthSquared(targetX, targetY) < Epsilon)
            return false;

        // Calculate angle difference using dot product (more efficient than atan2)
        // cos(angle) = dot(a, b) / (|a| * |b|)
        float dot = Dot(forwardX, forwardY, targetX, targetY);
        float magProduct = Length(forwardX, forwardY) * Length(targetX, targetY);
        float cosAngle = dot / magProduct;

        // Convert FOV to radians and get half-angle
        float halfFovRadians = (fovDegrees * MathF.PI / 180f) * 0.5f;
        float cosHalfFov = MathF.Cos(halfFovRadians);

        // If cos(angle) >= cos(halfFov), then angle <= halfFov (target is within cone)
        return cosAngle >= cosHalfFov;
    }

    /// <summary>
    /// Rotates a vector by angle in radians.
    /// </summary>
    public static (float x, float y) Rotate(float x, float y, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return (x * cos - y * sin, x * sin + y * cos);
    }

    /// <summary>
    /// Linear interpolation between two values.
    /// </summary>
    public static float Lerp(float a, float b, float t)
        => a + (b - a) * t;

    /// <summary>
    /// Clamps value to [min, max].
    /// </summary>
    public static float Clamp(float value, float min, float max)
        => Math.Clamp(value, min, max);

    /// <summary>
    /// Clamps value to [0, 1].
    /// </summary>
    public static float Clamp01(float value)
        => Math.Clamp(value, 0f, 1f);

    /// <summary>
    /// Wraps a value to [0, max).
    /// </summary>
    public static float Wrap(float value, float max)
    {
        value %= max;
        if (value < 0f)
            value += max;
        return value;
    }

    /// <summary>
    /// Wraps coordinates to [0, width) and [0, height) for toroidal world.
    /// </summary>
    public static (float x, float y) WrapPosition(float x, float y, float width, float height)
        => (Wrap(x, width), Wrap(y, height));

    /// <summary>
    /// Reflects a coordinate off a boundary if it exceeds [0, max].
    /// Also inverts the corresponding velocity component.
    /// </summary>
    public static void ReflectPosition(ref float pos, ref float vel, float max)
    {
        if (pos < 0f)
        {
            pos = -pos;
            vel = MathF.Abs(vel);
        }
        else if (pos > max)
        {
            pos = 2f * max - pos;
            vel = -MathF.Abs(vel);
        }
    }

    /// <summary>
    /// Smoothstep interpolation (cubic Hermite).
    /// </summary>
    public static float SmoothStep(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Smootherstep interpolation (quintic).
    /// </summary>
    public static float SmootherStep(float t)
    {
        t = Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>
    /// Fast inverse square root approximation (Quake III algorithm).
    /// Good enough for normalization when speed matters more than precision.
    /// </summary>
    public static float FastInvSqrt(float x)
    {
        // Modern C# JIT is pretty good, so we'll use normal sqrt for now
        // If profiling shows this is a bottleneck, we can add the bit-hack version
        return 1f / MathF.Sqrt(x);
    }

    /// <summary>
    /// Applies steering force to velocity with max force and max speed constraints.
    /// Returns the new velocity vector.
    /// </summary>
    public static (float vx, float vy) ApplySteeringForce(
        float vx, float vy,
        float fx, float fy,
        float maxForce,
        float maxSpeed,
        float dt)
    {
        // Clamp force magnitude
        (fx, fy) = ClampMagnitude(fx, fy, maxForce);

        // Integrate velocity
        vx += fx * dt;
        vy += fy * dt;

        // Clamp speed
        return ClampMagnitude(vx, vy, maxSpeed);
    }
}
