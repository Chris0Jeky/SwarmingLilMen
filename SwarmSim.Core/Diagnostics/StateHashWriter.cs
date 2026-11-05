using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using SwarmSim.Core.Utils;

namespace SwarmSim.Core.Diagnostics;

/// <summary>
/// Streams an explicitly versioned model-state profile into SHA-256, using little-endian integers,
/// exact IEEE-754 bits and one-byte booleans. Memory use does not grow with the state being hashed.
/// </summary>
/// <remarks>
/// Each profile owns its field order and must write lengths before variable-sized sequences.
/// A changed field order, meaning, or coverage needs a new profile version. Calling this writer
/// neither advances random streams nor captures restorable state. Writers are not thread-safe.
/// </remarks>
public sealed class StateHashWriter : IDisposable
{
    private readonly IncrementalHash _hash;
    private readonly string _profile;
    private bool _finished;

    public StateHashWriter(string profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Length is < 1 or > 128 ||
            profile.Any(character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-'))
            throw new ArgumentException("A model-state profile must contain 1 through 128 lowercase ASCII letters, digits or hyphens.", nameof(profile));

        _profile = profile;
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        _hash.AppendData("SwarmSim.ModelState\0"u8);
        WriteInt32(profile.Length);
        Span<byte> bytes = stackalloc byte[128];
        int length = Encoding.ASCII.GetBytes(profile.AsSpan(), bytes);
        _hash.AppendData(bytes[..length]);
    }

    public void WriteInt32(int value)
    {
        EnsureOpen();
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        _hash.AppendData(bytes);
    }

    public void WriteUInt32(uint value)
    {
        EnsureOpen();
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        _hash.AppendData(bytes);
    }

    public void WriteInt64(long value)
    {
        EnsureOpen();
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        _hash.AppendData(bytes);
    }

    public void WriteUInt64(ulong value)
    {
        EnsureOpen();
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        _hash.AppendData(bytes);
    }

    public void WriteSingle(float value) => WriteUInt32(BitConverter.SingleToUInt32Bits(value));

    public void WriteDouble(double value) => WriteUInt64(BitConverter.DoubleToUInt64Bits(value));

    public void WriteBoolean(bool value)
    {
        EnsureOpen();
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value ? (byte)1 : (byte)0;
        _hash.AppendData(bytes);
    }

    /// <summary>Writes the retained PCG state and odd increment without consuming any draws.</summary>
    public void WriteRandomState(Pcg32Rng random)
    {
        ArgumentNullException.ThrowIfNull(random);
        Pcg32State state = random.Snapshot;
        WriteUInt64(state.State);
        WriteUInt64(state.Increment);
    }

    /// <summary>Finishes this writer once; subsequent writes or finishes are rejected.</summary>
    public ModelStateHash Finish()
    {
        EnsureOpen();
        _finished = true;
        return new ModelStateHash(_profile, Convert.ToHexString(_hash.GetHashAndReset()));
    }

    public void Dispose()
    {
        _finished = true;
        _hash.Dispose();
    }

    private void EnsureOpen()
    {
        if (_finished)
            throw new InvalidOperationException("A model-state hash writer cannot be reused after finishing or disposal.");
    }
}
