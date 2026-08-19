# CONCEPT-EXPANSION-M1: conceptual front end

The conceptual front end lets Firmament authors structure a design before constructing geometry. Its bounded M1 pipeline is:

```text
Firmament V2 source
  -> authoritative Concept IR (compile-time only)
  -> Feature AIR
  -> Construction AIR
  -> authoritative BRepPlan
  -> BRep
  -> STEP AP242
```

## Language constructs

`Concept` declares a structural contract. It has named members and spatial types, but no values and no geometry:

```firmament
Concept MountingFrame {
    Bounds: Box3
    TopPlane: Plane
    CenterAxis: Axis
    MountPoints: Point3[]
}
```

`Concept Struct` creates a statically resolved semantic/spatial value. It is compile-time-only and cannot emit a body, face, edge, vertex, coedge, or STEP entity:

```firmament
Concept Struct BracketConcept: MountingFrame {
    Bounds: Box3 { Size: [80mm, 50mm, 25mm] }
    TopPlane: Bounds.Face(+Z)
    CenterAxis: Bounds.Center.Axis(+Z)
    MountPoints: Grid {
        Within: Bounds.Face(+Z).Inset(10mm)
        Columns: 2
        Rows: 1
    }
}
```

`Struct` and `Model` are source aliases for the same materialized declaration. The parser normalizes both to `FirmamentV2Document`; Concept IR retains `SourceSpelling` for diagnostics and formatting:

```firmament
Struct Bracket: MountingFrame {
    Box Base { Bounds: BracketConcept.Bounds }
    Modify Base {
        EdgeFinish TopBreak {
            Face: BracketConcept.TopPlane
            Target: Boundary
            Kind: Chamfer
            Distance: 1.5mm
        }
    }
}
```

The equivalent header `Model Bracket: MountingFrame { ... }` has identical compiler meaning. The older, already-supported `Model Name mm` chamfer syntax remains accepted and was not globally rewritten.

## Authoritative Concept IR

`ConceptIrDocument` is attached to the parser-owned Firmament V2 document. It contains:

- `ConceptIrDefinition` structural requirements;
- non-materialized `ConceptIrStructInstance` values and declared concept membership;
- typed resolved values with deterministic stable IDs;
- the normalized materialized declaration and its original `Struct`/`Model` spelling;
- explicit consumer/input/provenance bindings;
- the erasure state `ErasedBeforeFeatureAir`.

This is the compiler representation used to bind materialized inputs, not an after-the-fact report assembled from geometry. Build JSON serializes this same IR under `conceptIr`.

Concept IR contains no BRep topology IDs and no raw dictionary-shaped spatial values. The M1 typed value set used by resolution is `Box3`, `Plane`, `Axis`, `Region2`, `Point3`, and ordered `PointSet`; type names reserve the adjacent `Length`, `Point2`, `Vector3`, `Box2`, and spatial collection vocabulary for compatible expansion. Lengths retain their dimensional meaning at parsing boundaries and are resolved in the document unit (`mm`); they are not accepted as unitless dimensions.

## Spatial resolution and static expansion

M1 implements these typed operations:

- `Box3 { Size: [x, y, z] }` with a centered XY frame and Z extent from `0` to `z`;
- `Bounds.Center` as the box center;
- `Bounds.Face(+X|-X|+Y|-Y|+Z|-Z)` as a plane;
- `Bounds.Center.Axis(direction)` as an axis;
- rectangular face `.Inset(length)` as a bounded `Region2`;
- `Grid` over such a region with positive integer `Columns` and `Rows`;
- deterministic row-major point expansion and compile-time index validation.

A one-column or one-row grid is centered on that dimension. Multi-element dimensions include both inset boundaries. Every generated point has a stable path identity such as `concept:BracketConcept.MountPoints[0]`, an ordinal, resolved coordinates, and source provenance. No loop or runtime pattern remains after resolution, and Firmament gains no mutable state or general control flow.

## Structural conformance and diagnostics

Concept Struct conformance is compiler-owned, deterministic, and structural. Required names must exist and their types must match; `PointSet` conforms to `Point3[]`. M1 uses strict member checking for a Concept Struct that explicitly declares `: ConceptName`.

The front end reports stable diagnostics for missing and unknown members, type mismatches, invalid spatial derivations, out-of-range point indices, circular compile-time dependencies, and illegal references from the compile-time phase to the materialized declaration. It also rejects materialized `Hole` syntax on this new path rather than silently discarding it, because holes combined with the production chamfer route are outside the currently admitted modification network.

A materialized `Struct: ConceptName` now uses the M2 `Expose` block shown in the checked-in demo. M1's spatial values and chamfer proof are unchanged; materialized structural conformance is documented in [`concept-materialization-m2.md`](concept-materialization-m2.md).

## Erasure and provenance

Erasure means “not materialized,” not “forgotten.” Concept Struct instances are absent from `FirmamentV2Document.Solids`, so the executor cannot create a body for them. Their values are copied into typed materialized bindings:

```text
BracketConcept.Bounds   -> Bracket.Base.Bounds
BracketConcept.TopPlane -> Bracket.TopBreak.Face selection
```

The existing AIR chamfer compiler then receives resolved dimensions and `+Z`, without any Concept-IR topology. Build JSON retains both provenance paths in `air.feature.provenance`, alongside the authoritative BRepPlan and STEP reimport report.

## Relationship to Forge concepts

The two uses of “concept” are deliberately distinct:

- A language `Concept` is a compile-time structural type contract owned by the Firmament compiler.
- A Forge C# concept is executable schema, validation, DFM, or PMI behavior supplied by a runtime concept pack.

Basic member/type conformance does not execute C# and does not depend on a Forge validator. A future bridge may let Forge behavioral rules target language-conforming values, but it must preserve this ownership boundary.

## Production proof and M1 limits

The checked-in proof is [`docs/development/milestones/firmament/demo-sources/concept-expansion-m1.firmament`](../../../../fixtures/Regression/DemoRegression/concept-expansion-m1.firmament). It resolves bounds, a top plane, a center axis, and two mounting points; materializes one box from the bounds; selects the chamfer face from the resolved plane; follows `AirPrismaticTopFaceBoundaryChamfer`; exports AP242; and successfully reimports a closed manifold.

M1 is intentionally not a parametric constraint solver or arbitrary compile-time programming system. It has no general functions, loops, mutable values, constraint propagation, cross-instance dependency graph, or materialized member-surface inference. M2 adds typed `Point3` hole consumption and explicit materialized exposure without changing those boundaries or widening the chamfer modification network.
