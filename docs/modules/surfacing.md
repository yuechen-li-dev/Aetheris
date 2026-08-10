# Surfacing Module

Surfacing is not synonymous with NURBS. Aetheris prefers the mathematical construction that explains the shape; spline control is a lower-level refinement mechanism.

`Aetheris.Surfacing` 0.1.0 owns `Surfacing.RuledSurface` and `Surfacing.RuledTransition`. Both lower through the small `RuledSurfaceIr`: stable construction identity, two compatible boundaries, typed construction kind, boundary provenance, and retained developability evidence. Source intent remains `RULED_SURFACE` even where STEP serializes the exact degree-(1,1) form as a B-spline support.

M0 admits line-line boundaries as exact bilinear ruled patches and coaxial circle-circle boundaries as exact cylinders or cones. `RuledTransition` uses the same exact construction but records transition/section intent rather than introducing a generic `Loft`. Arbitrary non-rational splines, mixed curve families, trimming networks, continuity optimization, and NURBS editing are not claimed.

The canonical saddle uses two skew boundary lines. Linear interpolation between them evaluates the bilinear hyperbolic-paraboloid family directly; the closed showcase panel retains two degree-(1,1) ruled support faces and four planar sides. It does not approximate authoring intent with a high-degree patch. `RuledCanopy` is the module-owned Template proof.

Future local spline refinement should consume a selected region of a `RuledSurface`/`RuledTransition`, retain the parent construction and boundary provenance, and emit explicit refinement evidence. Raw spline patches are not the default authoring representation. That seam is documented only; M0 implements no spline editing.

Evidence: [M0 validation report](artifacts/m0/validation-report.json), [CLI inspection](artifacts/m0/inspection-evidence.md), [saddle STEP](artifacts/m0/ruled-saddle.step), and [ownership audit](artifacts/m0/ownership-audit.md).
