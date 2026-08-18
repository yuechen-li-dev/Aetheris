# AETHERIS-SHEETMETAL-M4 evidence

M4 reaches **meaningful bounded production capability**, not full commercial parity. The shared line/arc contour kernel, exact profile composition, stable semantic paths, template generators, and exact single-corner relief cases work through real formed/flat/STEP paths. CTC-03 remains `NeedsReview`, and simultaneous multi-corner relief plus nested-chain arrangement is the isolated next blocker.

## Delivered

- Shared `PlanarContour2` outer/inner loop IR with stable IDs, plane frame, native line/arc/circle geometry, and provenance.
- Public bounded intersection, split, trim, validation, explicit-side offset, and known-topology arrangement operations.
- Exact flat region, cut, relief, and (when admitted) composed blank contours; analytic arcs survive SVG and flat STEP.
- Typed `Open`, `Mitered`, `RectangularRelief`, and `RoundRelief` corner records.
- Canonical base/flange/formed/flat Concept Paths and `aetheris sheetmetal paths` inspection.
- Capability-aware invalid-member diagnostics.
- Module-owned `LBracket`, `UChannel`, and `FourWallTray` generators that emit ordinary Sheet Metal semantics.
- PSU enclosure authoring fixture and semantic-only DFM repair fixture.
- Exact-blank/relief and minimum-flange DFM rules with deterministic suggestions.

## Evidence map

- [Contour audit and API](contour-kernel.md)
- [Corner and relief behavior](corner-relief.md)
- [Concept Paths and authoring](concept-path-ergonomics.md)
- [Templates and dogfood](templates.md)
- [CTC-03 formed comparison](ctc03-formed-comparison.md)
- [CTC-03 flat comparison](ctc03-flat-comparison.md)
- [DFM](dfm-summary.md)
- [Validation, performance, determinism](validation-report.md)

Retained generated artifacts:

- `psu-enclosure-formed.step`, `psu-enclosure-flat.step`, `psu-enclosure-flat.svg`
- `ctc03-m4-formed.step`, `ctc03-m4-flat.step`, `ctc03-m4-flat.svg`

## Architecture verdict

**Does Aetheris now have a sufficiently exact 2D contour/corner kernel?** Yes for bounded common line/arc profiles, open/miter topology, individual rectangular/round corner reliefs, cuts, and ordinary exact blank composition. No for the full M4 ambition: multiple interacting relief removals on four-wall/nested hostile graphs can still be rejected by arrangement angular ordering, and curved formed relief walls are not yet emitted.

**Are Concept/Concept Path ergonomics good enough?** Yes for the current authored family. `Main.Front`, `FrontWall.Outer`, `FrontWall.Bend`, and their `Flat.*` counterparts are stable and inspectable; wrong members and capabilities produce useful diagnostics. The same resolved Profile/contour substrate is used outside Sheet Metal.

**Do templates demonstrate parameter-first authoring?** Yes. L brackets, U channels, and four-wall trays lower through ordinary semantics and preserve path shape across specializations. They are a C# module API today, not yet first-class generic declarations in Firmament source.

**Largest blocker:** robust multi-corner seam/relief arrangement and corresponding exact formed trim-wall materialization, especially when several corner removals meet nested flanges or ambiguous historical contours. This is narrower than M3’s generic “exact 2D kernel” blocker and is backed by typed `sheetmetal-exact-blank-contour` diagnostics on the M3 four-relief tray and CTC-03.
