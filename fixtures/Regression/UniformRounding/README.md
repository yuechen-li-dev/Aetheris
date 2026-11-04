# Rounding every edge of bodies with circular and concave edges

Each source fillets every edge of a body that is not Box or Polyhedron stock, so
the body is built first and then rounded whole: `cylinder-rounded` (two rim tori),
`frustum-rounded` (rim tori on a cone), `box-hole-rounded` (a through bore whose
rims round to tori next to the block's cylinder strips and sphere corners) and
`d-profile-rounded` (an extruded line-and-arc profile: tori along the arc, a
cylinder down each line-to-arc edge, spheres at the four corners). With concave
edges filled in: `bushing-rounded` (a revolved stepped bushing: the shoulder's
inner corner is a concave torus), `counterbore-rounded` (the step's corner) and
`blind-hole-rounded` (the floor's corner). `rounded-rect-rounded` runs strips on
into each other where flat and arc walls meet tangentially; `ell-rounded` fills
the L's concave inner edge first and rounds over it. Refused by name:
`boss-mixed-corner` (two concave edges and a convex one at each foot corner) and
`pocket-split-face` (the composed plate's walls are split at the pocket's depth).

The Firmament UniformRoundingRoute tests build them; the BlendNetworkRemoval
experiments remove the rounding again.
