# Assertion witnesses

| Source | Normal admission | Evidence |
| --- | --- | --- |
| `box.firmament` | Accepted | Exact 480 mm³, world bounds [-5,-4,0]–[5,4,6] mm, 6/12/8/1 face/edge/vertex/shell counts |
| `through-hole-plate.firmament` | Accepted | 80×50×8 mm stock minus an 8 mm through-hole: 32000−128π mm³, unchanged outer bounds and one shell |
| `sphere.firmament` | Accepted | Analytic 4π·10³/3 mm³ lies inside the bounded SDF interval; ±200 mm³ assertion encloses it |
| `sphere-tight.firmament` | Accepted | A ±4 mm³ assertion with bounded SDF integration |
| `two-through-holes.firmament` | Accepted | Every final hole participates in the SDF: 32000−256π mm³ |
| `imported-plate.firmament` | Harness asset required | Imports the exported plate and its dependency lock; has no retained construction field |
| `two-bodies.firmament` | Rejected | Existing multiple-material-body admission gate rejects independent outputs before assertion evaluation; never measure Second under First's name |

The tests mutate expected values, fields and topology to prove rejection without
changing these witnesses. `measure` explicitly builds without evaluating declared
assertions and reports that level; it is not a successful validation substitute.

Six historical curved-fillet specimens in `../CanonicalGeometry/` now use
`Assert Topology Body { shells: 1 }` in place of unprovable historical volume
assertions. They compile normally and retain their existing deterministic STEP,
topology and geometry regression checks. A separate negative test appends an
unprovable volume assertion and still requires `measurement-unavailable` rejection.
Current behavior is documented in [checks](../../../docs/public/reference/language/checks.md).

To reproduce the imported-plate measurement from the repository root:

```powershell
New-Item -ItemType Directory -Force artifacts/local/sdf-final-body | Out-Null
aetheris build fixtures/Regression/Assertions/through-hole-plate.firmament --out artifacts/local/sdf-final-body/plate.step
Copy-Item fixtures/Regression/Assertions/imported-plate.firmament artifacts/local/sdf-final-body/imported-plate.firmament
aetheris lock artifacts/local/sdf-final-body/imported-plate.firmament --update
aetheris measure artifacts/local/sdf-final-body/imported-plate.firmament --sdf --error 20 --json
```

The harness uses the same source through in-memory resources and supplies the
matching lock. No pin or geometry-only import rule is bypassed.
