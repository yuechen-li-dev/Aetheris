# Helios bracket edge selection and full-length bend finishes

## Problem and correction

Picking an edge tinted its whole occurrence gold, obscuring which edge was
selected. The old UI advertised an inset fillet on an extrusion cap while the
bracket's inside bend was unavailable. That local cap blend produced short
terminations and did not provide the expected full-width corner finish.

The viewport now highlights only the selected occurrence's edge: a thick gold
line with a translucent halo, with cyan hover feedback. The body remains in
ordinary shading. Both Telos and WebGL selection paths carry occurrence identity.

The compiler publishes source-correspondent convex and reflex profile corners
as longitudinal edge bindings. Helios labels these Outside corner / Inside
bend, full length. Selecting the bracket crease in Iso and applying a 2 mm
fillet creates one exact concave cylindrical surface through the 20 mm width.
The corresponding chamfer creates one planar surface. Outside corners use the
same route with the opposite material effect.

## Geometry and source transaction

`EdgeFinish` with `Face: Longitudinal` and a source target such as
`section.Outer.Vertex3_Vertex4.Start` modifies one polygon corner before exact
profile extrusion. The materializer uses tangent circular arcs for fillets and
equal-distance trimmed legs for chamfers. It does not approximate a fillet with
a mesh or invoke a general surface/surface Boolean. The feature preserves source
provenance and contributes engineering metadata to the model tree.

At least 0.01 mm must remain on adjacent legs and between the new curve and all
nonadjacent boundaries. Exact arc/line distance and intersection checks reject
crossings, tangencies and slivers. Invalid sizes and anonymous or misspelled
feature fields produce diagnostics. Preview does not edit source; Apply commits
one undoable edit. STEP reimport is checked for closed oriented topology.

## Scope

- One finish on one straight-sided polygon outer corner per profile extrusion.
- Inside and outside longitudinal corners; XY, YZ and XZ profile planes.
- Cap-edge chamfer remains available. The legacy explicit inset cap-fillet
  source route remains readable, but the UI disables it because a full-edge
  cap fillet is not implemented.
- Existing Box +X/+Z single-edge routes remain available.
- Curved profile corners, imported bodies, profiles with inner loops and
  multiple finishes are outside this route.
- Picked-face holes and longitudinal finishes still lack a combined-history
  adapter. Both actions are disabled with a specific explanation on that body;
  accepted geometry and source remain intact.
- Delete or undo the earlier cap-fillet demo before trying the new bend.

## Verification

- 3,531 Firmament tests and 760 CLI tests passed.
- 29 focused native cases passed, including 15 longitudinal cases. Independent
  analytic area checks verify inside addition and outside removal, cylinder
  radius, full extrusion bounds, all three profile planes, clearance rejection,
  engineering metadata and closed oriented STEP round trips.
- Development-browser UI tests passed for inside fillet, inside chamfer,
  outside fillet, and the disabled end-face fillet. Tests click projected model
  coordinates in the real viewport and use compiler-projected selection data.
- Production AOT built successfully. Native WASM is 60,431,161 decoded bytes,
  SHA-256 `799fc770b7216a16b89c487c2197be52c1cafaf07cfeecab56ec09f7b1b36f6d`.
- All 24 production graph workflows passed their final checks: the initial
  suite passed 22, with two new hierarchy assertions using the wrong ARIA role;
  after changing `row` to `treeitem`, both affected complete workflows passed.
  Those workflows verify actual picks, one-edge/no-body highlighting, automatic
  preview, Apply, model hierarchy, source, STEP analysis, undo and redo.
- Five focused production WebGL cases passed: inside fillet/chamfer, outside
  fillet with invalid-size recovery, disabled cap fillet and holed-body refusal.
- All 251 Helios unit tests and six SDK selection tests passed. TypeScript,
  the production frontend build and both repositories' `git diff --check` passed.
- Hosted verification is recorded with the deployed build evidence.

The deployment includes complete working-tree patches against the published
Helios and Aetheris baselines. Owning repositories are not committed or pushed
by this task.
