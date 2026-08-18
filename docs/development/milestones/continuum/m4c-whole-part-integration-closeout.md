# AETHERIS-CONTINUUM-M4C — whole-part curved-support integration closeout

## Status

M4C is closed successfully. Fixed 12³ smart-boundary volume integration beats the unchanged 24³ brute-force control for the generic coaxial part and reference HexBolt, including all three HexBolt orientations. The production curved/multi-support path has no volumetric MSAA fallback. It uses exact Plane/Cylinder/Cone/Torus ray partitions, bounded CIR interval classification, deterministic projection planning, and support-local maps retained on every contributor. Release Cut integration falls from the M4B reference of about 594 ms to 439 ms (generic) and 442 ms (HexBolt baseline).

This closes the geometric blocker for conventional sparse linear elasticity. It does not introduce AMR, mechanics, new surface families, or mesh authority.

## M4B weak-path audit and orientation pathology

Two concrete defects caused the old approximately 1% result.

1. The benchmark built `CutCellBoundarySet` objects but accumulated the classifier's 4×4×4 occupancy, not composed occupancy. Curved local-map improvements therefore could not affect the headline.
2. Planar-only cells were clipped by every plane in the shell, treating a non-convex whole part as a single global convex half-space intersection. Baseline alignment maximized those cells; rotations happened to bypass many of them.

Regular, grid-aligned Cylinder/Cone footprints also repeated the same 4³ sampling phase, making their errors coherent. Rotation decorrelated the fallback samples and appeared much better despite identical exact geometry. M4C accumulates the real composer, clips planar cells only by bounded local candidates, and replaces three-dimensional binary coverage with exact-support line partitions. Per-cell audits and before/after worst-cell lists identify the former `SingleFace`, `TwoFaceEdge`, and `MultiFaceTrimJunction` offenders directly.

## Bounded multi-support composite domain

Every non-planar Cut cell is integrated as a deterministic two-dimensional family of bounded rays through the fixed cell:

1. Exact BRep Plane, Cylinder, Cone, and Torus supports provide all ray intersections.
2. Candidate bounds, cone branch, and CIR boundary checks reject non-owning support roots.
3. Sorted roots partition a ray into bounded intervals; one CIR midpoint determines each interval's material occupancy.
4. The selected projection integrates occupied interval lengths over its structured footprint.
5. The same roots supply support-owned boundary area through the best-conditioned Cartesian projection, preventing double area.

A bounded one-dimensional CIR root recovery is used only when conservative face bounds omit a trim owner on a cell edge. It is not volumetric MSAA. Production fallback order is exact planar clipping, exact-support structured composite rays, then explicit bounded MSAA after rejection. The target runs use the first two only and report zero MSAA samples.

Each `CutCellBoundaryContributor` retains exact face ID, support kind, edges, vertices, material-side evidence, local frame/map, certificate, and map resolution. BRep shared edges and vertices continue to classify `TwoFaceEdge`, `ThreeFaceCorner`, `FilletContact`, and `MultiFaceTrimJunction`; CIR owns the occupied intervals. Hyperbola trims remain BRep edge constraints and never become a CIR primitive.

## Support-family paths

- Plane uses exact local convex clipping when it is the only family and exact ray intersections in mixed cells.
- Cylinder keeps the M1 generator/circumferential frame and anisotropic map, while exact quadratic intersections participate in every whole-part composite.
- Cone uses generator/circumferential frames, local radius, semi-angle, and anisotropic resolution. Exact quadratic roots plus the forward-cone branch handle Plane/Cone and Hyperbola-trim junctions.
- Torus reuses the M3 support/map machinery. Whole-part occupancy uses the same exact torus implicit equation with deterministic quartic root isolation. Derivative-partitioned isolation retains all visible ray branches, so a tangent-horizon cell is represented by several bounded charts rather than forced into one height map. Periodic seams are resolved by root ordering and deterministic de-duplication.

No separate whole-part torus geometry algorithm, tessellation, or random tie-breaking was introduced.

## JudgmentEngine

Single-face and planar fast paths bypass JudgmentEngine. Multi-face cells can have three valid Cartesian projection plans. Their utility is deterministic:

`100 × mean support-normal conditioning − 2 × estimated support/evaluation cost`

Axes with tangent/degenerate conditioning are rejected before scoring. Candidate name, admissibility, utility, conditioning, cost, rejection reason, and selection are persisted per cell. Baseline HexBolt used 248 integration-plan JudgmentEngine calls. The fixed-policy ablation produced 0.027021% volume error; utility selection produced 0.023551%, so the scored policy remains. Composition ambiguity uses the pre-existing bounded topology judgment (36 baseline HexBolt calls).

## Results

| Fixture / orientation | M4B volume | M4C volume | fine control | M4B area | M4C area | Release Cut integration |
|---|---:|---:|---:|---:|---:|---:|
| generic coaxial | 0.929606% | 0.024783% | 0.610790% | 1.376899% | 0.153632% | 439 ms |
| HexBolt baseline | 1.010675% | 0.023551% | 0.459491% | 1.709262% | 0.168072% | 442 ms |
| HexBolt Y 29° | 0.138321% | 0.011560% | 0.030153% | — | 0.285227% | 480 ms |
| HexBolt compound | 0.081250% | 0.018391% | 0.041712% | — | 0.471401% | 372 ms |

Fixed smart-boundary volume beats fine sampling by about 18.5× on generic and 19.5× on baseline HexBolt. Baseline is now on the rotated accuracy scale without orientation-specific policy.

Baseline HexBolt records 99,104 exact-support evaluations, 473,960 CIR classifications, 1,064 structured resolution promotions, and zero MSAA samples. Generic records 99,048, 435,872, 1,016, and zero respectively. Local map sample cache hit rate is zero because frame origins are cell-local; a broad cache was intentionally not added.

## Area ownership limitation

The exact-support composite reports area from the same roots as volume. Its curved-family contributions are stable across orientation, but a Cut-cell-only sum cannot uniquely own an exact planar boundary coincident with a lattice face. Baseline and compound local planar totals therefore retain 3–6% grid-face ownership bias. Applying face-domain trim rejection was tested and rejected because it removed valid shared-transition roots and degraded compound volume.

The production headline retains the independent deterministic CIR area control (0.15–0.47% error), while both same-domain area and per-support totals remain persisted diagnostics. This is an integration-reporting limitation, not an occupied-volume or exact-geometry ambiguity, and does not block assembling conventional element volumes and boundary loads with an explicit lattice-face ownership convention.

## Regressions and evidence

- M1 Cylinder: volume 0.018660%, area 0.025611%; green.
- M2 Sphere: volume 0.003056%, area 0.004722%; green.
- M3 root fillet: volume 0.017908%, area 0.047143%; green and shares the torus support/map core.
- Focused M4C tests cover fixed-vs-fine accuracy, support-map retention, edge/fillet/trim identity, Hyperbola-trim Cone/Plane junctions, deterministic utility selection, four-branch torus quartics, orientation robustness, and zero production MSAA fallback.

Deterministic JSON evidence is under `docs/development/milestones/continuum/artifacts/m4c/`, including benchmark summary, support/composition audits, all-orientation cell audits, worst cells, utility traces and ablation, orientation matrix, M4B comparison, fixed-vs-fine comparison, regressions, and hashes.

## Recommendation for M5

Proceed to conventional sparse linear elasticity on the fixed lattice. Preserve the M4C compositor as geometric authority, adopt one explicit half-open ownership rule for lattice-coincident boundary loads, and keep the bounded per-cell oracle in development tests. Tiny-cut stabilization, AMR, and additional surface families remain separate future work.
