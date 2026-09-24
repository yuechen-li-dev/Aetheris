# GEOM-INTERSECTION-CURVE-X0 — qualification record

**Downstream status (BREP-CROSSHOLE-X1):** The subsequent [solid cross-hole topology milestone](BREP-CROSSHOLE-X1.md) now realizes finite host/tool pcurves and builds one closed solid-cylinder cross-hole BRep with a STEP round trip. The historical X0 verdict and measurements below remain the record of the earlier geometry-only milestone. Hollow, semantic features, the full clevis witness, and cylindrical Perforation remain outside this topology milestone.

**Executive verdict: Meaningful progression.** Aetheris now has a bounded first-class exact authority for the two branches of a perpendicular, diametral cylinder-cylinder intersection and an adaptively refined, non-rational cubic B-spline representation with an analytic deviation bound. The authority supplies 3D evaluation, derivative, and both analytic cylinder pcurves. It can be retained alongside the finite curve in `CurveGeometry`. Cross-hole BRep topology, finite pcurve serialization, STEP export of a cross-hole, Solid/Hollow construction, Firmament semantics, the clevis pin, and cylindrical Perforation are **not implemented**; the original CrossHole milestone is not accepted.

## Exact authority and admission

`CylinderCylinderIntersectionCurve` owns the host and tool `CylinderSurface` values, their centerline intersection, explicit positive/negative cutter-axis branch, parameter domain, and sense. The admitted axes must be perpendicular and meet at the declared center to numerical alignment. The cutter radius must stay inside the host radius with linear clearance. Oblique, eccentric, tangent/oversize and invalid-domain inputs fail with distinct diagnostics. This is the bounded diametral case, not a general surface-intersection solver.

In the host/cutter orthonormal frame the exact evaluator uses `y=r cos(t)`, `z=r sin(t)`, `x=±sqrt(R²-r² cos²(t))`. Its derivative is analytic. The host pcurve takes the host cylinder's angular coordinate and axial coordinate, unwrapping relative to the domain start; a branch sweeps less than π while `r<R`, so this is deterministic across the chosen host seam. The cutter pcurve advances its angular coordinate by `t-start` and uses the evaluated cutter-axis coordinate. Both lift to the same exact 3D locus within floating-point tolerance. The two branch identities do not depend on BRep face order.

## Finite representation and error contract

`CylinderIntersectionCurveRealizer` builds cubic Hermite Bézier spans from exact endpoint positions and derivatives, then stores them as a degree-3 non-rational `BSpline3Curve`. It subdivides a span only when its bound exceeds the remaining linear-tolerance budget; refinement has an explicit segment cap and controlled failure. The exact authority is retained in `QualifiedCylinderIntersectionRepresentation` and by `CurveGeometry.FromCertifiedIntersection`; the spline remains a representation, not the geometric authority.

The cubic Hermite remainder on a span of width `h` is at most `M₄ h⁴/384`, where `M₄` bounds the fourth derivative of the exact 3D curve. The implementation bounds derivatives of `g(t)=R²-r²cos²(t)` and `sqrt(g)` using the minimum `g` over that span. It combines the analytic remainder with a separate, scale-dependent floating-point allowance of `256 ε scale`, where `ε=2.2204460492503131e-16`. Admission requires their sum to fit the requested `ToleranceContext.Linear` (default **1e-6 mm**). The analytic term is a mathematical bound in real arithmetic; the floating-point allowance is an engineering safeguard, not interval-arithmetic proof of every machine operation. Because the exact curve lies on both cylinders and is the lift of both analytic pcurves, the same positional bound applies to surface distance and 3D/exact-pcurve mismatch. Finite BRep pcurves have not yet been realized.

## Focused measurements

The focused tests use a host radius of `0.79375 mm`, including the supplied McMaster clevis-pin hole radius `0.5953125 mm`. Each row samples 2,049 parameters after constructing the analytic bound. Values below are millimeters except counts.

| Cutter radius | Bézier spans | 3D controls | Bound incl. numeric allowance | Sampled 3D deviation | Host residual | Tool residual |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0.1 | 32 | 128 | 5.836e-7 | 4.330e-7 | 1.892e-7 | 3.868e-7 |
| 0.5953125 | 128 | 512 | 2.293e-7 | 8.012e-8 | 4.593e-8 | 9.001e-9 |
| 0.75 | 204 | 816 | 9.822e-7 | 7.420e-8 | 5.372e-8 | 1.134e-8 |

The two exact branches were sampled at 1,025 parameters for both cylinder equations and lifted pcurves. A rotated radial cutter checked frame independence. A centered-difference test checked the analytic tangent. Host seam unwrapping was checked across the negative cutter-axis branch, and tightening tolerance increased segment count. This is geometric qualification of the curve authority and finite 3D representation, **not** qualification of a solid cut.

Reproduce the focused tests:

```text
dotnet test Aetheris.Kernel.Core.Tests --no-restore --filter FullyQualifiedName~CylinderCylinderIntersectionCurveTests --logger "console;verbosity=detailed" --verbosity quiet
```

## Remaining topology and STEP boundary

The BRep model can hold a `CurveGeometry` with its exact intersection authority and certified 3D spline, but there is no cross-hole face/edge builder or finite host/tool pcurve realization. `BrepBooleanSafeCompositionGraphValidator` still rejects a transverse cutter. Thus no manifold solid or Hollow body, semantic `CrossHole.Wall`, source map, seam-split edge identity, or production STEP round trip exists. `Step242Exporter` can serialize the ordinary non-rational B-spline type, but no cross-hole topology has been wired to it; STEP support for this operation is **unqualified**. The supplied clevis STEP remains dimensional evidence only. No authored clevis or barrel-vent witness was generated. Accordingly, 1/8/32-hole BRep, export and tessellation timings are unavailable.

The next bounded construction must realize both pcurves with qualified 3D-lift error, bind them to seam-aware coedges, form the outer (and for Hollow, inner) trims and cutter wall, then pass manifold/preflight/export/reimport checks. Only then should Firmament `CrossHole`, the McMaster witness, and cylindrical Perforation lower through this path. No mesh or voxel Boolean has been introduced.

## Build and regression

The focused intersection tests passed 9/9, including the rotated-frame case added after the serial run. `dotnet build Aetheris.slnx --no-restore -m:1 --verbosity quiet` passed; the first full build reported seven existing WebAssembly warnings, and the final incremental build reported two. The serial `dotnet test Aetheris.slnx --no-build --no-restore -m:1 --verbosity quiet` run passed every project suite except one assertion in Kernel.Core (1,078 passed, 1 failed, including the first eight intersection tests). The previously recorded `SurfaceMeshIrTests.ThroughHolePlate_UsesMultiLoopPlanarBands_AndLowersToWatertightMesh` expects 90 planar cells and obtains 87; it exercises a planar box/cylinder hole and is isolated from the new intersection code. The failure reproduced in isolation. Its expected count was not rewritten, so the full solution test result is **not green**.
