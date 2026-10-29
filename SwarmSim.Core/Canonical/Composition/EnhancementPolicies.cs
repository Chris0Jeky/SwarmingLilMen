namespace SwarmSim.Core.Canonical;

public sealed class WhiskerAvoidancePolicy(float timeHorizon, float radius, float weight) : ILocalSteeringPolicy
{
    public PolicyMetadata Metadata { get; } = new(CanonicalPolicyIds.Whisker, SteeringRole.Whisker,
        priority: 0, requiresRemainingForce: true);
    public int MaxIntents => 1;

    public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
    {
        int hitCount = 0;
        if (!observation.Neighbors.IsEmpty)
        {
            Vec2 forward = observation.Self.Forward;
            float lookAhead = context.TargetSpeed * MathF.Max(0.05f, timeHorizon);
            float whiskerRadius = MathF.Max(0.1f, radius);
            Vec2 right = new Vec2(forward.Y, -forward.X);
            Vec2 whiskerAccum = Vec2.Zero;
            foreach (ref readonly NeighborView neighbor in observation.Neighbors)
            {
                Vec2 toN = neighbor.RelativePosition;
                float along = Vec2.Dot(forward, toN);
                if (along <= 0f || along > lookAhead) continue;
                float lateral = Vec2.Dot(right, toN);
                float absLat = MathF.Abs(lateral);
                if (absLat > whiskerRadius) continue;
                hitCount++;
                float side = lateral >= 0f ? 1f : -1f;
                float gain = (1f - absLat / whiskerRadius) * (1f - along / lookAhead);
                whiskerAccum += right * (side * gain);
            }
            if (!whiskerAccum.IsNearlyZero())
            {
                Vec2 desired = whiskerAccum.WithLength(context.TargetSpeed * weight);
                Vec2 steerW = desired - observation.Self.Velocity;
                intents.AddForce(steerW);
            }
        }
        if (!context.WhiskerHits.IsEmpty) context.WhiskerHits[0] = hitCount;
    }
}

/// <summary>Uses the world's compact, independently named per-agent PCG state.</summary>
public sealed class WanderPolicy(float strength, float rate) : ILocalSteeringPolicy
{
    internal bool UsesRandom => strength > 0f;
    public PolicyMetadata Metadata { get; } = new(CanonicalPolicyIds.Wander, SteeringRole.Wander,
        priority: 100, requiresRemainingForce: true);
    public int MaxIntents => 1;

    public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents)
    {
        if (strength <= 0f) return;
        if (context.WanderRandom.IsEmpty || context.WanderAngle.IsEmpty)
            throw new InvalidOperationException("WanderPolicy requires world-owned per-agent state.");
        float angleChange = (-1f + context.WanderRandom[0].NextFloat() * 2f) * rate * context.DeltaTime;
        context.WanderAngle[0] += angleChange;
        Vec2 wanderDirection = new Vec2(MathF.Cos(context.WanderAngle[0]), MathF.Sin(context.WanderAngle[0]));
        Vec2 wander = wanderDirection * strength * context.TargetSpeed;
        intents.AddForce(wander);
    }
}
