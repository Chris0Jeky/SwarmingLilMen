using System.Runtime.CompilerServices;

namespace SwarmSim.Core.Canonical;

/// <summary>
/// An explicit composition, consumed by one world. Policies, resolver and stages are owned by
/// that world; build a fresh composition for another world, including when components appear
/// stateless. This prevents an injected mutable policy from coupling otherwise independent runs.
/// </summary>
public sealed class CanonicalPipeline
{
    private static readonly ConditionalWeakTable<object, object> Owners = new();
    private static readonly object OwnershipLock = new();
    private object? _ownershipToken;
    private PolicyEntry[] _policies;
    private IReadOnlyList<PolicyMetadata> _descriptions;
    private SteeringIntent[] _intentScratch;

    internal ISteeringResolver Resolver { get; }
    internal SeparationPriorityStage? PriorityStage { get; }
    internal ICanonicalVelocityStage[] VelocityStages { get; }
    internal int[] VelocityStageExecutions { get; }
    internal int PriorityExecutions { get; set; }
    internal PolicyEntry[] Entries => _policies;
    internal Span<SteeringIntent> IntentScratch => _intentScratch;
    public IReadOnlyList<PolicyMetadata> Policies => _descriptions;

    public CanonicalPipeline(IEnumerable<ILocalSteeringPolicy> policies, ISteeringResolver resolver,
        IEnumerable<ICanonicalVelocityStage>? velocityStages = null,
        SeparationPriorityStage? priorityStage = null)
    {
        ArgumentNullException.ThrowIfNull(policies);
        Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _policies = policies.Select(policy => new PolicyEntry(policy)).ToArray();
        ValidatePolicyIds(_policies);
        StableSort(_policies);
        _descriptions = Array.AsReadOnly(_policies.Select(entry => entry.Metadata).ToArray());
        _intentScratch = new SteeringIntent[MaximumIntentCount(_policies)];
        VelocityStages = velocityStages?.ToArray() ?? [];
        var stageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ICanonicalVelocityStage stage in VelocityStages)
        {
            ArgumentNullException.ThrowIfNull(stage);
            if (string.IsNullOrWhiteSpace(stage.Id) || !stageIds.Add(stage.Id))
                throw new ArgumentException("Velocity stage IDs must be nonempty and unique.", nameof(velocityStages));
        }
        if (priorityStage is not null && !stageIds.Add(priorityStage.Id))
            throw new ArgumentException("Priority and velocity stage IDs must be unique.", nameof(priorityStage));
        PriorityStage = priorityStage;
        VelocityStageExecutions = new int[VelocityStages.Length];
    }

    public static CanonicalPipeline FromSettings(CanonicalWorldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Mode switch
        {
            CanonicalMode.Enhanced => Enhanced(settings),
            CanonicalMode.ClassicReynolds => ClassicReynolds(settings),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), "Unknown canonical mode.")
        };
    }

    /// <summary>The established enhanced behavior, with every contribution/stage installed explicitly.</summary>
    public static CanonicalPipeline Enhanced(CanonicalWorldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new CanonicalPipeline(
            [new WhiskerAvoidancePolicy(settings.WhiskerTimeHorizon, settings.SeparationRadius, settings.WhiskerWeight),
             new SeparationRule(settings.SeparationWeight, settings.SeparationRadius),
             new AlignmentRule(settings.AlignmentWeight), new CohesionRule(settings.CohesionWeight),
             new WanderPolicy(settings.WanderStrength, settings.WanderRate)],
            new PriorityForceBudgetResolver(),
            [new ShapedAvoidanceStage(settings), new TurnRateStage(settings.MaxTurnRateDegPerSecond), new TargetSpeedStage()],
            new SeparationPriorityStage(settings));
    }

    /// <summary>Three independent clamped forces, their sum, then target-speed normalization.</summary>
    public static CanonicalPipeline ClassicReynolds(CanonicalWorldSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.FieldOfView != 360f)
            throw new ArgumentException("Classic Reynolds composition requires a 360-degree field of view.", nameof(settings));
        return new CanonicalPipeline(
            [new SeparationRule(settings.SeparationWeight, settings.SeparationRadius,
                new PolicyMetadata(CanonicalPolicyIds.Separation, SteeringRole.Separation, priority: 20)),
             new AlignmentRule(settings.AlignmentWeight,
                new PolicyMetadata(CanonicalPolicyIds.Alignment, SteeringRole.Alignment, priority: 30)),
             new CohesionRule(settings.CohesionWeight,
                new PolicyMetadata(CanonicalPolicyIds.Cohesion, SteeringRole.Cohesion, priority: 40))],
            new IndependentClampSumResolver(), [new TargetSpeedStage()]);
    }

    /// <summary>No policies, neighbor queries or velocity stages; integration retains velocity bits.</summary>
    public static CanonicalPipeline Empty() => new([], new PriorityForceBudgetResolver());

    internal void Attach()
    {
        if (_ownershipToken is not null) throw new InvalidOperationException("A pipeline belongs to exactly one world.");
        // Ownership only needs a unique marker. Referencing the world here (or in Owners' values)
        // would retain all simulation storage whenever a caller kept a policy/pipeline reference.
        object ownershipToken = new();
        var components = new List<object> { this, Resolver };
        if (PriorityStage is not null) components.Add(PriorityStage);
        components.AddRange(VelocityStages);
        foreach (PolicyEntry entry in _policies)
        {
            components.Add(entry.Policy);
            if (entry.Policy is TrustedLegacyRuleAdapter legacy) components.Add(legacy.OwnedRule);
        }
        Claim(components, ownershipToken);
        _ownershipToken = ownershipToken;
    }

    internal void AddPolicy(ILocalSteeringPolicy policy)
    {
        var entry = new PolicyEntry(policy);
        foreach (PolicyEntry existing in _policies)
            if (existing.Metadata.Id == entry.Metadata.Id)
                throw new ArgumentException($"Duplicate policy ID '{entry.Metadata.Id}'.", nameof(policy));
        Claim(policy is TrustedLegacyRuleAdapter legacy ? [policy, legacy.OwnedRule] : [policy], _ownershipToken!);
        Array.Resize(ref _policies, _policies.Length + 1);
        _policies[^1] = entry;
        StableSort(_policies);
        _descriptions = Array.AsReadOnly(_policies.Select(item => item.Metadata).ToArray());
        if (_intentScratch.Length < entry.MaxIntents) _intentScratch = new SteeringIntent[entry.MaxIntents];
    }

    internal PolicyEntry? FindPolicy(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        foreach (PolicyEntry entry in _policies)
            if (entry.Metadata.Id == id) return entry;
        return null;
    }

    internal void BeginTick()
    {
        foreach (PolicyEntry entry in _policies) entry.Executions = 0;
        Array.Clear(VelocityStageExecutions);
        PriorityExecutions = 0;
    }

    private static void Claim(IEnumerable<object> components, object owner)
    {
        // Construction/mutation only. No lookup, lock, allocation or boxing occurs during Step.
        lock (OwnershipLock)
        {
            var unique = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (object component in components)
            {
                if (!unique.Add(component) || Owners.TryGetValue(component, out _))
                    throw new InvalidOperationException("Pipeline component instances cannot be shared or registered twice; create a fresh instance per world.");
            }
            foreach (object component in unique) Owners.Add(component, owner);
        }
    }

    private static void ValidatePolicyIds(PolicyEntry[] policies)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (PolicyEntry entry in policies)
            if (!ids.Add(entry.Metadata.Id))
                throw new ArgumentException($"Duplicate policy ID '{entry.Metadata.Id}'.", nameof(policies));
    }

    private static int MaximumIntentCount(PolicyEntry[] policies)
    {
        int maximum = 0;
        foreach (PolicyEntry entry in policies) maximum = Math.Max(maximum, entry.MaxIntents);
        return maximum;
    }

    private static void StableSort(PolicyEntry[] entries)
    {
        // Registration is rare and arrays are small. Insertion sort preserves equal-priority order.
        for (int i = 1; i < entries.Length; i++)
        {
            PolicyEntry item = entries[i];
            int j = i - 1;
            while (j >= 0 && entries[j].Metadata.Priority > item.Metadata.Priority)
            {
                entries[j + 1] = entries[j];
                j--;
            }
            entries[j + 1] = item;
        }
    }

    internal sealed class PolicyEntry
    {
        internal ILocalSteeringPolicy Policy { get; }
        internal PolicyMetadata Metadata { get; }
        internal int MaxIntents { get; }
        internal bool Enabled { get; set; } = true;
        internal int Executions { get; set; }
        internal PolicyEntry(ILocalSteeringPolicy policy)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Metadata = policy.Metadata ?? throw new ArgumentException("Policy metadata is required.", nameof(policy));
            MaxIntents = policy.MaxIntents;
            if (MaxIntents < 0) throw new ArgumentException("Intent capacity cannot be negative.", nameof(policy));
        }
    }
}
