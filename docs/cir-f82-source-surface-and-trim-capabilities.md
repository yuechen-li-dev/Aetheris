# CIR-F8.2 — source-surface extraction + trim capability matrix

## Purpose

CIR-F8.2 adds two substrate services without changing production materialization behavior:

1. `TrimCapabilityMatrix`: centralized source-surface-family pair policy for trim curve support.
2. `SourceSurfaceExtractor`: inventory extraction of `SourceSurfaceDescriptor` values from CIR trees.

This keeps architecture moving from pair-specific materializers toward descriptor-first surface-family materialization.

## Capability semantics

Each pair `(SurfacePatchFamily A, SurfacePatchFamily B)` resolves to:

- classification: exact-supported / special-case-only / deferred / unsupported
- candidate trim families (line/circle/ellipse/polyline/bspline/algebraic/unsupported)
- whether orientation/placement restrictions apply
- explicit reason text

Current notable entries:

- Planar×Planar → exact, line
- Planar×Cylindrical → special-case-only, line/circle/ellipse
- Planar×Spherical → exact, circle
- Planar×Prismatic → exact, line/polyline
- Planar×Conical → deferred
- Planar×Toroidal → deferred
- Cylindrical×Cylindrical → deferred
- Spherical×Cylindrical → deferred
- Toroidal×* → deferred

## Source-surface extraction behavior

`SourceSurfaceExtractor.Extract(CirNode, NativeGeometryReplayLog?)` traverses CIR including booleans and transform nodes.

- Box: 6 planar descriptors (`top/bottom/left/right/front/back`)
- Cylinder: 1 cylindrical side + 2 planar caps
- Sphere: 1 spherical descriptor
- Torus: 1 toroidal descriptor + deferred-materialization diagnostic
- Booleans: both operands are inventoried; retained/discarded classification is intentionally deferred
- Transform nodes: transforms are composed and preserved on descriptors

Replay/provenance facts are attached opportunistically using latest replay operation when available (op index, placement-kind hint in provenance string).

## Intentionally not implemented

- No BRep face/edge emission
- No trim-curve computation
- No retained-face classification
- No generated topology naming
- No change to existing pair-specific rematerializers or STEP behavior

## CIR-F8.3 recommendation

Use extracted source surfaces + matrix lookup to produce first `FacePatchDescriptor` dry-run candidate sets for subtract(box,cylinder) and subtract(box,sphere), with explicit retained-surface diagnostics before any topology assembly.
