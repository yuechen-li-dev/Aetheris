# AIR-FIRMAMENT-A2.3 — Firmament V2 DFM templates, concepts, and PMI doctrine

## 1. Purpose

A2.3 defines Firmament V2's manufacturing/process doctrine before parser work. Firmament V2 is a typed declarative record language for CAD construction intent and manufacturable design intent, so manufacturing constraints should be source-level typed records and concepts rather than hidden spreadsheets or tribal-knowledge tables.

This milestone covers DFM templates, typed concepts, PMI as the umbrella product/manufacturing information term, the relationship to `with`, the future relationship to `where`, admissibility, later STEP/PMI lowering, and explicit non-goals. It is design, documentation, and metadata-fixture work only.

A2.3 does not implement a full V2 parser, backend behavior, STEP PMI export, GD&T geometry checks, material/FEA behavior, new geometry features, corpus migration, or production route replacement.

## 2. Terminology

```text
template<Process>:
  a manufacturing/process context, written with C++-style generic-looking syntax on purpose,
  but not C++ metaprogramming.

concept:
  a named typed manufacturing/admissibility fact used by DFM checks, feature admissibility, or later lowering policy.

PMI:
  Product and Manufacturing Information attached to source-level semantic geometry/features/materials.
  PMI includes GD&T, material, surface finish, process notes, inspection notes, and related manufacturing annotations.
```

PMI is not just GD&T. GD&T is one category inside PMI alongside material, surface finish, inspection notes, manufacturing notes, datum/tolerance-related annotations, and feature-linked manufacturing metadata.

## 3. Firmament template doctrine

Firmament V2 uses `template<Process>` syntax for manufacturing templates.

Templates are typed DFM/admissibility contexts. They are not C++-style metaprogramming. They do not generate arbitrary source code. They do not introduce loops, conditionals, macros, or templates-as-computation. They do not mutate geometry. They do not bypass AIR lowering.

```firmament
template<CNC> ShopDefault {
    concept minimumToolRadius: 0.1 mm
    concept minimumWallThickness: 0.8 mm
    concept minimumHoleDiameter: 0.8 mm
}
```

## 4. Why `template<Process>` syntax is allowed

Firmament V2 intentionally uses `template<Process>` syntax for manufacturing process templates. This borrows the familiar generic-looking shape while giving it a narrower and safer meaning: manufacturing context, not code generation.

```text
C++ template:
  compile-time metaprogramming / type-level code generation.

Firmament template:
  typed manufacturing/process context for admissibility and PMI.
```

The syntax is admitted as a source-language aesthetic, not as permission for arbitrary compile-time execution.

## 5. Template declaration and use

The following are long-term design examples, not parser promises.

Reusable template declaration:

```firmament
template<CNC> ShopDefault {
    concept minimumToolRadius: 0.1 mm
    concept minimumWallThickness: 0.8 mm
    concept preferredInsideCorner: Fillet
}
```

Model application:

```firmament
model Bracket {
    units mm

    use ShopDefault

    solid base: Box {
        size: [80, 40, 10]
    }
}
```

Alternative inline template context:

```firmament
model Bracket {
    units mm

    template<CNC> {
        concept minimumToolRadius: 0.1 mm
        concept minimumWallThickness: 0.8 mm
    }

    solid base: Box {
        size: [80, 40, 10]
    }
}
```

Model-level template style, design-only:

```firmament
model Bracket template<CNC> {
    units mm

    concept minimumToolRadius: 0.1 mm
    concept minimumWallThickness: 0.8 mm

    solid base: Box {
        size: [80, 40, 10]
    }
}
```

## 6. Concept doctrine

Concepts are typed named facts.

```firmament
concept minimumToolRadius: 0.1 mm
concept minimumWallThickness: 0.8 mm
concept maximumAspectRatio: 4
concept minimumDraftAngle: 2 deg
concept preferredInsideCorner: Fillet
concept allowedMaterials: [Aluminum6061, Steel1018]
```

Rules:

- concepts are typed;
- concepts are unit-aware;
- concepts are source-level manufacturing facts;
- concepts may participate in feature admissibility diagnostics;
- concepts do not construct geometry by themselves.

Suggested diagnostics:

- `firmament-concept-type-mismatch`
- `firmament-concept-unit-mismatch`
- `firmament-concept-unknown`
- `firmament-concept-duplicate`
- `firmament-template-process-unknown`
- `firmament-template-concept-missing`
- `firmament-dfm-constraint-violation`

## 7. DFM admissibility examples

CNC example, design-only:

```firmament
template<CNC> ShopDefault {
    concept minimumToolRadius: 0.1 mm
}

model PocketExample {
    units mm

    use ShopDefault

    solid base: Box {
        size: [80, 40, 10]
    }

    modify base {
        feature pocket: Pocket {
            on: face(+Z)
            depth: 4
            cornerRadius: 0.05
        }
    }
}
```

Expected future diagnostic:

```text
firmament-dfm-minimum-tool-radius-violation
```

because `cornerRadius < minimumToolRadius`.

FDM example, design-only:

```firmament
template<FDM> PrinterDefault {
    concept nozzleDiameter: 0.4 mm
    concept minimumWallThickness: 0.8 mm
    concept maximumUnsupportedOverhang: 45 deg
    concept layerHeight: 0.2 mm
}
```

Sheet metal example, design-only:

```firmament
template<SheetMetal> ShopDefault {
    concept thickness: 1.5 mm
    concept minimumBendRadius: 1.5 mm
    concept kFactor: 0.42
}
```

## 8. `with` and templates

Templates/configs can be derived with `with`. Canonical design-only example:

```firmament
template<CNC> ShopDefault {
    concept minimumToolRadius: 0.1 mm
    concept minimumWallThickness: 0.8 mm
}

template<CNC> RoughingOnly = ShopDefault with {
    concept minimumToolRadius: 0.5 mm
}
```

Doctrine:

- `with` remains immutable record derivation;
- derived templates/concepts are revalidated;
- derived templates do not mutate the original template.

## 9. PMI doctrine

```text
PMI is source-level product/manufacturing information attached to semantic model entities.

PMI includes:
  GD&T;
  datum definitions;
  tolerances;
  material assignments/specifications;
  surface finish;
  heat treatment/process notes;
  inspection notes;
  manufacturing notes;
  feature-level semantic annotations.
```

PMI is not geometry construction. PMI may affect admissibility, manufacturing validation, inspection, STEP export later, and documentation/artifacts.

## 10. PMI examples

The following are design examples, not parser promises.

GD&T-style / datum example:

```firmament
pmi BracketInspection {
    datum A: base.face(+Z)
    datum B: base.face(+X)

    flatness base.face(+Z) {
        tolerance: 0.05 mm
    }

    position sideHole.axis {
        tolerance: 0.10 mm
        datums: [A, B]
    }
}
```

Material PMI example:

```firmament
material aluminum: Isotropic {
    youngs: 69 GPa
    poisson: 0.33
}

pmi MaterialSpec {
    assign aluminum to base

    note {
        text: "Material: Aluminum 6061-T6"
    }
}
```

Surface finish PMI example:

```firmament
pmi FinishSpec {
    surfaceFinish base.face(+Z) {
        roughnessRa: 1.6 um
    }
}
```

Manufacturing note example:

```firmament
pmi ManufacturingNotes {
    note {
        target: sideHole.wallFace
        text: "Deburr bore after machining."
    }
}
```

## 11. Relationship between concepts and PMI

```text
concept:
  typed process/admissibility fact in a template.

PMI:
  product/manufacturing annotation or requirement attached to model entities.

A concept may constrain whether geometry is manufacturable.
PMI may record the manufacturing/inspection requirement that should later appear in artifacts.
```

For example, `concept minimumToolRadius: 0.1 mm` constrains pocket/chamfer admissibility, while `surfaceFinish face(+Z) { roughnessRa: 1.6 um }` records a manufacturing requirement.

## 12. Relationship to `where`

```text
where:
  local record/config constraints.

template<Process> + concept:
  manufacturing/process context and DFM constraints.

PMI:
  product/manufacturing annotation/tolerance information.
```

No `where` implementation, constraint solver, or GD&T checker is introduced in A2.3.

## 13. Relationship to AIR/BRep/STEP/CIR

```text
Firmament V2 template/concept/PMI source
  -> semantic manufacturing intent
  -> Feature AIR / PMI AIR later
  -> BRepPlan/BRep role attachment where applicable
  -> STEP PMI export later if admitted
```

There is no STEP PMI export in A2.3. There are no BRep topology behavior changes. CIR remains analysis-only and not topology authority. PMI may later attach to semantic roles and BRep entities, but Firmament source should reference semantic selectors/feature outputs, not backend IDs.

## 14. Non-spreadsheet doctrine

```text
Firmament configs are records, not spreadsheets.
Manufacturing templates are source records, not hidden Excel tables.
External spreadsheets/CSV may be imported/exported by tooling later, but they are not the canonical language semantics.
```

Examples:

- customer/product variants use records + `with`;
- manufacturing constraints use `template<Process>` + `concept`;
- PMI uses `pmi` records.

## 15. Pilot metadata/design fixtures

A2.3 adds metadata-only Firmament V2 fixtures under `fixtures/FirmamentV2/Templates/` and `fixtures/FirmamentV2/PMI/`. Because no V2 parser exists, valid examples are classified as future design / parser-not-ready / not-implemented by metadata, invalid examples report metadata diagnostics, and none are sent through the V1 parser as random parse failures.

Valid future examples use `firmament-v2-parser-not-ready`. Invalid examples use diagnostics such as `firmament-concept-unit-mismatch`, `firmament-template-process-unknown`, and `firmament-raw-backend-id-reference-forbidden`.

## 16. Tests

Focused tests should discover A2.3 template and PMI fixtures, assert `syntax-version: FirmamentV2`, assert metadata classification, assert invalid fixture expected diagnostics, assert V2 template/concept/PMI fixtures are not V1 parse failures, verify this doctrine mentions templates/concepts/PMI and non-goals, and keep existing V1 box and side-hole golden path fixtures green.

## 17. Docs updates

A2.3 is referenced from the A2, A2.1, A2.2, A1 fixture-corpus, and AIR-A0 constitution docs as source/semantic doctrine. The references do not imply parser, backend, STEP PMI export, material/FEA, or geometry implementation.

## 18. Non-goals

A2.3 explicitly preserves: no parser implementation; no backend implementation; no geometry implementation; no STEP PMI export; no GD&T checker; no material/FEA behavior; no constraint solver; no source evaluator; no conditionals; no loops; no functions; no macros; no shell implementation; no fillet implementation; no surfacing implementation; no pattern implementation; no broad Boolean; no CIR topology authority; no STEP behavior change; no BRep topology behavior change; no Firmasm redesign; no corpus migration; no production route replacement; no general side-hole support; no arbitrary face/axis support; no production Boolean invocation; no production analyzer/map behavior changes; no CIR evaluator/tape behavior changes; no route-selection/JudgmentUtility production behavior changes; no AirEdgeSweep behavior changes; no BrepBoundedChamfer/BrepBoundedFillet behavior changes; no triangle migration; and no NURBS/freeform implementation.
