# FORGE-CS-A1 read-only interop facades

Milestone: **FORGE-CS-A1**

Phase 2 doctrine remains:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

## Purpose

FORGE-CS-A1 adds the first read-only C# interop layer over existing Firmament V2 bound state. The goal is to let later Phase 2 concept logic read typed values, concept applications, diagnostics, and registry contracts without duplicating the parser or binder.

This milestone is adapter-only. It does not change Firmament syntax, parser behavior, binder behavior, runtime behavior, CLI behavior, validation-report policy, or AP242 export behavior.

## Types added

Runtime interop contracts now live under:

```text
Aetheris.Forge.Abstractions.FirmamentInterop
```

Added contracts:

- `IFirmamentVariables`
- `FirmamentVariable`
- `FirmamentValue`, `FirmamentScalarValue`, `FirmamentValueKind`
- `FirmamentTolerance`, `FirmamentToleranceKind`
- `FirmamentSourceSpan`
- `ConceptId`
- `FirmamentConceptApplicationView`
- `FirmamentConceptFieldView`
- `FirmamentConceptApplicationKind`
- `FirmamentFieldKind`
- `FirmamentDiagnostic`
- `FirmamentDiagnosticSeverity`
- `IForgeConcept`
- `IForgeConceptPack`
- `IForgeRegistry`
- `ForgeConceptRegistry`
- `ConceptSchemaBuilder`
- `ConceptSchemaField`
- `ConceptValidationContext`

Concrete V2 adapters now live under:

```text
Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop
```

Added adapters:

- `FirmamentV2VariablesAdapter`
- `FirmamentV2ConceptApplicationAdapter`
- `FirmamentV2DiagnosticAdapter`

## Adapter attachment points

A1 attaches to existing Phase 1 bound state only:

- `FirmamentV2Document.BoundLets`
- `FirmamentV2Document.BoundLetRecords`
- `FirmamentV2ManufacturingConceptDeclaration`
- `FirmamentV2FeatureConceptDeclaration`
- `FirmamentV2BoundConceptField`
- `FirmamentV2LiteralValue`
- `FirmamentV2Tolerance`
- `FirmamentV2SourceSpan`

No second binder or alternate parse path was added.

## Value and tolerance preservation

`FirmamentV2VariablesAdapter` exposes:

- top-level lets by name such as `holeDiameter`;
- dotted record fields such as `MountingPattern.spacingX`;
- `All` with both top-level names and dotted names.

The adapter preserves:

- primitive kind;
- nominal value;
- numeric value when available;
- unit text;
- tolerance kind, plus, minus, unit, and value kind;
- source span.

Dimensional values are not flattened to raw `double`. Length and angle values remain typed `FirmamentScalarValue` instances with `Kind`, `Nominal`, `NumericValue`, `Unit`, and optional `Tolerance`.

## Concept application facade

`FirmamentV2ConceptApplicationAdapter` projects current bound concept declarations into read-only views:

- `process<CNC>` becomes `new ConceptId("process", "CNC")`;
- `feature mountHole: hole<Countersink>` becomes a feature application view with `Name = "mountHole"`;
- target fields remain target-source strings;
- value fields remain typed `FirmamentValue` instances;
- aliased tolerance evidence on concept fields is preserved through `BoundValue.AliasTolerance`;
- source spans are carried forward where available.

This facade does not replace `FirmamentV2ForgeConceptRegistry` and does not invoke validators from the CLI.

## Diagnostic facade

`FirmamentDiagnostic` is the new structured diagnostic shape for later A2/A3 work. A1 also adds `FirmamentV2DiagnosticAdapter` helpers to map:

- existing `FirmamentV2ValidationDiagnostic` rows;
- parser diagnostic codes plus fatal/warning classification.

The facade preserves code, severity, message, source span, target metadata, and field metadata.

## Registry contracts

A1 adds the in-process registry contracts plus a minimal `ForgeConceptRegistry` implementation for deterministic registration and lookup inside the current process.

Current behavior:

- registration is explicit and in-process only;
- duplicate `ConceptId` registration throws a deterministic `InvalidOperationException`;
- no external assembly loading occurs;
- no plugin discovery occurs;
- no Roslyn compilation occurs.

`ConceptSchemaBuilder` is intentionally minimal for A1, but already supports:

- required target;
- required length;
- required angle;
- required string;
- required bool;
- required float;
- required int;
- tolerance-required markers.

Schema enforcement is not wired into runtime validation yet.

## Tests

Focused adapter tests were added in:

```text
Aetheris.Kernel.Firmament.Tests/ForgeCsA1InteropTests.cs
```

The tests cover:

- variable interop over top-level lets and dotted record fields;
- value/unit/tolerance/source-span preservation;
- concept application and field adaptation for `process<CNC>` and `hole<Countersink>`;
- in-process registry registration, resolution, and deterministic duplicate behavior;
- diagnostic facade metadata and serialization basics.

Follow-on milestone: [`forge-cs-a2-standard-concept-descriptors.md`](forge-cs-a2-standard-concept-descriptors.md) mirrors the built-in Phase 1 Forge concepts as runtime C# `IForgeConcept` descriptors while keeping parser/CLI/report/export behavior unchanged.

## Explicit non-scope

A1 intentionally does not add:

- CLI invocation of C# validators;
- C# validator execution through the validation report;
- plugin loading;
- external assembly loading;
- Roslyn compilation;
- template behavior;
- mutation or source patching;
- parser changes;
- binder changes;
- runtime changes;
- AP242 export changes.
