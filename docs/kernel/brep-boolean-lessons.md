# BRep Boolean lessons

## Intersection is not topology

`Union`, `Subtract`, and `Intersect` express set intent. An exact result additionally needs bounded trims, outer and inner loops, face supports and senses, a closed shell, stable identity, bindings, and validation. Intersection geometry supplies local evidence; it does not choose those graph elements or explain product intent.

## Through hole and blind hole

The bounded box-cylinder through-hole case works because the recipe already knows one cylindrical wall, entry and exit rings, and the affected planar faces. A blind hole adds state: one exterior opening, a termination circle, a bottom face, and cavity-facing orientation. That is a different known topology recipe, not merely a shorter intersection interval.

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

## M3 extraction lesson

Strict loop closure immediately exposed that several canonical legacy builders use historical coedge-sense conventions, while orthogonal merged rectangles can retain T-junction incidence. M3 kept those outputs stable and isolated the seams. This is the practical distinction: a recipe may carry historical representation policy, while Surgery should state and enforce the topology invariant expected of new callers.

The scalable direction remains:

```text
high-level intent -> recognized bounded recipe -> explicit BRep Surgery -> validation
```
