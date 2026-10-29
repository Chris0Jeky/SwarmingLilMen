// PCG core adapted to C# from pcg-c-basic, Copyright 2014 Melissa O'Neill.
// Licensed under the Apache License, Version 2.0. See licenses/PCG-APACHE-2.0.txt.
// Changes: compact managed value state, explicit unchecked arithmetic, bound validation,
// and specified float/double adapters. See THIRD_PARTY_NOTICES.md.
using System.Numerics;

namespace SwarmSim.Core.Utils;

/// <summary>
/// Compact 16-byte state for world-owned per-agent arrays. Copying deliberately copies the draw
/// position; mutate retained array elements by reference. Public consumers use Pcg32Rng ownership.
/// </summary>
internal struct Pcg32State
{
    private ulong _state;
    private readonly ulong _increment;

    internal Pcg32State(ulong initialState, ulong sequence)
    {
        _state = 0;
        _increment = unchecked((sequence << 1) | 1ul);
        NextUInt32();
        _state = unchecked(_state + initialState);
        NextUInt32();
    }

    internal uint NextUInt32()
    {
        ulong previous = _state;
        _state = unchecked(previous * 6364136223846793005ul + _increment);
        uint word = unchecked((uint)(((previous >> 18) ^ previous) >> 27));
        return BitOperations.RotateRight(word, (int)(previous >> 59));
    }

    internal uint NextUInt32(uint exclusiveUpperBound)
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

    internal float NextFloat() => (NextUInt32() >> 8) * (1f / 16777216f);

    internal double NextDouble()
    {
        ulong high = NextUInt32() >> 5;
        ulong low = NextUInt32() >> 6;
        return ((high << 26) | low) * (1d / 9007199254740992d);
    }
}
