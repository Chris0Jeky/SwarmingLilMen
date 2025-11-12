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
    private readonly Rng _rng;
    private ulong _tickCount;
    private float _neighborDistanceSum;
    private int _neighborDistanceSamples;
    private float _minNeighborDistance = float.MaxValue;
    private float _maxNeighborDistance;
    private PerceptionSnapshot _lastPerceptionSnapshot;

    public CanonicalWorld(CanonicalWorldSettings settings, ISpatialIndex spatialIndex)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _spatialIndex = spatialIndex ?? throw new ArgumentNullException(nameof(spatialIndex));

        int capacity = Math.Max(settings.InitialCapacity, 1);
        _activeBoids = new Boid[capacity];
        _nextBoids = new Boid[capacity];

        int maxNeighbors = Math.Max(settings.MaxNeighbors, 4);
        _neighborScratch = new int[maxNeighbors];
        _neighborWeightScratch = new float[maxNeighbors];
        _instrumentation = new RuleInstrumentation(capacity);

        _spatialIndex.Initialize(capacity);
        _rng = new Rng(settings.Seed);

        InitializeDefaultRules();
    }

    public int Count { get; private set; }

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

        _activeBoids[Count++] = new Boid(position, normalizedVelocity, group);
        return true;
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
        float separationPriorityThreshold = MathF.Max(0f, Settings.SeparationPriorityRadiusFactor * Settings.SenseRadius);

        for (int i = 0; i < Count; i++)
        {
            Boid boid = current[i];
            Vec2 steering = Vec2.Zero;
            float remainingForce = Settings.MaxForce;

            if (_rules.Count > 0)
            {
                int neighborCount = _spatialIndex.QueryNeighbors(current, i, Settings.SenseRadius, _neighborScratch);
                int filtered = FilterByFieldOfView(
                    boid.Forward,
                    boid.Position,
                    _neighborScratch.AsSpan(0, neighborCount),
                    _neighborWeightScratch,
                    current,
                    fieldOfViewCos,
                    i,
                    out float neighborWeightSum);
                var neighbors = _neighborScratch.AsSpan(0, filtered);
                var neighborWeights = _neighborWeightScratch.AsSpan(0, filtered);

                float minDistForAgent = float.MaxValue;
                float maxDistForAgent = 0f;
                float distanceSum = 0f;

                if (filtered > 0)
                {
                    (distanceSum, minDistForAgent, maxDistForAgent) = ComputeNeighborDistanceStats(current, boid.Position, neighbors);
                    _neighborDistanceSum += distanceSum;
                    _neighborDistanceSamples += filtered;
                    _minNeighborDistance = MathF.Min(_minNeighborDistance, minDistForAgent);
                    _maxNeighborDistance = MathF.Max(_maxNeighborDistance, maxDistForAgent);
                }

                float separationBoost = 1f;
                if (filtered > 0 && separationPriorityThreshold > 0f && minDistForAgent < separationPriorityThreshold)
                {
                    float ratio = (separationPriorityThreshold - minDistForAgent) / separationPriorityThreshold;
                    separationBoost = MathUtils.Lerp(1f, Settings.SeparationPriorityBoost, MathUtils.Clamp(ratio, 0f, 1f));
                }

                var context = new RuleContext(
                    Settings.TargetSpeed,
                    Settings.MaxForce,
                    Settings.SenseRadius,
                    fieldOfViewCos,
                    deltaTime,
                    separationBoost,
                    _instrumentation);

                if (_rules.Count > 0)
                {
                    Vec2 separation = _rules[0].Compute(i, boid, current, neighbors, neighborWeights, context);
                    if (TryAccumulateSteering(ref steering, ref remainingForce, separation, out float sepMagnitude))
                    {
                        _instrumentation.RecordSeparation(i, sepMagnitude);
                    }
                }

                if (_rules.Count > 1)
                {
                    Vec2 alignment = _rules[1].Compute(i, boid, current, neighbors, neighborWeights, context);
                    if (TryAccumulateSteering(ref steering, ref remainingForce, alignment, out float alignMagnitude))
                    {
                        _instrumentation.RecordAlignment(i, alignMagnitude);
                    }
                }

                if (_rules.Count > 2)
                {
                    Vec2 cohesion = _rules[2].Compute(i, boid, current, neighbors, neighborWeights, context);
                    if (TryAccumulateSteering(ref steering, ref remainingForce, cohesion, out float cohMagnitude))
                    {
                        _instrumentation.RecordCohesion(i, cohMagnitude);
                    }
                }

                _instrumentation.SetNeighborCount(i, filtered);
                _instrumentation.SetNeighborWeightSum(i, neighborWeightSum);
            }

            if (Settings.WanderStrength > 0f && remainingForce > 0f)
            {
                float jitter = _rng.NextFloat(0f, 2f * MathF.PI);
                Vec2 wander = new Vec2(MathF.Cos(jitter), MathF.Sin(jitter)) * Settings.WanderStrength * Settings.TargetSpeed;
                TryAccumulateSteering(ref steering, ref remainingForce, wander, out _);
            }

            Vec2 nextVelocity = boid.Velocity + steering * deltaTime;
            if (!nextVelocity.IsNearlyZero())
            {
                nextVelocity = nextVelocity.WithLength(Settings.TargetSpeed);
            }
            else
            {
                nextVelocity = boid.Velocity.WithLength(Settings.TargetSpeed);
            }

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

    private static (float distanceSum, float minDist, float maxDist) ComputeNeighborDistanceStats(ReadOnlySpan<Boid> boids, Vec2 origin, ReadOnlySpan<int> neighbors)
    {
        float sum = 0f;
        float minDist = float.MaxValue;
        float maxDist = 0f;

        foreach (int idx in neighbors)
        {
            Vec2 delta = boids[idx].Position - origin;
            float dist = MathF.Sqrt(delta.LengthSquared);
            sum += dist;
            minDist = MathF.Min(minDist, dist);
            maxDist = MathF.Max(maxDist, dist);
        }

        return (sum, minDist == float.MaxValue ? 0f : minDist, maxDist);
    }

    private static int FilterByFieldOfView(
        Vec2 forward,
        Vec2 origin,
        Span<int> candidates,
        Span<float> weights,
        ReadOnlySpan<Boid> boids,
        float fieldOfViewCos,
        int selfIndex,
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
            Vec2 delta = boids[index].Position - origin;

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
            float dot = Vec2.Dot(forward, direction);
            float normalized = (dot - fieldOfViewCos) / range;

            if (normalized <= 0f)
                continue;

            float weight = normalized >= 1f ? 1f : normalized;
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

    public int QueryVisibleNeighbors(int index, Span<int> buffer, Span<float> weights)
    {
        if (index < 0 || index >= Count)
            return 0;

        var boids = _activeBoids.AsSpan(0, Count);
        int neighborCount = _spatialIndex.QueryNeighbors(boids, index, Settings.SenseRadius, buffer);
        float halfAngleRad = (Settings.FieldOfView * MathF.PI / 180f) * 0.5f;
        float fieldOfViewCos = MathF.Cos(halfAngleRad);
        return FilterByFieldOfView(
            boids[index].Forward,
            boids[index].Position,
            buffer.Slice(0, neighborCount),
            weights,
            boids,
            fieldOfViewCos,
            index,
            out _);
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
            neighborStats);
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
            (int Min, int Max, float Avg) neighborCountStats)
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
    }
}
