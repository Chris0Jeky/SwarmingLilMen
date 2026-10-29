// PCG core adapted to C# from pcg-c-basic, Copyright 2014 Melissa O'Neill.
// Licensed under the Apache License, Version 2.0. See licenses/PCG-APACHE-2.0.txt.
// Changes: managed instance API, checked-context-safe arithmetic, argument validation,
// and specified 24-bit float / 53-bit double adapters. See THIRD_PARTY_NOTICES.md.
using System.Numerics;

namespace SwarmSim.Core.Utils;

/// <summary>
/// PCG32 XSH-RR: specified integer arithmetic, independent of System.Random and runtime version.
/// Retain one instance per stochastic consumer. Instances are mutable and not thread-safe.
/// This simulation generator is not a cryptographic random-number source.
/// </summary>
public sealed class Pcg32Rng
{
    private ulong _state;
    private readonly ulong _increment;

    /// <summary>
    /// Initializes the published PCG32 sequence. All state bits and the low 63 sequence bits
    /// are significant; the high sequence bit is ignored by the PCG seeding protocol.
    /// </summary>
    public Pcg32Rng(ulong initialState, ulong sequence)
    {
        _increment = unchecked((sequence << 1) | 1ul);
        NextUInt32();
        _state = unchecked(_state + initialState);
        NextUInt32();
    }

    /// <summary>Returns a full-width unsigned 32-bit word.</summary>
    public uint NextUInt32()
    {
        ulong previous = _state;
        _state = unchecked(previous * 6364136223846793005ul + _increment);
        uint word = unchecked((uint)(((previous >> 18) ^ previous) >> 27));
        return BitOperations.RotateRight(word, (int)(previous >> 59));
    }

    /// <summary>Returns an unbiased integer in [0, exclusiveUpperBound).</summary>
    public uint NextUInt32(uint exclusiveUpperBound)
    {
        if (exclusiveUpperBound == 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound), "The bound must be positive.");

        uint threshold = unchecked(0u - exclusiveUpperBound) % exclusiveUpperBound;
        while (true)
        {
            uint value = NextUInt32();
            if (value >= threshold) return value % exclusiveUpperBound;
        }
    }

    /// <summary>Returns one of 2^24 equally spaced values in [0, 1), using one word.</summary>
    public float NextFloat() => (NextUInt32() >> 8) * (1f / 16777216f);

    /// <summary>Returns one of 2^53 equally spaced values in [0, 1), using two words.</summary>
    public double NextDouble()
    {
        ulong high = NextUInt32() >> 5;
        ulong low = NextUInt32() >> 6;
        return ((high << 26) | low) * (1d / 9007199254740992d);
    }
}
