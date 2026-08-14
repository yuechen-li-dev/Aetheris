# M3 validation report

- Focused `Aetheris.SheetMetal.Tests`: 23/23 pass.
- Focused Sheet Metal CLI tests: 5/5 pass.
- L-bracket: one bend; formed/flat STEP reimport and reference refold pass.
- U-channel: two bends; formed/flat STEP reimport and reference refold pass.
- Electronics tray: four bends, four cuts, four corners/reliefs; formed body 56 faces / 128 edges / 76 vertices; formed/flat STEP reimport and refold pass.
- CTC-03 formed: one body/shell, 78 faces, 172 edges, 96 vertices; 64 planes and 14 cylinders; enclosed-manifold after STEP reimport.
- CTC-03 flat: 392.051790 x 612.597761 x 1.905 mm; one body/shell, 50 planar faces, 144 edges, 96 vertices; enclosed-manifold after STEP reimport.
- Throw-away-source gate passes from an isolated Firmament copy.
- CTC-03 post-generation comparison: overall `NeedsReview`; all bends/cuts pass, global historical trims/flat contour do not.

Full solution restore succeeds; full build succeeds with zero warnings/errors; 2,836 discovered tests pass. `Aetheris.FrictionLab.Tests` contains no discoverable tests, as before. No generic Boolean union was added to Sheet Metal lowering.
