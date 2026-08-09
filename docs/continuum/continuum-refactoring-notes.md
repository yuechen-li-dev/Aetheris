# Continuum refactoring notes

These items were discovered during AETHERIS-CONTINUUM-M0 and intentionally not expanded into broad reconstruction.

## Historical SDF types retain `Cir*` names

- **Current state:** `CirNode`, `CirTape`, `CirBounds`, and related records now live under `Aetheris.Continuum.Backends.Sdf`, but their names predate the rule that CIR is broader than SDF.
- **Problem:** the namespace communicates backend ownership, while individual names can still suggest that the SDF tree is the whole CIR model.
- **Why larger than M0:** renaming the public family affects Firmament lowering/materializers, FrictionLab experiments, docs, external API consumers, and serialized/reflected type-name assumptions that need a compatibility policy.
- **Proposed direction:** rename toward `SdfNode`, `SdfTape`, and a general bounds type in a source-breaking milestone; keep `SdfContinuumRegion` as the explicit CIR adapter and retire old names once consumers migrate.
- **Blocks M0:** no.
- **Recommended milestone:** CONTINUUM-M1A API naming and compatibility review.

## Legacy AIR/prismatic bridge reaches Core internals

- **Current state:** the relocated `Bridges/Air` and `Mirrors` code uses internal AIR and prismatic source records through `InternalsVisibleTo("Aetheris.Continuum")`.
- **Problem:** dependency direction is correct, but the friend-assembly surface is implicit and wider than a small stable bridge contract.
- **Why larger than M0:** extracting public source descriptors requires coordinated AIR/BRepPlan design and would affect experimental chamfer/prismatic routes.
- **Proposed direction:** define a small immutable public/internal bridge DTO in Core that exposes only admitted section vertices, correspondence, source identity, and provenance; have Continuum consume that DTO.
- **Blocks M0:** no; Core has no Continuum reference and behavior remains regression-tested.
- **Recommended milestone:** AIR-CIR-M1 bridge contract.

## Firmament contains substantial CIR-to-BRep recovery logic

- **Current state:** Firmament recognizers/materializers and FrictionLab experiments consume CIR trees to recover bounded BRep output.
- **Problem:** historical documents sometimes frame CIR as constructive boundary authority; this conflicts with the new authority contract if generalized.
- **Why larger than M0:** the paths contain feature-family policy, topology stitching, trim oracles, recovery diagnostics, and many regressions. Moving or redesigning them would be kernel-wide work.
- **Proposed direction:** retain only explicit, bounded rematerializers as consumer adapters with declared loss/admissibility; prefer AIR/BRep construction as the exact path and prohibit generic implicit-to-exact claims without proof.
- **Blocks M0:** no; existing consumers compile and retain behavior.
- **Recommended milestone:** CIR-BREP authority cleanup after Continuum M1 experiments.

## Generic BRep-to-CIR conversion is absent

- **Current state:** generated Firmament/AIR shapes may carry admitted mirrors; arbitrary imported trimmed BRep/STEP does not.
- **Problem:** future continuum analysis of arbitrary imports needs occupied-region interpretation and boundary correspondence.
- **Why larger than M0:** correct conversion requires oriented closed-shell interpretation, trimmed periodic analytic surfaces, robust point containment, tolerance/error contracts, and face-identity mapping.
- **Proposed direction:** start with a bounded closed analytic-shell adapter that exposes occupancy and exact face candidates; do not begin with universal tessellation-as-authority conversion.
- **Blocks M0:** no; analytic fixtures prove the substrate.
- **Recommended milestone:** CONTINUUM-BREP-BRIDGE-M0, separately scoped.

## SDF interval and transform contracts need mathematical labels

- **Current state:** the tape preserves analytic interval bounds and transformed primitives; `CirTransformNode.Evaluate` returns the child scalar after inverse transformation.
- **Problem:** under general non-rigid scaling, the scalar may retain correct sign/occupancy but is not necessarily exact Euclidean signed distance. `CirIntersectNode.Bounds` also uses a safe union rather than a tight intersection.
- **Why larger than M0:** tightening requires auditing transform classes, Lipschitz/error metadata, empty intersections, and all interval regression expectations.
- **Proposed direction:** distinguish sign-correct implicit field, conservative interval field, and exact-distance capability; advertise exact signed distance only when transform/composition rules preserve it.
- **Blocks M0:** no; M0 classification requires occupancy and bounded classification, not universal exact-distance claims.
- **Recommended milestone:** SDF-CAPABILITIES-M1.

## Serialization/versioning contract is missing

- **Current state:** no general CIR serializer was found. Existing diagnostics and CLI records are runtime/debug shapes; repository sources were updated to the new namespace directly.
- **Problem:** reflected type-name persistence or future public interchange would be brittle across the extraction.
- **Why larger than M0:** a stable format requires schema/version/identity decisions beyond moving code.
- **Proposed direction:** define a representation-neutral CIR descriptor schema with explicit backend payload/version only when a real persistence consumer exists.
- **Blocks M0:** no.
- **Recommended milestone:** first milestone that needs persisted Continuum artifacts.

## Boundary-area estimation is intentionally absent

- **Current state:** analytic fixtures report exact boundary area, while sampled estimated area is null.
- **Problem:** occupancy coverage alone does not define a trustworthy surface-area estimator.
- **Why larger than M0:** a useful estimate should consume reconstructed local cuts, normals, or offset maps and carry an error contract.
- **Proposed direction:** make boundary-position and area error part of the first BoundaryOffsetMap experiment.
- **Blocks M0:** no; volume convergence and Cut-cell localization are demonstrated.
- **Recommended milestone:** CONTINUUM-M1 offset-surface/MSAA experiment.

## BoundaryOffsetMap V1 integration is Z-extruded

- **Current state:** M1 integrates sampled local graphs whose `V` tangent is the lattice Z axis. This covers the analytic through-cylinder and oblique-plane controls with one representation-neutral polygon clipper.
- **Problem:** arbitrary oriented/trimmed surfaces need local-domain clipping and surface-area/first-moment integration that is not reducible to one extruded XY section.
- **Impact:** the V1 map representation is general enough to carry such data, but its first occupancy/area estimator is intentionally bounded.
- **Proposed fix:** add a local patch-domain integration contract and test it first on one non-extruded analytic sphere or torus patch; do not route through arbitrary triangles or a global atlas.
- **Blocks M1:** no; both declared M1 fixtures are exact Z extrusions and fail explicitly otherwise.
- **Suggested future milestone:** CONTINUUM-M2 local patch integration.

## Independent offset-map validation dominates experimental query cost

- **Current state:** every generated M1 map is checked at `(2*resolution+1)^2` independent half-stride points. The best cylinder run spends 32,192 of 45,120 raw requests on validation.
- **Problem:** this is appropriate proof cost for M1 but too expensive as a default production cache policy.
- **Impact:** fixed-lattice accuracy and runtime still improve, but exact-query counts make the validation/production distinction important.
- **Proposed fix:** retain exhaustive mode for evidence and add a deterministic bounded validation policy or analytic error certificate capability for trusted supports.
- **Blocks M1:** no; query accounting includes the cost rather than hiding it.
- **Suggested future milestone:** CONTINUUM-M2 map certification and validation policy.

## M2 projected-footprint integration is accuracy-oriented, not production-cheap

- **Current state:** M2 removes the arbitrary-orientation blocker by projecting each Cut-cell box to the exact local frame, taking its convex footprint, and deterministically integrating material thickness and bilinear-map area. A dense bounded quadrature makes the sphere evidence stable.
- **Problem:** the quadrature dominates the production-like M2 timing (roughly 400 ms of the 451 ms adaptive baseline) even though it performs no exact BRep queries.
- **Impact:** fixed-lattice geometry beats the 32-cubed reference on volume error and the runtime certificate eliminates oracle queries, but the experiment does not yet establish an end-to-end setup-time win.
- **Proposed direction:** integrate clipped piecewise-bilinear graph patches analytically or with a conservative adaptive error estimate; preserve the current dense path as the experimental oracle. Do not introduce AMR or make triangles geometry authority.
- **Blocks M2:** no; M2's intended exact-BRep identity, arbitrary-frame, fidelity, area/volume, orientation, and certification proofs all execute on the real path.
- **Suggested future milestone:** CONTINUUM-M3 torus plus cheap local graph integration.
# Exact face-normal semantic naming

- **Issue:** `ExactBrepBoundaryQuery.OutwardNormal` historically encoded the sphere assumption that the bound face normal is outward. The production `ConcaveFilletConstruction` torus support is deliberately oriented into material, so `FaceGeometryBinding.SameSense` alone does not establish a universal material/outward convention across construction families.
- **Impact:** Continuum must not silently negate a concave face normal. Doing so reversed the root-fillet material-side probe during M3 bring-up.
- **Proposed direction:** introduce a kernel-level oriented-shell query that returns both exact support normal and shell material-side classification. Until that exists, Continuum fixtures must state the construction contract explicitly and verify it against CIR with deterministic two-sided probes.
- **Blocker status:** not an M3 blocker; M3 records `exactFaceNormalIsMaterialSide` explicitly and fails on CIR disagreement.
- **Recommended future milestone:** multi-face whole-part Cut-cell composition, where oriented-shell authority can replace per-fixture declarations.
