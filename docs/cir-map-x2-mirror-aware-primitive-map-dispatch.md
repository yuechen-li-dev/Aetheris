# CIR-MAP-X2 — Mirror-aware primitive map dispatch prototype

Status: **Lab/test-only prototype** (no production analyzer or CLI behavior change).

## 1. Purpose and scope

CIR-MAP-X2 proves that AIR/CIR mirror admission metadata can drive a bounded map-backend selection decision without changing production `analyze map` behavior. The milestone adds a focused test/lab dispatcher named `CirMapDispatchPrototype` in `Aetheris.Kernel.Core.Tests` that asks `CirMirrorAdmissionService` whether a requested analyzer use is allowed, then selects the CIR tape map prototype only for exact primitive mirrors and map-occupancy requests.

The prototype remains intentionally non-production. It does not route `StepAnalyzer`, `aetheris analyze map`, imported STEP bodies, production BRep map code, Boolean code, topology code, AIR emitters, or exporters through the dispatcher.

## 2. References

CIR-MAP-X2 builds directly on:

- `docs/air-cir-a0-authority-and-mirror-contract.md` for the AIR/CIR/BRep authority boundary and topology-loss guardrails;
- `docs/air-cir-x1-mirror-metadata-prototype.md` for `CirMirrorAdmissionService`, `CirMirrorStatus`, `CirMirrorCapability`, and primitive/prismatic admission vocabulary;
- `docs/cir-map-x1-primitive-map-prototype.md` for the CIR tape-backed primitive map evaluator and BRep raycast summary baseline;
- `docs/cir-m0-design.md` and `docs/cir-e0-evaluation-runtime.md` for CIR node/tape evaluation context.

## 3. Dispatcher/API shape

The lab dispatcher types are internal to the test assembly:

- `CirMapDispatchPrototype` — policy entry point;
- `CirMapDispatchResult` — selected backend, mirror admission result, optional map result, optional BRep comparison, diagnostics, and recommendation;
- `CirMapBackendCandidate` — candidate backend/admissibility/reason record;
- `CirMapBackendKind` — `CirTape`, `CirNode`, `BrepRaycastBaseline`, or `Unsupported`;
- `CirMapAnalyzerUse` — requested analyzer use such as `MapOccupancy`, `FaceIdentity`, `TopologyParity`, `PointContainment`, and `SectionSampling`;
- `CirMapBaselineComparison` — stable summary comparison between CIR and BRep maps.

Inputs are deliberately explicit:

- a `CirMirrorAdmission` source descriptor;
- the requested analyzer use;
- the `CirNode` primitive when a CIR map is potentially allowed;
- a `CirMapPrototypeRequest` containing view, rows, cols, bounds, sample count, root iterations, and tolerance;
- an optional `BrepBody` baseline for primitive parity comparison.

Outputs include:

- selected backend;
- raw mirror admission result from `CirMirrorAdmissionService`;
- optional CIR map result when the CIR path is selected;
- optional BRep baseline summary comparison;
- deterministic diagnostics;
- a finite recommendation string.

## 4. Backend selection policy

The dispatcher applies this policy:

1. Convert the requested analyzer use to a `CirMirrorCapability` and ask `CirMirrorAdmissionService`.
2. If admission is `mirror-admitted-exact`, the requested use is map occupancy, and the admitted capabilities include `MapOccupancy`, select `CirTape`.
3. If the request asks for face identity or topology parity, reject CIR as lossy for the request and select `Unsupported`.
4. If the source is prismatic/profile-chamfer and mirror admission is unavailable or unsupported, select `Unsupported` and emit no prismatic-mirror-used diagnostics.
5. If an optional BRep primitive baseline is provided, evaluate the existing BRep raycast baseline over the same request and compare bounded summaries.
6. Never route production CLI/analyzer calls through this dispatcher.

`CirNode` is kept only as a semantic primitive source for lowering to tape; the selected successful backend is `CirTape`.

## 5. Mirror-admitted primitive cases

The X2 proof cases are:

- box, top view;
- box, front view;
- cylinder, top view;
- cylinder, front view;
- sphere, top view;
- sphere, front view.

For each case:

- mirror admission status is `mirror-admitted-exact`;
- selected backend is `CirTape`;
- map occupancy is produced by the CIR tape evaluator;
- the BRep raycast baseline is compared over stable summaries;
- diagnostics include backend selection, baseline comparison, parity success, no prismatic mirror use, and no production analyzer behavior change.

## 6. Rejected and unavailable cases

CIR-MAP-X2 explicitly rejects or defers these paths:

- **Face identity:** `BoxPrimitive + FaceIdentity` is rejected as `mirror-rejected-lossy-for-request` and produces no CIR map result.
- **Topology parity:** `BoxPrimitive + TopologyParity` is rejected as `mirror-rejected-lossy-for-request` and produces no CIR map result.
- **Prismatic section transition:** `PrismaticSectionTransition + MapOccupancy` remains unsupported/unavailable and does not select CIR.
- **Profile-authored chamfer:** `ProfileAuthoredVerticalChamfer + MapOccupancy` remains unavailable and does not select CIR.

These rejections are intentional. CIR/FRep map occupancy is a field question; face IDs, loops, split-face lineage, and exact topology remain BRep/explicit-topology responsibilities unless a later milestone designs a representation that preserves them.

## 7. Map summary and BRep baseline comparison fields

The dispatcher compares the same stable summary fields used by the X1 primitive map prototype:

- total sample count;
- hit sample count;
- empty sample count;
- minimum thickness;
- maximum thickness;
- average thickness.

The current test tolerance for primitive BRep/CIR thickness deltas is `0.075` model units. Occupancy counts must match exactly for the selected deterministic grids. Thickness is tolerance-bounded because CIR zero-crossings and BRep ray intersections are independent implementations with different boundary conventions.

## 8. Diagnostics contract

CIR-MAP-X2 diagnostics are deterministic and machine-checkable. The prototype emits:

- `cir-map-x2-dispatch-started`
- `cir-map-x2-mirror-admission-requested:<source>`
- `cir-map-x2-mirror-admitted-exact:<source>`
- `cir-map-x2-backend-selected:cir-tape`
- `cir-map-x2-backend-selected:unsupported`
- `cir-map-x2-brep-baseline-compared:<source>`
- `cir-map-x2-map-parity-succeeded:<source>`
- `cir-map-x2-map-parity-warning:<source>:<reason>`
- `cir-map-x2-mirror-rejected-lossy-for-request:<request>`
- `cir-map-x2-mirror-unavailable:<source>`
- `cir-map-x2-no-prismatic-mirror-used`
- `cir-map-x2-no-production-analyzer-behavior-changed`

The finite recommendation vocabulary is:

- `cir-map-dispatch-ready-for-primitive-lab`
- `cir-map-dispatch-needs-tape-hardening`
- `cir-map-dispatch-mirror-unavailable`
- `cir-map-dispatch-lossy-request-rejected`
- `cir-map-dispatch-deferred`

The current implementation reaches `cir-map-dispatch-ready-for-primitive-lab`, `cir-map-dispatch-mirror-unavailable`, and `cir-map-dispatch-lossy-request-rejected` in tests. The remaining recommendation values are reserved for follow-on hardening/deferred branches.

## 9. Limitations

CIR-MAP-X2 does **not** provide:

- production CLI behavior changes;
- production analyzer behavior changes;
- public API changes;
- prismatic mirrors;
- map support claims for prismatic BReps;
- new CIR node kinds;
- CIR-to-BRep extraction;
- STEP exporter/importer changes;
- Boolean core changes;
- BRep topology changes;
- AIR emitter behavior changes;
- default gated corpus execution.

The dispatcher is a lab policy proof, not a general imported-body map router.

## 10. Tests run

Validation runs during the milestone:

```text
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "CirMapX2|CirMapX1|CirMirror" -v minimal
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "AIR-CIR|AirCir|CirMirror|CirMap|CIR|Cir|BrepSpatialQueries|Raycast|BrepPrimitives|Step242|Primitive|Boolean|SafeComposition" -v minimal
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "Analyze|Map|CliBaseline|Step|Prismatic|AirChamfer|Experimental|Corpus" -v minimal
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "CIRLab|PrismaticSectionTransition|PrismaticTopEdgeChamfer|ProfileStackChamfer|ProfileChamfer|ProfileStack|LineArcProfileExtrude|Profile2D|AirChamfer|EdgeSweep" -v minimal
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "FirmamentPrimitive|FirmamentStepExporter|SemanticRecovery|FrepMaterializer|Rematerialize|Chamfer|Fillet|Corner|ProfileStack|LineArcProfileExtrude" -v minimal
```

All listed runs passed. EDGE-X12 / EDGE-PRISMATIC-X6 gated corpus stability tests remain opt-in and are not required by default.

## 11. Recommended next milestone

Recommended next work is one of:

1. **CIR-MAP-X3** — experimental CLI/lab dispatch behind an explicit flag, still preserving default `analyze map` behavior;
2. **CIR-PRISMATIC-X1** — prismatic mirror feasibility, likely requiring a real section-stack/convex-polyhedral field strategy rather than metadata-only admission;
3. **AIR-CIR-X2** — mirror provenance/staleness hardening before any wider analyzer routing.

Do not claim prismatic map support until an actual admitted mirror exists and can be compared against a trustworthy baseline.

## CIR-PRISMATIC-X1 follow-up note

CIR-PRISMATIC-X1 proves prismatic mirror feasibility in a lab/test-only path for `rectangle-inset` and `top-edge-chamfer`, and recommends the convex half-space/polyhedron strategy as the next implementation direction. CIR-MAP-X2 dispatch remains primitive-only in production-facing terms: prismatic map dispatch is still unavailable until a later milestone promotes an explicitly admitted prismatic mirror into the dispatch contract.

## CIR-PRISMATIC-X2 prismatic mirror note

CIR-PRISMATIC-X2 implements the first reusable internal convex-polyhedron mirror for bounded prismatic section stacks, but CIR-MAP-X2 dispatch remains primitive-only. Prismatic map dispatch is still not integrated and remains pending an explicit follow-on milestone such as EDGE-PRISMATIC-X8; no production `analyze map` support claim is added by X2.
