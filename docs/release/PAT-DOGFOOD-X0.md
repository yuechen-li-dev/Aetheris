# PAT-DOGFOOD-X0 — Points-as-Tori reproduction and Aetheris fit test

Date: 2026-09-17  
Verdict: **Useful but bounded**  
Reimplementation decision: **Not yet**

## Executive verdict

Points-as-Tori (PAT) earns a place as an experimental, source-derived point-cloud field for smooth, closed, consistently oriented data. It does not earn authority for exact CAD distance, topology, manufacturing intent, thin-sheet occupancy, or open-surface sign.

The published pretrained model generalized extremely well to exact Aetheris spheres and tori. At 2,048 samples, the sphere had 0.0051 mm RMS distance error and no sign errors; the torus had 0.0196 mm RMS and a 0.04% sign-error rate. The analytic PAT query path sustained about 180k queries/s for 1M CPU queries after preprocessing in the final uncontended run (180k–213k/s across repeated runs).

The limits are equally clear. A sharp box had 0.574 mm RMS error and 5.0% sign errors overall; its edge-near and corner-near RMS errors were 0.95 mm and 1.34 mm, respectively. The 0.5 mm plate had 16.4% sign errors. An actual Aetheris Sheet Metal `ProfileDelta` part had 1.61 mm RMS error, 13.4% sign errors, and only 4 of 128 surface-normal probes bracketed a usable zero crossing. An open slab had 32.4% sign errors. These are disqualifying for exact occupancy, cut-cell material authority, and manufacturing geometry.

Recommended use:

- reconstruction guidance and scan-to-CAD residuals for smooth closed data;
- approximate visualization, surface extraction, and proximity hints with an explicit validity band;
- an experimental Imported Geometry / Reconstruction lane, never canonical BRep or Firmament authority.

Do not port the full stack into .NET yet. Keep the pinned upstream implementation external for one narrow follow-up that validates a real scan-to-CAD fitting residual and explores portable inference. If that succeeds, wrap pretrained inference first and consider porting only the analytic query core.

## Reproduction basis and license audit

| Item | Recorded value |
|---|---|
| Project | [Points as Tori project page](https://nzfeng.github.io/research/PointsAsTori/index.html) |
| Repository | [nzfeng/points-as-tori](https://github.com/nzfeng/points-as-tori) |
| Revision | `253bf9fc5e79831a03f0486e0c42feeb67c1c546` |
| Paper | [ACM DOI 10.1145/3811385](https://doi.org/10.1145/3811385) |
| Code license | Root repository license is MIT |
| Model | `src/pointsastori/models/FundamentalFormPredictor.pkl`, 6,366,869 bytes |
| Model SHA-256 | `AAA2F3882C36AC32D57361377A73AC830C47D03D6167D51249127A6C40EF7FF9` |
| Weight license | No separate explicit license found; do not redistribute without clarification |
| Sample/data license | No separate explicit license found; keep fetched samples local |
| Native dependencies | C++17, nanobind, OpenMP, nanoflann; upstream also vendors/uses libigl and CGAL paths |
| Python dependencies observed | Python 3.11, JAX 0.8.2, Flax 0.12.2, NumPy, trimesh and build dependencies |
| Redistribution constraint | Treat weights and bundled samples as fetch-only, hash-pinned local artifacts. Review the selected libigl/CGAL build path before distributing a binary, because CGAL licensing depends on the packages used and may require GPL compliance or a commercial license. |
| Runtime constraint | Learned precompute requires JAX/Flax. The packaged query extension is native C++/OpenMP. This Windows run used JAX CPU despite an installed RTX 3070. |

The upstream repository, model, and samples are cloned under ignored `artifacts/local/pat-x0/upstream`; none are committed here.

## Published demo reproduction

The canonical `block.obj` demo was reproduced with the upstream sampler: 2,048 surface points, seed 42, center-and-scale normalization, and upstream mesh normals. The published interactive GUI evaluates all source points in a shader; the headless reproduction used the public Python `Shape3D.signed_distance` path with its default 32 nearest local tori.

| Measurement | Result |
|---|---:|
| Sampling | 0.0044 s |
| Model/precompute | 3.43 s |
| Queries | 48,400 |
| Query time | 0.289 s |
| RMS against packaged mesh | 0.0177 normalized units |
| p95 absolute error | 0.0310 normalized units |
| Maximum absolute error | 0.0439 normalized units |
| Sign-error rate | 0.44% |

Visual proof is written to `artifacts/local/pat-x0/demo/author-demo.png`; machine-readable values are in `artifacts/local/pat-x0/demo/demo.json`.

Two minimal Windows compatibility changes were required in the ignored upstream clone: add `<numeric>` in `shape_2d.cpp`, and use a signed OpenMP loop index in `shape_3d.cpp`. No algorithm, weights, network, or evaluation equation was changed.

## Implementation observed

PAT does not learn a global implicit field. For every source sample, the implementation builds a local 64-point neighborhood. It normalizes supplied normals, translates the center point to the origin, constructs a deterministic local orthonormal frame from the center normal, and divides coordinates by the median neighbor distance from the center. There is no epsilon guard on that median scale.

The Flax predictor receives positions and normals as two `[batch, 64, 3]` tensors. Its transformer has a 128-wide embedding, eight attention heads, eight layers, a 512-wide MLP, and no dropout. It has 1,588,358 float32 parameters and emits `[batch, 6]`: six local quadratic/fundamental-form coefficients `(a00, a01, a10, a11, a02, a20)`. Those coefficients are converted to local curvature/principal-frame information and then to one analytic torus per source point. The network neither stores nor answers the global field.

For a torus with center `c`, axis `u`, major radius `R`, and signed minor radius `r`, the local evaluator is:

```text
sign(r) * (sqrt((|cross(q - c, u)| - R)^2 + dot(q - c, u)^2) - |r|)
```

Query evaluation is fully analytic after preprocessing, plus nearest-neighbor lookup and blending; it does not invoke the network per query. The public 3D API uses a nanoflann index and 32 nearest source tori. For neighbor distances `d`, it uses an exponential kernel equivalent to `exp(-lambda * (d - shift))`, with `shift = 0.5 * max(d)` and `lambda = 64 / shift`, then normalizes the weights. The sign convention is negative inside for consistently outward-oriented input normals.

This differs in detail from the paper's fixed-radius discussion and from the demo GUI's all-point shader path. Results in this report identify the actual public API path being measured.

## Aetheris fixture and truth pipeline

`Aetheris.CLI mesh` materializes exact Firmament/BRep fixtures through the production compiler and exports oriented `SurfaceMeshIR` OBJ meshes. Analytic truth is used for the sphere, cylinder, torus, slab/box, sharp box, and thin plate. Other BRep fixtures use a qualified triangle closest-point distance plus watertight inside/outside classification; their truth therefore inherits tessellation error and is labeled `qualified-mesh`, not exact analytic truth.

The corpus includes sphere, cylinder, torus, capped frustum, slab, all-edge filleted box, rounded-rectangle prism, sharp box, chamfered box, through hole, counterbore, 0.5 mm thin plate, phone-like smooth chassis, and a real formed Sheet Metal part. The frustum is the existing canonical `fixtures/Canonical/Primitives/frustum.firmament`; its 20 mm bottom radius, 10 mm top radius, and 30 mm height drive a closed-form capped-frustum distance authority, and sampled points are lifted from tessellation onto the exact caps and conical side. The last fixture builds `fixtures/Canonical/SheetMetal/profile-delta-tab-family.firmament`, including its `Table`, immutable `with` specialization, typed `ProfileDelta`, exact blank, and 90-degree flange. The resulting formed STEP has 18 faces and tessellates to 860 triangles. This confirms the programmable profile path while exercising PAT on actual bent thin geometry.

Sampling is deterministic with seed `20260917`. The harness supports area-weighted and uniform-triangle selection, position noise in millimeters, angular normal noise, local normal inversions, random removal, missing caps, nonuniform clustering, and deliberately open surfaces. No retraining or fine-tuning occurs.

Queries cover on-surface, surface-near, inside, outside, far field, sharp edges, corners, hole/counterbore transitions, and thin-wall lines. Metrics use millimeters and exclude exact boundary points from sign classification.

## Accuracy at 2,048 clean oriented samples

| Fixture | Truth | RMS | p95 | Max | Sign error | Zero-set RMS | Plane baseline RMS |
|---|---|---:|---:|---:|---:|---:|---:|
| Sphere | analytic | 0.0051 | 0.0090 | 0.0172 | 0.00% | 0.0057 | 0.0103 |
| Cylinder | analytic | 0.1996 | 0.5681 | 0.9476 | 1.50% | 0.1600 | 1.0845 |
| Torus | analytic | 0.0196 | 0.0372 | 0.1080 | 0.04% | 0.0150 | 0.0517 |
| Frustum | analytic | 0.2283 | 0.6459 | 1.2756 | 1.35% | 0.0546* | 1.7830 |
| Slab | analytic | 0.1381 | 0.3258 | 0.8764 | 3.06% | 0.3066 | 1.5518 |
| Filleted box | qualified mesh | 0.7185 | 1.1183 | 8.2177 | 5.77% | 0.4915 | 2.4165 |
| Rounded rectangle prism | qualified mesh | 0.1528 | 0.4099 | 0.8805 | 2.63% | 0.2805 | 0.7547 |
| Sharp box | analytic | 0.5736 | 1.3367 | 2.1009 | 5.04% | 0.4446 | 2.1047 |
| Chamfered box | qualified mesh | 0.0639 | 0.1597 | 0.3637 | 3.01% | 0.0568 | 0.4496 |
| Through hole | qualified mesh | 0.9530 | 1.2365 | 19.2282 | 4.99% | 0.4284 | 2.2173 |
| Counterbore | qualified mesh | 0.6337 | 1.0304 | 13.0835 | 4.96% | 0.4388 | 1.7753 |
| Thin plate | analytic | 0.3129 | 0.5693 | 1.1880 | 16.44% | 0.4090* | 2.6613 |
| Bent Sheet Metal `ProfileDelta` | qualified mesh | 1.6104 | 2.7820 | 17.8555 | 13.37% | 4.2494* | 12.6771 |
| Phone-like chassis | qualified mesh | 0.4115 | 0.9548 | 2.3914 | 3.00% | 0.6991* | 2.3416 |

`*` The zero-set statistic is conditional on a bracketed crossing. The frustum bracketed 115/128 probes; only 1/128 plate probes, 4/128 bent-sheet probes, and 23/128 chassis probes bracketed a root, so the latter three values must not be read as broad reconstruction success.

PAT materially beats the nearest oriented tangent-plane baseline over most complete fixtures. It is not uniformly better where CAD intent is least smooth: close to sharp-box edges the PAT RMS was 0.95 mm versus 0.39 mm for the plane baseline, and at corners it was 1.34 mm versus 0.66 mm. PAT rounds and biases corners rather than preserving their exact intersection semantics. Hole and counterbore transitions show the same local smoothing plus large far/inside outliers.

The torus-on-torus test is a strong sanity check: 0.0196 mm RMS, 0.0150 mm zero-set RMS, and 0.04% sign errors with an exact torus of major radius 12 mm and minor radius 3 mm. The sphere is better still. PAT is genuinely effective in its intended smooth-curvature regime.

## Sampling, noise, missing data, and orientation

Sphere precompute scaled approximately linearly: 1k, 2k, 5k, and 20k points took 1.65 s, 3.29 s, 5.99 s, and 21.6 s. Sphere error was already near its floor at 1k–2k and improved only modestly with more points. The 100k density requested in the broad matrix was not run: CPU/JAX precompute extrapolates beyond the useful X0 budget and does not change the observed authority boundary.

| Sphere perturbation | RMS | p95 | Sign error |
|---|---:|---:|---:|
| 0.01 mm position noise | 0.0058 | 0.0104 | 0.00% |
| 0.05 mm position noise | 0.0205 | 0.0390 | 0.00% |
| 0.10 mm position noise | 0.0390 | 0.0778 | 2.72% |
| 0.50 mm position noise | 0.5859 | 0.7111 | 15.02% |
| 25-degree normal noise | 0.0803 | 0.1469 | 1.10% |
| 5% locally flipped normals | 0.1684 | 0.1535 | 1.43% |
| 25% missing cap | 0.3544 | 0.8342 | 5.21% |
| 25% random removal | 0.0055 | 0.0104 | 0.00% |
| Clustered/nonuniform density | 0.0100 | 0.0170 | 0.00% |

Random thinning is benign when coverage remains global. A coherent missing region is not. Consistent outward normals are a hard preprocessing requirement: angular degradation is gradual, while inversions inject the wrong local sign and outliers. Curvature approaching sample spacing likewise degrades once a 64-point neighborhood spans materially different sheets or feature branches.

The intentionally open slab produced 4.19 mm RMS, 9.70 mm p95, and 32.4% sign errors, with 1,037 false-material classifications versus 12 false-void classifications. Signed distance is not meaningful for this case. A local unsigned residual may still be useful if the caller explicitly opts out of occupancy semantics.

## Performance and memory

Environment: Windows 10 build 26200, AMD Family 25 Model 97, 16 logical CPUs, Python 3.11.2, JAX `TFRT_CPU_0`; NVIDIA RTX 3070 8 GB present but not used by this JAX installation.

| Queries | Time | Throughput |
|---:|---:|---:|
| 1 | 78.1 us | 12.8k/s |
| 1,000 | 7.90 ms | 127k/s |
| 100,000 | 0.536 s | 186k/s |
| 1,000,000 | 5.544 s | 180k/s |

The query stage is CPU/OpenMP and analytic. The dominant interactive cost is per-cloud preprocessing: local neighborhood construction, JAX model inference, torus conversion, and index construction. The harness records their aggregate because the public implementation does not expose stable separate timers for all four stages.

Representation storage is approximately 48 bytes/source point for float64 point+normal data, 56 bytes/source point for torus parameters, and 256 bytes/source point for 64 precompute neighbor indices. A 32-neighbor query batch also needs about 128 bytes/query for indices before temporary arrays. The model file is 6.37 MB and its parameters occupy about 6.35 MB as float32. The first measured process gained roughly 720 MB RSS because loading JAX/XLA and allocator state dominates compact model/field storage; subsequent per-case readings are allocator-cached and are not additive object sizes.

No supported upstream GPU query path was found. GPU precompute is possible where JAX has a matching accelerator build, but was not available in this Windows environment. ONNX export is not direct: the model is Flax/JAX with custom preprocessing and the native analytic evaluator. TorchScript does not apply. A portable inference proof would need `jax2tf`/SavedModel or a separately validated translation and exact coefficient-parity tests; that is deliberately deferred.

## Field operations and query relevance

The harness solves `phi(x) = 0` and `phi(x) - 1 mm = 0` along known normals. On the sphere, zero-set RMS was 0.0057 mm and the +1 mm offset RMS was 0.0057 mm, showing that analytic offset/morphology is useful on favorable smooth closed data. Extracted surfaces remain derived approximations, not CAD offsets.

Sphere tracing used 961 rays. Of 489 expected hits, PAT found 481, missed 8, produced no false hits, averaged 13.7 steps for hits, and reached a maximum of 73. That is adequate for experimental visualization but not a proof that the blended field is a globally conservative distance bound.

The occupancy experiment uses `phi(x) < 0`. It is excellent for a clean sphere and unacceptable for a thin plate, bent sheet, sharp/open geometry, or incomplete scans. PAT therefore should not currently feed authoritative cut-cell FEA material classification. It may be tested later as one experimental classifier with boundary uncertainty and an independent admission check.

For clearance and fitting, PAT can supply a smooth residual from a well-oriented scan to an exact CAD query set. It must not supply authoritative minimum clearance: coherent holes, thin walls, and far-field outliers can change both magnitude and sign. The practical near-term use is a robustly truncated residual for reconstruction guidance or ICP-like fitting, cross-checked against exact Aetheris geometry.

## Failure modes

- Sharp edges and corners are rounded and biased; the local plane baseline is better immediately around the sharp-box features.
- Close parallel sheets contaminate each other's 64-point neighborhoods. Thin-wall sign is unreliable even when global orientation is correct.
- Coherent missing patches create false continuation and sign failures; random thinning is much less harmful.
- Open surfaces do not define a stable inside/outside relation for this implementation.
- Normal noise degrades curvature prediction; local flips are especially hazardous.
- Nonuniform density was tolerated on the tested sphere but changes median normalization and neighbor support, so it is not an invariant guarantee.
- Far/inside queries on holed BReps produced rare but very large outliers, up to 19.2 mm in this corpus.
- The learned predictor was trained around clean 2,048-point shapes. Dense, noisy, incomplete, thin CAD-like inputs are out of distribution and must be admitted empirically, not assumed.

## Authority boundary and future domain shape

The correct relationship is:

```text
ImportedPointCloud
    -> PointTorusField (source-derived, approximate, provenance-bearing)
    -> Reconstruction / Analysis / Experimental field queries
    -> explicit reconstruction and admission step, if a mesh or BRep is desired
```

It is not:

```text
Firmament solid -> PAT -> replacement topology or manufacturing authority
```

PAT should live alongside imported/reconstructed geometry as a query/evaluation authority with declared source, scale, valid distance band, sign capability, and confidence/qualification metadata. It should not implement Aetheris' exact distance interface and should not become a CIR topology source. A future, non-frozen sketch is:

```text
PointTorusField
  SourcePointCloud
  NormalField
  LocalTorusParameters
  KernelSettings
  Acceleration
  ValidityBand
  SignCapability
  Provenance
```

`SignCapability` must be an admitted state, not a Boolean assumption: for example, unavailable for open/unoriented input, locally qualified for incomplete data, or closed-oriented-qualified after validation. Queries outside `ValidityBand` should reject or explicitly return an unqualified result. The approximate field and any derived mesh must be hard-type-separated from exact distance, occupancy, and canonical geometry interfaces so ordinary assignment cannot promote them accidentally.

Any `phi=0` mesh must be marked derived/approximate. Promotion to canonical BRep requires an explicit reconstruction/admission workflow with its own tolerances and validation. Firmament solids must continue through existing BRep authority.

## Determinism

Repeating precompute and query with the same cloud, normals, weights, and settings on the measured CPU path produced a maximum difference of 0.0 mm. The model has dropout disabled. Accelerator kernels were not exercised; a future GPU qualification must record device/version and numeric tolerance rather than assuming bitwise determinism.

## Reproduction

From the repository root in PowerShell:

```powershell
pwsh -File tools/PATDogfoodX0/run.ps1 -Phase all
```

The command chain hash-pins and fetches upstream, applies the two documented compiler-only compatibility edits in the ignored clone, validates the model hash, builds the native/Python package, exports Aetheris fixtures, builds and inspects the programmable Sheet Metal profile, runs the author demo, executes the corpus, writes raw CSV/JSON, and generates plots/renders.

Useful bounded phases are:

```powershell
pwsh -File tools/PATDogfoodX0/run.ps1 -Phase prepare
pwsh -File tools/PATDogfoodX0/run.ps1 -Phase demo
pwsh -File tools/PATDogfoodX0/run.ps1 -Phase benchmark
```

Tracked compact evidence is in [PAT-DOGFOOD-X0.evidence.json](PAT-DOGFOOD-X0.evidence.json). Raw evidence is ignored under:

```text
artifacts/local/pat-x0/demo/demo.json
artifacts/local/pat-x0/benchmark/query-evidence.csv
artifacts/local/pat-x0/benchmark/summary.json
artifacts/local/pat-x0/benchmark/benchmark-plots.png
artifacts/local/pat-x0/benchmark/*-slice.png
```

Each raw query row records fixture, point count, perturbation scenario/level, category, PAT value, truth value, absolute and signed error, sign correctness, baseline value, and timing context.

## Fresh-agent qualification

Three agents were given only the tracked guide, scripts, and evidence—no hidden run instructions.

1. The reproduction agent ran `prepare` and `benchmark`, validated the pinned revision/model, produced 99,870 query rows, and independently recovered the sphere and sharp-box values in this report. It also recovered the sharp edge/corner reversal versus the tangent-plane baseline and 0.0 mm deterministic repeat deltas. Its only workflow friction was that the runner has no per-fixture filter, so the bounded reproduction executes the full corpus.
2. The authority agent placed `PointTorusField` in Imported Geometry / Reconstruction, required provenance, a validity band, admitted sign capability, and hard type separation, and independently rejected exact-distance, BRep, Firmament, CIR-topology, manufacturing, cut-cell occupancy, and exact-clearance authority. It identified the risk that a generic “SDF-like” interface could accidentally erase these qualifications; the boundary above now states rejection/unqualified behavior explicitly.
3. The extensibility agent added the existing canonical capped frustum without changing PAT. It introduced a closed-form exact SDF and exact support lifting, proved mesh vertices within `9.99e-13` mm and sampled points within `7.11e-15` mm of that authority, and ran the full case. PAT measured 0.2283 mm RMS, 0.6459 mm p95, 1.35% sign errors, and 115/128 bracketed zero probes. Focused CLI tests passed 443/443.

Repository validation compiled the full `Aetheris.slnx`. The parallel Debug test invocation reported two unrelated tessellation-sensitive failures: one deterministic-scaffold assertion and one five-second spring display-tessellation timeout. Each exact failed test passed immediately when rerun alone with `-m:1` (1/1 and 1/1). PAT's Python compilation, PowerShell parse, compact/raw JSON checks, upstream/model hash checks, fixture exports, benchmark, and `git diff --check` all passed.

## Decision and next experiment

**Does PAT earn a place?** Yes, as a useful but bounded point-cloud SDF-like reconstruction/query field for smooth, closed, consistently oriented data. No, as a general signed-distance or occupancy authority.

**Should Aetheris port it?** Not yet. The evidence justifies preserving the benchmark and evaluating a pinned external wrapper. It does not justify owning a Flax-to-.NET neural implementation, accepting unresolved model redistribution, or baking smooth-field assumptions into CIR. If a production use case survives one more test, the smallest plausible integration is pretrained inference behind a process boundary plus a separately portable analytic/query core—not a full rewrite.

**Narrow follow-up:** PAT-X1 should perform one real scan-vs-CAD fitting experiment on a smooth chassis, using a truncated residual and held-out exact Aetheris queries, while proving a portable model-export path with coefficient parity. Stop if the residual does not improve convergence or if portable inference cannot preserve the six predicted coefficients within an agreed tolerance.
