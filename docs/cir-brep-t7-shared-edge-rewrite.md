# CIR-BREP-T7: One bounded shared-edge/coedge rewrite using remapped topology references

## Scope

CIR-BREP-T7 performs exactly one bounded shared-edge rewrite inside the stitch executor, only after T6 combined-body remap succeeds and remapped concrete topology references are available.

## Topology mutability diagnosis

- `TopologyModel` stores entities in dictionaries and supports only `Add*` APIs.
- Topology entities (`Edge`, `Coedge`, `Loop`, `Face`, etc.) are immutable records.
- Coedge edge-reference rewrite is therefore implemented as bounded body rebuild: copy topology, replace the two target `Coedge.EdgeId` values with the canonical edge id, preserve all other topology and bindings.

## Duplicate edge policy (T7)

- **Policy A (bounded): keep duplicate edge** after coedge rewrite.
- Duplicate edge is intentionally retained and becomes unreferenced by coedges.
- Edge binding for duplicate edge is retained in T7 to avoid expanding cleanup scope.

## Geometry binding policy

- Canonical edge binding remains unchanged.
- Rewritten coedges now point to canonical edge id.
- Duplicate edge binding is retained (deferred cleanup milestone).
- Invariant check verifies all bindings still point to existing topology ids.

## Vertex policy

- Vertex merge is explicitly deferred.
- T7 does not merge or rewrite vertices.
- Diagnostics include `vertex-merge-deferred`.

## Invariant checks after rewrite

- Every coedge references an existing edge.
- Every loop references existing coedges.
- Every face references existing loops.
- Every edge binding references existing edge.
- Every face binding references existing face.

## Execution behavior

Mutation is attempted only when all gates pass:

- ready stitch candidate exists,
- candidate kind is `SharedTrimIdentity`,
- entry tokens match,
- roles + orientation are compatible,
- T6 remap succeeded,
- remapped candidate entries exist,
- remapped refs include edge + coedge ids,
- remapped ids resolve in combined body.

If any gate fails, executor returns deferred/unsupported with precise diagnostics and no mutation.

## Claims and non-claims

- Full shell closure is **not claimed**.
- STEP export is **not attempted**.
- Single-candidate bounded scope remains explicit and deterministic.

## Next milestone

- Add bounded duplicate-edge cleanup and binding cleanup policy (only when proven invariant-safe).
- Evaluate bounded vertex equivalence/merge strategy for stitched trim endpoints.
