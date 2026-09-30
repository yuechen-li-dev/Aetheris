# 3DM-BREP-BIND-X0 qualification

**Verdict: Meaningful progression.** Aetheris now turns 22 of 27 local Cartesian/Formas Product Design BReps into enclosed, oriented Aetheris bodies, trusted non-rational/analytic STEP files, and successful STEP reimports. Five bodies remain partial, so the complete Product Design model is not yet available as one usable artifact.

## Binding path

`Aetheris.ThreeDm.ThreeDmBrepBinder` maps source vertices and shared edge indices once, then reconstructs trim order, coedge sense, outer/inner loop roles, face sense, and a shell. Rhino analytic planes, cylinders, cones, spheres, and tori become native supports when Rhino recognizes them. Unknown rational surfaces and curves reuse the X2 shared reducers and carry `SplineRecoveryProvenance` with source object UUID and face/edge index. Exact non-rational spline supports stay spline-backed. Authored Firmament geometry is unchanged.

The binder reuses Rhino UV trims where they lift onto the recovered support within 0.1 mm; otherwise it calls `BrepPcurveRecovery.Populate` and `BrepPcurveValidator`. It makes no unrestricted topology repairs or endpoint snaps. The shared pcurve inverter now treats its finite-difference denominators as parameter spans, avoiding a false world-tolerance divide-by-zero on narrow domains. A genuinely closed polynomial spline edge is admitted by STEP preflight when its declared trim endpoints coincide. These changes preserve the authored geometry invariants.

Run locally:

```text
aetheris recover-3dm testdata/3DM/cartesian-product-metres.3dm --bind-step --out-dir artifacts/local/3dm-bind-x0/product --json
```

The exporter is the trusted production route with enforced preflight. Every emitted body is reimported and checked for enclosure, orientation, and sampled bounding-box drift. Output is deterministic per source object (`object-NNN.step`). All derived files remain ignored and local; no Cartesian/Formas source asset is redistributed.

## Product Design witness at 0.1 mm

| Metric | Result |
| --- | ---: |
| Source BReps / faces / trims | 27 / 185 / 742 |
| Bodies exported and reimported | 22 |
| Bodies partial | 5 |
| Faces / trims bound across all attempts | 130 / 443 |
| Faces / trims in qualified bodies | 99 / 302 |
| Recognized analytic / exact non-rational spline / recovered rational spline faces in qualified bodies | 64 / 4 / 31 |
| Retained Rhino UV pcurves / recovered pcurves in qualified bodies | 82 / 220 |
| Qualified enclosed, orientation-consistent shells | 22 |
| Worst qualified pcurve lift residual | 0.09907974 mm |
| Worst sampled source-to-STEP bounding-box coordinate drift | 0.00189385 mm |
| Total warm CLI time | 9.33 s |

Source read took 4.89 ms; the X2 support study took 1.39 s; per-body binding totaled 3.07 s; export, reimport, and their checks totaled 3.17 s. These timings do not isolate face construction from trim binding within each body.

Object 3 is a compact recovered-surface witness: 3 faces and 6 trims bound, 2 Rhino pcurves retained, 4 pcurves recovered, 0.01095 mm worst pcurve residual, enclosed and oriented, STEP reimport successful. Its sampled bounding-box coordinate drift is 0.0002455 mm. Object 8 is a larger 9-face, 42-trim witness. Source-edge and STEP-reimport wireframes show the same ring/block outline, with no obvious missing face or exploded trim. The local source, STEP, and render references are `artifacts/local/3dm-probe/body-3-source.png`, `artifacts/local/3dm-probe/body-3.png`, `artifacts/local/3dm-probe/body-8-source.png`, and `artifacts/local/3dm-bind-x0/product/object-008.png`.

## Partial bodies and next boundary

| Source object | First decisive finding |
| ---: | --- |
| 1 | Trusted STEP preflight finds a cylinder trim 0.000012966 mm off support, above its 0.000001 mm kernel gate. The source recovery budget is 0.1 mm; no global kernel tolerance was relaxed. |
| 5 | Face 1, loop 1 has a singular Rhino trim without a 3D edge. |
| 21 | Trusted STEP preflight finds edge-curve endpoint mismatches. |
| 22 | The bound shell has disconnected face components. |
| 26 | Face 1, loop 1 has a singular Rhino trim without a 3D edge. |

The per-body JSON under `artifacts/local/3dm-bind-x0-product.json` preserves detailed diagnostics and STEP paths. Successful bodies remain available when a different body fails. No snap, trim-closure adjustment, seam split, or healing operation was applied.

Furniture source object 1 (6 faces, 24 trims) also binds, exports, and reimports; it retains 16 Rhino pcurves, recovers 8, and has 0.00907 mm worst lift residual. Furniture object 0 remains partial because two spline pcurves could not be inverted within 0.1 mm.

The complete lamp/Product Design model is still missing five BReps. The next bounded work is singular-trim representation and measured resolution of the three remaining preflight/shell cases. Structure and Interior were not processed in this milestone.

**Downstream X1 closeout (2026-09-29):** All five former partials have bounded resolutions. All 27 source BReps now bind/export/reimport, and one local AP242 assembly contains 28 closed solids because source object 22 has two disconnected components. The X0 verdict and metrics above describe the original X0 run; see [3DM-BREP-BIND-X1.md](3DM-BREP-BIND-X1.md) for the later result and its source-topology limits.

## Validation

Release solution build passed with 0 errors. The fast kernel lane passed 1,002 tests; the full serial solution lane passed, including 452 CLI tests, 1,135 kernel tests, and 1,692 Firmament tests. Local Product Design and Furniture STEP roundtrip tests are included in the CLI lane when their ignored source files are present. The `Aetheris.FrictionLab.Tests` assembly reports no discoverable tests.
