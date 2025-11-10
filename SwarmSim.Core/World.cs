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

    // ===== Population Tracking =====
    public int Capacity { get; private set; }
    public int Count { get; private set; }

    public ulong TickCount { get; private set; }
    public float SimulationTime { get; private set; }

    // ===== Agent Data (SoA) =====
    // Position
    public float[] X { get; private set; }
    public float[] Y { get; private set; }

    // Velocity
    public float[] Vx { get; private set; }
    public float[] Vy { get; private set; }

    // Forces (scratch buffers for systems)
    public float[] Fx { get; private set; }
    public float[] Fy { get; private set; }

    // Resources
    public float[] Energy { get; private set; }
    public float[] Health { get; private set; }
    public float[] Age { get; private set; } // In seconds

    // State
    public byte[] Group { get; private set; }
    public AgentState[] State { get; private set; }
    public Genome[] Genomes { get; private set; }

    // Combat timers
    public float[] LastAttackTime { get; private set; }

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

        // Clear force accumulators
        ClearForces();

        // TODO: Run systems in order
        // 1. SenseSystem
        // 2. BehaviorSystem
        // 3. CombatSystem
        // 4. ForageSystem
        // 5. ReproductionSystem
        // 6. MetabolismSystem
        // 7. IntegrateSystem
        // 8. LifecycleSystem

        // For now, just do basic integration with random forces
        BasicIntegration(dt);

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
    /// Basic integration for testing (applies forces, updates velocity and position).
    /// TODO: Replace with proper IntegrateSystem.
    /// </summary>
    private void BasicIntegration(float dt)
    {
        for (int i = 0; i < Count; i++)
        {
            // Skip dead agents
            if (State[i].HasFlag(AgentState.Dead))
                continue;

            // Integrate velocity
            Vx[i] += Fx[i] * dt;
            Vy[i] += Fy[i] * dt;

            // Apply friction
            Vx[i] *= Config.Friction;
            Vy[i] *= Config.Friction;

            // Clamp to max speed
            float speed = MathUtils.Length(Vx[i], Vy[i]);
            if (speed > Config.MaxSpeed)
            {
                float scale = Config.MaxSpeed / speed;
                Vx[i] *= scale;
                Vy[i] *= scale;
            }

            // Integrate position
            X[i] += Vx[i] * dt;
            Y[i] += Vy[i] * dt;

            // Apply boundary conditions
            ApplyBoundary(i);
        }
    }

    /// <summary>
    /// Applies boundary conditions to an agent based on Config.BoundaryMode.
    /// </summary>
    private void ApplyBoundary(int idx)
    {
        switch (Config.BoundaryMode)
        {
            case BoundaryMode.Wrap:
                (X[idx], Y[idx]) = MathUtils.WrapPosition(X[idx], Y[idx], Config.WorldWidth, Config.WorldHeight);
                break;

            case BoundaryMode.Reflect:
                MathUtils.ReflectPosition(ref X[idx], ref Vx[idx], Config.WorldWidth);
                MathUtils.ReflectPosition(ref Y[idx], ref Vy[idx], Config.WorldHeight);
                break;

            case BoundaryMode.Clamp:
                X[idx] = MathUtils.Clamp(X[idx], 0f, Config.WorldWidth);
                Y[idx] = MathUtils.Clamp(Y[idx], 0f, Config.WorldHeight);
                // Stop at boundary
                if (X[idx] == 0f || X[idx] == Config.WorldWidth) Vx[idx] = 0f;
                if (Y[idx] == 0f || Y[idx] == Config.WorldHeight) Vy[idx] = 0f;
                break;
        }
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
