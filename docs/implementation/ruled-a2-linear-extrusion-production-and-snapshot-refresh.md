# RULED-A2: Linear-extrusion production fixture and NIST snapshot audit

## Production geometry added

RULED-A2 adds a Core STEP production fixture that builds an Aetheris B-rep body in memory and exports it through `Step242Exporter`. The fixture is an intentionally open single-face surface body: one bounded elliptic strip whose support surface is `SurfaceGeometryKind.LinearExtrusion`.

The constructed directrix is an `ELLIPSE` in the XY plane with major radius 4 and minor radius 2. The extrusion vector is `(0, 0, 5)`. The face boundary uses two half-ellipse edges and two linear seam edges, so the artifact is a bounded CAD surface body rather than a hand-authored STEP probe.

Generated artifact path:

- `testdata/step242/generated/ruled-a2/ellipse-linear-extrusion-production.step`

## Why `SURFACE_OF_LINEAR_EXTRUSION` is exact

The support surface is the translational sweep of a non-circular conic directrix along a straight vector. Because the directrix is elliptical rather than circular, the surface should not simplify to the current elementary `CYLINDRICAL_SURFACE` lane. Exporting it as `SURFACE_OF_LINEAR_EXTRUSION` preserves the directrix plus vector semantics exactly and avoids degrading the surface to `B_SPLINE_SURFACE_WITH_KNOTS`.

## Directrix subset used

RULED-A1 supported line/circle swept directrices for the skeleton probes. RULED-A2 extends the swept-surface directrix subset to include `ELLIPSE` for both export and import decoding. This is intentionally narrow: it only enables the existing `Ellipse3Curve` representation as a swept directrix and does not add general curve-library expansion or `RULED_SURFACE` support.

## Reimport evidence

`Step242LinearExtrusionSurfaceProductionTests` verifies that the production fixture:

1. exports successfully through `Step242Exporter`;
2. contains `SURFACE_OF_LINEAR_EXTRUSION` and `ELLIPSE`;
3. does not contain `B_SPLINE_SURFACE_WITH_KNOTS`;
4. reimports through `Step242Importer`;
5. reimports as a single body, shell, face, loop, four edges, and four vertices;
6. preserves `SurfaceGeometryKind.LinearExtrusion` with an `Ellipse3` directrix.

## FreeCAD validation status

FreeCAD validation remains optional and is not made mandatory for CI. In this environment, no FreeCAD command-line executable was found on `PATH`, so the generated AP242 artifact could not be externally validated here. The intended optional command is:

```powershell
.\tools\Validate-Step-FreeCAD.ps1 .\testdata\step242\generated\ruled-a2\ellipse-linear-extrusion-production.step
```

For follow-on ruled/swept experimentation, RULED-TOOLING-A0 adds an InlineStep-based probe harness so developers can wrap a small probe asset instead of hand-authoring a full fixture; see `docs/implementation/ruled-tooling-a0-inline-step-probe-harness.md`.

## NIST snapshot refresh details

The RULED-A1 semantic change preserves exact swept-surface semantics during canonical STEP output. That can change byte-stable canonical hashes without changing import success, diagnostics status, or topology counts. The previously affected audit tests are the NIST per-file and aggregate audit snapshot checks in `Step242NistAuditHarnessTests`:

- `NistCorpus_PerFile_AuditReport_IsStable_AndMatchesSnapshot`
- `NistCorpus_AggregateAuditReport_IsByteStableAcrossConsecutiveRuns_AndMatchesSnapshot`

For RULED-A2, the broad NIST/audit/snapshot filter was run after the production fixture/directrix work. It passed against the checked-in snapshots, which confirms that the snapshot baseline in this branch already reflects the intentional swept-surface semantic delta and that no additional blind hash refresh was necessary. The validation preserved the expected audit invariants: import status remained stable, diagnostics status remained stable, topology counts remained stable, files reimported through the harness, and there was no unrelated snapshot churn.

Validation command used for this audit:

```bash
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "Nist|Snapshot|Audit|Step242"
```

## Deferred work

Deferred explicitly from RULED-A2:

- Firmament ruled syntax;
- AIR ruled transition primitive;
- general ruled surface construction;
- `SURFACE_OF_REVOLUTION` production geometry;
- degree-1 B-spline production construction;
- `RULED_SURFACE` support;
- mandatory FreeCAD/SolidWorks CI;
- SolidWorks validation.
