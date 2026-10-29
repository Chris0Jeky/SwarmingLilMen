using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Canonical;

public static class CanonicalStageIds
{
    public const string Priority = "separation-priority";
    public const string ShapedAvoidance = "shaped-avoidance";
    public const string TurnRate = "turn-rate";
    public const string TargetSpeed = "target-speed";
}

/// <summary>The enhanced composition's optional per-agent hysteresis/hold/ramp stage.</summary>
/// <remarks>State is held by the world, not shared by stage instances or policies.</remarks>
public sealed class SeparationPriorityStage
{
    private readonly float _enterThreshold;
    private readonly float _exitThreshold;
    private readonly float _rampInTime;
    private readonly float _rampOutTime;
    private readonly float _holdTime;

    public string Id => CanonicalStageIds.Priority;
    /// <summary>Runs before policies at this priority; default whisker (0) runs first.</summary>
    public int Priority { get; }

    public SeparationPriorityStage(CanonicalWorldSettings settings, int priority = 10)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _enterThreshold = MathF.Max(0f, settings.SeparationPriorityRadiusFactor * settings.SenseRadius);
        _exitThreshold = MathF.Max(_enterThreshold, settings.SeparationPriorityExitFactor * settings.SenseRadius);
        _rampInTime = Math.Max(settings.SeparationPriorityRampInTime, 1e-6f);
        _rampOutTime = Math.Max(settings.SeparationPriorityRampOutTime, 1e-6f);
        _holdTime = Math.Max(settings.SeparationPriorityHoldTime, 0f);
        Priority = priority;
    }

    internal void Apply(int neighborCount, float nearestDistance, float deltaTime,
        ref bool active, ref float blend, ref float holdTimer)
    {
        if (active && holdTimer > 0f) holdTimer -= deltaTime;
        bool shouldEnterPriority = neighborCount > 0 && nearestDistance <= _enterThreshold;
        if (shouldEnterPriority && !active)
        {
            active = true;
            holdTimer = _holdTime;
        }
        else if (active)
        {
            bool shouldExitPriority = (neighborCount == 0 || nearestDistance >= _exitThreshold) && holdTimer <= 0f;
            if (shouldExitPriority)
            {
                active = false;
                holdTimer = 0f;
            }
        }
        float targetBlend = active ? 1f : 0f;
        float maxDelta = active ? Math.Min(1f, deltaTime / _rampInTime) : Math.Min(1f, deltaTime / _rampOutTime);
        blend = MathUtils.MoveTowards(blend, targetBlend, maxDelta);
    }
}

public readonly record struct VelocityStageContext(
    Boid Self, Vec2 NearestDelta, float PriorityBlend, float AllowedSpeed, float DeltaTime);

/// <summary>An explicitly installed, world-owned post-force velocity stage.</summary>
public interface ICanonicalVelocityStage
{
    string Id { get; }
    Vec2 Apply(in VelocityStageContext context, Vec2 velocity);
}

public sealed class ShapedAvoidanceStage : ICanonicalVelocityStage
{
    private readonly float _softRadius;
    private readonly float _hardRadius;
    private readonly float _gradualRadius;
    public string Id => CanonicalStageIds.ShapedAvoidance;

    public ShapedAvoidanceStage(CanonicalWorldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _softRadius = settings.SeparationRadius * 1.2f;
        _hardRadius = MathF.Max(0f, settings.SeparationPriorityRadiusFactor * settings.SenseRadius);
        _gradualRadius = settings.SeparationRadius * 2.0f;
    }

    public Vec2 Apply(in VelocityStageContext context, Vec2 velocity)
    {
        Vec2 nearestDelta = context.NearestDelta;
        if (!nearestDelta.IsNearlyZero())
        {
            float nearestDist = MathF.Sqrt(nearestDelta.LengthSquared);
            if (nearestDist < _gradualRadius)
            {
                Vec2 awayDir = (-nearestDelta).Normalized;
                Vec2 forward = context.Self.Forward;
                Vec2 right = new Vec2(forward.Y, -forward.X);
                float lateralComponent = Vec2.Dot(awayDir, right);
                Vec2 lateralDir = right * MathF.Sign(lateralComponent);
                float blendWeight = MathUtils.SmoothStep(_hardRadius, _softRadius, nearestDist);
                Vec2 shapedAvoidance = Vec2.Lerp(awayDir, lateralDir, blendWeight);
                float distanceRatio = MathUtils.Clamp01((_gradualRadius - nearestDist) / (_gradualRadius - _hardRadius));
                float gradualInfluence = distanceRatio * distanceRatio;
                float combinedInfluence = MathF.Max(context.PriorityBlend, gradualInfluence * 0.5f);
                Vec2 biasedVelocity = Vec2.Lerp(velocity, shapedAvoidance.WithLength(context.AllowedSpeed), combinedInfluence * 0.7f);
                velocity = biasedVelocity;
            }
        }
        return velocity;
    }
}

/// <summary>Limits direction only. A separate target-speed stage supplies the magnitude.</summary>
public sealed class TurnRateStage(float maxTurnRateDegPerSecond) : ICanonicalVelocityStage
{
    public string Id => CanonicalStageIds.TurnRate;
    public Vec2 Apply(in VelocityStageContext context, Vec2 velocity)
    {
        Vec2 normalizedCurrent = context.Self.Velocity.IsNearlyZero() ? context.Self.Forward : context.Self.Velocity;
        float currentAngle = MathF.Atan2(normalizedCurrent.Y, normalizedCurrent.X);
        float desiredAngle = MathF.Atan2(velocity.Y, velocity.X);
        float deltaAngle = MathUtils.AngleDifference(currentAngle, desiredAngle);
        float maxTurnRad = maxTurnRateDegPerSecond * MathF.PI / 180f * context.DeltaTime;
        float clampedAngle = MathUtils.Clamp(deltaAngle, -maxTurnRad, maxTurnRad);
        float finalAngle = currentAngle + clampedAngle;
        return new Vec2(MathF.Cos(finalAngle), MathF.Sin(finalAngle));
    }
}

public sealed class TargetSpeedStage : ICanonicalVelocityStage
{
    public string Id => CanonicalStageIds.TargetSpeed;
    public Vec2 Apply(in VelocityStageContext context, Vec2 velocity) => velocity.WithLength(context.AllowedSpeed);
}
