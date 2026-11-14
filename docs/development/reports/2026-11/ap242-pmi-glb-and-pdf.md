# GLB and PDF drawing from the PMI in an existing AP242 file

Goal: produce a 3D GLB with callouts and a 2D PDF drawing directly from the semantic PMI a third-party AP242 file
carries, in millimetres, tested on NIST CTC-01.

## What changed

- Schema reader (`Step242ForeignPmiReader`):
  - plain tolerance subtypes (`PERPENDICULARITY_TOLERANCE(...,(#51))`) now read their datum system. Before, CTC-01's
    perpendicularity lost datum A.
  - `ITEM_IDENTIFIED_REPRESENTATION_USAGE` with `SET_REPRESENTATION_ITEM` now associates faces. Datums B and C had
    no faces before.
  - a `DATUM_FEATURE` read through its datum is a mirror, not an unsupported record.
  - PMI transfer (re-export) carries those usages too, so the round trip keeps the datum faces it now reads.
- 3D callouts (`PmiDisplayProjection`):
  - requirements on several faces point at their largest plane;
  - `linear distance` between parallel planes is drawn between them;
  - angles are drawn in degrees;
  - sizes the file states without a value use the model nominal in parentheses;
  - text height scales up to 24 mm for large parts (it was capped at 8 mm, so a 800 mm part's labels were unreadable).
- `StepPmiPresentation` (new) and `aetheris mesh <file.stp> --format glb`: part plus callouts as GLB.
- Reference drawing (`drawing generate --reference-only`):
  - numbered balloons on views, and a PMI SCHEDULE table (ID, characteristic, requirement, datums, STEP entity);
  - accepts schema-read PMI from non-millimetre files, since that reader converts every measure;
  - only requires face association.

## Evidence

- CTC-01: 21 records. GLB has 21 callouts. The PDF has 6 sheets (overview, top/front/right balloons, two schedule sheets).
- CTC-03 (inch): the schedule reads `0.127` for a 0.005 in flatness and `Ø50.8 ±0.254` for a 2.000 ±0.010 in bore.
  The PDF contains no inch text.
- `StepPmiPresentationTests` (4 tests). The `DrawingGenerationTests` reference test was rewritten for balloons.
- NIST sweep:

  | Output | Succeeds on | Of |
  |---|---|---|
  | GLB | CTC-01, FTC-08-tg | 17 |
  | PDF | CTC-01, CTC-03, FTC-08 ×2, FTC-11, STC-09, STC-10 | 17 |

  Remaining failures name their cause:
  - composite and group shape aspects (CTC-04, FTC-07/10, STC-06/07);
  - records with no face association (CTC-02/05, FTC-06/09, STC-08);
  - advanced zone or datum parameters in the 3D path.

## Verdict

Success for CTC-01, both outputs. Meaningful progression for the corpus. The next blockers are composite shape-aspect
targets and a 3D callout for advanced parameters, each isolated by the diagnostics above.

## Follow-up: whole NIST corpus, balloons, authored placement

- Reader:
  - composite and group datum features are mirrors of their datums;
  - datum targets link to their datum through `FEATURE_FOR_DATUM_TARGET_RELATIONSHIP`;
  - tolerance parameters are read in mm: projected zone `P`, unequally disposed `U`, per unit length/area `/`;
  - common datums (`A-B`), `TYPE_QUALIFIER` (MAX/MIN/AVG) and symmetry/concentricity are read;
  - a value format qualifier (`NR2 1.3`, decimals in the file's own unit) does not apply to converted values and is
    ignored;
  - unread parameters are named in `PresentationGaps`. No NIST record has one now.
- Auxiliary geometry (`Step242AuxiliaryGeometry`) locates records attached to target points and isolated curves.
  Points outside the part's envelope (CTC-05 #957, a curve in another frame) are not trusted: the record is listed
  `NOT LOCATED`.
- 3D:
  - multi-face, non-planar and non-Z-hole targets point at their largest face;
  - a size on alike holes without a quantity is drawn as `NX`, and a size on holes of different diameters still
    rejects;
  - unlocated records go to the notes panel;
  - crowded callouts slide sideways before giving up.
- Drawing (`--reference-only`):
  - balloons placed by `DrawingBalloonLayout` on rings around the view (angular order, JudgmentEngine slot choice,
    crossing swaps);
  - leaders end in an arrow on edges, a dot on face-on surfaces, and a triangle for datums;
  - views fill the sheet;
  - the `REF` envelope dimensions moved to the notes.
- Authored placement: `Label` entries in the `Pmi` block (`record`, `view`, `side`, `at`), validated by the parser
  and applied by drawing generation. `#N` resolves through the imported STEP file.
- PPTX inspection counts slide by slide; concatenating a dense NIST drawing's slides ran out of memory under the
  parallel lane.
- Sweep: PDF 17/17, GLB 17/17. A callout count equal to the record count is checked on CTC-01/02/04/05,
  FTC-07/10.
- Fresh-eye review:
  - CTC-01 view sheets have no crossing leaders;
  - FTC-06 has 3, where many requirements share one boss;
  - leaders into the interior of dense parts still cross model edges, which is accepted since the anchors are interior.
