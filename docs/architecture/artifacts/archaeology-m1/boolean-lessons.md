# BRep Boolean lessons from the implemented families

## 1. Why the public API looks simpler than the work

`Union`, `Subtract`, and `Intersect` describe set intent. A valid exact BRep result additionally needs bounded trims, vertices, edges, cyclic coedges, loops (including holes), faces on correct supports, oriented closed shells, geometry bindings, stable IDs/provenance, and manifold validation. The dispatcher succeeds where Aetheris already knows enough about the operands and expected result to author that graph directly.

Pairwise surface intersection is only evidence about where supports meet. It does not decide which bounded portions survive, which loops are outer versus inner, whether a contact is tangent/degenerate, how prior trims participate, or what product feature the user intended.

## 2. Repository lessons by family

| Family/history | What makes the implemented case bounded | What expanded when the next case arrived |
|---|---|---|
| box x box | axis-aligned extents reduce intersection/classification to interval/cell arithmetic; results are a single box or connected orthogonal cell boundary | partial overlaps, face contacts, pockets, existing occupied cells, connectivity, coplanar face merging, and cases not representable as one box required separate policies/builders |
| box - cylinder through hole | known planar entry/exit faces, one cylindrical wall, two circular trims, and known inner-loop orientation | offsets, multiple holes, root history, arbitrary axes, tangency, and hole interference required composition state and validators |
| blind cylinder/cone | one entry face plus a known termination cap; span classification is explicit | entry side, contained tools, cap orientation, cone radii, rotated blind tools, and termination coincidence add distinct topology/tolerance cases |
| counterbore/countersink | coaxial two-segment stack predicts entry ring, transition/shoulder, deep wall, and optional bottom | radius/depth ordering, cone/cylinder transition representation, entry-face rules, and prior segment history require a named family classifier |
| stepped coaxial stack | all segments are world-Z, coaxial, with one through segment and ordered top-entry blind tiers | an `Holes.Count == 1` gate rejected the third subtract before builder execution; fixing admission required N-level history policy, then production fixtures exposed placement/order coupling |
| rotated cylinder/cone | bounded axis recognition and section representation make some through cases tractable | rotated cone remains deferred because the builder/export path has a section-curve representation mismatch; rotated blind cases add termination topology not covered by the through builder |
| prismatic cuts/keyway | caller supplies a recognized polygon/slot/box footprint with bounded extrusion span; topology is ring extrusion with known inner orientation | root kind, containment, through versus blind, mixed prior analytic history, footprint tangency/overlap, and cylinder-root slot geometry each need separate recognition/rebuild routes |
| sphere cavity/opening | a single sphere and its containment/opening relationship to a box are known | multiple spheres, mixed sphere+prism history, tangent openings, and partial intersections require new face/loop cases; they remain bounded/deferred |
| orthogonal union | occupied axis-aligned cells give a finite boundary extraction problem | arbitrary orientations/supports and analytic unions lose the cell-boundary simplification |
| torus | recognition of a toroidal support exists | topology reconstruction remains deferred: more intersection curve families, multiple branches, periodic parameter domains, tangency and representation/export issues appear before a robust result can be authored |

## 3. Stepped-hole history is architectural evidence

The observed sequence was:

```text
through (small)
  -> blind continuation (medium)
  -> second blind continuation (large/shallow)
```

The third subtract originally fell through to independent-hole interference because the coaxial continuation classifier ran only when `composition.Holes.Count == 1`. This was not merely a local bug. The gate encoded an implicit topology/history assumption: the recognized result model and policy had only represented a pair. Supporting the third tier required an N-level coaxial classifier with constraints over the whole history (axis, center, span types, radius ordering, depth ordering). The builder could construct an injected N=3 composition in one probe, yet the production route still needed admission, placement, execution ordering, diagnostics, and downstream STEP regression work.

The lesson is that a successful pair builder does not generalize the state machine around it. Prior operation history is part of admissibility and expected topology.

## 4. Recursive generic CIR execution did not remove the problem

`GenericCirBrepExecutorLab` recursively mapped CIR primitives, transforms, and Boolean nodes to `BrepPrimitives` and `BrepBoolean`. Through holes, blind holes, and counterbores succeeded because the lower Boolean stack already supported those exact families. Stepped subtraction still failed; countersink was blocked by primitive exposure in that experiment. Merely making traversal generic moved no topology knowledge: each recursive Boolean node still arrived at the same family dispatcher with the same history, representation, and builder constraints.

The experiment therefore separates two concerns:

- generic expression-tree evaluation is feasible;
- generic topology reconstruction is not supplied by that evaluation.

## 5. The combinatorial expansion

A robust result depends jointly on:

```text
source support family x tool support family
x source topology x tool topology
x intersection curve/contact type
x tangency or degeneracy
x prior operation/composition history
x bounded parameter domains
x tolerance state
x expected output topology and orientation
```

Adding “one surface” is not one row. It cross-products with supported roots, spans, orientations, prior holes/pockets/slots, coincident/tangent cases, curve representations, trim-domain behavior, exporter representations, and validation expectations. Central dispatcher growth therefore couples recognition, policy, history and topology building into an ever-larger matrix even when every individual implemented family is correct.

## 6. Architecture answer

Generalized BRep Boolean topology reconstruction is unscalable as a central ever-expanding family dispatcher because each admitted case requires bespoke knowledge of the expected bounded topology across a cross-product of supports, operand topology, intersections, degeneracy, history, tolerance, and output representation. Numerical intersection supplies only local geometric evidence; it cannot decide feature intent or the result graph. A central dispatcher makes every new product feature pay for inference across combinations that its authoring path already knew.

The scalable direction is:

```text
high-level construction intent
  -> recognized bounded recipe (owns expected topology and policy)
  -> BRep Surgery (owns explicit graph realization and validation)
```

## 7. Educational preservation

Preserve as worked examples:

- `BrepBoolean.cs` Judgment routing and deterministic rejection diagnostics;
- `BrepBooleanOrthogonalUnionBuilder` for cell-boundary reconstruction;
- `BrepBooleanBoxCylinderHoleBuilder` for through, blind, sphere, counterbore/countersink, and stepped analytic topology;
- `BrepBooleanPolygonalPrismThroughCutBuilder` for outer/inner loop orientation;
- `BrepBooleanCylinderOpenSlotBuilder` for mixed plane/cylinder explicit topology;
- `BrepBooleanBoxMixedThroughVoidBuilder` for history-aware remap/composition;
- safe-composition graph and stepped root-cause tests;
- deferred-pile and FrictionLab strategy/generic-executor documents.

Recommended durable docs:

1. `docs/kernel/brep-surgery.md`: contracts and low-level mechanics.
2. `docs/kernel/brep-boolean-lessons.md`: why Boolean looks simple; intersection versus topology; box/box; through and blind holes; counterbore; stepped N-level history; rotated/conic representation; prismatic/keyway; sphere; torus; recursive CIR; intent plus Surgery.

Later comments should be placed at recipe builder entry points, family classifiers, and Surgery boundaries. They should state the known expected topology and bounding assumptions, plus why policy stays above Surgery; individual mechanical methods need concise invariant comments, not essays.
