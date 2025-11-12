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
            return len <= 1e-6f ? Zero : new Vec2(X / len, Y / len);
        }
    }

    public Vec2 WithLength(float length) => IsNearlyZero() ? Zero : new Vec2(X / Length * length, Y / Length * length);

    public Vec2 ClampMagnitude(float maxLength)
    {
        if (maxLength <= 0f)
            return Zero;

        float sqr = LengthSquared;
        float maxSqr = maxLength * maxLength;

        if (sqr <= maxSqr)
            return this;

        float invLen = 1f / MathF.Sqrt(sqr);
        float scale = maxLength * invLen;
        return new Vec2(X * scale, Y * scale);
    }

    public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

    public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
    {
        t = MathF.Clamp(t, 0f, 1f);
        return new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 v) => new(-v.X, -v.Y);
    public static Vec2 operator *(Vec2 v, float scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vec2 operator *(float scalar, Vec2 v) => v * scalar;
    public static Vec2 operator /(Vec2 v, float scalar) => new(v.X / scalar, v.Y / scalar);
}
