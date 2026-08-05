# CONCEPT-PLANE-DRILLED-HOLES-X1 — implementation boundary audit

## Status

This Hole milestone is **not implemented** in the current production compiler.  The
immutable local material-frame prerequisite is now implemented by
[CONSTRUCTION-PLANES-X1](construction-planes-x1.md); local-frame host subtraction and
drill-point cones remain deferred. This
document records the verified boundary so that a future implementation can extend
the existing compiler-owned `AirHoleFeature` family instead of creating a parallel
feature system or accidentally treating a mutable BRep face as an authoring
reference.

Concept frames contain pre-solved spatial intent. Features consume those frames;
they do not solve for placement.

A drilled hole is a semantic construction composed of a cylindrical shaft and a
conical drill-point termination, not merely a subtracted cylinder.

## Existing architecture (verified 2026-08-05)

* Firmament V2 parses `hole<shaft>` into `FirmamentV2SemanticHoleDecl`, then
  lowers it in `FirmamentV2SemanticHoleLowering` to `AirHoleFeature`.
* `AirHoleFeature` owns a shaft, a `ThroughAll` or fixed `Depth` end condition,
  and stack components for simple shaft, counterbore, and countersink.  It has
  no drill-point component, shaft-vs-total depth convention, or frame placement.
* Its placement is `AirFaceLocalHolePlacement`: an entry-face name plus `U/V`
  coordinates.  This is precisely the mutable-face-shaped abstraction this
  milestone must replace for new Concept-frame placement.
* The executable lane is `AirHoleSimpleShaftMaterializer`, then
  `ProfileStackExtrudeExecutor`.  It accepts only rectangular hosts and only
  `top/+Z` or `bottom/-Z` placement. `AirHoleCompositeMaterializer` retains the
  same Z-axis restriction.
* The authoritative through-hole correspondence presently publishes only
  `HoleEntryLoop`, `HoleExitLoop`, and one `HoleWallFace`.  Selection parsing
  exposes only those roles.
* `ConceptIrPlaneValue` currently holds only origin and normal.  The Concept
  hole resolver accepts only a `+Z` plane, requires `Point3` input, and rejects
  every other plane with `firmament-concept-point-projection-unsupported`.
* The existing `FirmamentV2SideHoleRoutePolicy` belongs to the separate
  `region ... cut Cylinder ... through` route. Its own documentation explicitly
  states that it is controlled, face-selector based, and not general side-hole
  support. It is not a valid implementation substrate for immutable Concept
  frames.

## Required native extension (not a parallel model)

The next implementation must evolve these existing types together:

1. Extend `ConceptIrPlaneValue`, or introduce a sibling *Concept IR* frame value,
   to own normalized `Origin`, `Normal`, `AxisU`, `AxisV`, handedness, and source
   provenance. Validate nonzero vectors and mutual orthogonality at Concept
   resolution time.
2. Extend the existing semantic-hole declaration and `AirHoleFeature` placement
   with an immutable frame identity, local `Point2`, resolved world mouth point,
   and explicit construction direction. Do not overload
   `AirFaceLocalHolePlacement` for this: its `EntryFaceName` contract encodes
   exactly the forbidden attachment model.
3. Extend `AirHoleEndCondition` with unambiguous `ShaftDepth` and `TotalDepth`,
   and add a compiler-owned drill-point stack component. Its included point angle
   must be visible and validated; a candidate default is 118 degrees only after
   it is declared as a documented manufacturing default. For radius `r` and
   included angle `theta`, use `r / tan(theta / 2)` as the exact axial tip length.
4. Add an authoritative local-frame analytic materializer. It must construct
   exact cylinder and cone surfaces and publish mouth, transition, cone, and tip
   descendants while intersecting bounded host-material intervals. The existing
   profile-stack and box-cylinder builders are Z-axis-only and cannot establish
   this contract by a coordinate relabeling patch.

Only after that materializer exists should Cadmata consume its compiler-published
frame and analytic envelopes, and `inspect-selections` gain the new roles.

## Why no partial parser/AIR patch was made

Adding syntax or a frame record without an admitted materializer would create a
second, non-executable Hole model and make false claims about exact BRepPlan,
STEP, M8, correspondence, and Cadmata evidence. Conversely, modifying the
existing entry-face model would preserve face attachment under a new spelling.
Both violate the milestone contract.

The required work spans Concept parsing/resolution, Hole AIR, exact arbitrary-axis
BRep planning, host material interval evaluation, correspondence, selection
grammar, inspection, and Cadmata. It should be scheduled as a single coherent
compiler increment after the current Z-only profile-stack lane is generalized or
a bounded local-frame analytic feature lane is introduced.

## CTC side-hole evidence

No source-side CTC dimensions or analytic STEP evidence sufficient to declare
mouth centers, diameters, depths, or point angles was found in the inspected
repository materials. Existing CTC reconstruction documents should therefore not
be updated with a candidate declaration. The next CTC increment is blocked on
the reusable local-frame drill-point capability plus measured source evidence;
it must not infer values from screenshots.

## Validation performed

`dotnet restore Aetheris.slnx` and
`dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1` both succeeded on the
current worktree. The build reports pre-existing JavaScript dependency
vulnerability warnings from the JavaScript SDK audit; it has no .NET errors.
