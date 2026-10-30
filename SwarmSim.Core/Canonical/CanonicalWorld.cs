using System;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Canonical;

public sealed class CanonicalWorld
{
    private readonly CanonicalWorldSettings _settings;
    private readonly ISpatialIndex _spatialIndex;
    private readonly CanonicalPipeline _pipeline;
    private int _legacyRuleSequence;
    private bool _isStepping;
    private bool _usesWanderRandom;

    private Boid[] _activeBoids;
    private Boid[] _nextBoids;
    private readonly int[] _neighborScratch;
    private readonly float[] _neighborWeightScratch;
    private readonly NeighborView[] _neighborViewScratch;
    private readonly RuleInstrumentation _instrumentation;
    private readonly Pcg32State[] _wanderRngs;
    private readonly bool[] _priorityState;
    private readonly float[] _priorityBlend;
    private readonly float[] _priorityHoldTimers;
    private readonly float[] _nearestDistances;
    private readonly float[] _nearestAngles;
    private readonly int[] _whiskerCounts;
    private readonly float[] _wanderAngles;
    private ulong _tickCount;
    private float _neighborDistanceSum;
    private int _neighborDistanceSamples;
    private bool _separationPriorityTriggered;
    private float _minNeighborDistance = float.MaxValue;
    private float _maxNeighborDistance;
    private int _lastTickAgentCount;

    public CanonicalWorld(CanonicalWorldSettings settings, ISpatialIndex spatialIndex)
        : this(settings, spatialIndex, CanonicalPipeline.FromSettings(settings)) { }

    /// <summary>Builds a world with only the supplied policies and stages; nothing is installed implicitly.</summary>
    public CanonicalWorld(CanonicalWorldSettings settings, ISpatialIndex spatialIndex, CanonicalPipeline pipeline)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _spatialIndex = spatialIndex ?? throw new ArgumentNullException(nameof(spatialIndex));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        if (!Enum.IsDefined(settings.Mode)) throw new ArgumentOutOfRangeException(nameof(settings), "Unknown canonical mode.");
        if (settings.Mode == CanonicalMode.ClassicReynolds && settings.FieldOfView != 360f)
            throw new ArgumentException("Classic Reynolds mode requires a 360-degree field of view.", nameof(settings));
        Rng.ValidateExternalSeed(settings.Seed, nameof(settings));
        RandomStreams = new RandomStreams(settings.Seed);
        if (!float.IsFinite(settings.TargetSpeed) || settings.TargetSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.TargetSpeed,
                "TargetSpeed must be finite and positive.");
        }
        if (!float.IsFinite(settings.SenseRadius) || settings.SenseRadius <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SenseRadius,
                "SenseRadius must be finite and positive.");
        }
        if (!float.IsFinite(settings.MaxForce) || settings.MaxForce <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.MaxForce,
                "MaxForce must be finite and positive.");
        }
        if (!float.IsFinite(settings.FixedDeltaTime) || settings.FixedDeltaTime <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.FixedDeltaTime,
                "FixedDeltaTime must be finite and positive.");
        }
        if (!float.IsFinite(settings.SeparationRadius))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationRadius,
                "SeparationRadius must be finite.");
        }
        if (!float.IsFinite(settings.SeparationWeight))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationWeight,
                "SeparationWeight must be finite.");
        }
        if (!float.IsFinite(settings.AlignmentWeight))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.AlignmentWeight,
                "AlignmentWeight must be finite.");
        }
        if (!float.IsFinite(settings.CohesionWeight))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.CohesionWeight,
                "CohesionWeight must be finite.");
        }
        if (!float.IsFinite(settings.FieldOfView) || settings.FieldOfView <= 0f || settings.FieldOfView > 360f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.FieldOfView,
                "FieldOfView must be finite and in (0, 360].");
        }
        if (!float.IsFinite(settings.WorldWidth) || settings.WorldWidth <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.WorldWidth,
                "WorldWidth must be finite and positive.");
        }
        if (!float.IsFinite(settings.WorldHeight) || settings.WorldHeight <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.WorldHeight,
                "WorldHeight must be finite and positive.");
        }
        if (!float.IsFinite(settings.MaxTurnRateDegPerSecond) || settings.MaxTurnRateDegPerSecond < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.MaxTurnRateDegPerSecond,
                "MaxTurnRateDegPerSecond must be finite and non-negative.");
        }
        if (!float.IsFinite(settings.WanderStrength) || settings.WanderStrength < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.WanderStrength,
                "WanderStrength must be finite and non-negative.");
        }
        if (!float.IsFinite(settings.WanderRate) || settings.WanderRate < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.WanderRate,
                "WanderRate must be finite and non-negative.");
        }
        if (!float.IsFinite(settings.WhiskerTimeHorizon))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.WhiskerTimeHorizon,
                "WhiskerTimeHorizon must be finite.");
        }
        if (!float.IsFinite(settings.WhiskerWeight) || settings.WhiskerWeight < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.WhiskerWeight,
                "WhiskerWeight must be finite and non-negative.");
        }
        if (!float.IsFinite(settings.SeparationPriorityRadiusFactor))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationPriorityRadiusFactor,
                "SeparationPriorityRadiusFactor must be finite.");
        }
        if (!float.IsFinite(settings.SeparationPriorityExitFactor))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationPriorityExitFactor,
                "SeparationPriorityExitFactor must be finite.");
        }
        if (!float.IsFinite(settings.SeparationPriorityBoost) || settings.SeparationPriorityBoost < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationPriorityBoost,
                "SeparationPriorityBoost must be finite and non-negative.");
        }
        if (!float.IsFinite(settings.SeparationSpeedDroop)
            || settings.SeparationSpeedDroop < 0f
            || settings.SeparationSpeedDroop > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationSpeedDroop,
                "SeparationSpeedDroop must be finite and between 0 and 1, inclusive.");
        }
        if (!float.IsFinite(settings.SeparationPriorityHoldTime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationPriorityHoldTime,
                "SeparationPriorityHoldTime must be finite.");
        }
        if (!float.IsFinite(settings.SeparationPriorityRampInTime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationPriorityRampInTime,
                "SeparationPriorityRampInTime must be finite.");
        }
        if (!float.IsFinite(settings.SeparationPriorityRampOutTime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SeparationPriorityRampOutTime,
                "SeparationPriorityRampOutTime must be finite.");
        }

        if (spatialIndex.WorldWidth != settings.WorldWidth || spatialIndex.WorldHeight != settings.WorldHeight)
        {
            throw new ArgumentException(
                "Spatial index world extents must exactly equal CanonicalWorldSettings.WorldWidth and WorldHeight.",
                nameof(spatialIndex));
        }

        int capacity = Math.Max(settings.InitialCapacity, 1);
        _activeBoids = new Boid[capacity];
        _nextBoids = new Boid[capacity];

        int maxNeighbors = Math.Max(settings.MaxNeighbors, 4);
        EffectiveMaxNeighbors = maxNeighbors;
        _neighborScratch = new int[maxNeighbors];
        _neighborWeightScratch = new float[maxNeighbors];
        _neighborViewScratch = new NeighborView[maxNeighbors];
        _instrumentation = new RuleInstrumentation(capacity);
        _priorityState = new bool[capacity];
        _priorityBlend = new float[capacity];
        _priorityHoldTimers = new float[capacity];
        _nearestDistances = new float[capacity];
        _nearestAngles = new float[capacity];
        _whiskerCounts = new int[capacity];
        _wanderAngles = new float[capacity];
        _wanderRngs = new Pcg32State[capacity];

        _spatialIndex.Initialize(capacity);

        _pipeline.Attach();
        _usesWanderRandom = _pipeline.FindPolicy(CanonicalPolicyIds.Wander)?.Policy is WanderPolicy { UsesRandom: true };
    }

    public int Count { get; private set; }

    /// <summary>
    /// Gets the maximum number of spatial-query candidates <see cref="Step"/> considers per boid:
    /// <see cref="CanonicalWorldSettings.MaxNeighbors"/> with the same lower bound the simulation
    /// applies. Diagnostics must size their query buffers to this to observe what steering saw;
    /// a wider buffer reports neighbours the simulation discarded and hides real truncation.
    /// </summary>
    public int EffectiveMaxNeighbors { get; }

    public ReadOnlySpan<Boid> Boids => _activeBoids.AsSpan(0, Count);

    public CanonicalWorldSettings Settings => _settings;

    /// <summary>Named stochastic consumers derive independent streams from the world seed.</summary>
    public RandomStreams RandomStreams { get; }

    public RuleInstrumentation Instrumentation => _instrumentation;

    public ulong TickCount => _tickCount;

    public IReadOnlyList<PolicyMetadata> Policies => _pipeline.Policies;

    /// <summary>
    /// Explicit trusted compatibility path. The rule retains full-world/index access, but its
    /// returned force is now resolved in stable priority order (before default wander).
    /// </summary>
    public void AddRule(IRule rule)
    {
        EnsureNotStepping();
        ArgumentNullException.ThrowIfNull(rule);
        AddPolicy(new TrustedLegacyRuleAdapter(rule, new PolicyMetadata($"legacy-rule-{_legacyRuleSequence++}")));
    }

    public void AddPolicy(ILocalSteeringPolicy policy)
    {
        EnsureNotStepping();
        _pipeline.AddPolicy(policy);
        if (policy is WanderPolicy { UsesRandom: true })
        {
            _usesWanderRandom = true;
            for (int i = 0; i < Count; i++) InitializeWander(i);
        }
    }

    /// <summary>Disables emission by stable ID; a zero rule weight is not an enable/disable switch.</summary>
    public void SetPolicyEnabled(string id, bool enabled)
    {
        EnsureNotStepping();
        var entry = _pipeline.FindPolicy(id) ?? throw new KeyNotFoundException($"Unknown policy '{id}'.");
        entry.Enabled = enabled;
    }

    public bool IsPolicyEnabled(string id) =>
        (_pipeline.FindPolicy(id) ?? throw new KeyNotFoundException($"Unknown policy '{id}'.")).Enabled;

    /// <summary>Number of per-agent calls in the last completed/in-progress tick; absent IDs return zero.</summary>
    public int GetPolicyExecutionCount(string id) => _pipeline.FindPolicy(id)?.Executions ?? 0;

    public int GetStageExecutionCount(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_pipeline.PriorityStage?.Id == id) return _pipeline.PriorityExecutions;
        for (int i = 0; i < _pipeline.VelocityStages.Length; i++)
            if (_pipeline.VelocityStages[i].Id == id) return _pipeline.VelocityStageExecutions[i];
        return 0;
    }

    private void EnsureNotStepping()
    {
        if (_isStepping) throw new InvalidOperationException("A pipeline cannot be changed or stepped recursively during Step.");
    }

    public bool TryAddBoid(Vec2 position, Vec2 velocity, byte group = 0)
    {
        EnsureNotStepping();
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(velocity.X) || !float.IsFinite(velocity.Y))
            return false;
        if (Count >= _activeBoids.Length)
            return false;

        Vec2 normalizedVelocity = velocity.IsNearlyZero()
            ? new Vec2(1f, 0f).WithLength(Settings.TargetSpeed)
            : velocity.WithLength(Settings.TargetSpeed);

        int index = Count;
        if (_usesWanderRandom) InitializeWander(index);
        // Normalize at ingest so every stored position lies in [0, extent). Both spatial indexes
        // normalize before subtracting, while Vec2.MinimumImageDelta -- used by field-of-view
        // filtering and every steering rule -- subtracts the stored coordinates directly. A boid
        // seeded outside the world would therefore be accepted by the query and simultaneously
        // rejected by SeparationRule at the inclusive radius boundary, until the first Step
        // happened to wrap it. Establishing the invariant here is cheaper than re-wrapping in the
        // per-neighbor hot path, and it is the same normalization Step already applies.
        (float wrappedX, float wrappedY) = MathUtils.WrapPosition(
            position.X, position.Y, Settings.WorldWidth, Settings.WorldHeight);
        _activeBoids[index] = new Boid(new Vec2(wrappedX, wrappedY), normalizedVelocity, group);
        Count = index + 1;
        return true;
    }

    private void InitializeWander(int index)
    {
        _wanderRngs[index] = RandomStreams.CreateAgentState("canonical.wander", (ulong)index);
        _wanderAngles[index] = _wanderRngs[index].NextFloat() * (MathF.PI * 2f);
    }

    public void SetVelocity(int index, Vec2 velocity)
    {
        EnsureNotStepping();
        if (index < 0 || index >= Count)
            return;
        if (!float.IsFinite(velocity.X) || !float.IsFinite(velocity.Y))
            return;

        Vec2 normalizedVelocity = velocity.WithLength(Settings.TargetSpeed);
        var boid = _activeBoids[index];
        _activeBoids[index] = new Boid(boid.Position, normalizedVelocity, boid.Group);
    }

    public void Step(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be finite and positive.");
        EnsureNotStepping();
        _isStepping = true;
        try { StepCore(deltaTime); }
        finally { _isStepping = false; }
    }

    private void StepCore(float deltaTime)
    {
        var current = _activeBoids.AsSpan(0, Count);
        _spatialIndex.Rebuild(current);
        _instrumentation.Prepare(Count);
        _pipeline.BeginTick();
        _neighborDistanceSum = 0f;
        _neighborDistanceSamples = 0;
        _minNeighborDistance = float.MaxValue;
        _maxNeighborDistance = 0f;
        _separationPriorityTriggered = false;
        var next = _nextBoids.AsSpan(0, Count);
        float fieldOfViewCos = MathF.Cos((Settings.FieldOfView * MathF.PI / 180f) * 0.5f);
        bool observeNeighbors = _pipeline.Entries.Length > 0 || _pipeline.PriorityStage is not null
            || _pipeline.VelocityStages.Length > 0;

        for (int i = 0; i < Count; i++)
        {
            Boid boid = current[i];
            Vec2 steering = Vec2.Zero;
            float remainingForce = Settings.MaxForce;
            Vec2 nearestDelta = Vec2.Zero;
            int filtered = 0;
            float neighborWeightSum = 0f;
            float minDistForAgent = float.MaxValue;
            float nearestAngle = 0f;
            _whiskerCounts[i] = 0;

            if (observeNeighbors)
            {
                SpatialQueryResult query = _spatialIndex.QueryNeighbors(current, i, Settings.SenseRadius, _neighborScratch);
                _instrumentation.RecordTruncation(i, query.IsTruncated);
                filtered = FilterByFieldOfView(boid.Forward, boid.Position,
                    _neighborScratch.AsSpan(0, query.Count), _neighborWeightScratch, current,
                    fieldOfViewCos, Settings.FieldOfView, i, Settings.WorldWidth, Settings.WorldHeight,
                    out neighborWeightSum);
                var visible = _neighborScratch.AsSpan(0, filtered);
                (float distanceSum, float minDistance, float maxDistance, Vec2 delta) =
                    BuildLocalNeighbors(boid, current, visible);
                if (filtered > 0)
                {
                    minDistForAgent = minDistance;
                    nearestDelta = delta;
                    _neighborDistanceSum += distanceSum;
                    _neighborDistanceSamples += filtered;
                    _minNeighborDistance = MathF.Min(_minNeighborDistance, minDistForAgent);
                    _maxNeighborDistance = MathF.Max(_maxNeighborDistance, maxDistance);
                    if (!nearestDelta.IsNearlyZero())
                    {
                        // Preserve ordinary normalization exactly. A representable large distance
                        // can have an overflowing squared float; divide by the recovered distance
                        // in that exceptional case instead of producing a zero direction.
                        Vec2 toNearest = float.IsFinite(nearestDelta.LengthSquared)
                            ? nearestDelta.Normalized : nearestDelta / minDistForAgent;
                        float dotProduct = Vec2.Dot(boid.Forward, toNearest);
                        nearestAngle = MathF.Acos(MathUtils.Clamp(dotProduct, -1f, 1f)) * 180f / MathF.PI;
                    }
                }
            }
            _nearestDistances[i] = observeNeighbors ? minDistForAgent : 0f;
            _nearestAngles[i] = nearestAngle;
            var observation = new LocalObservation(boid, _neighborViewScratch.AsSpan(0, filtered));
            bool priorityApplied = false;
            float separationMagnitude = 0f;
            float alignmentMagnitude = 0f;
            float cohesionMagnitude = 0f;

            foreach (CanonicalPipeline.PolicyEntry entry in _pipeline.Entries)
            {
                if (!priorityApplied && _pipeline.PriorityStage is { } priority
                    && entry.Metadata.Priority >= priority.Priority)
                {
                    ApplyPriority(i, filtered, minDistForAgent, deltaTime);
                    priorityApplied = true;
                }
                if (!entry.Enabled || (entry.Metadata.RequiresRemainingForce && remainingForce <= 0f)) continue;

                float separationBoost = MathUtils.Lerp(1f, Settings.SeparationPriorityBoost, _priorityBlend[i]);
                var context = new LocalPolicyContext(Settings.TargetSpeed, Settings.MaxForce, Settings.SenseRadius,
                    fieldOfViewCos, deltaTime, separationBoost, _priorityBlend[i],
                    _wanderRngs.AsSpan(i, 1), _wanderAngles.AsSpan(i, 1), _whiskerCounts.AsSpan(i, 1));
                var writer = new IntentWriter(entry.Metadata, _pipeline.IntentScratch[..entry.MaxIntents]);
                if (entry.Policy is TrustedLegacyRuleAdapter legacy)
                {
                    // Only this explicitly named adapter receives full-world state/global indices.
                    var legacyContext = new RuleContext(Settings.TargetSpeed, Settings.MaxForce, Settings.SenseRadius,
                        fieldOfViewCos, deltaTime, separationBoost, _instrumentation, Settings.WorldWidth, Settings.WorldHeight);
                    legacy.EmitTrusted(i, boid, current, _neighborScratch.AsSpan(0, filtered),
                        _neighborWeightScratch.AsSpan(0, filtered), legacyContext, ref writer);
                }
                else
                {
                    entry.Policy.Emit(observation, context, ref writer);
                }
                writer.ValidateLease(entry.Metadata, _pipeline.IntentScratch[..entry.MaxIntents]);
                entry.Executions++;
                foreach (ref readonly SteeringIntent intent in writer.Written)
                {
                    if (!ReferenceEquals(intent.Metadata, entry.Metadata))
                        throw new InvalidOperationException("A policy cannot forge another intent's arbitration metadata.");
                    float applied = _pipeline.Resolver.Accumulate(intent, _priorityBlend[i], Settings.MaxForce,
                        ref steering, ref remainingForce);
                    if (!float.IsFinite(steering.X) || !float.IsFinite(steering.Y) || !float.IsFinite(applied))
                        throw new InvalidOperationException("A steering resolver produced a nonfinite result.");
                    switch (intent.Metadata.Role)
                    {
                        case SteeringRole.Separation: separationMagnitude += applied; break;
                        case SteeringRole.Alignment: alignmentMagnitude += applied; break;
                        case SteeringRole.Cohesion: cohesionMagnitude += applied; break;
                    }
                }
            }
            if (!priorityApplied && _pipeline.PriorityStage is not null)
                ApplyPriority(i, filtered, minDistForAgent, deltaTime);

            _instrumentation.SetNeighborCount(i, filtered);
            _instrumentation.SetNeighborWeightSum(i, neighborWeightSum);
            _instrumentation.RecordSeparation(i, separationMagnitude);
            _instrumentation.RecordAlignment(i, alignmentMagnitude);
            _instrumentation.RecordCohesion(i, cohesionMagnitude);
            // Record force composition before explicitly installed velocity stages change it.
            _instrumentation.RecordSteering(i, steering);

            // A genuinely empty composition does not even add a signed zero to velocity.
            Vec2 nextVelocity = _pipeline.Entries.Length == 0 ? boid.Velocity : boid.Velocity + steering * deltaTime;
            float prioritySpeed = Settings.TargetSpeed * (1f - Settings.SeparationSpeedDroop * _priorityBlend[i]);
            float allowedSpeed = _priorityBlend[i] > 0f ? prioritySpeed : Settings.TargetSpeed;
            var velocityContext = new VelocityStageContext(boid, nearestDelta, _priorityBlend[i], allowedSpeed, deltaTime);
            for (int stageIndex = 0; stageIndex < _pipeline.VelocityStages.Length; stageIndex++)
            {
                nextVelocity = _pipeline.VelocityStages[stageIndex].Apply(velocityContext, nextVelocity);
                _pipeline.VelocityStageExecutions[stageIndex]++;
                if (!float.IsFinite(nextVelocity.X) || !float.IsFinite(nextVelocity.Y))
                    throw new InvalidOperationException("A velocity stage produced a nonfinite result.");
            }

            Vec2 nextPosition = boid.Position + nextVelocity * deltaTime;
            (float wrappedX, float wrappedY) = MathUtils.WrapPosition(nextPosition.X, nextPosition.Y, Settings.WorldWidth, Settings.WorldHeight);
            nextPosition = new Vec2(wrappedX, wrappedY);
            next[i] = new Boid(nextPosition, nextVelocity, boid.Group);
        }
        (_activeBoids, _nextBoids) = (_nextBoids, _activeBoids);
        _tickCount++;
        _lastTickAgentCount = Count;
    }

    private void ApplyPriority(int index, int neighborCount, float nearestDistance, float deltaTime)
    {
        _pipeline.PriorityStage!.Apply(neighborCount, nearestDistance, deltaTime,
            ref _priorityState[index], ref _priorityBlend[index], ref _priorityHoldTimers[index]);
        _pipeline.PriorityExecutions++;
        _separationPriorityTriggered |= _priorityBlend[index] > 0f;
    }

    private (float distanceSum, float minDist, float maxDist, Vec2 nearestDelta) BuildLocalNeighbors(
        Boid self, ReadOnlySpan<Boid> boids, ReadOnlySpan<int> neighbors)
    {
        float sum = 0f;
        float minDist = float.MaxValue;
        float maxDist = 0f;
        Vec2 nearestDelta = Vec2.Zero;

        for (int n = 0; n < neighbors.Length; n++)
        {
            Boid neighbor = boids[neighbors[n]];
            Vec2 relative = Vec2.MinimumImageDelta(self.Position, neighbor.Position, Settings.WorldWidth, Settings.WorldHeight);
            Vec2 reverse = Vec2.MinimumImageDelta(neighbor.Position, self.Position, Settings.WorldWidth, Settings.WorldHeight);
            float dist = NeighborDistance(relative);
            _neighborViewScratch[n] = new NeighborView(relative, reverse, neighbor.Velocity - self.Velocity,
                neighbor.Velocity, neighbor.Group, _neighborWeightScratch[n], dist);

            // Reuse the same displacement/distance for diagnostics. Keep the original ascending
            // candidate accumulation and strict tie comparison; reverse delta remains independent.
            sum += dist;
            if (dist < minDist)
            {
                minDist = dist;
                nearestDelta = relative;
            }
            maxDist = MathF.Max(maxDist, dist);
        }

        return (sum, minDist == float.MaxValue ? 0f : minDist, maxDist, nearestDelta);
    }

    private static float NeighborDistance(Vec2 delta)
    {
        float squared = delta.LengthSquared;
        // Keep the established float arithmetic for ordinary distances. Widen only when a
        // subnormal/zero-underflow or overflowing square loses a representable geometric length.
        if (float.IsNormal(squared)) return MathF.Sqrt(squared);
        return (float)Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y);
    }

    private static int FilterByFieldOfView(
        Vec2 forward,
        Vec2 origin,
        Span<int> candidates,
        Span<float> weights,
        ReadOnlySpan<Boid> boids,
        float fieldOfViewCos,
        float fieldOfViewDegrees,
        int selfIndex,
        float worldWidth,
        float worldHeight,
        out float totalWeight)
    {
        totalWeight = 0f;
        if (candidates.IsEmpty || weights.IsEmpty)
            return 0;

        bool fullCircle = fieldOfViewCos <= -1f;
        int keep = 0;
        float range = MathF.Max(1e-6f, 1f - fieldOfViewCos);

        for (int i = 0; i < candidates.Length; i++)
        {
            int index = candidates[i];
            if (index == selfIndex)
            {
                continue;
            }
            Vec2 delta = Vec2.MinimumImageDelta(origin, boids[index].Position, worldWidth, worldHeight);

            if (delta.IsNearlyZero())
            {
                candidates[keep] = index;
                weights[keep] = 1f;
                totalWeight += 1f;
                keep++;
                continue;
            }

            if (fullCircle)
            {
                candidates[keep] = index;
                weights[keep] = 1f;
                totalWeight += 1f;
                keep++;
                continue;
            }

            Vec2 direction = delta.Normalized;
            if (!MathUtils.IsWithinFieldOfView(forward.X, forward.Y, direction.X, direction.Y, fieldOfViewDegrees))
                continue;

            float dot = Vec2.Dot(forward, direction);
            float normalizedRaw = (dot - fieldOfViewCos) / range;

            if (normalizedRaw <= 0f)
                continue;

            float weight = normalizedRaw >= 1f ? 1f : normalizedRaw;
            candidates[keep] = index;
            weights[keep] = weight;
            totalWeight += weight;
            keep++;
        }

        return keep;
    }

    public bool TryGetMetrics(int index, out RuleInstrumentation.Metrics metrics)
    {
        return _instrumentation.TryGetMetrics(index, out metrics);
    }

    /// <summary>
    /// Rebuilds the spatial index for the current state and returns field-of-view-filtered neighbors.
    /// </summary>
    /// <param name="index">Boid whose visible neighborhood is queried.</param>
    /// <param name="buffer">Caller-owned neighbor-index buffer.</param>
    /// <param name="weights">Caller-owned field-of-view weight buffer.</param>
    /// <returns>The visible count and whether the bounded spatial query omitted candidates.</returns>
    public SpatialQueryResult QueryVisibleNeighbors(int index, Span<int> buffer, Span<float> weights)
        => QueryVisibleNeighbors(_activeBoids.AsSpan(0, Count), index, buffer, weights);

    /// <summary>
    /// Inputs to the last completed tick, paired with Instrumentation. Empty before the first tick.
    /// This borrowed view is overwritten by the next Step; copy it to retain it across steps.
    /// </summary>
    public ReadOnlySpan<Boid> LastTickInputs => _nextBoids.AsSpan(0, _lastTickAgentCount);

    /// <summary>
    /// Requeries the last tick's input positions with the supplied buffer cap. Use EffectiveMaxNeighbors
    /// to reproduce the built-in indexes' steering candidate set, rather than querying moved agents.
    /// </summary>
    public SpatialQueryResult QueryLastTickVisibleNeighbors(int index, Span<int> buffer, Span<float> weights)
    {
        EnsureNotStepping();
        return QueryVisibleNeighbors(LastTickInputs, index, buffer, weights);
    }

    private SpatialQueryResult QueryVisibleNeighbors(ReadOnlySpan<Boid> boids, int index, Span<int> buffer, Span<float> weights)
    {
        if (index < 0 || index >= boids.Length)
            return new SpatialQueryResult(0, false);

        int outputCapacity = Math.Min(buffer.Length, weights.Length);
        _spatialIndex.Rebuild(boids);
        SpatialQueryResult query = _spatialIndex.QueryNeighbors(
            boids,
            index,
            Settings.SenseRadius,
            buffer.Slice(0, outputCapacity));
        int neighborCount = query.Count;
        float halfAngleRad = (Settings.FieldOfView * MathF.PI / 180f) * 0.5f;
        float fieldOfViewCos = MathF.Cos(halfAngleRad);
        int visibleCount = FilterByFieldOfView(
            boids[index].Forward,
            boids[index].Position,
            buffer.Slice(0, neighborCount),
            weights.Slice(0, outputCapacity),
            boids,
            fieldOfViewCos,
            Settings.FieldOfView,
            index,
            Settings.WorldWidth,
            Settings.WorldHeight,
            out _);
        return new SpatialQueryResult(visibleCount, query.IsTruncated);
    }

    /// <summary>
    /// Explicitly copies the last tick's whole-world researcher telemetry into reusable,
    /// caller-owned buffers. This does not query the spatial index or change the simulation.
    /// </summary>
    /// <remarks>
    /// The destination must hold the boids present in the last completed tick. Boids added
    /// between ticks first appear after the next <see cref="Step"/>. Before the first step,
    /// capture returns tick zero, zero agents and empty per-agent spans. Buffer contents remain
    /// unchanged until the caller explicitly captures into that destination again.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The destination is null.</exception>
    /// <exception cref="ArgumentException">The destination's capacity is too small.</exception>
    public void CaptureTelemetryInto(CanonicalTelemetry destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Capacity < _lastTickAgentCount)
            throw new ArgumentException("Telemetry capacity must cover every agent in the last tick.", nameof(destination));

        destination.Capture(
            _tickCount,
            _instrumentation,
            _neighborDistanceSamples > 0 ? _neighborDistanceSum / _neighborDistanceSamples : 0f,
            _neighborDistanceSamples > 0 ? _minNeighborDistance : 0f,
            _neighborDistanceSamples > 0 ? _maxNeighborDistance : 0f,
            _separationPriorityTriggered,
            _nearestDistances.AsSpan(0, _lastTickAgentCount),
            _nearestAngles.AsSpan(0, _lastTickAgentCount),
            _whiskerCounts.AsSpan(0, _lastTickAgentCount));
    }

    /// <summary>
    /// Compatibility adapter for the old telemetry name. Allocates an independent snapshot
    /// on each call after the first tick; prefer <see cref="CaptureTelemetryInto"/> with a reused
    /// <see cref="CanonicalTelemetry"/> destination for allocation-free capture.
    /// </summary>
    public PerceptionSnapshot CapturePerceptionSnapshot()
    {
        if (_tickCount == 0)
            return default;

        var neighborStats = _instrumentation.NeighborCountStats;
        float[] nearestDistances = new float[_lastTickAgentCount];
        float[] nearestAngles = new float[_lastTickAgentCount];
        int[] whiskerCounts = new int[_lastTickAgentCount];
        Array.Copy(_nearestDistances, nearestDistances, _lastTickAgentCount);
        Array.Copy(_nearestAngles, nearestAngles, _lastTickAgentCount);
        Array.Copy(_whiskerCounts, whiskerCounts, _lastTickAgentCount);

        return new PerceptionSnapshot(
            _tickCount,
            _lastTickAgentCount,
            neighborStats.Avg,
            _neighborDistanceSamples > 0 ? _neighborDistanceSum / _neighborDistanceSamples : 0f,
            _neighborDistanceSamples > 0 ? _minNeighborDistance : 0f,
            _neighborDistanceSamples > 0 ? _maxNeighborDistance : 0f,
            _instrumentation.AverageNeighborWeight,
            _instrumentation.AverageSeparationMagnitude,
            _instrumentation.AverageAlignmentMagnitude,
            _instrumentation.AverageCohesionMagnitude,
            neighborStats,
            _separationPriorityTriggered,
            nearestDistances,
            nearestAngles,
            whiskerCounts);
    }

    /// <summary>
    /// Legacy compatibility shape for researcher telemetry, not a policy observation.
    /// New callers should reuse <see cref="CanonicalTelemetry"/> with explicit capture.
    /// </summary>
    public readonly struct PerceptionSnapshot
    {
        public PerceptionSnapshot(
            ulong tick,
            int agentCount,
            float averageNeighborCount,
            float averageNeighborDistance,
            float minNeighborDistance,
            float maxNeighborDistance,
            float averageNeighborWeight,
            float averageSeparationMagnitude,
            float averageAlignmentMagnitude,
            float averageCohesionMagnitude,
            (int Min, int Max, float Avg) neighborCountStats,
            bool separationPriorityTriggered,
            float[] nearestDistances,
            float[] nearestAngles,
            int[] whiskerCounts)
        {
            TickCount = tick;
            AgentCount = agentCount;
            AverageNeighborCount = averageNeighborCount;
            AverageNeighborDistance = averageNeighborDistance;
            MinNeighborDistance = minNeighborDistance;
            MaxNeighborDistance = maxNeighborDistance;
            AverageNeighborWeight = averageNeighborWeight;
            AverageSeparationMagnitude = averageSeparationMagnitude;
            AverageAlignmentMagnitude = averageAlignmentMagnitude;
            AverageCohesionMagnitude = averageCohesionMagnitude;
            NeighborCountStats = neighborCountStats;
            SeparationPriorityTriggered = separationPriorityTriggered;
            NearestDistances = nearestDistances;
            NearestAngles = nearestAngles;
            WhiskerCounts = whiskerCounts;
        }

        public ulong TickCount { get; }
        public int AgentCount { get; }
        public float AverageNeighborCount { get; }
        public float AverageNeighborDistance { get; }
        public float MinNeighborDistance { get; }
        public float MaxNeighborDistance { get; }
        public float AverageNeighborWeight { get; }
        public float AverageSeparationMagnitude { get; }
        public float AverageAlignmentMagnitude { get; }
        public float AverageCohesionMagnitude { get; }
        public (int Min, int Max, float Avg) NeighborCountStats { get; }
        public bool SeparationPriorityTriggered { get; }
        public float[] NearestDistances { get; }
        public float[] NearestAngles { get; }
        public int[] WhiskerCounts { get; }
    }
}
