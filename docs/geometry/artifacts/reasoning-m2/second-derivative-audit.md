# Second-derivative audit

| Area | Finding | Classification / M2 action |
|---|---|---|
| Kernel.Core hyperbola | `Hyperbola3Curve.SecondDerivative` already implements the native cosh/sinh form | Reused through the public adapter |
| Kernel.Core non-rational B-spline curve | Exact polynomial first derivative existed; second derivative was missing | Extended the derivative-control-polygon evaluator; no finite differences |
| Circle, ellipse, line | First derivatives existed in family adapters | Added local exact-form second derivatives |
| Shared unit-aware expression tree | Forward first AD for curves and patches | Extended once to `Duu/Duv/Dvv`; reused by both dimensions |
| Surfacing ruled patches | Boundary evaluators exposed point/tangent; Panel wrapper finite-differenced first jets | Added ruled second jets from boundary jets: linear interpolation, mixed derivative, zero `Dvv` |
| Section/Boundary patches | Procedural point evaluators/materializers | Second capability remains unavailable rather than invented |
| SurfaceMeshIR/Drawing/FEA | No authoritative reusable fundamental-form or curvature service found | No duplicated curvature math added there |
| Legacy blend/fillet search | No general production curvature layer found | Curvature implemented once in `Aetheris.Geometry` |

Duplicated numerical derivative pressure remains in display/materialization code, but it serves approximation/tessellation rather than an authored public second jet. M2 does not broaden that refactor.
