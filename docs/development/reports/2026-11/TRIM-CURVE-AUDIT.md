# Trim-curve (pcurve) audit: our method or the source data?

**Question.** NIST FTC-08, FTC-11, STC-07, STC-10 and CTC-04 import with thousands of
pcurve validator errors, and E3's removals failed full round trips. Is Aetheris wrong, or
are the files?

**Answer.** Almost entirely Aetheris: four bugs across validation, recovery and export.
The source files are correct by STEP, apart from 23 edges whose 3D curves sit off their
surfaces beyond the file's own declared accuracy.

## Method today

The NIST files carry no pcurves. On import Aetheris **derives** them (`BrepPcurveRecovery`):
analytic surfaces map 3D curve samples to (U, V) by `atan2`, unwrap angles, and fit cubic
segments with finite-difference tangents, qualified at 1 µm. **Validation**
(`BrepPcurveValidator`) lifts each pcurve onto its surface against the 3D curve, checks the
coedge's start and end against its vertices, and checks UV continuity to the next coedge.
The **exporter** validates before writing pcurves and silently omits them on failure.

## Findings

| Finding | Owner | Evidence |
|---|---|---|
| Validator matched vertices to trim ends ignoring the edge's sense (`OrientedEdgeSense`, i.e. EDGE_CURVE same_sense) | ours | FTC-08 1,688 → 0, STC-10 1,493 → 0, STC-07 2,170 → 16; every FTC-08 edge's curve ends meet its vertices once sense is applied |
| Validator rejected loops closing at a sphere pole or cone apex (any U there is one point) | ours | CTC-04, STC-07, STC-08 → 0 |
| Validator ignored the file's declared distance accuracy at vertices | ours | FTC-11 vertices 2.5–3.75 µm off their line, file declares 5 µm; → 0 |
| Recovery took `atan2` noise for U at a pole or apex | ours | 276 exact curves (≤1e-6 mm on their surface) failed with claimed residuals to 0.115 mm |
| Recovery's last knot fell one ulp short of the trim, rejecting its own pcurve | ours | "requires a finite in-domain trim" on exact lines and B-splines |
| Exporter rewrote straight edges to run start→end but kept the pcurve's old parameterisation; with same_sense .F. the pcurve ran backwards | ours | 156 mm lift errors on reimport; hidden while the validator's false errors made the exporter omit pcurves |
| 3D curve off its surface beyond the file's declared accuracy | source | 23 coedges: CTC-04 50 µm (declared 5), FTC-11 14 µm, STC-07 5.2 µm, STC-10 5.9 µm |

With the validator fixes, all 16 exact-geometry NIST files validate with **zero**
diagnostics at their declared accuracy.

## Tolerant edges (follow-up, same day)

Decision: **measure, record, don't repair.** When recovery cannot meet the 1 µm policy but
the edge's measured curve-to-surface gap is within the file's declared distance accuracy
(10 µm when the file declares none), the pcurve is kept and qualified at its measured gap,
tagged `SourceTolerant`. Geometry is not touched. Beyond the declared accuracy the edge
stays unqualified.

- 9 of 16 exact NIST files now qualify fully (was 4). Recorded gaps: worst 34.6 µm on FTC-07
  (declared 127 µm). The 23 edges beyond declared accuracy still fail.
- Exports of a body with tolerant edges declare a distance uncertainty of at least its
  worst gap, so Aetheris never claims more precision than it holds. Bodies without tolerant
  edges declare nothing, as before.
- Tolerant traces are not written to STEP; readers re-derive them. Recovered traces on
  B-spline supports are not written either: once SO-101's recovery succeeded, one was
  written and came back outside the reread surface's domain (U −14.7). **Known issue**,
  not yet diagnosed (likely knot or parameter mapping between write and read).
- `BrepThroughHoleRevisionTests` expected the default CTC-01 import to leave trims missing;
  tolerant edges now do by default what its explicit source-accuracy policy did.

Still open: 6 exact CTC-04 curves at the cubic fitter's 1.9 µm limit (now tolerant, within
5 µm), and coedges on B-spline surfaces not measured in this audit.

## Effects

- E3 blend removals passing the full pcurve round trip: 67 → 305 of 308.
- FTC-11 through the InlineStep route (0.1 mm recovery allowance) now qualifies; its
  "not qualified" status in `ExplicitGdtTests` came from the validator bug. That test's
  unqualified-UV export path needs a genuinely unqualified specimen.
- NIST report hashes changed for FTC-08, STC-06 and STC-08: their exports now carry valid
  pcurves instead of omitting them.

Regression tests: `Step242PcurveAuditTests`.
