# Rounding every edge of bodies with circular edges

Each source fillets every edge of a body that is not Box or Polyhedron stock, so
the body is built first and then rounded whole: `cylinder-rounded` (two rim tori),
`frustum-rounded` (rim tori on a cone), `box-hole-rounded` (a through bore whose
rims round to tori next to the block's cylinder strips and sphere corners) and
`d-profile-rounded` (an extruded line-and-arc profile: tori along the arc, a
cylinder down each line-to-arc edge, spheres at the four corners).

The Firmament UniformRoundingRoute tests build them; the BlendNetworkRemoval
experiments remove the rounding again.
