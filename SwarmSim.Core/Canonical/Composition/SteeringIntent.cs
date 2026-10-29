using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SwarmSim.Core.Canonical;

public enum SteeringIntentKind { Force }
public enum SteeringRole { Custom, Whisker, Separation, Alignment, Cohesion, Wander }

public static class CanonicalPolicyIds
{
    public const string Whisker = "whisker";
    public const string Separation = "separation";
    public const string Alignment = "alignment";
    public const string Cohesion = "cohesion";
    public const string Wander = "wander";
}

/// <summary>Immutable identity and arbitration metadata captured once at registration.</summary>
/// <remarks>
/// Lower priorities run first; equal priorities retain registration order. Weight multiplies
/// the emitted force. It is separate from the classic boid rules' desired-speed weights.
/// </remarks>
public sealed class PolicyMetadata
{
    public string Id { get; }
    public SteeringRole Role { get; }
    public int Priority { get; }
    public float Weight { get; }
    public float PriorityAttenuation { get; }
    public bool ReserveRemainingForce { get; }
    public bool RequiresRemainingForce { get; }

    public PolicyMetadata(string id, SteeringRole role = SteeringRole.Custom, int priority = 50,
        float weight = 1f, float priorityAttenuation = 0f, bool reserveRemainingForce = false,
        bool requiresRemainingForce = false)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A policy ID is required.", nameof(id));
        if (!Enum.IsDefined(role)) throw new ArgumentException("Unknown steering role.", nameof(role));
        if (!float.IsFinite(weight) || weight < 0f)
            throw new ArgumentException("Intent weight must be finite and non-negative.", nameof(weight));
        if (!float.IsFinite(priorityAttenuation) || priorityAttenuation < 0f || priorityAttenuation > 1f)
            throw new ArgumentException("Priority attenuation must be in [0, 1].", nameof(priorityAttenuation));
        Id = id;
        Role = role;
        Priority = priority;
        Weight = weight;
        PriorityAttenuation = priorityAttenuation;
        ReserveRemainingForce = reserveRemainingForce;
        RequiresRemainingForce = requiresRemainingForce;
    }
}

/// <summary>A tagged output. Only the writer can stamp identity, priority and weight.</summary>
public readonly struct SteeringIntent
{
    internal SteeringIntent(PolicyMetadata metadata, Vec2 payload)
    {
        Metadata = metadata;
        Payload = payload;
    }
    public SteeringIntentKind Kind => SteeringIntentKind.Force;
    public Vec2 Payload { get; }
    public PolicyMetadata Metadata { get; }
    public string PolicyId => Metadata.Id;
    public int Priority => Metadata.Priority;
    public float Weight => Metadata.Weight;
}

/// <summary>Caller-owned bounded output. Overflow and nonfinite forces fail explicitly.</summary>
public ref struct IntentWriter
{
    private readonly PolicyMetadata _metadata;
    private readonly Span<SteeringIntent> _buffer;
    public int Count { get; private set; }
    public int Capacity => _buffer.Length;
    public readonly ReadOnlySpan<SteeringIntent> Written => _buffer[..Count];

    public IntentWriter(PolicyMetadata metadata, Span<SteeringIntent> buffer)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _buffer = buffer;
        Count = 0;
    }

    public void AddForce(Vec2 force)
    {
        if (!float.IsFinite(force.X) || !float.IsFinite(force.Y))
            throw new ArgumentException("A steering intent must contain a finite force.", nameof(force));
        if (Count == _buffer.Length)
            throw new InvalidOperationException($"Policy '{_metadata.Id}' exceeded its declared intent capacity ({_buffer.Length}).");
        _buffer[Count++] = new SteeringIntent(_metadata, force);
    }

    internal readonly void ValidateLease(PolicyMetadata expectedMetadata, Span<SteeringIntent> expectedBuffer)
    {
        if (!ReferenceEquals(_metadata, expectedMetadata) || _buffer.Length != expectedBuffer.Length
            || !Unsafe.AreSame(ref MemoryMarshal.GetReference(_buffer), ref MemoryMarshal.GetReference(expectedBuffer)))
            throw new InvalidOperationException("A policy must use its borrowed intent writer; replacing its metadata or buffer is not allowed.");
    }
}
