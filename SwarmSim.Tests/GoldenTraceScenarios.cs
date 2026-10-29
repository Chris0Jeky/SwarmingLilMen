using SwarmSim.Core;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Tests;

// Versioned experiment construction for the committed golden traces. Initial coordinates and
// directions are exact integers; random spawning cannot hide a dynamics/platform difference.
internal static class GoldenTraceScenarios
{
    internal const uint Seed = 424242;
    internal const float LegacyMaxForce = 3f;
    internal const float CanonicalMaxForce = 0.75f;
    internal static readonly int[] CheckpointTicks = { 0, 1, 16, 64, 256 };

    private static readonly (float X, float Y, float Vx, float Vy)[] InitialAgents =
    {
        (1f, 1f, 4f, 0f),
        (127f, 95f, -4f, 0f),
        (18f, 12f, 0f, 4f),
        (24f, 18f, 0f, -4f),
        (64f, 48f, 3f, 0f),
        (71f, 52f, 0f, -3f),
        (112f, 48f, -3f, 0f),
        (119f, 54f, 0f, 3f)
    };

    internal static World CreateLegacy(float maxForce = LegacyMaxForce)
    {
        var config = new SimConfig
        {
            Seed = Seed,
            InitialCapacity = InitialAgents.Length,
            MaxCapacity = InitialAgents.Length,
            WorldWidth = 128f,
            WorldHeight = 96f,
            BoundaryMode = BoundaryMode.Wrap,
            GridCellSize = 8f,
            SenseRadius = 24f,
            SeparationRadius = 6f,
            FieldOfView = 270f,
            SeparationWeight = 1.25f,
            AlignmentWeight = 0.75f,
            CohesionWeight = 0.6f,
            WanderStrength = 0.15f,
            MaxNeighbors = 6,
            CollisionAvoidanceRadius = 3f,
            CollisionAvoidanceBoost = 2f,
            SeparationCrowdingThreshold = 3,
            SeparationCrowdingBoost = 1.5f,
            MaxForce = maxForce,
            MaxSpeed = 12f,
            SpeedModel = SpeedModel.Damped,
            Friction = 0.98f,
            FixedDeltaTime = 1f / 32f,
            CompactionInterval = 1024
        };
        var world = new World(config);
        foreach (var agent in InitialAgents)
        {
            int index = world.AddAgent(agent.X, agent.Y);
            world.Vx[index] = agent.Vx;
            world.Vy[index] = agent.Vy;
        }
        return world;
    }

    internal static CanonicalWorld CreateCanonical(float maxForce = CanonicalMaxForce)
    {
        var settings = new CanonicalWorldSettings
        {
            Seed = Seed,
            InitialCapacity = InitialAgents.Length,
            WorldWidth = 128f,
            WorldHeight = 96f,
            TargetSpeed = 6f,
            MaxForce = maxForce,
            SenseRadius = 24f,
            SeparationRadius = 6f,
            FieldOfView = 270f,
            MaxNeighbors = 6,
            SeparationWeight = 1.25f,
            AlignmentWeight = 0.75f,
            CohesionWeight = 0.6f,
            WanderStrength = 0.2f,
            WanderRate = 0.75f,
            MaxTurnRateDegPerSecond = 120f,
            WhiskerTimeHorizon = 0.75f,
            WhiskerWeight = 1.25f,
            SeparationPriorityRadiusFactor = 0.2f,
            SeparationPriorityExitFactor = 0.35f,
            SeparationPriorityBoost = 1.75f,
            SeparationPriorityHoldTime = 0.125f,
            SeparationPriorityRampInTime = 0.0625f,
            SeparationPriorityRampOutTime = 0.1875f,
            SeparationSpeedDroop = 0.03125f,
            FixedDeltaTime = 1f / 32f
        };
        var world = new CanonicalWorld(settings, new GridSpatialIndex(8f, settings.WorldWidth, settings.WorldHeight));
        foreach (var agent in InitialAgents)
        {
            if (!world.TryAddBoid(new Vec2(agent.X, agent.Y), new Vec2(agent.Vx, agent.Vy)))
                throw new InvalidOperationException("Golden trace initialization failed.");
        }
        return world;
    }

    internal static uint[] KinematicBits(World world)
    {
        var bits = new uint[world.Count * 4];
        for (int i = 0; i < world.Count; i++)
        {
            bits[i * 4] = BitConverter.SingleToUInt32Bits(world.X[i]);
            bits[i * 4 + 1] = BitConverter.SingleToUInt32Bits(world.Y[i]);
            bits[i * 4 + 2] = BitConverter.SingleToUInt32Bits(world.Vx[i]);
            bits[i * 4 + 3] = BitConverter.SingleToUInt32Bits(world.Vy[i]);
        }
        return bits;
    }

    internal static uint[] KinematicBits(CanonicalWorld world)
    {
        var bits = new uint[world.Count * 4];
        for (int i = 0; i < world.Count; i++)
        {
            var boid = world.Boids[i];
            bits[i * 4] = BitConverter.SingleToUInt32Bits(boid.Position.X);
            bits[i * 4 + 1] = BitConverter.SingleToUInt32Bits(boid.Position.Y);
            bits[i * 4 + 2] = BitConverter.SingleToUInt32Bits(boid.Velocity.X);
            bits[i * 4 + 3] = BitConverter.SingleToUInt32Bits(boid.Velocity.Y);
        }
        return bits;
    }
}
