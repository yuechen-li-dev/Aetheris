# SMOOTH-CORNER-X1 — bounded smooth footprint

Outcome: success for the immediate footprint blocker. `SmoothRoundedRect2` replaces circular corners with a fixed symmetric polynomial law whose curvature matches the straight sides and across each corner midpoint. This does not complete SURF-G2-X1 parent-support surface blending.

The declaration takes `Center`, `Size`, `CornerExtent`, and optional `Rotation`. It produces four lines and eight cubic spans through the ordinary resolved Profile representation. SectionChain converts the existing cubic carrier to degree-three B-splines; ordinary extrusion uses the existing B-rep planner. No equation interpreter, public control cage, alternate executor, or post-B-rep repair was added. See [the law and authoring contract](../public/firmament/boundaries.md#smooth-polynomial-corners).

The shape is a squircle-style polynomial, not an exact superellipse or a fit to Apple's drawing. Its midpoint is derived from the fourth-power superellipse. Its side curvature is analytically zero, and midpoint reflection preserves geometric curvature. General G2 surface construction remains unimplemented; planar caps are sharp joins and excluded from side-join claims.

## Realized evidence

- The 60 x 40 mm, 8 mm corner-extent, three-section fixture has 36 inspected side/section joins, all `G2WithinSampledTolerance`; maximum shape-operator residual is 2.11e-15 /mm. The previous circular fixture measures 0.125 /mm.
- STEP reimports as one enclosed-manifold solid: 26 faces, 60 edges, 36 vertices; envelope is exactly 60 x 40 x 16 mm in the reimport report.
- The motivating plateau witness now uses smooth corners, with its existing 72.76 x 43 mm envelope, 11.5 mm proxy corner extent, and 2.55 mm height. All 12 inspected side joins pass; maximum curvature residual is 6.11e-16 /mm. Reimport is enclosed-manifold, 14 faces. Its diagnostic top view was visually inspected from the realized STEP.
- Tests cover phone envelope dimensions (77.98 x 163.43 mm, extent 19.43 mm), a rotated 64 x 44 mm variant, degenerate extent rejection, and ordinary template-based extrusion. Phone dimensions are a carrier witness, not a contour-fit qualification.

Generic STEP SHA-256: `67A8614CAD3E6E23AA37C89E3FABE41F1AC52A61C9775D97BFDA564E5B718191`.
Plateau STEP SHA-256: `43AE075C1322E69987E8B704F92B6D7DF1D63986312D3FCE9F66A7D6040DF0D4`.

## Validation

Release solution build: zero warnings and errors. All 20 test-project commands completed serially with exit code zero; 3580 tests passed (FrictionLab reports no tests). Repeated STEP exports are byte-identical. The CLI NuGet package was built. Raw logs and reports remain under `artifacts/local/smooth-corner-x1/`.

## Reproduce

```powershell
dotnet build Aetheris.slnx -c Release
$cli = Resolve-Path Aetheris.CLI/bin/Release/net10.0/aetheris.exe
& $cli section-chain build fixtures/Canonical/SectionChain/smooth-corner-sections.firmament --out artifacts/local/smooth-corner-x1/witness.step --json
& $cli section-chain build fixtures/Experiments/IPhone17ProMax/smooth-plateau-witness.firmament --out artifacts/local/smooth-corner-x1/plateau.step --json
& $cli analyze artifacts/local/smooth-corner-x1/plateau.step --json
& $cli wireframe artifacts/local/smooth-corner-x1/plateau.step --out artifacts/local/smooth-corner-x1/plateau-top.svg --view top --density 2 --samples 128 --json
```

The next isolated blocker is a transverse surface law that matches the supporting faces' tangent planes and geometric curvature. Smooth footprint curvature is now available to that construction through the existing semantic path.
