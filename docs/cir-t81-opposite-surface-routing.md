# CIR-T8.1 opposite-surface routing cleanup

T8 attached oracle trim evidence but used broad opposite-operand fields for every loop.

## Routing policy

Per loop, opposite routing now uses descriptor provenance + family:
- exactly one match: `oracle-trim: specific-opposite-selected` and strong evidence
- multiple matches: `oracle-trim: multiple-opposite-sources-deferred`
- no provenance match but family exists: `oracle-trim: broad-opposite-field-only` (diagnostic-only evidence)
- no compatible opposite: `oracle-trim: missing-opposite-source`

## Strong vs broad

`RetainedRegionLoopDescriptor` now tracks:
- `OracleTrimStrongEvidence`
- `OracleTrimRoutingDiagnostic`

Readiness trim-oracle layer reports strong vs broad/deferred counts.

## Guardrails

No BRep emission, STEP export, torus generic exactness, naming changes, or public CLI changes.

## JudgmentEngine

Not used. Multiple indistinguishable opposite sources are deferred, not scored.

## T9

Add per-loop restricted-field slicing by selected opposite descriptor for truly pair-specific contouring where opposite operands are composite.
