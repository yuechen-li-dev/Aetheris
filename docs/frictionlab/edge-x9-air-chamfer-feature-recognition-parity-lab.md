# EDGE-X9 — AirChamfer candidate body feature-recognition parity lab

## 1) Purpose and scope
EDGE-X9 adds a **lab-only** parity probe for the EDGE-V2 `AirChamferRealBodyPrototype` candidate body to validate downstream-recognition-readiness in controlled cases before any shadow-route experiment.

## 2) References
- `docs/aetheris-v2-sweep-first-architecture.md`
- `docs/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`
- `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- EDGE-X3..X8 + EDGE-V1/V2 labs and prototype docs.

## 3) Why this follows candidate body + STEP smoke
V2-A3 showed geometry/STEP parity alone is insufficient; bounded-recognition consumers depend on local adjacency/corner topology semantics.

## 4) Controlled cases
- canonical orthogonal EDGE-V2 candidate
- safe non-orthogonal EDGE-V2 candidate
- invalid/deferred request
- legacy-dependent triangle/chamfer fixture (explicitly non-compared)

## 5) Candidate recognition contract
Expected minimum signals for candidate readiness:
- one planar chamfer face
- two trimmed adjacent planar faces
- two transition edges
- no cylindrical faces
- closed manifold body signal
- original sharp edge replaced
- chamfer local adjacency surrogate satisfied

## 6) Recognition / adjacency ledger
Captured ledger includes body counts, planar/cylindrical breakdown, manifold/STEP smoke flags, chamfer/trim/transition counts, surrogate adjacency incidence, recognized/admissible candidate counts, first divergence, and deterministic diagnostics.

## 7) Legacy comparison status
For EDGE-X9 controlled synthetic cases, legacy comparison is currently marked unavailable using deterministic diagnostic:
- `edge-x9-legacy-comparison-unavailable:controlled-case-not-comparable`

## 8) Per-case results
| case | candidate produced | recognition contract | legacy comparison | first divergence | recommendation |
|---|---:|---:|---|---|---|
| canonical-orthogonal-edge-v2-candidate | yes | pass | unavailable | - | air-chamfer-candidate-ready-for-shadow-route-probe |
| safe-nonorthogonal-edge-v2-candidate | yes | pass | unavailable | - | air-chamfer-candidate-ready-for-shadow-route-probe |
| invalid-distance-deferred | no | fail | unavailable | prototype-status-Deferred | air-chamfer-candidate-keep-legacy-authority |
| legacy-triangle-dependent-fixture | no | fail | unavailable | prototype-status-FallbackLegacy | air-chamfer-candidate-keep-legacy-authority |

## 9) Topology / adjacency findings
Controlled successful candidate body exposes the expected 6/12/8 topology with planar-only faces and expected chamfer transition contract from EDGE-V2 summary plus deterministic local adjacency surrogate checks.

## 10) STEP smoke reference
EDGE-X9 consumes EDGE-V2 step smoke status and emits `edge-x9-step-smoke-succeeded` on successful controlled candidate runs.

## 11) Invalid/deferred cases
Invalid/deferred fixtures stop before recognition parity and emit first-divergence diagnostics tied to prototype status.

## 12) Legacy authority / no route replacement
Legacy `BrepBoundedChamfer` remains authoritative; no production route replacement.

## 13) No-3D-Boolean guarantee
Probe path emits `edge-x9-no-3d-boolean-used`; no boolean-core behavior change.

## 14) Non-goals
No production behavior change, no production replacement, no fillet work, no chains/corners expansion, no STEP/Boolean changes.

## 15) Recommended next milestone
- If parity remains stable: EDGE-V3 non-authoritative shadow-route probe behind controlled seam.
- If mismatch appears: EDGE-X9.1 adjacency/recognizer hardening with first-divergence driven scope.

## 16) EDGE-V3 consumer note
EDGE-V3 consumes the parity probe through `AirChamferFeatureRecognitionParityLab.EvaluateCandidateEvidence(...)` so the non-authoritative `AirChamferShadowRoute` can reuse EDGE-X9 recognition-summary and first-divergence logic after invoking `AirChamferRealBodyPrototype`, without replacing production chamfer behavior.
