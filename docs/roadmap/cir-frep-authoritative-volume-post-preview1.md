# CIR/FRep authoritative volume after Preview 1

## Target

Make `Assert Volume` consume a deterministic CIR/FRep occupied-volume result with an explicit absolute error interval, while independently requiring exact-BRep STEP reimport and topology validation.

## Missing work

1. Define a volume-result contract: estimate, lower/upper bounds, absolute target, convergence state, method, and deterministic work limits.
2. Upgrade adaptive CIR integration from sampled mixed cells to certified occupancy bounds. Interval-classified inside/outside cells already provide the scaffold; unresolved cells must contribute explicit `[0, cellVolume]` uncertainty and subdivide until the requested bound or resource limit.
3. Add semantic CIR mirrors for admitted Profile/Compose families. Whole-loop Fillet needs the same radius, station, ExactRolling versus SphereSeamCompatibility, rounded-source, miter, and top/side occupancy semantics as the BRep planner.
4. Preserve that CIR mirror through the build result for dual-representation verification. Do not serialize it into STEP or pretend STEP contains CIR.
5. Decide artifact-equivalence policy. The Preview-safe design is semantic CIR volume plus separate post-STEP manifold, analytic-family, and determinism gates. Generic post-STEP BRep-to-CIR reconstruction should be a distinct later project.
6. Replace `EvaluateVolumeAssertions`' measurement dependency with a representation-neutral occupied-volume result. Retain BRep analytic/tessellated values as cross-check fields.
7. Add fault tests for wrong radius, missing hole/feature, and policy mismatch; add scale, translation, determinism, and performance matrices.

## Milestone decomposition

- V1: certified adaptive CIR volume bounds for existing primitives and Boolean compositions.
- V2: dual-representation build-result contract and primitive/hole `Assert Volume` migration.
- V3: Profile extrusion and bounded chamfer/fillet CIR mirrors.
- V4: seven-station ExactRolling and SphereSeamCompatibility parity, oracle comparison, and promotion reconsideration.
- V5, separately justified: imported BRep-to-CIR reconstruction, only if product requirements demand volumetric analysis of arbitrary STEP input.

## Existing oracles

- primitive and hole analytic formulas;
- `SdfAdaptiveVolumeEstimatorTests` and calibration tests;
- M2 frozen STEP hashes and per-face forensic evidence;
- independent whole-loop values 913725.7396023329 and 913733.5792146825 mm³;
- FreeCAD/OCCT validity and matching volume evidence;
- deterministic STEP, manifold, analytic-family, and zero-NURBS tests.

Display tessellation must remain non-authoritative because M2 demonstrated close coarse/fine convergence alongside 1.8–3.8% systematic trim-domain bias.
