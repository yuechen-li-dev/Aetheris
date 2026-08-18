# 00 — Purpose and Scope

LLMs need a CAD strategy manual because final geometry does not teach process. A BRep or STEP file can record faces, edges, loops, topology, and analytic surfaces, but it usually does not preserve the choices a competent CAD user made to build the part robustly.

These notes help LLMs infer authoring intent, not merely list geometry.

```text
Geometry is what exists.
Modeling strategy is how a competent CAD user would construct it robustly.
Semantic decompilation tries to recover the second from the first.
```

## Primary uses

These notes are especially aimed at:

- Semantic decompilation from BRep, STEP, DisplayIR, screenshots, and inspection reports.
- Firmament V2 candidate authoring.
- Missing capability analysis for Firmament, AIR, CIR, BRep, DisplayIR, and related tools.
- Plausible feature-tree recovery.
- Separating geometric equivalence from resilient authoring strategy.

## Scope

The folder collects practical CAD modeling strategy adapted for LLMs. It focuses on how to reason about model construction order, stable references, feature decomposition, and design intent.

These notes were started from observed CAD practice and Aetheris decompilation experiments. They are not a claim of novelty and are not a personal doctrine.

## Non-goals

These notes do not:

- Define Firmament syntax by themselves.
- Authorize parser changes.
- Authorize lowering changes.
- Change AIR, CIR, BRep, STEP import/export, DisplayIR, tessellation, or product behavior.
- Replace implementation specifications or tests.
- Claim that there is only one valid way to model a part.

When these notes expose a missing capability, record the gap explicitly and propose a separate implementation milestone.
