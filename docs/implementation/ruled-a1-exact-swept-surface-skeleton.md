# RULED-A1 exact swept surface skeleton

## Scope delivered

RULED-A1 adds a narrow AP242 exact-surface lane for:

- `SURFACE_OF_LINEAR_EXTRUSION`
- `SURFACE_OF_REVOLUTION`
- degree-1 `B_SPLINE_SURFACE_WITH_KNOTS` ruled classification

This milestone does not add Firmament ruled syntax, AIR ruled-transition primitives, general lofting, general NURBS conversion, full ruled-face construction, or `RULED_SURFACE`.

## Internal surface kinds added

`Aetheris.Kernel.Core/Geometry/SurfaceGeometry.cs` now includes:

- `SurfaceGeometryKind.LinearExtrusion`
- `SurfaceGeometryKind.SurfaceOfRevolution`

Backed by:

- `LinearExtrusionSurface`
  - `CurveGeometry Directrix`
  - `Vector3D ExtrusionVector`
- `SurfaceOfRevolutionSurface`
  - `CurveGeometry Directrix`
  - `Point3D AxisOrigin`
  - `Direction3D AxisDirection`

The directrix is stored through the existing reusable `CurveGeometry` model instead of a one-off surface-only line record.

## STEP entities imported/exported

Importer support was added in `Step242Importer` and `Step242SubsetDecoder` for:

- `SURFACE_OF_LINEAR_EXTRUSION`
- `SURFACE_OF_REVOLUTION`
- `AXIS1_PLACEMENT`
- `VECTOR` decoding for swept-surface axes

Exporter support was added in `Step242Exporter` for:

- `SURFACE_OF_LINEAR_EXTRUSION`
- `SURFACE_OF_REVOLUTION`
- `AXIS1_PLACEMENT`
- free directrix `LINE` / `CIRCLE`
- free `VECTOR`

The importer/exporter preserve these as exact swept surfaces and do not collapse them to B-spline fallback.

## Directrix subset supported

Imported/exported directrix subset:

- `LINE`
- `CIRCLE`

RULED-A1 only requires line directrix coverage in tests. Circle directrix support was cheap because the STEP subset decoder and curve model already handled circles cleanly.

Unsupported swept directrix curves still fail deterministically through the importer/exporter unsupported-surface path. They are not silently degraded to B-spline.

## Degree-1 B-spline ruled classification

`Step242BsplineRuledClassifier` adds an exactness report without overwriting the original B-spline surface data.

Classification rules:

- `degreeU == 1 || degreeV == 1` -> exact ruled candidate
- `degreeU == 1 && degreeV == 1` -> exact bilinear ruled patch
- `degreeU > 1 && degreeV > 1` -> not ruled by degree alone

Reported fields:

- `IsRuledCandidate`
- `RulingDirection`
  - `U`
  - `V`
  - `Both`
  - `None`
- `IsBilinearPatch`
- `Exactness`
  - `ExactRuled`
  - `None`
- `Reason`

This classification is intentionally separate from analytic recovery. The existing bounded cylinder recovery lane still runs for its rational `(1,3)` / `(3,1)` cases, but degree-1 B-splines are no longer conceptually “approximation garbage” by default.

## Fixtures

Probe fixtures added under `testdata/step242/probes/`:

- `surface-of-linear-extrusion-line.step`
- `surface-of-revolution-line.step`
- `bspline-degree-1-1-bilinear.step`

These are small importable AP242 probes for exact surface import/export/reimport testing.

## Validation commands

Primary validation commands used for RULED-A1:

```powershell
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.Kernel.Core.Tests\Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "FullyQualifiedName~Step242RuledSurfaceSkeletonTests|FullyQualifiedName~Step242BSplineSurfaceWithKnotsTests|FullyQualifiedName~Step242OcctCylinderRecoveryTests|FullyQualifiedName~Step242ConicalSurfaceRegressionTests|FullyQualifiedName~Step242InlineSurfaceConstructorTests|FullyQualifiedName~Step242ExporterTests|FullyQualifiedName~Step242ImporterTests"
dotnet test Aetheris.Kernel.Firmament.Tests\Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --filter "InlineStep"
dotnet run --project Aetheris.CLI -- --help
```

Broader `Step242|Surface|Bspline|Ruled|Extrusion|Revolution` filtering also reaches NIST audit snapshot tests. Those snapshots change because canonical export now preserves additional exact swept-surface semantics. That broad command is still informative, but it is not the best focused gate for the bounded RULED-A1 lane.

## External validation

FreeCAD validation command:

```powershell
.\tools\Validate-Step-FreeCAD.ps1 .\testdata\step242\probes\surface-of-linear-extrusion-line.step
.\tools\Validate-Step-FreeCAD.ps1 .\testdata\step242\probes\surface-of-revolution-line.step
.\tools\Validate-Step-FreeCAD.ps1 .\testdata\step242\probes\bspline-degree-1-1-bilinear.step
```

If `FreeCADCmd` is not on `PATH`, the script skips cleanly and reports the skip.

## Known limitations

- `RULED_SURFACE` is still not implemented.
- No Firmament ruled syntax was added.
- No AIR ruled-transition primitive was added.
- No general loft or general NURBS conversion was added.
- No automatic simplification from swept surfaces to elementary surfaces was added here.
- Multi-loop hole classification for the new swept surface kinds is still not a dedicated path; RULED-A1 stays on the minimal exact import/export skeleton.
- The broad NIST audit snapshot suite needs explicit snapshot refresh if the project wants canonical hashes to encode the new preserved surface semantics.
