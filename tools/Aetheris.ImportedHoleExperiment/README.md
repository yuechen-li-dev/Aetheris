# Imported-hole qualification harness

This repository-local tool consumes the production `BrepThroughHoleRevision`
owner. It does not contain a second surgery engine or parse Firmament. Use the
[golden Firmament fixture](../../fixtures/Regression/InlineStep/production/ctc01-hole-move.firmament)
for the complete source pin, explicit geometry-only import, structured diagnostics
and replacement-hole semantic PMI path.

From the repository root in PowerShell:

```powershell
dotnet build tools/Aetheris.ImportedHoleExperiment -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet tools/Aetheris.ImportedHoleExperiment/bin/Release/net10.0/Aetheris.ImportedHoleExperiment.dll testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp qualify artifacts/local/imported-hole-production/kernel-cases
```

`qualify` is a CTC-01-specific witness, using the source's 0.01 mm accuracy. It
checks three accepted cases (30 mm move, overlapping 5 mm move and no-op) and five
refusals (collision, breakout, non-hole, blind hole and intersecting lobe). Each
success must export/reimport as qualified. Input topology and geometry must remain
unchanged after every case. An unexpected acceptance/failure exits nonzero.

Other operations:

```powershell
# Inspect cylindrical face geometry, source entities and adjacency.
dotnet tools/Aetheris.ImportedHoleExperiment/bin/Release/net10.0/Aetheris.ImportedHoleExperiment.dll testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp inventory

# Move the complete coaxial group containing sequential face 1 by +30 mm X.
# CTC requires the explicit source accuracy budget; the normal default is .001 mm.
dotnet tools/Aetheris.ImportedHoleExperiment/bin/Release/net10.0/Aetheris.ImportedHoleExperiment.dll testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp move 1 30 0 artifacts/local/imported-hole-production/single --import-tolerance-mm 0.01
```

`move` writes `replace.step` and `report.json`; `qualify` also writes `cases.json`.
Outputs default to ignored `artifacts/local/imported-hole-experiment/`. These are
geometry-only kernel witnesses, not annotation-preserving manufacturing releases.
The auto-generated 2.5 mm stock padding belongs to this fixture harness; production
Firmament requires the author to declare the `within` envelope.

The initial family is an isolated, constant-diameter Z-axis round through-hole
between two planar supports. Conservative whole-support bounds reject unproved
clearance. Unknown support/curve bounds, blind/intersecting/blended/threaded holes,
resizing and other frames reject. The kernel validates intermediate suppression,
final incidence, pcurves and export preflight. No general Boolean fallback occurs.

CTC now qualifies all 636 trims at its declared 0.01 mm accuracy. A cone-apex chart
fix repairs four singular generators; remaining strict .001 mm failures reflect
actual source curve/support discrepancies around .0034 mm. Geometry is not refit.
See the [production closeout](../../docs/development/reports/2026-10/IMPORTED-HOLE-REVISION-PRODUCTION-2026-10-20.md).
Original dual-strategy experiments are retained as historical reports, rather
than executable alternative production implementations.
