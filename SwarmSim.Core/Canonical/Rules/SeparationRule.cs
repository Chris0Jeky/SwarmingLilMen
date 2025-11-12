namespace SwarmSim.Core.Canonical;

public sealed class SeparationRule : IRule
{
    private readonly float _weight;
    private readonly float _radius;
    private readonly float _radiusSq;

    public SeparationRule(float weight, float radius)
    {
        _weight = MathF.Max(weight, 0f);
        _radius = MathF.Max(radius, 0.0001f);
        _radiusSq = _radius * _radius;
    }

    public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighborIndices, ReadOnlySpan<float> neighborWeights, RuleContext context)
    {
        if (neighborIndices.IsEmpty)
            return Vec2.Zero;

        Vec2 accumulator = Vec2.Zero;

        for (int i = 0; i < neighborIndices.Length; i++)
        {
            int neighborIndex = neighborIndices[i];
            float weight = neighborWeights.Length > i ? neighborWeights[i] : 1f;
            if (weight <= 0f)
                continue;

            Vec2 delta = self.Position - boids[neighborIndex].Position;
            float distSq = delta.LengthSquared;

            if (distSq <= 0f || distSq > _radiusSq)
                continue;

            float dist = MathF.Sqrt(distSq);
            if (dist <= 1e-4f)
                continue;

            Vec2 direction = delta / dist;
            float strength = MathF.Max(0f, 1f - dist / _radius);
            float influence = strength / dist * weight;
            accumulator += direction * influence;
        }

        if (accumulator.IsNearlyZero())
            return Vec2.Zero;

        Vec2 desired = accumulator.WithLength(context.TargetSpeed * _weight * context.SeparationPriorityBoost);
        Vec2 steer = desired - self.Velocity;
        return steer;
    }
}
