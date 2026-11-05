# Versioned model-state fingerprints

The kinematic v1 hash remains unchanged: ordered count, position and velocity bits. It deliberately
cannot see an ant's load, a pheromone cell, a clock or a random stream's draw position. Native
engines can additionally implement a model-owned state profile. Schema 3 manifests record its
`modelStateProfile` and `finalModelStateHash` alongside the original kinematic and CSV hashes.

A profile identifies a bounded set of persistent native model state. It is an observation for
replay verification, not serialized state and not a checkpoint/restore API. The profile does not
claim to identify arbitrary plugin, callback, custom spatial-index or external process state.
Legacy and canonical do not currently declare a model-state profile. They continue to write
schema 2 manifests without either new property; neither a blank hash nor a kinematic hash is
substituted for an unsupported model-state capability.

## Binary contract

[`StateHashWriter`](../SwarmSim.Core/Diagnostics/StateHashWriter.cs) feeds SHA-256 incrementally.
Its header is the ASCII bytes of `SwarmSim.ModelState` followed by a zero byte, a signed
little-endian 32-bit profile byte length, then the ASCII profile bytes. Profile names contain
1–128 lowercase ASCII letters, digits or hyphens. The version is part of that exact name.

The profile specifies the subsequent field order and types. Integers use their declared 32- or
64-bit little-endian representation. Singles and doubles use their exact IEEE-754 bits, including
signed zero; booleans are one byte, zero or one. Sequences include their logical count before
their elements. The writer does not add field names, padding, separators, culture-dependent
formatting or platform-native endianness. It returns uppercase SHA-256 hex. The independent
fixed byte-vector test in [`ModelStateHashTests`](../SwarmSim.Tests/ModelStateHashTests.cs) pins
the header and primitive encoding.

The writer uses fixed-size temporary storage and incremental hashing. It does not copy agents
or fields into a growing serialization buffer: additional memory is constant with respect to
population and grid size, while time is linear in the covered state. Hashing is a diagnostic
operation at the end of a CLI run, outside the simulation step and runtime metric interval.

Changing a profile's coverage, field order, representation or meaning requires a new profile
version. Changing model dynamics still requires the normal trajectory/reference review; adding
this fingerprint changes neither the v1 bytes nor any existing platform golden fixture.

## Native profiles

### `aco-state-v1`

[`AntColonyWorld.StateHash`](../SwarmSim.Core/Aco/AntColonyWorld.cs) writes these sections in order:

1. Complete settings in declaration order: `Deposit`, `Evaporation`, `FieldCadence`, `Speed`,
   `TurnNoise`, `SensorAngle`, `SensorDistance`, `FieldColumns`, `FieldRows`, `NestX`, `NestY`,
   `NestRadius`, `FoodX`, `FoodY`, `FoodRadius`, `FoodAmount`, `WorldWidth`, `WorldHeight`,
   `FixedDeltaTime`, `Seed`. Settings retain their declared scalar types.
2. Signed 64-bit `TickCount`, signed 32-bit `FoodRemaining`, signed 64-bit `FoodReturned`,
   signed 32-bit `CarryingCount`, then the agent-phase-completed boolean.
3. Signed 32-bit ant count, then each ant's single-precision X, Y and heading and its carrying
   boolean in stable ant order. Heading is stored directly, without reconstructing velocity.
4. Signed 32-bit field-cell count, then every single-precision cell in row-major order. The
   settings already contain the grid dimensions and world extents.
5. Signed 32-bit pending-deposit count, then each pending deposit's single-precision X and Y in
   application order.
6. Signed 32-bit retained forage-stream count, then each stream's unsigned 64-bit PCG state and
   unsigned 64-bit odd increment in stable ant order.

The profile is valid between phase invocations, including after `AgentPhase` and before
`FieldPhase`. Pending deposits and the phase flag are persistent in that interval and therefore
covered. Unused deposit-buffer capacity is excluded. The step distance is derived from settings;
the initialization-only spawn generator is discarded. Neither contributes independent retained
state. Reading a fingerprint does not consume a random draw or apply a deposit.

### `vicsek-state-v1`

[`VicsekWorld.StateHash`](../SwarmSim.Core/Vicsek/VicsekWorld.cs) writes:

1. Single-precision `Noise`, `Speed`, `InteractionRadius`, `WorldWidth`, `WorldHeight` and
   `FixedDeltaTime`, then unsigned 32-bit `Seed`.
2. Signed 64-bit `TickCount` and signed 32-bit agent count.
3. Each current agent's single-precision X, Y, Vx and Vy in stable agent order.
4. The retained noise generator's unsigned 64-bit PCG state and unsigned 64-bit odd increment.

The public observation boundary is between completed ticks. The next-state buffer and neighbor
scratch are overwritten before use, and the native spatial index is rebuilt from current agents;
those derived buffers are excluded. State inside an injected custom index is outside this native
profile. The initialization-only spawn generator is discarded. Neither profile is thread-safe
against concurrent world mutation.

## Manifest compatibility and verification

| Manifest | Reader and replay behavior |
| --- | --- |
| Schema 1 | Original boids configuration and default metric contract; original v1 and CSV checks |
| Schema 2 | Existing engine/metric contract; original v1 and CSV checks |
| Schema 3 | Schema 2 metric contract plus an exact engine-supported profile and a valid model-state SHA-256; all three hashes must match |

Schema 1 and 2 reject either new property even when explicitly `null`. Schema 3 rejects an
unsupported engine, missing/unknown/wrong-version profile, or missing/malformed state hash
before constructing a world or creating output. The engine registry declares each supported
profile, and the runner also checks that the adapter's optional state capability agrees with
that declaration. Profiles are matched exactly; hash hex is compared case-insensitively.

Old schema 2 ACO/Vicsek manifests still replay against their original two checks. A successful
replay writes a new schema 3 result containing the newly observed model-state fingerprint; it
does not retroactively claim that the old record verified hidden state. Canonical/legacy results
remain schema 2. A failed model-state comparison removes staging output just like a failed
kinematic or CSV comparison and publishes no completed run.

The fingerprint adds visibility, not cross-platform float portability. Exact replay remains
scoped to the recorded implementation/platform/runtime band. Manifests with nondeterministic
runtime metrics remain ineligible for exact CSV replay, including schema 3 manifests.

The regression witnesses cover ACO field-only changes, loads/resources, pending deposit
locations and forage draw positions, Vicsek noise draw positions and clocks, configuration
changes before motion differs, observational purity, old-schema replay and malformed/tampered
schema 3 records. Existing deterministic trajectory and RNG goldens remain unchanged.
