# CIR-F10.4: bounded annular / trimmed planar loop policy for `PlanarSurfaceMaterializer`

## Investigation summary

Inspected current implementation and topology/export conventions before adding policy:

- `PlanarSurfaceMaterializer` currently emits only untrimmed rectangle and untrimmed circle planar faces.
- Core topology model supports multiple loops per face (`Face.LoopIds`) and STEP exporter maps first loop to `FACE_OUTER_BOUND` and subsequent loops to `FACE_BOUND`.
- Existing `boolean_box_cylinder_hole` and boolean builders already construct planar faces with outer+inner loops for holes.
- However, F10 surface-patch trim descriptors do not currently carry canonical geometric bindings for inner circular loops that can be safely converted into explicit BRep inner loop edges in this scoped materializer path.

Result: policy and diagnostics landed; trimmed inner-circle emission remains explicitly deferred.

## Policy status (CIR-F10.4)

`PlanarSurfaceMaterializer` now exposes explicit loop-emission policy:

- Outer rectangle: supported
- Outer circle: supported
- One inner circle: **deferred**
- Multiple inner loops: **unsupported (explicitly rejected)**
- Inner-loop orientation policy: deferred until canonical trim-loop geometry/topology binding is available

## Behavior

- Readiness gate remains mandatory (`no readiness, no emission`).
- Untrimmed rectangle and untrimmed circle emission paths remain unchanged.
- Trimmed planar patch requests with inner loops are rejected with precise diagnostics:
  - one inner loop: deferred with blocker reason
  - multiple inner loops: rejected as out of CIR-F10.4 scope

No partial topology is emitted for deferred/unsupported trimmed cases.

## SEM-A0 guardrails

Preserved:

- no generated topology naming
- no selector identity expansion
- no STEP production-path behavior change

## Rejected/deferred cases (explicit)

- multiple inner loops
- non-circular inner loops
- nested holes
- arbitrary trimmed planar faces

## Next milestone

Add canonical retained-loop geometric binding for inner circular trims (center/normal/radius + edge-use orientation contract) so `PlanarSurfaceMaterializer` can safely emit one outer + one inner loop annular planar topology in this path.
