# AETHERIS-FEA-M5 verification package

This directory is generated from `plate-with-hole.firmament` with:

```powershell
dotnet run --project Aetheris.CLI -- fea docs/fea/artifacts/m5/plate-with-hole.firmament --out-dir docs/fea/artifacts/m5 --json
```

`analysis-ir.json` is normalized engineering intent, `native-results.json` contains sparse/solver/equilibrium/performance evidence, `residual-history.json` contains PCG residuals, `sparse-system-metrics.json` isolates assembly metrics, `displacement-stress-summary.json` provides comparison values, and `verification.inp` is the conventional C3D8 Abaqus deck.

For Abaqus/Standard, open or submit `verification.inp`, run the `LINEAR_STATIC` step, and compare `U`, `RF`, `S`, and `E` with the JSON evidence. Expected canonical native values are approximately 10.162 µm maximum displacement, 15.683 MPa maximum cell-center von Mises stress, and 10 kN total reaction. The Abaqus mesh is intentionally different: partial Cut cells are omitted.

The `convergence` directory contains coarse and fine source/runs. The measured table is in `docs/fea/m5-linear-elasticity.md`. No Abaqus execution has been performed. The requested rotated orientation case is not present because general oriented semantic-face load integration is not yet implemented; this is a declared M5 limitation, not missing evidence presented as success.
