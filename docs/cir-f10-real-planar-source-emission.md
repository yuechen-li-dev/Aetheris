# CIR-F10: real dry-run planar evidence integration (bounded scaffold)

## Outcome

**Outcome B — meaningful progression.**

The F8/F8.10 real source-surface pipeline is now wired to a bounded planar payload derivation helper, but current `SourceSurfaceDescriptor` content does not include explicit rectangle corner/dimension geometry for box faces (it carries role tokens such as `top`, `bottom`, etc.).

Because of the readiness rule (**no readiness, no emission**) and anti-fabrication requirement, CIR-F10 does **not** synthesize rectangle geometry from incomplete descriptor payload.

## What was integrated

- Added `PlanarPatchPayloadBuilder.TryBuildRectanglePayload(SourceSurfaceDescriptor, out string? payload, out string diagnostic)`.
- Builder accepts existing bounded payload (`rect3d:...`) and returns success.
- Builder rejects real F8 extractor role payloads (`top`, `bottom`, etc.) with a precise blocker diagnostic.
- Added focused tests against real `SourceSurfaceExtractor.Extract(new CirBoxNode(...))` descriptors and readiness-gated emission refusal.

## Supported behavior in this milestone

- Synthetic F9 `rect3d:` path remains supported and unchanged.
- Real extracted planar descriptors can now be evaluated by an explicit conversion helper, with clear rejection diagnostics when rectangle geometry is missing.

## Rejection behavior

The helper/materializer now distinguishes:

- non-planar source family,
- missing payload reference,
- planar payload that is not bounded rectangle geometry (`rect3d:`),
- readiness gate rejection (`NotApplicable`/`Deferred`/`Unsupported`).

## Why this is not full box/shell materialization

CIR-F10 intentionally remains bounded:

- no full box face-set assembly,
- no shell stitching,
- no trim-loop solving,
- no non-planar family emission,
- no STEP/export changes,
- no generated topology naming.

## Exact blocker for Outcome A

To emit from a real extracted box face without synthetic payloads, `SourceSurfaceDescriptor` (or adjacent dry-run evidence object) needs exact bounded planar geometry for at least one untrimmed face, e.g. one of:

- four world-space face corners, or
- local rectangle extents + frame axes + transform.

Current descriptor payload role strings (`top`, `bottom`, `left`, ...) are insufficient to derive exact bounded corners safely.

## Recommended next step

Extend the source-surface evidence contract for box planar faces with deterministic bounded geometry fields (corners or equivalent local frame/extents) so the payload builder can produce `rect3d:` without inference/fabrication.

- CIR-F10.1 follow-up: box face descriptors now include bounded planar corner geometry so real-source planar payload derivation can produce rect3d without relying on role tokens.
