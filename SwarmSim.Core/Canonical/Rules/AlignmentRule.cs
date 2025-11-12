namespace SwarmSim.Core.Canonical;

public sealed class AlignmentRule : IRule
{
    private readonly float _weight;

    public AlignmentRule(float weight)
    {
        _weight = MathF.Max(weight, 0f);
    }

    public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighborIndices, ReadOnlySpan<float> neighborWeights, RuleContext context)
    {
        if (neighborIndices.IsEmpty)
            return Vec2.Zero;

        Vec2 averageVelocity = Vec2.Zero;
        float totalWeight = 0f;

        for (int i = 0; i < neighborIndices.Length; i++)
        {
            int neighborIndex = neighborIndices[i];
            float weight = neighborWeights.Length > i ? neighborWeights[i] : 1f;
            if (weight <= 0f)
                continue;

            averageVelocity += boids[neighborIndex].Velocity * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return Vec2.Zero;

        averageVelocity = averageVelocity / totalWeight;

        if (averageVelocity.IsNearlyZero())
            return Vec2.Zero;

        Vec2 desired = averageVelocity.WithLength(context.TargetSpeed * _weight);
        Vec2 steer = desired - self.Velocity;
        Vec2 clamped = steer.ClampMagnitude(context.MaxForce);
        context.Instrumentation?.RecordAlignment(selfIndex, clamped.Length);
        return clamped;
    }
}
