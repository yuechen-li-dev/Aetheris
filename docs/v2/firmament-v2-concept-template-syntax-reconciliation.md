# Firmament V2 concept/template syntax reconciliation

## X3 canonical-root update

Advanced Profile/Compose, semantic Slots, and source-grounded Selections are
now admitted under `Model { Units: mm }`. Their production readers are
adapter-only; the canonical parser records normalized declarations first.
Selection names are unique in the canonical Selection namespace and bind only
to authored Profile/Hole/Slot provenance, never raw topology IDs. Templates,
records, arrays, and general Pattern syntax still retain separate migration
work; see [`../firmament/v2-static-authoring-closure-x3.md`](../firmament/v2-static-authoring-closure-x3.md).

Milestone: **V2-FIRMAMENT-SYNTAX-RECONCILE-A0**

This note is the Phase 1 reference for how `let`, Forge-backed concepts, and future templates relate in Firmament V2. It is intentionally narrow: it reconciles source syntax and fixture doctrine without adding language/runtime behavior.

## Phase 1 boundary

Firmament V2 Phase 1 remains a manufacturing-intent data language over existing STEP/AP242 models. It stays pure data: immutable `let`, typed values, acyclic expression graphs, no loops, no conditionals, no functions, no mutation, no hidden state machines, and no Turing completeness.

Phase 1 is concept-first, not model-generation-template-first. Concepts annotate, validate, and constrain existing geometry/manufacturing intent; templates that generate or suggest structure are deferred.

## What is a `let`?

A `let` is immutable typed manufacturing data. It records values and grouped records that describe source-level manufacturing facts. A `let` has no logic, hidden behavior, mutation, or lifecycle. Concepts and PMI may reference `let` values.

```firmament
let MountingPattern {
    holeDiameter: length = 6.0mm tol 0.05mm
    holeSpacingX: length = 80.0mm tol +0.10mm -0.05mm
    holeCount: int = 4
}
```

Use lowercase primitive types: `int`, `float`, `length`, `angle`, `string`, and `bool`.

## What is a concept?

A concept application is a semantic classification/contract over existing geometry and manufacturing intent. The generic source shape is:

```text
conceptFamily<ConceptName> { ... }
```

The parser recognizes this generic shape. Forge descriptors, not parser branches, define the available concept families and concepts, their required fields, field types, validation rules, DFM checks, and later PMI/report behavior. The parser must not hardcode every concept.

Examples:

```firmament
manufacturing process<CNC> {
    material: Aluminum6061
    minimumToolRadius: 1.5mm
}

feature mountHole: hole<Countersink> {
    target: part.region("mountHoleA")
    diameter: MountingPattern.holeDiameter
    countersinkDiameter: MountingPattern.countersinkDiameter
    angle: 90deg
}
```

In these examples, `process<CNC>` and `hole<Countersink>` are concept-family applications. `process` and `hole` identify the semantic family; `CNC` and `Countersink` identify the family-specific concept. Prefer PascalCase concept names where current Forge descriptors do.

## What is a template?

In Phase 1, prefer the words **concept** or **concept pack** for manufacturing-intent validation. A template is a future specialization that can instantiate or suggest source/geometry from a concept. A concept can validate and annotate existing geometry without generating geometry.

```text
Concept:
  semantic meaning + required fields + validation/DFM/PMI/report obligations.

Template:
  optional future mechanism that can instantiate or suggest source/geometry from a concept.
```

This distinction keeps Phase 1 from drifting back into full modeling or geometry generation.

## Forge descriptor vs parser responsibilities

Parser grammar owns only stable syntax shapes:

- immutable typed `let` values and grouped records;
- dotted references to record fields;
- arithmetic and toleranced typed expressions where implemented;
- generic `conceptFamily<ConceptName>` application blocks;
- `pmi` blocks as manufacturing information records.

Forge descriptors own semantic catalog behavior:

- which families and concepts exist;
- required and optional fields;
- field types and unit expectations;
- validation and rejection reasons;
- DFM checks where implemented;
- future PMI/report obligations.

Do not add hardcoded magic angle tags such as:

```firmament
<cnc> { ... }
<countersink> { ... }
```

Those forms omit the semantic family. Prefer:

```firmament
manufacturing process<CNC> { ... }
feature h1: hole<Countersink> { ... }
```

## PMI keyword doctrine

Use `pmi` as the manufacturing information block keyword. Do not use `gdt` as the authoring keyword. GD&T is a category of PMI, not the Phase 1 block name.

## Illustrative Phase 1 example

The following is canonical illustrative syntax for Phase 1 doctrine. Some lowering/export behavior remains implementation-dependent and may be deferred by current fixtures.

```firmament
let MountingPattern {
    holeDiameter: length = 6.0mm tol 0.05mm
    countersinkDiameter: length = 10.0mm tol 0.10mm
}

manufacturing process<CNC> {
    material: Aluminum6061
    minimumToolRadius: 1.5mm
}

feature mountHole: hole<Countersink> {
    target: part.region("mountHoleA")
    diameter: MountingPattern.holeDiameter
    countersinkDiameter: MountingPattern.countersinkDiameter
    angle: 90deg
}

pmi {
    datum A {
        target: part.region("baseFace")
    }

    diameter mountHoleDiameter {
        target: part.region("mountHoleA")
        dimension: MountingPattern.holeDiameter
    }
}
```

## Deferred syntax and behavior

Phase 1 defers:

- templates that generate geometry;
- full modeling concepts;
- move-hole/local BRep surgery;
- automatic feature reconstruction;
- automatic DFM execution where not already implemented;
- PMI lowering for export-deferred controls;
- graphical PMI;
- full GD&T/Y14.5 coverage.

## R1 validation report link

The V2 Phase 1 R1 validation/report layer preserves this syntax doctrine while exposing bound lets, Forge concept applications, PMI records, tolerances, export-supported/deferred status, and diagnostics. See [`../implementation/v2-phase1-r1-validation-report.md`](../implementation/v2-phase1-r1-validation-report.md).
