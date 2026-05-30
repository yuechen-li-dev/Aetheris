# EDGE-LOOP-X1 — Top-face outer-loop chamfer through prismatic transition lab

## 1. Purpose and scope

EDGE-LOOP-X1 is a lab-only / production-adjacent proof for the first Class B face-boundary loop chamfer. It proves a **uniform symmetric chamfer around the top face outer loop of a rectangular prism** by lowering the request through the existing prismatic section-transition lane.

The proof is intentionally narrow:

- rectangular prism source;
- top planar cap face only;
- outer loop only;
- closed ordered loop with four coedges;
- uniform symmetric chamfer distance;
- history-known construction;
- all four top outer-loop edges chamfered together;
- planar split-preserving BRep emission through `PrismaticSectionTransitionEmitter`.

This is not a production route and does not replace any existing chamfer or fillet behavior.

## 2. Relationship to EDGE-LOOP-A0

EDGE-LOOP-A0 identified face-boundary loops as Class B selections: ordered, face-owned loops rather than arbitrary edge graphs. Its recommended first implementation target was a top-face outer-loop chamfer of a rectangular prism through a prismatic section transition. EDGE-LOOP-X1 implements that exact first-scope lab and keeps the rest of the EDGE-LOOP-A0 taxonomy deferred.

## 3. Class B loop selection model

The admitted selection is machine-checkable:

- owning face: top cap;
- loop kind: outer loop;
- loop closed: true;
- ordered coedge/edge count: 4;
- ordered corners inherited from the rectangular top-cap boundary;
- rule: uniform symmetric chamfer.

The emitted diagnostics include loop-selection evidence such as `edge-loop-x1-loop-selection-created`, `edge-loop-x1-owning-face-top-cap`, `edge-loop-x1-loop-kind-outer`, `edge-loop-x1-loop-closed`, `edge-loop-x1-loop-edge-count:4`, and `edge-loop-x1-uniform-chamfer-rule-validated`.

## 4. Candidate geometry and section stack

The canonical geometry is:

- width = 10;
- depth = 8;
- height = 6;
- chamferDistance = 1.

The lab builds a three-section prismatic stack:

| Section | Z | Profile |
|---|---:|---|
| `z0` | 0 | full rectangle `(-5,-4)`, `(5,-4)`, `(5,4)`, `(-5,4)` |
| `z1` | 5 | full rectangle `(-5,-4)`, `(5,-4)`, `(5,4)`, `(-5,4)` |
| `z2` | 6 | inset rectangle `(-4,-3)`, `(4,-3)`, `(4,3)`, `(-4,3)` |

The correspondence is explicit identity correspondence by vertex and edge index. Each top loop edge contributes to one chamfer transition face, so the chamfer transition face count is four.

## 5. Prismatic emitter route

`PrismaticTopFaceLoopChamferPrototype` validates the loop request, constructs the section stack, creates identity correspondence, and invokes `PrismaticSectionTransitionEmitter`. The candidate path is constructive: it emits the body from sections rather than mutating an existing BRep topology.

The lab wrapper `TopFaceLoopChamferPrismaticLab` exposes deterministic rows for canonical, larger-distance, non-square, invalid, and deferred cases.

## 6. Topology findings

For the canonical three-section rectangular stack with split-preserving topology, the emitted body matches the expected count family:

| Metric | Value |
|---|---:|
| section count | 3 |
| vertices | 12 |
| edges | 20 |
| faces | 10 |
| planar faces | 10 |
| cylindrical faces | 0 |
| cap faces | 2 |
| lower prism side faces | 4 |
| transition faces | 4 |
| chamfer transition faces | 4 |
| loops | 10 |
| coedges | 40 |
| bounds | `[-5,-4,0]..[5,4,6]` |

The topology has the same broad count family as the single top-edge prismatic route, but the role classification differs: all four upper transition faces are chamfer faces instead of only one.

## 7. STEP smoke findings

The STEP smoke is performed through the existing exporter on the emitted body. The canonical row confirms these required markers are present:

- `ISO-10303-21`;
- `MANIFOLD_SOLID_BREP`;
- `ADVANCED_FACE`;
- `PLANE`.

It also confirms these markers are absent:

- `CYLINDRICAL_SURFACE`;
- `BREP_WITH_VOIDS`.

## 8. Invalid, rejected, and deferred cases

The lab rejects or defers unsupported requests before invoking the prismatic emitter:

- non-positive or non-finite width/depth/height: `edge-loop-x1-invalid-dimensions-rejected`;
- non-positive or non-finite chamfer distance: `edge-loop-x1-invalid-chamfer-distance-rejected`;
- chamfer too large for width/depth/height bounds: `edge-loop-x1-chamfer-distance-too-large-rejected`;
- non-closed loop: `edge-loop-x1-non-closed-loop-rejected`;
- inner loop: `edge-loop-x1-non-outer-loop-deferred`;
- open chain: `edge-loop-x1-open-chain-deferred`;
- arbitrary graph: `edge-loop-x1-arbitrary-graph-rejected`;
- non-uniform rule: `edge-loop-x1-non-uniform-rule-rejected`;
- non-planar owning face: `edge-loop-x1-non-planar-owning-face-deferred`.

Recommendations remain inside the allowed vocabulary: `face-loop-chamfer-ready-for-corpus`, `face-loop-chamfer-needs-loop-selection-hardening`, `face-loop-chamfer-needs-corner-policy-hardening`, `face-loop-chamfer-invalid-rejected`, and `face-loop-chamfer-deferred`.

## 9. Route exclusion guarantees

The candidate diagnostics explicitly confirm:

- no AirEdgeSweep;
- no BrepBoundedChamfer;
- no topology graft;
- no 3D Boolean;
- no coplanar merge;
- no production route replacement.

There are also no STEP importer/exporter changes, Boolean core changes, BRep topology behavior changes outside the prototype/lab, AIR emitter behavior changes, CIR analyzer behavior changes, triangle migration retry, arbitrary graph support, or NURBS/freeform expansion.

## 10. One loop operation, not four Class A chamfers

EDGE-LOOP-X1 admits and emits the top rim as one Class B loop operation. The loop selection is created once, validated once, and lowered to one section stack with identity correspondence across the full rectangular profile.

This matters because four unrelated Class A single-edge chamfers would not prove loop ownership, closed-loop ordering, all-or-nothing validation, corner consistency, or whole-loop route diagnostics. EDGE-LOOP-X1 records `edge-loop-x1-class-b-loop-route` and `edge-loop-x1-not-four-independent-single-edge-chamfers` to make that distinction machine-checkable.

## 11. Relationship to the prismatic top-edge single-edge route

The existing top-edge route proves that a single history-known top rim edge can be represented by a three-section stack with a stable lower prism interval and a chamfer transition interval. EDGE-LOOP-X1 uses the same prismatic section-transition building block, but changes the top profile from a one-sided inset to an all-sides inset. That converts one changed top edge into four changed top-loop edges while preserving the section split at `z = height - chamferDistance`.

## 12. Non-goals

EDGE-LOOP-X1 does not provide:

- production route admission;
- default production behavior changes;
- inner-loop support;
- open-chain support;
- side-face loop support;
- no-history/imported loop support;
- fillet loop support;
- mixed or variable rule support;
- unequal-distance chamfer support;
- arbitrary edge graph support;
- STEP importer/exporter changes;
- Boolean core changes;
- topology graft or mutation behavior;
- AIR emitter changes;
- CIR analyzer authority changes.

## 13. Tests run

The focused lab tests are in `Aetheris.FrictionLab.Tests/CIRLab/TopFaceLoopChamferPrismaticLabTests.cs`. They cover:

- canonical top-face outer-loop chamfer;
- larger valid chamfer distance;
- non-square valid prism;
- deterministic row summaries and diagnostics;
- topology counts;
- STEP smoke markers;
- route-exclusion diagnostics;
- invalid, rejected, and deferred cases.

The implementation validation also reruns the requested prismatic, AirChamfer, chamfer/fillet, and CIR lab filters.

## 14. Recommended next milestones

1. **EDGE-LOOP-X2 loop chamfer artifact/corpus** — add deterministic STEP/JSON artifact rows for the admitted top-face loop chamfer.
2. **EDGE-LOOP-X3 no-history/imported loop rejection diagnostics** — prove imported/no-history loop requests fail deterministically without implying local topology mutation support.
3. **EDGE-FILLET-A0 fillet architecture audit using selection taxonomy** — apply the Class A/B/C/D taxonomy to fillet routes before implementing loop fillets.
