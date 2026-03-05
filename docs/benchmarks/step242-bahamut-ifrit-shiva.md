# STEP242 Benchmark Fixtures: BAHAMUT / IFRIT / SHIVA

This benchmark set adds three deterministic AP242 STEP fixtures under `testdata/step242/benchmarks/` for import → validate → tessellate → pick → export profiling and determinism checks.

## Fixtures

- **BAHAMUT** (`BAHAMUT.step`)
  - Focus: topology-orientation semantics and parser robustness signals in one file.
  - Includes: planar outer loop with two inner loops, mixed orientation booleans (`EDGE_CURVE`, `ORIENTED_EDGE`, `FACE_BOUND`, `ADVANCED_FACE`), typed numeric values, mixed-case entity spelling, block comment, complex representation context assignment.
- **IFRIT** (`IFRIT.step`)
  - Focus: precision scale spread and tolerance-sensitive numeric setup.
  - Includes: very small inner hole against large outer scale, near-axis direction vector for an analytic cylinder/cone context, exponent numeric formats.
- **SHIVA** (`SHIVA.step`)
  - Focus: parser/context expression robustness with intentionally high-entropy STEP text formatting.
  - Includes: complex context assignment, typed measure wrappers, unusual whitespace/comments, mixed-case logical tokens.

## Running benchmark smoke tests

```bash
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "FullyQualifiedName~Step242Benchmark"
```

The perf smoke tests print per-stage elapsed milliseconds and per-thread allocations for:

1. Import
2. Validate
3. Tessellate
4. Export

Bounds are intentionally loose to reduce CI flakiness; output is intended for trend monitoring.

## Cross-language comparison guidance (C# vs C++)

Use the same three `.step` inputs and the same stage order in both pipelines:

1. Parse/import STEP to topology/geometry model
2. Validate topology+binding consistency
3. Tessellate display mesh
4. Perform pick smoke query
5. Export canonical STEP and hash

For deterministic comparison:

- normalize line endings to LF,
- hash canonical exported text only,
- compare hash after one canonicalization re-import/export cycle.
