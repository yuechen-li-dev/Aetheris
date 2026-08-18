# AIR-X1 — Minimal AIR wrappers for proven constructive lanes

## 1. Purpose and scope

AIR-X1 introduces a minimal internal AIR envelope around three already-proven constructive lanes:

1. profile extrusion;
2. prismatic section transition;
3. top-face loop chamfer lowered as a Class B loop operation through prismatic section transition.

The milestone validates typed AIR values, immutable lowering summaries, provenance, deterministic diagnostics, route identity, selection/rule metadata, topology summaries, and optional STEP smoke hooks. It does not replace any production route.

## 2. Relationship to AIR-A0

AIR-X1 follows the AIR-A0 constitution: Aetheris remains a compiler for BRep, AIR is constructive geometry MIR, BRep remains explicit topology authority, CIR remains an admitted evaluation mirror, and STEP remains serialization. The implemented wrappers sit at the `Constructive AIR -> existing emitter -> BRep` boundary and deliberately stop short of BRepPlan.

## 3. Implemented AIR model/envelope

The internal `Aetheris.Kernel.Core.Air` envelope defines node kind, route kind, authority, selection class, rule kind, mirror status references, provenance, diagnostic, topology summary, STEP smoke summary, lowering summary, and AIR body records. The values are internal and test-visible through existing friend assembly patterns.

## 4. AIR node kinds and route kinds added

Node kinds:

- `ProfileExtrude`
- `PrismaticSectionTransition`
- `TopFaceLoopChamfer`
- `Unsupported`

Route kinds:

- `ProfileExtrudeEmitter`
- `PrismaticSectionTransitionEmitter`
- `TopFaceLoopChamferPrismatic`
- `Unsupported`

## 5. Provenance and diagnostics shape

`AirProvenance` records milestone, source kind, feature name/id, route name, selection class, rule kind, construction-history kind, whether the wrapped lane is an existing production/proven route, and notes. `AirDiagnostic` records code, severity, message, and optional details. Diagnostics are sorted/deduplicated for deterministic tests.

## 6. Wrapped lanes

### Profile extrusion

`AirProfileExtrudeWrapper` lives in Firmament because the existing `LineArcProfileExtrudeEmitter` is owned by Firmament materialization. It uses a canonical `10 x 8` rectangle extruded to height `6`, invokes the existing line/arc profile extrusion emitter, and reports AIR-X1 provenance without changing default production behavior.

### Prismatic section transition

`AirPrismaticSectionTransitionWrapper` lives in Core AIR and invokes `PrismaticSectionTransitionEmitter` with a canonical split-preserving three-section rectangle/inset stack: `z=0` full rectangle, `z=5` full rectangle, and `z=6` inset rectangle. STEP smoke is enabled because the existing emitter exposes it cheaply.

### Top-face loop chamfer

`AirTopFaceLoopChamferWrapper` lives in Core AIR and invokes `PrismaticTopFaceLoopChamferPrototype` using width `10`, depth `8`, height `6`, and chamfer distance `1`. The wrapper preserves Class B metadata: selection class `FaceBoundaryLoop`, rule `UniformChamfer`, and route `TopFaceLoopChamferPrismatic`.

## 7. Topology summaries and STEP smoke behavior

The prismatic and loop chamfer wrappers report the canonical split topology: `12` vertices, `20` edges, `10` planar faces, `0` cylindrical faces, `10` loops, `40` coedges, and bounds `[-5,-4,0]..[5,4,6]`. The profile wrapper reports the topology produced by the existing profile emitter rather than guessed counts. STEP smoke is checked for prismatic and loop-chamfer wrappers; the profile wrapper leaves STEP smoke as not checked in AIR-X1.

## 8. Route-exclusion guarantees

The top-face loop chamfer wrapper records guarantees that it is not a production route replacement; does not use AirEdgeSweep, BrepBoundedChamfer, topology graft, 3D Boolean, or coplanar merge; and is not four independent single-edge chamfers. The prismatic wrapper records split-preserving topology and no coplanar merge. The profile wrapper records no production route replacement.

## 9. What AIR-X1 does not do

AIR-X1 does not implement production route replacement, BRepPlan, a route-selection or JudgmentEngine framework, CIR mirror behavior changes, Firmament lowering changes, STEP exporter/importer changes, BRep topology behavior changes, Boolean behavior changes, chamfer/fillet/shell geometry changes, AirEdgeSweep changes, BrepBoundedChamfer/BrepBoundedFillet route changes, arbitrary graph support, import/recovery, triangle migration retry, NURBS/freeform expansion, or test weakening.

## 10. Tests run

Focused AIR tests were added for profile extrusion provenance, prismatic split-preserving topology, Class B loop metadata, and deterministic wrapper summaries. Required filtered Core and Firmament tests were run during implementation; broader required filters are part of the final validation log.

## 11. Recommended next milestone

AIR-X2 should introduce route-selection/admissibility metadata only after more wrappers need bounded strategy choice. AIR-X3 should introduce a BRepPlan for prismatic section transition if the next goal is to separate constructive planning from direct emitter invocation. AIR-X1 evidence suggests BRepPlan for the prismatic section stack is the narrower next technical step.

## AIR-X2 route-selection note

AIR-X2 now records deterministic route decisions for the AIR-X1 wrapper lanes. Profile extrusion and prismatic section transition use direct selection; top-face loop chamfer uses switch/match classification. These decisions are internal/test-visible and do not replace production routes.

## AIR-X3 BRepPlan note

AIR-X1 wrappers remain thin envelopes around proven lanes. AIR-X3 adds a non-production, internal/test-visible BRepPlan for the canonical prismatic section transition lane, but it does not replace the AIR-X1 wrapper or the existing emitter path.


## AIR-X4 top-face loop chamfer BRepPlan role note

The AIR-X1 top-face loop chamfer wrapper remains the existing proven lane. AIR-X4 adds a non-production BRepPlan wrapper that reuses the prismatic plan and records Class B provenance plus chamfer semantic roles for upper transition faces without replacing this wrapper.

## AIR-X6 trace consumption

`aetheris trace` consumes the AIR-X1 wrapper summaries for `prismatic-section-transition` and `top-face-loop-chamfer` as source/provenance evidence. The wrappers remain non-production lowering evidence and are not route replacements.
