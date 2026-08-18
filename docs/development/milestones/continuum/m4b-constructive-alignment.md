# M4B constructive CIR/BRep alignment

AETHERIS-CONTINUUM-M4B establishes one typed source lineage for complete production coaxial geometry:

```text
ExactCoaxialPartRecipe / HexBoltSpec
    -> ExactCoaxialConstructionPlan
       -> ExactConstructionMaterializer -> complete exact BRep
       -> ExactCoaxialContinuumRegion   -> complete CIR
```

The CIR covers the regular-prism head, cone/plane treatment, top cap, shoulder/root transition, cylinder, end frustum, and end cap. Hyperbola curves remain exact BRep trim identity; CIR represents their occupied-space consequence and does not duplicate trim topology.

`CirBrepAssociation` records CIR region, BRep body, outer shell, semantic model, and construction-source identity. The consistency gate checks deterministic face/edge/vertex boundary samples, interior/exterior probes, conservative extent containment, and lineage. It rejects a different source identity and accepts the generic coaxial part and McMaster reference HexBolt.

Whole-shell candidates use exact BRep curve evaluations for trim bounds. Plane, Cylinder, Cone, and Torus faces are admitted. Material side is resolved by exact support normals plus CIR two-sided probes. Single-face, edge, corner, trim-junction, and fillet-contact composition are reported; JudgmentEngine is called only for the remaining multi-face trim ambiguity.

The persisted metrics under `artifacts/m4b` use one fixed 12³ lattice and a 24³ brute-force control, without AMR. Fixed Cut cells use exact planar composition or bounded CIR sampling for curved multi-support cells; boundary area uses a deterministic orientation-corrected CIR Crofton control. JSON files are diagnostics, not a stable interchange schema.

Double-precision `Transform3D` removes the prior float storage floor. Unrotated association extent residuals return to double roundoff. Rotated CIR bounds remain conservative transformed containers, so their reported containment residual is intentionally non-zero; all exact boundary and occupancy probes still agree.

## SDF decompilation

Existing SDF-to-BRep tooling is retained as reverse engineering for intent recovery. The admitted hole-family policy declares predetermined family topology, exactness within the recognized family, rejection conditions, and known losses. It is not described as general CIR inversion.

## Result and remaining numerical work

The architecture and whole-part representation identity are settled for the admitted production coaxial family. M4B does not claim that every non-planar Cut-cell uses a closed-form exact integrator: Plane is exact, torus reuses M3 structured maps, and the general Cylinder/Cone multi-support path currently uses a bounded CIR sampling control. Tightening that numerical path is the remaining trigger before mechanics tests demand stricter integration tolerances; it does not reopen representation authority.
