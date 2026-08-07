# Profile EdgeFinish chimera closure X2 — analytic policy card

X2 supersedes the *derived rounded-source surface matrix* in X1. Its historical
source card is now superseded by X3's release card; see
`profile-edgefinish-chimera-release-closure-x3.md`. The source remains
source-bound: no anonymous
BRep-edge selection, Boolean repair, NURBS, or sampled finish is admissible.

Historical status update: X4 adds the pre-emission, typed source-order shell
plan for the X3 seven-station release card. This X2 policy matrix remains
unchanged; the production mixed-shell B-rep emitter is still the next boundary.
See `mixed-analytic-edgefinish-shell-x4.md`.

`ProfileEdgeFinishAnalyticPolicy` is the code-level conformance table. It uses
the JudgmentEngine to choose one bounded candidate deterministically and retains
the rejected candidate reasons. The policy is evaluated before BRep emission.
`Regular` is smooth within its trimmed domain; `BoundedDegenerate` requires
explicit finite topology (an apex/contact); `InteropSensitive` is exact but
requires downstream-kernel smoke; and `Invalid` has no source-order-preserving,
manifold patch under the current policy.

## Chamfer matrix (F = 4 mm)

| Station | Source | Side | Rs | Relation | Planner | Family | Regularity | Admission |
| --- | --- | --- | ---: | --- | --- | --- | --- | --- |
| ConvexSharp | SharpLineLine | Convex | 0 | Zero | LineChamferPlan | Plane | Regular | Supported |
| ConvexSmall | ArcDerived | Convex | 2 | LessThan | ArcChamferCollapsedOffsetRejected | Cone | Invalid | UnsupportedWithTypedDiagnostic |
| ConvexMedium | ArcDerived | Convex | 4 | Equal | ArcChamferApexPlan | Cone | BoundedDegenerate | SupportedWithExplicitPolicy |
| ConvexLarge | ArcDerived | Convex | 8 | GreaterThan | ArcChamferConePlan | Cone | Regular | Supported |
| ReflexSharp | SharpLineLine | Reflex | 0 | Zero | LineChamferPlan | Plane | Regular | Supported |
| ReflexSmall | ArcDerived | Reflex | 2 | LessThan | ArcChamferConePlan | Cone | Regular | Supported |
| ReflexMedium | ArcDerived | Reflex | 4 | Equal | ArcChamferConePlan | Cone | Regular | Supported |
| ReflexLarge | ArcDerived | Reflex | 8 | GreaterThan | ArcChamferConePlan | Cone | Regular | Supported |

For a source circular side face, take the section at `z = top - F` on the
authored cylinder and its material offset at `z = top`. The ruled locus is the
right circular cone between radii `Rs - F` for convex material and `Rs + F` for
reflex material. Tangent Line→Arc and Arc→Line source joins produce direct
plane/cone seams; an extra blend would be non-policy. `Rs = F` terminates at one
explicit apex vertex, never a zero-radius edge. `Rs < F` crosses the source axis;
X2 rejects it as `ProfileBoundaryChamferCollapsedOffsetInvalid` pending a proof
of a useful manifold split topology.

## Fillet matrix (F = 4 mm)

| Station | Source | Side | Rs | Relation | Planner | Family | R/r regime | Regularity | Admission |
| --- | --- | --- | ---: | --- | --- | --- | --- | --- | --- |
| ConvexSharp | SharpLineLine | Convex | 0 | Zero | ConvexSharpSphereJunctionPlan | Sphere | — | Regular | Supported |
| ConvexSmall | ArcDerived | Convex | 2 | LessThan | ArcFilletSpindleRejected | Torus | R=2,r=4 Spindle | Invalid | UnsupportedWithTypedDiagnostic |
| ConvexMedium | ArcDerived | Convex | 4 | Equal | ArcFilletSphereLimitPlan | Sphere | R=0,r=4 limit | BoundedDegenerate | SupportedWithExplicitPolicy |
| ConvexLarge | ArcDerived | Convex | 8 | GreaterThan | ArcFilletTorusPlan | Torus | R=4,r=4 Horn | InteropSensitive | SupportedWithExplicitPolicy |
| ReflexSharp | SharpLineLine | Reflex | 0 | Zero | ReflexSharpExactRollingPlan | Torus | R=4,r=4 Horn | InteropSensitive | SupportedWithExplicitPolicy |
| ReflexSmall | ArcDerived | Reflex | 2 | LessThan | ArcFilletTorusPlan | Torus | R=6,r=4 Ring | Regular | Supported |
| ReflexMedium | ArcDerived | Reflex | 4 | Equal | ArcFilletTorusPlan | Torus | R=8,r=4 Ring | Regular | Supported |
| ReflexLarge | ArcDerived | Reflex | 8 | GreaterThan | ArcFilletTorusPlan | Torus | R=12,r=4 Ring | Regular | Supported |

The signed rolling locus is `Rs - F` on convex material and `Rs + F` on reflex
material. Its absolute torus major radius is used only after preserving this
material-side sign. A zero convex locus is a true sphere limit, not a degenerate
torus. The convex spindle patch is rejected as
`ProfileBoundaryFilletSpindlePatchInvalid`; it is not silently split or
self-intersected. The sharp reflex default remains ExactRolling horn-torus.
`ReflexJunction: SphereSeamCompatibility` remains an opt-in, geometry-plan
alternative only for that sharp junction; it selects a sphere and never replaces
the default.

## Current materialization boundary

This repository revision completes the classified policy card and focused tests,
but does **not** claim the mixed line/arc BRep composer or finished artifacts.
Until it exists, curved requests fail before topology with
`ProfileBoundary*ArcMaterializationNotImplemented`, including station, Rs/F
relation, selected exact planner/family, regularity, admission, and the typed
invalid diagnostic where applicable. Thus no Cone/Sphere/Torus is misreported as
emitted. Existing sharp line-line materializers, their STEP orientation
regression, and the base artifacts remain unchanged.

The next bounded implementation is the direct line/cone and cylinder/torus-or-
sphere seam materializer, followed by apex/horn external-kernel smoke. Only then
may whole-loop Chamfer/Fillet STEP files, Assert Volume literals, hashes, and a
Preview-1 readiness claim be added.
