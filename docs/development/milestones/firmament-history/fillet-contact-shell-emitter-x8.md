# Fillet contact-shell emitter X8

## Production wiring

X8 closes the production gap between `ProfileEdgeFinishMixedShellPlan`, the
X7 `ProfileFilletContactShellPlan`, and B-rep allocation.  A closed-loop
Profile Fillet now follows this route:

1. source binding resolves the complete outer loop in Profile order;
2. analytic policy selects each rounded Cylinder/Sphere/Torus patch;
3. the contact planner publishes cap contacts, side chains, incidence, and
   source-side fragment ownership;
4. `ProfileFilletContactShellMaterializer` preallocates vertices and named
   shared edges, emits parent cap/side fragments and analytic patches, then
   verifies exactly two opposite uses for every edge;
5. the ordinary manifold, STEP preflight, reimport, and volume-assertion gates
   run unchanged.

The former production stop was in `ProfileFilletShellPlanner.TryPlan`: curved
segments reached `ProfileBoundaryFilletArcMaterializationNotImplemented`, and
a line-only closed loop reached
`ProfileBoundaryFilletLoopTopologyNotMaterialized`.  Immediately before that
stop, production already had a source-ordered `ProfileEdgeFinishMixedShellPlan`;
the X7 contact plan was tested but never called.  The closed-loop branch now
constructs and validates both plans and passes both to the materializer.

## Contact and topology consumption

Rounded line/arc patches expose exact side and cap curves, predecessor and
successor meridians, semantic descendants, regularity, and provenance.  A
ConvexMedium arc has a spherical limit and a cap vertex rather than a
zero-length edge.  ConvexLarge is a horn torus.  ReflexSmall, ReflexMedium,
and ReflexLarge are regular ring tori with major radii 6, 8, and 12 mm and
minor radius 4 mm.

Sharp convex line/line neighborhoods use the exact direct intersection of the
two adjacent rolling cylinders.  The shared miter boundary is a planar ellipse
with major radius `sqrt(2) F`, minor radius `F`, and no spherical/toroidal
corner patch.  ExactRolling reflex junctions allocate the horn torus and
preserve the vertical notch as a point incidence. SphereSeamCompatibility deliberately uses a different
neighborhood: the parent side reaches the depth vertex, separate roll/support
edges reach the two sphere seams, and the support patch closes those seams.
It is not treated as an ExactRolling torus with a surface substitution.

Source-side faces consume the planned source identity and contact edge names.
The top cap consumes the ordered cap chain and reflex cap arcs.  Segment
patches and junction patches consume the same preallocated edges.  Closed-loop
emission allocates no endpoint termination faces.  No coordinate weld,
Boolean, post-B-rep repair, or NURBS fallback is used.

## Validation evidence

The default and compatibility release cards both build as one enclosed,
orientation-consistent manifold body and reimport through Aetheris STEP.  The
default result has 37 faces, 86 edges, 84 vertices, 14 Plane faces, 17
Cylinder faces, 1 Sphere face, 5 Torus faces, six convex miter ellipses, and no
NURBS. Compatibility has 38 faces, 88 edges, 87 vertices, 15 Plane faces, 17
Cylinder faces, 2 Sphere faces, 4 Torus faces, six convex miter ellipses, and no NURBS. Thus the policy change is the
expected horn-torus to compatibility-sphere/support delta.

FreeCAD 1.0.2 imports both persistent files without healing.  Each is valid,
closed, and contains one solid and one shell.  FreeCAD reports volumes of
913725.7396023329 mm^3 (ExactRolling) and 913733.5792146825 mm^3
(SphereSeamCompatibility), while preserving Plane/Cylinder/Sphere/Toroid
families.

The fixture values were independently derived by adaptive integration of the
source-section family.  Aetheris's current trimmed-curved-face verifier is
numerical and reports conservative error envelopes of 41226.22 and 41250.62
mm^3 for these large, many-patch shells, so the literal `Assert Volume`
tolerances are rounded up to those certified envelopes.  External-kernel
values agree with the independently integrated expectations.  Tightening the
internal verifier's curved-trim bound is verification work, not fillet
topology work.

Repeated builds are byte deterministic; source order controls component,
fragment, vertex, edge, face, and STEP order.  The focused tests build twice,
compare raw STEP text, reimport, check incidence, assert the analytic-family
delta, and assert zero NURBS and zero termination descendants.

## Legacy and remaining limits

M1, M2, M3 ExactRolling, and the local compatibility fixture remain on their
established materializers and retain their existing geometry/mass behavior.
They share the same component geometry and contact extraction, but X8 does not
rewrite those already-proven open-chain emitters.  The generic old diagnostics
remain only for unsupported open curved chains and other routes outside the
closed whole-loop branch.

Remaining non-release limits are bottom whole-loop Fillet, inner loops,
cavity interaction, variable radius, three-edge convergence, arbitrary
angles, and the intentionally rejected ConvexSmall policy row.  None is
silently approximated.

## Documentation-only authoring exercise

The release card is authorable on the first attempt from the canonical syntax:
`Target: Chimera.Outer On: Top Kind: Fillet Radius: 4mm`.  No topology or
fragment declarations are authored.  The inspection evidence identifies the
ConvexMedium sphere, three rounded reflex ring tori, ConvexLarge horn torus,
and sharp ExactRolling horn torus.  Adding
`ReflexJunction: SphereSeamCompatibility` changes only the sharp reflex
neighborhood.  Requesting ConvexSmall continues to produce the typed spindle
policy diagnostic.  No parser or B-rep archaeology is required by the author.
