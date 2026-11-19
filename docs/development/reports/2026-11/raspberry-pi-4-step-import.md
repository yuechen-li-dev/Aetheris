# Raspberry Pi 4 Model B STEP import

Reported on Hacker News alongside the STEP Tools "Boxy" part: `raspberry_pi_4_model_b.step`, distributed by
step.parts. The file is 52 MB, schema AUTOMOTIVE_DESIGN, and the length unit is mm. It holds one product with 89
solids (`MANIFOLD_SOLID_BREP`) and 1978 surface models (`SHELL_BASED_SURFACE_MODEL`, 3190 open faces).

## Before

Single-part import correctly routes the file to the assembly path, because it has 89 rigid roots.
`asm import-step` stops at the first solid that fails, `#15`, with `Importer.Pcurve.Consistency`. Probing each solid
separately: 75 of 89 import, and all 14 failures are pcurve consistency failures.

## Causes found and fixed

1. **STEP cone v was read in Aetheris's apex convention.**
   - ISO 10303-42 puts a conical surface's v = 0 at its placement circle, where the radius is R. `ConeSurface`
     puts v = 0 at the apex.
   - Source pcurves on cones were read unshifted, so they were off by R / tan α. The exporter wrote them unshifted
     too: our own round trips agreed with themselves, but not with other readers.
   - Both directions now convert through `ConeSurface.StepVOffset`.
   - No corpus file had pcurves on a cone with R > 0, which is why tests never caught this.
2. **Some pcurves follow the right path at the wrong speed.** A pcurve must run at its edge's parameter: point t of
   the pcurve is point t of the edge. This file breaks that in three ways:
   - Circles on planes are written as three rational 120° arcs with knots in radians. A rational arc is not
     angle-uniform, so point t lands up to 0.039 rad along the circle from the edge's point. That is 0.117 mm at
     radius 3, which matches the reported deviation exactly.
   - Cone generators advance v by their slant length, not their axial length.
   - Some spline edges' pcurves are parameterized independently of the edge.

   The fix applies only when two things hold: the source pcurve's whole path lies on the edge's curve, and it
   covers the trimmed edge, on the same turn of a periodic surface. Then the importer replaces the pcurve with one
   built from the edge, the same way a missing pcurve is built. Its lift onto the surface is proved to stay on the
   edge. The import records a repair, `step-import-pcurve-reparameterized`. A pcurve whose path leaves the edge's
   curve is a real gap and is still refused.
3. **Extrusion surfaces could not be evaluated in the pcurve check.** The validator had no evaluator for
   `SURFACE_OF_LINEAR_EXTRUSION`, so faces on one were refused as `unsupported-evaluator`. It now evaluates them per
   ISO 10303-42: the directrix at u, moved v times the extrusion vector.

## After

79 of 89 solids import. The remaining 10 each have real gaps between the face geometry and its edges that are
larger than the accuracy the file declares (2e-5 mm), up to 9.4e-4 mm. For example, solid `#106936` has plane faces
whose edge vertices sit up to 4.5e-4 mm off the plane. An independent reader (springrep) also reads these solids as
tolerant.

Accepting them is a policy change: the importer would have to admit gaps larger than the file's own declared
accuracy. It is not made here.

## Also found

- **Surface models are dropped without a message.** The multibody path imports only the solids; the 1978 surface
  models are not reported.
- **Coverage.** No corpus file had pcurves on cones; the new tests extend the handcrafted cone instead
  (`Step242PcurveConventionTests`):
  - exact source pcurves, read with STEP's cone v;
  - an export, then a reread;
  - the rational circle with the slant-speed seam;
  - a seam pcurve on the wrong line, which is refused.

## Snapshot hashes

Six NIST files with cones now export their pcurves on cones in STEP's v. Their canonical hashes changed and were
updated in the shared snapshot and for Linux. The overrides for Windows (5 entries) and macOS (2 entries) are stale.
They must be regenerated on those platforms, so `NistCorpus_PerFile_AuditReport_MatchesSnapshot` fails there until
they are.

## Verdict (before the follow-up)

Meaningful progression. Two real importer defects are fixed, one of them a cone convention error that affected
export as well, and one missing evaluator is added. The file still does not import as a whole: the remaining
blockers are tolerance policy, plus the surface models that are dropped without a message.

## Follow-up: tolerant edges (option 2)

Policy: accept the measured gap and record it; never move geometry. Explicit healing, if it comes later, is a
separate step that reports what is wrong and asks before changing anything.

### What changed

- **A failed source pcurve is set aside.** When a source pcurve disagrees with its edge beyond the declared
  accuracy, it is treated as redundant evidence that has failed. It is set aside and rebuilt from its edge, the same
  way a missing pcurve is built. The edge's own gap to its surface is then measured.
- **Gaps within budget are kept as tolerant edges.** A gap up to the tolerant budget is recorded per trim. The
  budget is the larger of the declared accuracy and `BrepPcurveRecovery.TolerantGapFloorMillimetres`, which is 10 µm.
- **Anything else is refused.** If the rebuild fails, or the gap exceeds the budget, the original consistency
  failure is returned.
- **The 10 µm floor now also applies when an accuracy is declared.** Before, a file declaring a tight accuracy read
  worse than one declaring none. This now also covers exact constructions such as plane projections, whose trace
  always exists, so the gap shows only in the lift residual.
- **Disclosure.** Imports now report two things:
  - `ImportQualification.Repairs` gains `step-import-source-pcurves-rebuilt`;
  - `ImportQualification.Tolerances` gains `tolerant-edges`: the edges built here whose gap exceeds the declared
    accuracy, how many there are, and the largest gap.

  `analyze` prints both. `asm import-step` lists them per definition with `--json` and prints a count without it.
- **Surface models are reported.** The multibody path now warns that its 1978 surface models were not imported
  (`Importer.Assembly.SurfaceModelsNotImported`).

### Fixed while there

- **Swept faces were oriented backwards.** Faces on `SURFACE_OF_LINEAR_EXTRUSION` or `SURFACE_OF_REVOLUTION` have
  no closed-form loop projection, so they fell back to "aligned". Two newly admitted solids exported with eight and
  two such faces flipped, and an independent reader rejected their loops. These faces now take their side from the
  source's same_sense, like B-spline faces.
- **The recovery residual threw on an unknown surface.** It now shares the pcurve check's evaluator, which knows
  linear extrusions.

### Evidence

- **All 89 solids import, every one Qualified.** `asm import-step` writes all 89 components.
  - 56 carry disclosed repairs.
  - 10 carry tolerant edges: exactly the 10 that failed before, with 392 tolerant edges, the largest gap 1.03 µm.
- **An independent reader (springrep) accepts all 89 exported components with 0 issues.** It compared each one with
  the same solid split out of the source file:
  - **87 of 89 match exactly:** the same faces, edges, area and volume.
  - **`#106936` volume differs by 3.7e-3 mm³ (1.4e-6 relative).** This is the solid with the largest gaps; its
    tolerant faces' area depends on which trim a reader integrates.
  - **`#76506` volume differs by 1.3e-5 mm³.**
  - **Face and edge counts match on all 89.**
- **Tests (handcrafted cone):**
  - a wrong source seam pcurve is rebuilt and disclosed;
  - a 5 µm gap is accepted, disclosed and measured as 5 µm (nothing moved);
  - a 50 µm gap is refused.

- **NIST:** FTC-11 declares 5 µm. Two more of its trims, at 5.2 and 8.3 µm, are now kept as tolerant edges; one at
  14 µm still fails. Five NIST canonical hashes changed. None of the five has a platform override, so the shared
  snapshot covers every platform.

Verdict: success. The reported file imports in full, with every compromise disclosed. Surface models remain out of
scope and are reported as such.
