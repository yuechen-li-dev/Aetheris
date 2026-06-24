# Aetheris V2.0 Phase 1 Scope Contract

Milestone: **V2-PHASE1-L0**

This document is the scope contract for Aetheris V2.0 Phase 1. It is intentionally conservative: it defines the sellable workflow, the language/product constraints that must be preserved, the finish-line capabilities, and the work that is explicitly deferred.

## 1. Executive summary

Aetheris V2.0 Phase 1 is a **STEP/AP242 manufacturing-intent workbench**. It focuses on existing models, editable semantic overlays, PMI and tolerance authoring, Forge-backed DFM checks, and validation/reporting.

Phase 1 is not a full CAD modeling push. Full modeling and compiler-oriented CAD generation remain valid long-term directions, and compiler/modeling work may continue later, but they are deliberately outside the Phase 1 finish line.

The Phase 1 commercial wedge is narrower and more immediate: help LLMs and engineers safely add, inspect, validate, and transport manufacturing intent on existing CAD/STEP/AP242 models.

In plain terms:

```text
existing STEP/AP242 model
  -> canonicalize/import
  -> inspect/analyze/map/section
  -> label faces/regions
  -> add dimensions/tolerances/PMI
  -> apply Forge concept/DFM checks
  -> export enriched AP242
  -> validate/report
```

## 2. Product wedge

The immediate paid workflow is:

```text
Input:
  existing STEP/AP242 file

Aetheris:
  canonicalizes/imports
  analyzes/maps/sections
  labels regions
  adds PMI/dimensions/tolerances
  applies Forge process/concept checks
  exports enriched AP242
  emits a validation report

Output:
  enriched AP242 + report + editable Firmament overlay
```

Phase 1 should prioritize work that proves the value of this workflow:

- PMI injection and enrichment over existing geometry.
- PMI audit/checking for missing, inconsistent, or unsupported manufacturing intent.
- Forge-backed DFM and concept checks over existing regions/features.
- Supplier, manufacturing, and inspection data integrity during AP242 transport.
- Editable Firmament overlays that make the added intent auditable and reviewable.

2D drawings become secondary in this workflow, but Phase 1 does not require eliminating drawings on day one. Drawings may still be used for review, customer delivery, or supplier compatibility while AP242 PMI becomes the authoritative transport path where supported.

## 3. Non-goals for Phase 1

Phase 1 explicitly does **not** include:

- Full CAD modeling from scratch.
- Automatic arbitrary decompilation.
- Feature-history recovery.
- Generic fillet/chamfer authoring.
- General loft/NURBS modeling.
- AI driving a CAD UI to draw parts.
- Full drawing replacement.
- Graphical PMI/layout authoring.
- Turing-complete scripting.
- Hidden state machines.

SolidWorks, FreeCAD, Onshape, and similar systems may be used as verification/viewer hosts or future add-in targets. Aetheris does not need to replace legacy CAD in Phase 1.

## 4. Firmament V2.0 language doctrine

Firmament V2.0 is a **typed manufacturing-intent data language**. See [`firmament-v2-concept-template-syntax-reconciliation.md`](firmament-v2-concept-template-syntax-reconciliation.md) for the Phase 1 doctrine reconciling immutable `let` data, Forge-backed concepts, and deferred templates.

Firmament V2.0 is **not** a general programming language.

Hard doctrine:

- Pure data.
- Immutable `let` bindings.
- Typed values.
- Acyclic expression graph.
- No conditionals.
- No loops.
- No user functions.
- No mutation.
- No hidden state machines.
- No Turing completeness.

This doctrine exists because Firmament files must be auditable. Humans, LLMs, suppliers, manufacturing engineers, and inspection systems must be able to reason about the file without executing hidden behavior. Manufacturing intent must remain visible as data, not concealed behind logic, state, or control flow.

## Phase 1 language status note

- L1 primitive immutable typed `let` declarations are implemented.
- L2 grouped one-level `let` records and exact dotted record-field references are implemented for parser/model/binder exposure.
- L3 scalar arithmetic expression graph is implemented with strict primitive type rules, evaluated dependencies, and acyclic validation; same-record field expression binding remains deferred.
- L4 toleranced dimensional `let` syntax is implemented for `length`/`angle` scalar lets and literal record fields, with exact-alias tolerance preservation and nominal-only arithmetic drop diagnostics; PMI wiring and automatic tolerance propagation remain out of scope.

## 5. `let` declarations and typed values

Firmament V2.0 should use `let`, not `param`, for named values.

Primitive type names should likely be lowercase:

```firmament
let holeCount: int = 4
let holeDiameter: length = 6.0mm tol 0.05mm
let draftAngle: angle = 3deg
let scale: float = 1.25
```

Planned direction:

- `let` bindings are immutable named values.
- Record/grouped `let` blocks are allowed for related manufacturing intent.
- Dotted references access grouped values.
- Basic arithmetic may reference previous `let` values.
- Dimensional values are unit-aware.
- `int` and `float` are distinct types; counts, indices, and pattern quantities should use `int`, not `float`.

Record/group example:

```firmament
let MountingPattern {
    holeDiameter: length = 6.0mm tol 0.05mm
    holeSpacingX: length = 80.0mm tol +0.10mm -0.05mm
    holeSpacingY: length = 40.0mm tol 0.10mm
    holeCount: int = 4
}
```

## 6. Tolerance doctrine

Dimensions emitted as PMI must have tolerances.

Preferred ergonomic syntax:

```firmament
let holeDiameter: length = 6.0mm tol 0.05mm
let slotWidth: length = 12.0mm tol +0.10mm -0.05mm
```

Rules:

- `tol 0.05mm` means bilateral plus/minus tolerance.
- `tol +0.10mm -0.05mm` means asymmetric tolerance.
- Plain helper `let` values may exist without tolerances.
- Any value emitted as a PMI dimension must resolve to either:
  - a toleranced `let`, or
  - an explicit tolerance in the PMI record.

Tolerance propagation is deferred:

- Automatic tolerance propagation through arithmetic is not required for Phase 1.
- Arithmetic should use nominal values unless an explicit tolerance-aware design is introduced.
- If tolerance is dropped through arithmetic, diagnostics or warnings should be considered so PMI does not silently lose required tolerance evidence.

## 7. Arithmetic doctrine

Firmament V2.0 may allow basic arithmetic only:

```text
+
-
*
/
parentheses
references to previous lets
```

Firmament V2.0 must not allow:

- Conditionals.
- Loops.
- Functions.
- Recursion.
- User-defined operators.

Type rules should distinguish at least:

- `int`.
- `float`.
- `length`.
- `angle`.
- Possibly `material`.
- Future semantic types.

`int` exists for exact counts, indices, and pattern counts. It should not be collapsed into `float`, because exact count semantics matter for manufacturing intent, validation, and diagnostics.

## 8. Forge V2.0 concept/template direction

Forge should soft-pivot from model-generation templates to **extensible manufacturing concept packs plus DFM/PMI rule descriptors over existing geometry**.

Concept applications should become extensible and Forge-backed. Aetheris should move away from hardcoded `<cnc>` magic.

Planned shape:

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

The parser should understand the generic form:

```text
conceptFamily<ConceptName>
```

Forge should validate:

- The concept family exists.
- The concept exists.
- Required fields are present.
- Field types and units are valid.
- DFM checks pass or produce explicit findings.
- Compatible PMI/annotation outputs can be generated where applicable.

The parser should not hardcode every concept. Concept-specific rules belong in Forge descriptors and validation layers, not in scattered parser special cases.

## 9. PMI doctrine

Use the keyword `pmi`, not `gdt`.

Reasons:

- `pmi` is broader and more product-facing.
- GD&T has symbol and magic-frame connotations.
- Firmament should express human-readable manufacturing intent first, then lower to AP242/GD&T constructs where appropriate.

A PMI block should include:

- Dimensions.
- Datums.
- Tolerances.
- Semantic constraints.
- Formal controls where needed.

Example:

```firmament
pmi {
    datum A {
        target: part.region("baseFace")
    }

    diameter mountHoleADiameter {
        target: part.region("mountHoleA")
        dimension: MountingPattern.holeDiameter
    }

    flatness baseFlatness {
        target: part.region("baseFace")
        tolerance: 0.03mm
    }

    coplanar topFaceToDatumA {
        target: part.region("topFace")
        datum: A
        tolerance: 0.05mm
    }

    perpendicular sideToDatumA {
        target: part.region("sideFace")
        datum: A
        tolerance: 0.05mm
    }
}
```

Record-shaped PMI is the authoring form. Symbolic GD&T frames are lowering/export targets, not the primary Firmament authoring UX.

## 10. Minimal Phase 1 feature set

The conservative Phase 1 finish line is:

1. Existing STEP/AP242 canonicalization and InlineStep overlay.
2. Region/face labeling sufficient for PMI targets.
3. `let` declarations with typed values.
4. `let` record groups and dotted references.
5. Basic arithmetic expression graph with acyclic validation.
6. Tolerance syntax for dimensions.
7. `pmi` block for:
   - datum,
   - diameter,
   - linear distance or size dimension,
   - flatness,
   - parallel/perpendicular/coplanar relation.
8. Forge concept application syntax for `process<CNC>` and at least one hole concept.
9. DFM/PMI validation report.
10. AP242 export with semantic PMI evidence.
11. Demo packet/workflow proving the value.

Conservative boundaries:

- Anything requiring broad CAD modeling should be deferred.
- Anything requiring full ASME Y14.5 coverage should be treated as future work unless a narrow control is needed for the demo workflow.
- Graphical PMI layout is not required for the Phase 1 finish line.

## 11. Validation philosophy

Every Phase 1 feature should have:

- Parser/model tests.
- Semantic validation tests.
- AP242 output evidence if it exports.
- Analyzer/report evidence if it validates.
- No silent success on unsupported paths.

External verification:

- SolidWorks, FreeCAD, Onshape, or similar tools may be used as validation/viewer hosts.
- External viewers are not required for core CI unless explicitly configured.
- If an external viewer is used, the result should be treated as evidence and captured in the demo/reporting workflow.

## 12. Explicitly deferred

Deferred beyond Phase 1:

- Full CAD compiler/modeling.
- Arbitrary decompilation.
- Fillet/chamfer modeling.
- General ruled/loft modeling.
- Automatic feature reconstruction.
- Automatic tolerance propagation.
- Full ASME Y14.5 coverage.
- Graphical PMI/drawing views.
- Viewer/add-ins beyond an initial validation bridge.
- Automatic LLM-driven modeling in legacy CAD.

## 13. Example Phase 1 Firmament file

This example is illustrative of the intended Phase 1 direction. It is not a claim that the current parser accepts this syntax today.

```firmament
part ExistingBracket from InlineStep {
    source: "bracket.step"
}

let MountingPattern {
    holeDiameter: length = 6.0mm tol 0.05mm
    countersinkDiameter: length = 10.0mm tol 0.10mm
    holeSpacingX: length = 80.0mm tol +0.10mm -0.05mm
    holeSpacingY: length = 40.0mm tol 0.10mm
    holeCount: int = 4
}

let Plate {
    thickness: length = 8.0mm tol 0.10mm
    edgeClearance: length = 12.0mm
}

manufacturing process<CNC> {
    material: Aluminum6061
    minimumToolRadius: 1.5mm
}

regions {
    baseFace: part.face("canonical:face:base")
    topFace: part.face("canonical:face:top")
    sideFace: part.face("canonical:face:side-x-min")
    mountHoleA: part.region("recognized:hole:A")
    mountHoleB: part.region("recognized:hole:B")
}

feature mountHoleA: hole<Countersink> {
    target: part.region("mountHoleA")
    diameter: MountingPattern.holeDiameter
    countersinkDiameter: MountingPattern.countersinkDiameter
    angle: 90deg
}

pmi {
    datum A {
        target: part.region("baseFace")
    }

    diameter mountHoleADiameter {
        target: part.region("mountHoleA")
        dimension: MountingPattern.holeDiameter
    }

    distance mountHoleSpacingX {
        targetA: part.region("mountHoleA")
        targetB: part.region("mountHoleB")
        dimension: MountingPattern.holeSpacingX
    }

    flatness baseFlatness {
        target: part.region("baseFace")
        tolerance: 0.03mm
    }

    coplanar topFaceToDatumA {
        target: part.region("topFace")
        datum: A
        tolerance: 0.05mm
    }

    perpendicular sideToDatumA {
        target: part.region("sideFace")
        datum: A
        tolerance: 0.05mm
    }
}
```

## 14. Recommended next implementation order

Recommended post-L0 order:

```text
L1: let declarations + primitive typed literals — implemented as top-level immutable primitive literals (`int`, `float`, `length`, `angle`, `string`, `bool`) with records, references, arithmetic, and tolerances still deferred to later L-levels.
L2: let record groups + dotted references
L3: arithmetic expression graph + acyclic validation
L4: tolerance syntax and tolerant dimension values
F1: Forge concept-family application syntax — implemented for descriptor-validated `manufacturing process<CNC>` and `feature name: hole<Concept>` declarations; validation only, with no DFM execution, PMI lowering, or geometry/modeling behavior.
P1: pmi block v2 record-shaped dimensions/datums/basic controls — implemented for `datum`, `diameter`, `distance`, `flatness`, `parallel`, `perpendicular`, and `coplanar` authoring/binding. Datum and diameter can lower through existing semantic AP242 paths when targets resolve; distance/flatness/relation controls are preserved with export deferred.
R1: validation/report integration — implemented as a structured `firmamentV2Validation` report exposed by `aetheris validate <file-or-fixture> --json`, covering bound lets, tolerances, Forge concepts, PMI records, export-supported/deferred status, and fatal/non-fatal diagnostics without adding syntax or AP242 lowering. See [`../implementation/v2-phase1-r1-validation-report.md`](../implementation/v2-phase1-r1-validation-report.md).
D1: demo/update packet for V2.0 Phase 1 workflow
```

Each milestone should preserve the Phase 1 boundary: existing-model manufacturing intent first, no hidden behavior, no full modeling expansion, and no silent success on unsupported paths.
