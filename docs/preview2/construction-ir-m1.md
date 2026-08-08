# P2-CONSTRUCTION-IR-M1 — exact coaxial construction extraction

## Audit and boundary

`HexBoltBuilder` proved an exact sequence: a six-sided prism, a cone shared by six planar trims, a planar top cap, an interior root blend, a split periodic shank cylinder, a split tip cone, and a planar tip cap. Dimensions, bolt admission, thread metadata, and semantic names are family knowledge. Regular-polygon math, analytic supports, cone/plane hyperbola edges, periodic splitting, and face incidence are reusable exact-construction capability.

## IR and materialization

`ExactConstructionIr.cs` defines a bounded vocabulary: `RegularPrismConstruction`, `AxialCylinderConstruction`, `AxialFrustumConstruction`, `ConePlanarTrimConstruction`, `ConcaveFilletConstruction`, and `AxialSectionStackConstruction`. `ConstructionSemanticGroup` is separate from geometry. A plan names shared supports (`head-cone`, `shank-cylinder`, `tip-cone`, `under-head-torus`) before topology emission.

`HexBoltConstructionPlanner` contains family admission and lowering. The emitter consumes `ExactCoaxialConstructionPlan`, which contains no `HexBoltSpec`. `ExactBrepEmissionContext` owns deterministic vertex, edge, curve, support, loop, coedge, face, shell, and body allocation. `RegularPrismMaterializer`, `ConePlanarTrimMaterializer`, `AxialCylinderMaterializer`, `AxialFrustumMaterializer`, and `PeriodicTorusBlendMaterializer` emit their bounded responsibilities; `CoaxialConstructionMaterializer` only orders their shared boundaries. Cylinder, cone, and torus face uses split deterministically at Y-axis seams while retaining one support ID.

`ConcaveFilletConstruction` adds material to the interior cylinder/shoulder corner; its geometry is a periodic torus blend. It is intentionally distinct from convex, material-removing profile edge fillets.

## Exact conics and scope

The admitted cone/plane family builds `Hyperbola3Curve` boundaries on the shared cone support and regular-prism side planes. Parameters are projected from known endpoints and oriented for the owning loop. No arbitrary surface intersection, Boolean, sweep, loft, or NURBS facility was introduced.

## Migration and evidence

`ExactCoaxialPartBuilder` now lowers directly to `ExactCoaxialConstructionPlan`, returns `ExactCoaxialPartDefinition`, and invokes `ExactConstructionMaterializer`; it has no `HexBoltSpec`, `HexBoltDefinition`, or `HexBoltBuilder` dependency in its production path. The latter is a compatibility/golden-oracle facade over the same generic planner/materializer, avoiding a second geometry kernel.

Tests cover hex and octagonal prism derivation, a fully emitted octagonal non-bolt spacer, root-torus dimensions, shared support identity across split faces, cone/plane trim intent, and planned materialization with Plane/Cylinder/Cone/Torus. Existing M2 tests compare M1-equivalent topology, analytic families, semantics, signatures, STEP reimport, and no `B_SPLINE` output for reference, M10x50, and nonstandard variants.

Current limitations are deliberate: the plan is one connected regular-prism/coaxial stack, periodic splitting is the proven two-half-face policy, cone/plane trimming covers the admitted right-circular-cone/regular-prism-plane family, and the root blend covers a coaxial cylinder/perpendicular shoulder. It is not CSG, arbitrary surface intersection, arbitrary polygon meshing, or a general fillet solver.

## Validation snapshot

The reference construction-IR STEP has SHA-256 `fb45f414bcaba96bdbf6b7773def91c6c3057e4af0bb70d5273d5fe344c61174` on repeated builds. Aetheris reimport reports one body, one shell, 21 faces, 44 edges, 26 vertices, an enclosed manifold, 9 planar faces, 2 cylindrical faces on one support, 8 conical faces on two supports, 2 toroidal faces on one support, and zero B-spline surfaces. FreeCAD/OCCT imports one valid closed solid without healing and reports `Cone:8,Cylinder:2,Plane:9,Toroid:2`.

A Debug-build micro-measurement (Windows, warm process) measured plan creation at about 0.047 ms, direct generic BRep emission at 0.83 ms, the compatibility builder facade including planning and validation at 1.68 ms, and STEP export at 1.81 ms per reference part. These are guardrail measurements rather than a benchmark contract.
