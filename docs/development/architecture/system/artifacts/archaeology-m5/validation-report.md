# Validation report

Validation results:

- `dotnet restore Aetheris.slnx`: clean.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: clean, zero warnings/errors.
- Core full suite: 959 passed (includes Recipe/Surgery/Boolean/STEP parity and boundary tests).
- Firmament default: 1,115 passed. Opt-in V1 compatibility: 1,734 passed.
- CLI: 364 passed; Forge Host: 10; Server: 54; Continuum/SurfaceMeshIR: 148.
- Remaining solution projects: Collaboration 5, FEA 12, Geometry 58, Modules 37, Reconstruction 24, Semantics 9, sample 8; all passed.
- Focused Boolean/CIR FrictionLab: 31 passed.
- Full opt-in FrictionLab: 394 passed, 5 known failures.
- CLI V2 dogfood parsed `Hole<Shaft> Mount`, built STEP hash `EE665BD001DF97D210E4FBB5D535FF2E26FC58601AA369B204089B7378799450`, reimported one enclosed manifold with 7 faces/15 edges/12 vertices, six planes, and one cylinder. The hash is unchanged from the pre-migration artifact.

The five failures are exactly the known `TriangleHexPrismProfileParityLabTests` non-finite `ParameterInterval` baseline at `ProfileExtrusionBRepPlan.cs:128`; no M5 path is present in their stacks.
