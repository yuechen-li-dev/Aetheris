# CIR-T7: Selected trim candidate → tiered trim representation bridge

## Purpose

CIR-T7 adds an internal bridge from CIR-T6 snap selection results into a tiered trim representation object.
It is diagnostic-first and behavior-preserving: accepted analytic candidates remain internal only.

## Tiered representation model

`TieredTrimCurveRepresentation` carries three layers:

1. **Analytic tier** (`AnalyticLine` / `AnalyticCircle` data in UV, with errors)
2. **Numerical tier** (stitched chain points and status)
3. **Surface-intersection provenance tier** (source surface key/family, opposite field role/kind, route and chain)

It also carries explicit state flags:

- `AcceptedInternalAnalyticCandidate`
- `ExactStepExported=false`
- `BRepTopologyEmitted=false`

## Export capability distinction

CIR-T7 maps to explicit internal export capability states:

- `ElementaryCurveCandidate` (future line/circle export could be possible)
- `NumericalOnlyNotExportable`
- `Deferred`
- `Unsupported`

`ElementaryCurveCandidate` does **not** imply STEP export happened.

## Route behavior

- `AnalyticCircle` selection builds an analytic-circle representation + numerical contour + provenance.
- `AnalyticLine` selection builds an analytic-line representation + numerical contour + provenance.
- `NumericalOnly` preserves numerical contour and marks not exportable.
- `Deferred` / `Unsupported` preserve diagnostics and stay non-exported.

## Torus policy

CIR-T7 preserves candidate-only semantics and never claims generic torus exactness.
No quartic recognition is introduced.

## Non-goals (unchanged)

- No BRep edge/coedge/loop emission
- No STEP curve entity emission
- No retained-loop descriptor mutation
- No BSpline fitting
- No boolean behavior change
- No public CLI behavior change

## JudgmentEngine usage

No new JudgmentEngine use was introduced in T7. The bridge is deterministic mapping from T6 selected route into representation.

## Next step (T8)

Integrate tiered trim representations into retained loop descriptors/materialization planning while preserving SEM-A0 naming/provenance guardrails and keeping export semantics explicit.
