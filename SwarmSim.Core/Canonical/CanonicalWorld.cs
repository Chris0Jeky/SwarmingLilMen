namespace SwarmSim.Core.Canonical;

public sealed class CanonicalWorld
{
    private readonly CanonicalWorldSettings _settings;
    private readonly ISpatialIndex _spatialIndex;
    private readonly List<IRule> _rules = new();

    private Boid[] _activeBoids;
    private Boid[] _nextBoids;
    private readonly int[] _neighborScratch;

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
                if (neighborCount > 0)
                {
                    var neighbors = _neighborScratch.AsSpan(0, neighborCount);
                    foreach (IRule rule in _rules)
                    {
                        steering += rule.Compute(boid, current, neighbors, context);
                    }

                    steering = steering.ClampMagnitude(Settings.MaxForce);
                }
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
            next[i] = new Boid(nextPosition, nextVelocity, boid.Group);
        }

        SwapBuffers();
    }

    private void SwapBuffers()
    {
        ( _activeBoids, _nextBoids ) = ( _nextBoids, _activeBoids );
    }
}
