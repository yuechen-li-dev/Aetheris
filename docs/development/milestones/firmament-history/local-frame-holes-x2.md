# Local-frame semantic holes (X2)

Construction Planes provide pre-solved local material coordinates. Holes remain ordinary local-Z drilling features.

The first X2 production lane accepts a semantic shaft hole whose immutable
`ConstructionPlanePlacement` carries the traced Concept Plane identity, frame
origin, proper local axes, and a local XY center.  It does not use an entry-face
name, a raw topology identifier, or a completed-body transform.  The legacy
face-local `AirFaceLocalHolePlacement` remains the compatibility route for
existing sources only.

For an admitted rectangular Box host, the compiler transforms the eight host
corners into the supplied local frame. It admits only a proper signed-permutation
frame with the mouth on local Z=0 and material on local +Z. This proves one exact
host interval `[0, span]`; directions away from the host and mouths outside the
cross-section are rejected explicitly. The result is an authoritative
`LocalFrameHoleBRepPlan` which owns Hole frame/interval/descendants and delegates
shared vertices, analytic circle/cylinder supports, DirectedEdgeUse loops, faces,
shell, and materialization to `ProfileExtrusionBRepPlan`.

This lane currently implements only a simple `ThroughAll` shaft on a simple Box.
It publishes Mouth loop/edge, Exit loop/edge, and Shaft wall-face descendants,
then exports an exact analytic cylinder through the normal STEP path. Blind
flat-bottom and DrillPoint (`ShaftDepth`/`TotalDepth`, default 118deg) are not
implemented by this narrow lane yet; they must not silently fall back to an
oversized cut or mutable face attachment.

X3 makes this kernel lane reachable from ordinary Firmament source. See
[Construction Plane Hole source X3](construction-plane-hole-source-x3.md) for
the source declaration, typed placement union, inspection evidence, and the
strict compatibility boundary with legacy face-local holes.
