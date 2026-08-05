# PROFILE-EXTRUSION-BREPPLAN-X1

`ProfileExtrusionBRepPlan` is the authoritative topology boundary for standalone line/arc Profile extrusion. **The BRepPlan owns topology. Emitters materialize plans; they do not invent construction.**

The production route is resolved local `Profile` plus a `ConstructionPlane` (or immutable `WorldXY`) and local start/end depths, then `ProfileExtrusionConstructionAir`, then `ProfileExtrusionBRepPlan`, then `ProfileExtrusionBRepMaterializer`, BRep, and AP242. The plan is inspectable with `aetheris inspect-profile <source> --json` before materialization.

The plan contains typed vertices, curves with exact trim and edge sense, edges, explicit `DirectedEdgeUse` coedges, cap/side loops, exact plane/cylinder surfaces, face `SameSense`, shell/body, frame and depth interval, deterministic plan IDs, and correspondence. IDs are derived from resolved Profile/loop/segment provenance plus local role; numeric kernel IDs are deterministic plan-order realization IDs, never random GUIDs.

`LineArcProfileExtrudeEmitter` is retained only as the compatibility entry point. It calls the planner and then the strict materializer. The materializer validates planned directed closure and bindings, allocates the plan identities in plan order, binds supplied exact geometry, and publishes no independently-created correspondence.

Local start/end are canonical roles. Legacy top/bottom names are compatibility aliases for local +Z/-Z only. Cap planes use -/+ local Z, line sides use the frame-mapped profile-facing normal, and arc sides use a cylinder whose axis is local Z and reference direction is local X. Inner loops retain authored local winding and yield cap inner loops and cylindrical wall faces; no coordinate search is used.

The reusable extension seam for LOCAL-FRAME-HOLE-X2 is the plan primitive set: add planned vertices/curves/surfaces/loops/faces and plan-owned descendants to `ProfileExtrusionBRepPlan` (or its successor generic construction-plan container), then consume it with `ProfileExtrusionBRepMaterializer`. A hole can reuse the frame mapping, cap conventions, cylinder binding, `DirectedEdgeUse`, correspondence publication, STEP path, and Cadmata source-to-descendant handoff. Drill cones and host subtraction are intentionally not part of X1.
