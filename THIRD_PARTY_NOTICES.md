# Third-party source notices

## PCG32

`SwarmSim.Core/Utils/Pcg32Rng.cs` adapts the generator, seeding protocol and rejection sampler
from [PCG's minimal C implementation](https://github.com/imneme/pcg-c-basic/blob/master/pcg_basic.c).

Copyright 2014 Melissa O'Neill <oneill@pcg-random.org>.

The adapted file is licensed under the [Apache License, Version 2.0](licenses/PCG-APACHE-2.0.txt).
Changes for this project: C# managed instance API, explicit unchecked arithmetic, bound validation,
and specified 24-bit float / 53-bit double adapters. The named-stream derivation is project code.
The repository's root license remains unchanged.

See [PCG documentation](https://www.pcg-random.org/using-pcg-c-basic.html) for the generator's
published reference sequence and seeding contract.
