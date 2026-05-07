# CIR-F15: emitted topology identity map bridge

## Why this metadata exists

CIR-F14 proved dry-run pairing evidence can match boundaries via `InternalTrimIdentityToken`, but emitted patch topology had no deterministic mapping back to those tokens. CIR-F15 adds internal emitted-topology identity metadata so later shell stitching can map emitted edges/coedges/loops back to pairing evidence without introducing public topology naming.

## Model

`EmittedTopologyIdentityMap` and `EmittedTopologyIdentityEntry` are internal/test-facing only. Entries carry:

- stable patch-local key (`edge:<id>`, `coedge:<id>`, `loop:<id>`)
- kind (`Edge`, `Coedge`, `Loop`, `Seam`, `Face`)
- optional `InternalTrimIdentityToken`
- role (`InnerCircularTrim`, `CylindricalTopBoundary`, `CylindricalBottomBoundary`, `CylindricalSeam`, `OuterBoundary`, `Unmapped`)
- orientation policy and diagnostics

## SEM-A0 boundary

This bridge is diagnostic identity only. It does **not** expose Firmament selectors, public topology names, PMI topology references, merge behavior, or STEP behavior changes.

## Behavior

- Planar `EmitRectangleWithInnerCircle` attaches an internal identity token to emitted inner circular edge/coedge/loop metadata when retained loop token evidence exists.
- Planar outer rectangle boundaries are intentionally tagged as unmapped/internal-only.
- Cylindrical retained wall emission tags seam role metadata and attaches top/bottom boundary token metadata when loop evidence can be mapped; missing/ambiguous mappings emit explicit diagnostics.
- Planar patch-set results propagate per-entry emitted identity metadata.
- Shell assembler diagnostics now report whether token-attached emitted candidates are visible and token-match candidates exist, while keeping assembly blocked.

## Remaining work before stitching

- deterministic remap alignment against dry-run `PlannedEdgeUse`/`PlannedCoedgePairing` ordering keys
- tie-break policy when multiple emitted entries could satisfy one token
- actual shell stitch/merge execution (future milestone)

## Recommended next step

CIR-F16 should consume `EmittedTopologyIdentityMap` entries in a bounded stitch candidate resolver that maps dry-run planned pairs to concrete emitted coedge pairs, with deterministic ambiguity rejection diagnostics.
