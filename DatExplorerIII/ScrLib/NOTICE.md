# Provenance

ScrLib is a new C# .NET 10 reader for Arcanum's binary script format.
Opcode names, numeric identifiers and flags in `ScriptOpcodes.g.cs` were
adapted from `src/game/script.h` in the user's Arcanum Community Edition
checkout on 17 October 2026. That generated file records the header's SHA256.
The binary layout was checked against `script_file_load_hdr` and
`script_file_load_code` in `src/game/script.c`.

This is an adaptation, not an official library from the Arcanum CE authors.
The Sustainable Use License supplied by that checkout is retained in
`LICENSE.md`. Its terms apply to the derived material; no additional license
grant is claimed here. Upstream: https://github.com/alexbatalov/arcanum-ce

Two script fixtures come from TechErrorCode's Automaton Gunner release,
identified as version 1.0.4 by its manifest. Upstream credits are retained in
`tests/ScrLib.Tests/Fixtures/CREDITS.txt`. Fixtures are test data and are not
included in the library NuGet package. No executable or DAT archive is included.
