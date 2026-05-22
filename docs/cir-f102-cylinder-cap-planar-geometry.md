# CIR-F10.2: bounded planar geometry for cylinder cap source surfaces

## Purpose

CIR-F10.1 added bounded planar **rectangular** geometry for `CirBoxNode` planar descriptors so real extracted box faces can deterministically derive `rect3d:` payloads.

CIR-F10.2 extends bounded planar **source evidence** for `CirCylinderNode` planar cap descriptors to include deterministic **circular** bounded geometry.

This milestone is evidence-enrichment only. It does not emit circular cap topology.

## Geometry model update

`BoundedPlanarPatchGeometry` is now explicit about shape kind:

- `Rectangle`
  - corners (`Corner00`, `Corner10`, `Corner11`, `Corner01`)
  - normal
- `Circle`
  - center (`Center`)
  - normal
  - radius (`Radius`)

Factory helpers (`CreateRectangle`, `CreateCircle`) keep construction deterministic and explicit.

## Cylinder cap extraction behavior

For `CirCylinderNode`, extraction now produces:

- 1 cylindrical side descriptor (unchanged),
- 2 planar cap descriptors (`cap-top`, `cap-bottom`) with circular bounded geometry when representable.

Per cap:

- center is computed in world-space,
- normal is transformed to world-space,
- radius is transformed from radial basis vectors,
- role and orientation remain stable (`cap-top` forward, `cap-bottom` reversed).

## Transform policy in CIR-F10.2

Supported:

- identity,
- translation,
- transforms that keep radial x/y magnitudes equal after transform (still circular under current model).

Deferred with diagnostic:

- transforms that produce non-uniform radial magnitudes (`radiusX != radiusY`) indicating non-circular cap bounds under current circle-only bounded model.

Diagnostic code:

- `cylinder-cap-circular-geometry-deferred`

## Payload builder behavior

`PlanarPatchPayloadBuilder.TryBuildRectanglePayload` remains rectangle-only and now explicitly rejects circular bounded planar geometry with a clear deferred diagnostic.

No circular cap is forced into `rect3d:`.

## Non-goals preserved

- no circular cap BRep loop/face emission,
- no cylinder side emission changes,
- no shell assembly changes,
- no STEP export or Boolean behavior changes,
- no public CLI surface change,
- no generated topology naming (SEM-A0 guardrails preserved).

## Next milestone

Add a circle-cap planar emission path in `PlanarSurfaceMaterializer` (or a closely scoped successor) that consumes circular bounded planar geometry directly rather than coercing to `rect3d:`.
