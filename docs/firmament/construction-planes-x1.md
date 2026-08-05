# Construction Planes X1

Concept geometry declares spatial intent. Construction Planes turn that intent into local material coordinates.

A Construction Plane is an immutable, compiler-known proper frame traced from a pre-solved `Concept Struct` plane. It is not face attachment, a mutable sketch plane, a nearest-face lookup, or a constraint solver.

```firmament
Concept Struct SideLayout {
    PositiveXDatum: Plane {
        Origin: [10mm, 2mm, 3mm]
        Normal: [1, 0, 0]
        Up: [0, 0, 1]
    }
}

Construction Plane PositiveXWorkplane { Trace: SideLayout.PositiveXDatum }

Profile SideProfile Using PositiveXWorkplane {
    Rect2 Outline { Center: [0mm, 0mm]; Size: [20mm, 10mm] }
    // named Line2/Segment boundary declarations...
}
```

`Normal` becomes local `AxisZ`. `Up` is projected into the plane as local `AxisY`; `AxisX = AxisY × AxisZ`, and `AxisY` is recomputed as `AxisZ × AxisX`. This produces normalized, orthogonal, deterministic right-handed frames (`determinant = +1`). If `Up` is omitted, a fixed world-axis priority is used, never a camera-relative word. Degenerate normals and parallel orientation hints are rejected with frame-specific diagnostics.

The mapping is explicit:

`world(local(u,v,w)) = Origin + u AxisX + v AxisY + w AxisZ`.

Features remain ordinary in local coordinates. The compiler emits their exact world-space geometry from the Construction Plane basis. `Point2`, profile bounds, curves, and depth retain their local meaning; extrusion follows local +Z. The compatibility `XY`/`WorldXY` Profile source uses the immutable world-XY Construction Plane.

The line/arc Profile materializer consumes that frame directly. It creates transformed vertices and line curves; cap planes use ±local Z; line side faces use transformed local normals; circular arcs create cylinders whose axis is local Z and whose seam is local X. It does not transform a completed BRep, tessellate, spatially match topology, or use a Boolean.

Semantic descendants now publish `LocalStartBoundary`, `LocalEndBoundary`, `LocalStartCapLoop`, and `LocalEndCapLoop`. Existing `Top`/`Bottom` aliases remain for compatibility, but explicitly mean local +Z/-Z and never global Z. Provenance is published as `ConceptPlane -> ConstructionPlane -> Profile -> LineArcProfileExtrude`.

`aetheris inspect-profile <file> --json` reports the compact construction-frame tuple near the Profile identity. Cadmata receives compiler-owned Concept-plane, Construction-plane, and world-mapped Profile entities; it does not infer a frame from BRep geometry.

## Profile extrusion BRepPlan X1

Construction Planes define local material coordinates. Authoritative BRepPlans carry those coordinates into exact topology. `ProfileExtrusionConstructionAir` normalizes a local Profile plus the immutable frame and local interval; `ProfileExtrusionBRepPlan` owns all vertices, supports, edges, directed uses, loops, faces, senses, shell/body, and source correspondence before any BRep is made. The default world-XY frame is the same route, not a parallel planner. `inspect-profile --json` includes that pre-materialization plan.

Construction Plane semantic shaft Holes now use the same source-level frame
contract: `From: Workplane`, local `Center: Point2(...)`, and local `+Z`
`ThroughAll`. The bounded X3 route and compatibility policy are documented in
[Construction Plane Hole source X3](construction-plane-hole-source-x3.md).

Current scope is a proper rigid frame and standalone/additive planar line/arc Profile extrusion. Reflections, scaling, shearing, arbitrary completed-plan transforms, mutable face attachment, generic constraints, and host-subtracted local-frame holes remain unsupported. LOCAL-FRAME-HOLE-X2 should consume `ConstructionPlane` directly for placement and add only the bounded host/subtraction contract; it must not add topology attachment or recompute the frame.
