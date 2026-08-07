# Profile EdgeFinish chimera release closure X3 — seven-station card

X3 separates language policy from valid release geometry. The conceptual matrix
remains eight rows in `ProfileEdgeFinishAnalyticPolicy`, but `ConvexSmall` is
not present in the release Profile: with `Rs = 2 mm` and `F = 4 mm`, it is
invalid for both analytic routes. The release card therefore contains
ConvexSharp, ConvexMedium, ConvexLarge, ReflexSharp, ReflexSmall, ReflexMedium,
and ReflexLarge only. Its outer source loop has 17 segments (12 lines and five
arcs) and independent
area/volume evidence `(38240 + 4 - π) mm² × 24 mm = 917780.601776314 mm³`.

The two discoverable canonical invalid cases are:

- `fixtures/FirmamentV2/Canonical/invalid/profile-edgefinish-convex-small-chamfer-invalid.firmament`
- `fixtures/FirmamentV2/Canonical/invalid/profile-edgefinish-convex-small-fillet-invalid.firmament`

Their precise diagnostics are respectively
`ProfileBoundaryChamferConvexArcRadiusTooSmall` and
`ProfileBoundaryFilletConvexArcSpindleUnsupported`. Both name the station,
source/finish radii, `Rs < F`, convex material side, derived negative offset or
spindle locus, exact reason, and actionable correction. They do not say
`PlannerRequired` and do not offer a NURBS escape hatch.

The M1 straight-fillet assertion is reconciled in this revision. The exact
removed volume for span `S` and radius `r` is `S*r²*(1 - π/4)`; for the 20 × 10
× 8 fixture, `1600 - 14*4*(1 - π/4) = 1587.982297150257 mm³`. The prior generic
tessellated estimate undercounted the quarter-cylinder removal. The mass
evaluator now recognizes the emitted axis-aligned-box/finite-quarter-cylinder
BRep pattern directly, giving the analytic result after STEP reimport with a
`0.01 mm³` assertion tolerance.

The code-backed matrix remains authoritative and unchanged:

| Finish | ConvexSmall | ConvexMedium | ConvexLarge | Reflex rounded arcs |
| --- | --- | --- | --- | --- |
| Chamfer | Invalid collapsed offset | Cone apex / BoundedDegenerate | Cone frustum / Regular | Cone frusta / Regular |
| Fillet | Invalid spindle | Sphere limit / BoundedDegenerate | Horn torus / InteropSensitive | Ring tori / Regular |

Sharp convex remains the sphere-junction route. Sharp reflex remains exact
horn-torus rolling by default, with `SphereSeamCompatibility` as an intentional
opt-in plan. No NURBS policy exists.

## X4 planning follow-up

X4 now supplies a typed, source-order pre-emission mixed-shell plan with
Plane/Cone and Cylinder/Torus-or-Sphere patch variants, explicit source-bound
seams, and Cone-apex/Sphere-limit degeneracy records. It is deliberately not a
patch-stitching route. The source card was also audited: its retained Profile
has 17 segments (12 lines and five arcs), not the stale 18-segment count stated
by earlier notes.

## Remaining materialization boundary

The release card is a valid base artifact. Whole-loop Chamfer now emits through
the authoritative Plane/Cone source-order shell and has a persistent STEP
artifact. Fillet still intentionally stops before topology on the first arc
with its policy-rich `ProfileBoundaryFilletArcMaterializationNotImplemented`
diagnostic because sharp junction patches have not yet been extracted from the
existing finite two-line-body planners. Therefore this document does not claim
completed Fillet/compatibility artifacts, external-kernel smoke, or Preview-1
readiness. The next implementation must create reusable sharp junction topology
and compose it with the existing cylinder/torus/sphere seam plan; it may not
replace that work with a spline or Boolean fallback.
