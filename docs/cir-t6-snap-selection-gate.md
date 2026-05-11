# CIR-T6 — Snap Candidate Ranking/Selection + Analytic Acceptance Gate

## Purpose

CIR-T6 adds deterministic bounded selection over CIR-T5 snap candidates. It chooses internal analytic trim candidates (circle/line) only when admissible, otherwise falls back to numerical-only/deferred routes.

This milestone is **selection and gate only**:
- no BRep topology emission,
- no STEP export behavior change,
- no final trim-curve representation integration.

## Candidate routes

- `AnalyticCircle`
- `AnalyticLine`
- `NumericalOnly`
- `Deferred`
- `Unsupported`

Export capability is classified separately:
- `ElementaryCurveCandidate`
- `NumericalOnlyNotExportable`
- `Deferred`
- `Unsupported`

`ElementaryCurveCandidate` means compatibility with future elementary-curve export wiring, not actual export in CIR-T6.

## JudgmentEngine usage decision

CIR-T6 is a bounded competing-strategy problem (line vs circle vs fallback), so `JudgmentEngine` is used to make admissibility, scoring, rejection reasons, and deterministic tie-breaking explicit.

## Admissibility and scoring

Per chain:
- Circle is admissible when candidate kind/status/sample-count/error and closed-loop suitability pass.
- Line is admissible when candidate kind/status/sample-count/error and open/boundary suitability pass.

Ranking:
- lower error yields higher score,
- circle gets preference on closed loops,
- line gets preference on open/boundary chains,
- deterministic tie-break follows JudgmentEngine ordering.

## Fallback behavior

If no analytic candidate is admissible and numerical contour data exists, route is `NumericalOnly` with diagnostics indicating analytic rejection/defer and non-exportable status.

If no chain/candidate data exists, route is `Unsupported`.

## Torus policy

Torus contours may still produce a candidate circle in special geometry cases. CIR-T6 diagnostics explicitly state that generic torus exactness/boolean support is not implied.

## Non-goals

- ellipse acceptance
- BSpline fitting
- BRep topology
- STEP export
- torus quartic/exact recognition
- boolean behavior changes

## SEM-A0 status

SEM-A0 guardrails remain preserved: CIR-T6 introduces routing/diagnostics only, without generated topology naming or provenance model expansion.

## Next step (T7)

Integrate selected analytic candidates into trim-curve representation routing (CURVE track) while preserving the same acceptance/export separation and guardrails.
