# Frontend X0 source witnesses

These are reconstructed minimal specimens from Claude's 2026-10-13 audit, not the
original twelve complete programs. The supplied report includes a scoreboard and
fragments but no full source attachments. Its original text is preserved in
`docs/development/firmament-authoring-pass-2026-10.md`.

Run the real CLI with `validate <fixture> --json` and `build <fixture> --output
artifacts/local/frontend-x0/<name>.step --json`. Negative tests additionally use
the public in-memory engineering compiler. Output and detailed baseline/after
records belong under ignored `artifacts/local/frontend-x0/`.

| Specimen | Purpose |
| --- | --- |
| malformed-v2 | Failed parse cannot validate successfully or fall through to V1 |
| unknown-model-statement | Model-level unknown Fillet statement |
| unknown-modify-statement | Modify-level unknown Fillet statement |
| unknown-box-field | Unknown dimension field must not disappear |
| unsupported-xz-layout | Asymmetric six-edge L outline, 60 x 30 x 3 mm; XY must not substitute for XZ |
| combined-fillet-hole | Parsed features rejected at the actual capability boundary |
| unresolved-semantic-source | Selection binding must preserve AbsentEdge and its explanation |
| edgefinish-generic-variant | Unsupported generic spelling must not validate as build-admitted |
| through-pocket | Finite Pocket reaching through the stock must suggest the through-cut vocabulary |
| unknown-compose-statement | **Open defect:** advanced Compose ignores nested Fillet |
| unknown-boundary-field | **Open defect:** boundary lowering discards heigth before Compose admission |

The last two remain reproduction evidence, not accepted language forms. Do not
turn their current successful compilation into a compatibility requirement.

## Twelve-part audit coverage map

Original authoring success rates cannot be rerun without the original sources.
Existing repository witnesses and the new minimal cases cover these families:

| Audit part | Repository witness / limitation |
| --- | --- |
| 1 Plate, holes, fillet | combined-fillet-hole; canonical Features/Composition/box-counterbore-chamfer |
| 2 L-bracket | unsupported-xz-layout; canonical ProfileComposition L-bracket regressions |
| 3 Revolved bushing | canonical Revolve corpus; schema Mechanical owns Revolve |
| 4 Spacer family | canonical Templates; existing Record/Template regression suite |
| 5 Grid plate | existing FirmamentV2TemplatePatternTests and ConceptPoint3HoleTests |
| 6 Enclosure tray | canonical Features/Pocket/rectangular-pocket; through-pocket |
| 7 Datum assembly | canonical AssemblyInterfaces corpus |
| 8 Spring and hook | canonical WireForm/helix; WireForm regressions |
| 9 NEMA17 plate | existing FeatureFunctionTests and NamedSetTests; no original plate source |
| 10 Bolt-circle flange | canonical Symmetry/radial-bolt-circle |
| 11 Bent tube | canonical Sweep corpus |
| 12 Slot/counterbore/chamfer | canonical Features and ProfileComposition regressions; no original block source |
