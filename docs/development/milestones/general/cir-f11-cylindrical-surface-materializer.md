# CIR-F11: first bounded CylindricalSurfaceMaterializer emission

## Scope

This milestone adds the first bounded cylindrical surface-family emission path: a **single retained cylindrical wall patch** for canonical `Subtract(Box,Cylinder)` evidence.

Implemented shape:
- full angular span (0..2π),
- finite axial span between two planar cuts,
- one cylindrical face with one loop,
- loop uses the same seam convention as `BrepPrimitives.CreateCylinder` side face (single seam edge used forward/reversed + top/bottom circular edges).

Not implemented:
- full `box - cylinder` shell/solid assembly,
- STEP/export behavior changes,
- arbitrary cylindrical trim spans,
- non-canonical transforms (non-translation-safe axis conventions),
- non-cylindrical surface families.

## Readiness and evidence gate

`CylindricalSurfaceMaterializer.EmitRetainedWall` is strict:
- rejects readiness `NotApplicable/Deferred/Unsupported` (`no readiness, no emission`),
- requires `FacePatchRetentionRole.ToolBoundaryRetainedInsideBase`,
- requires cylindrical source family,
- requires canonical `CylindricalSurfaceGeometryEvidence` with finite radius/height,
- requires circular `MouthTrim` loop evidence and non-deferred loop readiness.

Missing/deferred evidence returns explicit diagnostics.

## Topology convention

Emission follows cylinder primitive side-face boundary convention:
- one side face,
- one loop,
- seam edge used twice (forward + reversed),
- one top circular boundary edge and one bottom circular boundary edge,
- cylindrical surface binding on the emitted face,
- no cap faces emitted.

## Why this is not full materialization

CIR-F11 is intentionally bounded to first cylindrical wall patch emission only. It does not attempt shell stitching, pairwise adjacency assembly, or boolean replacement.

## Remaining blocker before full shell assembly

To produce full `Subtract(Box,Cylinder)` BRep materialization through this path, the next step is shell-level integration that composes:
- emitted retained planar patches,
- emitted retained cylindrical wall patch,
- coedge pairing/closure evidence from the dry-run pipeline,
- robust orientation/adjacency stitching into one shell/body.
