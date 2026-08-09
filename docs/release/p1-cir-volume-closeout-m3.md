# P1-CIR-VOLUME-CLOSEOUT-M3

## Decision

Outcome B: intentional defer. Correct post-STEP CIR/FRep occupied-volume verification is a large reconstruction project, not Preview 1 wiring. Whole-loop Fillet remains Experimental.

## Architecture map

| Layer | Implemented responsibility | Relevant limits |
| --- | --- | --- |
| Firmament primitive lowering | Boxes, cylinders, cones, spheres, tori and basic Boolean composition lower to `SdfNode` | canonical Profile/Compose whole-loop Fillet does not lower to CIR; `Fillet` is explicitly rejected by `FirmamentCirLowerer` |
| CIR semantic field | Primitive signed-distance evaluators, transforms, union, subtraction and intersection | no node describing a trimmed Profile Fillet or its ExactRolling/compatibility policy |
| CIR runtime | `SdfTape` point and interval evaluation | primitive/Boolean instruction set only |
| CIR volume | dense regular-cell center sampling and adaptive region subdivision/direct sampling | deterministic and approximate, but no returned absolute error bound or convergence certificate |
| CIR to BRep | narrow rematerializers for box-minus-cylinder and box-minus-box; torus subtraction recognized but unsupported | direction is opposite the required bridge |
| BRep to CIR | none | reconstructing occupied material from arbitrary imported trimmed analytic faces requires topology/domain interpretation |
| BRep | exact topology/boundaries and STEP export/reimport | bounded exact mass recognizers exist for a few families; generic mass uses display tessellation |
| Tessellation | display and diagnostic volume estimate | not authoritative for `Assert Volume` after M3 |

The whole-loop BRep producer constructs analytic Plane/Cylinder/Sphere/Torus patches directly. It does not retain a CIR mirror in `FirmamentStepExportResult`, and CIR does not survive STEP. Consequently neither policy has a pre-export CIR occupied-region model suitable for volume comparison.

## Current Assert Volume pipeline

Before M3:

`Assert source -> build BRep -> STEP -> reimport BRep -> BrepMassProperties -> compare -> build gate`

The substitution point is `FirmamentBuildAndExport.EvaluateVolumeAssertions`. A future CIR measurement result can be supplied there without changing syntax or STEP topology verification. Today there is no correct CIR input at that point.

After M3, exact BRep recognizers retain their existing bounded gate. The generic `DeterministicTrimmedFaceTriangulationBoundaryIntegral` result is explicitly tagged `IsTessellatedSanityEstimate=true`, `IsAuthoritativeForVolumeAssertion=false`, is serialized with the assertion result, and does not fail a build. It remains useful for diagnostics and comparison.

## Complexity classification

Post-STEP CIR is **Large**. It needs either a BRep-to-occupied-region interpreter for trimmed periodic analytic surfaces and shell orientation, or a dual-representation Profile Fillet CIR lowering plus an artifact-equivalence contract. Both also need a CIR volume estimator with an explicit error contract. Existing adaptive sampling reports counts and an estimate but no lower/upper volume interval. Wiring it as authoritative would merely replace triangle sampling with unbounded voxel sampling.

## Evidence and promotion

M2 remains the forensic comparison. ExactRolling is externally/source-derived as 913725.7396023329 mm³ while the BRep sanity estimate is 881896.8785532190 mm³. Compatibility is 913733.5792146825 mm³ while its sanity estimate is 879274.5010217372 mm³. FreeCAD 1.0.2 matches the source-derived values. M2's partial planar experiment was reverted after M3 full-corpus validation exposed incorrect shared trim orientation on other bodies. CIR values do not exist for either whole-loop policy, so no new tolerance is justified and the promotion gate fails.

Authority boundaries are now explicit:

- BRep owns topology, exact boundaries, and interchange.
- CIR/FRep owns occupied-region evaluation and is the intended future volume authority.
- tessellation owns display and coarse sanity evidence.

No geometry, STEP artifact, support status, or freeze-manifest claim changed.
