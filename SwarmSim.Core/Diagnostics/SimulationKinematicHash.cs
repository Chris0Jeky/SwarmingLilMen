using System.Buffers.Binary;
using System.Security.Cryptography;
using SwarmSim.Core.Canonical;

namespace SwarmSim.Core.Diagnostics;

/// <summary>
/// Computes a version-1 diagnostic hash over ordered agent kinematics.
/// </summary>
/// <remarks>
/// Version 1 hashes the logical agent count followed by the exact IEEE-754 bits of ordered
/// X, Y, Vx, and Vy components. It is not a whole-world state fingerprint: configuration,
/// clocks, RNG and wander state, groups, lifecycle and resource state, genomes, capacity, and
/// scratch state are intentionally excluded.
/// </remarks>
public static class SimulationKinematicHash
{
    /// <summary>Computes the version-1 ordered kinematic hash for a legacy world.</summary>
    public static string Compute(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendInt32(hash, world.Count);

        for (int i = 0; i < world.Count; i++)
        {
            AppendSingle(hash, world.X[i]);
            AppendSingle(hash, world.Y[i]);
            AppendSingle(hash, world.Vx[i]);
            AppendSingle(hash, world.Vy[i]);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Computes the version-1 ordered kinematic hash for a canonical world.</summary>
    public static string Compute(CanonicalWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        ReadOnlySpan<Boid> boids = world.Boids;
        AppendInt32(hash, boids.Length);

        foreach (Boid boid in boids)
        {
            AppendSingle(hash, boid.Position.X);
            AppendSingle(hash, boid.Position.Y);
            AppendSingle(hash, boid.Velocity.X);
            AppendSingle(hash, boid.Velocity.Y);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendSingle(IncrementalHash hash, float value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, BitConverter.SingleToUInt32Bits(value));
        hash.AppendData(bytes);
    }
}
