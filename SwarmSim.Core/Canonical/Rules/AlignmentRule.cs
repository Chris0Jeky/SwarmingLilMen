namespace SwarmSim.Core.Canonical;

public sealed class AlignmentRule : IRule
{
    private readonly float _weight;

    public AlignmentRule(float weight)
    {
        _weight = MathF.Max(weight, 0f);
    }

    public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighborIndices, RuleContext context)
    {
        if (neighborIndices.IsEmpty)
            return Vec2.Zero;

        Vec2 averageVelocity = Vec2.Zero;
        foreach (int neighborIndex in neighborIndices)
        {
            averageVelocity += boids[neighborIndex].Velocity;
        }

        if (averageVelocity.IsNearlyZero())
            return Vec2.Zero;

        Vec2 desired = averageVelocity.WithLength(context.TargetSpeed * _weight);
        Vec2 steer = desired - self.Velocity;
        return steer.ClampMagnitude(context.MaxForce);
    }
}
