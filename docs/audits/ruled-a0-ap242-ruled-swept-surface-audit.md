# RULED-A0 AP242 exact ruled/swept surface audit

## 1. Summary verdict

Aetheris currently has a solid elementary-surface AP242 lane for `PLANE`, `CYLINDRICAL_SURFACE`, `CONICAL_SURFACE`, `SPHERICAL_SURFACE`, and `TOROIDAL_SURFACE`, plus a generic `B_SPLINE_SURFACE_WITH_KNOTS` preservation lane. It does **not** currently parse or export `SURFACE_OF_LINEAR_EXTRUSION`, `SURFACE_OF_REVOLUTION`, or `RULED_SURFACE` in the STEP surface binding path.

Degree-1 B-spline surfaces are accepted by the geometry model because `BSplineSurfaceWithKnots` permits degrees greater than or equal to one, and the STEP importer/exporter preserves the stored degrees and knot data. However, the current recovery lane does not label degree-1 surfaces as exact ruled surfaces. It only attempts a bounded rational B-spline-to-cylinder recovery for a degree `(1,3)` or `(3,1)` two-ring cubic profile, then otherwise preserves the B-spline surface as a B-spline.

Therefore, the minimal next implementation path is not `RULED_SURFACE` first. It is:

1. add exact internal surface kinds for linear extrusion and revolution;
2. parse/export `SURFACE_OF_LINEAR_EXTRUSION` and `SURFACE_OF_REVOLUTION` without degrading them to generic B-splines;
3. add a degree-1 B-spline ruled classifier that records exact ruled semantics while preserving original B-spline data;
4. only then decide whether a schema-backed `RULED_SURFACE` lane is useful.

## 2. Current importer support matrix

| Representation | Import | Evidence and notes |
| --- | --- | --- |
| `PLANE` | yes | `Step242Importer.BindAdvancedFaceSurface` dispatches `PLANE` to `ReadPlaneSurface` and stores `SurfaceGeometry.FromPlane`. |
| `CYLINDRICAL_SURFACE` | yes | The importer dispatches `CYLINDRICAL_SURFACE` to `ReadCylindricalSurface` and stores `SurfaceGeometry.FromCylinder`. |
| `CONICAL_SURFACE` | yes | The importer dispatches `CONICAL_SURFACE` to `ReadConicalSurface` and stores `SurfaceGeometry.FromCone`. |
| `SPHERICAL_SURFACE` | yes | The importer dispatches `SPHERICAL_SURFACE` to `ReadSphericalSurface` and stores `SurfaceGeometry.FromSphere`. |
| `TOROIDAL_SURFACE` | yes | The importer dispatches `TOROIDAL_SURFACE` to `ReadToroidalSurface` and stores `SurfaceGeometry.FromTorus`. |
| `SURFACE_OF_LINEAR_EXTRUSION` | no | No STEP importer dispatch, decoder, surface kind, or constructor-specific code was found for this entity. It will reach the unsupported-surface failure path. |
| `SURFACE_OF_REVOLUTION` | no | No STEP importer dispatch, decoder, surface kind, or constructor-specific code was found for this entity. It will reach the unsupported-surface failure path. |
| `B_SPLINE_SURFACE_WITH_KNOTS` degree 1 | partial | The decoder reads degree values generically, and `BSplineSurfaceWithKnots` allows degree 1. Import preserves it as a B-spline unless cylinder recovery succeeds. It is not classified as ruled. |
| `B_SPLINE_SURFACE_WITH_KNOTS` degree 1,1 | partial | Same as degree-1 generally: it can be imported and preserved as B-spline data, but no bilinear ruled-patch semantic classification exists. |
| `RULED_SURFACE` if schema-present | no | No current STEP implementation code references `RULED_SURFACE`; only older docs mention that exporter work was intentionally out of scope. |

## 3. Current exporter support matrix

| Representation | Export | Evidence and notes |
| --- | --- | --- |
| `PLANE` | yes | `Step242Exporter.BuildSurface` maps `SurfaceGeometryKind.Plane` to `BuildPlane`. |
| `CYLINDRICAL_SURFACE` | yes | `BuildSurface` maps `SurfaceGeometryKind.Cylinder` to `BuildCylinder`, which writes `CYLINDRICAL_SURFACE`. |
| `CONICAL_SURFACE` | yes | `BuildSurface` maps `SurfaceGeometryKind.Cone` to `BuildCone`, which writes `CONICAL_SURFACE`. |
| `SPHERICAL_SURFACE` | yes | `BuildSurface` maps `SurfaceGeometryKind.Sphere` to `BuildSphere`, which writes `SPHERICAL_SURFACE`. |
| `TOROIDAL_SURFACE` | yes | `BuildSurface` maps `SurfaceGeometryKind.Torus` to `BuildTorus`, which writes `TOROIDAL_SURFACE`. |
| `SURFACE_OF_LINEAR_EXTRUSION` | no | No `SurfaceGeometryKind` or exporter builder exists for linear-extrusion surfaces. |
| `SURFACE_OF_REVOLUTION` | no | No `SurfaceGeometryKind` or exporter builder exists for revolution surfaces. |
| `B_SPLINE_SURFACE_WITH_KNOTS` degree 1 | partial | If an internal `BSplineSurfaceWithKnots` has degree 1, `BuildBSplineSurfaceWithKnots` writes those degree fields unchanged. Aetheris does not yet choose degree-1 B-spline export as an exact ruled semantic lane. |
| `B_SPLINE_SURFACE_WITH_KNOTS` degree 1,1 | partial | Same as degree-1 generally: preserved if already represented as B-spline, but not deliberately emitted as bilinear ruled patch by a ruled-surface exporter. |
| `RULED_SURFACE` if schema-present | no | No exporter builder or surface kind exists. |

## 4. Combined support matrix

| Representation | Import | Export | Reimport | External CAD | Notes |
| --- | --- | --- | --- | --- | --- |
| `PLANE` | yes | yes | yes | unknown | Internal AP242 import/export/reimport is supported by elementary surface lanes; external CAD validation is not yet documented as evidence for this audit. |
| `CYLINDRICAL_SURFACE` | yes | yes | yes | unknown | Export fixtures contain `CYLINDRICAL_SURFACE`; importer has a direct analytic lane. |
| `CONICAL_SURFACE` | yes | yes | yes | unknown | Export fixtures contain `CONICAL_SURFACE`; importer has a direct analytic lane. |
| `SPHERICAL_SURFACE` | yes | yes | yes | unknown | Direct import/export code exists; audit did not add external evidence. |
| `TOROIDAL_SURFACE` | yes | yes | yes | unknown | Direct import/export code exists; audit did not add external evidence. |
| `SURFACE_OF_LINEAR_EXTRUSION` | no | no | no | unknown | Not represented in current internal surface model or STEP binding. |
| `SURFACE_OF_REVOLUTION` | no | no | no | unknown | Not represented in current internal surface model or STEP binding. |
| `B_SPLINE_SURFACE_WITH_KNOTS` degree 1 | partial | partial | partial | unknown | Accepted and serialized as B-spline data, but not marked as exact ruled. |
| `B_SPLINE_SURFACE_WITH_KNOTS` degree 1,1 | partial | partial | partial | unknown | Accepted and serialized as B-spline data, but not marked as bilinear ruled. |
| `RULED_SURFACE` if schema-present | no | no | no | unknown | No schema/parser/exporter evidence found in implementation. |

## 5. Recovery-lane behavior

`Step242BsplineSurfaceRecoveryLane` currently has two candidates: `analytic_cylinder` and `reject`. The analytic cylinder candidate is only admissible when the source entity is rational-like and the control net matches a bounded two-ring cubic profile. The supported profiles are degree `(1,3)` with two U rows and four V columns, or degree `(3,1)` with four U rows and two V columns.

Important consequences:

- Degree-1 in one direction is used only as part of the cylinder probe shape; it is not a general ruled-surface classifier.
- Degree `(1,1)` bilinear patches are not recognized as a special exact ruled representation.
- Non-rational degree-1 B-spline ruled patches fall through to ordinary B-spline preservation.
- The lane is appropriately bounded for analytic recovery, but it should not be the final home for ruled-surface semantics unless expanded with explicit candidate names, rejection reasons, and preservation policy.

## 6. Profile-stack/prismatic export behavior

The audited prismatic/extrude code builds planar side faces for polygonal extrusions and analytic cylindrical/conical faces for recognized round/conical features. It does not emit `SURFACE_OF_LINEAR_EXTRUSION` for prismatic side faces. In many polygonal-prismatic cases this is still exact because a line extruded along a vector is a plane, and `PLANE` is the preferred simpler AP242 representation. For non-planar directrix extrusions, Aetheris has no current exact swept-surface lane and would need a new internal surface kind rather than forcing the representation through generic B-splines or tessellation.

## 7. Recommended export preference ladder

Use the following export ladder for ruled/swept surfaces:

1. `PLANE` for planar faces, including line extrusions that simplify to planes.
2. `CYLINDRICAL_SURFACE` for true circular cylinders.
3. `CONICAL_SURFACE` for true cones/frusta.
4. `SPHERICAL_SURFACE` / `TOROIDAL_SURFACE` where the internal analytic surface exactly matches.
5. `SURFACE_OF_LINEAR_EXTRUSION` for exact translational sweeps of a supported STEP directrix curve where the surface does not simplify to an elementary surface.
6. `SURFACE_OF_REVOLUTION` for exact rotational sweeps of a supported STEP directrix curve where the surface does not simplify to cylinder/cone/sphere/torus.
7. `B_SPLINE_SURFACE_WITH_KNOTS` with degree 1 in the ruling direction for exact ruled tensor-product patches when no elementary or swept entity is a better semantic fit.
8. Higher-degree `B_SPLINE_SURFACE_WITH_KNOTS` fallback for genuine smooth/NURBS surfaces and existing analytic recovery fixtures.

Rationale: current code already has stable elementary-surface lanes, and those should remain preferred. `SURFACE_OF_LINEAR_EXTRUSION` and `SURFACE_OF_REVOLUTION` should be added before treating degree-1 B-splines as the primary ruled path, because AP242 swept surfaces carry stronger directrix/sweep semantics. Degree-1 B-splines should still be treated as exact when they are the best available representation.

## 8. Recommended import/recovery ladder

Use the following import/recovery ladder:

1. Preserve elementary surfaces directly: `PLANE`, `CYLINDRICAL_SURFACE`, `CONICAL_SURFACE`, `SPHERICAL_SURFACE`, `TOROIDAL_SURFACE`.
2. Preserve `SURFACE_OF_LINEAR_EXTRUSION` as an exact swept surface when the directrix curve and vector can be decoded.
3. Preserve `SURFACE_OF_REVOLUTION` as an exact swept surface when the directrix curve and axis can be decoded.
4. Detect `B_SPLINE_SURFACE_WITH_KNOTS` with degree 1 in one direction as an exact ruled candidate.
5. Detect degree `(1,1)` as an exact bilinear ruled patch.
6. Attempt optional simplification, in order: plane, cylinder, cone, surface of linear extrusion, surface of revolution.
7. If simplification is not exact/admissible, preserve the exact degree-1 B-spline ruled patch and record that it is exact ruled, not approximate recovery garbage.
8. Use the existing higher-degree B-spline analytic recovery lane only for bounded degree-2/3 or rational approximation/recovery cases with explicit candidate names, scoring, and rejection reasons.

## 9. External validation plan

Internal Aetheris roundtrip is necessary but insufficient. External validation should use optional tools and manual CAD inspection without making CI depend on commercial software.

### FreeCAD optional validation

This milestone adds `tools/Validate-Step-FreeCAD.ps1`, an optional script that:

- checks whether `FreeCADCmd.exe`, `FreeCADCmd`, or `freecadcmd` is on `PATH`;
- prints a clear skipped message and exits `0` when FreeCAD is unavailable;
- opens the supplied STEP file with FreeCAD `Import.open` when available;
- prints object count and shape validity when available;
- exits nonzero if FreeCAD import fails or imported shapes report invalid.

Example commands:

```powershell
pwsh ./tools/Validate-Step-FreeCAD.ps1 ./testdata/firmament/exports/cylinder_basic.step
pwsh ./tools/Validate-Step-FreeCAD.ps1 ./testdata/step242/probes/surface-of-linear-extrusion-line.step
```

The second command is a future probe command until that fixture exists.

### SolidWorks manual validation

For each generated AP242 file:

1. Open SolidWorks.
2. Use **File -> Open** and select the STEP AP242 output.
3. Confirm import succeeds.
4. Confirm a surface body or solid body exists.
5. Confirm there are no visibly broken faces or failed import diagnostics.
6. For PMI demo files, note that AP242 PMI may or may not display graphically depending on SolidWorks settings.
7. For ruled/swept fixtures, inspect face/surface properties if the installed SolidWorks version exposes them.

Limitation: SolidWorks may not expose the original STEP surface entity type directly in normal UI. If it only reports a healed body/surface, use that as external import evidence, not as proof that the exact AP242 entity survived semantically.

## 10. Minimal next implementation milestone

Recommended next milestone: **RULED-A1 — exact swept surface import/export skeleton and ruled B-spline classification**.

Scope:

1. Add internal `SurfaceGeometryKind.LinearExtrusion` and `SurfaceGeometryKind.SurfaceOfRevolution` records with directrix curve references/values and sweep vector or axis.
2. Add STEP decoder support for `SURFACE_OF_LINEAR_EXTRUSION` and `SURFACE_OF_REVOLUTION` for a minimal curve subset: `LINE`, `CIRCLE`, and existing B-spline curves if already decodable.
3. Add exporter builders for those two entities.
4. Add small static/import fixtures for:
   - `surface-of-linear-extrusion-line.step`;
   - `surface-of-revolution-line.step`;
   - `bspline-degree-1-1-bilinear.step`.
5. Add importer tests proving current unsupported entities become supported and reexport as exact swept entities where possible.
6. Add a degree-1 B-spline classifier that records exact ruled/bilinear semantics and does not call it approximation by default.
7. Keep FreeCAD validation optional and non-CI-blocking unless a future environment explicitly installs FreeCAD.

Exit criterion: Aetheris can import/export/reimport at least one linear-extrusion surface, one revolution surface, and one degree `(1,1)` bilinear B-spline patch with explicit exactness semantics.

## 11. Deferred features

- General loft syntax or modeling features.
- Firmament ruled syntax.
- AIR ruled transition primitive.
- Full BRep ruled face materialization beyond the minimal fixture path.
- General NURBS conversion.
- Broad STEP schema expansion unrelated to the three target lanes.
- Required FreeCAD/SolidWorks execution in normal CI.
- `RULED_SURFACE` implementation unless future schema/code investigation proves it is relevant to target AP242 data.

## 12. Direct answers to audit questions

1. `SURFACE_OF_LINEAR_EXTRUSION` parse: **no**.
2. `SURFACE_OF_LINEAR_EXTRUSION` export: **no**.
3. `SURFACE_OF_REVOLUTION` parse: **no**.
4. `SURFACE_OF_REVOLUTION` export: **no**.
5. Degree-1 `B_SPLINE_SURFACE_WITH_KNOTS` parse/export without approximation: **partial**. It is accepted and preserved as a B-spline, but not classified as exact ruled.
6. `Step242BsplineSurfaceRecoveryLane` degree-1 distinction: **no for general ruled**, **partial for cylinder recovery** because it probes degree `(1,3)`/`(3,1)` rational-like profiles only.
7. Profile-stack extrude/prismatic faces: **elementary when currently supported**. Polygonal extrude sides emit planes; round/conical recognized features emit cylinders/cones. No swept-surface entities are emitted.
8. `RULED_SURFACE` support/schema evidence: **no implementation evidence found**.
9. Exact entities surviving Aetheris import/export/reimport today: **elementary surfaces yes; B-spline surfaces partially as B-splines; linear extrusion/revolution/ruled no**.
10. Exact entities surviving external CAD import: **unknown pending FreeCAD/SolidWorks validation**.

## 13. Code-search evidence

Required searches were run for:

```text
SURFACE_OF_LINEAR_EXTRUSION
SURFACE_OF_REVOLUTION
B_SPLINE_SURFACE_WITH_KNOTS
B_SPLINE_SURFACE
BsplineSurfaceRecovery
CONICAL_SURFACE
CYLINDRICAL_SURFACE
RULED_SURFACE
swept_surface
```

Key files/classes:

- `Aetheris.Kernel.Core/Step242/Step242Importer.cs` — elementary surface import dispatch, B-spline surface resolution, unsupported surface fallback.
- `Aetheris.Kernel.Core/Step242/Step242SubsetDecoder.cs` — `B_SPLINE_SURFACE_WITH_KNOTS`, cylinder, cone, sphere, torus decoders.
- `Aetheris.Kernel.Core/Step242/Step242Exporter.cs` — elementary and B-spline surface exporters.
- `Aetheris.Kernel.Core/Step242/Step242BsplineSurfaceRecoveryLane.cs` — bounded rational B-spline cylinder recovery lane.
- `Aetheris.Kernel.Core/Geometry/SurfaceGeometry.cs` — current internal surface-kind vocabulary.
- `Aetheris.Kernel.Core/Geometry/Surfaces/BSplineSurfaceWithKnots.cs` — B-spline surface invariants, including degree `>= 1`.
- `Aetheris.Kernel.Core/Brep/Features/BrepExtrude.cs` — extrude side faces created as planes.
- `Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanBoxCylinderHoleBuilder.cs` — recognized analytic hole/frustum faces created as cylinder/cone surfaces.
- `docs/air-v5-frustum-ruled-transition-production.md` — confirms older `RULED_SURFACE` exporter work was explicitly not in that milestone's scope.
