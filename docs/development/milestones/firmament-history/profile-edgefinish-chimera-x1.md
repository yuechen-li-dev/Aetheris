# Profile EdgeFinish chimera X1 — source-card and planner boundary (superseded by X2 policy card)

> This is historical X1 evidence. The current valid source card is the X3
> seven-station release card; ConvexSmall moved to canonical-invalid fixtures.
> Its policy matrix is superseded by [the X2 policy card](profile-edgefinish-chimera-closure-x2.md) and [the X3 release note](profile-edgefinish-chimera-release-closure-x3.md).

X1 introduces the permanent *source* conformance card for Profile edge finishes:
`fixtures/Canonical/valid/profile-edgefinish-chimera-base.firmament`.
It is a single CCW outer Profile extruded from 0 to 24 mm.  `F = 4 mm`; the
rounded source radii are Small = 2 mm, Medium = 4 mm, and Large = 8 mm.
The explicit straight isolation runs are 32–40 mm (at least `8F`).

The eight source-bound selections are `ConvexSharpStation`, `ConvexSmallStation`,
`ConvexMediumStation`, `ConvexLargeStation`, `ReflexSharpStation`,
`ReflexSmallStation`, `ReflexMediumStation`, and `ReflexLargeStation`.  Rounded
stations own a named arc (`ConvexSmallArc`, etc.); sharp stations own the two
adjacent named lines.  This is deliberately a Profile identity contract, never a
BRep-edge discovery contract.

## Baseline evidence

The base fixture builds and exports as a line/arc extrusion. It has one body,
one closed shell according to the STEP importer, 19 Profile segments (13 lines
and 6 circular arcs), and bounds `[0, 0, 0]` to `[420, 100, 24]`. The literal
source-space assertion is independently obtained by Green's theorem on the
line/arc boundary: area `38240 mm^2 × 24 mm = 917760 mm^3`.

The profile extrusion exporter now preserves `EDGE_CURVE.same_sense` for
clockwise arcs. Independent STEP reimport therefore agrees with the source
proof at `917760 mm^3`; this specifically prevents reflex cylindrical faces
from being trimmed through their complementary sweep by SolidWorks. The compact
section-normalization report still needs separate directed-arc graph work, but
it is no longer used to contradict this exact-prism volume evidence. The
canonical V2 parser still does not evaluate this assertion itself for
source-bound Profile selections, so that parser integration remains follow-up
work rather than silently claimed coverage.

The persistent base artifact is
`artifacts/edgefinish/profile-edgefinish-chimera-base.step` (23,383 bytes,
SHA-256 `7aa40fe01467be3475716261e2ce0019266a9cb1677108aceb57072a976f22ed`).
Its production canonicalization is
`artifacts/edgefinish/profile-edgefinish-chimera-base.canonical.step` (23,579
bytes, SHA-256 `15b1949efe524364f9664d9f84bb8f022f279f9e9ff837b9e051e89ac99157fc`).

## Current admission matrix

The fixtures for whole-loop Chamfer and Fillet are present, but are intentionally
not called a passing conformance result.  Both current materializers are
line-only.  The first source-ordered curved station is reported exactly rather
than hidden behind a loop-topology fallback:

| Route | Actual whole-loop result | First diagnostic |
| --- | --- | --- |
| Chamfer, Top, Distance 4 | Not admitted | `ProfileBoundaryChamferArcSegmentPlannerRequired:station=ReflexSmall:...:radiusRelation=LessThan:missingPlanner=ChamferArcDerivedExtrusionEdge` |
| Fillet, Top, Radius 4 | Not admitted | `ProfileBoundaryFilletArcSegmentPlannerRequired:station=ReflexSmall:...:radiusRelation=LessThan:missingPlanner=FilletArcDerivedExtrusionEdge` |
| Fillet, ReflexSharp, SphereSeamCompatibility | Not admitted on this mixed line/arc host | the same typed arc-host planner boundary, scoped to `ReflexSharpStation` |

The sharp line-line primitives remain covered by their existing focused fixtures:
convex uses cylinders plus a sphere; reflex defaults to the exact rolling
horn-torus patch, and `SphereSeamCompatibility` remains an explicit distinct
override.  X1 does not alter that policy.

## Derived rounded-source surface matrix

This is the exact geometry a bounded rounded-source planner must own; it is not
claimed as emitted topology today.  For a Top chamfer, the section transition of
an arc-derived cylinder is a right circular cone.  Convex material offsets to
`Rs - F`; reflex material offsets to `Rs + F`.

| Station class | Rs/F | Chamfer surface/admission | Fillet rolling surface |
| --- | --- | --- | --- |
| Convex Small | 0.5 | cone crosses its axis: invalid without a specific collapsed-offset policy | signed major locus `Rs-F=-2`; equivalent spindle torus (`R=2, r=4`), not admitted |
| Convex Medium | 1 | cone apex at the axis: bounded degenerate if trimmed topology is explicitly represented | spherical limit (`R=0, r=4`), bounded degeneracy requiring a dedicated planner |
| Convex Large | 2 | regular conical frustum | ring torus (`R=4, r=4`) is horn, so interop-sensitive and requires a trimmed-patch planner |
| Reflex Small | 0.5 | regular conical frustum | ring torus (`R=6, r=4`), regular |
| Reflex Medium | 1 | regular conical frustum | ring torus (`R=8, r=4`), regular |
| Reflex Large | 2 | regular conical frustum | ring torus (`R=12, r=4`), regular |

The sharp rows are line-line junctions, not arc offsets: convex selects the
direct cylinder/cylinder miter junction; reflex selects the existing horn torus by default.

## Required next bounded work

The missing subsystem is not a generic BRep fillet or Boolean fallback.  It is a
source-bound line/arc Profile section-transition and rolling-patch planner that
can emit, trim, and compose Plane/Cylinder/Cone/Torus/Sphere faces in Profile
order.  It must first provide a mixed line/arc shell route before focused sharp
station selections can reuse the existing junction emitters on this card.

Consequently, no Chamfer/Fillet/compatibility STEP artifact, external-kernel
smoke result, finished-body volume, canonical hash, or successful whole-loop
support matrix is recorded yet. The base artifact is correct source-prism
evidence, but not the finished-feature conformance card.
