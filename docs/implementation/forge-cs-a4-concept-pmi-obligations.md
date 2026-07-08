# FORGE-CS-A4 concept PMI obligations

Milestone: **FORGE-CS-A4**

Phase 2 doctrine remains:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

## Purpose

FORGE-CS-A4 lets built-in C# Forge concepts contribute **report-only** PMI obligations to the existing Firmament V2 validation report surfaced by:

```bash
dotnet run --project Aetheris.CLI -- validate <file.firmament|file.firmfixture> --json
```

The goal is intentionally narrow:

- built-in C# concepts can declare bounded PMI obligations;
- the report compares those obligations against existing authored `pmi` records;
- satisfied and missing obligations become structured report rows;
- missing obligations surface as warning-only findings;
- no Firmament source mutation, PMI auto-generation, or AP242 export behavior is added.

## Obligation provider API

The A1/A3 runtime interop layer now includes an optional provider surface:

```csharp
public interface IForgePmiObligationProvider
{
    IEnumerable<PmiObligation> GetPmiObligations(ConceptValidationContext context);
}
```

`PmiObligation` carries:

- `Kind`
- `SourceConcept`
- `SourceName`
- `TargetSource`
- `ExpectedDimensionField`
- `Severity`

This interface is optional. `IForgeConcept` itself was not widened, so concepts without PMI behavior remain unchanged.

## Built-in concept coverage

A4 adds report-only diameter PMI obligations for these built-in hole concepts:

- `hole<Shaft>`
- `hole<Counterbore>`
- `hole<Countersink>`

Each emits one deterministic through-diameter obligation:

- `kind: diameter`
- `expectedDimensionField: diameter`
- `target: <feature target field source>`
- `sourceConcept: hole<...>`
- `sourceName: <feature declaration name>`
- `severity: warning`

`process<CNC>` contributes no PMI obligations in A4.

Counterbore-specific and countersink-specific secondary obligations such as entry diameter/depth/angle remain deferred to keep A4 bounded.

## Evaluation policy

Obligations are evaluated only for concept applications whose A3 runtime diagnostics contain **no fatal findings**.

Chosen policy:

- valid concept application -> evaluate PMI obligations
- invalid concept application -> emit no PMI obligation rows

This avoids misleading “missing PMI” warnings for concepts that are already semantically invalid.

## Statuses and diagnostics

Implemented A4 statuses:

- `satisfied`
- `missing`

Missing obligations emit warning diagnostics with:

- `code: forge.pmi.obligation.missing`
- `severity: warning`

Satisfied obligations emit no additional diagnostics.

Missing obligations do **not** make the top-level validation report invalid. Report status policy remains:

- fatal diagnostics -> `invalid`
- otherwise export-deferred PMI present -> `valid-with-deferred-export`
- otherwise -> `valid`

## Matching rules

A4 uses conservative matching against existing bound record-shaped PMI from `FirmamentV2BoundPmiBlock`.

Implemented diameter matching rule:

- PMI record kind is `diameter`
- bound PMI target exactly matches the obligation `TargetSource`

This is the bounded minimum-acceptable Phase 2 rule. A4 does **not** attempt geometric equivalence, topology equivalence, or richer dimension-source identity matching.

## Report JSON shape

`firmamentV2Validation` now includes:

- `conceptPmiObligations[]`

Each row includes:

- `kind`
- `sourceConcept`
- `sourceName`
- `target`
- `expectedDimensionField`
- `status`
- `severity`
- `matchedPmi` when satisfied
- `diagnosticCode` when missing

The summary now also carries:

- `pmiObligationCount`
- `satisfiedPmiObligationCount`
- `missingPmiObligationCount`

## Tests and fixtures

Added/report-updated coverage for:

- satisfied countersink diameter obligation
- missing shaft diameter obligation warning
- missing obligation plus export-deferred PMI preserving `valid-with-deferred-export`
- invalid concept not emitting misleading PMI obligation rows
- CLI JSON surfacing `conceptPmiObligations`

Added fixtures:

- `fixtures/FirmamentV2/Language/valid/concept-pmi-obligation-satisfied.valid.firmfixture`
- `fixtures/FirmamentV2/Language/valid/concept-pmi-obligation-missing-warning.valid.firmfixture`
- `fixtures/FirmamentV2/Language/valid/concept-pmi-obligation-with-deferred-export.valid.firmfixture`
- `fixtures/FirmamentV2/Language/invalid/concept-pmi-obligation-invalid-countersink.invalid.firmfixture`

Also normalized missing fixture metadata on several existing Firmament V2 fixtures so corpus-style CLI tests can classify them consistently.

## Explicit non-scope

FORGE-CS-A4 does not add:

- Firmament document mutation
- PMI auto-creation
- AP242 export changes
- external concept packs
- plugin loading
- Roslyn compilation
- templates
- geometry generation
- parser changes
- parser registry replacement
