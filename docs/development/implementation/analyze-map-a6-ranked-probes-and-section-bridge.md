# ANALYZE-MAP-A6 ranked probes and section bridge

A6 keeps `aetheris analyze map` on the local-measurement path: start with a coarse six-view map, rank bounded component questions, and emit point-probe, local-map, and section-slice follow-ups instead of brute-forcing larger global grids.

## Why not full maps

CTC-01 dogfooding showed 16x16 six-view maps are useful but already noticeable in tight LLM loops, while 32x32 global maps can be too slow. A6 therefore preserves coarse maps and asks smaller local questions around suspicious components.

## Command shape

```bash
aetheris analyze map part.step --views six --resolution 16x16 --llm --rank-probes --json
aetheris analyze map part.step --views six --resolution 16x16 --llm --evidence-bundle --json
```

`--evidence-bundle` implies ranked probes. Existing detailed map output remains present.

## JudgmentEngine integration decision

The existing `JudgmentEngine<TContext>` is a single-winner deterministic chooser. A6 needs a ranked list of many useful local questions, so the implementation uses a small deterministic scorer that is JudgmentEngine-compatible in shape: explicit admissible candidates, scores, evidence terms, tie-breaking, and rejection-by-low-score potential. A future adapter can move this into a multi-selection JudgmentEngine API without changing the JSON schema.

## Scoring factors

Higher scores are assigned to:

- interior no-hit components;
- cylinder, cone, torus, and sphere surface-family clusters;
- analytic provenance;
- fallback components needing truth-checking;
- small isolated height-band components;
- central/non-border regions;
- moderate component size.

Lower scores are assigned to large border-touching no-hit regions because they are often exterior silhouette gaps.

Each ranked probe includes score, normalized score, reasons, evidence terms, uncertainty, classification hint, and recommended actions.

## Map/section bridge

For every ranked component, A6 emits:

- a compact point-probe command using the component centroid;
- two `aetheris analyze section` commands through the centroid, mapped from the current view plane to supported `--xy`, `--xz`, or `--yz` section syntax;
- a local map refinement recommendation.

Follow-up evidence is not executed automatically in A6. Local map commands are marked unsupported because `analyze map` does not yet accept explicit bounds.

## Evidence bundle schema

The `evidenceBundle` object contains:

- `source`;
- `coarseMap` with resolution, view count, and summary-only flag;
- `rankedQuestions` mirroring `rankedProbes`;
- flattened `suggestedActions`;
- empty `executedEvidence` for A6;
- `limits` with max ranked items and max executed probes;
- notes about non-execution and bounds limitations.

## Compact point-probe summary

Point-probe JSON now includes `pointSummary` with:

- hit count;
- first/last hit family, position, face id, and backend mode;
- compressed family sequence;
- backend mode counts;
- coordinate range along the ray;
- compact diagnostics.

The full `hits` array is retained.

## Limitations

- No feature reconstruction or hole/slot assertions are made.
- No automatic section or point-probe execution is performed by the evidence bundle.
- Local map windows are recommendations until explicit bounds are supported.
- Cross-view correlation and repeated/symmetric feature grouping remain future work.

## Next milestone candidates

- Execute local map bounds.
- Adaptive refinement around high-ranked components.
- Cross-view correlation.
- Face/surface-family grids.

## Phase closeout

The first LLM-oriented analyze-map phase is summarized in `docs/development/reports/analyze-map-phase-closeout-a0.md`.
