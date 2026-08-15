# Sheet Metal M8 — CTC-03 intent recovery

## Verdict: Meaningful progression

M8 removes the previous missing-opening blocker. The canonical NIST CTC-03 STEP now recognizes 15 holes, two slots, seven bends, and 15 planar regions. The source-independent Firmament program regenerates the same opening inventory and all 17 opening comparisons pass. It also models the partial-span service flange and its 101.6 x 12.7 mm outer tab.

Profile-M2 now reconstructs the complex central front and rear mounting-flange free edges through generic semantic edge attachment. It is still not a full CTC-03 reconstruction: endpoint corner ownership, left-wall tapered ends, and right-wall attachment cutbacks remain simplified. The comparison remains `NeedsReview`, but source-to-generated formed RMS improves from 19.4627 to 10.6140 mm, p95 from 52.4846 to 19.0713 mm, and flat height residual from 12.7078 to 0.00782 mm. The isolated next blocker is cross-edge corner composition, not single-edge fragment placement or hole recognition.

## Delivered mechanism

The authored compiler now has a bounded semantic-layout pass between parsing and exact profile lowering:

```text
Concept / Concept Struct
  -> Datum, Pattern, Tab, Require
  -> resolved SheetMetalSemanticLayout
  -> stable generated cut paths + validated constraints
  -> exact formed profiles and PlanarContour2 flat blank
```

`Datum` names local workpoints. `Pattern` owns size, count, pitch, and generated member identities. `Tab` extends a bounded partial flange edge. `Require` validates required members and explicit equal-size, equal-pitch, or mirror claims. Pattern resolution also records equal-size and equal-pitch constraints automatically. The CLI exposes the resolved layout under `sheetMetal.semanticLayout` and reports `semanticResolve` timing.

The M8 CTC program resolves three datums, nine patterns, one tab, 17 generated cuts, and 18 admitted pattern constraints. Stable paths include `Ctc03Layout.BaseFastenerPattern[0]` and `Ctc03Layout.ServiceInnerHoles[1]`.

## Evidence map

- [Final source](ctc03-final.firmament)
- [Feature and missing-feature inventory](ctc03-feature-inventory.md)
- [Semantic layout and real syntax](ctc03-semantic-layout.md)
- [Formed and flat comparison](ctc03-comparison.md)
- [PMI and datum audit](ctc03-pmi-parity.md)
- [Iteration and LLM friction](ctc03-iteration-journal.md)
- [Validation, performance, hashes, and code-quality verdict](validation-report.md)
- [Independent formed STEP](ctc03-formed.step)
- [Independent flat STEP](ctc03-flat.step)
- [Independent flat SVG](ctc03-flat.svg)

The non-CTC generalization fixture is [`m8-semantic-panel.firmament`](../../../../../fixtures/FirmamentV2/SheetMetal/m8-semantic-panel.firmament).

## Direct architecture answers

**Can Aetheris fully reconstruct CTC-03 as the actual manufactured part?** No. It now reconstructs the complete recognized bend/opening skeleton and the distinctive service tab, but not every manufactured edge trim.

**Did the semantic sketch reduce reconstruction mistakes?** Yes. Named datums and regular patterns replaced repeated point edits; contradictions are rejected before topology construction; and the final feature comparison reached 17/17 pass without post-lowering feature matching hacks.

**Did it generalize?** Yes, within its bounded scope. The independent semantic-panel fixture uses the same datum/pattern lowering and stable paths, and tests prove deterministic output.

**Largest remaining blocker?** Bounded corner programs that may consume both an owning edge endpoint and the adjacent edge endpoint while preserving one explicit corner owner. Single-edge fragments now compose deterministically and retain correspondence; the remaining CTC wall tapers and mounting-flange end chamfers cross that seam.
