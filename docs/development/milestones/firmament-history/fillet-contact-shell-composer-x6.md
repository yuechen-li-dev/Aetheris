# Fillet contact-shell composer X6

X6 introduces the contact-boundary IR used to keep Profile Fillet shell
composition honest.  A `ProfileFilletContactBoundary` records a component and
source owner, exact curve and trim, parameter traversal, endpoints, regularity
evidence, and provenance.  `ProfileFilletComponentContactContract` groups the
cap contact, source-side contacts, and predecessor/successor interfaces for a
single analytic component.  `ProfileFilletContactShellPlan` is immutable and
source ordered; it is intentionally free of B-rep faces, shell ids, and
endpoint-termination topology.

The contact planner now validates the composition boundary before allocating a
face.  Rounded source-tangent components can describe their source and cap
contacts, while a sharp line/line vertex is rejected with
`ProfileFilletContactSharpJunctionComponentRequired`.  The release card proves
the first unresolved contact is the wraparound `Outer.Bottom.Start` junction.
This is materially earlier and more specific than the former
`ArcDerivedCompositionPending` route failure.

The remaining implementation is narrowly defined: resolve each sharp M2/M3
component's displaced cap/side contacts into the graph, then emit one
parent-owned cap, the trimmed source sides, and the shared analytic edges.  It
must not replace those contacts with offset-profile intersections, suppress
STEP preflight, introduce NURBS, or add Boolean/post-B-rep repair.

X7 adds the missing parent-side ownership layer above these component contacts:
an ordered `ProfileFilletSideContactChain`, explicit two-face edge-incidence
contracts, point-only contacts, and side-fragment plan records.  The graph is
validated before B-rep allocation; see `fillet-side-contact-chain-x7.md`.

X8 completes this boundary for the admitted whole-loop route.  Production now
constructs the X6/X7 plan, allocates the parent cap, source-side fragments, and
shared analytic edges from it, and emits deterministic non-NURBS Fillet and
SphereSeamCompatibility artifacts.  Existing M1/M2/M3 materializers remain
the ground truth for open-chain endpoint policy.  See
`fillet-contact-shell-emitter-x8.md`.
