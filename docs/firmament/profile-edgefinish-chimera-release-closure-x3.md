# Profile EdgeFinish chimera release closure X3 — seven-station card

X3 separates language policy from valid release geometry. The conceptual matrix
remains eight rows in `ProfileEdgeFinishAnalyticPolicy`, but `ConvexSmall` is
not present in the release Profile: with `Rs = 2 mm` and `F = 4 mm`, it is
invalid for both analytic routes. The release card therefore contains
ConvexSharp, ConvexMedium, ConvexLarge, ReflexSharp, ReflexSmall, ReflexMedium,
and ReflexLarge only. Its outer source loop has 18 segments and independent
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

## Remaining closure boundary

The release card is a valid base artifact, but the authoritative mixed
line/arc Plane/Cone and Cylinder/Torus-or-Sphere composer is not yet emitted.
Whole-loop Chamfer/Fillet intentionally stop before topology with a policy-rich
`ProfileBoundary*ArcMaterializationNotImplemented` diagnostic. Therefore this
document does not claim completed finish STEP artifacts, external-kernel smoke,
or Preview-1 readiness. The next implementation must create direct analytic
seams and finite apex/sphere/horn topology; it may not replace that work with a
spline or Boolean fallback.
