# CIR-BREP-X8 generic CIR tree → BRep executor feasibility lab

## Purpose and scope
FrictionLab-only feasibility probe for recursive `CirNode -> BrepBody` execution; no production wiring changes.

## Code inspected
`CirNode` / all node types, `BrepPrimitives`, `BrepBoolean` + safe composition/coaxial families, `HoleRecoveryPolicy` variants, `HoleRecoveryExecutor`, STEP exporter.

## Prototype shape
`GenericCirBrepExecutorLab` recursively maps CIR primitives/booleans/transforms into BRep constructors + `BrepBoolean` calls and emits structured per-scenario diagnostics.

## Support matrix
- Box/Cylinder/Sphere/Torus: supported via `BrepPrimitives`.
- Cone: unsupported in this lab scope (no public cone primitive constructor from FrictionLab assembly).
- Transform: identity + pure translation only.
- Boolean: subtract/union/intersect via `BrepBoolean`.

## Scenario results
- Through-hole: succeeded, STEP succeeded.
- Blind-hole: succeeded, STEP succeeded.
- Counterbore (coaxial subtract stack): succeeded, STEP succeeded.
- Countersink: blocked by cone primitive mapping in lab scope.
- Stepped-hole: failed on subtract stack (`boolean-subtract-failed`), matching deferred semantic characterization intent.
- Unsupported transform: correctly rejected.
- Box-sphere/box-torus: executed through generic path (status captured by tests).

## Semantic comparison
Generic behavior is aligned with current semantic executor outcomes for through/blind/counterbore (success), stepped (still blocked), and countersink remains blocked in this experiment due to lab-scope cone primitive access.

## STEP smoke
On successful generic outputs: exporter produced expected STEP markers (`ISO-10303-21`, `ADVANCED_FACE`, solid root marker) and analytic surface markers as available.

## SafeBooleanComposition findings
Per subtract diagnostics include whether safe composition persisted. Multi-step counterbore preserved executable composed state. Stepped still fails at repeated coaxial subtract despite recursive-tree execution, indicating recursive execution alone does not remove the existing blocker.

## Stepped-hole conclusion
Generic recursive execution does **not** inherently solve stepped-hole in current boolean stack.

## Architecture recommendation
Hybrid path: keep semantic policy for admissibility/ordering + use generic executor where primitive coverage is complete; retain variant-specific fallback for known hard families (stepped, cone-gated paths) until public primitive/boolean coverage closes.

## HoleRecoveryExecutor disposition
Keep current executor as production path; treat generic CIR executor as a future internal execution backend once cone + stepped blockers are closed.

## Recommended milestone
M1: expose/standardize cone primitive construction for non-production labs and add deeper safe-composition observability hooks.

## Confidence
- Through/blind/counterbore: high.
- Stepped blocker characterization: high.
- Countersink generic viability: medium (blocked by scope/API visibility, not proven false).
