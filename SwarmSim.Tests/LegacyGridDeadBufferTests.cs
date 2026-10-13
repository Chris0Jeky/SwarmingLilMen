using System.Reflection;
using SwarmSim.Core;
using SwarmSim.Core.Systems;

namespace SwarmSim.Tests;

/// <summary>
/// Regression coverage for owner issue #132: recently-dead agents that have not yet been
/// compacted must not exhaust the bounded legacy neighbor buffer (256 slots), hiding live
/// neighbors from <see cref="SenseSystem"/>.
/// </summary>
public sealed class LegacyGridDeadBufferTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(600)]
    [InlineData(1000000)]
    public void DeadAgentsDoNotExhaustNeighborBuffer(int compactionInterval)
    {
        // 257 dead exceeds the 256 bounded Query3x3 slots; with dead in the grid the two
        // live neighbors are pushed out of the buffer and SenseSystem records zero.
        const int deadCount = 257;

        var config = new SimConfig
        {
            InitialCapacity = 512,
            WorldWidth = 1000f,
            WorldHeight = 1000f,
            SenseRadius = 50f,
            SeparationRadius = 20f,
            FieldOfView = 360f, // Omnidirectional: only distance guards filter neighbors
            CompactionInterval = compactionInterval,
        };
        var world = new World(config, seed: 42u);

        // Two live agents 2 units apart: well inside SenseRadius, outside the 0.01 skip.
        int live0 = world.AddAgent(100f, 100f, group: 0);
        int live1 = world.AddAgent(102f, 100f, group: 0);

        // 257 same-cell agents, then marked dead (compaction has not run yet).
        for (int i = 0; i < deadCount; i++)
            world.AddAgent(100f, 100f, group: 0);
        for (int i = live1 + 1; i < world.Count; i++)
            world.MarkDead(i);

        // Single tick: TickCount (1) < CompactionInterval, so dead slots are still present.
        world.Tick();

        var sense = (SenseSystem)typeof(World)
            .GetField("_senseSystem", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(world)!;

        Assert.Equal(1, sense.NeighborCounts[live0]);
        Assert.Equal(1, sense.NeighborCounts[live1]);
    }
}
