# Adapter and dogfood evidence

The test `ExactKernelFamiliesExposePointsDerivativesTangentsAndDomains` covers bounded line, full circle, arc-like ellipse trim, hyperbola, and non-rational B-spline support. It verifies points, raw derivatives, unit tangents, domains, regularity, B-spline degree, and the circle seam. `ReversedNativeTrimPreservesAuthoredOrientationWithoutChangingSupportIdentity` proves directed orientation.

The expression fixture evaluates a parabola, sinusoid, and helix with automatic derivatives, rejects non-Length coordinates, and detects division by zero. Firmament syntax is deliberately deferred; the public API supplies the usable seam without a language redesign.

Panel proof: `PanelEdgeAndPipeRouteUseTheSameDirectedPublicCurveLayer` creates a real ruled canopy Panel, evaluates its North semantic edge and tangent through `AuthoredCurve`, and verifies semantic-owner provenance. The Panel support is reused; no duplicate edge geometry is authored. Existing `PanelEdgeCorrespondence.SameDirection` / `OppositeDirections` remains mate evidence and does not rewrite curve identity.

Piping proof: the same test lowers a real standard elbow and obtains stable ordered `Line3`, `Circle3`, `Line3` centerline curves. It verifies inlet, bend endpoint, outlet endpoint, piece identities, and ordering while `PipeRouteIr` retains route intent.

Construction proof: `ConstructiveConeSectionHyperbolaKeepsItsBoundedNonAuthoringRole` runs the exact signed-permutation transverse cone/world-Z construction, bounds its hyperbola branch, and evaluates its first jet. This remains a construction-specific result, not a generic intersection-authoring service.

Machine-readable fixed evaluations and jets are in `deterministic-evaluations.json`. Their SHA-256 is in `deterministic-hashes.json`. `performance.md` records smoke timings for evaluation, first jets, expressions, Panel adaptation, and Piping adaptation.
