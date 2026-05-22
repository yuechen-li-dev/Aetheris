# CIR-F6: replay-guided materializer strategy registry

CIR-F6 refactors bounded CIR→BRep rematerialization from a hard-coded switch into a strategy registry.

## Why this is not arbitrary F-Rep→BRep recovery

Aetheris is not trying to infer BRep from opaque sampled fields. Instead it uses:

- typed CIR node kinds,
- Firmament replay operations,
- provenance/tool kinds,
- resolved placement facts.

That enables bounded exact strategy selection (`replay + CIR subtree -> exact builder`).

## Registry shape

`CirBrepMaterializer` now evaluates a set of named strategies via `JudgmentEngine`.

Each strategy provides:

- stable strategy name,
- admissibility check,
- rejection reason,
- deterministic score,
- materialization execution.

A shared `CirBrepMaterializerContext` carries:

- CIR root,
- optional replay log,
- latest replay operation (derived helper).

## Current strategies

- `subtract_box_cylinder` -> `subtract(box,cylinder)`
- `subtract_box_box` -> `subtract(box,box)`

Both preserve existing bounded behavior (translation-only transforms, simple subtract trees).

## Replay/CIR verification model

When replay is available, strategies require latest replay op to match:

- operation kind `boolean:subtract`,
- expected tool kind (`cylinder` or `box`).

CIR subtree checks remain required and authoritative for shape form.

When replay is absent, tree-only fallback is still allowed, but reflected in strategy rejection diagnostics.

## Diagnostics

Failure now reports:

- no strategy matched,
- per-strategy rejection reasons,
- replay/CIR mismatch reasons,
- transform/subtree mismatch details,
- builder execution failures when applicable.

## Recommended CIR-F7 next step

Add the next bounded subtract family as another strategy (no registry redesign), and extend replay-guided checks to include resolved placement compatibility preconditions before execution.
