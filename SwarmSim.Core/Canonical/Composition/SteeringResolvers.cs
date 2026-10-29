namespace SwarmSim.Core.Canonical;

/// <summary>Resolves intents in the pipeline's stable priority order.</summary>
public interface ISteeringResolver
{
    /// <returns>The magnitude actually added by this intent, for researcher instrumentation.</returns>
    float Accumulate(in SteeringIntent intent, float priorityBlend, float maxForce,
        ref Vec2 total, ref float remainingForce);
}

/// <summary>The enhanced pipeline's original shared budget, attenuation and separation reservation.</summary>
public sealed class PriorityForceBudgetResolver : ISteeringResolver
{
    public float Accumulate(in SteeringIntent intent, float priorityBlend, float maxForce,
        ref Vec2 total, ref float remainingForce)
    {
        Vec2 contribution = WeightedForce(intent, priorityBlend);
        if (remainingForce <= 0f) return 0f;
        float lengthSq = contribution.LengthSquared;
        if (lengthSq <= 1e-6f) return 0f;
        float length = MathF.Sqrt(lengthSq);
        float spend = MathF.Min(length, remainingForce);
        Vec2 normalized = contribution / length;
        Vec2 clamped = normalized * spend;
        total += clamped;
        remainingForce -= spend;
        if (intent.Metadata.ReserveRemainingForce) remainingForce = 0f;
        return spend;
    }

    internal static Vec2 WeightedForce(in SteeringIntent intent, float priorityBlend)
    {
        Vec2 contribution = intent.Payload;
        // Preserve the exact old expression/order for the built-in alignment/cohesion path.
        if (intent.Metadata.Weight != 1f) contribution *= intent.Metadata.Weight;
        if (intent.Metadata.PriorityAttenuation != 0f)
        {
            float attenuation = 1f - (priorityBlend * intent.Metadata.PriorityAttenuation);
            contribution *= attenuation;
        }
        if (!float.IsFinite(contribution.X) || !float.IsFinite(contribution.Y)
            || !float.IsFinite(contribution.LengthSquared))
            throw new ArgumentException("The weighted force must have a finite squared magnitude.", nameof(intent));
        return contribution;
    }
}

/// <summary>
/// Classic Reynolds: clamp each contribution independently, then sum. No shared post-clamp is
/// applied; the total can deliberately exceed MaxForce. Reservation/attenuation metadata is not
/// used by this resolver, but the explicit output weight still applies.
/// </summary>
public sealed class IndependentClampSumResolver : ISteeringResolver
{
    public float Accumulate(in SteeringIntent intent, float priorityBlend, float maxForce,
        ref Vec2 total, ref float remainingForce)
    {
        Vec2 contribution = PriorityForceBudgetResolver.WeightedForce(intent, 0f).ClampMagnitude(maxForce);
        total += contribution;
        return contribution.Length;
    }
}
