using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SwarmSim.Core.Utils;

/// <summary>
/// Pure named-stream derivation. Creation order and draws from other consumers cannot affect
/// a stream. Retain returned generators: creating the same identity again restarts its sequence.
/// </summary>
public sealed class RandomStreams
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static ReadOnlySpan<byte> Domain => "SwarmSim.RandomStreams.v1\0"u8;

    /// <summary>The versioned derivation and generator contract, also described in docs/RANDOM_STREAMS.md.</summary>
    public const string Contract = "sha256-pcg32-v1";

    public ulong Seed { get; }

    public RandomStreams(ulong seed) => Seed = seed;

    /// <summary>Creates a world-level stream. Names are exact UTF-8, without normalization.</summary>
    public Pcg32Rng CreateStream(string name) => Create(name, null);

    /// <summary>
    /// Creates a consumer stream for a stable agent identity. The caller owns identity stability
    /// across reordering or lifecycle changes; a world stream differs from agent zero.
    /// </summary>
    public Pcg32Rng CreateStream(string name, ulong agentId) => Create(name, agentId);

    private Pcg32Rng Create(string name, ulong? agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        int nameBytes = StrictUtf8.GetByteCount(name);
        int headerBytes = Domain.Length + sizeof(ulong) + sizeof(byte) + sizeof(uint);
        var encoded = new byte[checked(headerBytes + nameBytes + (agentId.HasValue ? sizeof(ulong) : 0))];
        Domain.CopyTo(encoded);
        int offset = Domain.Length;
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(offset), Seed);
        offset += sizeof(ulong);
        encoded[offset++] = agentId.HasValue ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(offset), (uint)nameBytes);
        offset += sizeof(uint);
        StrictUtf8.GetBytes(name.AsSpan(), encoded.AsSpan(offset, nameBytes));
        offset += nameBytes;
        if (agentId.HasValue)
            BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(offset), agentId.Value);

        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(encoded, digest);
        return new Pcg32Rng(BinaryPrimitives.ReadUInt64LittleEndian(digest),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[sizeof(ulong)..]));
    }
}
