# 3DM-RECOVERY-X2 qualification

**Verdict: Meaningful progression.** Real Rhino rational edge and surface support geometry now passes through the shared bounded non-rational STEP recovery algorithms. The 3DM path still has no trim/topology binding or STEP export, so this is not an accepted 3DM-to-STEP milestone.

## Boundary and tolerance

`Aetheris.ThreeDm` converts OpenNURBS control nets, weights, and knot vectors to the existing core spline representation. It attempts Rhino analytic recognition first. Unrecognized rational curves use `BSplineCurveRationalReduction` (adaptive cubic segments); unrecognized rational surfaces use `BSplineSurfaceRationalReduction` (adaptive Greville refinement). The generic result is attached to a source edge or face as `GenericRecovery`, separate from the analytic candidate. It records method, configured budget, maximum sampled deviation, independent Rhino sample statistics, and recovered control counts. The parent BRep report carries the source object UUID and entity indices.

The default recovery budget is **0.1 mm**, configurable with `recover-3dm --recovery-tolerance-mm`. Source tolerance remains separate and governs the older analytic candidate study. Kernel tolerance is not altered. A surface reducer cap of 256 controls per axis and 8,192 controls total prevents unbounded collocation allocation. The shared reducers stop when the requested tolerance is met; the 3DM bridge also checks the result independently against Rhino at matched parameters. These are sampled bounds, not continuous error proofs.

Recovered support geometry belongs to import/interoperability. The authored Firmament materializer and analytic geometry types are untouched. A future exact recognition pass can replace a `GenericRecovery` candidate for the same source edge or face before BRep binding. No recovered spline is promoted to a plane, cylinder, or other analytic authority by this fallback.

## Local witness measurements

All files below are ignored local Cartesian/Formas assets under `testdata/3DM`; none is copied into tracked output. Timings are warm CLI analysis passes on this host and cover source read, recognition, and support reduction together. They do not include topology construction or STEP export.

| Witness | Source BReps | Faces | Edges | Recovered rational curves | Recovered rational surfaces | Worst measured deviation | Manifold / STEP | Time |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| Product Design, 0.1 mm | 27 | 185 | 346 | 53 | 83 | 0.09908 mm | Source 27/27 / unavailable | 2.04 s |
| Product Design, 0.05 mm | 27 | 185 | 346 | 53 | 83 | 0.04497 mm | Source 27/27 / unavailable | 2.06 s |
| Furniture, 0.05 mm | 67 | 402 | 804 | 0 | 0 | N/A | Source 67/67 / unavailable | 1.03 s |
| Structure, 0.05 mm | 316 | 2,010 | 4,433 | 0 | 0 | N/A | Source 316/316 / unavailable | 1.35 s |
| Interior, 0.05 mm | 539 | 31,997 | 49,705 | 0 | 0 | N/A | Source 539/539 / unavailable | 6.58 s |

The Product Design source has 143 Rhino-recognized rational analytic edge supports and 21 qualified rational analytic surface supports in the existing study. Its 53 unclassified rational edges and 83 unresolved rational surfaces all produced generic supports at both settings. Aggregate generic control count increased from 3,227 to 3,776 at the tighter setting. At an intentionally extreme 0.00001 mm budget, 53 curves and 37 surfaces qualify while 46 surfaces report failure; all 27 source BRep records remain visible. The other three witnesses have no unresolved rational supports in this study; this does not mean their bodies are imported.

## Blocking work for accepted X2

The current `ThreeDmRecovery.Analyze` returns source topology inventory and support candidates, not `BrepBody`. It does not map Rhino vertices, edge uses, loops, trims, face orientation, and shell enclosure into Aetheris topology. In particular, source pcurves have not been bound or qualified against recovered supports. `BrepPcurveRecovery.Populate` is the existing shared pcurve path to reuse after topology and geometry bindings exist. Without that work, no honest manifold, volume, STEP, or STEP reimport result can be reported. Failed per-body conversion and partial preservation likewise require an actual conversion path.

The next pass should build one complete source BRep through the existing topology/binding model, reuse the shared pcurve binder, validate enclosure and orientation, then run production STEP export and reimport. It should preserve per-object diagnostics so one bad body does not suppress good bodies. This boundary keeps generic source recovery out of authored Firmament geometry.

## Downstream status

The later [3DM-BREP-BIND-X0](3DM-BREP-BIND-X0.md) pass binds and reimports 22 of the 27 Product Design BReps through production STEP. The five remaining bodies have explicit trim, preflight, or enclosure diagnostics. This does not change the X2 support-only verdict above.
