# Firmament V2 F1 Forge concept-family applications

F1 implements the first Forge-backed concept-family application syntax for Firmament V2 Phase 1.

## Supported syntax

Firmament V2 now accepts generic `family<Concept>` applications in two declaration forms:

```firmament
manufacturing process<CNC> {
    material: "Aluminum6061"
    minimumToolRadius: 1.5mm
}

feature mountHole: hole<Countersink> {
    target: part.region("mountHoleA")
    diameter: MountingPattern.holeDiameter
    countersinkDiameter: MountingPattern.countersinkDiameter
    angle: 90deg
}
```

The parser stores the concept-family application shape as `familyName`, `conceptName`, and source span. Manufacturing declarations store the application and fields; feature declarations also store the feature name.

## Forge descriptor lookup behavior

Concept applications are parsed generically. Concept names are not parser branches. After parsing, Firmament V2 validates the family and concept through the built-in Forge concept descriptor registry used by this phase-one workbench path.

The registry is intentionally narrow and descriptor-oriented. It records field names, required status, and basic field kind expectations so later Forge milestones can add richer descriptor loading, DFM compatibility, PMI lowering, and reporting without changing the parser syntax.

## Built-in concepts supported

F1 includes these built-in descriptors:

- `process<CNC>`
- `hole<Countersink>`
- `hole<Shaft>`
- `hole<Counterbore>`

Canonical spelling is case-sensitive as shown above for concept names, with lower-case family names.

## Field validation rules

F1 validates:

- unknown concept family;
- unknown concept within a known family;
- missing required fields;
- unknown fields;
- duplicate fields;
- basic field kind mismatches for length, angle, material, and target fields.

Field values may be primitive literals, scalar let references, dotted let-record references, material identifiers/strings, or target expressions such as `part.region("mountHoleA")`. Toleranced let references preserve their alias tolerance metadata in the bound field value.

Material typing remains intentionally simple: `material` accepts a string literal or bare material identifier and is treated as material identity text. Rich material catalogs are deferred.

## Diagnostics

F1 adds deterministic diagnostics:

- `firmament-v2-concept-unknown-family`
- `firmament-v2-concept-unknown-concept`
- `firmament-v2-concept-missing-required-field`
- `firmament-v2-concept-unknown-field`
- `firmament-v2-concept-duplicate-field`
- `firmament-v2-concept-field-type-mismatch`
- `firmament-v2-concept-invalid-target`
- `firmament-v2-concept-descriptor-unavailable`

## Explicit non-scope

F1 does not execute DFM checks, lower concept applications to PMI, create or mutate geometry, materialize hole features, or introduce hidden behavior. It also does not hardcode a complete concept catalog into parser grammar; concept-specific requirements live in descriptors/validation.
