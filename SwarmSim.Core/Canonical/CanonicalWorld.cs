using System;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Canonical;

public sealed class CanonicalWorld
{
    private readonly CanonicalWorldSettings _settings;
    private readonly ISpatialIndex _spatialIndex;
    private readonly List<IRule> _rules = new();

    private Boid[] _activeBoids;
    private Boid[] _nextBoids;
    private readonly int[] _neighborScratch;
    private readonly float[] _neighborWeightScratch;
    private readonly RuleInstrumentation _instrumentation;
    private readonly Rng?[] _wanderRngs;
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
    private PerceptionSnapshot _lastPerceptionSnapshot;

    public CanonicalWorld(CanonicalWorldSettings settings, ISpatialIndex spatialIndex)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _spatialIndex = spatialIndex ?? throw new ArgumentNullException(nameof(spatialIndex));
        Rng.ValidateExternalSeed(settings.Seed, nameof(settings));
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

        int capacity = Math.Max(settings.InitialCapacity, 1);
        _activeBoids = new Boid[capacity];
        _nextBoids = new Boid[capacity];

        int maxNeighbors = Math.Max(settings.MaxNeighbors, 4);
        EffectiveMaxNeighbors = maxNeighbors;
        _neighborScratch = new int[maxNeighbors];
        _neighborWeightScratch = new float[maxNeighbors];
        _instrumentation = new RuleInstrumentation(capacity);
        _priorityState = new bool[capacity];
        _priorityBlend = new float[capacity];
        _priorityHoldTimers = new float[capacity];
        _nearestDistances = new float[capacity];
        _nearestAngles = new float[capacity];
        _whiskerCounts = new int[capacity];
        _wanderAngles = new float[capacity];
        _wanderRngs = new Rng?[capacity];

        _spatialIndex.Initialize(capacity);

        InitializeDefaultRules();
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

    public RuleInstrumentation Instrumentation => _instrumentation;

    public ulong TickCount => _tickCount;

    public void AddRule(IRule rule)
    {
        _rules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
    }

    public bool TryAddBoid(Vec2 position, Vec2 velocity, byte group = 0)
    {
        if (Count >= _activeBoids.Length)
            return false;

        Vec2 normalizedVelocity = velocity.IsNearlyZero()
            ? new Vec2(1f, 0f).WithLength(Settings.TargetSpeed)
            : velocity.WithLength(Settings.TargetSpeed);

        int index = Count;
        if (Settings.WanderStrength > 0f)
        {
            Rng wanderRng = Rng.CreateFromDerivedSeed(DeriveWanderSeed(Settings.Seed, index));
            _wanderRngs[index] = wanderRng;
            _wanderAngles[index] = wanderRng.NextFloat(0f, MathF.PI * 2f);
        }
        _activeBoids[index] = new Boid(position, normalizedVelocity, group);
        Count = index + 1;
        return true;
    }

    internal static uint DeriveWanderSeed(uint seed, int agentIndex)
    {
        uint value = seed + 0x9E3779B9u * ((uint)agentIndex + 1u);
        value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
        value = (value ^ (value >> 13)) * 0xC2B2AE35u;
        value ^= value >> 16;
        return value;
    }

    public void SetVelocity(int index, Vec2 velocity)
    {
        if (index < 0 || index >= Count)
            return;

        Vec2 normalizedVelocity = velocity.WithLength(Settings.TargetSpeed);
        var boid = _activeBoids[index];
        _activeBoids[index] = new Boid(boid.Position, normalizedVelocity, boid.Group);
    }

    public void Step(float deltaTime)
    {
        if (deltaTime <= 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be positive.");

        var current = _activeBoids.AsSpan(0, Count);
        _spatialIndex.Rebuild(current);
        _instrumentation.Prepare(Count);
        _neighborDistanceSum = 0f;
        _neighborDistanceSamples = 0;
        _minNeighborDistance = float.MaxValue;
        _maxNeighborDistance = 0f;
        var next = _nextBoids.AsSpan(0, Count);
        float fieldOfViewCos = MathF.Cos((Settings.FieldOfView * MathF.PI / 180f) * 0.5f);
        float separationEnterThreshold = MathF.Max(0f, Settings.SeparationPriorityRadiusFactor * Settings.SenseRadius);
        float separationExitThreshold = MathF.Max(separationEnterThreshold, Settings.SeparationPriorityExitFactor * Settings.SenseRadius);
        float rampInTime = Math.Max(Settings.SeparationPriorityRampInTime, 1e-6f);
        float rampOutTime = Math.Max(Settings.SeparationPriorityRampOutTime, 1e-6f);
        float holdTime = Math.Max(Settings.SeparationPriorityHoldTime, 0f);
        _separationPriorityTriggered = false;

        for (int i = 0; i < Count; i++)
        {
            Boid boid = current[i];
            Vec2 steering = Vec2.Zero;
            float remainingForce = Settings.MaxForce;
            bool separationDominant = false;
            Vec2 nearestDelta = Vec2.Zero;
            int whiskerHitCount = 0;

            if (_rules.Count > 0)
            {
                SpatialQueryResult query = _spatialIndex.QueryNeighbors(current, i, Settings.SenseRadius, _neighborScratch);
                int neighborCount = query.Count;
                float fieldOfViewDegrees = Settings.FieldOfView;

                int filtered = FilterByFieldOfView(
                    boid.Forward,
                    boid.Position,
                    _neighborScratch.AsSpan(0, neighborCount),
                    _neighborWeightScratch,
                    current,
                    fieldOfViewCos,
                    fieldOfViewDegrees,
                    i,
                    Settings.WorldWidth,
                    Settings.WorldHeight,
                    out float neighborWeightSum);
                var neighbors = _neighborScratch.AsSpan(0, filtered);
                var neighborWeights = _neighborWeightScratch.AsSpan(0, filtered);

                if (filtered > 0 && remainingForce > 0f)
                {
                    Vec2 forward = boid.Forward;
                    float lookAhead = Settings.TargetSpeed * MathF.Max(0.05f, Settings.WhiskerTimeHorizon);
                    float whiskerRadius = MathF.Max(0.1f, Settings.SeparationRadius);
                    Vec2 right = new Vec2(forward.Y, -forward.X);
                    Vec2 whiskerAccum = Vec2.Zero;

                    foreach (int idx in neighbors)
                    {
                        Vec2 toN = Vec2.MinimumImageDelta(boid.Position, current[idx].Position, Settings.WorldWidth, Settings.WorldHeight);
                        float along = Vec2.Dot(forward, toN);
                        if (along <= 0f || along > lookAhead) continue;
                        float lateral = Vec2.Dot(right, toN);
                        float absLat = MathF.Abs(lateral);
                        if (absLat > whiskerRadius) continue;
                        whiskerHitCount++;
                        float side = lateral >= 0f ? 1f : -1f;
                        float gain = (1f - absLat / whiskerRadius) * (1f - along / lookAhead);
                        whiskerAccum += right * (side * gain);
                    }

                    if (!whiskerAccum.IsNearlyZero())
                    {
                        Vec2 desired = whiskerAccum.WithLength(Settings.TargetSpeed * Settings.WhiskerWeight);
                        Vec2 steerW = desired - boid.Velocity;
                        TryAccumulateSteering(ref steering, ref remainingForce, steerW, out _);
                    }
                }

                _whiskerCounts[i] = whiskerHitCount;

                float minDistForAgent = float.MaxValue;
                float maxDistForAgent = 0f;
                float distanceSum = 0f;
                float nearestAngle = 0f;

                if (filtered > 0)
                {
                    (distanceSum, minDistForAgent, maxDistForAgent, nearestDelta, _) = ComputeNeighborDistanceStats(
                        current,
                        boid.Position,
                        neighbors,
                        Settings.WorldWidth,
                        Settings.WorldHeight);
                    _neighborDistanceSum += distanceSum;
                    _neighborDistanceSamples += filtered;
                    _minNeighborDistance = MathF.Min(_minNeighborDistance, minDistForAgent);
                    _maxNeighborDistance = MathF.Max(_maxNeighborDistance, maxDistForAgent);

                    if (!nearestDelta.IsNearlyZero())
                    {
                        Vec2 toNearest = nearestDelta.Normalized;
                        float dotProduct = Vec2.Dot(boid.Forward, toNearest);
                        nearestAngle = MathF.Acos(MathUtils.Clamp(dotProduct, -1f, 1f)) * 180f / MathF.PI;
                    }
                }

                _nearestDistances[i] = minDistForAgent;
                _nearestAngles[i] = nearestAngle;

                if (_priorityState[i] && _priorityHoldTimers[i] > 0f)
                {
                    _priorityHoldTimers[i] -= deltaTime;
                }

                bool shouldEnterPriority = filtered > 0 && minDistForAgent <= separationEnterThreshold;
                if (shouldEnterPriority && !_priorityState[i])
                {
                    _priorityState[i] = true;
                    _priorityHoldTimers[i] = holdTime;
                }
                else if (_priorityState[i])
                {
                    bool shouldExitPriority = (filtered == 0 || minDistForAgent >= separationExitThreshold) && _priorityHoldTimers[i] <= 0f;
                    if (shouldExitPriority)
                    {
                        _priorityState[i] = false;
                        _priorityHoldTimers[i] = 0f;
                    }
                }

                float targetBlend = _priorityState[i] ? 1f : 0f;
                float maxDelta = _priorityState[i] ? Math.Min(1f, deltaTime / rampInTime) : Math.Min(1f, deltaTime / rampOutTime);
                _priorityBlend[i] = MathUtils.MoveTowards(_priorityBlend[i], targetBlend, maxDelta);
                bool priorityActive = _priorityBlend[i] > 0f;
                _separationPriorityTriggered |= priorityActive;
                separationDominant = priorityActive;
                float separationBoost = MathUtils.Lerp(1f, Settings.SeparationPriorityBoost, _priorityBlend[i]);

                var context = new RuleContext(
                    Settings.TargetSpeed,
                    Settings.MaxForce,
                    Settings.SenseRadius,
                    fieldOfViewCos,
                    deltaTime,
                    separationBoost,
                    _instrumentation,
                    Settings.WorldWidth,
                    Settings.WorldHeight);

                if (_rules.Count > 0)
                {
                    Vec2 separation = _rules[0].Compute(i, boid, current, neighbors, neighborWeights, context);
                    Vec2 clampedSep = separation.ClampMagnitude(Settings.MaxForce);
                    if (clampedSep.LengthSquared > 1e-6f)
                    {
                        steering += clampedSep;
                        remainingForce = 0f;
                        _instrumentation.RecordSeparation(i, clampedSep.Length);
                    }
                }

                if (_rules.Count > 1)
                {
                    Vec2 alignment = _rules[1].Compute(i, boid, current, neighbors, neighborWeights, context);
                    float alignmentAttenuation = 1f - (_priorityBlend[i] * 0.7f);
                    alignment *= alignmentAttenuation;
                    if (TryAccumulateSteering(ref steering, ref remainingForce, alignment, out float alignMagnitude))
                    {
                        _instrumentation.RecordAlignment(i, alignMagnitude);
                    }
                }

                if (_rules.Count > 2)
                {
                    Vec2 cohesion = _rules[2].Compute(i, boid, current, neighbors, neighborWeights, context);
                    float cohesionAttenuation = 1f - (_priorityBlend[i] * 0.7f);
                    cohesion *= cohesionAttenuation;
                    if (TryAccumulateSteering(ref steering, ref remainingForce, cohesion, out float cohMagnitude))
                    {
                        _instrumentation.RecordCohesion(i, cohMagnitude);
                    }
                }

                for (int ruleIndex = 3; ruleIndex < _rules.Count; ruleIndex++)
                {
                    _ = _rules[ruleIndex].Compute(i, boid, current, neighbors, neighborWeights, context);
                }

                _instrumentation.SetNeighborCount(i, filtered);
                _instrumentation.SetNeighborWeightSum(i, neighborWeightSum);
            }

            if (Settings.WanderStrength > 0f && remainingForce > 0f)
            {
                Rng wanderRng = _wanderRngs[i]!;
                float angleChange = wanderRng.NextFloat(-1f, 1f) * Settings.WanderRate * deltaTime;
                _wanderAngles[i] += angleChange;
                Vec2 wanderDirection = new Vec2(MathF.Cos(_wanderAngles[i]), MathF.Sin(_wanderAngles[i]));
                Vec2 wander = wanderDirection * Settings.WanderStrength * Settings.TargetSpeed;
                TryAccumulateSteering(ref steering, ref remainingForce, wander, out _);
            }

            Vec2 nextVelocity = boid.Velocity + steering * deltaTime;
            float prioritySpeed = Settings.TargetSpeed * (1f - Settings.SeparationSpeedDroop * _priorityBlend[i]);
            float allowedSpeed = _priorityBlend[i] > 0f ? prioritySpeed : Settings.TargetSpeed;

            if (!nearestDelta.IsNearlyZero())
            {
                float nearestDist = MathF.Sqrt(nearestDelta.LengthSquared);
                float rSoft = Settings.SeparationRadius * 1.2f;
                float rHard = separationEnterThreshold;
                float rGradualStart = Settings.SeparationRadius * 2.0f;

                if (nearestDist < rGradualStart)
                {
                    Vec2 awayDir = (-nearestDelta).Normalized;
                    Vec2 forward = boid.Forward;
                    Vec2 right = new Vec2(forward.Y, -forward.X);
                    float lateralComponent = Vec2.Dot(awayDir, right);
                    Vec2 lateralDir = right * MathF.Sign(lateralComponent);
                    float blendWeight = MathUtils.SmoothStep(rHard, rSoft, nearestDist);
                    Vec2 shapedAvoidance = Vec2.Lerp(awayDir, lateralDir, blendWeight);
                    float distanceRatio = MathUtils.Clamp01((rGradualStart - nearestDist) / (rGradualStart - rHard));
                    float gradualInfluence = distanceRatio * distanceRatio;
                    float combinedInfluence = MathF.Max(_priorityBlend[i], gradualInfluence * 0.5f);
                    Vec2 biasedVelocity = Vec2.Lerp(nextVelocity, shapedAvoidance.WithLength(allowedSpeed), combinedInfluence * 0.7f);
                    nextVelocity = biasedVelocity;
                }
            }
            Vec2 normalizedCurrent = boid.Velocity.IsNearlyZero() ? boid.Forward : boid.Velocity;
            float currentAngle = MathF.Atan2(normalizedCurrent.Y, normalizedCurrent.X);
            float desiredAngle = MathF.Atan2(nextVelocity.Y, nextVelocity.X);
            float deltaAngle = MathUtils.AngleDifference(currentAngle, desiredAngle);
            float maxTurnRad = Settings.MaxTurnRateDegPerSecond * MathF.PI / 180f * deltaTime;
            float clampedAngle = MathUtils.Clamp(deltaAngle, -maxTurnRad, maxTurnRad);
            float finalAngle = currentAngle + clampedAngle;
            Vec2 limitedDir = new Vec2(MathF.Cos(finalAngle), MathF.Sin(finalAngle));
            nextVelocity = limitedDir.WithLength(allowedSpeed);

            Vec2 nextPosition = boid.Position + nextVelocity * deltaTime;
            (float wrappedX, float wrappedY) = MathUtils.WrapPosition(nextPosition.X, nextPosition.Y, Settings.WorldWidth, Settings.WorldHeight);
            nextPosition = new Vec2(wrappedX, wrappedY);
            next[i] = new Boid(nextPosition, nextVelocity, boid.Group);
        }

        SwapBuffers();
        _tickCount++;
        UpdatePerceptionSnapshot();
    }

    private void SwapBuffers()
    {
        ( _activeBoids, _nextBoids ) = ( _nextBoids, _activeBoids );
    }

    private static bool TryAccumulateSteering(ref Vec2 total, ref float remaining, Vec2 contribution, out float appliedMagnitude)
    {
        appliedMagnitude = 0f;
        if (remaining <= 0f)
            return false;

        float lengthSq = contribution.LengthSquared;
        if (lengthSq <= 1e-6f)
            return false;

        float length = MathF.Sqrt(lengthSq);
        float spend = MathF.Min(length, remaining);
        Vec2 normalized = contribution / length;
        Vec2 clamped = normalized * spend;
        total += clamped;
        remaining -= spend;
        appliedMagnitude = spend;
        return true;
    }

    private static (float distanceSum, float minDist, float maxDist, Vec2 nearestDelta, int nearestIdx) ComputeNeighborDistanceStats(
        ReadOnlySpan<Boid> boids,
        Vec2 origin,
        ReadOnlySpan<int> neighbors,
        float worldWidth,
        float worldHeight)
    {
        float sum = 0f;
        float minDist = float.MaxValue;
        float maxDist = 0f;
        Vec2 nearestDelta = Vec2.Zero;
        int nearestIdx = -1;

        foreach (int idx in neighbors)
        {
            Vec2 delta = Vec2.MinimumImageDelta(origin, boids[idx].Position, worldWidth, worldHeight);
            float dist = MathF.Sqrt(delta.LengthSquared);
            sum += dist;
            if (dist < minDist)
            {
                minDist = dist;
                nearestDelta = delta;
                nearestIdx = idx;
            }
            maxDist = MathF.Max(maxDist, dist);
        }

        return (sum, minDist == float.MaxValue ? 0f : minDist, maxDist, nearestDelta, nearestIdx);
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
    {
        if (index < 0 || index >= Count)
            return new SpatialQueryResult(0, false);

        var boids = _activeBoids.AsSpan(0, Count);
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

    public PerceptionSnapshot CapturePerceptionSnapshot() => _lastPerceptionSnapshot;

    private void InitializeDefaultRules()
    {
        AddRule(new SeparationRule(_settings.SeparationWeight, _settings.SeparationRadius));
        AddRule(new AlignmentRule(_settings.AlignmentWeight));
        AddRule(new CohesionRule(_settings.CohesionWeight));
    }

    private void UpdatePerceptionSnapshot()
    {
        var neighborStats = _instrumentation.NeighborCountStats;
        float avgDistance = _neighborDistanceSamples > 0 ? _neighborDistanceSum / _neighborDistanceSamples : 0f;
        float minDistance = _neighborDistanceSamples > 0 ? _minNeighborDistance : 0f;
        float maxDistance = _neighborDistanceSamples > 0 ? _maxNeighborDistance : 0f;

        float[] nearestDistancesCopy = new float[Count];
        float[] nearestAnglesCopy = new float[Count];
        int[] whiskerCountsCopy = new int[Count];
        Array.Copy(_nearestDistances, nearestDistancesCopy, Count);
        Array.Copy(_nearestAngles, nearestAnglesCopy, Count);
        Array.Copy(_whiskerCounts, whiskerCountsCopy, Count);

        _lastPerceptionSnapshot = new PerceptionSnapshot(
            _tickCount,
            Count,
            neighborStats.Avg,
            avgDistance,
            minDistance,
            maxDistance,
            _instrumentation.AverageNeighborWeight,
            _instrumentation.AverageSeparationMagnitude,
            _instrumentation.AverageAlignmentMagnitude,
            _instrumentation.AverageCohesionMagnitude,
            neighborStats,
            _separationPriorityTriggered,
            nearestDistancesCopy,
            nearestAnglesCopy,
            whiskerCountsCopy);
    }

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
