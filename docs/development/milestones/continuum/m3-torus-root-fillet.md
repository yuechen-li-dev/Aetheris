# AETHERIS-CONTINUUM-M3: exact torus/root-fillet Cut cells

## Result

M3 answers the milestone question **yes** for the geometry substrate, with one explicit scope boundary: whole-part multi-face composition remains M4 work. A fixed 16³ lattice now carries production-generated concave root-fillet BRep face IDs, evaluates the exact torus directly, builds principal-curvature-aligned rectangular maps, unwraps both periodic parameters continuously, clips local integration to the BRep trim, and uses structured polygon/adaptive integration before an MSAA fallback. No AMR, mechanics, NURBS, or mesh boundary authority was added.

The baseline root fixture used 4,096 volume cells and 168 torus Cut cells. It obtained 0.017900% composite volume error and 0.047143% root-fillet area error. The 32³ / 32,768-cell MSAA control had 0.166207% volume error, so the fixed lattice was about 9.3 times more accurate with one eighth as many volume cells. The torus map oracle reported no false accepts, a maximum position error of 3.931e-4, and a maximum normal error of 0.188019°.

## Primary fixture and exact authority

The fixture reuses `ExactCoaxialPartBuilder` and `ConcaveFilletConstruction`. Its materializer creates two trimmed half-faces on one exact `TorusSurface`, adjacent through actual BRep edges to the shoulder `PlaneSurface` and shaft `CylinderSurface`. The experiment isolates that production root region with an analytic CIR shoulder/quarter-torus/shaft occupancy model. CIR remains occupancy authority; BRep remains authority for face identity, evaluation, projection, recovered parameters, normals, directions, and curvature. `SurfaceMeshIR` is not consulted.

The local profile has shaft radius `a = 0.8`, minor radius `r = 0.3`, and torus major radius `R = a + r = 1.1`. Its exact quarter-torus area is

`A = π² R r - 2π r²`.

The independent composite volume reference is the sum of the head cylinder, the analytic revolved quarter-circle transition,

`π [R²r - (π/2)Rr² + (2/3)r³]`,

and the shaft cylinder.

## Torus query, seam, and differential geometry

For torus axis `A`, reference axes `X,Y`, major direction `D(u)=cos(u)X+sin(u)Y`, major radius `R`, and minor radius `r`, the exact support is

`P(u,v) = C + [R + r cos(v)] D(u) + r sin(v) A`.

Projection recovers

- `u = atan2(q·Y, q·X)`
- `v = atan2(q·A, |q-(q·A)A|-R)`

and reevaluates the exact support. Both angles normalize to `[0,2π)` at the public boundary and unwrap to the closest representative around a local reference. Seam tests exercise values immediately on either side of `0/2π`; local domains therefore do not expand by a full revolution.

The normalized parameter derivatives supply the principal directions. With the geometric support normal, the principal curvatures are

- major direction: `κu = cos(v)/(R + r cos(v))`
- minor direction: `κv = 1/r`

with signs adjusted by the bound face sense. Local `U` follows the exact major-angle principal direction; `V = N × U`; and `N` is the explicitly validated material-side normal. Reorthogonalization removes only transform roundoff and does not replace the exact direction choice.

## Concave material side and contacts

The production concave construction orients its torus face normal into material. That is declared as a construction contract, not inferred by a nearest-point heuristic. Three major angles and three minor angles receive two-sided probes: `P + εN` must be occupied by CIR and `P - εN` must be outside. Construction fails immediately on disagreement.

Topology-based contact validation finds shared edges rather than coordinate-near surfaces. For Torus↔Plane and Torus↔Cylinder it verifies face kinds, shared edge identity, endpoint incidence, position residual, and normal tangency. The baseline residual is at numerical zero and the angular tangency error is below the `1e-5°` admission threshold.

The root is the bounded minor-parameter interval `[π,3π/2]`, not a full torus. A signed trim distance is attached to each sampled map. Structured surface triangles are clipped against both the Cartesian cell and this exact parameter trim. A small analytic-support halo is permitted while constructing maps for contact cells; it is never attributed to root area.

## BoundaryOffsetMap and certificate

For each Cut cell, the exact torus is solved as the local graph

`P(u,v) = P0 + uU + vV + h(u,v)N`.

`h` comes from a deterministic Newton solve of the analytic torus implicit equation along `N`; map nodes never come from tessellation. The existing bilinear `Nu×Nv` map remains a derived cache, not geometry authority.

The admitted resolutions are `4, 8, 16, 24` independently in each direction. `JudgmentEngine` admits candidates whose engineering certificate satisfies the requested position and normal bounds, then minimizes node count deterministically. The certificate uses principal-curvature magnitudes, directional map spacing, patch extent, a minor-circle horizon rejection, and second-order position/normal variation. It is an engineering bound, not a formal proof.

Baseline distribution:

| Resolution | Cells |
|---|---:|
| 4×24 | 8 |
| 8×16 | 8 |
| 8×24 | 52 |
| 16×16 | 36 |
| 16×24 | 64 |

The tighter-radius sweep moves almost all cells to `24` samples in the high-curvature minor direction, demonstrating curvature-driven anisotropy rather than fixture-ID branching.

## Integration bottleneck audit

M2’s projected-footprint volume loop triangulated the compact hull, then laid a uniform 64² microtriangle grid over every hull triangle. A typical six-vertex footprint therefore issued 16,384 thickness/map evaluations per Cut cell. It separately evaluated four map points for each cell in a fixed 24×24 area sweep (2,304 evaluations per Cut cell). The audit found hull construction and allocation negligible; repeated bilinear evaluation in dense uniform thickness sampling dominated, with the area sweep secondary. There were no point-in-polygon tests in the old path; the convex-hull triangulation implicitly supplied the domain.

M3 represents the projected box footprint explicitly as a deterministic counter-clockwise polygon with vertices, signed area, source, and degeneracy. It clips that polygon against each bilinear map rectangle, triangulates the small clipped polygons, uses degree-two triangle quadrature, and subdivides only when centroid-versus-three-point thickness variation exceeds the volume tolerance. Surface area uses map-node triangles clipped in 3D to the cell and source trim. Work buffers are local and bounded; diagnostics record evaluations, triangles, subdivisions, maximum depth, and estimated buffer bytes.

The fallback hierarchy is:

1. structured map-cell polygon integration;
2. error-driven deterministic triangle subdivision;
3. MSAA when a contact/other-face cell is not a single torus graph or when map/MSAA occupancy disagreement exceeds the admitted threshold.

That strategy selection uses `JudgmentEngine`, so admissibility and fallback are explicit. Dense 64² integration remains only the independent control/oracle.

## Old versus new

Representative release runs (timings vary with host load):

| Fixture | Old method | New method | Accuracy change | Integration/setup effect |
|---|---|---|---|---|
| Cylinder | 4³ MSAA | analytic column polygon | volume 0.235506% → 0.046930%; area 5.181638% → 0.233887% | 7.09 ms → 5.73 ms total fixture path |
| Sphere | dense projected 64² | structured polygon/adaptive | volume 0.002759% → 0.003056%; area 0.021314% → 0.004722% | 475.34 ms → 232.37 ms; evaluations 11,474,432 → 1,562,042 |
| Root fillet | dense projected 64² | structured polygon/adaptive | root area 0.047143%; composite volume 0.017900% | 166.72 ms → 110.75 ms for local torus integration in the latest representative audit |

The sphere regression is the clean comparison to M2’s 390–480 ms bottleneck: the new path preserves the M2 accuracy scale, improves area accuracy, removes about 84.5% of evaluations, and reduces integration time by roughly 55%. Production-like runtime excludes the independent map oracle and dense control. For the primary composite fixture it includes explicit MSAA fallback on Plane/Cylinder/contact cells; that is why its total runtime is larger than torus integration alone.

## Orientation matrix

| Orientation | Cut / torus cells | Volume error | Root-area error | Max position | Max normal | Production / oracle ms |
|---|---:|---:|---:|---:|---:|---:|
| baseline | 996 / 168 | 0.017900% | 0.047143% | 3.931e-4 | 0.188019° | 622.10 / 142.76 |
| rotate Y 29° | 1130 / 128 | 0.005046% | 0.024426% | 4.492e-4 | 0.175754° | 301.35 / 117.21 |
| compound 17/31/13° | 1118 / 98 | 0.002065% | 0.005574% | 4.864e-4 | 0.072100° | 190.42 / 98.31 |

The area, frame, seam, and normal results remain stable under rotation. Composite volume changes modestly because non-torus face/contact cells deliberately use the bounded MSAA fallback; no volume-lattice refinement occurs.

## Curvature matrix

| Minor radius | Curvature ratio | Torus cells | Volume error | Root-area error | Resolution response |
|---:|---:|---:|---:|---:|---|
| 0.35 | 3.286 | 109 | 0.002794% | 0.014335% | 85 at 24×24; 24 anisotropic |
| 0.30 | 3.667 | 98 | 0.002065% | 0.005574% | 91 at 24×24; 7 at 16×24 |
| 0.25 | 4.200 | 87 | 0.000927% | 0.002398% | 86 at 24×24; 1 at 16×24 |

## Diagnostics, determinism, and limitations

Per-cell JSON records the index, exact BRep face ID, support kind, frame, recovered torus parameters, principal curvatures, resolution, footprint polygon, active fraction, certificate, oracle errors, geometry/contact class, integration work, contribution, and offset range. The baseline minimum active fraction is zero; 92 Cut cells are below 1%, 204 below 5%, and 220 below 10%. These are diagnostics only—no stabilization or solver exists.

Repeated geometry runs serialize identically and produce the same SHA-256 artifact hash. Timings are deliberately excluded from the deterministic projection.

M3 does not yet compose all exact faces of the full production part into one general Cut cell. Plane/Cylinder/contact occupancy uses the explicit MSAA fallback in this fixture, and no HexBolt secondary run was needed because the primary fixture already uses the same production `ConcaveFilletConstruction` materializer used by HexBolt. The recommended M4 is multi-face whole-part Cut-cell composition plus a kernel oriented-shell/material-side query; it should reuse the torus query, trim, certificate, and structured integrator unchanged.

Machine-readable evidence is under `docs/development/milestones/continuum/artifacts/m3/`.
