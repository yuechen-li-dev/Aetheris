# Rounding every edge of bodies with circular and concave edges

Each source fillets every edge of a body that is not Box or Polyhedron stock, so
the body is built first and then rounded whole: `cylinder-rounded` (two rim tori),
`frustum-rounded` (rim tori on a cone), `box-hole-rounded` (a through bore whose
rims round to tori next to the block's cylinder strips and sphere corners) and
`d-profile-rounded` (an extruded line-and-arc profile: tori along the arc, a
cylinder down each line-to-arc edge, spheres at the four corners). With concave
edges filled in: `bushing-rounded` (a revolved stepped bushing: the shoulder's
inner corner is a concave torus), `counterbore-rounded` (the step's corner) and
`blind-hole-rounded` (the floor's corner). `ell-mixed-corner` is refused: its
inner corner joins a concave edge with convex ones.

The Firmament UniformRoundingRoute tests build them; the BlendNetworkRemoval
experiments remove the rounding again.
