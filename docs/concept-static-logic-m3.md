# CONCEPT-STATIC-LOGIC-M3: bounded static selection

Firmament supports finite compile-time selection without becoming a scripting language. `Enum` and `Match` are accepted only in the Concept IR phase. Every successful selection produces an ordinary typed Concept IR value; executable branch nodes are not part of Feature AIR, Construction AIR, BRepPlan, BRep, or STEP.

## Syntax and semantics

Enum types and variants use PascalCase. Variants are finite and retain source-declaration order:

```firmament
Enum BracketVariant {
    Compact
    Standard
}

Concept Struct BracketConcept {
    Variant: BracketVariant = Standard
}
```

Duplicate variants and unknown enum types or variants are errors. Enum values are compile-time-only. They may remain in Concept IR/report provenance, but never lower into geometry.

`Match` is an expression with Rust-style fat-arrow arms:

```firmament
ChamferDistance: Match Variant {
    Compact => 1mm
    Standard => 1.5mm
}
```

The M3 scrutinee domain is exactly enum values and `bool`. Enum arms use exact variant names; boolean arms use lowercase `true` and `false`. Wildcards, guards, ranges, destructuring, predicates, and first-match semantics are deferred.

Every `Match` must explicitly enumerate its finite domain. Missing arms are reported individually. A duplicate exact arm is rejected as both duplicate and unreachable. Invalid boolean patterns and unknown enum patterns are diagnosed before selection.

## Typed evaluation and dependencies

All arms are bound and evaluated through the existing typed Concept IR representations before the selected arm is accepted. Their types must agree exactly, preserving dimensional kind and spatial/collection shape. Thus `1mm` and `90deg` do not unify. M3 supports scalar `Length`, `Angle`, `Bool`, `Int`, `Float`, and `String` values plus the currently representable spatial values and derivations, including `Box3`, `Plane`, `Axis`, and `Grid`/`Point3[]` results. Units use the existing canonical Concept rules (`mm` and `deg` in the current surface).

Static members are resolved through a deterministic dependency evaluator. References may be declared later in the same Concept Struct. Dependencies include the scrutinee and every arm expression, so a cycle in a selected or unselected branch is rejected. Cycle diagnostics include the member chain where available. Concept IR cannot reference materialized-only values.

Evaluation is finite, total for admitted syntax, side-effect free, and has no loops, recursion, I/O, reflection, host-language execution, search, backtracking, or constraint solving.

## Erasure and report evidence

The authoritative member table stores only the resolved `ConceptIrValue`. `ConceptIrDocument.StaticSelections` is non-executable evidence containing the member, scrutinee, scrutinee type/value, selected arm, result kind/value, source span, and provenance. `ConceptIrDocument.ErasureStatus` remains `ErasedBeforeFeatureAir`.

Feature AIR consumers read resolved values exactly as if they had been declared directly. For example, a selected `Box3` becomes the materialized box bounds and a selected `Length` becomes the AIR chamfer distance. The report's AIR section contains neither a Match node nor conditional execution. Concept Struct instances remain `Materialized: false`; only `Struct` or `Model` creates bodies.

The fixtures [static-logic-m3-compact.firmament](../demos/static-logic-m3-compact.firmament) and [static-logic-m3-standard.firmament](../demos/static-logic-m3-standard.firmament) exercise the same declarations with different enum values and produce distinct valid AP242 geometry.

## Diagnostics

M3 emits stable diagnostic families for unknown/duplicate enums, unknown variants, duplicate/unreachable arms, non-exhaustive matches, invalid boolean arms, arm type mismatches, invalid scrutinee types, circular static dependencies, and selected-branch evaluation failure. Existing Concept diagnostics continue to cover invalid spatial derivations and illegal materialized-phase references.

## Deferred static logic

M3 does not add declaration/feature presence selection, runtime conditionals, wildcard arms, guards, arbitrary predicates, user functions, loops, mutation, general interpretation, inferred design relationships, or a constraint solver. A sensible next expansion is compile-time selection over a small closed set of declaration bundles, but only after defining an explicit bounded expansion IR and preserving the same pre-Feature-AIR erasure rule.
