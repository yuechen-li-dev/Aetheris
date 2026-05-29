# AIR-CIR-X1 — AIR/CIR mirror metadata prototype

Status: **Internal/test-visible metadata prototype**. No production analyzer, CLI, STEP, Boolean, BRep topology, AIR emitter, CIR node-kind, prismatic mirror, or CIR-to-BRep extraction behavior changes.

## 1. Purpose and scope

AIR-CIR-X1 adds the small metadata seam that future mirror-aware analyzer dispatch needs before it can safely choose CIR/FRep. The seam answers:

- whether a CIR mirror is admitted;
- which analyzer-style uses the mirror may answer;
- which topology/identity facts are known to be lost;
- where the mirror claim came from;
- why a mirror is unavailable, rejected, stale, or intentionally lossy.

This is metadata-first. X1 does not create new CIR fields, lower prismatic AIR to CIR, or change current analyzers.

## 2. References

X1 follows the authority contract in [AIR-CIR-A0](air-cir-a0-authority-and-mirror-contract.md): Firmament owns user/feature intent, AIR owns constructive topology intent, BRep owns materialized topology and STEP/export, and CIR/FRep owns field/evaluation answers only after a mirror is admitted.

X1 also follows [CIR-MAP-X1](cir-map-x1-primitive-map-prototype.md), which proved lab-only CIR/tape map parity for primitive box, cylinder, and sphere mirrors while leaving prismatic bodies `mirror-unavailable`.

## 3. Type/API shape

The internal/test-visible implementation lives in `Aetheris.Kernel.Core.Cir.Mirrors` and is intentionally not wired into production analyzers.

- `CirMirrorStatus` is the stable status vocabulary.
- `CirMirrorCapability` describes admitted uses: point containment, approximate volume, map occupancy, section sampling, face identity, and topology parity.
- `CirMirrorLossFlags` records known losses: face identity, loop identity, split-face lineage, feature-role labels, boundary precision, and exact topology.
- `CirMirrorProvenance` records source representation kind, source route, optional source label, mirror/emitter version, optional topology summary, optional tolerance context, and diagnostics.
- `CirMirrorAdmission` is the request: source representation kind, route, atom kind, requested capabilities, optional topology summaries, tolerance context, and diagnostics label.
- `CirMirrorAdmissionResult` returns status, allowed capabilities, known losses, provenance, deterministic diagnostics, and a descriptor.
- `CirMirrorAdmissionService` is the small internal helper that evaluates X1 admission requests.

## 4. Stable statuses

The status strings are stable and machine-checkable:

- `mirror-unavailable`
- `mirror-admitted-exact`
- `mirror-admitted-conservative`
- `mirror-admitted-approximate`
- `mirror-rejected-unsupported-atom`
- `mirror-rejected-lossy-for-request`
- `mirror-rejected-stale-or-mismatched`

## 5. Supported admitted primitives

X1 admits only the primitive mirrors already proven by CIR-MAP-X1:

| Source route | Atom kind | Status | Allowed uses |
| --- | --- | --- | --- |
| `BrepPrimitives.CreateBox` | `BoxPrimitive` | `mirror-admitted-exact` | point containment, approximate volume, map occupancy, section sampling |
| `BrepPrimitives.CreateCylinder` | `CylinderPrimitive` | `mirror-admitted-exact` | point containment, approximate volume, map occupancy, section sampling |
| `BrepPrimitives.CreateSphere` | `SpherePrimitive` | `mirror-admitted-exact` | point containment, approximate volume, map occupancy, section sampling |

These primitive mirrors still do **not** support face identity or topology parity. Their known losses include face identity, loop identity, split-face lineage, feature-role labels, boundary precision limits, and exact topology unavailable.

## 6. Explicit unavailable/rejected examples

- `PrismaticSectionTransitionEmitter` is rejected as `mirror-rejected-unsupported-atom` and also reports mirror-unavailable diagnostics. X1 creates no prismatic mirror.
- `ProfileVertexChamferExtrudeEmitter` remains `mirror-unavailable` / deferred. X1 must not silently claim that profile-authored vertical chamfer routes have a profile-prism CIR mirror.
- A primitive request requiring face identity or topology parity is `mirror-rejected-lossy-for-request` because the admitted primitive CIR field cannot answer explicit topology identity questions.
- A request with mismatched expected/actual topology summaries is `mirror-rejected-stale-or-mismatched`.

## 7. Diagnostics contract

Diagnostics are deterministic strings intended for tests and future dispatch plumbing. Current X1 diagnostics include:

- `air-cir-x1-mirror-admission-started`
- `air-cir-x1-mirror-admitted-exact:<source>`
- `air-cir-x1-mirror-unavailable:<source>`
- `air-cir-x1-mirror-rejected-unsupported-atom:<source>`
- `air-cir-x1-mirror-rejected-lossy-for-request:<request>`
- `air-cir-x1-mirror-rejected-stale-or-mismatched:<source>`
- `air-cir-x1-capability-map-occupancy`
- `air-cir-x1-capability-point-containment`
- `air-cir-x1-capability-approximate-volume`
- `air-cir-x1-loss-face-identity`
- `air-cir-x1-loss-topology-parity`
- `air-cir-x1-no-production-analyzer-behavior-changed`
- `air-cir-x1-no-prismatic-mirror-created`
- `air-cir-x1-no-cir-to-brep-extraction`

## 8. Relationship to analyzers

Future map or section dispatch can consume this seam before selecting a CIR backend. The intended future shape is:

1. ask for mirror admission with the requested analyzer use;
2. accept only statuses/capabilities that cover the request;
3. reject field mirrors for face identity/topology parity outputs;
4. preserve deterministic diagnostics when no mirror is admitted.

No current `analyze map`, `analyze section`, `StepAnalyzer`, CLI default behavior, or production analyzer behavior changes in X1.

## 9. Relationship to prismatic

Prismatic mirrors remain unavailable. X1 only makes that status explicit and testable for prismatic section transitions and profile-authored chamfers. Exact prismatic mirror support still needs a future CIR-PRISMATIC-X1 feasibility milestone, likely involving a first-class convex/polyhedral or section-stack field model rather than metadata-only admission.

## 10. Non-goals

- no new CIR nodes;
- no prismatic mirror;
- no analyzer behavior change;
- no CLI default behavior change;
- no STEP exporter/importer change;
- no Boolean core change;
- no BRep topology change;
- no AIR emitter behavior change;
- no CIR-to-BRep extraction;
- no production map support claim for prismatic BReps.

## 11. Tests run

X1 adds focused `CirMirrorAdmissionTests` for primitive exact admission, prismatic unsupported atom rejection, profile-authored chamfer unavailability, lossy face/topology requests, stale/mismatch rejection, and deterministic repeated diagnostics.

Required validation run for this milestone should include the AIR-CIR/CIR-map focused kernel filter plus CLI, friction-lab, and Firmament filters listed in the implementation task. Gated corpus stability tests are not required by default.

## 12. Recommended next milestone

Recommended next work is one of:

1. **CIR-MAP-X2** — mirror-aware primitive map dispatch prototype that consumes X1 metadata while preserving current CLI defaults;
2. **CIR-PRISMATIC-X1** — prismatic mirror feasibility for exact section-stack/convex polyhedral fields;
3. **EDGE-PRISMATIC-X8** — hybrid map dispatch only after mirror metadata and actual mirror availability exist.

## 13. CIR-MAP-X2 consumption note

CIR-MAP-X2 is the first lab/test-only consumer of `CirMirrorAdmissionService` for map backend selection. The X2 dispatcher asks admission for the requested analyzer use, selects the CIR tape primitive map backend only when a box/cylinder/sphere mirror is `mirror-admitted-exact` for `MapOccupancy`, and rejects face identity/topology parity as lossy. Prismatic section transitions and profile-authored chamfers remain mirror-unavailable or unsupported; no production analyzer, CLI, STEP, Boolean, BRep topology, AIR emitter, CIR node kind, prismatic mirror, or CIR-to-BRep behavior changes are introduced.

## CIR-PRISMATIC-X1 prismatic feasibility note

CIR-PRISMATIC-X1 evaluates a bounded prismatic mirror in the friction-lab layer only. For `rectangle-inset` and `top-edge-chamfer`, the lab can produce `mirror-admitted-exact` metadata for point containment and map occupancy using AIR-authored section data, while still rejecting face identity and topology parity as lossy.

The AIR-CIR-X1 production admission service remains intentionally conservative: prismatic mirrors are unavailable to production dispatch unless a later X2-style milestone promotes a bounded half-space/convex-polyhedron or section-stack evaluator into an explicitly admitted mirror path.
