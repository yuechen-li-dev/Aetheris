# STEP-AP242-HARDEN-X1 — FTC-06 import validity house call

## 1. Problem statement

FTC-06 imported into Aetheris, re-exported as STEP, and then opened in SolidWorks with import diagnostics showing faulty faces. The visible symptom cluster was concentrated around spherical, toroidal, cylindrical, and conical trimmed features rather than open-shell gaps.

## 2. FTC-06 / FTC-07 correction

This house call is FTC-06 only.

- FTC-06: import/export validity defect investigated here.
- FTC-07: separate current issue where local import hangs.

No FTC-07 hang work is included in this change.

## 3. Reproduction commands

Locate the fixture:

```powershell
rg -n "FTC-06|FTC06|ftc_06|nist_ftc_06|NIST.*06|Ftc.*06" .
```

Reproduce the import/export path:

```powershell
dotnet run --project Aetheris.CLI -- canon testdata\step242\nist\FTC\nist_ftc_06_asme1_ap242-e2.stp --out artifacts\ftc06\nist_ftc_06_canonical.step --json
dotnet run --project Aetheris.CLI -- analyze testdata\step242\nist\FTC\nist_ftc_06_asme1_ap242-e2.stp --json
dotnet run --project Aetheris.CLI -- analyze artifacts\ftc06\nist_ftc_06_canonical.step --json
```

Focused regression lane:

```powershell
dotnet test Aetheris.Kernel.Core.Tests\Aetheris.Kernel.Core.Tests.csproj -f net10.0 --filter "Step242Ftc06SameSenseRegressionTests|ImportedCylindricalFaceWithFalseSameSense|FTC06|Ftc06|same_sense" --logger "console;verbosity=minimal"
```

## 4. Observed SolidWorks symptom summary

- SolidWorks opened the exported FTC-06 artifact but reported faulty faces.
- The visible hotspots were spherical/toroidal/cylindrical/conical trimmed features.
- Reported gaps were zero in the screenshot, suggesting invalid face orientation/trim semantics rather than shell leakage.

## 5. Aetheris internal diagnosis

FTC-06 imports as one enclosed manifold body with stable topology counts:

- faces: 187
- edges: 476
- vertices: 310
- surface families: 71 plane, 88 cylinder, 8 cone, 8 sphere, 12 torus

The critical forensic finding was face orientation loss on curved faces:

- the source FTC-06 STEP contains many curved `ADVANCED_FACE(...,.F.)` entries
- importer preserved `same_sense` only for planar faces
- importer dropped `same_sense` for cylinders, cones, spheres, and tori
- exporter then wrote every `ADVANCED_FACE` back out as `.T.`

Source FTC-06 curved false-sense counts:

- cylindrical: 65
- toroidal: 12
- spherical: 4
- conical: 4

This means Aetheris was changing the orientation contract of many curved faces during round-trip export even when topology counts stayed stable.

## 6. Root cause found

Minimal root cause:

- `ADVANCED_FACE.same_sense` was not preserved in `FaceGeometryBinding` for non-planar faces.
- `Step242Exporter` serialized all faces with `same_sense = .T.` regardless of imported orientation.

This is especially dangerous for periodic/trimmed analytic faces such as cylinders, spheres, toroids, and cones, which matches the external symptom cluster in FTC-06.

## 7. Fix applied

Applied narrow fix:

- extended `FaceGeometryBinding` with `SameSense`
- stored imported `ADVANCED_FACE.same_sense` in the binding model
- emitted `ADVANCED_FACE` with the preserved binding sense during export

No general NURBS work, Boolean work, or topology redesign was introduced.

## 8. Tests added

Added focused regressions:

- `ExportBody_ImportedCylindricalFaceWithFalseSameSense_PreservesAdvancedFaceSense`
- `Step242Ftc06Import_DoesNotRegress`
- `Step242Ftc06Export_HasStableTopologySummary`
- `Step242Ftc06ProblemFace_AdvancedFaceSameSense_Regression`
- `Step242Ftc06_DiagnosticsIdentifyNoKnownInvalidTrimCondition`

The FTC-06 regression verifies that the exported curved-face false-sense mix now matches the source FTC-06 orientation ledger instead of being flattened to `.T.`.

## 9. Remaining limitations / blockers

- This fix addresses curved `ADVANCED_FACE.same_sense` loss only.
- It does not prove that every SolidWorks faulty-face report is eliminated; SolidWorks automation is not part of the repo evidence lane.
- Full solution build currently has an unrelated `Aetheris.Server` file-lock failure in this workspace.
- The broad STEP/NIST filter also hits pre-existing canonical SHA snapshot drift unrelated to this FTC-06 orientation fix.

## 10. Non-goals

- FTC-07 hang
- general NURBS support
- general Boolean behavior changes
- Firmament V2 feature work
- AIR Region route policy changes
- CIR authority model changes
- broad BRep redesign
