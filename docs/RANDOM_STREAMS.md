# Named deterministic random streams

Every new stochastic consumer owns a named stream. `RandomStreams` derives independent mutable
`Pcg32Rng` instances from a root seed, exact purpose name and optional stable agent identity.
Construction order and another stream's draw count do not affect the resulting sequence.
The implementation uses specified integer arithmetic instead of delegating to `System.Random`.

```csharp
var streams = new RandomStreams(seed: 42);
Pcg32Rng environmentNoise = streams.CreateStream("experiment.environment");
Pcg32Rng agentNoise = streams.CreateStream("policy.exploration", agentId: 17);

// Retain each instance for its consumer. Creation restarts the sequence; it is not a lookup.
float unit = agentNoise.NextFloat();             // [0, 1), one word
double preciseUnit = agentNoise.NextDouble();    // [0, 1), two words
uint choice = agentNoise.NextUInt32(5);          // [0, 5), rejection avoids modulo bias
```

`CanonicalWorld.RandomStreams` exposes the factory with that world's seed. Canonical wander
retains one `canonical.wander` stream per insertion index. The initial draw chooses its angle;
later draws change that angle only when wander is enabled and the force budget allows it.
World-level streams and agent zero have different identities. The factory accepts all 64-bit
seed/ID values, while the existing public configuration seed range remains `0..int.MaxValue`.

## Identity and ownership

Use a stable purpose name rather than enumeration position, a type's hash code, time, thread ID,
or construction order. Names are case-sensitive strict UTF-8 with no Unicode normalization.
Empty/whitespace-only names and malformed surrogate sequences fail at construction. Names that
look alike but encode differently remain distinct. All consumers that intentionally share common
random numbers must agree on the exact seed/name/agent tuple and the sequence of draw methods.

The factory holds no mutable generator state and is not a registry/cache. Two creations with the
same identity return independent objects starting at the same point. Retain one instance per
consumer; accidentally requesting the same name twice does not allocate a new logical identity.
PCG sequence selectors have 63 bits and are derived from a hash: identity collisions are not
mathematically impossible. This contract establishes reproducible separation, not a proof that
all conceivable names map injectively into finite generator state.

Canonical insertion indices are stable today because canonical agents cannot be removed or
reordered. Lifecycle work must introduce persistent identities before those operations change
that assumption. Changing capacity alone or adding an unrelated distant agent does not reseed
existing agents. Generators are mutable and not thread-safe; no parallel scheduling is introduced.

Isolation is **between consumers**, not between different draw positions within one consumer.
Changing settings can change whether wander spends force, hence when that consumer draws.
Paired experiments get common random numbers when their consumer draw schedules match; this
API is not tick-addressed noise or a guarantee of matching draws after divergent branches.

## Version 1 byte contract

`RandomStreams.Contract` is `sha256-pcg32-v1`. Stream construction hashes these concatenated bytes:

| Part | Encoding |
| --- | --- |
| Domain | UTF-8/ASCII `SwarmSim.RandomStreams.v1` followed by one zero byte |
| Root seed | Unsigned 64-bit, little-endian |
| Agent-present tag | One byte: `0` for a world stream, `1` for an agent stream |
| Name byte length | Unsigned 32-bit, little-endian |
| Name | Exact strict UTF-8 bytes |
| Agent ID, if present | Unsigned 64-bit, little-endian |

The first eight SHA-256 digest bytes, read little-endian, initialize PCG state; the next eight
select its sequence. PCG32 uses the low 63 sequence bits, sets the increment to `(sequence << 1)
| 1`, advances from zero once, adds the initial state modulo 2^64, and advances again.
The generator is PCG32 XSH-RR with multiplier `6364136223846793005` and modulo-2^64 state
arithmetic. It rotates the 32-bit xorshifted **previous** state by that state's high five bits.

`NextFloat` takes the high 24 bits of one word and scales by 2^-24. `NextDouble` concatenates
27 high bits from one word with 26 from the next and scales by 2^-53. Bounded draws discard
words below `2^32 mod bound`, then take the remainder. Zero bound fails before any draw.
These algorithms, adapters and derivation bytes are part of the versioned contract.

The [published PCG reference](https://www.pcg-random.org/using-pcg-c-basic.html) starts with
`A15C02B7 7B47F409 BA1D3330 83D2F293 BFA4784B CBED606E` for initial state 42 / sequence 54.
Ordinary tests pin these words plus additional independently computed words, adapter bits,
rejection cases, named identities, full-width inputs and Unicode names. The adapted PCG core's
attribution and license are in [third-party notices](../THIRD_PARTY_NOTICES.md).

## Migration and evidence

Issue #26's intentional reference update affects canonical **wander-enabled** trajectories.
The legacy `Rng` wrapper, its consumers and committed legacy/System.Random fixtures are unchanged.
Canonical inputs and math are otherwise unchanged. Fixtures retain exact equality rather than
adding numeric tolerance. See [determinism](DETERMINISM.md) for old/new and cross-platform records.

Generator words and their float/double adapters no longer depend on `System.Random` internals.
World motion still uses platform-sensitive floating-point math; initialization in the CLI and
legacy engine still uses the old RNG. This is not a cross-platform trajectory guarantee.
Replay of earlier wander-enabled canonical manifests requires the earlier binary/revision.
Attempting them with this implementation fails hash verification and publishes no completed run.

The trajectory isolation regression registers a fourth consumer, confirms it draws once per agent
per tick, and compares every kinematic hash for 256 ticks. Existing per-agent/capacity isolation and
cross-process repeat/replay checks remain active. Retained generators allocate zero managed bytes
over 100,000 warm batches of bounded/float/double draws; stream creation intentionally allocates.

```sh
dotnet test SwarmSim.Tests/SwarmSim.Tests.csproj -c Release \
  --filter "FullyQualifiedName~RngTests|FullyQualifiedName~GoldenTraceTests|FullyQualifiedName~DeterminismTests" \
  -- RunConfiguration.TreatNoTestsAsError=true
```
