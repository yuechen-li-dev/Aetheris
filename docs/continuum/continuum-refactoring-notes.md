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
