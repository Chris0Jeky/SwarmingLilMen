// PCG core adapted to C# from pcg-c-basic, Copyright 2014 Melissa O'Neill.
// Licensed under the Apache License, Version 2.0. See licenses/PCG-APACHE-2.0.txt.
// Changes: managed instance API, checked-context-safe arithmetic, argument validation,
// and specified 24-bit float / 53-bit double adapters. See THIRD_PARTY_NOTICES.md.
namespace SwarmSim.Core.Utils;

/// <summary>
/// PCG32 XSH-RR: specified integer arithmetic, independent of System.Random and runtime version.
/// Retain one instance per stochastic consumer. Instances are mutable and not thread-safe.
/// This simulation generator is not a cryptographic random-number source.
/// </summary>
public sealed class Pcg32Rng
{
    private Pcg32State _state;

    /// <summary>
    /// Initializes the published PCG32 sequence. All state bits and the low 63 sequence bits
    /// are significant; the high sequence bit is ignored by the PCG seeding protocol.
    /// </summary>
    public Pcg32Rng(ulong initialState, ulong sequence) => _state = new Pcg32State(initialState, sequence);

    internal Pcg32Rng(Pcg32State state) => _state = state;

    /// <summary>Returns a full-width unsigned 32-bit word.</summary>
    public uint NextUInt32() => _state.NextUInt32();

    /// <summary>Returns an unbiased integer in [0, exclusiveUpperBound).</summary>
    public uint NextUInt32(uint exclusiveUpperBound) => _state.NextUInt32(exclusiveUpperBound);

    /// <summary>Returns one of 2^24 equally spaced values in [0, 1), using one word.</summary>
    public float NextFloat() => _state.NextFloat();

    /// <summary>Returns one of 2^53 equally spaced values in [0, 1), using two words.</summary>
    public double NextDouble() => _state.NextDouble();
}
