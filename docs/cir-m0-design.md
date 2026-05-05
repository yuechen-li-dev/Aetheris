# CIR-M0: Constructive Intermediate Representation (Design + POC)

## 1) What CIR is

CIR is a typed constructive geometry MIR for Aetheris: an internal geometry tree composed of primitive and CSG nodes that can be evaluated lazily for analysis without first materializing full BRep topology.

## 2) What CIR is not

CIR is not:
- Firmament source syntax,
- STEP or any interchange format,
- a direct replacement for BRep topology,
- a stable public authoring API.

## 3) Relationship to Firmament

Firmament currently parses and lowers to primitive/boolean execution plans. CIR would fit between parsed model and backend execution as a canonical internal geometry MIR (`Firmament -> AST/ParsedModel -> CIR -> backend`).

## 4) Relationship to Forge

Forge and StandardLibrary shape generators can eventually emit CIR subtrees when a path only needs geometric analysis (containment/volume/map/section) before committing to topology materialization.

## 5) Relationship to BRep

BRep remains the authoritative materialized solid backend for existing production flows. CIR is currently side-by-side and does not replace or mutate existing BRep execution.

## 6) Relationship to Boolean engine

In CIR, boolean operations are cheap tree composition (`Union/Subtract/Intersect`) via implicit-field composition rules. This allows direct evaluators to run without immediate BRep reconstruction.

## 7) Relationship to analyzer tools

Point containment and approximate volume can evaluate directly from CIR node `Evaluate(point)` over node bounds. This POC includes deterministic grid-sampled volume estimation that bypasses BRep containment.

## 8) Risks / blockers

- **Materialization complexity:** reliable CIR->BRep extraction for broad geometry/trimmed surfaces remains a major follow-up.
- **Signed distance semantics:** CSG `min/max` composition yields a signed implicit field, but not guaranteed exact SDF everywhere.
- **Dual-kernel risk:** introducing CIR plus BRep risks duplicated semantics unless one path becomes canonical with bounded adapters.
- **PMI intent continuity:** semantic/manufacturing intent currently hangs off feature/operation flows, not CIR nodes; traceability policy is needed.
- **Migration strategy:** rolling in CIR incrementally needs strict scope boundaries so production path behavior remains stable.

## 9) Recommended CIR-M1

1. Add an **experimental Firmament->CIR lowerer** for bounded ops (`box`, `cylinder`, `subtract`, optional `translate`).
2. Add a CIR analyzer API for containment + coarse volume with reproducible tolerances.
3. Add a bounded CIR->BRep adapter only for already-supported safe subset (`box`, `cylinder`, `box-cylinder subtract`) and compare outputs to existing BRep boolean results.
4. Define node-level metadata hooks for preserving feature identity/intent lineage.

## POC status summary

- Implemented CIR node kinds: `Box`, `Cylinder`, `Sphere`, `Union`, `Subtract`, `Intersect`, `Transform`.
- Implemented node bounds + point evaluate + deterministic grid volume estimator.
- Prototype does **not** implement CIR->BRep extraction or STEP export.
- Existing production Firmament/BRep flow is unchanged.
