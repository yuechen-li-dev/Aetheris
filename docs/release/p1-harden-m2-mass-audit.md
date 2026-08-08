# P1-HARDEN-M2 mass-property audit

## Current architecture

`BrepMassProperties.Evaluate` first validates one closed, connected, two-use shell. It then tries three bounded recognizers: sphere/cylinder seam bodies, vertical line/arc prisms, and the axis-aligned box quarter-cylinder fillet. All other bodies use `BrepDisplayTessellator` twice and integrate oriented mesh triangles as tetrahedra against the origin.

| Surface/trim route | Current treatment |
| --- | --- |
| Plane/cylinder vertical line-arc prism | exact section-area integration; circles and lines |
| Axis-aligned finite quarter-cylinder box fillet | exact recognized volume |
| Sphere/cylinder seam lattice family | exact recognized caps and cylinders |
| Other Plane, Cylinder, Cone, Sphere, Torus trims | deterministic display tessellation at two resolutions |
| B-spline or a face the tessellator cannot fill | unavailable; partial volume is rejected |

The generic result is therefore not an exact analytic evaluator. Its reported bound is

`max(|fine volume - coarse volume|, |fine area - coarse area|, fine surface area * linearTolerance * 4)`.

With the default `linearTolerance = 0.1 mm`, the second term is approximately 41,000 mm³ for the chimera. It is a whole-shell displacement envelope, not a per-face quadrature certificate. The evaluator exposes face signed volume, area, triangle count, surface family, face sense, and orientation coherence, but not per-face error or parameter-domain subdivisions.

## Baseline diagnosis

The defect is both value and bound, not bound alone. Before M2 code changes, authoritative STEP reimport measured:

| Artifact | Expected / FreeCAD (mm³) | Aetheris (mm³) | Delta (mm³) | Relative | Reported bound (mm³) |
| --- | ---: | ---: | ---: | ---: | ---: |
| ExactRolling | 913725.7396023329 | 881896.8785532190 | -31828.8610491139 | -3.4834% | 41238.3782898643 |
| SphereSeamCompatibility | 913733.5792146825 | 879274.5010217372 | -34459.0781929452 | -3.7712% | 41262.7733089027 |

The two-resolution delta was only 6.1364 mm³ for ExactRolling while its absolute bias was 31,828.8610 mm³. Refinement convergence therefore did not detect systematic trim-domain error. Face evidence localized a major part to the horizontal trimmed cap: its chordal display-domain area was 33,556.4996 mm².

M2's attempted Green-boundary replacement produced apparently improved chimera values but M3 full-corpus validation exposed shared curve-orientation regressions up to 4,000,000 mm³ on established prismatic fixtures. That attempt was therefore reverted rather than retained as a brittle special case. Cylinder/ellipse, sphere, ring-torus, horn-torus, and general trim-domain integrals remain on the non-authoritative mesh route.

## Orientation and trim findings

All frozen faces have bindings and all tessellated triangles are coherent with the interpreted face normal. Signed contributions include both positive and negative curved faces as expected for an origin-based divergence integral. No evidence currently justifies changing STEP `same_sense`.

Periodic Cylinder/Sphere/Torus domains are inferred by the display tessellator from 3D trim curves and then sampled. The close coarse/fine result combined with the large cross-kernel delta is evidence of a stable wrong bounded domain or complement on one or more curved faces, not ordinary chord convergence. The next continuation must add exact/deterministic curve-to-parameter-domain boundary integration for those families and certify its quadrature error; increasing display mesh resolution is not an acceptable authoritative fix.

## Promotion decision

The gate does not pass. Geometry hashes and cross-kernel validity remain good, but authoritative post-STEP volume is still wrong by thousands of mm³ and its bound is still about 42,000 mm³. Whole-loop Fillet remains `Experimental`.
