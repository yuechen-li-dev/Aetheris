# LANG-FEATURE-X1 — pure Feature functions

## Executive verdict

**Accepted.** Firmament can express reusable typed semantic construction through deterministic Feature functions without adding general runtime control flow. Definitions lower to the existing semantic declaration grammar before Feature AIR; validation, inspection, build, and STEP export therefore consume the same admitted expansion.

## Semantics

- Parameters are ordered, strongly typed, and file-local. Calls accept positional or named arguments; trailing parameters may have typed defaults.
- A body contains zero or more immutable typed `let` derivations and exactly one terminal `return`.
- X1 return types are existing Mechanical semantic operations: `Hole<Shaft>`, `Hole<Counterbore>`, `Hole<Countersink>`, `Boss`, `Pocket`, and `EdgeFinish`.
- Feature calls may call another Feature. A finite call graph is checked before expansion; direct and indirect cycles produce `firmament-feature-recursion:<A>:<B>:...`.
- Expansion is pure source-to-semantic normalization. It reads no topology or global mutable state and does not materialize a definition by itself.
- Invocations inside a `Modify`, `Compose`, or bounded `Pattern` retain authored order. Relative selectors are left in the returned operation and resolve against the current construction state at the invocation position.
- Feature and ordinary construction declarations use distinct typed namespaces. Parameters and local bindings never enter the global symbol table.

The supported control-flow boundary is deliberately narrow:

| Supported | Unsupported |
| --- | --- |
| typed calls | `if` / `else` / ternary / `match` |
| immutable typed derivation | mutable state or reassignment |
| one terminal return | multiple or early returns |
| bounded `Pattern` composition | `for`, `foreach`, or `while` |
| `Template` specialization outside Feature | direct or indirect recursion |

Firmament intentionally does not provide general runtime control flow. A program describes one deterministic engineering construction, not a runtime family of topologically unrelated outputs. Pattern owns finite repetition, Template owns compile-time specialization, Concept owns admissibility, and Feature owns reusable semantic transformation.

## Existing Feature vocabulary

Before X1, `Feature` was not a top-level callable keyword. It appeared as the `Feature:` field of schema-owned Pattern records and throughout the implementation as the engineering category / Feature AIR boundary. X1 unifies with that vocabulary: a Feature function returns an existing semantic feature or operation and expands before Feature AIR. It does not introduce a second unrelated geometry executor. Existing `Feature:` fields and persisted Template feature generators are unchanged.

Body-to-Body functions were audited and not admitted. Current Mechanical use cases are cleaner as returned typed operations applied by the existing ConstructionState in source order. Multi-operation Feature groups are likewise deferred because the flagships do not justify a new tuple, list, or operation-group type.

## Before and after

The counterbore flagship replaces repeated seven-field declarations with a named call while retaining visible typed intent:

```firmament
// Before: repeat this block at every center.
Hole<Counterbore> LeftMount {
    On: +Z
    Center: Point2(-20mm, 0mm)
    Diameter: 8.5mm
    CounterboreDiameter: 14mm
    CounterboreDepth: 4mm
    End: ThroughAll
}

// After: the shared dimensions live in one pure definition.
M8Counterbore(Center: Point2(-20mm, 0mm))
```

For four mounting holes, the previous raw `Hole<Shaft>` body remains one Pattern element, but Feature gives that element a reusable typed identity:

```firmament
Pattern Mounts {
    Source: Design.Points
    MountHole(Center: Item)
}
```

There is no `for`: Pattern still owns the finite four-element expansion. The canonical fixtures reduce duplicated dimensional fields while keeping operation kind, placement, and termination explicit. The counterbore before/after qualification produces byte-identical STEP when the concrete generated feature name is held equal; semantic kind and dimensions also match.

| Flagship | Before | After | Duplicated call-site parameters | Semantic result |
| --- | ---: | ---: | ---: | --- |
| Two M8 counterbores | 16 construction lines | 11-line definition plus 2 one-line calls | 10 repeated fixed fields to 0 | Two typed `Hole<Counterbore>` operations |
| Four-hole Pattern element | 9-line Pattern/raw-Hole call site | 4-line Pattern/Feature call site, plus one reusable definition | 4 operation fields at the Pattern site to 0 | Four typed `Hole<Shaft>` operations |

The Pattern comparison separates reusable definition cost from call-site cost; Feature is justified by semantic naming and reuse, not by claiming every first use is shorter.

## Inspection and diagnostics

`aetheris inspect --json` reports `featureDefinitions`, `featureInvocations`, and deterministic `featureExpansion` counts, including parameter bindings, return type, expanded semantic kind, invocation ordinal, `generatedByFeature`, definition count, authored call count, and resulting pre-AIR node count. The resulting ordinary `features` inventory continues to report `Hole<Counterbore>`, `Hole<Shaft>`, or the other existing operation kind rather than an opaque function result.

Typed diagnostics cover argument count/name/type, local type errors, missing or multiple returns, wrong return type, unsupported body/control-flow syntax, duplicate declarations/parameters, and recursion. Argument failures include Feature name, parameter, expected/actual type, and invocation offset.

## Schema and library status

X1 supports `schema Mechanical`. `WireForm`, `Sweep`, and `SectionChain` reject Feature declarations with `firmament-feature-schema-unsupported:<schema>`; shared cross-schema Feature IR is a follow-up, not implied support. Features are file-local. No Standard Library Feature was promoted because the flagships are application policy (`M8Counterbore`, mounting layout, and edge finish), not a stable universal library primitive.

## Qualification

Canonical fixtures:

- `fixtures/Canonical/Feature/simple-hole-feature.firmament`
- `fixtures/Canonical/Feature/feature-with-derived-values.firmament`
- `fixtures/Canonical/Feature/feature-pattern.firmament`
- `fixtures/Canonical/Feature/nested-feature-call.firmament`
- `fixtures/Canonical/Feature/edge-finish-feature.firmament`
- `fixtures/Canonical/Feature/boss-pocket-feature.firmament`

Invalid fixtures cover wrong/missing/extra arguments, missing/wrong return, direct/indirect recursion, conditional syntax, and loop syntax under `fixtures/Invalid/Feature/`.

The automated lane covers parse/bind, defaults, named arguments, local unit-aware arithmetic, nested calls, Pattern composition, recursion and control-flow rejection, typed diagnostics, structured provenance, STEP export/reimport, enclosed geometry, deterministic repeat behavior, and exact concrete-vs-Feature STEP parity. X1 performs a bounded linear scan plus finite call expansion; no runtime geometry work is added by definitions.

Final local qualification:

- Release solution build: passed with zero warnings and zero errors.
- Feature-focused tests: 24 passed.
- Full `Aetheris.Kernel.Firmament.Tests`: 1,330 passed.
- Full `Aetheris.CLI.Tests`: 410 passed.
- Canonical production qualification: all 147 fixtures passed, including all six Feature flagships.
- Nine invalid Feature fixtures: all rejected by CLI validation with fatal typed diagnostics.
- Freshly packed and isolated `Aetheris.CLI 2.0.0-preview.3`: validated and built the derived M8 counterbore fixture; STEP export/reimport evidence remained successful.
- Fresh-author doctrine audit: all six expected boundaries were understood; the discovered M8-dimension, Pattern-source, Pattern-dialect, and nested-call documentation ambiguities were corrected in the public syntax guide.
- Repository layout guard: passed (3,941 tracked files inspected).
