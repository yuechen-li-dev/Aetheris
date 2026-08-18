# AIR-EDGE-FINISH-CONSOLIDATION-M2

This milestone consolidates the admitted localized Box `SharedEdge(+X,+Z)` chamfer and fillet routes under one closed, exact localized-edge-replacement compiler model. It does not admit junctions, corners, imported/no-history bodies, arbitrary support surfaces, or alternative finish constructions.

## Shared model

`AirEdgeFinishFeature` is the common Feature AIR record. It owns semantic body identity, typed edge selection, typed rule (`AirEqualDistanceEdgeFinishRule` or `AirConstantRadiusEdgeFinishRule`), source span, history classification, and admission outcome. It is internal compiler IR; Firmament continues to expose semantic intent and never emitted BRep identities.

`LocalizedEdgeReplacementConstruction` is the common immutable Construction AIR. It owns the admitted finite selected edge, support ownership, retained regions, material side, explicit endpoint policy, cross-section, provenance, and a typed replacement geometry. `PlanarChamferReplacement` owns the straight setback boundaries and planar face. `CylindricalFilletReplacement` owns the exact start/end quarter arcs and cylindrical support surface.

The shared hard-admission gate proves history-known positive Box dimensions, the finite `+X/+Z` straight convex edge, two planar supports, a positive bounded rule value, and the explicit-owned-endpoint policy. A single admitted construction is selected directly; there are no alternatives to score. Rule identity is checked before common admission, so a wrong kind remains a precise authored-input error. Oversize, invalid selection, invalid history, and degenerate geometry remain typed errors; chamfer and fillet retain their existing compatibility error codes.

## One topology authority

`LocalizedEdgeReplacementTopologyPlan` owns the 5-point section, planned vertices/edges, ordered loops/coedges, retained support faces, unaffected faces, replacement face, endpoint faces, stable boundary roles, and deterministic signature. `LocalizedEdgeReplacementCompilerModel.BuildBRepPlan` is the sole localized planner for both routes. It preserves the historical `LocalizedPlanarReplacement` and `LocalizedTangentBlend` plan kinds for compatibility while placing the shared realization plan in `AirBRepPlan.LocalizedEdgeReplacementRealizationPlan`.

The shared emitter consumes that construction/plan and owns all topology assembly. It performs one closed typed replacement switch only where geometry differs: the chamfer binds lines and planes; the fillet binds quarter-circle trims and a cylinder. Neither route independently reconstructs loops, coedges, or faces.

Stable roles include `RetainedSupportFaceA`, `RetainedSupportFaceB`, `ReplacementFace`, `ReplacementBoundaryA`, `ReplacementBoundaryB`, `EndpointTransitionStart`, `EndpointTransitionEnd`, and `UnaffectedFace`. Compatibility semantic roles additionally mark the replacement as `ChamferFace` or `FilletFace`.

## Reporting and export

`localizedEdgeFinish` is the authoritative common Firmament export trace. It records kind, rule, selected edge, `LocalizedEdgeReplacement` construction, typed replacement geometry, direct selection mode, ownership counts, endpoint policy, authoritative plan signature, valid preflight, and no legacy fallback. Existing `localizedChamfer` and `localizedFillet` traces remain unchanged for consumers.

Both routes still validate the emitted body with `BrepExportPreflight`, export with `BrepExportPreflightMode.Enforce` and `TrustedProductionRoute`, then STEP-reimport and manifold-check. No fallback route is selected.

## Regression evidence

The real CLI regression suites cover chamfer and fillet for `10 x 8 x 6` at 1 and 2, and `12 x 5 x 7` at 1; they also cover zero, oversized, unsupported-selection, and two-edge-junction rejection. They verify deterministic STEP, preflight, STEP reimport, topology counts, planar/cylindrical surface classification, exact setbacks/radius/axis, and the shared trace. The exact prismatic-profile volumes are `W*D*H - D*d^2/2` for a chamfer and `W*D*H - D*r^2*(1-pi/4)` for a fillet; for the `10 x 8 x 6`, value-2 smoke artifacts these are `464` and `473.132741...` respectively. CAD Assistant also completed an import/display smoke of the generated R=2 fillet STEP after consolidation.

## Deliberate limits and next seam

Endpoint ownership is only the two explicit Box end caps. Adjacent selected edges, junction/corner patches, variable-radius blends, and non-planar support pairs remain rejected before topology emission. A future bounded finish adds one typed `LocalizedReplacementGeometry` case and its exact curve/surface bindings; it reuses the admission, ownership, plan, and shell assembly rather than adding a second topology compiler. The next appropriate milestone is an explicitly witnessed two-edge corner/junction replacement, not a broader edge-selection expansion.

Historical evidence: [localized planar chamfer A1](localized-planar-single-edge-chamfer-a1.md) and [localized fillet M1](../../milestones/general/air-fillet-localized-m1.md).
