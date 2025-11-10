using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Systems;

/// <summary>
/// Queries the spatial grid to find neighbors and computes local aggregates for each agent.
/// These aggregates are used by BehaviorSystem to compute boids forces.
///
/// INVARIANTS:
/// - Reads from: X[], Y[], Vx[], Vy[], Group[], State[], Grid
/// - Writes to: NeighborCount[], SeparationX[], SeparationY[], AlignmentX[], AlignmentY[], CohesionX[], CohesionY[]
/// - Must not allocate memory
/// - Uses World.Grid for spatial queries
///
/// ALGORITHM:
/// For each agent:
/// 1. Query 3x3 grid neighborhood
/// 2. For each neighbor within SenseRadius:
///    - Count neighbors
///    - Accumulate separation vector (weighted by 1/r^2)
///    - Accumulate alignment vector (sum of velocities)
///    - Accumulate cohesion center (sum of positions)
/// 3. Store aggregates for BehaviorSystem to use
/// </summary>
public sealed class SenseSystem : ISimSystem
{
    // Per-agent aggregates (allocated once, reused each tick)
    private int[] _neighborCount = null!;

    // Separation: accumulated repulsion vectors (weighted by 1/r^2)
    private float[] _separationX = null!;
    private float[] _separationY = null!;

    // Alignment: sum of neighbor velocities
    private float[] _alignmentVx = null!;
    private float[] _alignmentVy = null!;

    // Cohesion: sum of neighbor positions
    private float[] _cohesionX = null!;
    private float[] _cohesionY = null!;

    // Temporary buffer for grid queries (stack-allocated during Run)
    private const int MaxNeighborsToCheck = 256;

    /// <summary>
    /// Initializes arrays to match world capacity.
    /// Called once by World during setup.
    /// </summary>
    public void Initialize(int capacity)
    {
        _neighborCount = new int[capacity];
        _separationX = new float[capacity];
        _separationY = new float[capacity];
        _alignmentVx = new float[capacity];
        _alignmentVy = new float[capacity];
        _cohesionX = new float[capacity];
        _cohesionY = new float[capacity];
    }

    /// <summary>
    /// Provides read-only access to neighbor counts for other systems.
    /// </summary>
    public ReadOnlySpan<int> NeighborCounts => _neighborCount;

    /// <summary>
    /// Provides read-only access to separation vectors.
    /// </summary>
    public ReadOnlySpan<float> SeparationX => _separationX;
    public ReadOnlySpan<float> SeparationY => _separationY;

    /// <summary>
    /// Provides read-only access to alignment vectors.
    /// </summary>
    public ReadOnlySpan<float> AlignmentVx => _alignmentVx;
    public ReadOnlySpan<float> AlignmentVy => _alignmentVy;

    /// <summary>
    /// Provides read-only access to cohesion centers.
    /// </summary>
    public ReadOnlySpan<float> CohesionX => _cohesionX;
    public ReadOnlySpan<float> CohesionY => _cohesionY;

    public void Run(World world, float dt)
    {
        var config = world.Config;
        int count = world.Count;

        var x = world.X;
        var y = world.Y;
        var vx = world.Vx;
        var vy = world.Vy;
        var group = world.Group;
        var state = world.State;
        var grid = world.Grid;

        float senseRadius = config.SenseRadius;
        float senseRadiusSq = senseRadius * senseRadius;
        float separationRadius = config.SeparationRadius;
        float separationRadiusSq = separationRadius * separationRadius;

        // Clear aggregates
        Array.Clear(_neighborCount, 0, count);
        Array.Clear(_separationX, 0, count);
        Array.Clear(_separationY, 0, count);
        Array.Clear(_alignmentVx, 0, count);
        Array.Clear(_alignmentVy, 0, count);
        Array.Clear(_cohesionX, 0, count);
        Array.Clear(_cohesionY, 0, count);

        // Stack-allocated buffer for neighbor queries
        Span<int> neighbors = stackalloc int[MaxNeighborsToCheck];

        for (int i = 0; i < count; i++)
        {
            // Skip dead agents
            if (state[i].HasFlag(AgentState.Dead))
                continue;

            float agentX = x[i];
            float agentY = y[i];
            byte agentGroup = group[i];

            // Query 3x3 neighborhood
            int neighborCount = grid.Query3x3(agentX, agentY, neighbors, MaxNeighborsToCheck);

            int senseCount = 0;
            float separationAccumX = 0f;
            float separationAccumY = 0f;
            float alignmentAccumVx = 0f;
            float alignmentAccumVy = 0f;
            float cohesionAccumX = 0f;
            float cohesionAccumY = 0f;

            // Process each potential neighbor
            for (int n = 0; n < neighborCount && n < MaxNeighborsToCheck; n++)
            {
                int j = neighbors[n];

                // Skip self
                if (i == j)
                    continue;

                // Skip dead agents
                if (state[j].HasFlag(AgentState.Dead))
                    continue;

                // Skip different groups (for now - Phase 3 will use aggression matrix)
                if (group[j] != agentGroup)
                    continue;

                // Calculate distance
                float dx = x[j] - agentX;
                float dy = y[j] - agentY;
                float distSq = dx * dx + dy * dy;

                // Check if within sense radius
                if (distSq > senseRadiusSq || distSq < 0.0001f) // Avoid division by zero
                    continue;

                senseCount++;

                // Separation: LINEAR repulsion within protected radius
                if (distSq < separationRadiusSq)
                {
                    // Linear repulsion: stronger when closer, but bounded
                    float dist = MathF.Sqrt(distSq);
                    if (dist > 0.001f) // Avoid division by zero
                    {
                        // Normalize direction and scale by (1 - distance/radius)
                        // This gives 1.0 repulsion at distance 0, 0.0 at separationRadius
                        float repulsionStrength = (separationRadius - dist) / separationRadius;
                        float normX = dx / dist;
                        float normY = dy / dist;

                        // Accumulate repulsion (pointing away from neighbor)
                        separationAccumX -= normX * repulsionStrength;
                        separationAccumY -= normY * repulsionStrength;
                    }
                }

                // Alignment: sum of neighbor velocities
                alignmentAccumVx += vx[j];
                alignmentAccumVy += vy[j];

                // Cohesion: sum of neighbor positions
                cohesionAccumX += x[j];
                cohesionAccumY += y[j];
            }

            // Store aggregates
            _neighborCount[i] = senseCount;
            _separationX[i] = separationAccumX;
            _separationY[i] = separationAccumY;
            _alignmentVx[i] = alignmentAccumVx;
            _alignmentVy[i] = alignmentAccumVy;
            _cohesionX[i] = cohesionAccumX;
            _cohesionY[i] = cohesionAccumY;
        }
    }
}
