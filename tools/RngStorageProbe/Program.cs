using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SwarmSim.Core.Canonical;
using SwarmSim.Core.Diagnostics;

static CanonicalWorld Create(int count)
{
    var settings = new CanonicalWorldSettings
    {
        InitialCapacity = count, Seed = 42, WanderStrength = 0.4f,
        WorldWidth = 10_000, WorldHeight = 10_000, SenseRadius = 10,
        MaxNeighbors = 32, TargetSpeed = 1, MaxForce = 0.2f,
        FixedDeltaTime = 1f / 60f
    };
    var world = new CanonicalWorld(settings, new GridSpatialIndex(25, 10_000, 10_000));
    for (int i = 0; i < count; i++)
        if (!world.TryAddBoid(new Vec2(10 + i % 316 * 31.5f, 10 + i / 316 * 31.5f), new Vec2(1, 0)))
            throw new InvalidOperationException("Population incomplete.");
    return world;
}

var warm = Create(16);
for (int i = 0; i < 100; i++) warm.Step(1f / 60f);
warm = null;
long beforeRetained = GC.GetTotalMemory(true);
long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
long start = Stopwatch.GetTimestamp();
var world = Create(100_000);
double constructionMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
long constructionAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocated;
long retained = GC.GetTotalMemory(true) - beforeRetained;
for (int i = 0; i < 100; i++) world.Step(1f / 60f);
long beforeStep = GC.GetAllocatedBytesForCurrentThread();
start = Stopwatch.GetTimestamp();
for (int i = 0; i < 1000; i++) world.Step(1f / 60f);
double stepMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
long stepAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeStep;
Console.WriteLine(JsonSerializer.Serialize(new
{
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), agents = world.Count,
    constructionMilliseconds, constructionAllocatedBytes = constructionAllocated,
    retainedManagedBytes = retained, warmSteps = 100, measuredSteps = 1000,
    stepAllocatedBytes = stepAllocated, stepMilliseconds,
    finalKinematicHash = SimulationKinematicHash.Compute(world)
}));
GC.KeepAlive(world);
