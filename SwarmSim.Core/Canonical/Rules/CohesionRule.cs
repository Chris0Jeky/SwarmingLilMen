namespace SwarmSim.Core.Canonical;

public sealed class CohesionRule : IRule
{
    private readonly float _weight;

    public CohesionRule(float weight)
    {
        _weight = MathF.Max(weight, 0f);
    }

    public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighborIndices, RuleContext context)
    {
        if (neighborIndices.IsEmpty)
            return Vec2.Zero;

        Vec2 center = Vec2.Zero;
        foreach (int neighborIndex in neighborIndices)
        {
            center += boids[neighborIndex].Position;
        }

        float count = neighborIndices.Length;
        Vec2 averageCenter = center / count;
        Vec2 toCenter = averageCenter - self.Position;

        if (toCenter.IsNearlyZero())
            return Vec2.Zero;

        Vec2 desired = toCenter.WithLength(context.TargetSpeed * _weight);
        Vec2 steer = desired - self.Velocity;
        return steer.ClampMagnitude(context.MaxForce);
    }
}
