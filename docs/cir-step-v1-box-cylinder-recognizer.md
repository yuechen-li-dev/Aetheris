# CIR-STEP-V1 Box-Cylinder Recognizer

## Scope
Production recognizer for canonical CIR shape:
- `CirSubtractNode(Box, Cylinder)`
- optional identity/pure-translation wrappers (`CirTransformNode`) around operands.

This milestone does **not** build BRep, export STEP, or wire rematerializer behavior.

## Inputs
`CirBoxCylinderRecognizerInput`:
- required: `CirNode Root` (authoritative geometry)
- optional: `NativeGeometryReplayLog ReplayLog` for provenance diagnostics
- optional: `SourceLabel`

`NativeGeometryState`, lowering plans, and execution-result adapters are intentionally deferred to CIR-STEP-V2.

## Accepted shape
Accepted only when all are true:
1. root is subtract,
2. lhs normalizes to `CirBoxNode`, rhs normalizes to `CirCylinderNode`,
3. wrappers (if any) are identity/pure-translation only,
4. dimensions finite positive,
5. cylinder is canonical Z-axis (no rotation allowed),
6. cylinder spans full box depth (through-hole policy),
7. strict XY clearance interior to box (offset allowed).

## Rejection policy
Stable reason-coded rejects include:
- non-subtract root,
- lhs non-box / rhs non-cylinder,
- unsupported transforms,
- invalid dimensions,
- not-through hole,
- tangent/grazing,
- outside-box footprint,
- nested/composite booleans.

Tolerance uses `ToleranceContext.Default.Linear` and classifies near-boundary as unsupported conservatively.

## Replay policy
Replay is optional and diagnostic-only:
- recognition decisions are based on CIR node geometry,
- replay enriches diagnostics with op/feature/source/tool details,
- replay mismatch does not force success and does not override geometry,
- if geometry succeeds but replay contradicts tool kind, recognizer returns success with `ReplayMismatch` reason.

## Relationship to FrictionLab X3/X3.1/X4
This production recognizer directly implements lab findings:
- real lowered fixture shape includes transform wrappers,
- root CIR node is geometry authority,
- replay is optional provenance,
- state/plan/result adapters are v2 follow-up.

## Next step (CIR-STEP-V2)
Add adapter layer from execution/lowering artifacts into recognizer input and consume recognized normalized result in direct box-with-round-hole BRep builder path.
