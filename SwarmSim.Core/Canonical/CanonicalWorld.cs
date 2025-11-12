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

        InitializeDefaultRules();
    }

    public int Count { get; private set; }

    public ReadOnlySpan<Boid> Boids => _activeBoids.AsSpan(0, Count);

    public CanonicalWorldSettings Settings => _settings;

    public RuleInstrumentation Instrumentation => _instrumentation;

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

    public void Step(float deltaTime)
    {
        if (deltaTime <= 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be positive.");

        var current = _activeBoids.AsSpan(0, Count);
        _spatialIndex.Rebuild(current);
        _instrumentation.Prepare(Count);
        var context = new RuleContext(
            Settings.TargetSpeed,
            Settings.MaxForce,
            Settings.SenseRadius,
            Settings.FieldOfView,
            deltaTime,
            _instrumentation);
        var next = _nextBoids.AsSpan(0, Count);

        for (int i = 0; i < Count; i++)
        {
            Boid boid = current[i];
            Vec2 steering = Vec2.Zero;

            if (_rules.Count > 0)
            {
                int neighborCount = _spatialIndex.QueryNeighbors(current, i, Settings.SenseRadius, _neighborScratch);
                int filtered = FilterByFieldOfView(
                    boid.Forward,
                    boid.Position,
                    _neighborScratch.AsSpan(0, neighborCount),
                    _neighborWeightScratch,
                    current,
                    context.FieldOfViewCos,
                    i,
                    out float neighborWeightSum);
                var neighbors = _neighborScratch.AsSpan(0, filtered);
                var neighborWeights = _neighborWeightScratch.AsSpan(0, filtered);

                foreach (IRule rule in _rules)
                {
                    steering += rule.Compute(i, boid, current, neighbors, neighborWeights, context);
                }

                _instrumentation.SetNeighborCount(i, filtered);
                _instrumentation.SetNeighborWeightSum(i, neighborWeightSum);

                steering = steering.ClampMagnitude(Settings.MaxForce);
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
    }

    private void SwapBuffers()
    {
        ( _activeBoids, _nextBoids ) = ( _nextBoids, _activeBoids );
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

    private void InitializeDefaultRules()
    {
        AddRule(new SeparationRule(_settings.SeparationWeight, _settings.SeparationRadius));
        AddRule(new AlignmentRule(_settings.AlignmentWeight));
        AddRule(new CohesionRule(_settings.CohesionWeight));
    }
}
