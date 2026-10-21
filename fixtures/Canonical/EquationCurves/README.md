# Equation curve qualification sources

These are complete buildable Firmament files. Public authoring documentation is
in docs/public/firmament/equation-curves.md.

- arch: degree-two point law, exact cubic representation, explicit baseline and
  5 mm extrusion. Nominal profile area is 1066.6666666666667 mm². The test also
  builds its XZ variant and samples the reimported STEP crown at world height 20 mm.
- record-arch: the same geometry with ordinary typed Record inputs.
- circle: recognized exact full circle, periodic contract, radius 10 mm.
- sine-arch: explicitly approximated sine crown, 0.05 mm maximum deviation,
  exact straight baseline and ordinary profile extrusion.
- helix: spatial construction curve with a 0.01 mm approximation budget. Keyed
  turn samples evaluate the original equation and drive an 8 mm Box height.
  This is a guide/layout witness, not a swept helical solid.

EquationCurveTests also cover radians/degrees, typed Record results, domain and
frame rejection, singularities, tolerance retention, unknown fields, and explicit
conic loop consumption. Approximation evidence checks the whole-interval bound
and independent intermediate samples. Geometry acceptance includes STEP reimport
and enclosed/oriented topology for the arch in XY and XZ.
