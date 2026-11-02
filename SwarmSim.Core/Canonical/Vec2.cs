using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Canonical;

public readonly struct Vec2
{
    public float X { get; }
    public float Y { get; }

    public Vec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 Zero => new(0f, 0f);

    public float LengthSquared => X * X + Y * Y;
    public float Length => MathF.Sqrt(LengthSquared);

    public bool IsNearlyZero(float epsilon = 1e-6f) => LengthSquared <= epsilon * epsilon;

    public Vec2 Normalized
    {
        get
        {
            float len = Length;
            if (!float.IsFinite(len) && float.IsFinite(X) && float.IsFinite(Y))
            {
                // A finite vector can overflow its squared float norm without losing its
                // direction. Keep the recovered norm in double even beyond float's range.
                double wideLength = Math.Sqrt((double)X * X + (double)Y * Y);
                return new Vec2((float)(X / wideLength), (float)(Y / wideLength));
            }
            return len <= 1e-6f ? Zero : new Vec2(X / len, Y / len);
        }
    }

    public Vec2 WithLength(float length)
    {
        if (IsNearlyZero()) return Zero;
        float len = Length;
        if (!float.IsFinite(len) && float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(length))
        {
            double wideLength = Math.Sqrt((double)X * X + (double)Y * Y);
            return new Vec2((float)(X / wideLength * length), (float)(Y / wideLength * length));
        }
        // Retain the ordinary divide-then-multiply rounding and nonfinite target behavior.
        return new Vec2(X / len * length, Y / len * length);
    }

    public Vec2 ClampMagnitude(float maxLength)
    {
        if (maxLength <= 0f)
            return Zero;

        float sqr = LengthSquared;
        float maxSqr = maxLength * maxLength;

        if (!float.IsFinite(sqr) && float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(maxLength))
        {
            // Finite components can overflow both squared floats, making their comparison
            // inconclusive. Keep the exceptional scale in double, including for tiny caps.
            double wideSqr = (double)X * X + (double)Y * Y;
            double wideMaxSqr = (double)maxLength * maxLength;
            if (wideSqr <= wideMaxSqr)
                return this;

            double wideScale = maxLength / Math.Sqrt(wideSqr);
            return new Vec2((float)(X * wideScale), (float)(Y * wideScale));
        }

        if (sqr <= maxSqr)
            return this;

        float invLen = 1f / MathF.Sqrt(sqr);
        float scale = maxLength * invLen;
        return new Vec2(X * scale, Y * scale);
    }

    public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

    /// <summary>
    /// Returns the shortest toroidal displacement from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <param name="from">Starting position.</param>
    /// <param name="to">Destination position.</param>
    /// <param name="worldWidth">Positive world width, or positive infinity for an unbounded axis.</param>
    /// <param name="worldHeight">Positive world height, or positive infinity for an unbounded axis.</param>
    /// <returns>The minimum-image displacement.</returns>
    /// <summary>
    /// Returns the shortest toroidal displacement from <paramref name="from"/> to <paramref name="to"/>.
    /// Both points are expected to already lie in <c>[0, extent)</c>; this subtracts them directly
    /// rather than re-normalizing, because it runs once per neighbor per rule.
    /// <see cref="CanonicalWorld"/> guarantees that invariant by normalizing on spawn and on every
    /// integration step. Passing raw out-of-range coordinates here can round differently from
    /// <see cref="ISpatialIndex"/>, which normalizes first, and make perception and steering disagree.
    /// </summary>
    public static Vec2 MinimumImageDelta(Vec2 from, Vec2 to, float worldWidth, float worldHeight)
    {
        if (float.IsNaN(worldWidth) || worldWidth <= 0f)
            throw new ArgumentOutOfRangeException(nameof(worldWidth), "World width must be positive.");
        if (float.IsNaN(worldHeight) || worldHeight <= 0f)
            throw new ArgumentOutOfRangeException(nameof(worldHeight), "World height must be positive.");

        return new Vec2(
            MathUtils.MinimumImageDelta(to.X - from.X, worldWidth),
            MathUtils.MinimumImageDelta(to.Y - from.Y, worldHeight));
    }

    public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
    {
        t = t < 0f ? 0f : (t > 1f ? 1f : t);
        return new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 v) => new(-v.X, -v.Y);
    public static Vec2 operator *(Vec2 v, float scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vec2 operator *(float scalar, Vec2 v) => v * scalar;
    public static Vec2 operator /(Vec2 v, float scalar) => new(v.X / scalar, v.Y / scalar);

}
