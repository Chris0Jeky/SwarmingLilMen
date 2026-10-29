namespace SwarmSim.Core.Metrics;

/// <summary>O(N) population, mean speed, polarization, and domain-aware centroid.</summary>
public sealed class MotionMetricProbe : IMetricProbe
{
    private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
        { "population", "meanSpeed", "polarization", "centroidX", "centroidY" });

    public IReadOnlyList<string> Channels => Names;
    public bool IsDeterministic => true;

    public void Sample(in MetricFrame frame, Span<double> output)
    {
        MetricMath.RequireOutput(output, Names.Count);
        output[0] = frame.Count;
        if (frame.Count == 0)
        {
            output[1] = output[2] = 0d;
            output[3] = output[4] = double.NaN;
            return;
        }

        double speedSum = 0d, headingX = 0d, headingY = 0d;
        double xSum = 0d, ySum = 0d, xSin = 0d, ySin = 0d, xCos = 0d, yCos = 0d;
        foreach (KinematicSample agent in frame.Agents)
        {
            double speed = Math.Sqrt((double)agent.Vx * agent.Vx + (double)agent.Vy * agent.Vy);
            speedSum += speed;
            if (speed > 0d)
            {
                headingX += agent.Vx / speed;
                headingY += agent.Vy / speed;
            }
            if (frame.Domain.IsPeriodic)
            {
                (double sinX, double cosX) = Math.SinCos(2d * Math.PI * MetricMath.CenteredRemainder(agent.X, frame.Domain.Width) / frame.Domain.Width);
                (double sinY, double cosY) = Math.SinCos(2d * Math.PI * MetricMath.CenteredRemainder(agent.Y, frame.Domain.Height) / frame.Domain.Height);
                xSin += sinX;
                xCos += cosX;
                ySin += sinY;
                yCos += cosY;
            }
            else
            {
                xSum += agent.X;
                ySum += agent.Y;
            }
        }

        output[1] = speedSum / frame.Count;
        // All members are in the denominator; stationary members contribute a zero unit heading.
        output[2] = Math.Clamp(Math.Sqrt(headingX * headingX + headingY * headingY) / frame.Count, 0d, 1d);
        output[3] = frame.Domain.IsPeriodic ? CircularMean(xSin, xCos, frame.Count, frame.Domain.Width) : xSum / frame.Count;
        output[4] = frame.Domain.IsPeriodic ? CircularMean(ySin, yCos, frame.Count, frame.Domain.Height) : ySum / frame.Count;
    }

    private static double CircularMean(double sinSum, double cosSum, int count, double extent)
    {
        // An axis with effectively zero mean resultant has no distinguished mean angle.
        if (Math.Sqrt(sinSum * sinSum + cosSum * cosSum) <= 1e-12d * count)
            return double.NaN;
        double angle = Math.Atan2(sinSum, cosSum);
        if (angle < 0d) angle += 2d * Math.PI;
        double result = extent * (angle / (2d * Math.PI));
        return result >= extent ? 0d : result;
    }
}
