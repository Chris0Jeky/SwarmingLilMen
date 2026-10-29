namespace SwarmSim.Core.Canonical;

/// <summary>
/// Explicit compatibility boundary for a trusted legacy IRule. Its old Compute call receives
/// the full world and global indices. This adapter does NOT confer the bounded-local guarantee
/// of ILocalSteeringPolicy; new policies should implement that interface directly.
/// </summary>
public sealed class TrustedLegacyRuleAdapter : ILocalSteeringPolicy
{
    private readonly IRule _rule;
    public PolicyMetadata Metadata { get; }
    public int MaxIntents => 1;

    public TrustedLegacyRuleAdapter(IRule rule, PolicyMetadata metadata)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public void Emit(in LocalObservation observation, in LocalPolicyContext context, ref IntentWriter intents) =>
        throw new InvalidOperationException("A trusted legacy adapter must be invoked by the world's explicit compatibility path.");

    internal void EmitTrusted(int selfIndex, Boid self, ReadOnlySpan<Boid> boids,
        ReadOnlySpan<int> neighbors, ReadOnlySpan<float> weights, RuleContext context, ref IntentWriter intents) =>
        intents.AddForce(_rule.Compute(selfIndex, self, boids, neighbors, weights, context));

    internal object OwnedRule => _rule;
}
