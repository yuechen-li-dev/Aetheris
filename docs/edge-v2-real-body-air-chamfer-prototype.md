# EDGE-V2 Real-body AirChamfer prototype

## Purpose and scope
EDGE-V2 packages EDGE-X8 controlled local topology graft into an **internal production-adjacent seam**: `AirChamferRealBodyPrototype`.
It is non-authoritative and preserves legacy `BrepBoundedChamfer` production authority.

References: EDGE-A0, EDGE-X3, EDGE-X4, EDGE-X5, EDGE-X6, EDGE-V1, EDGE-X7, EDGE-X8.

## Internal API shape
- Request: controlled source body, explicit target edge endpoints, explicit adjacent face normals, distance, convexity expectation, chain/legacy flags, optional STEP smoke.
- Result: status (`Succeeded|Rejected|Deferred|Failed|FallbackLegacy`), decision, judgment score, topology plan, geometry artifact, candidate body, topology summary, STEP smoke summary, deterministic diagnostics.

## Pipeline
1. controlled body/edge/face admission
2. invoke EDGE-V1 `AirChamferConvexPlanarPrototype`
3. topology plan extraction
4. geometry artifact extraction
5. controlled topology graft packaging
6. candidate body emission
7. topology contract validation
8. STEP smoke validation

## Candidate topology contract
- faceCount=6
- planarFaceCount=6
- edgeCount=12
- vertexCount=8
- chamferFaceCount=1
- trimmedAdjacentFaceCount=2
- transitionEdgeCount=2

## Diagnostics contract
Includes deterministic EDGE-V2 diagnostics such as:
- `edge-v2-real-body-prototype-started`
- `edge-v2-edge-v1-prototype-invoked`
- `edge-v2-judgment-engine-used`
- `edge-v2-topology-plan-created`
- `edge-v2-geometry-artifact-created`
- `edge-v2-topology-graft-applied`
- `edge-v2-candidate-body-created`
- `edge-v2-candidate-body-topology-validated`
- `edge-v2-step-smoke-succeeded`
- `edge-v2-request-rejected:<reason>` / `edge-v2-request-deferred:<reason>`
- `edge-v2-legacy-authority-preserved`
- `edge-v2-no-production-route-replacement`
- `edge-v2-no-3d-boolean-used`

## Accepted / deferred / rejected
Accepted: controlled convex planar single-edge canonical and safe non-orthogonal.
Deferred/rejected: invalid distance/edge/adjacency, non-planar marker, edge chain, corner chain, legacy-dependent triangle path.

## Guarantees
- production chamfer routes unchanged
- no public API changes
- no STEP exporter/importer changes
- no Boolean-core changes
- no fillet geometry or chain/corner implementation changes
- no 3D Boolean in candidate path

## Tests run
- FrictionLab focused AirChamfer test filter
- Kernel Firmament Chamfer/Fillet regression filter
- Kernel Core Judgment/Chamfer/Boolean regression filter

## Limitations and next milestone
Still controlled-only (single explicit convex planar edge).
Recommended next milestone: EDGE-X9 feature-recognition parity probe, or EDGE-V3 shadow route behind controlled seam.

## EDGE-X9 follow-on note
EDGE-X9 adds a lab-only feature-recognition parity probe (`AirChamferFeatureRecognitionParityLab`) for EDGE-V2 candidate-body readiness, including adjacency-contract checks and deterministic legacy-comparison boundary diagnostics, while preserving legacy authority and no production-route replacement.

## EDGE-V3 shadow-route wrapper note
EDGE-V3 wraps `AirChamferRealBodyPrototype` in `AirChamferShadowRoute`, a non-authoritative internal/test-only seam that invokes the EDGE-V2 candidate body path, captures topology/STEP/recognition evidence, and reports readiness while keeping legacy `BrepBoundedChamfer` authoritative and production output unchanged.
