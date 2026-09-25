# BREP-HELICAL-RIB-X0 — qualified geometry, topology stop

> Downstream status (X1): the bounded complete-turn manifold topology, AP242 export/reimport, and display witness are now implemented and qualified in `BREP-HELICAL-RIB-X1.md`. The X0 verdict below records the state at the end of the X0 geometry milestone.

**Verdict: Meaningful progression; the requested manifold BRep is not accepted.** The exact cylindrical helix and bounded trapezoidal rib authority now exist in Core, with certified non-rational polynomial realizations for all four boundary helices and three side surfaces. The missing layer is the direct BRep construction that replaces the rib footprint on the support cylinder, stitches shared helical edges, closes the two end caps, and resolves the periodic seam. No mesh, generic sweep, or generic Boolean substitute is used. No `Thread` syntax was added.

## Geometry authority

`CylindricalHelix3` is shared by the existing WireForm axis-coil evaluator and `HelicalRibGeometry`. It retains axis, reference radial, radius, pitch, handedness, axial origin, and angular domain. The rib admits right-hand, single-start, constant pitch/profile geometry and derives lead = pitch and turns = axial span / pitch. Its natural frame is the support axis and rotating radial vector; no Frenet transport is involved. The exact side law is linear across each root/flank/crest profile segment and helical in angle. Four stable boundary roles are `LeadingRoot`, `LeadingCrest`, `TrailingCrest`, and `TrailingRoot`; the side roles are `LeadingFlank`, `Crest`, and `TrailingFlank`. Start/end cap corners are exact evaluations at domain limits.

The canonical geometry test uses a 3.2 mm root radius, 4 mm crest radius, 1.25 mm pitch, 1.1 mm root width, 0.1 mm crest width, and 30 mm centerline axial span: **24 turns**, 0.15 mm minimum adjacent-turn axial clearance, root/crest diameters 6.4/8 mm. This is a metric-like envelope, not a standard thread claim. The footprint fits support bounds from 0 to 31.1 mm. Validation rejects nonfinite input, invalid radii, pitch/span, inverted crest/root widths, root width reaching pitch, nonorthogonal frame, and profile extending beyond support bounds.

## Qualified finite geometry

The boundary realizer emits cubic non-rational B-spline spans. The side realizer emits degree-(3,1) non-rational B-spline surfaces, cubic along the helix and linear across the profile. Both retain the exact authority in their result. On an angular span of width `h`, the axial component is represented exactly and the fourth derivative of the radial component has norm `r`; the cubic Hermite deviation is bounded by `r h^4 / 384`. A separate floating-point allowance is subtracted from the requested tolerance before choosing a bounded segment count. The returned certificate is the analytic bound plus that allowance. The default requested kernel tolerance is 1e-6 mm. Tests sample the canonical 24-turn witness against exact points on all three realized side surfaces and one boundary, and check every sampled error against its certificate. This is a real-arithmetic interpolation bound with a conservative scale-based numerical allowance; it is not yet a pcurve/3D edge consistency certificate for a BRep.

For later topology assembly, a shared edge must be derived from the adjacent surface boundary or realized on a common knot partition. Independently realized curves can choose different partitions even when they represent the same exact helix. The current authority preserves enough information to do that deterministically but does not claim the edge bindings yet.

## Exact missing topology layer

The outer support cylinder must exclude the helical attachment footprint. Its remaining skin must be split into deterministic helical gap patches across angular wraps. The leading/trailing flank and crest faces need coedges that share one curve binding at each boundary and at every seam split. The four-corner start/end caps must meet the support skin without T junctions, and the two stock end disks must close the shell. The current BRep construction utilities do not assemble that specific wound cylinder partition. Producing side surfaces alone, or unioning a floating swept strip, would fail the support-attachment and manifold requirements. This is the isolated next blocker.

There is therefore no canonical BRep body, topology count, STEP export/reimport, display tessellation, shaded render, or construction/topology/STEP timing to report. The finite side representations are geometry candidates, not evidence that exporter and importer accept the eventual trimmed helical faces. No pcurve error bound or orientation qualification is claimed.

## Reference scope

The supplied McMaster pair remains dimensional evidence only. Direct STEP records show 8 mm cylindrical major surfaces and a repeating 0.625 mm axial vertex interval in the threaded file, consistent with a 1.25 mm full pitch. The current Aetheris importer stops at non-circular weighted B-spline curve `#2175`; its weights cannot be dropped. Exact minor diameter, runout, and source topology correspondence remain unverified. Reports of misrendering in OCCT-based readers have not been independently verified here and are not used as acceptance evidence. See `THREAD-EXTERNAL-X0.md` for the measured inventory.

## Verification

The focused Core geometry tests cover one and 24 turns, derived lead/turns, pitch/length/depth changes, transformed axis frame, angular periodicity, support extent and adjacent-turn rejection, deterministic boundary realization, and sampled certified surface deviations. The shared helix evaluation path passed all 30 `WireFormTests`. Release solution build passed. The fast Core lane passed 987/987; the corpus-inclusive Core lane passed 1120/1120; Firmament passed 1649/1649. The full solution gate still exits nonzero on the same baseline failures observed before this milestone: two CLI tests (hex-bolt OBJ polygon count and SheetMetal routing) and five SheetMetal tests. They are outside this geometry work.
