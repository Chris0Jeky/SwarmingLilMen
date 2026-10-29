namespace SwarmSim.Core.Canonical;

public sealed class CohesionRule : IRule, ILocalSteeringPolicy
{
    private readonly float _weight;

    public PolicyMetadata Metadata { get; }
    public int MaxIntents => 1;

    public CohesionRule(float weight) : this(weight, null) { }

    public CohesionRule(float weight, PolicyMetadata? metadata)
    {
        _weight = MathF.Max(weight, 0f);
        Metadata = metadata ?? new PolicyMetadata(CanonicalPolicyIds.Cohesion, SteeringRole.Cohesion,
            priority: 40, priorityAttenuation: 0.7f);
    }

    public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents) =>
        intents.AddForce(ComputeLocal(observation, context));

    private Vec2 ComputeLocal(in LocalObservation observation, in LocalPolicyContext context)
    {
        if (observation.Neighbors.IsEmpty) return Vec2.Zero;
        Vec2 toCenter = Vec2.Zero;
        float totalWeight = 0f;
        foreach (ref readonly NeighborView neighbor in observation.Neighbors)
        {
            float weight = neighbor.FieldOfViewWeight;
            if (weight <= 0f) continue;
            toCenter += neighbor.RelativePosition * weight;
            totalWeight += weight;
        }
        if (totalWeight <= 0f) return Vec2.Zero;
        toCenter /= totalWeight;
        if (toCenter.IsNearlyZero()) return Vec2.Zero;
        Vec2 desired = toCenter.WithLength(context.TargetSpeed * _weight);
        Vec2 steer = desired - observation.Self.Velocity;
        return steer;
    }

    public Vec2 Compute(int selfIndex, Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighborIndices, ReadOnlySpan<float> neighborWeights, RuleContext context)
    {
        if (neighborIndices.IsEmpty)
            return Vec2.Zero;

        Vec2 toCenter = Vec2.Zero;
        float totalWeight = 0f;

        for (int i = 0; i < neighborIndices.Length; i++)
        {
            int neighborIndex = neighborIndices[i];
            float weight = neighborWeights.Length > i ? neighborWeights[i] : 1f;
            if (weight <= 0f)
                continue;

            toCenter += context.MinimumImageDelta(self.Position, boids[neighborIndex].Position) * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
            return Vec2.Zero;

        toCenter /= totalWeight;

        if (toCenter.IsNearlyZero())
            return Vec2.Zero;

        Vec2 desired = toCenter.WithLength(context.TargetSpeed * _weight);
        Vec2 steer = desired - self.Velocity;
        return steer;
    }
}
