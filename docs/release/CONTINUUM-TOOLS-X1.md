# CONTINUUM-TOOLS-X1 — bounded implicit reconstruction utilities

Date: 2026-09-17  
Verdict: **Accepted**

## Executive result

Aetheris now has two small, real, agent-usable decompilation utilities in `Aetheris.Continuum.Reconstruction`:

1. `PointTorusField`: a deterministic KD-tree-backed consumer of externally precomputed PAT local torus parameters, using the observed upstream analytic formula and exponential neighbor blend. Every query reports its qualification, signed capability, neighbor count, nearest-source distance, and reason.
2. `SharpDualContouring`: a clean-room regular-grid SDF to approximate explicit mesh path using sign-changing primal edges, finite-difference Hermite normals, constrained per-cell QEF placement, deterministic connectivity, OBJ export, provenance, and unconditional topology diagnostics.

This is not a general reconstruction framework, plugin model, strategy registry, hybrid PAT/DCSD system, or production BRep path. Learned PAT inference remains external. The contourer is the useful sharp QEF/dual-connectivity core, not a claim to reproduce the paper's slow iterative sphere optimizer.

## Evidence

The actual CLI workflow generated and consumed its own versioned JSON under `artifacts/local/continuum-tools-x1`.

| Case | Result |
|---|---:|
| PAT exact 10 mm sphere | exact values at all four probes (`-10`, `0`, `2`, `-1` mm) |
| PAT near-source probes | `Qualified`, signed |
| PAT center probe | correct value but `OutsideValidityBand` rather than silently authoritative |
| Sharp box grid | 25 x 25 x 25 nodes, offset from the zero set |
| Sharp-box output | 946 vertices, 1,888 triangles |
| Sharp-box forward vertex RMS / max | 0.040 / 0.190 mm |
| Sharp-box topology | 0 boundary, 0 nonmanifold, 1 component, 0 duplicate faces/vertices |
| Contour kernel time | 25.0 ms on this run |

The earlier exact-box dogfood measured Marching Cubes at 0.1700 mm RMS on a 32³ grid and the published DCSD optimizer at 0.0201 mm in 96.3 s, with 6 nonmanifold edges. X1 is not an apples-to-apples resolution comparison, but it establishes the intended trade: materially sharper placement than the measured MC reference, about four orders of magnitude less contour time than the upstream optimizer, and clean topology on the bounded box. The focused test also requires a recovered positive corner within 0.25 mm per coordinate and vertex RMS below 0.1 mm.

## PAT boundary

The imported representation is exactly the post-inference analytic boundary needed by Aetheris: source point and normal, torus center and unit axis, nonnegative major radius, and signed nonzero minor radius. `PointTorusProvenance` records source, parameter producer, model revision/hash, and arbitrary properties. The model still predicts six local fundamental-form coefficients externally; this library neither embeds nor replaces it.

The field has no naked `double Evaluate(...)`. Results distinguish `Qualified`, `UnsignedOnly`, `OutsideValidityBand`, and `InsufficientNeighborhood`. Sign is available only when metadata declares closed, globally oriented, single-sheet input. Open, unoriented, and thin/multi-sheet inputs evaluate an unsigned residual and explain why. The validity band is explicit and based on nearest source-sample distance.

This is an admission contract, not automatic geometric proof. A producer that falsely labels a bad cloud closed/oriented can still produce wrong answers. X0's measured failures remain governing evidence: sharp box 5.04% sign error, 0.5 mm plate 16.44%, bent `ProfileDelta` sheet 13.37%, and open slab 32.4%.

## DCSD-derived boundary

The implemented core accepts only a finite regular scalar grid with negative-inside convention. It builds one vertex per active dual cell and faces around sign-changing primal edges. It always emits:

- boundary-edge count;
- nonmanifold-edge count;
- connected-component count;
- duplicate-face and duplicate-vertex counts;
- active-cell, Hermite-constraint, and elapsed-time statistics;
- approximate provenance and an authority warning.

There is no automatic repair, adaptive octree, feature semantics, or direct BRep conversion. Boundary-touching surfaces may be open because the input must pad a closed zero set. Thin features below grid support can disappear or join. Finite-difference normals limit corner accuracy; this bounded box reached 0.040 mm RMS but a 0.190 mm maximum at corner-influenced cells.

## CLI and agent workflow

```powershell
dotnet run --project Aetheris.CLI -- continuum make-fixtures --out-dir artifacts/local/continuum-tools-x1
dotnet run --project Aetheris.CLI -- continuum pat-query artifacts/local/continuum-tools-x1/sphere-point-tori.json artifacts/local/continuum-tools-x1/sphere-queries.json --out artifacts/local/continuum-tools-x1/sphere-results.json
dotnet run --project Aetheris.CLI -- continuum contour artifacts/local/continuum-tools-x1/sharp-box-grid.json --out artifacts/local/continuum-tools-x1/sharp-box.obj --report artifacts/local/continuum-tools-x1/sharp-box-report.json
```

Inputs/outputs use versioned JSON and OBJ. Generated bulk artifacts remain ignored. The fixture recipe is under `fixtures/Experiments/ContinuumToolsX1/`.

## Size and complexity

The implementation is intentionally compact:

| Unit | Lines |
|---|---:|
| PAT field, qualification, deterministic KD tree | 210 |
| Sampled grid, QEF contouring, diagnostics, OBJ | 210 |
| JSON interchange | 54 |
| CLI commands and deterministic fixtures | 90 |
| Total production code | 564 |

There is no registry, provider abstraction, generic optimizer, hidden repair pass, or second geometry authority.

## Credit and license posture

PAT is credited to Feng et al., *Points as Tori: Fast Pointwise Signed Distance for Point Clouds* (SIGGRAPH 2026), with project and repository links in `Aetheris.Continuum/README.md`. PAT code is MIT; weights remain external because no separate weight license was found.

The contouring work credits Carrera et al., *Dual Contouring of Signed Distance Data*. Because upstream code provenance is ambiguous (MIT text naming Nick Sharp; paper PDF CC BY-NC-ND), X1 is clean-room and copies no upstream code or data.

## Authority and next boundary

Correct placement:

```text
ImportedPointCloud + external fitted parameters -> PointTorusField -> qualified residual/query evidence
SampledSdfGrid -> SharpDualContouring -> ApproximateSurfaceMesh + diagnostics
either result -> explicit decompilation/reconstruction admission -> stronger geometry, if separately justified
```

Neither path writes CIR topology or creates canonical BRep/Firmament/manufacturing intent. The smallest later experiment would use these tools in one explicit scan-to-CAD decompilation witness and require topology/qualification admission before downstream use; no hybrid field is justified here.

## Verification

- `dotnet build Aetheris.slnx`: succeeded, 0 errors; seven pre-existing WebAssembly/trimming/platform warnings
- `Aetheris.Continuum.Tests`: 153/153 passed
- focused `Aetheris.Continuum.Tests.Reconstruction`: 5/5 passed
- focused `ContinuumToolsCliTests`: 2/2 passed
- real CLI fixture generation, PAT query, contour, JSON report, and OBJ output
- fresh-agent PAT, DCSD, and decompilation workflow checks

## Fresh-agent results

Three agents started from the READMEs rather than implementation knowledge. All reproduced the workflow without edits. PAT returned the four exact values and the expected three qualified/one outside-band classifications. The contour agent independently parsed 946 OBJ vertices and 1,888 valid-index triangles, reproduced the five topology counters, and measured bounds at the intended box extents. The workflow agent passed the five focused tests and correctly kept model inference, grid construction, repair, semantic recovery, and stronger authority external.

Their friction findings were addressed in-place: every subcommand now has direct `--help`; PAT file-output mode prints a success summary; the README specifies millimetres, x-fastest flattening, query shape, configurable neighbor count, and independent qualification/sign checks. The report also states that the required topology counters are not self-intersection, orientation, degeneracy, or source-fit admission.
