using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Utils;

namespace SwarmSim.Benchmarks;

// Small fixed workloads: this is an equivalence experiment, not a throughput benchmark.
internal static class CanonicalExperiment
{
    internal enum Workload { Clustered, Seam, Capped, Mixed }

    internal readonly record struct TrajectoryResult(
        string Hash, long GridStepBytes, long NaiveStepBytes, long VisibleNeighbors, long TruncatedQueries);

    internal static void Run(TextWriter output)
    {
        output.WriteLine($"Canonical trajectory experiment; {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}");
        output.WriteLine("96 boids, 24 steps, dt=1/60; exact observable-state comparison at initialization and EVERY step.");
        output.WriteLine("Step allocation bytes exclude construction, oracle serialization and hashing; no wall-clock claim.");
        foreach (Workload workload in Enum.GetValues<Workload>())
        {
            uint seed = workload == Workload.Mixed ? 7919u : 1701u;
            CompareTrajectory(workload, seed, 96, 2); // Warm both paths on disposable worlds.
            TrajectoryResult? previous = null;
            for (int repeat = 0; repeat < 3; repeat++)
            {
                var result = CompareTrajectory(workload, seed, 96, 24, naiveFirst: repeat % 2 != 0);
                Require(previous is null || previous.Value.Hash == result.Hash, "Repeated trajectory changed");
                output.WriteLine($"{workload} seed={seed} repeat={repeat + 1} sha256={result.Hash} gridStepBytes={result.GridStepBytes} naiveStepBytes={result.NaiveStepBytes} visibleNeighborVisits={result.VisibleNeighbors} truncatedQueries={result.TruncatedQueries}");
                previous = result;
            }
        }
        var witness = CapBeforeFovWitness();
        output.WriteLine($"Cap-before-FOV witness: cappedVisible={witness.CappedVisible}, uncappedVisible={witness.UncappedVisible}; first-step state differs. Wider radius buffer is a semantic change.");
        output.WriteLine("Dense single-cell selection replay; array writes INCLUDE final heap sort. Radius checks N*(N-1) are derived from the actual loops, not instrumented CPU operations.");
        foreach (int count in new[] { 4, 32, 128, 256 })
        {
            var result = CanonicalSelectionExperiment.MeasureDenseSelection(count, 32);
            output.WriteLine($"dense N={count} cap=32 queries={count} radiusChecksPerIndex={count * (count - 1)} sortedWrites={result.SortedWrites} heapWrites={result.HeapWrites} gridQueryBytes={result.GridQueryBytes} naiveQueryBytes={result.NaiveQueryBytes}");
        }
    }

    internal static CanonicalWorld CreateWorld(Workload workload, uint seed, int count, bool grid)
    {
        var settings = new CanonicalWorldSettings
        {
            InitialCapacity = count, WorldWidth = 101f, WorldHeight = 79f,
            SenseRadius = 12f, MaxNeighbors = workload == Workload.Capped ? 8 : 32,
            TargetSpeed = 6f, MaxForce = 3f, SeparationRadius = 3f,
            FieldOfView = 240f, WanderStrength = 0.3f, Seed = seed
        };
        ISpatialIndex index = grid
            ? new GridSpatialIndex(settings.SenseRadius, settings.WorldWidth, settings.WorldHeight)
            : new NaiveSpatialIndex(settings.WorldWidth, settings.WorldHeight);
        var world = new CanonicalWorld(settings, index);
        var rng = new Rng(seed);
        for (int i = 0; i < count; i++)
        {
            float x = rng.NextFloat(0f, 1f);
            float y = rng.NextFloat(0f, 1f);
            Vec2 position = workload switch
            {
                Workload.Clustered => new Vec2(20f + i % 3 * 30f + x * 7f, 20f + i % 2 * 35f + y * 7f),
                Workload.Seam => new Vec2(i % 2 == 0 ? x * 4f : 101f - x * 4f, i % 4 < 2 ? y * 4f : 79f - y * 4f),
                Workload.Capped => new Vec2(48f + x * 2f, 36f + y * 2f),
                _ => new Vec2(x * 101f, y * 79f)
            };
            float angle = rng.NextFloat(0f, 2f * MathF.PI);
            Require(world.TryAddBoid(position, new Vec2(MathF.Cos(angle), MathF.Sin(angle)), (byte)(i % 3)), "Spawn failed");
        }
        return world;
    }

    internal static TrajectoryResult CompareTrajectory(Workload workload, uint seed, int count, int steps, bool naiveFirst = false)
    {
        var grid = CreateWorld(workload, seed, count, grid: true);
        var naive = CreateWorld(workload, seed, count, grid: false);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AssertSameState(grid, naive, hash);
        long gridBytes = 0, naiveBytes = 0, neighbors = 0, truncated = 0;
        for (int step = 0; step < steps; step++)
        {
            if (naiveFirst)
            {
                naiveBytes += StepAllocated(naive);
                gridBytes += StepAllocated(grid);
            }
            else
            {
                gridBytes += StepAllocated(grid);
                naiveBytes += StepAllocated(naive);
            }
            AssertSameState(grid, naive, hash);
            foreach (int visible in grid.Instrumentation.NeighborCounts) neighbors += visible;
            truncated += grid.Instrumentation.TruncatedCount;
        }
        return new(Convert.ToHexString(hash.GetHashAndReset()), gridBytes, naiveBytes, neighbors, truncated);
    }

    private static long StepAllocated(CanonicalWorld world)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        world.Step(world.Settings.FixedDeltaTime);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    internal static void AssertSameState(CanonicalWorld left, CanonicalWorld right, IncrementalHash? hash = null)
    {
        byte[] state = CaptureState(left);
        Require(state.AsSpan().SequenceEqual(CaptureState(right)), $"Observable state differs at tick {left.TickCount}");
        hash?.AppendData(state);
    }

    // Write every public boid/diagnostic value explicitly, preserving float bits (including signed zero).
    // Private RNG and priority buffers are exercised by future ticks, not claimed as serialized state.
    private static byte[] CaptureState(CanonicalWorld world)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(world.TickCount);
        writer.Write(world.Count);
        foreach (var boid in world.Boids)
        {
            writer.Write(boid.Position.X); writer.Write(boid.Position.Y);
            writer.Write(boid.Velocity.X); writer.Write(boid.Velocity.Y); writer.Write(boid.Group);
        }
        if (world.TickCount > 0)
        {
            for (int i = 0; i < world.Count; i++)
            {
                Require(world.TryGetMetrics(i, out var m), "Missing metrics");
                writer.Write(m.NeighborCount); writer.Write(m.NeighborWeightSum);
                writer.Write(m.SeparationMagnitude); writer.Write(m.AlignmentMagnitude); writer.Write(m.CohesionMagnitude);
                writer.Write(world.Instrumentation.SteeringMagnitudesSquared[i]);
                writer.Write(world.Instrumentation.QueryTruncated[i]);
            }
            var p = world.CapturePerceptionSnapshot();
            writer.Write(p.TickCount); writer.Write(p.AgentCount); writer.Write(p.AverageNeighborCount);
            writer.Write(p.AverageNeighborDistance); writer.Write(p.MinNeighborDistance); writer.Write(p.MaxNeighborDistance);
            writer.Write(p.AverageNeighborWeight); writer.Write(p.AverageSeparationMagnitude);
            writer.Write(p.AverageAlignmentMagnitude); writer.Write(p.AverageCohesionMagnitude);
            writer.Write(p.NeighborCountStats.Min); writer.Write(p.NeighborCountStats.Max); writer.Write(p.NeighborCountStats.Avg);
            writer.Write(p.SeparationPriorityTriggered);
            foreach (float value in p.NearestDistances) writer.Write(value);
            foreach (float value in p.NearestAngles) writer.Write(value);
            foreach (int value in p.WhiskerCounts) writer.Write(value);
        }
        writer.Flush();
        return stream.ToArray();
    }

    internal static (int CappedVisible, int UncappedVisible) CapBeforeFovWitness()
    {
        CanonicalWorld Create(int cap)
        {
            var settings = new CanonicalWorldSettings
            {
                InitialCapacity = 6, WorldWidth = 100, WorldHeight = 100, SenseRadius = 10,
                MaxNeighbors = cap, FieldOfView = 90, TargetSpeed = 2, MaxForce = 2,
                SeparationWeight = 0, AlignmentWeight = 1, CohesionWeight = 0, WhiskerWeight = 0,
                SeparationPriorityRadiusFactor = 0, SeparationPriorityExitFactor = 0
            };
            var world = new CanonicalWorld(settings, new GridSpatialIndex(10, 100, 100));
            Require(world.TryAddBoid(new Vec2(50, 50), new Vec2(1, 0)), "Spawn self failed");
            for (int i = 1; i <= 4; i++)
                Require(world.TryAddBoid(new Vec2(49, 48 + i), new Vec2(1, 0)), "Spawn rear neighbor failed");
            Require(world.TryAddBoid(new Vec2(53, 51), new Vec2(0, 1)), "Spawn front neighbor failed");
            return world;
        }
        var capped = Create(4);
        var uncapped = Create(8);
        int[] ids = new int[8];
        float[] weights = new float[8];
        var before = capped.QueryVisibleNeighbors(0, ids.AsSpan(0, 4), weights.AsSpan(0, 4));
        var after = uncapped.QueryVisibleNeighbors(0, ids, weights);
        Require(before.Count == 0 && before.IsTruncated && after.Count == 1 && !after.IsTruncated && ids[0] == 5,
            "Cap-before-FOV counterexample no longer holds");
        capped.Step(capped.Settings.FixedDeltaTime);
        uncapped.Step(uncapped.Settings.FixedDeltaTime);
        Require(capped.Boids[0].Velocity.Y != uncapped.Boids[0].Velocity.Y, "Witness must affect steering, not just diagnostics");
        return (before.Count, after.Count);
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
