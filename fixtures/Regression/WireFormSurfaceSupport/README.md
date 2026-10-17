# SurfaceCoil support admission boundary

These four original sources qualify the existing evaluable centerline law through
`WireFormAuthoring.Parse`. They are not complete buildable Model examples. The
normal frontend correctly refuses a Model containing support solids plus another
material operation; BlenderBall additionally contains two material solids.

The corpus modernization pass moved these sources here unchanged from Canonical.
`WireFormTests.CylinderSurfaceCoil_IsAxisCoilEquivalentAndSurfaceFamiliesRetainClearanceLaw`
retains the support clearance, sampled centerline equivalence, turn clearance and
approximation-error assertions. Do not interpret this internal parser evidence as
normal validate/build qualification.

Next prerequisite: define an explicit nonmaterial analytic support and bind its
reference through the owning AST scope, separately from material outputs. Do not
silently erase ordinary solids when a SurfaceCoil references them.
