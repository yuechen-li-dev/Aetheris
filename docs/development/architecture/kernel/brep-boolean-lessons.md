# BRep Boolean lessons

This is a practical guide for implementing a bounded topology recipe. The
public Boolean names are compatibility vocabulary; they are not evidence that
the kernel can infer arbitrary result topology.

## The Boolean API illusion

`Subtract(left, right)` looks uniform while its successful implementations are
not. A through-hole, blind pocket, open keyway, prismatic cut, and orthogonal
union each require different loop roles, face survival rules, orientation, and
history. The facade is useful routing compatibility, but the implementation
must eventually name the recognized construction it is performing.

## Intersection is not topology

`Union`, `Subtract`, and `Intersect` express set intent. An exact result additionally needs bounded trims, outer and inner loops, face supports and senses, a closed shell, stable identity, bindings, and validation. Intersection geometry supplies local evidence; it does not choose those graph elements or explain product intent.

## Through hole and blind hole

For the canonical box/cylinder case, recognition proves a world-Z cylinder
fully spans a box. `ThroughHoleRecipeRequest` then carries the box root, the
recognized hole descriptor, tolerance, feature identity, and
`SafeBooleanComposition` history. The expected result is fixed before topology
editing: six surviving exterior faces, one inner circular loop on each planar
support, one cylindrical wall, two rings, and a periodic seam. The recipe
supplies those loop senses and support bindings to Surgery, then validates and
STEP-round-trips the result.

This works reliably because the answer's topology is part of the admitted
family contract. A generic arbitrary-body subtraction lacks that contract: an
intersection curve cannot say which fragments survive, which loops are inner,
how faces are oriented, which identity survives, or whether earlier features
permit the operation.

A blind hole adds state: one exterior opening, a termination circle, a bottom
face, and cavity-facing orientation. That is a different known topology recipe,
not merely a shorter intersection interval. Rotated tools, tangency at entry,
non-planar supports, and intersection with an existing void likewise require
new bounded recognition and topology contracts, not flags on this recipe.

## Composition and the stepped-hole cliff

Safe composition records recognized construction history, not generic BRep mutation history. The historical stepped sequence was:

```text
small through subtract -> medium blind continuation -> large shallow continuation
```

The first and second operations succeeded, while the third originally crossed a `Holes.Count == 1` family/history gate before topology building. Generalizing the builder alone was insufficient; admission required constraints over the whole coaxial history. Topology reconstruction complexity therefore depends on accumulated operation history, not only the current two bodies. The stepped-stack root-cause regressions remain in place.

## Counterbore and the stepped transition

A counterbore predicts two cylindrical walls, an annular shoulder, ordered radii/depths, and possibly a blind bottom. The shoulder loop roles and orientations are known recipe facts. Treating it as two unrelated generic subtractions loses precisely the state required to author and validate that topology.

## Generic CIR execution

The generic CIR experiment recursively mapped a Boolean expression tree to `BrepBoolean`. It succeeded only for families already supported below and did not remove stepped/conic limitations. A generic syntax tree is a generic traversal mechanism; it does not create a generic topology reconstruction algorithm.

## Rotated, conic, overlap, and tangency cases

Rotating a cylinder or cone changes more than an axis value: support intersections, seam placement, parameter intervals, face splitting, and orientation all change. The preserved rotated/conic regressions show why recognition success cannot substitute for a complete reconstruction contract. Overlap and tangency are similarly topological events, not merely small distances. A zero-distance witness may mean intended contact, a non-manifold result, coincident support, or tolerance noise; the owning bounded family must decide.

The stepped-hole root-cause and continuation/history suites, overlap/tangency regressions, rotated/conic failures, mixed-continuation evidence, and generic CIR executor lab remain permanent educational evidence. Their entry points are indexed in the [M6 current-paths evidence](../system/artifacts/archaeology-m6/current-paths.md).

## Contrasting recipe: polygonal through cut

The polygonal recipe is deliberately not a circular-hole parameterization.
Recognition supplies ordered outer and inner footprints and a full through
span. The recipe creates one planar cavity wall per inner polygon segment and
multi-edge inner loops on both support faces. Explicit correspondence and wall
ordering replace the circular recipe's analytic rings and periodic seam. Both
recipes use the same Surgery mechanics because those mechanics only realize
caller-authorized loops, faces, shells, and validation.

## When to write your own BRep recipe

Use this decision ladder:

1. Prefer a Firmament Template for ordinary product generation.
2. Use a typed construction primitive when the result is a primitive.
3. Use an existing recognized Recipe when its admitted intent matches.
4. Add a reusable bounded Recipe when a construction family is broadly useful and its expected topology can be stated completely.
5. Use bespoke Surgery only when the consumer already knows the exact topology it must author.
6. Treat arbitrary-body Boolean as a compatibility or experimental path, not a default implementation strategy.

Do not add a central dispatcher case merely because a new pair of bodies can be recognized. The recognition must name a useful construction contract, and the result graph must be predictable enough for explicit validation.

## Do not do this

- Do not convert arbitrary intersection witnesses directly into authoritative trims.
- Do not treat numerical zero as proof of topological identity or intended contact.
- Do not recognize a temporary tool body again when the semantic caller already knows the construction intent.
- Do not add one central dispatcher case per product feature.

### Bespoke bounded example

For a known planar plate with a known inner polygonal opening, gather intersection/contact evidence only to check the authored polygon against the support. Author the outer loop and the explicitly ordered inner loop, create one known wall face per inner edge, assemble the caller-selected faces into a shell, validate manifold incidence/bindings/finite geometry, then inspect and reimport STEP. The developer owns the expected topology, orientation, IDs, provenance, and tolerance decisions. `IntersectionQuery` witnesses are not authoritative trims unless that bespoke recipe explicitly adopts them under its own contract.

## Writing the bounded contract

Write the expected face/edge/loop graph first. State the admitted root and tool
families, orientation convention, tolerance boundaries, analytic support
requirements, identity/history behavior, and exact rejection cases. Only then
construct geometry and call Surgery. If those facts cannot be stated without
inspecting arbitrary fragments and guessing their role, the proposed recipe is
not bounded enough.

## M3 extraction lesson

Strict loop closure immediately exposed that several canonical legacy builders use historical coedge-sense conventions, while orthogonal merged rectangles can retain T-junction incidence. M3 kept those outputs stable and isolated the seams. This is the practical distinction: a recipe may carry historical representation policy, while Surgery should state and enforce the topology invariant expected of new callers.

The scalable direction remains:

```text
high-level intent -> recognized bounded recipe -> explicit BRep Surgery -> validation
```
