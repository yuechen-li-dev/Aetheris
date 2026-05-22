# CIR-M3 placement anchor semantics alignment

CIR-M3 aligns CIR placement lowering with production-authored placement anchor semantics for a bounded subset, without introducing generated topology naming.

## What changed

- Added shared authored face-anchor helper: `FirmamentPlacementAnchorSemantics.TryResolveAuthoredFaceAnchorFromBounds`.
- `FirmamentCirLowerer` now resolves supported selector anchors through the shared helper instead of ad hoc inline top-face math.
- Supported CIR-M3 selector anchor subset:
  - `<feature>.top_face`
  - `<feature>.bottom_face`
- `place.offset` remains supported and unchanged.

## Guardrails (SEM-A0)

CIR-M3 explicitly does **not** introduce generated topology identity.

Unsupported selector anchors still fail clearly in CIR lowering, including:

- generated/aggregate topology ports (`edges`, `vertices`, `side_faces`, `circular_edges`)
- side-face axis semantics (`side_face`) in CIR placement lowering
- around-axis placement semantics (`around_axis`, `radial_offset`, `angle_degrees`)

This preserves SEM-A0 constraints: no invented edge/vertex naming and no user-facing topology IDs for incidental boolean topology.

## Differential status

The semantic-placement CIR-vs-BRep fixture from CIR-M2 is retained and tightened with bounds/volume sanity checks while preserving explicit unsupported diagnostics for out-of-scope selector forms.

## Deferred to future SEM milestones

- generated-edge and generated-vertex identity
- stable boolean-result topology identity semantics
- broader selector traversal/chaining semantics
