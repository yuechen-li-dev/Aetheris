# Continuum refactoring disposition after M4B

This is the active disposition for AETHERIS-CONTINUUM-M4B. Historical M0–M4 observations are closed here rather than retained as an archaeological backlog.

| Item | Decision | M4B action and result | Remaining debt / revisit trigger |
| --- | --- | --- | --- |
| Historical SDF `Cir*` names | RESOLVE IN M4B | Source-broken backend nodes, bounds, tape, instructions, payloads, analyzers, and adaptive estimators to `Sdf*`; no aliases remain. `SdfContinuumRegion` remains the CIR adapter. | None. |
| AIR/prismatic friend-assembly bridge | RESOLVE IN M4B | Added immutable Core-owned `ContinuumConstructionDescriptor` containing bounded sections, correspondence, admitted operations, source/semantic identity, and provenance. | `InternalsVisibleTo(Aetheris.Continuum)` remains for legacy AIR mirror experiments. Remove it when those experiments accept only the descriptor; do not widen the DTO to expose AIR/BRepPlan internals. |
| CIR/SDF to BRep recovery authority | RESOLVE IN M4B | Classified retained Firmament routes as bounded SDF decompilation/reverse engineering for intent recovery. `SdfDecompilationContract` declares family, purpose, predetermined/recovered topology, exactness, and loss/admissibility. | Rename remaining historical `Frep*`/`CirBrep*` class names only in a separately coordinated consumer API cleanup. Their authority is already bounded. |
| Generic BRep to CIR absence | DEFER WITH EXPLICIT REASON | Generated geometry dual-lowers from typed construction. No reverse-engineering of production BRep was added. | Revisit only for an imported STEP/BRep continuum consumer with a separately scoped analytic-shell containment/correspondence contract. |
| SDF mathematical capability labels | RESOLVE IN M4B | Added `SignCorrectOccupancy`, `ConservativeIntervals`, `ExactEuclideanSignedDistance`, and `Gradient`. Non-rigid transforms and general CSG do not advertise exact distance; intersection bounds remain explicitly conservative. | Add Lipschitz/error metadata only when a consumer needs quantitative field magnitude bounds. |
| Serialization/versioning | DEFER WITH EXPLICIT REASON | No stable public CIR serialization is promised. M4B JSON is deterministic diagnostic evidence only. | Revisit when a real persisted interchange consumer exists. |
| Boundary-area estimation absent | ALREADY RESOLVED | M1 introduced boundary-map area evidence; M2 generalized exact-support local integration; M4B adds an orientation-corrected CIR area control. | None as an architectural ambiguity. Accuracy improvements remain ordinary numerical work. |
| BoundaryOffsetMap V1 Z extrusion | ALREADY RESOLVED | M2 introduced arbitrary local frames and projected footprints; M3 added anisotropic torus maps and trims. | None. |
| Independent validation dominates cost | ALREADY RESOLVED | M3 separated runtime certificates from optional oracle validation and reported both costs. | Revisit certification policy only for production performance targets. |
| M2 dense projected-footprint cost | ALREADY RESOLVED | M3 structured clipping/integration replaced the dense production path while retaining the dense oracle. | None. |
| Exact face-normal semantic naming | RESOLVE IN M4B | `ExactSupportNormal`, `ParameterizationNormal`, and `MaterialSideClassifier.ClassifyMaterialSide` are separate. Removed `OutwardNormal`, `ExactFaceNormal`, and the root fixture direction flag. | Extend exact-support differential queries only when adding another admitted analytic support family. |
| Complete root-fillet BRep/CIR identity | RESOLVE IN M4B | `ExactCoaxialDualMaterializer` emits complete BRep and complete CIR from one `ExactCoaxialConstructionPlan`; generic coaxial and reference HexBolt associations pass. | None for the admitted coaxial family. |
| `Transform3D` single precision | RESOLVE IN M4B | Replaced `Matrix4x4` storage/operations with deterministic double-precision affine math, inverse, rigidity checks, and preserved first-then-second composition. | Consider a public matrix interchange DTO only if external serialization needs one. |

## Active debt

1. Remove the remaining Continuum friend access after legacy AIR mirror experiments migrate wholly to `ContinuumConstructionDescriptor`. Trigger: the next production use of that mirror path.
2. Imported STEP/BRep continuum support remains absent by design. Trigger: an approved imported-solid mechanics/continuum milestone with containment and face-correspondence requirements.
3. No CIR interchange schema exists. Trigger: a concrete persistence consumer.
4. Whole-part non-planar Cut-cell integration currently combines exact planar composition, M3 torus maps, and bounded CIR sampling controls for other curved cells. Trigger: mechanics accuracy requirements tighter than the persisted M4B error matrix; extend local exact maps rather than adding AMR or mesh authority.
