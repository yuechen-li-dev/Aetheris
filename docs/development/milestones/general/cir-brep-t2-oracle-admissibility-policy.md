# CIR-BREP-T2 Oracle Admissibility Policy

## Outcome

CIR-BREP-T2 lands a generalized oracle trim admissibility gate for planar patch-set inner-loop materialization.

## Gap classification

Current blocker class is primarily **B (Exactness/materialization-grade contract)** with a secondary **D (identity diagnostics completeness)** concern.

- Tiered oracle evidence may be present but is not materialization-grade unless analytic-circle + accepted-internal + bounded intake flags are satisfied.
- Numerical/deferred/unsupported tiers remain diagnostic-only.
- Identity token is currently treated as a diagnosable quality signal (not a hard blocker) for planar patch-set promotion.

## Generalized admissibility model

`PlanarSurfaceMaterializer.OracleTrimMaterializationAdmissibility.Evaluate(...)` centralizes route eligibility checks and emits:

- admissible yes/no,
- fallback route target,
- blocker enum,
- exactness tier label,
- conversion safety requirement flag,
- identity token status,
- deterministic diagnostics.

## Policy decision

**Policy A — oracle materialization-grade allowed when admissibility passes.**

For real `Subtract(Box,Cylinder)` planar patch-set candidates:

- try oracle route first when strong selected-opposite evidence exists,
- reject with explicit blocker diagnostics when inadmissible,
- preserve binder fallback as safe route.

## Safety constraints retained

- No numerical-only promotion.
- No deferred/unsupported promotion.
- No promotion when readiness or bounded planar rectangle prerequisites fail.
- UV→world conversion remains a hard post-admissibility gate; non-uniform scale rejects.
- Binder fallback remains available and explicit.

## Non-goals (unchanged)

- shell assembly,
- topology stitching,
- STEP behavior changes,
- BSpline fitting,
- torus recognition expansion,
- boolean scope expansion.
