# THREAD-EXTERNAL-X0 — qualification stop

**Verdict: Honest stop.** Aetheris cannot currently construct and export a closed, exact external helical thread through one Firmament feature. The existing `Helix` construct materializes a WireForm centerline, while `SurfaceFeatureValidator` explicitly routes thread/helical surface features to a non-emitting Forge plan. Core has bounded extrusion, revolution, and some analytic Boolean families, but no qualified profile-along-helix BRep emitter with start/end closure. Adding a syntax-only `Thread` would misrepresent material geometry, so X0 does not admit one.

## Supplied reference evidence

Local inputs (not copied into the repository): `91280A546_NO THREADS_Medium-Strength Class 8.8 Steel Hex Head Screw.STEP` and `91280A546_Medium-Strength Class 8.8 Steel Hex Head Screw.STEP`. Reproduce the smooth-body inspection with `dotnet run --project Aetheris.CLI -- analyze <smooth.step> --json` and the threaded import failure with the same command on `<threaded.step>`. Both files declare millimetres through `SI_UNIT(.MILLI.,.METRE.)`. The table separates import-verified values from direct STEP-entity inventory and inferences.

| Feature | Smooth McMaster | Threaded McMaster | Evidence / qualification |
| --- | ---: | ---: | --- |
| Topology | 62 faces, 148 edges, 96 vertices | 147 faces, 405 edges, 268 vertices | Smooth: CLI import; threaded: direct `ADVANCED_FACE`, `EDGE_CURVE`, `VERTEX_POINT` record counts, not imported topology validation. |
| Closed shell | 1 | 1 | Direct `CLOSED_SHELL` records; CLI also assesses smooth body as enclosed manifold. Threaded manifold status is unverified. |
| Overall vertex bounds | X ±6.5, Y ±7.50555, Z ±27.65 mm | Same | Direct `VERTEX_POINT` coordinate references. The head is at positive Z, tip at negative Z. |
| Major diameter | 8 mm | 8 mm | Smooth has two, threaded has 79 `CYLINDRICAL_SURFACE` records of radius 4 mm. These records support nominal major size but do not alone certify every crest. |
| Smooth shank extent | Z −26.7125 to +22.35 mm at radius 4; tip ends at −27.65 mm, radius 3.0625 | — | Smooth STEP vertex and analytic-cylinder inspection. Shank length under head to tip is 50 mm, with a 0.9375 mm tip chamfer. |
| Minor/root diameter | — | At least one end-region vertex radius 3.18810 mm (diameter 6.37620 mm) | This is a vertex sample, **not** a certified minimum over the thread surface. Exact root diameter remains unmeasured. |
| Pitch | — | 0.625 mm repeated axial vertex interval; 1.25 mm full pitch is consistent with two phase intervals | Inferred from repeated thread vertex levels. Helix curve/surface parameterization has not been imported and qualified. |
| Thread length/start/end | Smooth stock provides 50 mm shank | Thread records span much of Z −27.65 to +22.35 mm; exact root and runout bounds unverified | Reference thread start, termination, and runout require successful weighted-curve and swept-surface interpretation. |
| Aetheris modeled witness | Not built for part 91280A546 | Not built | No X0 `Thread` materializer or STEP result exists. Deviations cannot honestly be reported. |

The smooth body imports with 32 B-spline surfaces and 76 polynomial B-spline curves. The threaded file contains 160 `RATIONAL_B_SPLINE_CURVE` records. `aetheris analyze` fails on the first non-circular weighted curve: `Importer.Geometry.RationalBSplineCurve`, with `step242-rational-bspline-circle-fit-residual-exceeded`. Core's `CurveGeometry` has a polynomial `BSpline3` but no weighted-curve variant. Dropping rational weights would change the reference geometry and is not a qualified import route. The importer now names this exact dependency in its diagnostic.

## Intended semantic and geometry boundary

The admitted future intent is an external, cylindrical, constant-pitch, single-start, right-hand, 60-degree metric-style thread. It must own axis, major diameter, pitch, lead (= pitch), axial range, handedness, and profile family. A thread-specific 60-degree profile needs bounded crest, flank, root, and truncation; turn count derives from length/pitch. A major-diameter stock cylinder with a cut helical groove is the most direct candidate for this supplied smooth 8 mm blank, but the choice cannot be qualified until the helical wall, intersections with the stock cylinder, and planar start/end closures form one manifold BRep. No Boolean or mesh fallback is accepted here.

The existing HexBolt template at `fixtures/Compatibility/LegacyV1/Examples/hexbolt_template_m2.firmament` is a different McMaster part (91180A151, 35 mm shank), so it cannot silently serve as the 91280A546 witness. Its `SemanticAxialRegion ThreadRegion` is metadata with `MaterialGeometry: Cylinder`; it is not modeled thread geometry. A future 91280A546 witness should reuse its typed template architecture with a measured 50 mm specification and add exactly one Thread construct targeting the shank face. Head text marking is excluded.

## Missing qualification primitive

The next materializer needs a bounded helical profile sweep or equivalent direct screw-surface construction whose side faces, crest/root surfaces, seam edges, and clipped start/end caps are all exact or carry an explicit bounded realization error. It must emit a closed, consistently oriented BRep accepted by `BrepExportPreflight`, the display tessellator, STEP exporter, and STEP reimporter. The reference-analysis side separately needs weighted B-spline curve support (or a certified exact recovery for the particular curves) to measure profile and overlay accurately. These are substantive geometry representations, not a small parser addition.

## Acceptance status

No authored Thread source, source map, schema/LX exposure, modeled bolt, render, topology, STEP round trip, or construction/tessellation/export timings are claimed. The smooth reference inspection succeeds; the threaded reference import has the specific weighted-curve blocker above. Until the helical BRep primitive exists, a proposed `Thread` syntax or semantic-only feature would fail the mission's real modeled-geometry criterion.
