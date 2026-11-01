# PMI showcase: dogfooding PMI on a real machined part

**Goal.** Specify a composed machined part (boss, pockets, bore, counterbored holes,
chamfer) completely enough to make it without a drawing, and present it as an
annotated GLB. Use the attempt to find out whether the PMI system is usable, not just
technically present.

**Part.** `fixtures/Canonical/PMI/showcase-bearing-mount.firmament`, built by
`demos/Aetheris.PmiShowcase` into STEP AP242 and GLB. 28 semantic records: 3 datums,
17 sizes and locations, 5 geometric controls, 3 notes.

## What blocked a complete specification, and what changed

| Found | Change |
|---|---|
| `dimension:` never bound on Compose bodies (Statics were not passed to the Compose PMI binder) | Bound Statics flow to every mechanical construction route |
| No way to target a boss top or pocket floor; `face(+Z)` is ambiguous on a stepped part | `face(op.Top)`, `face(op.Bottom)`, `face(pocket.Floor)` |
| Datums on faces split by construction exported **unattached**, silently | Axis faces resolve to the extreme support plane (all coplanar pieces); an authored datum with no face rejects |
| No quantity: one hole annotated, or four separate callouts | Hole lists: one requirement with its quantity, on every shaft wall |
| Position on a counterbored hole also associated the counterbore wall | Hole sizes and controls apply to the shaft only |
| No part notes (material, general tolerance) | `Note` |
| Sizes written only in an Aetheris-private AP242 form | Also written as standard `DIMENSIONAL_SIZE`/`DIMENSIONAL_LOCATION` + `PLUS_MINUS_TOLERANCE`; readers count once |
| No way to state block, boss, pocket, counterbore or chamfer sizes, or feature locations | `Dimension` (construction parameters, values from the built body), `Location`, `HoleCallout` |
| `face(h.Wall)` rejected on Compose holes with a bare code | Accepted (the shaft); PMI record errors are located at the record and explained |
| Hole-wall strips tessellated as 32×256 grids (chord tolerance read as a parameter step) | Straight direction of a rectangular trim is one cell; the curved direction keeps its edge samples. GLB 95 MB → 2.6 MB |

Presentation was rewritten: callouts grouped per feature and placed in annotation
planes with collision avoidance (including in the default view's projection),
Y14.5-shaped frames and datum symbols drawn as vectors, filled text on panels, B-rep
edges over a translucent body, per-feature leader colours, sizes on their features,
overall dimensions outside the part, baseline locations.

## Evidence

- Showcase readback: every record associated with faces; display draws all 28.
- Standard form checked independently: with the native records renamed away, the
  foreign-PMI reader recovers each diameter, tolerance and location from the
  standard entities alone (`PmiShowcaseTests.Sizes_AreAlsoWrittenInStandardAp242Form_AndReadOnce`).
- Full lane passes. Drawing generation accepts the standard entities that restate a
  native record (`StandardMirrors` on the inspection); it still rejects foreign ones.

## Verdict

Meaningful progression toward success: the part is fully specified by its model and
the GLB reads as an engineering presentation.

## Stopped or deferred, with reasons

- **Rounded stock corners + boss + chamfer: works, after one gate change.** Arc outlines
  route to the rim planner (the chimera route: exact plane and cone chamfer patches),
  which grafts the source-planned cavities back into the finished skin. It admitted only
  through round holes. The graft was already generic, so it now retains anything hanging off
  the stock caps' inner loops (boss, blind pockets, counterbores), provided every
  footprint clears the rim corridor and no retained face reaches the stock's side walls.
  The showcase keeps the sharp outline; the rounded variant is covered by
  `PmiShowcaseTests.RoundedStock_RimChamferRetainsBossPocketsAndCounterbores`. The
  section-stack route stays line-only (inset, variable-outer interval, emitter).
  Not projected: the stock corner radius as a `Dimension`.
- **Standard form for quantity lists.** `2X` pocket lengths, depths and the chamfer
  are written in the native form only; single-feature sizes and all locations are
  written in both. A list needs one standard dimension per feature.
- **Basic flag in AP242.** `basic: true` is native and presentation-only; the standard
  qualifier for basic dimensions is not written yet.
- **Not projected yet:** fillet radius, countersink callouts, locations between two
  features (only from an axis face).
- **Helios** draws PMI from `Lines`; glyph outlines remain there, so it keeps showing
  text, but it does not yet use the filled text, panels or leader colours.
- **NIST STEP File Analyzer** was not run (Windows-only); confirm the standard
  entities with it before claiming conformance.
