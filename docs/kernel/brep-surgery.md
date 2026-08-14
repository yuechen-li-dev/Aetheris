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

## Complete recognized-recipe flow

The canonical through-hole path is the smallest complete example:

```text
BrepBoolean recognition + Judgment + SafeBooleanComposition
    -> ThroughHoleRecipeRequest (box root, one through cylinder, history, tolerance)
    -> recipe creates box edges plus entry/exit rings and periodic seam geometry
    -> BrepLoopBuilder realizes caller-ordered outer, inner, and wall loops
    -> BrepFaceBuilder assigns the two rings as support-face inner loops
       and creates the cylindrical cavity wall
    -> BrepShellAssembler assembles the seven caller-selected faces
    -> recipe binds line/circle curves and plane/cylinder surfaces
    -> BrepSurgeryValidation checks topology, bindings, and finite vertices
    -> STEP AP242 export and reimport validate the ordinary interchange path
```

The recipe, not Surgery, knows that the top and bottom planar faces survive,
that each receives one circular inner loop, and that one inward-facing
cylindrical wall joins them. The periodic seam is intentionally represented in
the canonical legacy sense; the narrowly named compatibility loop primitive
preserves this without weakening strict construction for new representations.

The polygonal through-cut is the contrast. Its request supplies corresponding
outer and inner footprints. Every polygon segment becomes a separately ordered
planar cavity wall. The support faces receive multi-edge inner loops, yet the
same loop/face/shell/validation substrate applies because Surgery has no hole or
prism feature knowledge.

## Recipe author checklist

- Know the expected topology before editing.
- Preserve analytic supports instead of approximating them.
- Supply loop and face orientation deliberately.
- Preserve provenance, feature identity, and construction history deliberately.
- Validate edge/face incidence and manifoldness.
- Treat recognition and geometry tolerance explicitly.
- Export and reimport through the normal STEP path.
- Do not use generic numerical intersections as topology authority.

## M3 compatibility seams

Some established Boolean builders predate `DirectedEdgeUse` closure semantics, and orthogonal retessellation can retain T-junction incidence across merged coplanar rectangles. M3 preserves their canonical coedge senses and assembly rather than silently changing STEP. The narrowly named `CreateKnownLoopPreservingLegacySense` shares deterministic coedge-cycle mechanics but deliberately omits the newer endpoint-closure check; it is internal and used only at documented compatibility seams. Strict new loop/shell construction uses `CreateKnownLoop` and `BrepShellAssembler`. The remaining seams are evidence for recipe-local orientation work in M4, not reasons to weaken the strict primitives.

## Advanced consumer boundary

> BRep Surgery is an escape hatch for explicit topology construction, not a substitute general Boolean solver.

An advanced caller must know the expected vertices, edges, ordered loop uses, outer/inner loop roles, faces, supports, orientations, and shell membership before invoking Surgery. Geometry queries may provide evidence, but Surgery does not infer trims, surviving fragments, feature intent, or tolerances.

Surgery guarantees deterministic construction from typed explicit inputs and validates graph ownership, edge incidence, bindings when required, and finite vertex geometry. The caller remains responsible for stable identity, feature provenance, construction history, analytic support choice, and a declared tolerance policy. Validate topology and bindings, inspect support kinds/orientations, export and reimport STEP, and compare downstream mesh behavior when relevant.

Current primitive classification is:

- `BrepEdgeUse`, strict known-loop construction, known-face construction, shell assembly, and validation are plausible `SAFE_ADVANCED` building blocks but remain `INTERNAL_ONLY` while their public identity/provenance contracts mature.
- the legacy-sense loop seam is `NOT_READY`; it exists only for canonical compatibility output.
- arbitrary topology mutation, ID injection, bypassed validation, and raw store access would be `REQUIRES_UNSAFE` and are not exposed.

Forge's existing `UNSAFE` extension consent governs arbitrary in-process extension loading; it is not a topology sandbox. Because CLR in-process code cannot provide that sandbox and no BRep-specific permission contract exists, neither Forge.Host nor Forge.KernelSDK exposes Surgery in M5. Existing recognized Recipes should become the first advanced construction surface if consumer pressure establishes a stable public contract.
