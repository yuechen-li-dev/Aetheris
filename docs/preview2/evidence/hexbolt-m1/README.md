# HexBolt M1 visual and CAD evidence

Reference: McMaster-Carr `91180A151_NO THREADS_Medium-Strength Class 8.8 Steel Hex Head Screw.STEP`.

The `reference-*` images are FreeCAD 1.0 renders of the supplied STEP. The
`generated-*` images are FreeCAD 1.0 renders of the deterministic Firmament export
`artifacts/hexbolt-m1/mcmaster-91180a151.step`. The views use the same renderer and
camera presets. `*-head.png` and `*-head-opposite.png` preserve both axial directions;
the isometric and side images are the useful shape comparison.

FreeCAD/OCCT import result for the generated artifact:

- objects 1, solids 1, shells 1
- closed `true`, valid `true`
- healing invoked `false`
- surface faces: Plane 9, Cone 8, Cylinder 2, Toroid 2
- volume: 2526.679304884845 mm³

The supplied reference volume in the same FreeCAD process is
2519.532295447053 mm³. The generated sharp-corner semantic hex is 7.147009437792
mm³ (+0.284%) larger; this is consistent with deliberately omitting the supplied
0.26 mm longitudinal head-corner rounds. The reference markings are also omitted.

Cadmata imported the initial exact export as a complete 18-face mixed-fallback
document, but its viewport omitted periodic full-circle shank patches. That finding
caused the final construction to split the torus, cylinder, and tip cone into ordinary
half patches sharing their analytic supports. The final V2 `hexbolt-m1` Cadmata
fixture now loads the 21-face body through the production build/reimport path and
publishes the `HexBolt` Template instance, its parameter table, all semantic regions,
generated descendants, and BRep face ownership. Server integration tests verify the
six-face top-chamfer ownership. The earlier browser screenshot could not be recaptured
because the in-app browser blocked localhost reload; the persisted FreeCAD views and
the Cadmata endpoint test are the final visual/topology evidence.
