# SurfaceMeshIR M5: foreign B-spline trims

Status: implemented and validated against NIST CTC-01.

> B-spline trim support in Mesh IR is an approximation-layer concession for foreign STEP boundaries, not general NURBS geometry support.

## Boundary and decision

Aetheris still admits only Plane, Cylinder, Cone, Sphere, and Torus as exact SurfaceMeshIR supports. `BSplineSurface` and NURBS support surfaces remain rejected. M5 only evaluates a non-rational `BSpline3Curve` already present as a trim in an imported foreign B-rep. The imported B-rep is not rewritten.

The trim-resolution order is:

1. retain an existing analytic edge;
2. recover an analytic edge only when the adjacent supports imply a bounded supported family and strict position, tangent, span, and orientation checks pass;
3. otherwise sample the imported non-rational spline by knot span under the active mesh policy.

`ForeignTrimResolution` records the source edge/kind separately from the meshing resolution, shared sample plan, adjacent support families, recognition evidence, tolerance, and provenance. This is a clean future hook for import recognition, but M5 does not wire it into authoritative STEP canonicalization.

## CTC-01 audit

The imported body is one enclosed manifold with 117 faces (56 Plane, 57 Cylinder, 4 Cone), 318 edges, and no spline support surfaces. The 14 apparent spline trims are all native STEP `B_SPLINE_CURVE_WITH_KNOTS`, degree 3, non-rational, open, non-periodic, parameterized on `[0,1]`, and imported without weights. Every adjacent pair is Cylinder ↔ Cylinder.

| Mesh edge | Adjacent faces | STEP EDGE_CURVE | STEP spline | Controls | Multiplicities |
|---:|---:|---:|---:|---:|---|
| 87 | 10, 67 | #1810 | #648 | 6 | 4,1,1,4 |
| 112 | 20, 68 | #1835 | #649 | 6 | 4,1,1,4 |
| 151 | 30, 71 | #1874 | #650 | 6 | 4,1,1,4 |
| 180 | 44, 64 | #1903 | #651 | 6 | 4,1,1,4 |
| 221 | 64, 65 | #1944 | #652 | 8 | 4,1,1,1,1,4 |
| 222 | 66, 67 | #1945 | #653 | 8 | 4,1,1,1,1,4 |
| 223 | 68, 69 | #1946 | #654 | 8 | 4,1,1,1,1,4 |
| 224 | 70, 71 | #1947 | #655 | 8 | 4,1,1,1,1,4 |
| 283 | 99, 104 | #2006 | #656 | 7 | 4,1,1,1,4 |
| 284 | 99, 100 | #2007 | #657 | 7 | 4,1,1,1,4 |
| 286 | 100, 101 | #2009 | #658 | 7 | 4,1,1,1,4 |
| 288 | 101, 102 | #2011 | #659 | 7 | 4,1,1,1,4 |
| 290 | 102, 103 | #2013 | #660 | 7 | 4,1,1,1,4 |
| 292 | 103, 104 | #2015 | #661 | 7 | 4,1,1,1,4 |

The complete knot vectors, control points, evaluated endpoints, endpoint tangents, orientations, and face support classifications are persisted in `evidence/surface-mesh-ir-m5/ctc-01-trim-edge-audit.json`.

### Recovery result

None of the 14 curves was exporter noise representing a supported line or conic. Cylinder-cylinder intersections do not, in general, imply one of Aetheris's bounded Line/Circle/Hyperbola trim families. A strict 33-point direct line test failed by 1.565–3.380 mm against a 0.05 mm recognition tolerance. No curve was falsely promoted.

Statistics:

- already analytic after import interpretation: 0 of these 14;
- recovered from adjacent supports: 0;
- recovered by direct recognition: 0;
- sampled as genuine non-rational B-spline trims: 14;
- unsupported: 0.

## Evaluation and sampling

`BSpline3Curve` uses standard de Boor evaluation over its expanded knot vector. M5 adds the exact derivative control polygon for deterministic tangent evaluation and exposes clipped, non-zero knot spans. Repeated knots therefore establish explicit subdivision boundaries.

Each span is traversed left-to-right. For `[t0,t1]`, the sampler evaluates endpoints, midpoint, and endpoint/midpoint tangents. It subdivides when either midpoint-to-chord distance exceeds `SurfaceMeshPolicy.TargetChordalError` or tangent change exceeds `TargetNormalErrorRadians`. Refinement depth and total boundary samples are hard bounds; inability to meet policy fails explicitly.

| Edges | Samples per edge | Max chord deviation | Max tangent deviation |
|---|---:|---:|---:|
| 87, 112, 151, 180 | 13 | 0.042294 mm | 0.059644 rad |
| 221–224 | 13 | 0.047220 mm | 0.085233 rad |
| 283, 284, 286, 288, 290, 292 | 9 | 0.031890 mm | 0.124150 rad |

The active default limits were 0.05 mm chord error, 10° normal/tangent error, depth 8, and the display policy's maximum sample count. Repeated runs produce identical parameters, counts, cells, and OBJ bytes.

## Shared authority and analytic UV projection

Every B-rep edge is planned once. Both adjacent patches consume the same `SurfaceMeshVertex` IDs and positions; coedge reversal changes order only. There is no post-export weld.

- Plane: orthogonal coordinates against the plane's exact U/V axes.
- Cylinder: angular coordinate from the exact radial basis and axial projection onto the cylinder axis.
- Cone: angular coordinate plus exact axial parameter; the apex singularity has a bounded dedicated band.
- Sphere/Torus: existing analytic inverse mappings remain available, although CTC-01's spline trims do not touch them.

Periodic U values are unwrapped in ordered-sample continuity, choosing ±2π shifts so adjacent samples never jump across the representation seam. Every projected sample is evaluated back on the analytic support and rejected if residual exceeds policy.

## Sampled trim bands and topology closure

Cylinder and Cone first use structured four-sided correspondence where it is valid. A simple convex sampled loop may use an exact-support boundary band. A concave sampled loop is triangulated directly in its unwrapped analytic parameter domain; every authoritative boundary sample is retained, and no arithmetic-centroid inset is allowed to cross the trim. Plane-with-holes triangulation similarly splits any triangle edge that spans collinear authoritative samples, so all shared edge segments survive rather than being silently simplified. Unequal-count planar annuli align their inner and outer parameter-space rings deterministically before stitching.

CTC also exposed a generic three-edge cone-apex sector. Its exact cone UV singularity is handled with a localized boundary band; this is based on topology and support family, not CTC IDs.

Coverage progression was:

1. edge 87 rejected as unsupported `BSpline3`;
2. all spline edges planned, exposing four-sided cylinder start-side assumptions on face 1;
3. rotated four-sided correspondence, exposing sampled cylinder face 10;
4. sampled analytic boundary bands, exposing cone-apex face 57;
5. cone-apex band, exposing 710 planar boundary cracks caused by removed collinear samples;
6. boundary-conforming planar splitting and area-centroid polygon lowering: 0 cracks, 0 nonmanifold edges, 0 zero-area triangles.

## CTC-01 result

The direct command is:

```text
aetheris mesh testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --format obj --output ctc-01.obj --json
```

Results:

- 117 patches; legacy fallback count 0;
- 11,130 cells: 8,321 quads, 2,766 triangles, 43 boundary polygons;
- 74.76% quad cells;
- 10,426 SurfaceMeshIR/OBJ positions and 20,974 lowered STL triangles;
- maximum measured boundary chord deviation 0.048934 mm;
- closed, connected, outward, 0 cracks, 0 nonmanifold edges, 0 duplicate or zero-area triangles;
- OBJ: 1,758,087 bytes, SHA-256 `3f465cd36e95a256aa472cc654b4fcb5b680c08f0d30be5541be98ccb68614d7`;
- STL: 1,048,784 bytes, SHA-256 `fb0e2208c9f1bb84c14966e8b938b84cba677b5306104d1f760476cf60ee2ccc`.

### Visual artifact correction

External Fusion inspection exposed long wedge/fan artifacts at the four large cylinder-cylinder corner transitions, an incomplete correction on the paired end transitions, and a pinched spoke at an unequal-count mounting-hole annulus. Boundary-only UV triangulation had a valid trim domain but joined distant samples with straight 3D chords, so it visibly cut through the cylindrical support. The corrected planner recognizes the monotone sampled cylinder strips on faces 10, 20, 30, 44, 65, 66, 69, and 70, inserts deterministic parameter rows evaluated on the exact cylinder, and joins unequal boundary sampling with localized zipper cells. Both halves of each transition now use the same support-following treatment. Annuli retain deterministic minimum-cost parameter-space ring alignment, and concave planar n-gons are explicitly constrained before export rather than leaving an importer to choose an invalid fan.

Regression validation requires quads and exact-support interior vertices in every affected cylinder patch, retains every authoritative boundary sample, bounds each cell to less than 0.2 radians of cylinder angle, checks support-normal winding, and confirms the final lowering is watertight with zero degenerate cells.

One warm corrected-geometry run recorded 134.2 ms for STEP/B-rep import, 2,449.7 ms for SurfaceMeshIR planning/validation, and 1,764.3 ms for OBJ lowering/serialization. Structured buffers are estimated at 464,520 bytes; sampled mesh edges range from 0.8486 to 38.8889 mm. These are diagnostic single-run timings, not a statistically controlled benchmark.

Fusion and Blender are installed on the validation machine, and Fusion supplied the visual evidence that drove this correction. Automated reimport/capture was unavailable in the current app-control session, so the regenerated OBJ still requires a fresh manual Fusion inspection before claiming final visual closeout. The OBJ topology checks and independent triangle lowering prove the corrected file remains closed and printable.

## Limitations

- Rational spline trims remain a separately typed policy question; CTC-01 contains none. Existing importer recovery for rational quadratic circles is unchanged.
- Spline/NURBS support surfaces remain inadmissible.
- Direct analytic recognition is intentionally narrow; M5 does not add arbitrary surface/surface intersection or general curve fitting.
- The sampled-boundary band is bounded to simple manifold chains/loops on supported analytic faces, not a universal freeform remesher.
- Fresh Fusion/Blender visual capture of the corrected evidence OBJ remains outstanding; importer automation was unavailable in the validation session.

The next mesh milestone should add visual DCC evidence and improve the exceptional-cell core of sampled cylinder-cylinder intersection neighborhoods while retaining the now-proven shared boundary and zero-fallback contracts.
