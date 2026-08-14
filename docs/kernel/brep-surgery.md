# Internal BRep Surgery

`Aetheris.Kernel.Core.Brep.Surgery` is the internal, low-level boundary for realizing topology that a caller already knows. It creates ordered coedge cycles, faces from caller-designated outer/inner loops, closed shell/body ownership, and runs structural/binding/finite-geometry validation. It does not recognize features, select Boolean families, interpret intersection curves, or decide which topology survives.

## Authority boundary

```text
recognition + Judgment policy + composition/history
    -> bounded recipe supplies exact topology and orientation
        -> BRep Surgery realizes and validates it
            -> canonical BRep/STEP validation
```

`IntersectionQuery`, `ClosestPointQuery`, `SignedSideQuery`, and `ContactQuery` remain evidence-only. Numerical contact can tell a recipe where supports meet; it cannot decide loop roles, surviving trims, cavity intent, or accumulated-history admissibility.

## Vocabulary and contracts

- An **edge** owns endpoint vertex IDs. A `BrepEdgeUse` supplies the caller's explicit traversal sense.
- A **loop** is a closed ordered cycle of coedges with deterministic next/previous links. `BrepLoopBuilder` rejects missing edges, repeated identical directed uses, and open endpoint chains.
- A **face** owns one caller-designated outer loop followed by zero or more caller-designated inner loops. `BrepFaceBuilder` does not discover loop membership or winding. Surface creation and binding remain explicit recipe work because current topology and geometry stores are separate.
- A **shell/body** is assembled from the exact face set supplied by the caller. `BrepShellAssembler` rejects missing/duplicate faces and any edge incidence other than two face-boundary uses.
- `BrepSurgeryValidation` reuses `BrepBindingValidator`/`TopologyGraphValidator` and additionally requires a finite point for every vertex.

All operations return `KernelResult<T>` with `KernelDiagnostic` errors. Surgery does not retry with inferred ordering or fallback feature interpretations. No local epsilon is used by these topology-only operations.

## Example flow

For a known rectangular through cut, the recipe creates corresponding lower/upper vertices and edges, specifies outer and reversed inner uses, asks Surgery to create faces and a closed shell, binds the already-known line/plane supports, and validates the body. Whether the requested subtraction belongs to that family was decided before Surgery was called.

## M3 compatibility seams

Some established Boolean builders predate `DirectedEdgeUse` closure semantics, and orthogonal retessellation can retain T-junction incidence across merged coplanar rectangles. M3 preserves their canonical coedge senses and assembly rather than silently changing STEP. The narrowly named `CreateKnownLoopPreservingLegacySense` shares deterministic coedge-cycle mechanics but deliberately omits the newer endpoint-closure check; it is internal and used only at documented compatibility seams. Strict new loop/shell construction uses `CreateKnownLoop` and `BrepShellAssembler`. The remaining seams are evidence for recipe-local orientation work in M4, not reasons to weaken the strict primitives.

Surgery is internal in M3. Public safe/unsafe API decisions are deferred until the contracts have more recipe evidence.
