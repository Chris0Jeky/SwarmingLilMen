namespace SwarmSim.Core.Metrics;

/// <summary>Foundation-neutral 2D position and velocity for one member of an analysis population.</summary>
public readonly record struct KinematicSample(float X, float Y, float Vx, float Vy);

/// <summary>Distance/centroid topology. The default value is unbounded Euclidean space.</summary>
public readonly struct MetricDomain
{
    private MetricDomain(float width, float height)
    {
        Width = width;
        Height = height;
        IsPeriodic = true;
    }

    public bool IsPeriodic { get; }
    public float Width { get; }
    public float Height { get; }
    public static MetricDomain Euclidean => default;

    public static MetricDomain Toroidal(float width, float height)
    {
        if (!float.IsFinite(width) || width <= 0f)
            throw new ArgumentOutOfRangeException(nameof(width), "Periodic width must be finite and positive.");
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height), "Periodic height must be finite and positive.");
        return new(width, height);
    }
}

/// <summary>
/// A borrowed, finite snapshot. Callers select live members and keep the backing storage stable
/// until sampling finishes. No engine neighbor list, FOV, cell size, or MaxNeighbors enters this view.
/// </summary>
public readonly ref struct MetricFrame
{
    public MetricFrame(ReadOnlySpan<KinematicSample> agents, MetricDomain domain)
    {
        foreach (KinematicSample agent in agents)
            if (!float.IsFinite(agent.X) || !float.IsFinite(agent.Y) || !float.IsFinite(agent.Vx) || !float.IsFinite(agent.Vy))
                throw new ArgumentException("Analysis positions and velocities must be finite.", nameof(agents));
        Agents = agents;
        Domain = domain;
    }

    public ReadOnlySpan<KinematicSample> Agents { get; }
    public MetricDomain Domain { get; }
    public int Count => Agents.Length;
}

internal static class MetricMath
{
    internal static double CenteredRemainder(double coordinate, double extent)
    {
        // Keep tiny negative coordinates near zero. Adding a large extent before
        // subtracting positions or constructing angles can erase their significant bits.
        double result = coordinate % extent;
        if (result > extent * 0.5d) result -= extent;
        else if (result < -extent * 0.5d) result += extent;
        return result;
    }

    internal static void RequireOutput(Span<double> output, int count)
    {
        if (output.Length < count)
            throw new ArgumentException($"At least {count} output values are required.", nameof(output));
    }
}
