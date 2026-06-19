# STEP-AP242-HARDEN-X2: FTC-07 Import Hang House Call

## Problem statement

FTC-07 was reported as a local "import hang" when loading `testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp`.

The primary question for this house call was whether the hang lived in:

- STEP/AP242 parsing/import,
- BRep/topology materialization,
- canonical export,
- server API orchestration, or
- UI/view materialization.

## FTC-06 / FTC-07 distinction

FTC-06 and FTC-07 are separate issues.

- FTC-06 was the curved `ADVANCED_FACE.same_sense` preservation regression and remains covered as a regression smoke.
- This house call is FTC-07 only.
- No new general NURBS support, no broad AP242 rewrite, and no FTC-06 behavior changes were introduced here.

## Reproduction commands

Fixture:

```powershell
testdata\step242\nist\FTC\nist_ftc_07_asme1_ap242-e2.stp
```

Core importer / exporter:

```powershell
dotnet run --project Aetheris.CLI -f net10.0 --no-build -- analyze testdata\step242\nist\FTC\nist_ftc_07_asme1_ap242-e2.stp --json
dotnet run --project Aetheris.CLI -f net10.0 --no-build -- canon testdata\step242\nist\FTC\nist_ftc_07_asme1_ap242-e2.stp --out artifacts\ftc07\nist_ftc_07_canonical.step --json
dotnet run --project Aetheris.CLI -f net10.0 --no-build -- analyze testdata\step242\nist\FTC\nist_ftc_07_asme1_ap242-e2.stp --face 9 --json
```

Server/API isolation:

```powershell
dotnet Aetheris.Server\bin\Debug\net10.0\Aetheris.Server.dll --urls http://127.0.0.1:5142
```

Then:

1. `POST /api/v1/documents`
2. `POST /api/v1/documents/{id}/import/step`
3. `POST /api/v1/documents/{id}/bodies/{occurrenceId}/display/prepare`

## Layer where the hang occurs

The hang does not occur in core STEP import.

Observed layer breakdown:

- `analyze` completed successfully in about 1 second.
- `canon` completed successfully in about 1.2 seconds.
- `POST /import/step` completed successfully in about 200 ms.
- `GET /documents/{id}` completed successfully in about 22 ms.
- The old local hang appeared when the UI/server advanced into display preparation.

Current classification:

- `ViewMaterializationHang`
- More specifically: bounded server-side display tessellation failure during planar face materialization.

This is not a raw AP242 kernel importer hang.

## Root cause found

FTC-07 imports into kernel/BRep successfully, but display preparation can stall while tessellating a planar multi-loop face.

The bounded diagnostic now reports:

```text
Display tessellation exceeded the bounded execution budget after ~5000 ms while processing face 9 on surface 'Plane' during phase 'PlanarTriangulationWithHoles'.
```

Supporting face inspection:

- `aetheris analyze ... --face 9 --json` reports `surfaceType = Plane`
- face 9 has 20 adjacent edges
- bounding box is planar at `y = -4.75`

So the FTC-07 local "import hang" was actually the viewer/display path blocking on planar hole triangulation for face 9, not STEP parse/import/export.

## Instrumentation added

Bounded execution diagnostics were added to display tessellation:

- per-run display tessellation execution budget
- timeout diagnostic category: `Viewer.Tessellation.Timeout`
- diagnostic message includes:
  - face id
  - surface kind
  - phase
  - elapsed milliseconds

Bounded phases now cover:

- top-level face tessellation
- trimmed-surface sampling/classification lanes
- planar loop flattening
- planar primary-loop selection
- planar triangulation with holes

## Fix applied

This milestone takes the safe bounded-diagnostic path rather than pretending FTC-07 view materialization is healthy.

Applied changes:

- display tessellation now runs under a bounded execution budget instead of hanging forever
- FTC-07 face 9 planar hole triangulation returns a precise timeout diagnostic
- server `display/prepare` returns a bounded failure instead of stalling indefinitely
- the client import flow no longer masquerades a display/materialization failure as an AP242 import failure

Client behavior now distinguishes:

- import succeeded
- view materialization failed

## UI/view-specific conclusion

Kernel import is not the blocker for FTC-07.

If FTC-07 fails locally after import, the active blocker is view/display materialization, specifically planar face hole triangulation during display preparation. The UI previously chained import directly into display preparation and could present that stall as if import itself had hung.

## Tests added

Core:

- `Step242Ftc07ViewMaterialization_CompletesOrReportsBoundedDiagnostic`
- `Step242Ftc07ViewMaterialization_ReportsPhaseAndFaceOnFailure`

Server:

- `Ftc07ServerImport_ReturnsBeforeViewMaterialization`
- `Ftc07ViewMaterialization_FailsWithDiagnosticInsteadOfHang`

Client:

- `reports view materialization failure without masquerading as import failure`

Regression smoke:

- FTC-06 same-sense regression remains green.

## Validation run

Commands run during this house call:

```powershell
dotnet build Aetheris.Kernel.Core.Tests\Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-restore
dotnet build Aetheris.Server.Tests\Aetheris.Server.Tests.csproj -f net10.0 --no-restore
dotnet test Aetheris.Kernel.Core.Tests\Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "FullyQualifiedName~Aetheris.Kernel.Core.Tests.Step242.Step242Ftc07ViewMaterializationRegressionTests.Step242Ftc07ViewMaterialization_CompletesOrReportsBoundedDiagnostic|FullyQualifiedName~Aetheris.Kernel.Core.Tests.Step242.Step242Ftc07ViewMaterializationRegressionTests.Step242Ftc07ViewMaterialization_ReportsPhaseAndFaceOnFailure" --logger "console;verbosity=minimal"
dotnet test Aetheris.Server.Tests\Aetheris.Server.Tests.csproj -f net10.0 --no-build --filter "FullyQualifiedName~Aetheris.Server.Tests.KernelApiIntegrationTests.Ftc07ServerImport_ReturnsBeforeViewMaterialization|FullyQualifiedName~Aetheris.Server.Tests.KernelApiIntegrationTests.Ftc07ViewMaterialization_FailsWithDiagnosticInsteadOfHang" --logger "console;verbosity=minimal"
dotnet test Aetheris.Kernel.Core.Tests\Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "Step242Ftc06SameSenseRegressionTests|FTC06|Ftc06|same_sense" --logger "console;verbosity=minimal"
npm test -- --run App.test.tsx
```

Representative direct API result:

- `POST /display/prepare` now returns HTTP 422 with `Viewer.Tessellation.Timeout`
- phase: `PlanarTriangulationWithHoles`
- face: `9`
- surface: `Plane`

## Remaining limitations / blockers

- FTC-07 still does not fully render through the current display tessellation path.
- The exact algorithmic defect inside planar hole triangulation for face 9 is not yet repaired here.
- This change intentionally prefers an honest bounded diagnostic over an unsafe broader triangulation rewrite.

## Non-goals

- no FTC-06 same-sense behavior changes
- no general NURBS support expansion
- no broad AP242 importer rewrite
- no general Boolean behavior changes
- no Firmament V2 side-hole semantics changes
- no AIR Region route policy changes
- no CIR authority changes
- no BRep redesign
