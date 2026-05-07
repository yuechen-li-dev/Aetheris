# CIR-F10.1: bounded planar geometry for real `SourceSurfaceDescriptor` box faces

## Why F10 blocked

CIR-F10 bridged real extracted descriptors into `PlanarPatchPayloadBuilder`, but box planar faces were emitted as role tokens (`top`, `bottom`, etc.) without bounded geometry. Role identity is insufficient to reconstruct deterministic rectangular corners.

## New bounded planar geometry contract

`SourceSurfaceDescriptor` now carries optional `BoundedPlanarPatchGeometry`:

- `Corner00`
- `Corner10`
- `Corner11`
- `Corner01`
- `Normal`

Corner ordering is deterministic and explicitly defines the rectangle perimeter for `rect3d:` derivation.

## Supported scope

Implemented only for planar faces extracted from `CirBoxNode`.

- all six box faces now include bounded planar geometry,
- corners are transformed to world-space during extraction,
- normals are computed from transformed corner vectors.

No generalized planar inference was added for non-box sources.

## Transform behavior

CIR-F10.1 applies the current `Transform3D` directly to box face corners and derives normals from transformed vectors. This supports translation and other affine transforms already represented by `Transform3D`; no new transform policy gates were introduced in this milestone.

## Payload derivation flow

`PlanarPatchPayloadBuilder.TryBuildRectanglePayload` now follows:

1. keep existing fast-path for existing `rect3d:` payload;
2. otherwise, if bounded planar geometry is present, derive `rect3d:` from corners;
3. otherwise keep existing rejection for missing bounded rectangle geometry.

## Non-goals preserved

- no full box materialization,
- no multi-face shell assembly path changes,
- no trimmed planar patch support,
- no cylindrical/spherical/toroidal emission changes,
- no STEP export behavior changes,
- no Boolean behavior expansion,
- no generated topology naming.

## Recommended next step

Use the same bounded-source evidence pattern for other truly bounded planar primitives (for example cylinder caps where bounded curves are explicitly represented), while retaining explicit rejection for sources lacking trustworthy bounds.
