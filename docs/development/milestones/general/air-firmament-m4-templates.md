# Firmament M4B compile-time Templates and Patterns

`Template` is a typed, compile-time declaration generator. M4A parses immutable Template IR, binds named static arguments, specializes deterministically, and expands to ordinary `Concept Struct`, `Struct`, or `Model` declarations before Concept IR and Feature AIR are built. It is not a runtime generic, a Forge plugin invocation, a macro text substitution system, or a constraint solver.

```firmament
Template <
    type TBody satisfies PrismaticBody,
    Width: Length,
    Depth: Length,
    Height: Length,
    Variant: BracketVariant = Standard
>
Struct MountingBracket: MountingFrame { ... }

Struct CompactBracket = MountingBracket <
    TBody: Box, Width: 60mm, Depth: 40mm, Height: 20mm, Variant: Compact
>
```

The authoritative IR contains declarations, type/value parameters, constraint Concept names, default expressions, applications, bound arguments, source spans, specialization identities, generated declaration paths, and erasure provenance. Arguments are named, checked for duplicates/unknown names, type-checked, and defaults are recorded in source-map provenance. Enum parameters are checked against the same `Enum` declarations used by static `Match`; an enum-bound `Match` is selected during specialization and recorded as provenance.

`satisfies` is structural capability conformance; it is neither inheritance nor dynamic dispatch. The first materialization descriptor recognizes `Box` as the explicit prismatic capability provider. Other supplied types must be a `Concept Struct` declared to satisfy the requested Concept. Unknown constraint Concepts, missing arguments, invalid defaults, and default dependency cycles are deterministic diagnostics.

`Require Name => expression` is evaluated after binding. Its supported M4A form is a finite conjunction of numeric comparisons with matching units; it validates declared invariants and never searches for values. A non-boolean value or false assertion is a compile-time diagnostic. Recursive template application graphs and recursive specializations are rejected with a readable chain.

Each application gets a deterministic specialization identity and readable generated paths such as `CompactBracket::Design` and `CompactBracket::Base`. `ConceptIrTemplateInstantiation` is provenance only: it contains source spans, bindings, defaults, generated paths, and `ExpandedBeforeFeatureAir`. The expanded source alone reaches Concept IR, then Feature AIR. No executable Template, Pattern, Match, or Require node is retained in Feature AIR.

M4A deliberately does not provide partial specialization, overload ranking, recursion, variadics, reflection, C# execution, or general scripting. Forge descriptors continue to describe external validation/DFM/PMI metadata; expanding a language Template never executes an assembly.

## M4B: bounded Pattern expansion

M4B completes the finite, typed generation half of the milestone. A `Pattern` lives in a specialized materialized declaration and consumes a statically resolved `Point3[]` Concept Struct member:

```firmament
Pattern MountHoles {
    Source: Design.MountPoints
    Hole<Shaft> Item {
        On: Base.Top
        Center: Item
        Diameter: HoleDiameter
        End: ThroughAll
    }
}
```

`Source` must resolve to a compile-time `Point3[]`; consequently `Item` is exactly `Point3`. The source is ordered, finite, and bounded to 1024 generated declarations. An empty source is valid and expands to zero declarations. Incompatible sources, missing `Center: Item`, unbound items, malformed feature templates, expansion-limit violations, and generated-name collisions are deterministic diagnostics.

Expansion happens after template argument/default binding, `Require`, static `Match`, and Concept Struct spatial evaluation. It produces ordinary semantic hole declarations, one per source element, such as `CompactBracket::MountHoles[0]`. Their readable paths are backed by deterministic specialization, pattern path, point ordinal, and point stable-ID provenance. No runtime loop exists.

The ordinary M2 semantic-hole route then receives each concrete `Point3`: Concept IR point value → resolved point placement → face-local placement → `AirHoleFeature`/composite materializer → BRep → AP242 STEP. Pattern, Template, Match, Require, and the item binding are source-map/report provenance only by Feature AIR time. `patternExpansions` in the existing build JSON records its count and generated paths.

The persisted end-to-end proofs are [template-m4b-compact.firmament](../../../../fixtures/DemoRegression/template-m4b-compact.firmament) and [template-m4b-standard.firmament](../../../../fixtures/DemoRegression/template-m4b-standard.firmament). They use the same `MountingBracket` shape (duplicated only because Firmament source imports are not yet a language feature), specialize distinct dimensions/enum arms, expand two through holes, and reimport as enclosed manifold bodies with two cylindrical surfaces.

M4 deliberately still excludes general `for`/`foreach`, mutation, recursive Templates/Patterns, partial specialization, variadics, overload ranking, reflection, runtime generics, arbitrary feature collection combinators, automatic hole/chamfer composition, and constraint solving. The next useful expansion is Concept-to-STEP matching evidence over generated semantic references, not general metaprogramming.
