# FORGE-CS-A2 standard concept descriptors

Milestone: **FORGE-CS-A2**

Phase 2 doctrine remains:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

## Purpose

FORGE-CS-A2 mirrors the built-in Phase 1 Forge concepts as C# `IForgeConcept` descriptors using the A1 interop contracts.

This milestone proves schema/descriptor parity only. It does not replace the current `FirmamentV2ForgeConceptRegistry`, does not invoke C# validators through `aetheris validate`, and does not change Firmament syntax, parser behavior, binder behavior, validation-report behavior, or AP242 export behavior.

## Types added

Runtime concept classes now live in:

```text
Aetheris.Forge/Standard/
```

Added runtime concept types:

- `CncProcessConcept`
- `ShaftHoleConcept`
- `CounterboreHoleConcept`
- `CountersinkHoleConcept`
- `StandardForgeRuntimeConceptPack`

These are distinct from the existing descriptor-only `Aetheris.Forge.Standard.StandardConceptPack`, which remains metadata-oriented and is not the Firmament V2 runtime concept registry.

## Runtime pack

`StandardForgeRuntimeConceptPack` is an in-process `IForgeConceptPack` that registers:

- `process<CNC>`
- `hole<Shaft>`
- `hole<Counterbore>`
- `hole<Countersink>`

Registration stays deterministic through the existing `IForgeRegistry` duplicate-registration behavior. No plugin loading, external assembly loading, or Roslyn compilation was added.

## Schema parity

The runtime C# concepts mirror the current Phase 1 registry fields exactly:

- `process<CNC>`
  - `material`: required `material`
  - `minimumToolRadius`: required `length`
- `hole<Shaft>`
  - `target`: required `target`
  - `diameter`: required `length`
- `hole<Counterbore>`
  - `target`: required `target`
  - `diameter`: required `length`
  - `counterboreDiameter`: required `length`
  - `counterboreDepth`: required `length`
- `hole<Countersink>`
  - `target`: required `target`
  - `diameter`: required `length`
  - `countersinkDiameter`: required `length`
  - `angle`: required `angle`

To express `process<CNC>.material` parity more faithfully, A2 extends `ConceptSchemaBuilder` with `RequiredMaterial(...)` and `ConceptSchemaValueKind.Material`.

Material remains string-compatible at the current parser/binder layer because the existing Phase 1 registry accepts `FirmamentV2PrimitiveType.String` for `Material` fields. A2 records that distinction as schema metadata only; it does not introduce a new parser-level value kind.

## No-op validator policy

Each built-in runtime concept implements `Validate(ConceptValidationContext)` as an intentional no-op for A2:

- no DFM checks;
- no PMI obligation generation;
- no mutation;
- no report contribution;
- no AP242 export interaction.

This keeps A2 at descriptor/schema parity. Runtime validation/report integration is deferred to A3.

## Registry relationship

`FirmamentV2ForgeConceptRegistry` remains the active Phase 1 parser registry.

A2 adds an internal descriptor-enumeration helper so tests can compare the new C# schemas against the current Phase 1 registry catalog without widening the production runtime surface unnecessarily.

## Tests

Focused tests in `Aetheris.Kernel.Firmament.Tests/ForgeCsA2StandardConceptTests.cs` cover:

- runtime pack registration and deterministic duplicate behavior;
- exact per-concept schema metadata;
- parity between C# schemas and `FirmamentV2ForgeConceptRegistry`;
- no-op `Validate(...)` behavior;
- unchanged parsing/report behavior for existing Phase 1 concept fixtures.

Follow-on milestone: [`forge-cs-a3-runtime-concept-validation.md`](forge-cs-a3-runtime-concept-validation.md) wires these built-in runtime descriptors into `aetheris validate` and the R1 validation report path.

## Explicit non-scope

A2 intentionally does not add:

- CLI invocation of C# concept validators;
- parser registry replacement;
- parser changes;
- binder changes;
- R1 validation-report behavior changes;
- DFM checks;
- PMI obligation generation;
- plugin loading;
- external assembly loading;
- Roslyn compilation;
- template generation;
- Firmament document mutation;
- AP242 export changes.
