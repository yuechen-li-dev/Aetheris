# Face Orientation Authority

STEP `ADVANCED_FACE.same_sense` is source evidence, not Aetheris face-orientation authority.

The exact STEP importer first creates import-only `ImportedFacePatch` values with support geometry, normalized trims, topology, and `SourceFaceOrientationEvidence`. `StepFaceOrientationResolver` then builds deterministic face adjacency from shared edges and propagates a relative orientation from the lowest face ID in each connected component. Nonmanifold adjacency is rejected; contradictory propagation is diagnosed and remains ambiguous rather than guessed. The importer never mutates support-surface parameterization.

For a closed component, the resolver tessellates the locally consistent candidate only as a bounded divergence-theorem oracle. Positive signed volume is required for an outer shell; negative signed volume is required for an inner void shell. A whole component is flipped when its material role requires the opposite sign. Open shells retain local consistency with `DerivedLocallyConsistentGlobalUnknown`; a near-zero or unavailable closed-shell volume is `Ambiguous`. No source flag is promoted to global authority in either case.

The completed boundary is structural:

```text
ImportedFacePatch (no orientation)
  -> StepFaceOrientationResolver
  -> FaceGeometryBinding.Orientation : ResolvedFaceOrientation
  -> OrientedFacePatch report/provenance
```

`FaceGeometryBinding` deliberately has no `SameSense` property. Display tessellation, mass properties, section analysis, Continuum queries, and STEP export consume `ResolvedFaceOrientation`. The raw STEP flag remains only in `BrepBody.FaceOrientationReport` for diagnostics and import reporting. A disagreement produces `Importer.StepOrientation.SourceOrientationMismatch` and records the source entity, derived value, shell, evidence summary, and chosen action.

`BrepDisplayTessellator` projects canonical orientation once when it creates each `DisplayFaceMeshPatch`. Assembly and Web Runtime exporters consume that patch directly and must not invoke another orientation pass. STEP export serializes the canonical resolved orientation; it never copies provenance back as authority.

X1B makes boundary role and periodicity explicit. `FaceBoundaryRoleBinding` records which face-local loop is outer or inner; allocation order is not semantic. `SurfacePeriodicity` records admitted U/V periods and owns modulo-period equality. Full-period constant-coordinate loops derive winding from signed continuous period traversal, while sphere and non-full conical loops prefer support-relative 3D boundary evidence over seam-sensitive planar shoelace area.

STEP export chooses its boundary serialization route through a bounded JudgmentEngine policy: complete explicit roles win, authored order is a fallback only when role evidence is absent, and malformed partial evidence is rejected. The numerical winding calculation itself is deterministic and is not utility-scored.

Current limitation: the topology model can bind a pcurve to each coedge use, but STEP `SURFACE_CURVE`/`SEAM_CURVE` import does not yet populate those bindings, and singular pole/apex `VERTEX_LOOP`s are not yet materialized as explicit loop topology. Therefore X1B does not claim general pcurve or degenerate-boundary acceptance beyond the qualified corpus paths.

This boundary does not alter stable face IDs, source STEP entity provenance, semantic PMI associations, support surfaces, trims, or pcurve parameterization.
