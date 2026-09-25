# BREP-BEZIER-SECTION-X0

## Verdict: Meaningful progression

The shared section stack now accepts bounded cubic Bézier profile edges. A generic
cubic profile can be added as a Boss or removed as a Pocket from a plate. Both
results are enclosed, consistently oriented BReps and export/reimport through
STEP AP242. An `O` region with an inner counter also Bosses successfully with
an exact curved wall. This removes the prior cubic arrangement and side-face
blocker for connected sections.

The complete milestone is not accepted. An `O`-shaped Pocket creates a second
material island at the counter in its upper slab. `ProfileArrangementBuilder.Compose`
currently requires exactly one outer material loop and reports
`arrangement-rejected:disconnected-or-invalid-material:outer-loops=2`. The
`PrismaticSectionSlab` contract also stores one `PrismaticSectionRegion`, so
CODEX and other multi-glyph sections need a bounded multi-region slab change
before they can lower as one ordinary Pocket/Boss operation. Quadratic profile
edges are still represented by exact degree-elevated cubics upstream; a native
quadratic section-edge carrier is not yet exposed. No bolt or AETHERIS plate
claim is made.

## Geometry path

- The section arrangement now evaluates, trims, reverses, classifies, and
  reconstructs cubic edges. Cubic area uses Green's theorem on polynomial
  coefficients. Ray containment splits the parameter at Y extrema and solves
  each monotone interval. Bounded intersection candidates use curve convex
  hulls and analytic arc boxes, followed by parameter refinement; all retained
  fragments remain exact curves.
- The section emitter stores each cubic edge as a clamped degree-three
  `BSpline3Curve` with the original four control points. It builds the wall as
  the existing `LinearExtrusionSurface` with that directrix and constant +Z
  vector, then transforms both through the profile construction frame.
  Thus its support is `S(u,v) = C(u) + vN`. Caps stay planar; cap and wall
  coedges share topology edge IDs.
- Existing line and circular-arc behavior remains on its established plane
  and cylinder branches. STEP already serializes/imports the B-spline directrix
  and linear-extrusion surface, so no exporter-only representation was added.
- The bounded arrangement intersection search is tolerance-qualified. It is
  not a general arbitrary planar Boolean engine. The input profile validator
  remains the authority for admissible source loops.

## Evidence

`BezierSectionStackTests` contains four positive cases: generic cubic Boss,
generic cubic Pocket, Inter `O` counter Boss, and Inter `C` Pocket. Each
checks section normalization, BRep enclosure and orientation, presence of a
linear-extrusion surface, STEP export and reimport, and preservation of that
surface kind and its four B-spline directrix control points to seven decimal
places. A fifth test fixes the exact current O-counter Pocket diagnostic.
`PlanarTextProfilesTests.SectionArrangementAcceptsItsExactCubicBoundary`
checks the former cubic rejection boundary now accepts a valid `O` profile.

The threaded HexBolt source was inspected with Aetheris.CLI successfully.
It was not modified. No production CODEX bolt, AETHERIS plate, visual catalogue
render, multi-region STEP count, or performance breakdown was produced. Text
syntax/schema/LX remains deferred while this backend limit exists.

Release build passed with seven existing WebAssembly warnings and no errors.
The fast Core lane passed 1,000 tests. The full solution lane passed all 1,682
Firmament tests and all new section tests. It retained the two CLI and five
SheetMetal failures documented in the prior release reports. One Core display
tessellation test exceeded its 10-second budget under full-suite load; it
passed alone in 881 ms immediately afterwards. The full solution command
therefore exited with failures; it is not reported as green.

## Next geometry boundary

Permit a slab to own several disjoint `PrismaticSectionRegion` values, and
derive transitions and cap/side topology across the complete set. Then verify
the O/D counter Pocket and multiple glyph regions before integrating the
threaded bolt. This is a section-stack topology change, not a typography or
font-normalization change.

## Downstream status (BREP-MULTIREGION-SLAB-X0)

The later slab work permits multiple material regions in one section. O and D counter Pockets, two O counters, and a multi-island AETHERIS Boss pass enclosure, orientation, and STEP roundtrip checks. The historical Bézier verdict above remains unchanged. CODEX still stops at a short X-glyph arrangement fragment before solid lowering; see `BREP-MULTIREGION-SLAB-X0.md`.
