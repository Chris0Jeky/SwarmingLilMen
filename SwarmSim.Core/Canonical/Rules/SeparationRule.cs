namespace SwarmSim.Core.Canonical;

public sealed class SeparationRule : IRule, ILocalSteeringPolicy
{
    private readonly float _weight;
    private readonly float _radius;
    private readonly float _radiusSq;

    public PolicyMetadata Metadata { get; }
    public int MaxIntents => 1;

    public SeparationRule(float weight, float radius) : this(weight, radius, null) { }

    public SeparationRule(float weight, float radius, PolicyMetadata? metadata)
    {
        _weight = MathF.Max(weight, 0f);
        _radius = MathF.Max(radius, 0.0001f);
        _radiusSq = _radius * _radius;
        Metadata = metadata ?? new PolicyMetadata(CanonicalPolicyIds.Separation, SteeringRole.Separation,
            priority: 20, reserveRemainingForce: true);
    }

    public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents) =>
        intents.AddForce(ComputeLocal(observation, context));

    private Vec2 ComputeLocal(in LocalObservation observation, in LocalPolicyContext context)
    {
        if (observation.Neighbors.IsEmpty) return Vec2.Zero;
        Vec2 accumulator = Vec2.Zero;
        foreach (ref readonly NeighborView neighbor in observation.Neighbors)
        {
            float weight = neighbor.FieldOfViewWeight;
            if (weight <= 0f) continue;
            Vec2 delta = neighbor.ReverseRelativePosition;
            float distSq = delta.LengthSquared;
            if (distSq <= 0f || distSq > _radiusSq) continue;
            float dist = MathF.Sqrt(distSq);
            if (dist <= 1e-4f) continue;
            Vec2 direction = delta / dist;
            float strength = MathF.Max(0f, 1f - dist / _radius);
            float influence = strength / dist * weight;
            accumulator += direction * influence;
        }
        if (accumulator.IsNearlyZero()) return Vec2.Zero;
        Vec2 desired = accumulator.WithLength(context.TargetSpeed * _weight * context.SeparationPriorityBoost);
        Vec2 steer = desired - observation.Self.Velocity;
        return steer;
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

            Vec2 delta = context.MinimumImageDelta(boids[neighborIndex].Position, self.Position);
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
