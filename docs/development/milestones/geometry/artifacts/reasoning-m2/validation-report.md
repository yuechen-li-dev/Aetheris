# M2 validation report

Generated machine-readable evidence:

- `canonical-second-jets-and-curvature.json`: line/circle/parabola jets, curve curvature, elliptic-paraboloid principal curvature, and local-minimum observations.
- `parameterization-invariance.json`: scaled/reversed patch results and orientation convention.
- `panel-continuity.json`: G0-pass/G1-fail, G1-pass/G2-fail, G2-pass, and unavailable-second-jet `Unknown` cases.
- `performance.json`: second-jet, curvature, and seam timings.
- `deterministic-hashes.json`: SHA-256 manifest for repeatability.

Validation completed on net10.0: restore succeeded; the full solution build succeeded with 0 warnings and 0 errors; 2,722 tests passed across 12 discovered test assemblies with 0 failed and 0 skipped. `Aetheris.FrictionLab.Tests` currently contains no discoverable tests. The focused counts are 25 Geometry tests and 37 Modules/Panel tests. Sampling remains `Sampled`; analytic curvature remains `ToleranceBounded`. SignedSide tangent-candidate enrichment was not implemented because it was optional and would couple a whole-domain predicate result to local observations without improving its classification.

The final Debug smoke run measured approximately 469 ns curve second jet, 933 ns patch second jet, 538 ns curve curvature, 1,633 ns principal curvature, 142 microseconds per 17-sample G1 seam, and 230 microseconds per 17-sample G2 seam. These are bounded observations, not optimization claims. Two complete evidence generations produced identical deterministic-manifest SHA-256 `56DF1F00992BA351314A3E6020A9730BDD8F7144DD1F1D1AF1B0B73B239D82D7`.

The real `aetheris modules showcase` path succeeded: its six Surfacing gallery entries reported second-jet capability and center differential inspection, with four curvature-available expression-backed entries and two honest unavailable procedural entries.

BoundaryPatch/SectionSurface and materialized non-rational B-spline surface supports retain first-jet-only public capability; non-rational B-spline curves expose exact second derivatives. Their unavailable quality inspection returns `Unknown`, which is useful pressure for a later interpolation/fairing milestone. The demonstrated G1/G2 distinction supports a future bounded blend capability, but M2 does not introduce a BlendSurface framework.
