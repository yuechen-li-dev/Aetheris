# CIR-M1: Experimental Firmament-to-CIR Lowering

CIR-M1 adds an **experimental** side-by-side lowerer from existing Firmament primitive-lowering plans into CIR trees.

## Supported subset (M1)

- Primitives: `box`, `cylinder`, `sphere`
- Booleans: `add`, `subtract`, `intersect`
- Placement: `place.on: origin` with simple `place.offset[3]` translation

## Unsupported in M1

Anything outside the subset above is rejected with explicit CIR-lowering diagnostics, including:

- `cone`, `torus`, prism/slot families
- `draft`, `chamfer`, `fillet`
- `library_part`, rounded-corner-box
- selector-based semantic placement anchors

## Important boundary

This does **not** replace production Firmament execution.

- Production path remains: Firmament -> primitive lowering -> BRep execution -> STEP export.
- CIR path is internal/test-facing for point classification and approximate volume only.
- No CIR->BRep materialization is added in M1.

## Recommended M2

- Expand placement lowering to a bounded selector subset with explicit reference-frame tests.
- Add differential tests comparing CIR-vs-BRep containment/approx volume on shared fixtures.
- Keep unsupported diagnostics first-class while growing subset coverage incrementally.
