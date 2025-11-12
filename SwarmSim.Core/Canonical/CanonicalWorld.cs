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

        _spatialIndex.Initialize(capacity);

        InitializeDefaultRules();
    }

    public int Count { get; private set; }

    public ReadOnlySpan<Boid> Boids => _activeBoids.AsSpan(0, Count);

    public CanonicalWorldSettings Settings => _settings;

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
        var context = new RuleContext(Settings.TargetSpeed, Settings.MaxForce, Settings.SenseRadius, Settings.FieldOfView, deltaTime);
        var next = _nextBoids.AsSpan(0, Count);

        for (int i = 0; i < Count; i++)
        {
            Boid boid = current[i];
            Vec2 steering = Vec2.Zero;

            if (_rules.Count > 0)
            {
                int neighborCount = _spatialIndex.QueryNeighbors(current, i, Settings.SenseRadius, _neighborScratch);
                int filtered = FilterByFieldOfView(boid.Forward, boid.Position, _neighborScratch.AsSpan(0, neighborCount), current, context.FieldOfViewCos);
                var neighbors = _neighborScratch.AsSpan(0, filtered);

                foreach (IRule rule in _rules)
                {
                    steering += rule.Compute(i, boid, current, neighbors, context);
                }

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

    private static int FilterByFieldOfView(Vec2 forward, Vec2 origin, Span<int> candidates, ReadOnlySpan<Boid> boids, float fieldOfViewCos)
    {
        if (candidates.IsEmpty)
            return 0;

        bool fullCircle = fieldOfViewCos <= -1f;
        int keep = 0;

        for (int i = 0; i < candidates.Length; i++)
        {
            int index = candidates[i];
            Vec2 delta = boids[index].Position - origin;

            if (delta.IsNearlyZero())
            {
                candidates[keep++] = index;
                continue;
            }

            if (fullCircle)
            {
                candidates[keep++] = index;
                continue;
            }

            Vec2 direction = delta.Normalized;
            if (Vec2.Dot(forward, direction) >= fieldOfViewCos)
            {
                candidates[keep++] = index;
            }
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
