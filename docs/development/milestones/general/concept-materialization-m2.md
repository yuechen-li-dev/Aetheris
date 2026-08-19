# CONCEPT-MATERIALIZATION-M2: typed points and materialized conformance

M2 is the first bridge from resolved compile-time Concept IR points into materialized semantic Feature AIR. It also makes a materialized `Struct` or `Model` satisfy a language `Concept` through an explicit semantic surface rather than inferred BRep topology.

## Source form

M2 reuses the existing semantic-hole syntax. A Concept point replaces the previously literal face-local `center`:

```firmament
Modify Base {
    hole<shaft> LeftMount {
        on: Base.Top
        center: BracketConcept.MountPoints[0]
        diameter: 8.5mm
        end: throughAll
    }
}
```

The bounded materialization lane accepts a history-known rectangular `Box`, a planar `+Z` entry face (`Base.Top`, `face(+Z)`, or a compatible Concept `Plane`), a positive shaft diameter, `throughAll`, and a resolved Concept IR `Point3`. M2 does not add a second hole compiler: it creates the same `FirmamentV2SemanticHoleDecl`, lowers to the existing `AirHoleFeature`, and uses `AirHoleSimpleShaftMaterializer` or `AirHoleCompositeMaterializer`.

Materialized conformance is declared with:

```firmament
Struct Bracket: MountingFrame {
    // materialized features
    Expose {
        Bounds: BracketConcept.Bounds
        TopPlane: Base.Top
        CenterAxis: BracketConcept.CenterAxis
        MountPoints: BracketConcept.MountPoints
    }
}
```

`Expose` is a structural semantic-member declaration. It is not the older box-record `expose { face(+Z) => top }` alias syntax, and it does not enumerate or infer topology.

## Typed Point3 conversion

The Concept resolver keeps each grid point as `ConceptIrPoint3Value`, including its coordinates, stable ID, ordinal, source member, and provenance. For a hole center it resolves the indexed value and performs this explicit conversion:

```text
ConceptIrPoint3Value + selected planar face
  -> plane-distance check (1e-9 model-unit tolerance)
  -> bounded rectangular face check
  -> +Z local frame (u = X, v = Y)
  -> FirmamentV2ResolvedPoint3 + FirmamentV2FaceLocalPoint2D
  -> AirResolvedPoint3PlacementSource + AirFaceLocalHolePlacement
```

The 3D value remains attached to AIR; only the materializer consumes `U` and `V`. No arbitrary projection is performed. An off-plane point reports `firmament-concept-point-not-on-placement-plane`; a point outside the bounded face reports `firmament-concept-point-outside-placement-face`; an unsupported face or projection rule reports `firmament-concept-point-projection-unsupported`. Invalid static indices and materialized values requested during Concept IR resolution retain their existing dedicated diagnostics.

Grid expansion remains deterministic row-major. Stable IDs are path based (`concept:BracketConcept.MountPoints[0]`) and do not depend on unrelated declarations.

## Semantic members and conformance

`ConceptIrSemanticMember` is the typed shared representation for a materialized exposure. It retains the member name and `ConceptIrType`, typed value where available, semantic reference where applicable, compiler phase (`ConceptIr` or `FeatureAir`), materialization category, provenance, source span, and stable identity.

M2 exposure sources are deliberately bounded:

- a typed Concept IR member such as `BracketConcept.Bounds` or `BracketConcept.MountPoints`;
- the history-known box top plane such as `Base.Top`;
- a semantic hole center such as `LeftMount.Center` when its source is a resolved Concept point.

Conformance checks the exposed surface only. It reports missing and unknown members, type mismatches, duplicate exposure names, invalid materialized references, unrepresentable semantic exposure, and circular exposure dependencies. `Concept Struct` remains compile-time-only and is checked against its directly resolved members; `Struct` and `Model` remain materialized and are checked against `Expose`. No Forge execution is required for either language-level check.

## Provenance and reports

Build JSON exposes the authoritative chain in two places:

```text
conceptIr.resolvedValues Point3
  -> conceptIr.bindings Bracket.LeftMount.Center
  -> features[].resolvedPoint3 / centerSource / centerStableId / pointOrdinal
  -> AirHoleFeature placement provenance
  -> AirHoleCompositeMaterializer
```

`conceptIr.materializedStruct` reports `satisfies`, `conformance`, and typed `exposedMembers`. Concept Struct instances remain marked `materialized: false` and `CompileTimeOnlyErased`; the build document contains only the materialized box solid.

## Geometry proof

[`docs/development/milestones/firmament/demo-sources/concept-materialization-m2.firmament`](../../../../fixtures/Regression/DemoRegression/concept-materialization-m2.firmament) resolves points `(-30,0,25)` and `(30,0,25)`, creates two 8.5 mm through holes, exports AP242, and reimports it. Concept-driven boxes preserve the Concept `Box3` frame (`Z=0..height`) rather than the legacy centered-Z semantic-hole frame. Independent `aetheris analyze` evidence is one body and one shell, `enclosed-manifold`, six planes, two cylinders, and materialized bounds `[-40,-25,0]..[40,25,25]`. Cylinder axes are `+Z`, top circular centers match the resolved Point3 coordinates, radii are `4.25`, and exact volume is `97162.74913472672 mm^3` (`80*50*25 - 2*pi*4.25^2*25`).

The M1 AIR top-boundary chamfer demo remains separate. The current production chamfer route admits exactly one edge finish and no semantic holes; combining it with holes fails with `air-chamfer-production-route-requires-one-box-and-one-edge-finish` rather than dropping a feature.

## Limits and next step

M2 does not infer Concept members from BRep topology, expose BRep IDs, implement arbitrary projection, broaden hole orientations, or add a constraint solver. It does not unify the current independent modification materializers.

The recommended next step is a typed evidence/matching layer that compares a `ConceptIrSemanticMember` or exposed materialized member against STEP-derived analytic evidence (plane, cylinder, bounds, point/axis and tolerance) while keeping STEP entity IDs out of the language Concept contract.
