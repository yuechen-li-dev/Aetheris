# AETHERIS-CONTINUUM-M2: exact BRep curved boundaries

## Outcome

M2 answers the milestone question **yes, with one performance qualification**. `Aetheris.Continuum` now carries a real exact Aetheris BRep sphere face through `BoundaryReference.ExactBrepFaceId`, uses CIR as the independent occupied-region authority, constructs deterministic arbitrary-oriented local offset graphs, selects rectangular `Nu x Nv` maps, and validates position and normal fidelity against the exact BRep support. It does this on a fixed 16 x 16 x 16 Cartesian lattice with no AMR, cell subdivision, octree, or solver work.

The curvature-aware fixed-lattice row has 0.002759% volume error and 0.021314% boundary-area error. The 32 x 32 x 32 brute-force reference has 0.005724% volume error. The fixed lattice therefore uses one eighth as many volume cells and wins on volume accuracy. The current deterministic projected-footprint CPU integrator is evidence-oriented and is slower than the fine-grid point sampler; M2 proves the geometry architecture and certification cost reduction, not an end-to-end runtime win.

## M1 cost audit

The M1 selective cylinder row made 45,120 raw requests. Of these, 32,192 (71.3475%) were independent validation requests. Its measured breakdown was 0.621 ms classification, 2.949 ms combined map construction/validation, 1.090 ms geometry sampling, 1.614 ms aggregation, and 6.274 ms total. It made 6,912 map-node requests, 6,016 geometry-sampling requests, 37,800 unique exact queries, and reused 7,320 requests (16.223%). The boundary-map cache hit rate was 96.644%; the nested geometry cache hit rate was 10.638%.

| operation | runtime | certification only | reusable/cacheable | cheaper replacement |
|---|---:|---:|---:|---|
| conservative cell classification | yes | no | lattice lifetime | CIR interval/bounds classification |
| exact map-node samples | yes | no | yes, by face/frame/sample key | none; these define the derived map |
| dense independent non-node validation | no | yes | generally no | analytic Hessian and normal bounds |
| nested occupancy/MSAA samples | policy-dependent | no | yes, nested hierarchy | conservative occupancy disagreement |
| cache lookup | yes | no | cache is the reuse mechanism | stable face/sample keys |
| volume/area aggregation | yes | no | derived results may be retained | deterministic local estimates |

M1's validation was valuable as an oracle but was not intrinsically a production runtime requirement. M2 keeps it and moves it out of map construction.

## Runtime and certification split

`RuntimeBoundaryMapBuild` samples map nodes and returns immediately with an `EngineeringBoundaryMapCertificate`. `CertifiedBoundaryMapValidation` independently samples half-stride non-node positions and compares the interpolated map against the exact support. Runtime code does not call the oracle unless an experiment, test, or debug caller explicitly requests it.

The sphere runtime certificate uses a conservative local Hessian estimate for `h(u,v) = R - sqrt(R^2 - u^2 - v^2)` and a second-order bound for linearly interpolated exact sphere normals. It reports `Acceptable`, `RefineMap`, or `Invalid`, plus position and normal bounds. It is explicitly an engineering certificate, not a formal general-surface proof, and performs zero exact queries. The primary adaptive run used 208,729 exact map-node queries versus 880,914 oracle queries. All 614 decisions agreed with the oracle: zero false accepts and zero false refines. The fixed 16 x 16 row had zero false accepts and eight conservative false-refine decisions.

## Exact boundary authority and query bridge

The primary fixture is the existing `BrepPrimitives.CreateSphere` body: one closed periodic exact `SphereSurface` face. No new convenience geometry was added. `BrepSphereContinuumRegion` owns a `CirTransformNode(CirSphereNode)` for classification and separately owns the BRep body/face reference. A deterministic five-direction consistency probe verifies that the CIR inside/outside result agrees with the BRep material-side normal; disagreement throws explicitly.

`ExactBrepBoundaryQuery` is the bounded M2 bridge. For an exact referenced sphere face it supports exact point and face-normal evaluation, nearest support projection, parameter recovery, transformed center/radius access, and exact-surface frame construction. Unsupported surface families fail explicitly. It does not inspect tessellation or `SurfaceMeshIR`, and it does not admit NURBS.

The local frame origin is the exact projection of the cell center. The material-side normal is the inward sphere normal. The tangent seed is selected deterministically from the exact sphere parameter axes by least alignment with the normal, projected and normalized; the second tangent is a cross product. This remains orthonormal, deterministic, independent of global XYZ, and stable at ordinary parameter seams and poles.

## Local maps, policy, and integration

Each Cut cell projects its eight corners into its own `(U,V)` frame and adds a 2% validation margin. This is a bounded cell-local support domain; there is no global face atlas and no triangle-derived window. The map stores exact offsets and exact material-side normals at rectangular nodes and uses bilinear offset/linear-normal interpolation.

Resolution candidates from 2 through 24 independently in U and V are evaluated with the engineering certificate. `JudgmentEngine` admits certified candidates and utility-scores fewer samples with a small spacing-isotropy tie break. This makes admissibility, scoring, rejection, and deterministic tie-breaking explicit. Sphere curvature is isotropic, but projected cell footprints are not, so the selected distribution includes many rectangular resolutions. Radius variants cause different selected distributions and query counts without fixture-ID special cases.

`BoundaryOffsetMap3DIntegrator` projects each Cartesian cell to the local tangent plane, constructs the exact convex footprint of its eight corners, and integrates material thickness along the local material-normal direction. Boundary area and first moment come from deterministic triangles over the bilinear map. These operations refine only local geometry evaluation; the owning lattice remains unchanged.

## Geometric refinement ladder

Percentages below are relative errors. Timings are Release-mode evidence from the checked-in run and include classification, map construction, the zero-query runtime certificate, sampling, and occupied-volume/boundary-area integration; oracle time is separate.

| strategy | map exact queries | oracle queries | volume | area | max position | max normal | runtime excl. oracle | oracle |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| MSAA 4 x 4 x 4 | 0 | 0 | 0.004474% | n/a | n/a | n/a | 4.671 ms | 0 |
| map 4 x 4 | 9,824 | 49,734 | 0.312678% | 0.116839% | 0.002176 | 0.016338 deg | 386.519 ms | 2.253 ms |
| map 8 x 8 | 39,296 | 177,446 | 0.050923% | 0.035984% | 0.000392 | 0.003529 deg | 405.591 ms | 8.135 ms |
| map 16 x 16 | 157,184 | 668,646 | 0.005801% | 0.011154% | 0.000087 | 0.000783 deg | 445.899 ms | 34.644 ms |
| map 24 x 24 | 353,664 | 1,474,214 | **0.001389%** | 0.016673% | 0.000037 | 0.000332 deg | 533.795 ms | 74.845 ms |
| curvature-aware anisotropic | 208,729 | 880,914 | **0.002759%** | 0.021314% | 0.000049 | 0.000429 deg | 459.124 ms | 43.566 ms |
| anisotropic + selective MSAA | 208,729 | 880,914 | 0.004742% | 0.021314% | 0.000049 | 0.000429 deg | 699.675 ms | 42.108 ms |
| 32 x 32 x 32 + MSAA 4 x 4 x 4 | n/a | n/a | 0.005724% | n/a | n/a | n/a | 22.500 ms | 0 |

The selective row refined 277 of 614 Cut cells. Its nested hierarchy made 22,640 raw occupancy requests, 20,424 unique queries, and reused 2,216 (9.788%). Arbitrary support-frame rotation did not change deterministic nesting. Selective MSAA improves the fixed-lattice result over the fine grid, but it did not improve on the non-selective adaptive row in this fixture; that is evidence against making it the sphere default.

The exact-support cache is scoped to Continuum and keyed by BRep face/frame/sample. Across different cell-local frames the primary run correctly records zero hits; rebuilding the same map produces full hits and is regression-tested. A global kernel cache was not introduced.

## Orientation matrix

The sphere's occupied set is rotationally symmetric, so these rotations stress the exact BRep parameter frame and local map orientation while leaving the CIR body and translated center fixed. This isolates orientation dependence in the boundary-map machinery.

| orientation | Cut cells | volume | area | max / RMS position | max normal | runtime excl. oracle |
|---|---:|---:|---:|---:|---:|---:|
| baseline | 614 | 0.002759% | 0.021314% | 4.876e-5 / 3.334e-5 | 4.292e-4 deg | 480.731 ms |
| X 31 deg | 614 | 0.002738% | 0.016149% | 4.871e-5 / 3.297e-5 | 4.740e-4 deg | 478.456 ms |
| X 23, Y 37, Z 11 deg | 614 | 0.002338% | 0.019720% | 4.874e-5 / 3.284e-5 | 4.736e-4 deg | 488.841 ms |

No orientation had a false accept. Face assignment, local frames, domains, resolutions, sampling, cache behavior, metrics, and artifacts are deterministic.

## Curvature matrix

| radius | Cut cells | map-node queries | volume | area | runtime excl. oracle |
|---:|---:|---:|---:|---:|---:|
| 0.8 | 398 | 213,848 | 0.004062% | 0.003967% | 331.670 ms |
| 1.0 | 614 | 277,878 | 0.002338% | 0.019720% | 488.807 ms |
| 1.2 | 877 | 327,195 | 0.001730% | 0.049223% | 688.045 ms |

The selected resolution and total work respond to radius, local domain, and Cut-cell count, not a sphere ID.

## Centroid, active fractions, conditioning, and determinism

The exact translated sphere centroid is `(0.047, -0.031, 0.023)`. The adaptive boundary-area centroid is `(0.047318, -0.030857, 0.022391)`. Per-cell artifacts include frame, domain, resolution, certificate, oracle errors, occupancy, domain aspect ratio, curvature anisotropy, offset range, normal variation, and sample count.

For the primary adaptive integration the minimum sampled active fraction is zero; 79 Cut cells are below 1%, 138 below 5%, and 171 below 10%. These are diagnostics only. No tiny-cut-cell stabilization or solver consequence is introduced.

Two complete repeated experiment runs produced identical timing-free JSON. SHA-256: `bf58e0aab5553fb3ff273ba616474980a97029c4bfbb6fd51d4fc51f2682d0c5`.

## Limitations and M3

- The exact query bridge intentionally supports sphere faces only in M2. Torus is the recommended M3 mechanical fixture because its unequal principal curvatures will test truly curvature-driven directional resolution rather than footprint-driven anisotropy.
- The sphere orientation matrix rotates its exact parameter frame, but rotational symmetry means it cannot expose all shape/grid alignment effects. Torus should provide that stronger test.
- Interpolated exact normal samples are the runtime default. The gradient-derived alternative was not added because exact-normal interpolation error was already below 0.0005 degrees and was not limiting.
- The projected-footprint integrator uses dense deterministic CPU quadrature to make the evidence trustworthy. Its 400+ ms integration cost dominates production-like timing and is the clearest M3 optimization target. Analytic integration of piecewise-bilinear graph patches clipped by a box should replace dense evidence quadrature before claiming a setup-time performance win.
- The selective trigger is generic (certificate plus occupancy disagreement), but it did not beat the adaptive-only sphere row.

Recommended M3: add exact torus face queries and principal-curvature directions, test concave material-side orientation and seam crossing, add a gradient-normal comparison, and replace dense footprint quadrature with conservative analytic/piecewise integration. Continue to keep AMR and physics out of scope.

## Reproduction and evidence

Run `dotnet run --project tools/Aetheris.Continuum.M2/Aetheris.Continuum.M2.csproj -c Release`.

Evidence is under `docs/continuum/artifacts/m2/`: `benchmark-summary.json`, `primary-curved-fixture-diagnostics.json`, `orientation-matrix.json`, `curvature-matrix.json`, `runtime-vs-oracle-cost.json`, `m1-cost-audit.json`, `deterministic-geometry.json`, and `deterministic-hashes.json`.
