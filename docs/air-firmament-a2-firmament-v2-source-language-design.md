# AIR-FIRMAMENT-A2 — Firmament V2 source language design audit

A2.3 adds manufacturing/process doctrine: `template<Process>` records for DFM contexts, typed `concept` facts for admissibility, and PMI records for product/manufacturing annotations with GD&T as one PMI category rather than the umbrella. See `docs/air-firmament-a2-3-dfm-templates-concepts-pmi.md`.

## Purpose

Firmament V2 is the next canonical human-facing Aetheris source language. It is a source language for authoring construction intent: names, units, selections, profiles, regions, feature operations, materials, and traceable feature history. It is not a serialized IR and it is not a backend topology language.

A2 is a design/audit milestone. It defines doctrine, policy, syntax shape, source constructs, lowering expectations, and fixture process. It does not implement a full V2 parser, migrate the corpus, add geometry features, replace production routes, change STEP behavior, change BRep topology behavior, or promote CIR to topology authority.

A2.2 adds `with` as the Firmament V2 record-derivation/configuration doctrine: immutable source records may derive named variants by overriding fields, then revalidating under the same admissibility contracts before AIR lowering. See `docs/air-firmament-a2-2-record-derivation-with.md`.

## Relationship to Firmament V1

```text
Firmament V1:
  legacy TOON/YAML structured syntax
  valid where already supported
  mostly frozen
  useful for existing corpus/interchange

Firmament V2:
  canonical human-facing source language
  record/block-style
  construction-intent-first
  developed through fixtures
```

Firmament V1 / legacy `.firmament` and `.firmfixture` syntax remains valid where already supported. V1 is useful as corpus, interchange, history, and structured regression evidence. V1 should be considered mostly frozen: maintain it, keep supported fixtures green, and avoid aggressive language expansion through V1 unless a narrow maintenance reason exists.

V2 is not a migration-compatibility exercise. Existing V1 files are not invalidated and do not need immediate migration. Aetheris should not promise broad backward compatibility beyond existing V1 support. V2 should be designed as the natural source-language shape for the corrected Aetheris V2 compiler architecture.

## Why V2 exists

Aetheris originally leaned toward a CSG / 3D primitive-first model. Development corrected that assumption. The architecture is now sweep/profile/ruled/region/construction-first, with AIR generating topology intent and BRep owning explicit topology/export authority.

Firmament V2 exists because the canonical human syntax should express authoring intent directly instead of inheriting a YAML/TOON interchange shape or encouraging raw Boolean trees as the primary mental model.

## Corrected architecture lessons

The architectural doctrine for V2 is:

- AIR is the topology-generating compiler IR.
- BRepPlan plans explicit topology emission.
- BRep is explicit topology and STEP/export authority.
- STEP is an emitted artifact, not construction truth.
- CIR/FRep is a lower-level computational implicit mirror for evaluation/analysis, effectively analysis-only with respect to topology, not topology authority.
- CIR-to-BRep as the default path is decompilation/materialization, not normal compilation.
- Boolean is an admitted route/backend when selected, not the core language model.
- Aetheris V2 should author sweeps, profiles, ruled transitions, regions, construction operations, and materials before raw primitive CSG expressions.

## Canonical syntax principles

Firmament V2 should follow these principles:

- Source files are construction programs, not serialized IR.
- Objects have names.
- Units are explicit.
- Features are named or traceable.
- Selections are explicit and semantic.
- Regions are scoped construction islands.
- Feature operations express intent.
- Lowering stages are explicit through trace and diagnostics.
- Syntax should prefer clarity over token terseness.
- V2 may use record/block-style constructors.
- V2 should avoid raw Boolean-tree-first authoring.
- V2 should not expose BRepPlan/BRep topology mechanics directly.
- V2 should not expose CIR as topology authority.

## Source-language model

Core source constructs:

- `model` — top-level construction program.
- `units` — explicit model unit declaration.
- `solid` — named solid-producing construction result.
- `profile` — named 2D profile or section.
- `material` — named material definition.
- `feature` — traceable source feature when an operation needs an explicit feature identity.
- `modify` — scoped modification block for an existing named solid.
- `region` — scoped construction island on/within a selection.

Initial constructors:

- `box`
- `cylinder`
- `sphere`
- `polygon`
- `rectangle`
- `extrude`
- `ruled`
- `shell`

Initial operations:

- `cut`
- `add`
- `chamfer`
- `fillet`
- `assign`

Initial selectors:

- `face(+X)`
- `face(-X)`
- `face(+Z).outerLoop`
- `edge(...)`
- future named selectors such as `edge topFront` or `face mountingFace` once binding rules are designed.

## Proposed canonical syntax shape

These examples are design examples, not parser promises unless a later milestone explicitly marks them implemented.

### A. Box primitive / basic solid

```firmament
model BoxExample {
    units mm

    solid base = box {
        size: [10, 8, 6]
    }
}
```

### B. Profile extrusion

```firmament
model ProfileExtrudeExample {
    units mm

    profile section = polygon {
        points: [
            [0, 0],
            [10, 0],
            [10, 6],
            [0, 6]
        ]
    }

    solid body = extrude section {
        height: 4
        direction: +Z
    }
}
```

### C. Ruled / section transition

```firmament
model RuledTransitionExample {
    units mm

    profile bottom = rectangle { size: [10, 8] }
    profile top = rectangle { size: [8, 6] }

    solid body = ruled {
        from: bottom at z: 0
        to: top at z: 4
    }
}
```

### D. Side-hole region golden path

```firmament
model SideHoleExample {
    units mm

    solid base = box {
        size: [10, 8, 6]
    }

    modify base {
        region sideHole on face(+X) {
            cut cylinder {
                radius: 1
                through: face(-X)
            }
        }
    }
}
```

### E. Chamfer

```firmament
model ChamferExample {
    units mm

    solid base = box {
        size: [10, 8, 6]
    }

    modify base {
        chamfer face(+Z).outerLoop {
            distance: 0.5
        }
    }
}
```

### F. Fillet future design

```firmament
model FilletExample {
    units mm

    solid base = box {
        size: [10, 8, 6]
    }

    modify base {
        fillet edge(+X, +Z) {
            radius: 0.5
        }
    }
}
```

### G. Shell future design

```firmament
model OpenTopShellExample {
    units mm

    solid base = box {
        size: [10, 8, 6]
    }

    solid shell = shell base {
        thickness: 1
        remove: face(+Z)
        direction: inward
    }
}
```

### H. Material future design

```firmament
model MaterialExample {
    units mm

    solid plate = box {
        size: [10, 8, 1]
    }

    material carbon = orthotropic {
        e1: 120 GPa
        e2: 8 GPa
        direction: [1, 0, 0]
    }

    assign carbon to plate
}
```

## Lowering model

Firmament V2 source should lower as:

```text
FirmamentV2Source
  -> FirmamentAst
  -> semantic intent
  -> Feature AIR
  -> Constructive / Compositional AIR
  -> BRepPlan
  -> BRep
  -> STEP/artifacts
```

Optional CIR mirrors are admitted from AIR when a mirror exists and declares its losses:

```text
Feature AIR / Constructive AIR
  -> CIR mirror, if available
```

CIR is not the topology path. V2 source should not lower through CIR as the default route to recover BRep topology.

## Fixture and corpus process

V2 language design should be fixture-driven, compiler-style. Future features should be written in V2 syntax first, marked with explicit implementation state, then advanced through parse, semantic intent, Feature AIR, Constructive/Compositional AIR, BRepPlan, BRep, STEP/artifacts, and trace diagnostics in later milestones.

V2 design fixtures may live under:

```text
fixtures/FirmamentV2/
  Primitive/
  Profile/
  Prism/
  Region/
  Chamfer/
  Fillet/
  Shell/
  Surfacing/
  Material/
  Invalid/
```

V2 fixtures may be classified as:

- `implemented`
- `not-implemented`
- `future-design`
- `invalid`

A2 pilot fixtures are metadata/design fixtures. They must not be treated as random V1 parser failures. Until a V2 parser exists, they should classify as parser-not-ready / future-design / not-implemented / invalid by metadata contract.

Existing V1 fixtures remain valid and may remain in their current locations.

## Initial feature families

Initial V2 design families are:

- primitive/basic solids for continuity and smoke tests;
- profile and prism constructions;
- ruled and section-transition constructions;
- regions for local construction islands such as controlled side holes;
- chamfer as bounded currently explored feature intent;
- fillet, shell, surfacing, and materials as future design intent;
- invalid fixtures for source-contract diagnostics such as missing units.

## Non-goals

A2 explicitly does not introduce:

- full V2 parser;
- corpus migration;
- broad feature implementation;
- new BRep behavior;
- new STEP behavior;
- CIR topology authority;
- Boolean-first language model;
- production route replacement;
- general side-hole support;
- arbitrary face/axis support;
- shell implementation;
- fillet implementation;
- surfacing implementation;
- broad Boolean backend admission;
- production Boolean invocation;
- STEP exporter/importer behavior changes;
- BRep topology behavior changes;
- production analyzer/map behavior changes;
- CIR evaluator/tape behavior changes;
- route-selection/JudgmentUtility production behavior changes;
- AirEdgeSweep behavior changes;
- BrepBoundedChamfer/BrepBoundedFillet behavior changes;
- triangle migration;
- NURBS/freeform expansion.

## Next milestones

Recommended next milestones:

1. Define a tiny V2 lexer/parser spike for model/units/basic named constructors without geometry expansion.
2. Add source diagnostics for missing units, duplicate names, and unsupported V2 constructs.
3. Lower one V2 parser-backed box fixture to existing Feature AIR without changing backend topology.
4. Lower one profile extrusion fixture through Feature AIR and existing Constructive AIR/profile emission evidence.
5. Promote side-hole V2 region syntax from design fixture to semantic intent only, then to AIR Region only, before any materialization expansion.
6. Keep fillet, shell, surfacing, and material fixtures as future-design until their compiler stages are separately admitted.

## A2.1 semantic contract addendum

AIR-FIRMAMENT-A2.1 extends this source-language design with the semantic-reference and admissibility contract that must be settled before parser work begins. See `docs/air-firmament-a2-1-semantic-references-admissibility-surface-doctrine.md` for the typed source-level reference model, the reserved `=>` exposure/binding syntax, compile-time degeneracy/admissibility doctrine, ruled/sweep/offset-first surface policy, limited-admission spline/NURBS policy, pattern-as-record/no-control-flow doctrine, and feature-output role vocabulary. A2.1 remains design/audit only: no full V2 parser, backend behavior change, corpus migration, or geometry feature implementation is introduced.
