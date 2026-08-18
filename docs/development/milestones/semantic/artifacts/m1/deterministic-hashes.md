# Determinism and performance evidence

Canonical native and imported descriptor hashes and the Forge artifact hash are
listed in `stable-ids.md`. STEP/BRep and Forge CIR association determinism remain
covered by the existing Forge evidence suite.

Developer-machine whole-process envelopes (three warm `dotnet run/test` calls,
including process startup) were 738-780 ms for native inspection, 748-756 ms for
Recognize inspection, and 1015-1039 ms for the Forge path/Selection/FEA test.
Forge's existing in-process evidence reports warm capability resolution 0.0025
ms, template invocation 0.5599 ms, extension lowering 0.0405 ms, and compiler
validation 1.3867 ms. These are diagnostic measurements, not benchmarks or gates.
