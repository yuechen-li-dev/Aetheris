# CIR-T10 — consume oracle-derived analytic circle trims in planar materializer path

## Purpose
CIR-T10 wires strong selected-field trim-oracle evidence into planar retained patch-set emission for `Subtract(Box, Cylinder)` base-side planar retained patches.

## Strong evidence requirements
Oracle trim is consumed only when all gates hold:
- `OracleTrimStrongEvidence == true`
- routing diagnostic is `oracle-trim: selected-opposite-field-used`
- representation kind is `AnalyticCircle`
- `AcceptedInternalAnalyticCandidate == true`
- `ExactStepExported == false`
- `BRepTopologyEmitted == false`
- source bounded planar geometry is a rectangle

Broad/deferred evidence remains diagnostic-only and is explicitly rejected as non-materialization grade.

## UV→world conversion policy
`OracleTrimLoopGeometryConverter.TryConvertAnalyticCircle(...)` converts analytic circle UV data into retained circular loop geometry using planar rectangle parameterization axes.

T10 policy is conservative:
- reject degenerate rectangle parameterization,
- reject non-uniform U/V world scaling (would create world ellipse from UV circle),
- emit precise diagnostics on rejection.

## Binder/oracle comparison policy
If both binder-derived and oracle-derived circles exist:
- compare center/radius with tight tolerance,
- if agreement: diagnose agreement and continue,
- if mismatch: conservative skip/defer with mismatch diagnostic.

## Rejected cases
- broad-only oracle routes,
- numerical-only/deferred/unsupported representations,
- sphere/torus non-analytic-circle routes,
- multiple inner loops,
- unsafe UV→world conversions.

## Out-of-scope guarantees
- no shell assembly,
- no STEP export behavior changes,
- no boolean behavior changes,
- no generated topology naming changes.

## Next step
Generalize conversion provenance/tolerance contracts so additional exact analytic retained-loop routes can be consumed without weakening SEM-A0 guardrails.

## T10.1 debug closure
Root cause observed in real `Subtract(Box,Cylinder)` patch-set emission:
- inner-circle emission candidates can carry strong oracle attachments on some retained loops, but the emitted entry can still be binder-driven when analytic-circle oracle evidence on the emitted inner-loop route is unavailable/rejected,
- this previously caused test expectations to require oracle-consumed diagnostics in all successful inner-circle emissions.

Final precedence/fallback policy:
- prefer strong analytic-circle oracle evidence when all strong gates pass,
- if oracle route is absent/rejected and binder retained circle exists, emit via binder fallback with explicit `oracle-trim-fallback-to-binder` diagnostics,
- preserve strict rejection diagnostics for broad/deferred/numerical-only routes.

No shell/export behavior changed.
