# AIR-FIRMAMENT-A2.1 — Semantic references, admissibility, and surface doctrine

A2.3 extends this admissibility doctrine with manufacturing/process contexts: `template<Process>` supplies typed DFM concepts, and PMI records attach product/manufacturing annotations to semantic selectors and feature outputs rather than backend IDs. See `docs/development/milestones/general/air-firmament-a2-3-dfm-templates-concepts-pmi.md`.

## 1. Purpose

A2.1 defines the Firmament V2 semantic contract before parser implementation. It closes the source-language design gaps left after A2 by specifying source-level references, exposure/binding, compile-time admissibility, degeneracy rejection, ruled/sweep/offset-first surface modeling, limited spline/NURBS admission, and feature-output roles.

This is a design/audit milestone. It does not implement a full V2 parser, new geometry features, backend behavior, corpus migration, or production route replacement.

## 2. Firmament V2 identity

```text
Firmament V2 is a typed declarative record language for CAD construction intent.

It is intentionally not Turing-complete.
It has no loops, no conditionals, no user-defined functions, and no arbitrary computation.

All repetition, branching, and construction strategy belong to explicit records, AIR lowering, compiler passes, or later admitted feature families — not source-language control flow.
```

Firmament V2 expresses named construction objects, typed records, semantic selectors, feature operations, scoped regions, materials, patterns as data, admissibility constraints, and lowering expectations.

## 3. Semantic subobject references

Initial source-level reference types are:

```text
FaceRef
EdgeRef
VertexRef
LoopRef
ProfileRef
RegionRef
FeatureRef
FeatureOutputRef
MaterialRef
```

These are source-level semantic references, not BRep IDs. Firmament V2 explicitly forbids raw BRep face IDs, raw edge IDs, coedge IDs, STEP entity IDs, backend-generated GUIDs as source references, and direct references to incidental topology ordering.

```text
Firmament V2 references authoring-level meaning.
BRepPlan/BRep may later materialize those references as topology roles/entities.
Firmament source must not depend on backend emission IDs.
```

## 4. Selector expression model

Initial selector examples include:

```firmament
base.face(+X)
base.face(-Z)
base.face(+Z).outerLoop
base.edge(+X, +Z)
base.vertex(-X, -Y, +Z)
profile.outerLoop
profile.segment("left")
feature.entryLoop
feature.exitLoop
feature.wallFace
feature.createdFaces
feature.affectedFaces
```

Selectors are typed. They resolve against the semantic body state at the relevant source point. They may lower to AIR selectors and BRepPlan roles. Selector ambiguity, unresolved selection, or selection that depends on incidental backend topology ordering is a compile error or a deliberate bounded blocker.

## 5. Fat-arrow exposure syntax

`=>` is reserved for semantic exposure/binding only.

```text
`=>` binds a selector or feature-output role to a source-level alias.

It does not mean mutation, control flow, lowering, Boolean composition, or execution order.
```

```firmament
solid base = box {
    size: [10, 8, 6]

    expose {
        face(+Z) => top
        face(+Z).outerLoop => topRim
        face(+X) => right
    }
}
```

Feature outputs may also be exposed:

```firmament
modify base {
    feature sideHole: Cut {
        on: face(+X)

        tool: Cylinder {
            radius: 1
            through: face(-X)
        }

        expose {
            entryLoop => inlet
            exitLoop => outlet
            wallFace => bore
        }
    }

    feature entryChamfer: Chamfer {
        target: sideHole.inlet
        distance: 0.2
    }
}
```

`sideHole.inlet` is a `FeatureOutputRef` alias. The canonical feature-output role remains `entryLoop`; the alias is source-level convenience and traceability.

## 6. Binding and versioning rules

```text
A modify block is source-order scoped.
Selectors inside a modify block resolve against the current semantic body state at that point.
Internally, the compiler may model body versions, but source syntax does not expose body version numbers by default.
Features may expose role outputs for later features.
```

```firmament
modify base {
    feature sideHole: Cut { ... }

    feature inletChamfer: Chamfer {
        target: sideHole.entryLoop
        distance: 0.2
    }
}
```

Downstream features should use feature outputs when referencing geometry created by prior features. This keeps the source tied to authoring meaning rather than later topology emission order.

## 7. Degenerate geometry as compile errors

```text
Firmament V2 rejects source that necessarily creates degenerate geometry in the admitted lowering route.
```

Compile errors include zero dimension box, negative radius cylinder, zero-height extrude, deterministically detectable self-intersecting profile, shell thickness that collapses a body, fillet radius exceeding admitted local clearance, chamfer distance collapsing selected geometry, ruled transition between incompatible sections without an admitted bridge rule, ambiguous selector, and impossible through target.

Suggested diagnostics:

```text
firmament-degenerate-dimension
firmament-negative-radius
firmament-zero-height-extrude
firmament-profile-self-intersection
firmament-shell-thickness-collapses-body
firmament-fillet-radius-exceeds-local-clearance
firmament-chamfer-distance-collapses-selection
firmament-ruled-sections-incompatible
firmament-selector-ambiguous
firmament-selector-unresolved
firmament-feature-not-admitted
firmament-feature-not-implemented
firmament-raw-backend-id-reference-forbidden
```

If the compiler cannot prove degeneracy at the source/semantic stage, later AIR/BRepPlan/BRep stages may return a bounded blocker. The compiler must not fake certainty.

## 8. Feature admissibility contracts

Every feature family should own an admissibility contract.

- Box: size components > 0.
- Cylinder: radius > 0; height > 0 for finite cylinders; explicit axis nonzero.
- Extrude: profile closed; profile non-self-intersecting when deterministically checked; height > 0; direction nonzero.
- RuledTransition: source and target sections admitted; sections compatible or bridge policy explicit; zero-area transition rejected.
- Shell: thickness > 0; direction admitted; removed face resolvable if required; inward shell must not collapse admitted body; offset self-intersection rejected or blocked.
- Chamfer: distance > 0; target selection admitted; distance must not collapse target under admitted local route; arbitrary graph rejected unless supported.
- Fillet: radius > 0; target selection admitted; radius must fit local clearance under admitted route; variable/mixed radii deferred unless supported.
- Pattern: seed feature exists; count > 0; spacing/direction admitted; pattern collisions may be compile errors or blockers if deterministically detected.
- Material: material properties have correct dimensions; assignment target must be resolvable; material field independence preserved.

## 9. Sweep / ruled / offset-first surface doctrine

Firmament V2 should prefer profiles, sweeps, ruled transitions, section transitions, analytic ruled surfaces, surface offsets, and normal/offset detail fields over raw primitive CSG-first authoring and spline/NURBS-first surfacing.

```text
Firmament V2 treats sweeps, profile extrusions, ruled transitions, and surface offsets as first-class construction intent.

Spline/NURBS surfaces are admitted detail/adaptation surfaces, not the default representation for ordinary sheet, ruled, or transition-like design intent.
```

Sheet metal and many Class-A-like surfaces should often be modeled as ruled/transition surfaces with optional normal/offset detail, not as one giant spline/NURBS patch. Ruled surfaces are exact construction intent when applicable. NURBS and splines are historical/editability tools and should not become the default ontology.

## 10. Ruled surface examples

These are design examples, not parser promises.

```firmament
solid body: RuledTransition {
    from: profileA at z: 0
    to: profileB at z: 6
}
```

```firmament
surface skin: RuledSurface {
    railA: Line {
        from: [0, 0, 0]
        to: [10, 0, 2]
    }

    railB: Line {
        from: [0, 8, 2]
        to: [10, 8, 0]
    }
}
```

```firmament
surface saddle: HyperbolicParaboloid {
    bounds: Rectangle { size: [10, 8] }
    height: 2
}
```

## 11. Surface offset doctrine

Surface offset is a first-class detailing operation, guarded by admissibility.

```firmament
surface outer: OffsetSurface {
    base: skin
    distance: 0.8
    direction: normal
}
```

```firmament
solid shell: Shell {
    target: base
    thickness: 1
    remove: face(+Z)
    direction: inward
}
```

```firmament
feature emboss: SurfaceOffsetDetail {
    target: base.face(+Z)

    offset: HeightField {
        source: ribPattern
        scale: 0.2
    }
}
```

```text
Surface offsets that self-intersect, collapse thickness, or create ambiguous topology are compile errors when deterministically provable, or bounded lowering blockers otherwise.
```

## 12. Spline/NURBS limited admission

```text
Spline/NURBS surfaces are not the default shape model for Firmament V2.

They may be admitted for:
  localized detail;
  imported compatibility;
  human-authored freeform styling where no ruled/analytic construction is available;
  normal/offset correction fields;
  bounded surfacing features.

They should not displace ruled/sweep/offset construction as the primary representation.
```

Future design examples, not parser promises:

```firmament
surface detail: SplineSurface {
    usage: detail
    control: ...
}
```

```firmament
surface styled: SplineOffset {
    base: ruledSkin
    normalOffset: detailMap
}
```

## 13. Pattern-as-record doctrine

Firmament V2 has no loops. Patterning is explicit data:

```firmament
feature holes: LinearPattern {
    seed: sideHole
    count: 6
    spacing: 10
    direction: +Y
}
```

Not:

```text
for i in 0..6 { ... }
```

Patterns are feature records. Repetition is declarative. AIR lowering decides expansion/materialization. Pattern collision/admissibility must be checked.

## 14. Variant/configuration-as-record doctrine

Firmament V2 has no conditionals. If variants are needed, represent them as explicit data. This is future design, not parser support:

```firmament
variant size: VariantSet {
    options: {
        small: { width: 10 height: 6 }
        large: { width: 20 height: 12 }
    }

    selected: small
}
```

## 15. Units and quantities

Firmament V2 examples may use inherited units:

```firmament
units mm

solid base: Box {
    size: [10, 8, 6]
}
```

or explicit quantities:

```firmament
solid base: Box {
    size: [10 mm, 8 mm, 6 mm]
}
```

Units are typed quantities internally. Dimensions must match expected fields. Invalid dimension usage is a compile error: `radius: -1 mm` is invalid; `youngs: 120 mm` is invalid; `distance: 0.5` under `units mm` is a valid length; `youngs: 120 GPa` is a valid stress/material quantity.

## 16. Feature-output role vocabulary

Initial canonical semantic roles are:

- Cut / side-hole: `entryLoop`, `exitLoop`, `wallFace`, `affectedFace`, `integrationPatch`, `createdFaces`, `affectedFaces`.
- Chamfer: `chamferFace`, `targetLoop`, `startLoop`, `endLoop`, `affectedEdges`.
- Fillet: `filletFace`, `spine`, `affectedEdges`, `cornerPatch`.
- Shell: `outerFaces`, `innerFaces`, `rimFaces`, `removedFaces`, `innerLoops`, `outerLoops`.
- Ruled transition: `startSection`, `endSection`, `transitionFaces`, `railEdges`.
- Pattern: `seed`, `instances`, `instanceTransforms`.
- Material: `assignedRegion`, `materialField`, `orientationField`, `authorityField`.

These are semantic roles, not raw backend IDs.

## 17. Firmasm note

```text
Firmament V2 is the human source language.
Firmasm remains a normalized machine-facing representation and may stay JSON/record-like.
A2.1 does not redesign Firmasm.
```

## 18. Pilot metadata/design fixtures

A2.1 adds metadata-only design fixtures under `fixtures/` for semantic references, feature-output aliases, forbidden backend IDs, degenerate dimensions, shell collapse, ruled/offset surface examples, and pattern-as-record examples. Until a V2 parser exists, valid future examples are classified as `not-implemented` with `firmament-v2-parser-not-ready`, and invalid examples are classified by metadata diagnostics such as `firmament-degenerate-dimension`, `firmament-shell-thickness-collapses-body`, and `firmament-raw-backend-id-reference-forbidden`. They must not be run through the V1 parser as random parse failures.

## 19. Tests

A2.1 tests should verify that metadata is recognized, V2 semantic-reference fixtures are not V1 parse failures, invalid admissibility fixtures report their expected metadata diagnostics, doctrine text mentions admissibility and ruled/sweep/offset-first policy, and existing V1 box and side-hole fixtures remain valid.

## 20. Docs update

A2.1 extends A2 rather than replacing it. A2 remains the broader V2 source-language audit; this document is the semantic-reference, admissibility, and surface-doctrine addendum. The A1 fixture-corpus doc records the expanded V2 metadata-only taxonomy. The A0 compiler IR constitution records that Firmament V2 rejects provable degenerate construction intent before backend emission when possible.

A2.2 extends this doctrine with `with` record derivation/configuration. `=>` remains the semantic exposure construct; `with` derives record values and must revalidate derived records rather than mutating selectors, backend topology, or existing objects. See `docs/development/milestones/general/air-firmament-a2-2-record-derivation-with.md`.

## 21. Non-goals

A2.1 explicitly preserves: no parser implementation; no backend implementation; no shell implementation; no fillet implementation; no surfacing implementation; no pattern implementation; no material implementation; no broad Boolean; no CIR topology authority; no STEP behavior change; no BRep topology behavior change; no Firmasm redesign; no corpus migration; no production route replacement; no general side-hole support; no arbitrary face/axis support; no production analyzer/map behavior changes; no CIR evaluator/tape behavior changes; no route-selection/JudgmentUtility production behavior changes; no AirEdgeSweep behavior changes; no BrepBoundedChamfer/BrepBoundedFillet behavior changes; no triangle migration; and no NURBS/freeform implementation.

## X3 parser-backed Box exposure note

AIR-FIRMAMENT-X3 advances the V2 named Box face exposure fixture from A2.1 metadata-only doctrine into the isolated Firmament V2 parser/frontend. The supported slice is limited to Box `expose { face(axis) => alias }` and `face(axis).outerLoop => alias` semantic summaries; aliases remain source-level names and not backend topology identifiers.
