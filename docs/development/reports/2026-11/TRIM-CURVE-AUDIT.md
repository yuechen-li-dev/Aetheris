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

## Remaining recovery failures (policy, not bugs)

- 201 coedges whose source curves sit 2–35 µm off their surfaces, within each file's
  declared accuracy. Recovery qualifies at a fixed 1 µm, so it demands more than the file
  promises. Deciding whether to qualify against `max(policy, source accuracy)` is a policy
  choice.
- 6 exact curves on CTC-04 fit to 1.9 µm against 1 µm (cubic fitting convergence).
- 89 coedges on B-spline surfaces were not measured in this audit.

## Effects

- E3 blend removals passing the full pcurve round trip: 67 → 305 of 308.
- FTC-11 through the InlineStep route (0.1 mm recovery allowance) now qualifies; its
  "not qualified" status in `ExplicitGdtTests` came from the validator bug. That test's
  unqualified-UV export path needs a genuinely unqualified specimen.
- NIST report hashes changed for FTC-08, STC-06 and STC-08: their exports now carry valid
  pcurves instead of omitting them.

Regression tests: `Step242PcurveAuditTests`.
