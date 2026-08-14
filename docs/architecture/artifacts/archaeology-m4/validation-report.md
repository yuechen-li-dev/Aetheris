# Validation report

Validation was run on 2026-08-13 (America/Los_Angeles).

- `dotnet restore Aetheris.slnx`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed with 0 warnings and 0 errors.
- Aetheris CLI rebuilt the canonical Firmament box/cylinder example and inspected
  its STEP as an enclosed manifold: 12 vertices, 15 edges, 7 faces, six planes,
  and one cylinder.
- Core: 953 passed, including Boolean, Surgery, Recipes, STEP AP242,
  StandardLibrary consumers, and SurfaceMeshIR.
- Recipe direct/performance tests: 5 passed. Exact legacy/recipe/facade STEP
  parity and typed rejection passed. A non-benchmark 100-iteration smoke
  measurement was 10.942 ms legacy versus 5.882 ms recipe on this run.
- Firmament: 1,115 passed. Focused hole/CIR/StandardLibrary/SurfaceMeshIR
  materializers: 292 passed.
- Focused STEP/SurfaceMesh/recipe: 294 passed.
- CLI: 364 passed.
- Opt-in FrictionLab: 394 passed after excluding the documented unrelated
  `TriangleHexPrismProfileParityLabTests`; the complete run was 394 passed and
  5 failed, all five manifestations of its pre-existing non-finite interval.

The pre-existing `TriangleHexPrismProfileParityLabTests` non-finite
`ParameterInterval` failure remains classified as unrelated unless its behavior
changes. No giant BRep or STEP dump is stored here.

One pre-existing artifact inconsistency was surfaced rather than hidden: the
checked-in `testdata/firmament/exports/boolean_box_cylinder_hole.step` reports
an old disconnected wall-loop ordering, while rebuilding its source through the
current real path reports `enclosed-manifold`. M4 did not rewrite the historical
fixture because canonical Firmament artifact migration is outside this phase;
the new/legacy/facade comparisons use current generated output.
