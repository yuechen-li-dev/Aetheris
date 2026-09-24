# BREP-CROSSHOLE-X1 — seam-aware diametral cross-hole topology

**Executive verdict: Accepted for one solid cylindrical stock cross-hole.** The kernel now builds a closed, oriented BRep from the qualified perpendicular cylinder-intersection authority. This is a bounded constructive operation for a finite solid cylinder with one through-diameter hole. It does not introduce a general Boolean, Firmament feature, Hollow support, pattern, or ClevisPin template.

## Construction and admission

`BrepDiametralCrossHole.Build` accepts an analytic host cylinder, finite axial length, perpendicular cutter direction, positive cutter radius strictly smaller than the host radius, and an axial hole-center distance from the host origin. It places the cutter axis through the host centerline. The full cutter circle must clear both stock ends. Invalid dimensions, end collisions, oblique axes, and tangent/oversize radii return explicit diagnostics. Eccentric placement is absent from this API, so it cannot accidentally be admitted.

The builder uses the two existing `CylinderCylinderIntersectionCurve` branches. Each branch retains the exact evaluator and derivative beside a certified non-rational cubic B-spline 3D representation. Its finite host and tool pcurves are built by `CylinderIntersectionPcurveRealizer` from the exact pcurve evaluators. Cubic Hermite bounds are checked against the same linear tolerance, including the 3D edge deviation and numerical allowance. `BrepPcurveValidator` checks all 16 coedge uses at 129 samples; its sampling is validation, never topology construction.

The host seam is chosen on the clear radial sector between the two openings, independent of the caller's cylinder X-axis. Consequently neither intersection branch crosses the host's resulting UV seam. The cutter seam is one geometric line edge with two face-local coedge pcurves at U=0 and U=2π. Each closed intersection branch is divided at t=π into two topological edges, because the export preflight rejects a non-conic self-loop edge. Both halves keep the same exact branch authority and certified 3D spline, with distinct trim intervals. No sampled vertices or duplicate edge geometry authority are introduced.

## Face and loop topology

The host cylindrical face has one outer loop and two inner loops for entry and exit. One cutter-cylinder wall face connects them; the planar end caps close the stock. The cutter wall face has the opposite surface sense to the tool cylinder, expressing the void. Every edge has exactly two coedge uses. The canonical and clevis-ratio fixtures each contain **4 faces, 8 edges, 6 vertices, 16 coedges, 2 opening loops, and 4 intersection edge segments**. There are no intersection seam splits after canonicalization; the cutter seam is represented once geometrically with two UV uses.

The builder runs binding validation, full coedge pcurve validation, and strict export preflight before returning. Focused tests also check shell enclosure, orientation, positive signed volume, and that volume is less than the uncut cylinder's. The mass-property value is a tessellated sanity estimate, not an exact occupied-volume measurement. The dedicated BRep display tessellator produces nonempty patches for all four faces. SurfaceMeshIR still explicitly rejects trim topology on a cylindrical face; this separate rendering route is not claimed for the cross-hole.

## Qualification witnesses

The canonical fixture uses R=2 mm, r=0.6 mm, L=10 mm, and center position 5 mm. Small and large valid cutter radii of 0.2 and 1.8 mm pass the same topology checks. The clevis-ratio kernel fixture uses host R=0.79375 mm and cutter r=0.5953125 mm; it is the **hole ratio only**, not a reconstructed clevis pin. Tests also move the hole to 3 and 7 mm, rotate its direction around the host, and use a translated host with a world-Y axis. Invalid zero radius, end collision, oblique axis, tangent radius, and oversize radius fail.

For the clevis ratio, the prior 3D realization has 128 cubic spans (512 control points) and a 2.293e-7 mm certified deviation bound. The finite pcurves use 160 host and 128 tool spans; total edge/host mismatch bound is 9.523e-7 mm and edge/tool mismatch bound is 4.582e-7 mm, each below the default 1e-6 mm tolerance. The measured sampled lift deviations were smaller; the bounds are the admission authority. The intersection remains exact in kernel metadata, while STEP carries its finite certified representation.

## STEP and inspection

The exporter now writes `TRIMMED_CURVE` for a B-spline edge whose trim interval is shorter than the underlying spline domain; the importer preserves that interval. Without this, the second half of each opening was exported as the first half and reimport failed cylindrical inner-loop containment. The canonical STEP now exports and reimports with two opening roles, two analytic cylindrical faces, positive oriented volume, and an enclosed shell.

`Aetheris.CLI analyze` on the local generated STEP reports one enclosed manifold body, 4 faces, 8 edges, 6 vertices, 2 plane and 2 cylinder surfaces, 4 B-spline edges, 16 pcurves, and two seam pcurve pairs. The bounding box is [-2,2] × [-2,2] × [-5,5] mm. STEP import currently reports its unit basis as assumed, so the millimeter statement comes from the authored kernel fixture and exporter setup. The `wireframe` CLI projection visibly shows both circular openings connected by the transverse wall. Local ignored debug outputs are `artifacts/local/crosshole-x1/canonical.step`, `canonical.svg`, and a browser-rendered `canonical.png` in the same directory.

Reproduce the focused checks:

```text
dotnet test Aetheris.Kernel.Core.Tests --no-restore --filter "FullyQualifiedName~BrepDiametralCrossHoleTests|FullyQualifiedName~CylinderCylinderIntersectionCurveTests" -m:1
dotnet run --project Aetheris.CLI -- analyze artifacts/local/crosshole-x1/canonical.step --json
```

The STEP file is generated by the focused round-trip test when `AETHERIS_CROSSHOLE_STEP_OUTPUT` names the desired local file. No source STEP or mesh Boolean is used to construct the body.

## Build and regression

On the 2026-09-23 local Windows .NET 10 run, `dotnet build Aetheris.slnx --no-restore -m:1 --verbosity quiet` completed with zero errors and seven existing WebAssembly warnings. The focused intersection and cross-hole selection passed **21/21** after the final assertion edit. The serial full solution run passed **3,874** tests and failed one existing `SurfaceMeshIrTests.ThroughHolePlate_UsesMultiLoopPlanarBands_AndLowersToWatertightMesh` assertion: the planar box-hole tessellator produced 87 cells where the test expects 90. That test was already failing in the preceding geometry-only qualification; it does not exercise the new cross-hole path. The full suite is therefore not green.
