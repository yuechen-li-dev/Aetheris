# SEMANTIC-LOOPS-X1 — source-grounded topology selections

Semantic selection preserves intent through topology change.

A source edge may become many material edges. The compiler preserves the descendant relation rather than pretending topology never changes.

## Admitted source form

```firmament
Selection OuterTopBoundary {
    Target: TopBoundary
    Source: MainPlate.ProfileSegments([SouthEdge, EastEdge, NorthEdge, WestEdge])
    Require: ClosedLoop
}

EdgeFinish TopEdgeBreak {
    Target: OuterTopBoundary
    Kind: Chamfer
    Distance: 1mm
}
```

X1 admits direct, scaffold-authored Profile extrusion. `ProfileSegments` is source identity, `Target` is a bounded role qualifier, and `Require` is the topology contract. Raw BRep, coedge, and STEP ids are not source syntax.

## Result and resolution contract

The immutable result forms are `VertexSet`, `EdgeSet`, `FaceSet`, `LoopSet`, and `Chain`. Every materialized descendant has a stable diagnostic id, source identity, topology role, parent/source relationship, and optional exact topology handle used internally by the materializer. Authoring never receives mutable BRep collections.

The Profile extrusion planner records correspondence before materialization: profile segment to local start/end boundary edge, longitudinal edge, and side face; profile loop to local start/end cap loop; circular inner wall to its cylinder face. The resolver consumes that plan-owned correspondence plus the materialized body. It does not scan emitted STEP, inspect coordinates, or recognize radii.

`ExactlyOne`, `OneOrMore`, `ConnectedChain`, `ClosedLoop`, and `NonEmptyFaceSet` are explicit contracts. Chain/loop validation uses `DirectedEdgeUse`: duplicate edges, disconnected components, branch vertices, missing closure, and a closed result requested as an open chain fail deterministically.

Typed failures are `SemanticSourceNotFound`, `NoMaterializedDescendants`, `AmbiguousBodyContext`, `SelectionCardinalityMismatch`, `DescendantsNotConnected`, `DescendantsBranch`, `DescendantsDoNotClose`, `MixedBoundaryRoles`, `UnsupportedTopologyChange`, and `SelectionConsumerMismatch`. Diagnostics retain requested source ids, candidate descendants, source span, body, role, and provenance chain.

## Production proof

`fixtures/FirmamentV2/Profile/valid/semantic-top-boundary-chamfer.firmament` resolves the four authored perimeter segments into a closed top-boundary chain, then feeds that validated semantic selection to the admitted rectangular top-loop chamfer route. The finish route emits its changed authoritative BRepPlan; it does not use a raw edge id or a finished-topology search.

The verification artifact is `artifacts/semantic-top-boundary-chamfer.step`, SHA-256 `3821ecb87303402c84abb2c07b3e92e916ff8887049120c7d83362d048be2c06`. Ordinary analysis reports one body, one shell, 10 faces, and `enclosed-manifold`. M8 reimport reports `isEnclosed: true` and `isOrientationConsistent: true`; external inspection remains honestly `ExternalInspectionPending`.

## Inspection and Cadmata handoff

`aetheris inspect-selections <source> --json` performs source parse, authoritative materialization, and selection resolution only. It emits stable id, kind, label, source span, source entity, parent, descendants, topology role, connectivity, traversal order, provenance, consumer, and diagnostics. It does not run STEP analysis or M8.

This is the Cadmata handoff shape. It intentionally includes geometry preview only where the producer has one, and does not add rendering or interactive selection editing.

## Compose X2 extension

The section-stack emitter now publishes correspondence while it consumes normalized arrangement fragments. Each retained Profile segment retains its `arrangement.partN` provenance interval and becomes a stable BRepPlan-side edge/face descendant carrying its slab context. CTC X3 resolves `LeftTopEar.Outer.OuterArc` as one open top-boundary chain and all six `CentralHex` segments as one closed top-boundary loop. No CTC finish is claimed: the current chamfer materializer is intentionally limited to its history-known rectangular profile case.
