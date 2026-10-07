# STEP-TRIM-TRIANGULATION-X4

Verdict: **Success — visual closeout accepted.** The broken screw and suspicious worm now have continuous local thread surfaces through the real Cadmata path. Codex reviewed all 26 matched-camera capture sets as Looks Correct. On 2026-10-07 the user reviewed all seven contact sheets and signed off all 26: “fantastic, everything looks good to me.” Engineering qualification remains independently 8 Qualified / 18 Degraded; visual acceptance does not promote degraded engineering imports.

## Actual failure and reproduction

The primary witness is `mcm-21`, `91280A546_Medium-Strength Class 8.8 Steel Hex Head Screw.STEP`, SHA-256 `e2770630a4bcd5e2d3623725a78105fe73466738b98f0a10e199489cb6acf088`. Rigid root 532 has 147 faces. Faces 143 / STEP #10869, 144 / #3300, and 145 / #2345 are nonrectangular spline thread supports.

Before editing, the current worker reproduced face 143 and production Cadmata captured the screw, worm and collar. Face 143 has one loop, 87 coedges, 2,664 sampled UV points, U `[0, 0.923627822598334]`, V `[0.014645322186259978, 0.9925395615913511]`. Its concave region covers about 96.38% of its UV bounding rectangle. The support is degree 1 by 2, has 2 U and 329 V knot values, and is not marked closed in either direction. It winds many times physically; it is not an angular seam-crossing cylinder or an invalid centre-fan polygon.

**Correction to the brief's premise:** the active path already used `PlanarPolygonTriangulator`, with the existing `EarCutTriangulator` as its bounded secondary strategy. There was no fan centre in this path. Ear-clipped diagonals crossed many native spline spans. Midpoint tests could alias repeated turning, and refinement using globally normalized UV edge lengths consumed the live-triangle cap before completing local quality. The visual result looked like a fan: flakes, long facets and incomplete thread regions. The original screw had 347,805 triangles; each of the three main spline faces reached approximately 65,536 triangles with fidelity warnings.

Evidence is local under `artifacts/local/step-trim-x4/before/`: exact support, ordered pcurves, UV loops, triangle output, timings and actual GPU pixels. `screw-cli.json` records CLI ground-truth inspection. Vendor sources are neither copied into fixtures nor committed.

## Bounded strategy

The display classifier distinguishes rectangular grid-like, convex, concave, holes and pathological input. It is deterministic dispatch, not competing interpretation, so it does not require a JudgmentEngine policy. Non-simple input is still checked by the shared triangulator. Periodic chart normalization remains upstream and is not reinterpreted as engineering topology.

Dense rectangular spline trims keep the X3 `QuadCell` path. `SplineRectangleTessellator.PlanGrid` now exposes its existing native-knot and physical-edge planning to nonrectangular spline trims. The rectangular algorithm and output remain unchanged.

For a dense nonrectangular spline:

1. Validate and triangulate the complete outer-minus-holes region with the existing utilities.
2. Clip the original loops into bounded knot-aligned cells and triangulate the local regions with Earcut. Its area guard rejects incomplete local triangulation.
3. If a clipped cell has awkward disconnected components or zero-width boundary bridges that the local utility declines, intersect the already validated global seed triangles with that cell. Only these convex intersections may use an anchor fan.
4. Evaluate vertices on the original support and run the existing bounded chord, centre-sag and normal checks. Bisection remains conforming and does not invent a support centre.
5. Join clipping roundoff at one billionth of a normalized display cell, retaining the first exact evaluated chart coordinate. Repeated vertex triangles are rejected. This affects disposable mesh adjacency only.

The first experiment clipped every global seed triangle across every grid cell. It retained far too many internal diagonals and exceeded the screw's 30-second budget, displaying only 142/147 faces. That experiment was rejected and its sweep interrupted; its diagnostics remain local. Direct local-region triangulation removed this allocation/refinement explosion. The subsequent complete corpus sweeps recovered all 5,552 faces.

No new polygon dependency was added. The existing Earcut port's upstream [ISC license](https://github.com/mapbox/earcut/blob/main/LICENSE) is now recorded in `THIRD_PARTY_NOTICES.md`. The planar triangulator remains the primary input validator.

## Holes, seams, orientation and identity

Inner bounds participate in each local outer-minus-holes region. Exceptional-cell fallback intersects valid seed triangles, so it cannot fill an excluded hole. Focused tests check concavity, a hole crossing several cells, winding, a many-turn support, a concave cylinder chart crossing 2π, classification and rejected self-crossed/collapsed input. Existing cylinder and closed-spline chart tests also remain in the gate.

Cylinder/cone coedges retain authored angular sweeps with integral-turn chart translation at joins. Closed spline normalization uses the support's native period and origin. Subdivision may duplicate seam locations for display; it never merges authoritative seam pcurves or changes BRep incidence. This is bounded support for the existing chart families, not universal repair of arbitrary periodic surfaces.

Generated triangles remain in one `DisplayFaceMeshPatch` with the original face ID. The existing canonical orientation projection is applied once by the owning display path. STEP source `SAME_SENSE` is not an orientation authority here. Cadmata click tests compare occurrence and BRep face identity, never triangle index, and exercise actual uploaded geometry.

The optional SurfaceMeshIR lowerers no longer fall back to an average-3D-centre fan for arbitrary boundary polygons. They use planar/local-chart polygon triangulation or reject the optional route. Existing structured cone-apex and regular support primitives retain their constrained construction; they are not a general trim fallback.

## The two suspicious parts

`mcm-11` worm, root 4950, face 28 / STEP #2647: the long ear-clipped facets and visible gaps were display defects. Local knot decomposition produces continuous helical surfaces aligned with the boundary overlay. Actual Cadmata orbit, close zoom and source-face click succeed.

`mcm-10` collar: face 72 / STEP #10166 on root 7849 is a spline thread surface, not an anonymous floating triangle. Outer cylinder face 37 / STEP #6094 has four loops: three inner bounds with 32, 4 and 33 coedges, plus the four-coedge outer bound. Its source boundaries explicitly describe the scalloped thread openings through the outer cylinder. An independent audit projects all 5,920 display triangle centres to that exact cylinder: zero centres lie in any of the three inner bounds, and all lie in the outer bound. This centroid audit is supporting evidence, not a proof about every complete triangle. Isolated solid and wire captures show the thread following those openings. Codex classifies the remaining chip-like appearance as source trim geometry rather than a detached tessellation flake. Human review should explicitly confirm this interpretation.

## Corpus, performance and regression evidence

All **26/26** files display all **5,552/5,552** bound faces. Engineering status remains **8 Qualified / 18 Degraded**; display improvements do not erase the existing 473 missing engineering pcurves. All **32/32** eligible rigid-root normalizations succeed with non-rational STEP, preserved topology counts and valid reimport pcurves. The four ball roots (1044, 4327, 4420, 4460) remain Qualified with zero orientation ambiguities and enclosed manifold incidence. The screw and connector normalization witnesses remain valid.

The final diagnostic-only cleanup was checked against the prior capture geometry: all 26 `mesh.json` files are byte-identical. Final code was additionally exercised by fresh production Cadmata captures for the three motivating parts. Raw output remains under ignored `artifacts/local/step-trim-x4/`; no generated diagnostic dump is promoted into source control.

| Part | Display ms before / after | Triangles before / after | Fidelity warnings before / after |
|---|---:|---:|---:|
| mcm-01 bevel pinion | 583 / 1,034 | 237,550 / 163,088 | 0 / 0 |
| mcm-10 collar | 3,538 / 2,874 | 1,000,455 / 430,599 | 12 / 2 |
| mcm-11 worm | 3,285 / 2,539 | 711,326 / 310,846 | 8 / 4 |
| mcm-21 screw | 1,224 / 2,329 | 347,805 / 228,571 | 3 / 3 |
| mcm-24 spring | 102 / 111 | 24,044 / 24,044 | 0 / 0 |

These are recorded worker wall times, including concurrent qualification activity, not a controlled microbenchmark. The largest observed changes stay within a few seconds, not two orders of magnitude. The screw's three remaining warnings describe bounded knot-grid planning, not the old exhausted 65,536-triangle refinement on each principal thread face. Worm warnings remain on other bounded spline faces; principal face 28 has none.

| Source face | Triangles | p95 / maximum 3D edge, mm | p95 longest/shortest edge ratio | Worst raw ratio |
|---|---:|---:|---:|---:|
| Screw 143 / #10869 | 26,248 | 1.457 / 1.702 | 19.32 | 2.29e9 |
| Screw 144 / #3300 | 24,575 | 1.247 / 1.292 | 95.79 | 5.51e6 |
| Screw 145 / #2345 | 26,554 | 1.448 / 1.580 | 19.20 | 2.30e9 |
| Worm 28 / #2647 | 12,835 | 0.539 / 0.633 | 3.80 | 1.02e3 |
| Collar 72 / #10166 | 12,333 | 0.173 / 0.218 | 3.98 | 1.40e9 |

**Quality limit:** this is not a globally shape-optimal mesh and not every sliver is removed. Near-coincident trim/grid intersections still create tiny boundary edges with very large raw ratios; the thin screw crest also needs elongated cells. The repaired main surfaces no longer have the large whole-turn diagonals and missing strips visible before X4. Reported physical edge sizes, raw aspect outliers and explicit budget warnings are retained rather than calling all triangles well-shaped. No arbitrary sliver deletion changes the source trim.

The spring remains 6,144 + 5,856 = **12,000 quads**, 24,000 spline triangles plus 44 end-face triangles, with no fidelity warning. Its grid, counts and sampled aspect statistics match X3. Gear sleeve repair and four ball roundtrips remain independent of the new path. All eligible normalized roots must preserve topology, valid pcurves and non-rational AP242 output. Engineering recovery remains bounded by the existing policy, at most 0.1 mm for the applicable ordinary imported parts; display quality never supplies export evidence.

Spring quad edge-ratio median / p95 / worst is 2.962 / 3.997 / 5.542 on face 1 and 2.548 / 4.119 / 6.762 on face 2, unchanged from X3.

The nine focused general-trim tests and all 18 NIST display cases pass (27/27 together). A late diagnostic-only attempt to warn whenever outer-minus-holes triangulation declined caused six NIST failures: established periodic charts intentionally use the existing native-grid mask. The warning was narrowed to abandonment of the new knot-local spline strategy; established periodic dispatch remains unchanged, and all 18 regressions pass without relaxing tests.

Validation: the current serial full-solution gate passes **4,380 tests across 20 populated test projects, zero failures and zero skips**, recorded in `full-gated.log`. The FrictionLab test project currently discovers no tests. Fast core lane passes 1,056/1,056. Solution build passes with zero errors and two existing SQLite WASM warnings. Frontend typecheck and build pass; 89 tests across 19 files pass. Shared Telos has 14 passing tests, including packed-face source picking and explicit field/proxy projection. Helios has 29 passing tests, successful TypeScript/development build and one real WebGPU/WASM inspection browser test. Helios's ordinary production build explicitly stops because the installed local SDK is the development package; its existing AOT SDK distribution gate was not bypassed or qualified by this work. Frozen Zig was skipped because its compiler/build infrastructure did not change.

## Supported Cadmata and Helios capability

The inspection projection now lives in `@aetheris/three-telos`, exported as `inspectSurfaces`, `surfaceInspectionFaces`, `inspectionFaceKey` and typed mode/face contracts. Cadmata consumes it through a compatibility re-export, and Helios consumes the same implementation in its actual `Viewport`. Both expose human controls for normal, surfaces, BRep wire, translucent overlay, face colours and occurrence-aware isolation. Camera and original source selection survive switching views.

Packed SDK meshes use triangle draw ranges sharing original GPU geometry; CPU picking keeps original triangle indices. Whole-model views show actual CIR/SDF fields. Isolated field faces and face colours explicitly use the retained BRep proxy. Helios labels that distinction and the absence of face-edge adjacency; its isolated wire shows occurrence edges. Cadmata's richer STEP metadata restricts wire to the isolated source face's edges.

`helios-inspection/` records the actual Helios wrapper with compiled Firmament WASM, five rendered modes, six source faces, matching cameras and a real click resolving `Body:occurrence / face:2 / original triangle 2`. The persistent `HeliosCAD/tests/surface-inspection.spec.ts` verifies the product control and source selection. `product-formalized/` records fresh current production Cadmata upload/orbit/zoom/isolation/click evidence. The human guide is [Surface and trim inspection](../public/firmament/surface-inspection.md). Screenshot/corpus capture remains a TypeScript harness through real product controls, with deterministic output under ignored artifacts, not another renderer.

## Visual review and exact human handoff

The final all-part capture folder is `artifacts/local/step-trim-x4/inspection-cleanup/`. Seven `contact-sheet-N.png` files cover all 26 parts in surfaces, wire, translucent overlay and face-colour overlay. Each specimen also has five full-resolution PNGs and its camera/source-face inventory. Before/after images are `mcm-21-before-after.png`, `mcm-11-before-after.png`, and `mcm-10-before-after.png`. `product-formalized/` contains fresh current close, orbit, isolated and picked source-face captures for the three motivating parts.

Codex's per-part review is recorded below and in `visual-review.json`.

| Specimen | Part | Codex visual classification | Evidence note |
|---|---|---|---|
| mcm-01 | Metal Bevel Pinion | Looks Correct | Bore, hub and bevel teeth align with source boundaries. |
| mcm-02 | Metal Bevel Gear | Looks Correct | Bevel tooth ring and stepped hub are continuous. |
| mcm-03 | Steel Taper-Lock Bushing-Bore Sprocket | Looks Correct | Sprocket teeth, taper bore and bolt openings remain open. |
| mcm-04 | NO THREADS_Metal Gear - 20 Degree Pressure Angle | Looks Correct | Sleeve and tooth tips follow source wire; no detached face. |
| mcm-05 | NO THREADS_Metal Gear - 20 Degree Pressure Angle | Looks Correct | Stepped sleeve and tooth-root transitions remain joined. |
| mcm-06 | Metal Gear - 20 Degree Pressure Angle | Looks Correct | Gear teeth and cylindrical hub follow the wire. |
| mcm-07 | Metal Gear - 20 Degree Pressure Angle | Looks Correct | Hub, teeth and radial opening match the source curves. |
| mcm-08 | Metal Internal Gear - 20 Degree Pressure Angle fusion | Looks Correct | Fusion internal gear has an open central bore and continuous ring. |
| mcm-09 | Metal Internal Gear - 20 Degree Pressure Angle | Looks Correct | Legacy internal gear has consistent internal teeth and open centre. |
| mcm-10 | Clamping Two-Piece Shaft Collar | Looks Correct | Thread breakthrough matches source inner bounds; isolated cylinder/thread audit supports source geometry. Human collar confirmation required. |
| mcm-11 | Steel Worm | Looks Correct | Repaired helix is continuous; long facets and missing strips are removed. |
| mcm-12 | Steel Ball Bearing | Looks Correct | Bearing races and spherical ball silhouettes remain plausible; four ball roundtrips independently valid. |
| mcm-13 | Pawl for 6 mm Wide Face Metal Ratcheting Gear | Looks Correct | Pawl outline and pivot hole match the trim wire. |
| mcm-14 | Metal Ratchet Gear | Looks Correct | Ratchet teeth and bore remain continuous and open. |
| mcm-15 | NO THREADS_Single U-Joint | Looks Correct | U-joint ends, yokes and pivots align with source curves. |
| mcm-16 | Metal Miter Gear | Looks Correct | Miter teeth, hub and bore remain coherent. |
| mcm-17 | Compact Push-In Signal-Power Connector | Looks Correct | Connector lance and contact details align with source wire. |
| mcm-18 | Mounted Steel Ball Bearing with Cast Iron Housing | Looks Correct | Housing mounting holes and bearing races remain open and coherent. |
| mcm-19 | NO THREADS_Zinc-Plated Steel Hex Nut | Looks Correct | Unthreaded nut bore and hex flats match source geometry. |
| mcm-20 | Zinc-Plated Steel Hex Nut | Looks Correct | Internal nut helix and bore remain visible. |
| mcm-21 | Medium-Strength Class 8.8 Steel Hex Head Screw | Looks Correct | Repaired screw helix is continuous; no detached flakes in orbit/close/isolation views. |
| mcm-22 | NO THREADS_Medium-Strength Class 8.8 Steel Hex Head Screw | Looks Correct | Unthreaded screw shaft and maker-mark head remain coherent. |
| mcm-23 | Interchangeable Point Lathe Center Point | Looks Correct | Lathe centre tip facets and shaft shoulder follow source topology. |
| mcm-24 | Compression Spring | Looks Correct | Spring coils and ends remain continuous; 12000-quad grid is unchanged. |
| mcm-25 | 1050-1095 Spring Steel Slotted Spring Pin | Looks Correct | Spring pin slot and open bore remain intact. |
| mcm-26 | Carbon Steel Clevis Pin | Looks Correct | Clevis pin head, shaft and cross hole remain coherent. |

**Human review completed on 2026-10-07:** the user reviewed the seven contact sheets showing all 26 models and accepted every model. The original review checklist below is retained for reproduction:

- Review all seven sheets; assign each specimen Looks Correct / Suspicious / Broken and record reviewer/date.
- Open the full-size screw and worm before/after and close views. Confirm continuous helix, no detached flakes, no filled trim region or large boundary crossing.
- Check collar cylinder #6094 and thread #10166 in the isolated views; confirm the source scalloped openings explain the exposed thread rather than accepting an unexplained chip.
- Confirm gear sleeves, spring coils/end trims, ball-bearing shapes, connector detail and visible holes remain plausible against the wire.
- Record any objection with specimen, occurrence, face/STEP entity and capture name. A Broken qualified specimen blocks acceptance.

The human visual gate is now closed. X4 visual closeout is **Accepted** with the mesh-quality limits above documented. Machine geometry/normalization qualification remains separately reported.

## Reproduction

Build the solution with `dotnet build Aetheris.slnx -c Release -m:1`. Run `scripts/qualify-step-legacy.ps1 -CorpusDirectory <local-McMaster-folder> -OutputDirectory artifacts/local/step-trim-x4/final-gated -NoBuild`.

For the actual product captures, run the current server DLL in the foreground on port 5087 with `--no-browser`, then invoke `scripts/inspect-step-legacy-cadmata.mts` with the Playwright module, corpus directory, ignored output directory, host URL, comma-separated specimen IDs and optional source STEP face IDs. Supply empty probe points and `product` as the final argument for orbit/zoom/click witnesses. The script installs no service. Stop the host after capturing.
