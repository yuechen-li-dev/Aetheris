# FORGE-CS-A0 current implementation audit

Milestone: **FORGE-CS-A0**  
Scope: audit/design only for the Phase 2 C# interop abstraction layer.

Status note, July 7 2026: **FORGE-CS-A1 has now implemented the first read-only interop facades and V2 adapters described here.** The A0 attachment-point analysis remains the rationale and reference map for those adapters.

Phase 2 doctrine remains:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

This document audits the current Phase 1 Forge/Firmament/R1/P2 implementation and proposes the smallest useful A1 abstraction layer. It does not define new runtime behavior, parser behavior, Firmament syntax, AP242 export behavior, plugin loading, Roslyn compilation, templates, or document mutation.

## 1. Executive summary

Phase 1 currently implements a parser/binder-centered Firmament V2 workbench. Typed `let` values, record fields, expression results, tolerances, concept applications, and record-shaped PMI all live in `Aetheris.Kernel.Firmament.FirmamentV2`, primarily in `FirmamentV2Ast.cs` and `FirmamentV2Parser.cs`. Forge concept applications are parsed generically as `family<Concept>`, but their current field schemas are hardcoded in `FirmamentV2ForgeConceptRegistry`, not supplied by executable C# concept packs. R1 validation reports are built by `FirmamentV2ValidationReportBuilder` directly from the parse result and bound document. P2 AP242 export bridges bound datum/diameter PMI through `FirmamentBuildAndExport.BuildV2SemanticPmi` into `Step242SemanticPmi`.

A1 should attach abstractions over the existing bound document rather than duplicating the binder. The first attachment points are:

- `FirmamentV2Document.BoundLets` and `FirmamentV2Document.BoundLetRecords` for `IFirmamentVariables`;
- `FirmamentV2ManufacturingConceptDeclaration` and `FirmamentV2FeatureConceptDeclaration` for concept application facades;
- `FirmamentV2BoundConceptField` for typed field interop;
- existing parser/report diagnostics for a `FirmamentDiagnostic` facade;
- `FirmamentV2ValidationReportBuilder.BuildConcepts` as the later A3 report integration point;
- `FirmamentV2BoundPmiRecord` and the R1 PMI rows as the later A4 PMI obligation comparison point.

Major risks are duplicating the binder, flattening length/angle/tolerance to `double`, letting C# mutate bound Firmament documents, bypassing report status policy, over-hardcoding built-ins in a second place, and adding external assembly loading too early.

Recommended A1 implementation order:

1. Add read-only value/tolerance/source facades and adapters over `FirmamentV2BoundLet`.
2. Add concept id/application/field facades over current bound concept declarations.
3. Add a diagnostic facade that preserves current code/severity/message/source span capacity.
4. Add a local in-process registry abstraction, initially populated from current built-ins or mirrors.
5. Add adapter tests only; do not invoke concept validators from CLI until A3.

## 2. Current bound Firmament value model

Current value model files and types:

| Concern | Current type/file |
| --- | --- |
| Document aggregate | `FirmamentV2Document` in `Aetheris.Kernel.Firmament/FirmamentV2/FirmamentV2Ast.cs:3` |
| Primitive type enum | `FirmamentV2PrimitiveType` in `FirmamentV2Ast.cs:15` |
| Typed scalar value | `FirmamentV2LiteralValue` in `FirmamentV2Ast.cs:18` |
| Source metadata | `FirmamentV2SourceSpan` in `FirmamentV2Ast.cs:14` |
| Scalar let AST | `FirmamentV2LetDeclaration` in `FirmamentV2Ast.cs:24` |
| Record/group AST | `FirmamentV2LetRecordDeclaration` and `FirmamentV2LetRecordField` in `FirmamentV2Ast.cs:28` |
| Dotted reference AST | `FirmamentV2DottedReferenceExpression` in `FirmamentV2Ast.cs:21` |
| Evaluated expression | `FirmamentV2BoundExpression` in `FirmamentV2Ast.cs:33` |
| Bound scalar/field value | `FirmamentV2BoundLet` in `FirmamentV2Ast.cs:34` |
| Bound record | `FirmamentV2BoundLetRecord` in `FirmamentV2Ast.cs:35` |
| Tolerance | `FirmamentV2Tolerance` and `FirmamentV2ToleranceKind` in `FirmamentV2Ast.cs:16` |

`FirmamentV2Parser.Parse` wires value binding in this order: `BindLetRecords`, `BindLets`, concept parsing, then PMI parsing (`FirmamentV2Parser.cs:261-268`). Primitive literal parsing is in `ParseLetLiteral` (`FirmamentV2Parser.cs:404`), tolerance parsing is in `ParseOptionalTolerance` and `ParseTolerance` (`FirmamentV2Parser.cs:365`, `FirmamentV2Parser.cs:379`), record binding is in `BindLetRecords` (`FirmamentV2Parser.cs:439`), and scalar/expression binding is in `ExpressionBinder` (`FirmamentV2Parser.cs:509`).

Answers:

- **What type represents a bound value?** `FirmamentV2BoundLet`. It is used both for top-level scalar lets and record fields inside `FirmamentV2BoundLetRecord.Fields`.
- **What type represents a typed scalar?** `FirmamentV2LiteralValue`, with `Type`, `Value`, optional `NumericValue`, optional `Unit`, and optional `Raw`.
- **What type represents length/angle/unit dimensions?** The dimensional kind is `FirmamentV2PrimitiveType.Length` or `FirmamentV2PrimitiveType.Angle`; the unit text is carried by `FirmamentV2LiteralValue.Unit`, currently `mm` for length and `deg` for angle in Phase 1 parsing.
- **What type represents tolerance?** `FirmamentV2Tolerance`, with `Kind`, positive `Plus`, positive `Minus`, `Unit`, `Type`, and `SourceSpan`.
- **How are dotted names represented?** In AST expressions as `FirmamentV2DottedReferenceExpression(RecordName, FieldName, Source)`. Bound records are dictionaries keyed by field name; consumers form dotted names themselves, as R1 does with `$"{r.Name}.{f.Name}"`.
- **How does a consumer currently read `MountingPattern.holeDiameter`?** It looks up `FirmamentV2Document.BoundLetRecords`, finds the record named `MountingPattern`, then reads `Fields["holeDiameter"]`. The parser has a private helper `ResolveBoundLet` for PMI binding that does exactly this before falling back to top-level lets (`FirmamentV2Parser.cs:1409`).
- **Where would `IFirmamentVariables` wrap or adapt this?** It should wrap `FirmamentV2Document.BoundLets` plus `FirmamentV2Document.BoundLetRecords`. `TryGet("MountingPattern.holeDiameter", out value)` should adapt the bound record field without flattening `FirmamentV2LiteralValue` or `FirmamentV2Tolerance`.

Diagnostics from expression/tolerance binding are currently string codes accumulated in `FirmamentV2ParseResult.Diagnostics` (`FirmamentV2Ast.cs:147`). Most are fatal via `FirmamentV2Parser.IsFatalDiagnosticCode` (`FirmamentV2Parser.cs:696`); `firmament-v2-tolerance-dropped-through-arithmetic` is intentionally nonfatal.

## 3. Current concept application model

Current concept files and types:

| Concern | Current type/file |
| --- | --- |
| Concept application AST | `FirmamentV2ConceptApplication` in `FirmamentV2Ast.cs:37` |
| Concept field AST | `FirmamentV2ConceptField` in `FirmamentV2Ast.cs:38` |
| Bound concept field | `FirmamentV2BoundConceptField` in `FirmamentV2Ast.cs:39` |
| Manufacturing declaration | `FirmamentV2ManufacturingConceptDeclaration` in `FirmamentV2Ast.cs:40` |
| Feature declaration | `FirmamentV2FeatureConceptDeclaration` in `FirmamentV2Ast.cs:41` |
| Phase 1 descriptor | `FirmamentV2ForgeConceptDescriptor` in `FirmamentV2ForgeConceptRegistry.cs:23` |
| Phase 1 field descriptor | `FirmamentV2ForgeFieldDescriptor` in `FirmamentV2ForgeConceptRegistry.cs:11` |
| Phase 1 field kind | `FirmamentV2ForgeFieldKind` in `FirmamentV2ForgeConceptRegistry.cs:3` |
| Phase 1 registry | `FirmamentV2ForgeConceptRegistry` in `FirmamentV2ForgeConceptRegistry.cs:25` |

The built-in F1 descriptor registry currently declares:

- `process<CNC>` with required `material: Material` and `minimumToolRadius: Length` (`FirmamentV2ForgeConceptRegistry.cs:40-44`);
- `hole<Countersink>` with required `target`, `diameter`, `countersinkDiameter`, and `angle` (`FirmamentV2ForgeConceptRegistry.cs:49-53`);
- `hole<Shaft>` with required `target` and `diameter` (`FirmamentV2ForgeConceptRegistry.cs:54-56`);
- `hole<Counterbore>` with required `target`, `diameter`, `counterboreDiameter`, and `counterboreDepth` (`FirmamentV2ForgeConceptRegistry.cs:57-61`).

There is also an existing `Aetheris.Forge.Abstractions` namespace/project, but today it is descriptor metadata, not the Phase 2 runtime interop layer. `ForgePackageDescriptor`, `ForgeConceptDescriptor`, `ForgeFieldDescriptor`, and related metadata records live in `Aetheris.Forge/Abstractions/ForgeDescriptors.cs`. `Aetheris.Forge.Standard.StandardConceptPack` creates descriptor-only standard concepts such as `Standard.CNC`, `Standard.ShaftHole`, `Standard.CounterboreHole`, and `Standard.CountersinkHole`, but these are not used by the Firmament V2 parser registry and do not execute validators.

Field validation happens in `FirmamentV2Parser.ValidateConceptApplication` (`FirmamentV2Parser.cs:1039`) and `BindConceptField` (`FirmamentV2Parser.cs:1067`). The parser validates unknown family/concept through `FirmamentV2ForgeConceptRegistry.TryGet`, then required fields, unknown fields, duplicate fields, and type compatibility. Target fields are special: `FirmamentV2ForgeFieldKind.Target` does not accept a scalar type; the binder stores the source string in `FirmamentV2BoundConceptField.TargetSource`.

Answers:

- **What type currently represents `process<CNC>`?** `FirmamentV2ConceptApplication(FamilyName: "process", ConceptName: "CNC", SourceSpan)` inside `FirmamentV2ManufacturingConceptDeclaration`.
- **What type currently represents `feature mountHole: hole<Countersink>`?** `FirmamentV2FeatureConceptDeclaration(Name: "mountHole", Application: FirmamentV2ConceptApplication("hole", "Countersink", ...), Fields, SourceSpan, BoundFields)`.
- **Where are descriptor field requirements declared?** In `FirmamentV2ForgeConceptRegistry.Build`, using private `Descriptor` and `Field` helpers. All current fields are required because `Field` returns `new(name, kind, true)`.
- **Where are required-field and type diagnostics emitted?** `ValidateConceptApplication` emits required/unknown/duplicate/unknown descriptor diagnostics, and `BindConceptField` emits field type mismatch/invalid target diagnostics.
- **How much is hardcoded in parser versus descriptor/binder?** The generic syntax is parser-owned, but the Phase 1 descriptor catalog is hardcoded in `FirmamentV2ForgeConceptRegistry`. Field type binding is also parser-owned. There are no pluggable C# validators.
- **Where should `IForgeConcept` adapt or replace current descriptors?** A1 should initially adapt current `FirmamentV2ForgeConceptDescriptor` and bound declarations. A2 can mirror built-ins as `IForgeConcept` descriptors. Replacement of the registry lookup in parse/bind should wait until A2/A3 because A0/A1 must avoid behavior changes.

## 4. Current diagnostics model

Firmament V2 parser/binder diagnostics are string codes on `FirmamentV2ParseResult.Diagnostics`. Fatal classification is a large explicit table in `FirmamentV2Parser.IsFatalDiagnosticCode` (`FirmamentV2Parser.cs:696`). R1 maps those strings to `FirmamentV2ValidationDiagnostic(Code, Severity, Message)` (`FirmamentV2ValidationReport.cs:16`) through `ToDiagnostic` (`FirmamentV2ValidationReport.cs:91`). R1 severity strings are currently `"fatal"` and `"warning"`.

Kernel/build/export diagnostics use a separate core shape: `KernelDiagnostic(Code, Severity, Message, Source)` in `Aetheris.Kernel.Core/Diagnostics/KernelDiagnostic.cs`, with `KernelDiagnosticSeverity.Info|Warning|Error` and `KernelDiagnosticCode` values such as `ValidationFailed`.

CLI JSON shapes:

- `aetheris validate --json` emits `{ firmamentV2Validation = report }` from `CliRunner.RunValidate` (`Aetheris.CLI/CliRunner.cs:323-324`).
- `aetheris build --json` emits `success`, path fields, migration/assist reports, `pmiExportEvidence`, or build diagnostics with `{ source, message, severity }` (`CliRunner.cs:241-265`).

Answers:

- **Can Phase 2 C# concepts emit existing diagnostics directly?** They can emit compatible code strings, but A1 should not expose raw string append as the public contract. A facade is needed so validators do not learn parser internals or accidentally bypass severity/status policy.
- **Is a new `FirmamentDiagnostic` facade needed?** Yes. It should be small and mappable to current R1 diagnostics: `Code`, `Severity`, `Message`, optional `SourceSpan`, optional `Target`, optional `FieldName`, and optional related ids.
- **How should C# concept diagnostics preserve severity, code/id, message, source span, and related target/field?** Preserve them in the facade, then adapt to R1 `FirmamentV2ValidationDiagnostic` and future per-concept/per-field rows. Source span should wrap `FirmamentV2SourceSpan` without requiring C# concepts to reference kernel AST types.
- **How does R1 decide report status from diagnostics?** `FirmamentV2ValidationReportBuilder.Build` counts diagnostics whose severity is `"fatal"` and sets status to `invalid` if any exist; otherwise it returns `valid-with-deferred-export` if any PMI record has export support `deferred`, else `valid` (`FirmamentV2ValidationReport.cs:27-39`).

## 5. Current R1 validation report data flow

Report model:

- `FirmamentV2ValidationReport` root has `Source`, `Status`, `Lets`, `Concepts`, `Pmi`, `ExportSupport`, `Diagnostics`, and `Summary` (`FirmamentV2ValidationReport.cs:5`).
- Rows are `FirmamentV2ValidationLet`, `FirmamentV2ValidationConcept`, `FirmamentV2ValidationConceptField`, `FirmamentV2ValidationPmiRecord`, and `FirmamentV2ValidationDimension` (`FirmamentV2ValidationReport.cs:18-23`).
- Summary is `FirmamentV2ValidationSummary` (`FirmamentV2ValidationReport.cs:15`).

Builder data flow:

```text
FirmamentV2Parser.Parse
  -> FirmamentV2ParseResult
  -> FirmamentV2ValidationReportBuilder.Build
  -> BuildLets from BoundLets/BoundLetRecords
  -> BuildConcepts from ManufacturingConcepts/FeatureConcepts
  -> BuildPmi from PmiBlock/BoundPmi
  -> status/summary/export matrix
```

`BuildLets` reads `FirmamentV2Document.BoundLets` and `BoundLetRecords` (`FirmamentV2ValidationReport.cs:43-53`). `BuildConcepts` reads `ManufacturingConcepts` and `FeatureConcepts` (`FirmamentV2ValidationReport.cs:55-67`). `BuildPmi` reads `PmiBlock.Records` and joins against `BoundPmi.Datums/Dimensions/Controls` (`FirmamentV2ValidationReport.cs:70-83`). Export-deferred PMI is represented in report rows through `ExportSupport` and `Reason`; `ExportSupport` returns `"deferred"` for everything except datum and diameter (`FirmamentV2ValidationReport.cs:99-100`).

CLI path: `aetheris validate <file> [--json]` reads the file, calls `FirmamentV2Parser.Parse`, builds the report, writes JSON under `firmamentV2Validation`, and returns nonzero only when status is `invalid` (`Aetheris.CLI/CliRunner.cs:287-326`).

Answers:

- **Where should C# concept contributions enter the report?** A3 should enter after parse/bind and before final status/summary computation, either by extending `FirmamentV2ValidationReportBuilder.Build` with concept validation results or by passing an enriched validation context/result into the builder.
- **Should C# concept validation run before, during, or after current R1 report building?** After current parse/bind and current schema validation, before report status is finalized. This preserves the parser/binder as the source of typed fields while letting C# concepts add diagnostics and rows.
- **How should concept diagnostics affect status?** Through the same severity policy as parser diagnostics: fatal/error concept diagnostics make report status `invalid`; warnings remain visible and non-blocking.
- **How should concept report rows be represented?** Extend current `FirmamentV2ValidationConcept` rows with C# validation status/contributions, not opaque logs. A1 can define the facade; A3 should map concept validator diagnostics to existing rows and top-level diagnostics.

## 6. Current P2 AP242 PMI bridge

Record-shaped PMI types:

- `FirmamentV2PmiBlock`, `FirmamentV2PmiRecord`, and `FirmamentV2PmiField` in `FirmamentV2Ast.cs:139-141`;
- `FirmamentV2BoundPmiBlock` and `FirmamentV2BoundPmiRecord` in `FirmamentV2Ast.cs:142-143`.

Binding path:

- `ParsePmi` builds records and bound records (`FirmamentV2Parser.cs:1304`).
- `TryBindPmiRecord` validates targets, resolves dimensions through bound lets/record fields, requires tolerance evidence for dimension references, and produces `FirmamentV2BoundPmiRecord` (`FirmamentV2Parser.cs:1386-1406`).

AP242 bridge:

- `FirmamentBuildAndExport.ValidateV2PmiExportSupport` rejects build/export for export-deferred record kinds (`FirmamentBuildAndExport.cs:431`).
- `FirmamentBuildAndExport.BuildV2SemanticPmi` reads legacy-compatible `document.Pmi` plus `document.BoundPmi` by name (`FirmamentBuildAndExport.cs:446-456`).
- Diameter records become `Step242SemanticPmiHole`, with tolerance plus/minus read from `boundPmi.DimensionTolerance` (`FirmamentBuildAndExport.cs:459-470`).
- Datum records become `Step242SemanticPmiDatum` (`FirmamentBuildAndExport.cs:473-476`).
- Target resolution for recognized/imported InlineStep regions is handled by `TryResolveV2RecognizedRegionTarget` and `ResolveV2DatumTarget` (`FirmamentBuildAndExport.cs:484`, `FirmamentBuildAndExport.cs:523`).

`Step242SemanticPmi` and its concrete records live in `Aetheris.Kernel.Core/Step242/Step242SemanticPmi.cs`: `Step242SemanticPmiHole` carries diameter/depth/family/tolerance plus/minus, `Step242SemanticPmiDatum` carries kind/label/target, and `Step242SemanticPmiNote` carries text.

Build JSON evidence is assembled in the CLI, not in the bridge itself. `CliRunner` emits `pmiExportEvidence.datum[]` from `FirmamentStepExportResult.DatumInspection` and `pmiExportEvidence.diameter[]` from `DimensionInspection` (`Aetheris.CLI/CliRunner.cs:265`). The evidence rows are marked `exportSupport: "supported"` and `exportEvidence: "found"`.

Answers:

- **Where should future concept-generated PMI obligations attach?** Not directly to AP242 export first. A4 should attach them as report-only obligations beside current R1 PMI rows, comparing against `FirmamentV2BoundPmiRecord` and current source `pmi` records.
- **Should obligations become report-only first?** Yes. That keeps A4 reviewable and avoids silent export mutation.
- **What type should represent a PMI obligation in A1/A4?** A1 can reserve a small `ForgePmiObligation` or `PmiObligation` shape with kind, source concept id/application name, target, expected dimension/tolerance requirement, status, and source span. A4 should implement it as report data before AP242 lowering.
- **How can concept obligations compare against existing `pmi` records?** Compare obligation kind/name/target/dimension requirements against `FirmamentV2BoundPmiBlock` records. For diameter obligations, compare target and length/tolerance evidence; for datum obligations, compare datum target/label; unresolved or export-deferred obligations remain report findings.

## 7. Proposed A1 abstraction layer

The smallest useful A1 layer should be read-only adapters over current bound types. Because `Aetheris.Forge.Abstractions` already exists as a project/namespace for descriptor metadata, A1 should either add runtime interop contracts there with clear names or use an internal sibling namespace such as `Aetheris.Forge.Abstractions.FirmamentInterop`. Do not add a new project until the repo shape demands it.

Proposed interfaces/types:

```csharp
namespace Aetheris.Forge.Abstractions;

public interface IFirmamentVariables
{
    bool TryGet(string name, out FirmamentValue value);
    FirmamentValue GetRequired(string name);
    IReadOnlyList<FirmamentVariable> All { get; }
}

public sealed record FirmamentVariable(
    string Name,
    FirmamentValue Value,
    FirmamentSourceSpan? SourceSpan);

public abstract record FirmamentValue
{
    public required string Name { get; init; }
    public required FirmamentValueKind Kind { get; init; }
    public FirmamentTolerance? Tolerance { get; init; }
    public FirmamentSourceSpan? SourceSpan { get; init; }
}

public enum FirmamentValueKind
{
    Int,
    Float,
    Length,
    Angle,
    String,
    Bool
}

public sealed record FirmamentScalarValue(
    string Name,
    FirmamentValueKind Kind,
    object Nominal,
    double? NumericValue,
    string? Unit,
    FirmamentTolerance? Tolerance,
    FirmamentSourceSpan? SourceSpan);

public sealed record FirmamentTolerance(
    FirmamentToleranceKind Kind,
    double Plus,
    double Minus,
    string Unit,
    FirmamentValueKind ValueKind,
    FirmamentSourceSpan? SourceSpan);

public sealed record ConceptId(string Family, string Concept)
{
    public override string ToString() => $"{Family}<{Concept}>";
}

public interface IForgeConcept
{
    ConceptId Id { get; }
    void Define(ConceptSchemaBuilder schema);
    IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context);
}

public interface IForgeConceptPack
{
    string Id { get; }
    Version Version { get; }
    void Register(IForgeRegistry registry);
}

public interface IForgeRegistry
{
    void Register(IForgeConcept concept);
    bool TryResolve(ConceptId id, out IForgeConcept concept);
}

public sealed record ConceptValidationContext(
    ConceptApplication Application,
    IFirmamentVariables Variables,
    IReadOnlyDictionary<string, FirmamentFieldValue> Fields);
```

Design principles:

- wrap/adapt `FirmamentV2BoundLet`, `FirmamentV2LiteralValue`, `FirmamentV2Tolerance`, `FirmamentV2ConceptApplication`, and `FirmamentV2BoundConceptField`;
- avoid duplicate parsing/binding;
- preserve units, tolerances, dependencies, and source spans;
- keep A1 interop read-only;
- no mutation, generation, external assembly loading, or template behavior;
- map diagnostics back into current report policy rather than inventing opaque logs.

## 8. Proposed concept validation flow

Target A1/A2/A3 flow:

```text
Firmament parse/bind
  -> current bound concept applications
  -> Forge registry resolves IForgeConcept
  -> schema validation over current fields
  -> concept Validate(context)
  -> diagnostics/report contributions
  -> R1 validation report
```

What remains in the existing binder:

- source parsing;
- generic `family<Concept>` application recognition;
- value expression binding;
- dotted reference resolution;
- unit/tolerance preservation;
- target source capture;
- duplicate/unknown field detection until A2/A3 intentionally replaces it.

What moves to C# concept descriptors over A2/A3:

- concept schema declaration;
- required/optional field policy;
- richer field constraints;
- DFM checks;
- PMI obligations;
- concept-specific report contributions.

Built-in concepts should first be mirrored in C# with behavior-equivalent schemas. A later milestone can replace `FirmamentV2ForgeConceptRegistry` lookup once report/test parity proves the mirror. Unsupported or missing concepts should continue to report deterministic diagnostics equivalent to `firmament-v2-concept-unknown-family`, `firmament-v2-concept-unknown-concept`, or `firmament-v2-concept-descriptor-unavailable`.

## 9. Proposed variable/field interop mapping

| Current type/file | Future interop facade | Notes |
| --- | --- | --- |
| `FirmamentV2BoundLet` in `FirmamentV2Ast.cs` | `FirmamentVariable` / `FirmamentScalarValue` | Preserve name, type, value, source span, dependencies, tolerance. |
| `FirmamentV2PrimitiveType.Int` | `FirmamentValueKind.Int` | `Nominal` is integral; no tolerance. |
| `FirmamentV2PrimitiveType.Float` | `FirmamentValueKind.Float` | Unitless numeric; no dimensional tolerance. |
| `FirmamentV2PrimitiveType.Length` plus `FirmamentV2LiteralValue.Unit` | `FirmamentLengthValue` or scalar with `Kind=Length` | Preserve `Unit`; do not coerce to raw `double`. |
| `FirmamentV2PrimitiveType.Angle` plus `FirmamentV2LiteralValue.Unit` | `FirmamentAngleValue` or scalar with `Kind=Angle` | Preserve `deg` and source span. |
| `FirmamentV2PrimitiveType.String` | `FirmamentStringValue` or scalar with `Kind=String` | Material identifiers currently bind as string values for concept fields. |
| `FirmamentV2PrimitiveType.Bool` | `FirmamentBoolValue` or scalar with `Kind=Bool` | Preserve native bool. |
| `FirmamentV2Tolerance` | `FirmamentTolerance` | Preserve plus/minus, kind, unit, dimensional kind, source span. |
| `FirmamentV2BoundLetRecord.Fields` | `IFirmamentVariables.TryGet("Record.field")` | Dotted name is facade behavior over record dictionary. |
| `FirmamentV2DottedReferenceExpression` | `FirmamentReference` metadata if exposed | Useful for dependency/source expression summaries. |
| `FirmamentV2BoundExpression.Dependencies` | `FirmamentValue.Dependencies` or metadata | Preserve dependency names for report/diagnostics. |
| `FirmamentV2BoundConceptField.BoundValue` | `FirmamentFieldValue.Value` | Field source plus typed value/tolerance. |
| `FirmamentV2BoundConceptField.TargetSource` | `FirmamentTargetReference` | Keep as structured source string first; richer target object can come later. |
| `FirmamentV2RecognizedRegion` | `FirmamentTargetReference.ResolvedRegion` later | Do not require A1 geometry resolution. |

## 10. Risks and design traps

| Risk | Mitigation |
| --- | --- |
| Duplicating the binder | A1 adapters must consume `FirmamentV2Document` bound state only. |
| Flattening units/tolerances to `double` | Make unit and tolerance required parts of dimensional value facades. |
| Letting C# mutate bound documents | Expose read-only records/interfaces only; no setters back into Firmament AST. |
| Letting concepts bypass diagnostics/report policy | Route validator diagnostics through a `FirmamentDiagnostic` facade and R1 severity mapping. |
| Over-hardcoding built-in concepts | Mirror built-ins once, test parity, then replace registry deliberately in A2/A3. |
| Adding external assembly loading too early | Keep A1 registry in-process and built-in only. |
| Turning concept validation into opaque logs | Concept validators return structured diagnostics/findings/obligations, not console text. |
| Overbuilding templates before concepts work | Keep templates out of A1-A4; no source patches or generation. |

## 11. Recommended next milestones

**FORGE-CS-A1:** Add abstraction interfaces/facades and adapters over current bound value/concept types.

A1 checklist:

- Add read-only `IFirmamentVariables` and value/tolerance/source facades.
- Add adapters from `FirmamentV2Document.BoundLets` and `BoundLetRecords`.
- Add `ConceptId`, concept application, field, and target facades over current concept declarations.
- Add `FirmamentDiagnostic` facade with severity/code/message/source/field/target.
- Add in-process registry abstractions without CLI invocation or plugin loading.
- Add adapter tests proving dotted names, units, tolerances, and source spans survive.

**FORGE-CS-A2:** Mirror/port built-in `process<CNC>` and `hole` concepts to C# descriptors using the new interfaces.

**FORGE-CS-A3:** Invoke C# concept validators through `aetheris validate` and R1 report.

**FORGE-CS-A4:** Add concept-generated PMI obligation/report rows.

**FORGE-CS-A5:** Optional trusted external concept-pack assembly loading.

This A0 audit changes documentation only. It intentionally does not alter runtime/compiler/export behavior.
