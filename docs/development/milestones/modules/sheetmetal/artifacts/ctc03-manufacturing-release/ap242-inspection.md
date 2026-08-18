# CTC-03 AP242 semantic inspection evidence

Artifact: `ctc03-manufacturing-ap242.step`

SHA-256: `29A02DBDCFF8CED5C33AFA953D2C61321D6547578BA22B661D0621D025201E7E`

## Independent STEP reimport

`aetheris analyze ctc03-manufacturing-ap242.step --json` reparsed the generated Part 21 data through the ordinary STEP importer.

| Check | Result |
| ----- | ------ |
| Bodies / shells | 1 / 1 |
| Faces / edges / vertices | 129 / 306 / 198 |
| Structural assessment | enclosed-manifold |
| Analytic surfaces | 86 planes, 43 cylinders, no unsupported surfaces |
| Analytic curves | 220 lines, 86 circles, no unsupported curves |
| Exported length unit | SI millimetre (`.MILLI.,.METRE.`) |

## Semantic record reinspection

The semantic inspector reads the exported STEP text, not the bound Firmament objects.

| Record class | Reinspected count | Representative retained content |
| ------------ | ----------------: | ------------------------------- |
| Datum | 3 | A -> MainDeck, B -> FrontWall, C -> LeftWall; one face association each |
| Dimension/diameter | 13 | 4x dia 16 +/-0.15 base holes; 2x 20 x 90 +/-0.20 slots; 2.0 +/-0.12 thickness |
| Geometric tolerance | 5 | Front/rear position 0.8 A|B|C; service position 0.6 A|B|C |
| Annotation | 8 | global process notes plus deck, mount-hole, and bend-associated instructions |

Resolved face-association examples:

- Base fastener diameter -> four exported cylindrical `ADVANCED_FACE` entities.
- Front mounting position -> two exported cylindrical `ADVANCED_FACE` entities.
- Protect-datum-A note -> one exported planar `ADVANCED_FACE` entity.
- Service-cut-before-form note -> two exported bend-cylinder `ADVANCED_FACE` entities.

The STEP uses formal `DATUM_FEATURE`, `DATUM`, complex `POSITION_TOLERANCE`, `DATUM_SYSTEM`, `DATUM_REFERENCE_COMPARTMENT`, dimensional representation items, and `GEOMETRIC_ITEM_SPECIFIC_USAGE`. It contains no `ANNOTATION_PLANE`; view orientation was not promoted into engineering semantics.

Inspector result: success, zero semantic diagnostics.

## Determinism

Two consecutive real CLI builds produced the same AP242 SHA-256:

`29A02DBDCFF8CED5C33AFA953D2C61321D6547578BA22B661D0621D025201E7E`

The automated test independently exports twice in memory and asserts byte-for-byte equality, STEP reimport success, record counts/content, actual face associations, and the absence of graphical annotation-plane state.
