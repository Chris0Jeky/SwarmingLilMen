using SwarmSim.Core.Spatial;
using SwarmSim.Core.Systems;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core;

/// <summary>
/// The simulation world containing all agents in Structure-of-Arrays (SoA) layout.
/// This class owns all agent data and provides controlled access for systems.
///
/// INVARIANTS:
/// - Count <= Capacity always holds
/// - Active agents are in indices [0, Count)
/// - Dead/free slots managed via free-list (not yet implemented)
/// - All arrays have length == Capacity
/// - No allocations during Tick() after initialization
/// </summary>
public sealed class World
{
    // ===== Configuration =====
    public SimConfig Config { get; private set; }
    public Rng Rng { get; private set; }

    // ===== Spatial Partitioning & Systems =====
    public UniformGrid Grid { get; private set; } = null!;
    private readonly List<ISimSystem> _systems = new();

    // Sense system needs to be accessible for BehaviorSystem
    private SenseSystem _senseSystem = null!;

    // ===== Population Tracking =====
    public int Capacity { get; private set; }
    public int Count { get; private set; }

    public ulong TickCount { get; private set; }
    public float SimulationTime { get; private set; }

    // ===== Agent Data (SoA) =====
    // Position
    public float[] X { get; private set; } = null!;
    public float[] Y { get; private set; } = null!;

    // Velocity
    public float[] Vx { get; private set; } = null!;
    public float[] Vy { get; private set; } = null!;

    // Forces (scratch buffers for systems)
    public float[] Fx { get; private set; } = null!;
    public float[] Fy { get; private set; } = null!;

    // Resources
    public float[] Energy { get; private set; } = null!;
    public float[] Health { get; private set; } = null!;
    public float[] Age { get; private set; } = null!; // In seconds

    // State
    public byte[] Group { get; private set; } = null!;
    public AgentState[] State { get; private set; } = null!;
    public Genome[] Genomes { get; private set; } = null!;

    // Combat timers
    public float[] LastAttackTime { get; private set; } = null!;

    // Free-list for efficient recycling (to be implemented)
    // private int _freeListHead = -1;
    // private int[] _nextFree;

    /// <summary>
    /// Creates a new world with the given configuration.
    /// </summary>
    public World(SimConfig config, uint seed)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Rng = new Rng(seed);

        // Validate configuration
        var errors = config.Validate();
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid config: {string.Join(", ", errors)}");

        Initialize(config.InitialCapacity);
    }

    /// <summary>
    /// Initializes or resizes all arrays to the given capacity.
    /// </summary>
    private void Initialize(int capacity)
    {
        Capacity = capacity;
        Count = 0;
        TickCount = 0;
        SimulationTime = 0f;

        // Allocate all arrays
        X = new float[capacity];
        Y = new float[capacity];
        Vx = new float[capacity];
        Vy = new float[capacity];
        Fx = new float[capacity];
        Fy = new float[capacity];
        Energy = new float[capacity];
        Health = new float[capacity];
        Age = new float[capacity];
        Group = new byte[capacity];
        State = new AgentState[capacity];
        Genomes = new Genome[capacity];
        LastAttackTime = new float[capacity];

        // Initialize spatial grid
        // Cell size = SenseRadius for optimal neighbor queries
        Grid = new UniformGrid(
            cellSize: Config.SenseRadius,
            worldWidth: Config.WorldWidth,
            worldHeight: Config.WorldHeight,
            capacity: capacity);

        // Initialize systems (Phase 2: Boids behavior)
        // Systems run in order each tick
        _systems.Clear();

        // Create and initialize SenseSystem
        _senseSystem = new SenseSystem();
        _senseSystem.Initialize(capacity);
        _systems.Add(_senseSystem);

        // BehaviorSystem depends on SenseSystem
        _systems.Add(new BehaviorSystem(_senseSystem));

        // WanderSystem adds small random forces for exploration (optional)
        // Only add if wanderStrength > 0
        if (Config.WanderStrength > 0f)
        {
            _systems.Add(new WanderSystem(wanderStrength: Config.WanderStrength));
        }

        // IntegrateSystem applies forces and updates positions
        _systems.Add(new IntegrateSystem());

        // Future systems:
        // - CombatSystem (Phase 3)
        // - ForageSystem (Phase 3)
        // - ReproductionSystem (Phase 4)
        // - MetabolismSystem (Phase 3)
        // - LifecycleSystem (Phase 3)
    }

    /// <summary>
    /// Adds a new agent to the world at the specified position.
    /// Returns the index of the new agent, or -1 if at capacity.
    /// </summary>
    public int AddAgent(
        float x, float y,
        byte group = 0,
        Genome? genome = null)
    {
        if (Count >= Capacity)
            return -1;

        int idx = Count++;

        // Position
        X[idx] = x;
        Y[idx] = y;

        // Velocity (start at rest)
        Vx[idx] = 0f;
        Vy[idx] = 0f;

        // Forces (cleared)
        Fx[idx] = 0f;
        Fy[idx] = 0f;

        // Resources
        Energy[idx] = Config.InitialEnergy;
        Health[idx] = Config.InitialHealth;
        Age[idx] = 0f;

        // State
        Group[idx] = group;
        State[idx] = AgentState.None;
        Genomes[idx] = genome ?? Genome.Default;
        LastAttackTime[idx] = float.NegativeInfinity;

        return idx;
    }

    /// <summary>
    /// Adds a new agent with random position and genome.
    /// </summary>
    public int AddRandomAgent(byte group = 0)
    {
        float x = Rng.NextFloat(0f, Config.WorldWidth);
        float y = Rng.NextFloat(0f, Config.WorldHeight);
        Genome genome = Genome.Random(Rng);

        return AddAgent(x, y, group, genome);
    }

    /// <summary>
    /// Spawns multiple agents in a circular region.
    /// </summary>
    public int SpawnAgentsInCircle(float centerX, float centerY, float radius, int count, byte group = 0)
    {
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            (float dx, float dy) = Rng.NextPointInCircle(radius);
            float x = centerX + dx;
            float y = centerY + dy;

            // Wrap to world bounds if needed
            if (Config.BoundaryMode == BoundaryMode.Wrap)
            {
                (x, y) = MathUtils.WrapPosition(x, y, Config.WorldWidth, Config.WorldHeight);
            }
            else
            {
                x = MathUtils.Clamp(x, 0f, Config.WorldWidth);
                y = MathUtils.Clamp(y, 0f, Config.WorldHeight);
            }

            Genome genome = Genome.Random(Rng);
            if (AddAgent(x, y, group, genome) >= 0)
                spawned++;
        }
        return spawned;
    }

    /// <summary>
    /// Marks an agent as dead. The slot will be recycled in the next compaction.
    /// </summary>
    public void MarkDead(int idx)
    {
        if (idx < 0 || idx >= Count)
            return;

        State[idx] = State[idx].SetFlag(AgentState.Dead);
    }

    /// <summary>
    /// Removes all dead agents by compacting the arrays.
    /// This is O(n) and should be called periodically, not every tick.
    /// </summary>
    public int CompactDeadAgents()
    {
        int writeIdx = 0;
        int removed = 0;

        for (int readIdx = 0; readIdx < Count; readIdx++)
        {
            if (State[readIdx].HasFlag(AgentState.Dead))
            {
                removed++;
                continue;
            }

            // If we've removed any, copy this agent to the write position
            if (writeIdx != readIdx)
            {
                CopyAgent(readIdx, writeIdx);
            }

            writeIdx++;
        }

        Count = writeIdx;
        return removed;
    }

    /// <summary>
    /// Copies agent data from one index to another (for compaction).
    /// </summary>
    private void CopyAgent(int from, int to)
    {
        X[to] = X[from];
        Y[to] = Y[from];
        Vx[to] = Vx[from];
        Vy[to] = Vy[from];
        Fx[to] = Fx[from];
        Fy[to] = Fy[from];
        Energy[to] = Energy[from];
        Health[to] = Health[from];
        Age[to] = Age[from];
        Group[to] = Group[from];
        State[to] = State[from];
        Genomes[to] = Genomes[from];
        LastAttackTime[to] = LastAttackTime[from];
    }

    /// <summary>
    /// Advances the simulation by one fixed timestep.
    /// MUST NOT ALLOCATE after initial warmup.
    /// </summary>
    public void Tick()
    {
        float dt = Config.FixedDeltaTime;

        // Rebuild spatial grid (O(n) operation)
        Grid.Rebuild(X, Y, Count);

        // Clear force accumulators
        ClearForces();

        // Run all systems in order
        // Phase 2: Boids flocking behavior
        // Current pipeline:
        // 1. SenseSystem - Query neighbors, compute aggregates
        // 2. BehaviorSystem - Apply boids rules (separation, alignment, cohesion)
        // 3. IntegrateSystem - Apply forces, update positions
        //
        // Future phases will add:
        // 4. CombatSystem (Phase 3)
        // 5. ForageSystem (Phase 3)
        // 6. ReproductionSystem (Phase 4)
        // 7. MetabolismSystem (Phase 3)
        // 8. LifecycleSystem (Phase 3)
        foreach (var system in _systems)
        {
            system.Run(this, dt);
        }

        // Update time
        TickCount++;
        SimulationTime += dt;

        // Periodic compaction
        if (TickCount % (ulong)Config.CompactionInterval == 0)
        {
            CompactDeadAgents();
        }
    }

    /// <summary>
    /// Clears force accumulators (called at start of each tick).
    /// </summary>
    private void ClearForces()
    {
        Array.Clear(Fx, 0, Count);
        Array.Clear(Fy, 0, Count);
    }

    /// <summary>
    /// Returns a read-only view of agent positions for rendering.
    /// </summary>
    public ReadOnlySpan<float> GetPositionsX() => new ReadOnlySpan<float>(X, 0, Count);
    public ReadOnlySpan<float> GetPositionsY() => new ReadOnlySpan<float>(Y, 0, Count);

    /// <summary>
    /// Returns a read-only view of agent groups for rendering.
    /// </summary>
    public ReadOnlySpan<byte> GetGroups() => new ReadOnlySpan<byte>(Group, 0, Count);

    /// <summary>
    /// Returns a read-only view of agent genomes for rendering.
    /// </summary>
    public ReadOnlySpan<Genome> GetGenomes() => new ReadOnlySpan<Genome>(Genomes, 0, Count);

    /// <summary>
    /// Gets basic statistics about the simulation.
    /// </summary>
    public WorldStats GetStats()
    {
        int aliveCount = 0;
        float totalEnergy = 0f;
        float totalSpeed = 0f;

        for (int i = 0; i < Count; i++)
        {
            if (!State[i].HasFlag(AgentState.Dead))
            {
                aliveCount++;
                totalEnergy += Energy[i];
                totalSpeed += MathUtils.Length(Vx[i], Vy[i]);
            }
        }

        return new WorldStats
        {
            TickCount = TickCount,
            SimulationTime = SimulationTime,
            TotalAgents = Count,
            AliveAgents = aliveCount,
            AverageEnergy = aliveCount > 0 ? totalEnergy / aliveCount : 0f,
            AverageSpeed = aliveCount > 0 ? totalSpeed / aliveCount : 0f
        };
    }
}

/// <summary>
/// Snapshot of world statistics at a point in time.
/// </summary>
public record struct WorldStats
{
    public ulong TickCount { get; init; }
    public float SimulationTime { get; init; }
    public int TotalAgents { get; init; }
    public int AliveAgents { get; init; }
    public float AverageEnergy { get; init; }
    public float AverageSpeed { get; init; }
}
