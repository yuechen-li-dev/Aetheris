# AIR-CHAMFER-PRODUCTION-M1 — authoritative bounded top-boundary chamfer

Status: implemented  
Date: 2026-08-03

## Implemented pipeline

The production path is now:

```text
Firmament V2 PascalCase source
  -> FirmamentV2EdgeFinishDecl
  -> AirChamferFeature
  -> AirPlanarProfileNode[3] + AirSectionTransitionNode
  -> AirBRepPlan containing PrismaticSectionTransitionTopologyPlan
  -> PrismaticSectionTransitionEmitter.Emit(authoritativePlan)
  -> BrepBody
  -> Step242Exporter
  -> Step242Importer verification
```

`TopFaceLoopChamfer` remains semantic Feature AIR intent. Construction AIR contains only ordered planar profiles, identity correspondence, and a split-preserving section transition. STEP performs serialization only.

## Source contract

The admitted syntax is:

```firmament
Model AirChamferCanonical mm

Box Base {
    Size: [10mm, 8mm, 6mm]
}

Modify Base {
    EdgeFinish TopBreak {
        Face: +Z
        Target: Boundary
        Kind: Chamfer
        Distance: 1mm
    }
}
```

This PascalCase form is intentionally narrow and does not rewrite or reinterpret older V1/V2 fixtures.

## Layer contracts

Feature AIR preserves body and feature identity, `FaceBoundary(+Z)`, equal distance in millimetres, source span, construction-history classification, and an explicit admitted/rejected/deferred reason. It contains no coedge, STEP, or materializer topology identifiers.

Construction AIR contains three immutable profiles:

- the original rectangle at `z = 0`;
- the original rectangle at `z = height - distance`;
- the inset rectangle at `z = height`.

The `AirSectionTransitionNode` references these profiles in order and records identity-by-profile-index correspondence plus `preserve-section-splits` policy.

`PrismaticSectionTransitionTopologyPlan` is the topology authority. It deterministically owns planned vertices, section and transition edges, cap/side/transition faces, boundary edge uses and orientation, ordering, expected loops/coedges, split policy, and a stable geometry/topology signature. `AirBRepPlan.RealizationPlan` stores that object, while the emitter consumes the same instance. The former independent prediction/emission relationship is no longer used by this production route.

## Admission and rejection

Admitted:

- one history-known axis-aligned rectangular-prism `Box`;
- its planar `+Z` complete outer boundary;
- uniform symmetric equal-distance chamfer;
- `distance > 0`, `distance < width/2`, `distance < depth/2`, and `distance < height`.

Explicitly rejected or deferred:

- zero/non-finite/oversized distances;
- faces other than `+Z`;
- targets other than `Boundary`;
- edge-finish kinds other than `Chamfer`;
- imported/no-history bodies;
- multiple finishes, holes combined with this first route, inner/open/single-edge/arbitrary selections, nonuniform/asymmetric rules, non-planar or curved support, and arbitrary corner networks.

An input that reaches the bounded AIR route never silently falls back to legacy materialization.

## Validation and evidence

The build report exposes Feature AIR, Construction AIR, authoritative BRepPlan, materialization route, manifold status, expected and actual topology, measured X/Y top inset, STEP SHA-256, and immediate STEP reimport topology/bounds/manifold status.

The canonical rectangle produces 12 vertices, 20 edges, 10 planar faces, 10 loops, and 40 coedges. These are validation consequences of the three-section four-vertex topology plan, not a geometry algorithm. Geometry tests inspect the four reimported vertices at `z = height` and verify their X/Y extrema equal the requested inset.

The fixture is `fixtures/FirmamentV2/Chamfer/valid/air-top-boundary-chamfer.valid.firmament`.

## Remaining limitations and legacy paths

This is not general chamfer support. It does not perform arbitrary BRep edge surgery or recognize imported history. `BrepBoundedChamfer` remains available for unrelated legacy behavior. `AirChamferRealBodyPrototype`, `AirChamferClosedWitnessLab`, and `AirChamferStepArtifactLab.WriteControlledCubeOneEdgeStep` remain lab/negative evidence and are not called by this route.

The next bounded step should add a second history-known construction family or a named face-pair single-edge selection only after giving it its own geometric construction and topology-plan proof; it should not reuse the false-positive cube relabeling chain.
