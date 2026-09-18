# DCSD-DOGFOOD-X0 — Dual Contouring sharp-feature recovery

## Executive verdict

**Verdict: Useful but bounded.** Dual Contouring of Signed Distance Data (DCSD) materially improves reconstruction of sharp geometry from a sampled SDF. On the exact 80 × 50 × 25 mm Aetheris box at 32³ cells, forward surface RMS fell from Marching Cubes' 0.1700 mm to 0.0201 mm. Exact-corner RMS fell from 2.117 mm to 0.0216 mm, and candidate sharp-edge p95 error fell from 2.715 mm to 1.307 mm. At 64³, DCSD reached 0.0099 mm forward RMS and 0.0105 mm corner RMS.

That result does **not** qualify the output as CAD geometry. The published optimizer took 96.3 s at 32³ and 619.5 s at 64³ for the sharp box on this CPU, versus 6.8 ms and 51.9 ms for Marching Cubes. The triangulated DCSD box had 6 and 12 nonmanifold edges; its 32³ p99 aspect ratio was 403 and its smallest triangle angle was 0.002°. Thin and bent-sheet outputs were worse: the 0.5 mm plate had 162 nonmanifold edges, and the real ProfileDelta sheet had 318. The reconstruction is useful sharp-feature evidence, not a generally admissible downstream mesh.

DCSD earns a place only as an external, experimental `SampledField -> ApproximateExplicitMesh` reconstruction candidate. It must not become BRep, Firmament, CIR topology, manufacturing, or feature-history authority. **Do not port it to .NET yet; wrap the pinned implementation first.**

The PAT/DCSD complementarity hypothesis is plausible but not proven enough to start mesh stitching. DCSD owns the sharper zero-set candidate; PAT remains the useful fast pointwise field for smooth, oriented point clouds. A smallest follow-up should compare both methods from one identical sampled source and add a topology/manifold admissibility gate before any region-level Judgment Engine experiment.

## Reproduction and licensing

| Item | Recorded value |
|---|---|
| Repository | `xianacarrera/dual-contouring-of-signed-distance-data` |
| Revision | `fa1962cd5825cfc9bb698569714f579e1d75a4e8` (2026-08-05) |
| Platform | Windows build 26200, MSVC 2022/18, CMake 4.4.3, Ninja, Python 3.11 |
| Hardware | AMD Ryzen 7 7700X, 8 cores / 16 logical processors, 31.1 GiB RAM; RTX 3070 present but reconstruction ran on CPU |
| Native module hash | SHA-256 `765A67BC0BC2393163873B4E2B58876142EEF2499DDDFCEE134A7C059871C79F` |
| Code license | Root `LICENSE` contains MIT terms, but the notice says `Copyright (c) 2019 Nick Sharp`, not the paper authors. Treat code provenance as unresolved until legal review. |
| Paper license | The paper PDF is marked CC BY-NC-ND 4.0; that grant is distinct from source code. |
| Model/weights | None. This method is not learned. |
| Bundled sample data | No repository-wide sample-data grant was found. Several scripts refer to third-party model sources. Upstream data stays fetched under ignored `artifacts/local`; no sample is redistributed here. |
| Dependencies | C++/Eigen/libigl/Polyscope/GLFW/OpenGL plus the repository submodules; Python uses NumPy, SciPy, gpytoolbox, libigl, Matplotlib, and trimesh. Subdependency licenses are mixed and must be audited before binary redistribution. |

The upstream repository says it was tested on macOS. Reproduction required four compatibility changes in the ignored checkout:

1. Add missing `<vector>` includes to `contouring.h` and `ui.h`.
2. Exclude obsolete `sample_fun.cpp`; it defines a second `contouring()` and MSVC selected that placeholder, causing `Ours` to return an empty mesh.
3. Change three OpenMP loop forms to MSVC-compatible indexed/signed loops.
4. Disable OpenMP for the benchmark. Although the patched parallel build compiled, the 32³ demo exceeded three minutes instead of the serial path's approximately 82 seconds. The objective, weights, iterations, Hermite update, and connectivity were not changed.

The C++ still initializes Polyscope/OpenGL even for headless Python calls. It does not show a window, but it is an unnecessary runtime/UI dependency for a future wrapper.

### Author-data demo

The demo consumes the repository's `data/cube.obj`, normalizes it, applies the authors' exact legacy-NumPy random rotation (seed `919993`) to avoid grid bias, samples a 33 × 33 × 33 node grid, supplies only scalar SDF values, and runs the published 100 outer / 100 inner iterations. DCSD produced 3,878 vertices and 3,878 quads (7,756 triangles after deterministic splitting) in 40.00 s. The qualified sampled-surface RMS was 0.0153 normalized longest-bbox units. Marching Cubes took 6.2 ms.

Visual: `artifacts/local/dcsd-x0/demo/author-cube.png`.

One important code-level boundary appeared immediately: active cells require both a strictly negative and a strictly positive corner. An axis-aligned cube whose zero set lands exactly on grid nodes can therefore produce no active cells. The authors rotate inputs to avoid this grid degeneracy; the Aetheris adapter does the same with seed `919993`.

## Implementation observed

The public path accepts a one-dimensional array of scalar samples `S`, regular-grid positions `GV`, three node resolutions, an iso value, and `ContouringOptions`. Negative means inside. It is a fixed, full regular grid; there is no adaptive octree path in the observed binding.

For each cell containing strict positive and negative corner values, sign-changing primal edges generate Hermite positions by linear interpolation. Normals come from finite differences/trilinear differentiation of sampled `S`; X0 passes neither Aetheris normals nor the optional exact-SDF/gradient callback. This verifies the gradient-free path.

Classic Dual Contouring averages constraints and solves a QEF. The paper's `Ours` path begins from Hermite centroids, assigns sampled-SDF spheres to cells/face intersections, and iterates vertex refinement. With `hermite_update=true`, later outer iterations update Hermite positions and normals from adjacent cell vertices. Per-cell inner minimization combines sphere constraints, a DC/QEF term, SVD thresholding, and regularization to the preceding vertex. Published settings used here are `mu=0.1`, `dc_weight=0.02`, `sphere_weight=1`, `svd_threshold=0.01`, three update weights of `0.2`, 100 outer iterations, and 100 inner iterations.

Connectivity is built around sign-changing primal edges, joining their four incident dual cells. The C++ output is quads even though the Python docstring says triangles. The harness splits each quad along `(0,1,2)` and `(0,2,3)` only for Aetheris-style triangle validation. This split does not explain edges shared by more than two faces; the reported nonmanifold counts reflect upstream connectivity/duplicate incidence and need a focused audit.

Boundary handling assumes a meaningful volumetric sign field and a padded closed zero set. A finite open surface cannot, in general, be recovered from `phi=z`: that field describes an unbounded plane and contains no finite patch boundary.

## Deterministic Aetheris adapter

`Aetheris.CLI mesh` exports the exact/qualified Firmament/BRep fixtures to ignored OBJ files. The SheetMetal witness uses the real authored `profile-delta-tab-family.firmament`, its `ProfileDelta` specialization, `aetheris build` to formed STEP, `sheetmetal inspect`, and then exact-BRep tessellation. DCSD is never fed PAT output.

The adapter:

1. Centers each source at its bounding-box center and divides by its longest dimension, matching upstream `normalize_points`. This normalization is mandatory because the optimizer uses fixed dimensionless weights.
2. Applies one deterministic rotation to avoid grid-aligned exact-zero degeneracy.
3. Pads every rotated bound by 8% of the normalized longest dimension.
4. Samples analytic signed distance for sphere, cylinder, torus, sharp box, the exact triangular wedge prism, and plates. Other BRep fixtures use qualified signed triangle distance; those measurements inherit Aetheris tessellation error.
5. Runs the standard sampled-SDF path with no exact gradient oracle.
6. Transforms vertices back to millimetres and labels the result an approximate mesh.

The full-grid input lower bound is 32 bytes/node (three float64 coordinates plus one float64 SDF): 1.15 MB at 33³ nodes, 8.79 MB at 65³, about 68.7 MB at 129³, and about 543 MB at 257³, before cells, sphere assignments, QEF state, mesh, and library overhead. For the bent sheet at 32³, grid creation took 0.00036 s and qualified SDF sampling 0.0830 s; reconstruction dominated at 173.1 s.

Raw evidence lives under ignored `artifacts/local/dcsd-x0/benchmark`. The compact, reviewable evidence is in `DCSD-DOGFOOD-X0.evidence.json`.

Visual evidence includes `sharp_box-comparison.png` (same-camera exact/MC/DCSD), `sharp-error-heatmap.png`, `sharp-corner-closeup.png`, `sdf-grid-slice.png`, `sharp-noise.png`, the sphere/thin-sheet/ProfileDelta comparisons, `thin-sweep.png`, `plots.png`, and the independent `sharp-wedge-prism-comparison.png`. These are generated artifacts and intentionally remain under `artifacts/local`.

## Primary accuracy and performance

The surface values below are one-way reconstructed-surface errors. `Sym RMS` adds the reverse sampled direction. Analytic fixtures use exact SDF in the forward direction; BRep fixtures use a deterministic dense-surface proxy and are labeled qualified rather than exact.

| Fixture | Grid / voxel | Method | Time | RMS | p95 | max | Sym RMS | Topology finding |
|---|---:|---|---:|---:|---:|---:|---:|---|
| Sphere | 32³ / 0.542 mm | MC | 0.007 s | 0.0080 | 0.0114 | 0.0141 | 0.0860 | closed manifold |
| Sphere | 32³ / 0.542 mm | DCSD | 79.35 s | **0.0024** | **0.0046** | **0.0101** | 0.0879 | closed manifold |
| Sharp box | 32³ / 3.530 mm | MC | 0.007 s | 0.1700 | 0.4249 | 1.4990 | 0.3773 | closed manifold |
| Sharp box | 32³ / 3.530 mm | DCSD | 96.27 s | **0.0201** | **0.0280** | **0.7553** | **0.3195** | 6 nonmanifold edges |
| Sharp box | 64³ / 1.765 mm | MC | 0.052 s | 0.0628 | 0.0931 | 0.6667 | 0.3142 | closed manifold |
| Sharp box | 64³ / 1.765 mm | DCSD | 619.53 s | **0.0099** | **0.0095** | **0.5086** | **0.3025** | 12 nonmanifold edges |
| 0.5 mm plate | 32³ / 1.778 mm | MC | 0.006 s | 0.1322 | 0.2344 | **0.2500** | 0.4673 | 13 components |
| 0.5 mm plate | 32³ / 1.778 mm | DCSD | 141.89 s | **0.1161** | **0.2306** | 0.8500 | **0.3013** | 1 component, 162 nonmanifold edges |
| ProfileDelta bend | 32³ / 4.205 mm | MC | 0.007 s | 0.9111 | **1.4270** | **2.3331** | 1.1068 | 8 components |
| ProfileDelta bend | 32³ / 4.205 mm | DCSD | 173.07 s | **0.9018** | 1.4477 | 3.4621 | **0.9118** | 4 components, 318 nonmanifold edges |

Doubling the sharp-box resolution increased DCSD time 6.4×, from 96.3 s to 619.5 s. A 128³ published-profile run was not justified after this scaling; 256³ has 16.97 million nodes, a 543 MB input lower bound, and substantially larger optimizer state. These were deliberately omitted rather than presenting a machine-crashing benchmark as useful evidence.

### Sharp-box features

Candidate reconstructed feature edges are adjacency edges whose unsigned normal angle exceeds 30°. Their midpoints are measured against the nearest exact box segment. Corners are exact box corners measured to the nearest reconstructed vertex. This localized protocol is deterministic but correspondence-free; isolated false feature edges affect its max and RMS.

| Grid | Method | edge RMS / p95 | corner RMS / max | mean absolute 90° dihedral error | face-interior plane RMS |
|---|---|---:|---:|---:|---:|
| 32³ | MC | 1.632 / 2.715 mm | 2.117 / 2.982 mm | 38.54° | numerical zero |
| 32³ | DCSD | **1.113 / 1.307 mm** | **0.0216 / 0.0363 mm** | **19.11°** | 0.0109 mm |
| 64³ | MC | **0.830 / 1.367 mm** | 1.207 / 1.415 mm | 37.54° | numerical zero |
| 64³ | DCSD | 0.920 / **0.745 mm** | **0.0105 / 0.0152 mm** | **18.58°** | 0.0040 mm |

DCSD decisively recovers corners and typical edge locations. Its mean dihedral estimate improves by roughly half but remains about 19° wrong, and its edge RMS at 64³ is slightly worse because false/outlier feature edges remain. The method preserves an excellent zero set while producing poor local triangles: at 32³, minimum angle was 0.002°, p99 aspect 403, and 5.06% of triangles had angle below 5°. Feature recovery and mesh admissibility are separate questions.

### CAD feature screen

The 10 outer / 20 inner screen is diagnostic, not the primary quality claim. At 32³ it produced: chamfered box 0.14 mm RMS, rounded rectangle prism 0.33 mm, through hole 0.78 mm, counterbore 0.71 mm, filleted box 0.92 mm, phone-like chassis 1.08 mm, and bent sheet 0.91 mm. All were one connected component except the bent sheet (four); every case except sphere had at least one nonmanifold edge. The screen confirms broad execution but does not establish chamfer width, bore diameter/depth, bend radius, or region-level normals to CAD tolerance.

An additional exact Aetheris witness, `sharp-wedge-prism.firmament`, extrudes a 60 × 40 mm right-triangle section through 20 mm. Its benchmark path uses the exact analytic Euclidean SDF of that closed triangular prism, not tessellated-distance qualification. At 32³ (2.318 mm voxel, 10 × 20 screen), Marching Cubes produced 0.1359 mm forward RMS with a closed manifold mesh. DCSD improved forward RMS to 0.0534 mm and p95 to 0.0304 mm, but produced 9 nonmanifold edges. The wedge therefore broadens the sharp-feature evidence while independently repeating the zero-set/connectivity split seen on the box.

## Thin, noisy, quantized, and open data

The deterministic thin-plate screen varied exact thickness while preserving a 40 × 30 mm footprint.

| Thickness | Grid / voxel | DCSD RMS | components | interpretation |
|---:|---:|---:|---:|---|
| 10 mm | 32³ / 1.84 mm | 0.01 mm | 1 | well resolved |
| 3 mm | 32³ / 1.79 mm | 0.02 mm | 1 | useful geometry |
| 1 mm | 32³ / 1.78 mm | 0.21 mm | 1 | below one voxel; thickness biased |
| 0.5 mm | 32³ / 1.78 mm | 0.11 mm | 1 | connected but nonmanifold |
| 0.25 mm | 32³ / 1.78 mm | 0.07 mm | 41 | topology lost |
| 0.25 mm | 64³ / 0.89 mm | 0.06 mm | 1 | connected, still thickness-biased |

Low pointwise distance does not prove wall preservation. For example, the 0.25 mm plate reconstructed with a 1.04 mm total z-span at 32³ and 0.80 mm at 64³. A useful wall needs multiple voxels across its thickness and an independent topology/thickness admission check.

On the sharp box, direct SDF noise of 0.05 voxel gave DCSD 0.0861 mm RMS versus MC 0.1990 mm; 0.20 voxel gave 0.3839 versus 0.4547 mm. DCSD remained closer but gained 4 and 24 nonmanifold edges. Float16 round-trip SDF values yielded 0.0312 mm RMS versus MC's 0.1700 mm, again with 6 nonmanifold edges. Robust zero-set placement does not cure connectivity.

For an intentional finite open rectangle, `phi=z` reconstructed an open grid-clipped plane with 192 boundary edges. The intended 40 × 30 mm footprint became 48.56 × 35.53 mm under DCSD because the scalar field contains no finite boundary. Signed volumetric DCSD is not an open-surface reconstruction method.

## PAT comparison and complementarity

The inputs differ: PAT receives an oriented point cloud and answers a reusable pointwise field; DCSD receives a dense sampled volumetric SDF and emits one explicit mesh. The numbers are therefore architectural evidence, not a controlled head-to-head accuracy leaderboard.

| Regime | PAT X0 | DCSD X0 | Interpretation |
|---|---|---|---|
| Sphere | 0.0051 mm RMS, field queries ~180k/s | 0.0024 mm mesh RMS, 79 s reconstruction | both excellent; PAT supplies reusable queries, DCSD supplies an offline mesh |
| Torus | 0.0196 mm RMS, 0.04% sign error | 64³ screen near 0.00 mm RMS | no evidence that DCSD damages smooth curvature, but comparison inputs differ |
| Sharp box | 0.5736 mm RMS, 5.04% sign error; edge/corner rounding | 0.0201 mm RMS at 32³; 0.0216 mm corner RMS | DCSD clearly owns the sharper candidate |
| 0.5 mm plate | 0.3129 mm RMS, 16.44% sign error | 0.1161 mm RMS but 162 nonmanifold edges | neither is occupancy/manufacturing authority |
| Bent sheet | 1.6104 mm RMS, 13.37% sign error | 0.9018 mm RMS but 318 nonmanifold edges | DCSD is geometrically closer; neither output is admissible |
| Phone chassis | 0.4115 mm PAT field RMS | 1.08 mm DCSD screening mesh RMS | PAT is preferable for the current smooth scan-query role |
| Open | PAT sign failed (32.4%) | finite boundary is not encoded by volumetric sign | neither qualified |

The simple claim “PAT wins smooth, DCSD wins sharp” is too coarse. DCSD is excellent on the exact sphere too, while PAT remains vastly more useful for pointwise query throughput after point-cloud preprocessing. Their strongest complement is representation and workflow, not merely curvature class.

There is **maybe** enough evidence for `SDF-HYBRID-X0`, after one prerequisite: run both candidates from a common sampled source and reject nonmanifold/thin/open regions explicitly. A future `ReconstructionCandidate` Judgment Engine could consider PAT, DCSD, and a plane/MC baseline, with admissibility first (closedness, manifoldness, topology, bounded error) and utility second (smooth residual, sharp-feature location, normal consistency, triangle quality). X0 does not implement that selection or splice meshes.

## Authority, port, and next decision

Conceptual placement:

```text
SampledField + provenance
  -> external DCSD reconstruction
  -> ApproximateExplicitMesh
  -> validation / region recognition / fitting
  -> explicit admission step
  -> canonical geometry only if independently reconstructed and admitted
```

The result carries source fixture/hash, grid frame and physical spacing, padding, normalization scale, upstream revision and binary hash, options, reconstruction hash, and validation metrics. It does not carry or recover analytic support ownership, topology intent, holes/chamfers/fillets as features, AIR, Firmament history, manufacturing semantics, or BRep authority. SDF-to-mesh is decompilation: many explicit surfaces can fit one sampled field.

**Port decision: wrap first / not yet.** The C++ implementation is slow, entangled with Polyscope, and produces invalid connectivity in precisely the thin and mixed-CAD cases Aetheris cares about. A .NET port would freeze unclear behavior before it earns an admissible contract. The existing script is the correct X0 boundary.

**Narrow next experiment:** `DCSD-CONNECTIVITY-X1`. Determine whether the nonmanifold incidences are an upstream connectivity defect, a quad-to-triangle interpretation issue, or an unavoidable sign-grid ambiguity; add a hard approximate-mesh admission gate. Only if the sharp witness remains strong and the mesh becomes valid should `SDF-HYBRID-X0` compare PAT/DCSD residuals region by region from identical evidence.

## Reproduce

```powershell
pwsh -File tools/DCSDDogfoodX0/run.ps1 -Phase all
```

The command clones and pins upstream plus recursive submodules under `artifacts/local/dcsd-x0`, applies the documented Windows compatibility patch, validates the revision, creates the Python environment, builds the native binding, exports exact Aetheris fixtures (including the authored SheetMetal `ProfileDelta` part), runs the author-data demo, generates normalized exact/qualified SDF grids, reconstructs MC/DCSD meshes, executes the thin/noise/open/determinism tests, and writes CSV/JSON/plots/renders under ignored local artifacts. A complete run is CPU-heavy because the 64³ sharp box alone takes about ten minutes with published iterations.

For a cheap independent re-analysis of already generated sharp-box outputs:

```powershell
pwsh -File tools/DCSDDogfoodX0/run.ps1 -Phase analyze
```

A fresh agent also reran the real 32³ published-profile adapter. It reproduced every geometric value above with byte-identical faces and `0.0 mm` maximum vertex delta; runtime varied from 96.3 s to 119.2 s. Its independent 64³ analysis confirmed the mixed edge result: DCSD improved edge p95 but had worse edge RMS and an 11.37 mm false-feature maximum. A second fresh agent added the exact authored wedge fixture without touching upstream and independently reproduced the better zero set plus nonmanifold-connectivity split. A third fresh reviewer rejected universal smooth/sharp authority and recommended connectivity/admissibility work before any hybrid implementation.

Tracked evidence: `docs/release/DCSD-DOGFOOD-X0.evidence.json`.

To export and run only the bounded wedge screen (without the full benchmark):

```powershell
pwsh -File tools/DCSDDogfoodX0/run.ps1 -Phase wedge
```
