# Assertion witnesses

| Source | Normal admission | Evidence |
| --- | --- | --- |
| `box.firmament` | Accepted | Exact 480 mm³, world bounds [-5,-4,0]–[5,4,6] mm, 6/12/8/1 face/edge/vertex/shell counts |
| `through-hole-plate.firmament` | Accepted | 80×50×8 mm stock minus an 8 mm through-hole: 32000−128π mm³, unchanged outer bounds and one shell |
| `sphere.firmament` | Accepted | Analytic 4π·10³/3 mm³ lies inside the bounded SDF interval; ±200 mm³ assertion encloses it |
| `two-bodies.firmament` | Rejected | Existing multiple-material-body admission gate rejects independent outputs before assertion evaluation; never measure Second under First's name |

The tests mutate expected values, fields and topology to prove rejection without
changing these witnesses. `measure` explicitly builds without evaluating declared
assertions and reports that level; it is not a successful validation substitute.

Six historical curved-fillet specimens in `../CanonicalGeometry/` now use
`Assert Topology Body { shells: 1 }` in place of unprovable historical volume
assertions. They compile normally and retain their existing deterministic STEP,
topology and geometry regression checks. A separate negative test appends an
unprovable volume assertion and still requires `measurement-unavailable` rejection.
See `docs/development/FIRMAMENT-ASSERTIONS-2026-10-19.md` for the earlier classification.
