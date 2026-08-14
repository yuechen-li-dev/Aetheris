# CTC-03 M4 flat comparison

CTC-03 M4 flat output is valid in the compatibility region model and exports physical STEP/SVG. The new strict exact-single-blank composition rejects this hostile multi-relief/nested graph with typed dangling-fragment/angular-order diagnostics, so CTC-03 remains fabrication `NeedsReview`.

M3 -> M4 comparison metrics are unchanged:

- generated size: `392.05179 x 612.59776 mm`;
- source dimension residuals: width `12.702447 mm`, height `12.707820 mm`;
- contour RMS / p95 / max: `52.776255 / 128.113679 / 128.113679 mm`;
- cuts: both `Pass`;
- bend-line count delta: `0`;
- overlap: `false`;
- comparison status: `Fail`, overall intent status `NeedsReview`.

This is an honest non-improvement in historical parity. M4 localizes the problem more sharply: exact line/arc removal works for bounded corner fixtures, while simultaneous CTC-03 corner removals and nested service/mounting flanges leave an unresolved arrangement graph. The retained M4 STEP/SVG are review artifacts, not approved tooling output.
