# Profile-boundary chamfer M1

M1 binds a chamfer to an authored Profile identity, never to a rediscovered BRep edge. The admitted target is a straight-line segment, a connected chain selected from one outer loop, or the complete outer loop of one prismatic Profile extrusion.

```firmament
Modify Body {
    EdgeFinish TopPerimeter {
        Target: Bracket.Outer
        On: Top
        Kind: Chamfer
        Distance: 1mm
    }
}
```

For one segment use `Target: Bracket.Outer.South`. A chain can be named with Selection:

```firmament
Selection BreakChain { Source: Bracket.Outer.[South, East, North] Require: ConnectedChain }
Modify Body { EdgeFinish Break { Target: BreakChain On: Top Kind: Chamfer Distance: 1mm } }
```

`On` is required. `Top` and `Bottom` are evaluated in the Profile extrusion's local construction-plane axis, not global Z. The inset uses exact line support offsets: material side follows resolved loop winding, selected-selected supports intersect exactly, and an open chain is closed by planar triangular termination patches without extending the finish to another source segment.

The current materialized contract is one outer line-only Profile loop, a positive equal distance below the extrusion thickness, and one segment/connected chain/whole loop. Curves are source-bound but not silently coerced: a selected circular arc now reports `ProfileBoundaryChamferArcSegmentPlannerRequired` with station/segment, convex-or-reflex material side, source radius, finish distance, radius relation, and the exact missing `ChamferArcDerivedExtrusionEdge` planner.  The X1 chimera card documents the required conical section-transition routes. Inner loops, disconnected or duplicate chains, and legacy `Face: +Z / Target: Boundary` requests receive typed rejection diagnostics.

Junction polarity is classified from resolved Profile semantics, never BRep edge direction: predecessor/successor tangents give the signed turn; loop winding and outer/inner role determine the material side. A positive material-side turn is a `ConvexProfileJunction`; a negative one is a `ReflexProfileJunction`. Thus an orthogonal outer CCW L notch has a `-90°` signed turn and a `270°` material interior angle. The planar inset construction intersects the two inward line supports at either class; it does not use a distinct rolling-surface patch. `profile-chamfer-reflex-junction-top.firmament`, the Bottom counterpart, and the mixed L-loop fixture make those guarantees explicit.

Compose now admits a Top whole-outer-loop chamfer with disjoint Shaft or Counterbore cavities. Admission conservatively rejects touching/intersecting circular cavity footprints in the transition corridor with `ProfileBoundaryChamferIntersectsShaft` or `ProfileBoundaryChamferIntersectsCounterbore`. Bottom Compose, open-chain Compose, inner-loop chamfers, and non-line source segments remain outside this bounded route.

The authoritative polyhedral section transition preserves EdgeFinish and Profile/loop/segment provenance. For the 20×10×8 rectangle at `d=1`, the whole-loop section proof is `7 * 200 + (200 + 4 * 171 + 144) / 6 = 1571.3333333333333 mm³`; the canonical fixture verifies that value after STEP reimport.
