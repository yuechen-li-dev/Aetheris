# Fillet side-contact chain X7

## Audit

There is no recoverable parent-shell emitter diff in the current repository
history or reflog.  The evidence retained in X5/X6 and in the successful local
M2/M3 materializers is sufficient to identify the failure class: adapting the
Chamfer parent-side loop gave each source side one coarse contact, while sharp
junctions have additional ownership boundaries.  That makes the parent side
skip a junction support boundary or allocate a coincident replacement.  The
result reaches the manifold gate with an edge used by one face or by three
faces; it is not a surface-family or STEP `same_sense` problem.

The local M2 emitter makes the ownership explicit:

- source side A owns its cylinder-side contact and the A-side support edge;
- source side B owns its cylinder-side contact and the B-side support edge;
- the convex-sphere patch owns its two sphere-to-roll seams;
- the planar junction-support face owns the opposite uses of the support
  boundaries.

The M3 ExactRolling emitter differs materially.  Its two cylindrical side
contacts meet at `junction-vertical-notch`; the horn torus has a point
incidence there.  There is no non-degenerate side-support edge at that point.
Representing it as one would create a zero-length edge and is forbidden.
`SphereSeamCompatibility` remains a separate component topology: its support
contacts must be supplied by that component, never copied from the torus case.

## Contact IR

`ProfileFilletSideContactChain` is source ordered and contains typed
`ProfileFilletSideContactEdge` and `ProfileFilletSideContactVertex` elements.
An edge stores its exact curve/trim, vertices, curve direction, component,
source-side owner, expected opposite owner, semantic role, provenance, and
predecessor/successor position.  A vertex contact carries no curve, so point
contacts cannot become degenerate edges.

`ProfileFilletContactEdgeIncidenceContract` allocates one edge identity before
emission and names both face uses with opposite traversal.  The parent side
and the junction/roll component must reference that same identity.  A
`ProfileFilletSourceSideTrimPlan` groups one or more
`ProfileFilletSideFaceFragmentPlan` records under the original source-side
identity, preserving semantic descendants when a side must split.

`ProfileFilletContactVertexIncidence` records edge and face incidence by
planned vertex identity; no coordinate welding is involved.

## Validation

`ProfileFilletContactGraphValidator` runs before B-rep allocation and reports
the specific planner invariants:

- `ProfileFilletContactEdgeMissingSecondFace`;
- `ProfileFilletContactEdgeOverSubscribed`;
- `ProfileFilletContactOrientationConflict`;
- `ProfileFilletSideContactChainOpen`;
- `ProfileFilletSideContactChainOutOfOrder`;
- `ProfileFilletDuplicateSupportEdge`.

It validates chain order/continuity, a single planned contract per contact
edge, distinct/opposite face uses, duplicate support identities, explicit
vertex references, and the absence of zero-length edge contacts.

## X8 completion

M1 has a reusable extracted line contact and a preallocated side/cylinder
incidence contract; focused tests cover the M2 roll-plus-support chain and M3
point-not-edge contact.  X8 now consumes this ownership model in the production
closed-loop materializer.  Rounded arc components publish exact contacts, and
SphereSeamCompatibility uses its distinct roll/support/sphere neighborhood.
Both seven-station artifacts pass planned incidence, manifold, STEP reimport,
and external FreeCAD validation.  See `fillet-contact-shell-emitter-x8.md`.
