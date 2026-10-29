using SwarmSim.Core.Metrics;

namespace SwarmSim.Parity;

public sealed class ParityGeometryProbe : IMetricProbe
{
    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[] {
        "headingCircularVariance", "turnRateMean", "turnRateP50", "turnRateP95",
        "flockRadius", "neighborCountMean", "neighborCountP50", "neighborCountP95", "collisionPairs", "minSpeed", "maxSpeed" });
    private readonly KinematicSample[] _previous;
    private readonly double[] _turns;
    private readonly double[] _neighbors;
    private readonly double _radiusSquared;
    private readonly double _dt;
    private int _previousCount = -1;

    public ParityGeometryProbe(int capacity, float senseRadius, float deltaTime)
    {
        if (capacity < 0 || !float.IsFinite(senseRadius) || senseRadius < 0 ||
            !float.IsFinite(deltaTime) || deltaTime <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _previous = new KinematicSample[capacity];
        _turns = new double[capacity];
        _neighbors = new double[capacity];
        _radiusSquared = (double)senseRadius * senseRadius;
        _dt = deltaTime;
    }

    public IReadOnlyList<string> Channels => Names;
    public bool IsDeterministic => true;

    public void SetPrevious(ReadOnlySpan<KinematicSample> agents)
    {
        if (agents.Length > _previous.Length) throw new ArgumentException("Previous population exceeds capacity.");
        foreach (KinematicSample agent in agents)
            if (!float.IsFinite(agent.Vx) || !float.IsFinite(agent.Vy)) throw new ArgumentException("Previous velocity must be finite.");
        agents.CopyTo(_previous);
        _previousCount = agents.Length;
    }

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        if (output.Length < Names.Count) throw new ArgumentException("Output does not hold the complete schema.");
        if (frame.Count != _previousCount) throw new InvalidOperationException("Prepare the matching fixed-identity previous population first.");
        int count = frame.Count, turns = 0;
        output[..Names.Count].Fill(double.NaN);
        if (count == 0) { output[5] = output[8] = 0; return; }
        Array.Clear(_neighbors, 0, count);
        double hx = 0, hy = 0, turnSum = 0, minSpeed = double.PositiveInfinity, maxSpeed = 0;
        double sx = 0, cx = 0, sy = 0, cy = 0, linearX = 0, linearY = 0, collisionPairs = 0;
        for (int i = 0; i < count; i++)
        {
            KinematicSample current = frame.Agents[i], previous = _previous[i];
            double vx = current.Vx, vy = current.Vy;
            double speed = Math.Sqrt(vx * vx + vy * vy);
            minSpeed = Math.Min(minSpeed, speed); maxSpeed = Math.Max(maxSpeed, speed);
            if (speed > 0) { hx += vx / speed; hy += vy / speed; }
            if (speed > 0 && (previous.Vx != 0 || previous.Vy != 0))
            {
                double angle = Math.Abs(Math.Atan2((double)previous.Vx * vy - (double)previous.Vy * vx,
                    (double)previous.Vx * vx + (double)previous.Vy * vy)) / _dt;
                _turns[turns++] = angle; turnSum += angle;
            }
            if (frame.Domain.IsPeriodic)
            {
                (double ax, double bx) = Math.SinCos(2 * Math.PI * (current.X % frame.Domain.Width) / frame.Domain.Width);
                (double ay, double by) = Math.SinCos(2 * Math.PI * (current.Y % frame.Domain.Height) / frame.Domain.Height);
                sx += ax; cx += bx; sy += ay; cy += by;
            }
            else { linearX += current.X; linearY += current.Y; }
            for (int j = i + 1; j < count; j++)
            {
                KinematicSample other = frame.Agents[j];
                double dx = Distance(current.X, other.X, frame.Domain.IsPeriodic ? frame.Domain.Width : 0);
                double dy = Distance(current.Y, other.Y, frame.Domain.IsPeriodic ? frame.Domain.Height : 0);
                double squared = dx * dx + dy * dy;
                if (squared <= _radiusSquared) { _neighbors[i]++; _neighbors[j]++; }
                if (squared <= 1d) collisionPairs++;
            }
        }
        output[0] = 1d - Math.Clamp(Math.Sqrt(hx * hx + hy * hy) / count, 0, 1);
        if (turns > 0)
        {
            Array.Sort(_turns, 0, turns);
            output[1] = turnSum / turns; output[2] = Quantile(_turns, turns, 0.5); output[3] = Quantile(_turns, turns, 0.95);
        }
        double centerX = frame.Domain.IsPeriodic ? Center(sx, cx, count, frame.Domain.Width) : linearX / count;
        double centerY = frame.Domain.IsPeriodic ? Center(sy, cy, count, frame.Domain.Height) : linearY / count;
        if (double.IsFinite(centerX) && double.IsFinite(centerY))
        {
            double radiusSum = 0;
            foreach (KinematicSample current in frame.Agents)
            {
                double dx = Distance(current.X, centerX, frame.Domain.IsPeriodic ? frame.Domain.Width : 0);
                double dy = Distance(current.Y, centerY, frame.Domain.IsPeriodic ? frame.Domain.Height : 0);
                radiusSum += dx * dx + dy * dy;
            }
            output[4] = Math.Sqrt(radiusSum / count);
        }
        double neighborSum = 0;
        for (int i = 0; i < count; i++) neighborSum += _neighbors[i];
        Array.Sort(_neighbors, 0, count);
        output[5] = neighborSum / count; output[6] = Quantile(_neighbors, count, 0.5); output[7] = Quantile(_neighbors, count, 0.95);
        output[8] = collisionPairs; output[9] = minSpeed; output[10] = maxSpeed;
    }

    private static double Center(double sin, double cos, int count, double extent)
    {
        if (Math.Sqrt(sin * sin + cos * cos) <= 1e-12 * count) return double.NaN;
        double angle = Math.Atan2(sin, cos);
        if (angle < 0) angle += 2 * Math.PI;
        return extent * angle / (2 * Math.PI);
    }

    private static double Distance(double from, double to, double extent)
    {
        double value = Math.Abs(to - from);
        if (extent == 0) return value;
        value %= extent;
        return Math.Min(value, extent - value);
    }

    private static double Quantile(double[] sorted, int count, double q)
    {
        double index = q * (count - 1);
        int lower = (int)index, upper = Math.Min(lower + 1, count - 1);
        return sorted[lower] + (index - lower) * (sorted[upper] - sorted[lower]);
    }
}
