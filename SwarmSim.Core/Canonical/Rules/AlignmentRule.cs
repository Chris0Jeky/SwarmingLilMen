namespace SwarmSim.Core.Canonical;

public sealed class AlignmentRule : IRule, ILocalSteeringPolicy
{
    private readonly float _weight;

    public PolicyMetadata Metadata { get; }
    public int MaxIntents => 1;

    public AlignmentRule(float weight) : this(weight, null) { }

    public AlignmentRule(float weight, PolicyMetadata? metadata)
    {
        _weight = MathF.Max(weight, 0f);
        Metadata = metadata ?? new PolicyMetadata(CanonicalPolicyIds.Alignment, SteeringRole.Alignment,
            priority: 30, priorityAttenuation: 0.7f);
    }

    public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents) =>
        intents.AddForce(ComputeLocal(observation, context));

    private Vec2 ComputeLocal(in LocalObservation observation, in LocalPolicyContext context)
    {
        if (observation.Neighbors.IsEmpty) return Vec2.Zero;
        Vec2 averageVelocity = Vec2.Zero;
        float totalWeight = 0f;
        foreach (ref readonly NeighborView neighbor in observation.Neighbors)
        {
            float weight = neighbor.FieldOfViewWeight;
            if (weight <= 0f) continue;
            averageVelocity += neighbor.Velocity * weight;
            totalWeight += weight;
        }
        if (totalWeight <= 0f) return Vec2.Zero;
        averageVelocity = averageVelocity / totalWeight;
        if (averageVelocity.IsNearlyZero()) return Vec2.Zero;
        Vec2 desired = averageVelocity.WithLength(context.TargetSpeed * _weight);
        Vec2 steer = desired - observation.Self.Velocity;
        return steer;
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
        return steer;
    }
}
