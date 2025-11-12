namespace SwarmSim.Core.Canonical;

public sealed class CohesionRule : IRule
{
    private readonly float _weight;

    public CohesionRule(float weight)
    {
        _weight = MathF.Max(weight, 0f);
    }

    public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighborIndices, ReadOnlySpan<float> neighborWeights, RuleContext context)
    {
        if (neighborIndices.IsEmpty)
            return Vec2.Zero;

        Vec2 center = Vec2.Zero;
        float totalWeight = 0f;

        for (int i = 0; i < neighborIndices.Length; i++)
        {
            int neighborIndex = neighborIndices[i];
            float weight = neighborWeights.Length > i ? neighborWeights[i] : 1f;
            if (weight <= 0f)
                continue;

            center += boids[neighborIndex].Position * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return Vec2.Zero;

        Vec2 averageCenter = center / totalWeight;
        Vec2 toCenter = averageCenter - self.Position;

        if (toCenter.IsNearlyZero())
            return Vec2.Zero;

        Vec2 desired = toCenter.WithLength(context.TargetSpeed * _weight);
        Vec2 steer = desired - self.Velocity;
        return steer;
    }
}
