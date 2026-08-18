# AIR-FIRMAMENT-A2.2 — Firmament V2 record derivation with `with`

A2.3 applies the same immutable `with` doctrine to manufacturing templates/configs: derived `template<Process>` records may override typed `concept` facts, are revalidated, and do not mutate the original template. See `docs/development/milestones/general/air-firmament-a2-3-dfm-templates-concepts-pmi.md`.

## 1. Purpose

A2.2 defines `with` as Firmament V2's record derivation and configuration mechanism. Firmament V2 is a typed declarative record language, so configuration should remain data-shaped: named records are authored once, then immutable derived records express variants by overriding selected fields.

This doctrine covers immutable record derivation, model variants, feature variants, material variants, configuration records, admissibility revalidation, and the forbidden meanings that `with` must never acquire. `with` is admitted because it preserves the language's record/object nature while giving users a clean way to define variants without conditionals, loops, macros, arbitrary logic, mutation, topology editing, or backend patching.

A2.2 is design, documentation, and metadata-fixture work only. It does not implement a full V2 parser, backend behavior, geometry features, source evaluation, or corpus migration.

## 2. Firmament V2 identity recap

```text
Firmament V2 is a typed declarative record language for CAD construction intent.

It has no loops, no conditionals, no user-defined functions, no arbitrary logic, and no backend topology IDs.

`with` provides configuration/variant ergonomics without adding general control flow.
```

Firmament V2 remains a source language for construction intent. Feature AIR, Constructive/Compositional AIR, BRepPlan, BRep, and STEP remain downstream compiler/backend stages rather than source-level editing targets.

## 3. `with` doctrine

```text
`with` creates a new source-level record value by copying an existing record and overriding selected fields.

It is immutable source-level record derivation.
It is not mutation.
It is not topology editing.
It is not control flow.
It is not backend patching.
It does not operate on BRep entities.
```

Example:

```firmament
solid base = Box {
    size: [10, 8, 6]
}

solid tall = base with {
    size: [10, 8, 12]
}
```

Meaning:

```text
`tall` is a new Box-derived source record with a different size.
`base` is unchanged.
Both records must satisfy Box admissibility.
```

## 4. Revalidation / admissibility

Derived records must be revalidated under the same admissibility contracts as directly-authored records. Copying a valid source record does not make every override valid; the derived record must still satisfy constructor, field-type, geometric, semantic-reference, and feature admissibility rules before lowering.

```firmament
solid base = Box {
    size: [10, 8, 6]
}

solid bad = base with {
    size: [10, 0, 6]
}
```

Expected diagnostic:

```text
firmament-degenerate-dimension
```

Nested feature derivation is also revalidated:

```firmament
feature hole = Cut {
    on: face(+X)

    tool: Cylinder {
        radius: 1
        through: face(-X)
    }
}

feature badHole = hole with {
    tool: hole.tool with {
        radius: -2
    }
}
```

Expected diagnostic:

```text
firmament-negative-radius
```

A compiler may report a wrapper diagnostic such as `firmament-with-derived-record-invalid`, but it must preserve the specific admissibility cause.

## 5. Valid uses of `with`

### A. Solid variants

```firmament
solid base = Box {
    size: [10, 8, 6]
}

solid small = base with {
    size: [8, 6, 4]
}

solid large = base with {
    size: [20, 16, 12]
}
```

### B. Feature variants

```firmament
feature sideHole = Cut {
    on: face(+X)

    tool: Cylinder {
        radius: 1
        through: face(-X)
    }
}

feature largeSideHole = sideHole with {
    tool: sideHole.tool with {
        radius: 2
    }
}
```

### C. Material variants

```firmament
material steel = Isotropic {
    youngs: 200 GPa
    poisson: 0.30
    density: 7850 kg/m^3
}

material softSteel = steel with {
    youngs: 180 GPa
}
```

### D. Pattern variants

```firmament
feature ribs = LinearPattern {
    seed: rib
    count: 6
    spacing: 10
    direction: +Y
}

feature denseRibs = ribs with {
    count: 12
    spacing: 5
}
```

Pattern examples remain doctrine/design unless a later milestone implements pattern parsing and lowering.

### E. Configuration records

```firmament
config defaultBox = BoxConfig {
    size: [10, 8, 6]
    holeRadius: 1
}

config largeBox = defaultBox with {
    size: [20, 16, 12]
    holeRadius: 2
}
```

`config` is design-level/future doctrine in A2.2. It records how configuration should stay declarative if admitted later; it is not parser-backed here.

## 6. Invalid uses of `with`

`with` requires a source-level record value. It is explicitly forbidden on semantic references and selectors:

```firmament
select topFace = base.face(+Z)

solid bad = topFace with {
    ...
}
```

Expected diagnostic:

```text
firmament-with-requires-record
```

Also forbidden:

- `with` on raw BRep IDs;
- `with` on STEP entity IDs;
- `with` on coedges/backend topology;
- `with` to mutate an existing object in place;
- `with` to bypass feature admissibility.

Suggested diagnostics:

- `firmament-with-requires-record`
- `firmament-with-field-not-found`
- `firmament-with-field-type-mismatch`
- `firmament-with-cannot-target-selector`
- `firmament-with-cannot-target-backend-id`
- `firmament-with-derived-record-invalid`

## 7. Nested `with`

Nested derivation is valid when every target is a source-level record value:

```firmament
feature largerHole = sideHole with {
    tool: sideHole.tool with {
        radius: 2
    }
}
```

Rules:

- inner derivation is evaluated as source record derivation;
- outer derived record receives the derived field value;
- all derived records are revalidated;
- no mutation occurs.

## 8. `with` and identity

```text
A `with` expression creates a new source object identity when bound to a new name.
The original object remains unchanged.
Derived objects may share provenance with their source record for trace/debugging.
```

Example:

```firmament
solid base = Box { size: [10, 8, 6] }
solid tall = base with { size: [10, 8, 12] }
```

`base` and `tall` are distinct source solids. The compiler may retain provenance that `tall` was derived from `base`, but that provenance is debug/trace metadata, not source mutation or shared backend topology identity.

## 9. `with` and source ordering

A `with` source must refer to a previously defined record in the current scope. Forward references are not part of A2.2 doctrine unless later admitted. Duplicate names remain invalid.

Suggested diagnostics:

- `firmament-name-unresolved`
- `firmament-duplicate-name`

## 10. `with` and no-control-flow doctrine

`with` replaces common configuration use cases that would otherwise push users toward conditionals.

Bad:

```text
if variant == large { ... }
```

Good:

```firmament
config large = default with {
    size: [20, 16, 12]
}
```

Variants are data. Patterns are data. Record derivation is data. No runtime branching, looping, macro expansion, or arbitrary execution is introduced.

## 11. Interaction with `=>`

`=>` binds semantic selectors and feature roles to names. `with` derives record values. They are separate constructs.

Valid:

```firmament
solid base = Box {
    size: [10, 8, 6]

    expose {
        face(+Z) => top
    }
}

solid tall = base with {
    size: [10, 8, 12]
}
```

Whether exposed aliases are copied into derived records by default is a design question, not implemented doctrine in A2.2. Conservative doctrine for now:

- record fields are copied by `with`;
- semantic exposure aliases are copied only if they remain valid under the derived record and the feature family admits that behavior;
- otherwise, alias copying may be blocked or re-resolved;
- A2.2 does not implement alias-copying behavior.

## 12. Lowering model

```text
Firmament V2 source
  -> parse
  -> resolve names / `with` derivation
  -> validate derived records
  -> semantic intent
  -> Feature AIR
  -> Constructive / Compositional AIR
  -> BRepPlan
  -> BRep
  -> STEP/artifacts
```

`with` should be resolved before Feature AIR lowering as source-level record derivation. Downstream stages should see admitted source records and provenance, not an instruction to mutate backend topology.

## 13. Firmasm note

Firmasm remains JSON/record-like and does not need a `with` redesign unless a later machine-level configuration representation is needed. A2.2 does not redesign Firmasm.

## 14. Pilot metadata/design fixtures

A2.2 adds metadata-only Firmament V2 fixtures under `fixtures/RecordDerivation/`. Until a V2 parser exists, valid examples are classified as future design/not implemented with `firmament-v2-parser-not-ready`, and invalid examples are classified by metadata diagnostics. They must not be sent through the V1 parser as random failures.

The pilot fixture set is:

```text
fixtures/RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture
fixtures/RecordDerivation/valid/feature-with-radius-variant-v2.valid.firmfixture
fixtures/RecordDerivation/valid/material-with-property-variant-v2.valid.firmfixture
fixtures/RecordDerivation/invalid/with-degenerate-box-v2.invalid.firmfixture
fixtures/RecordDerivation/invalid/with-selector-target-v2.invalid.firmfixture
fixtures/RecordDerivation/invalid/with-unknown-field-v2.invalid.firmfixture
```

## 15. Tests

A2.2 tests should verify that record-derivation fixtures are discoverable metadata-only Firmament V2 fixtures, that they are classified by metadata instead of as V1 parse failures, that invalid fixtures report expected diagnostics (`firmament-degenerate-dimension`, `firmament-with-requires-record`, `firmament-with-field-not-found`), that doctrine mentions immutable derivation, no mutation, no topology editing, admissibility revalidation, no control flow, nested `with`, and the interaction with `=>`, and that existing V1 box and side-hole fixtures remain valid.

## 16. Docs updates

A2.2 extends A2 and A2.1 rather than replacing them. A2 remains the broad Firmament V2 source-language audit, A2.1 remains the semantic-reference/admissibility/surface-doctrine addendum, and A2.2 is the record-derivation/configuration addendum. The A1 fixture-corpus doc records the expanded `RecordDerivation` V2 fixture taxonomy. The A0 compiler IR constitution records that `with` is source-level record derivation resolved before AIR lowering.

## 17. Non-goals

A2.2 explicitly preserves: no parser implementation; no backend implementation; no geometry implementation; no source evaluator; no conditionals; no loops; no functions; no macros; no shell implementation; no fillet implementation; no surfacing implementation; no pattern implementation; no material implementation; no broad Boolean; no CIR topology authority; no STEP behavior change; no BRep topology behavior change; no Firmasm redesign; no corpus migration; no production route replacement; no general side-hole support; no arbitrary face/axis support; no production analyzer/map behavior changes; no CIR evaluator/tape behavior changes; no route-selection/JudgmentUtility production behavior changes; no AirEdgeSweep behavior changes; no BrepBoundedChamfer/BrepBoundedFillet behavior changes; no triangle migration; and no NURBS/freeform implementation.

## AIR-FIRMAMENT-X2 parser-backed advancement

AIR-FIRMAMENT-X2 promotes the Box size variant from metadata-only design intent to a parser-backed Firmament V2 slice. The supported production subset is deliberately narrow: `solid derived: base with { size: [...] }` may derive only from a previously defined Box solid record in the same model scope. The derived record is resolved immutably, revalidated with Box admissibility, and lowered to Feature AIR `CreateBox`; the base record remains unchanged. General `with`, selectors, feature records, templates, concepts, PMI, `where`, shell, fillet, chamfer, surfacing, pattern, material, FEA, and topology mutation remain unsupported.
