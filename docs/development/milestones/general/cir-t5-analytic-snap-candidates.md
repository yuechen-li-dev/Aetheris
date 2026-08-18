# CIR-T5 — bounded analytic snap candidates for stitched restricted-field contours

## Purpose

CIR-T5 introduces deterministic **candidate-only** analytic snap analysis on stitched contour chains from CIR-T4.

Input: `SurfaceTrimContourStitchResult` with `SurfaceTrimContourChain2D` chains.  
Output: `RestrictedContourSnapAnalysisResult` with `RestrictedContourSnapCandidate` entries for line/circle fitting attempts and deferred/unsupported placeholders.

## Candidate-only policy

This milestone does **not**:

- accept exact trim curves,
- emit BRep topology,
- enable STEP exact export,
- alter Boolean behavior or materialization routing.

The result object hard-codes this milestone boundary via:

- `AcceptedCount = 0`
- `ExactTrimAccepted = false`
- `BRepTopologyImplemented = false`
- `StepExportImplemented = false`

## Fitting policy

### Circle fitting (closed chains)

- Closed-loop chains are fit in UV space.
- Fit method for T5 is bounded and stable: centroid + mean radius.
- Errors are radial residuals.
- Candidate emitted when `max radial error <= MaxCircleError`, otherwise rejected.

### Line fitting (open/boundary chains)

- Open/boundary chains are fit in UV space using first/last endpoint direction.
- Errors are perpendicular distances to the line.
- Candidate emitted when `max perpendicular error <= MaxLineError`, otherwise deferred.

### Deferred/unsupported categories

T5 surfaces explicit out-of-scope states:

- `EllipseDeferred`
- `BSplineDeferred`
- `Unsupported`

## Tolerance/error options

`RestrictedContourSnapOptions`:

- `MaxCircleError`
- `MaxLineError`
- `MinPointCount`

Conservative defaults are provided and diagnostics always echo selected option values.

## Torus/non-circular policy

Torus contours are treated exactly like any stitched chain in this milestone:

- if circular under chosen tolerances, a candidate may be emitted,
- otherwise rejected/deferred with high-error diagnostics.

No torus exactness claims are made in T5.

## Diagnostics

T5 diagnostics include:

- analysis start + chain count,
- option echoing,
- per-fit method and error metrics,
- candidate produced/rejected/deferred status,
- explicit exact-trim/topology/export-not-implemented markers.

## JudgmentEngine usage decision

T5 intentionally emits all evaluated candidates and does not choose a winner among competing strategies.

Because there is no bounded runtime strategy selection in this milestone, `JudgmentEngine` is intentionally **not** used yet.

## SEM-A0 guardrails

SEM-A0 guardrails remain preserved:

- no generated topology naming,
- no selector-provenance changes,
- no user-facing topology identity additions.

## Recommended CIR-T6 next step

CIR-T6 should add bounded candidate ranking/selection (likely via `JudgmentEngine`) and acceptance gate wiring, while still separating candidate generation from final exact trim/BRep/STEP realization until explicitly enabled.
