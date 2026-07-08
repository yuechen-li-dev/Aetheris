# Aetheris V2.0 Phase 2 C# interop scope contract

Milestone: **V2-PHASE2-L0**

This document opens Aetheris V2.0 Phase 2 with a documentation-only scope contract for **C#-backed Firmament automation and concept authoring**. It is the architecture doctrine future Phase 2 milestones must obey. It does not add runtime behavior, parser behavior, Firmament syntax, C# plugin loading, Roslyn compilation, template generation, geometry generation, AP242 export behavior, or tests.

Phase 1 closed on this manufacturing-intent path:

```text
existing STEP/AP242 model
  -> Firmament semantic overlay
  -> typed/toleranced manufacturing values
  -> Forge concept applications
  -> record-shaped PMI
  -> validation report
  -> AP242 datum/diameter export with evidence
```

Phase 2 begins from this doctrine:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

Firmament is not a scripting language. Firmament is an auditable manufacturing-intent data format.

C# is the scripting/logic language. C# defines concept schemas, validation rules, DFM checks, PMI obligations, report contributions, and eventually templates.

Forge is the typed interop boundary between Firmament data and C# logic.

## 1. Executive summary

Phase 2 is about **C#-backed Firmament automation and concept authoring**.

The Phase 2 boundary is intentionally narrow:

- Firmament remains pure data.
- C# owns logic.
- Forge provides typed interop between Firmament data and C# logic.
- Concepts come first.
- Templates and generation come later.

The first Phase 2 surface should make Firmament data available to C# safely and losslessly enough for validation. The second surface should let C# define concepts consumed by Firmament declarations. Those surfaces should strengthen the existing Phase 1 workflow without turning Firmament into a macro language.

## 2. Why not add scripting to Firmament?

Firmament must not grow:

- `if`;
- `for`;
- functions;
- mutation;
- user-defined logic;
- embedded C#;
- imports that execute code;
- a macro runtime.

The boundary is simple: Firmament records manufacturing intent; C# executes logic about that intent.

Reasons:

- C# already exists as the correct language for logic.
- Firmament needs to remain inspectable, deterministic, reviewable, and safe as data.
- Aetheris should not invent a worse C# inside a CAD data file.

Firmament files must remain useful to humans, LLMs, suppliers, manufacturing engineers, inspection systems, and CI without requiring hidden execution to understand what has been declared.

## 3. Phase 2 core surfaces

Phase 2 has two core C# interop surfaces:

```text
1. Variable interop:
   C# can read typed Firmament values safely.

2. Concept authoring:
   C# can define concepts consumed by Firmament declarations.
```

Read-only interop comes first. Phase 2 should initially expose what Firmament already declares, validate it, and report on it. Mutation, code generation, template expansion, geometry generation, and source patching are separate capabilities and must not be smuggled into the first interop layer.

## 4. Variable interop contract

The intended C# API shape should be explicit and typed even if final names evolve. A future abstraction may look like:

```csharp
public interface IFirmamentVariables
{
    FirmamentValue Get(string name);
    bool TryGet(string name, out FirmamentValue value);
    IReadOnlyList<FirmamentVariable> All { get; }
}
```

Typed helpers may include:

```csharp
GetLength("MountingPattern.holeDiameter")
GetAngle("Countersink.angle")
GetString("Material.name")
GetBoolean("Flags.requiresInspection")
```

A `FirmamentValue` must preserve:

- variable name;
- declared type;
- nominal value;
- unit/dimension;
- tolerance, if any;
- source span if available;
- dependency/source expression summary if available;
- diagnostics if relevant.

Do not flatten dimensional or toleranced values to raw `double`.

Examples:

- `6.0mm tol 0.05mm` must remain a length value with tolerance.
- `90deg` must remain an angle.
- `MountingPattern.holeDiameter` must be resolvable as a dotted reference.

The interop contract should preserve Phase 1 evidence: type, dimensionality, tolerance, source, dependency, and diagnostic context are part of the value, not optional UI decoration.

## 5. Concept authoring contract

Firmament declares concept applications. C# defines what those concepts mean.

Example Firmament consumption:

```firmament
feature mountHole: hole<Countersink> {
    target: part.region("mountHoleA")
    diameter: MountingPattern.holeDiameter
    countersinkDiameter: MountingPattern.countersinkDiameter
    angle: 90deg
}
```

Example future C# concept shape:

```csharp
public sealed class CountersinkHoleConcept : IForgeConcept
{
    public ConceptId Id => ConceptId.Parse("hole<Countersink>");

    public void Define(ConceptSchemaBuilder schema)
    {
        schema.RequiredTarget("target");
        schema.RequiredLength("diameter").RequireTolerance();
        schema.RequiredLength("countersinkDiameter").RequireTolerance();
        schema.RequiredAngle("angle");
    }

    public IEnumerable<FirmamentDiagnostic> Validate(ConceptValidationContext context)
    {
        // DFM checks, geometry compatibility checks, PMI obligations, report contributions.
    }
}
```

Contract:

- Firmament declares concept applications.
- C# defines concept schemas, validation, DFM checks, PMI obligations, report contributions, and later suggestions.
- The parser recognizes generic concept-family syntax.
- Forge/C# descriptors define schema and behavior.
- The parser should not hardcode every concept.

## 6. Concepts versus templates

```text
Concept:
  C# descriptor/validator for semantic meaning over existing geometry and manufacturing data.

Template:
  Future C# generator/suggester that can create or propose Firmament declarations, source patches, or eventually geometry.
```

Phase 2 begins with concepts.

Templates are deferred until:

- variable interop is stable;
- C# concept authoring works;
- validation/report integration works;
- generated suggestions can be represented as explicit patch/report artifacts.

Firmament should not gain loops just to express templates. Template behavior belongs in C# once the concept and report path can expose suggestions deterministically and reviewably.

## 7. Concept-pack model

Future concept packs should separate stable abstractions, built-in standard concepts, and company-specific trusted extensions. Suggested project structure:

```text
Aetheris.Forge.Abstractions
  IForgeConcept
  IForgeConceptPack
  IForgeRegistry
  ConceptId
  ConceptSchemaBuilder
  ConceptValidationContext
  FirmamentValue
  FirmamentDiagnostic

Aetheris.Forge.Standard
  process<CNC>
  hole<Shaft>
  hole<Counterbore>
  hole<Countersink>
  datumPlane

Company.Aetheris.ForgePack
  company-specific concepts and rules
```

Built-in concepts should migrate or mirror into C# descriptors first before trusted external assembly loading. The first proof should establish the abstraction and built-in behavior inside the repository, then consider external distribution only after validation/report integration is stable.

## 8. Trusted code boundary

C# concept packs are code execution.

Phase 2 initial policy:

- built-in C# concepts only;
- trusted local assemblies later;
- no sandbox guarantee;
- no untrusted concept-pack loading;
- no automatic NuGet restore/compile from Firmament;
- no `use "./SomeConcept.cs"` inside Firmament.

Preferred future CLI shape:

```bash
aetheris validate part.firmament --forge-pack Company.Aetheris.ForgePack.dll --json
```

External loading is deferred unless explicitly implemented in a later milestone. No Phase 2 L0/A0/A1 work should imply untrusted plugin loading, sandboxing, or dynamic code fetched from Firmament source.

## 9. Validation/report integration

Phase 2 C# concepts should integrate with the existing R1 validation report.

C# concepts may contribute:

- diagnostics;
- DFM findings;
- concept field validation;
- PMI obligation status;
- report rows;
- suggestions, later.

Reports must remain:

- deterministic;
- bounded;
- JSON-friendly;
- LLM-readable;
- explicit about warnings versus fatal errors.

Concept contribution should strengthen the current report contract rather than replacing it with opaque logs or free-form execution output.

## 10. Minimum Phase 2 proof

The smallest convincing Phase 2 implementation target is:

```text
1. Define C# Forge abstractions.
2. Implement or mirror CountersinkHoleConcept through the new interface.
3. Firmament declares feature mountHole: hole<Countersink>.
4. aetheris validate invokes the C# concept validator.
5. The validator reads typed/toleranced fields through variable/field interop.
6. The validator emits diagnostics for invalid fields and passes a valid fixture.
7. The validation report includes the C# concept contribution.
```

Example checks:

- diameter must be positive;
- countersinkDiameter must be greater than diameter;
- angle must be reasonable;
- diameter and countersinkDiameter require tolerance;
- optional `process<CNC>.minimumToolRadius` compatibility check.

This proof should use real CLI validation and real report output. It should not rely on parser hardcoding, raw `double` coercion, runtime mutation, or template generation.

## 11. Phase 2 milestone ladder

Proposed order:

```text
V2-PHASE2-L0:
  Scope contract and architecture doctrine.

FORGE-CS-A0:
  Audit current Forge descriptors, bound Firmament values, diagnostics, R1 report, and P2 PMI bridge.

FORGE-CS-A1:
  Add C# abstraction layer for variable interop and concept authoring.

FORGE-CS-A2:
  Port/mirror built-in process<CNC> and hole concepts to C# descriptors.

FORGE-CS-A3:
  Invoke C# concept validators through aetheris validate and R1 report.

FORGE-CS-A4:
  Add concept-generated PMI obligation/report rows, suggestions only, no mutation.

FORGE-CS-A5:
  Optional trusted external assembly loading for concept packs.
```

A5 may be deferred to Phase 2.5 if trusted external loading would distract from built-in concept interop, validation, and reporting.

## 12. Non-goals

Phase 2 L0 explicitly defers:

- new Firmament control flow;
- Firmament functions;
- Firmament embedded C#;
- user-defined concepts in Firmament;
- on-the-fly Roslyn compilation;
- external assembly loading in L0/A0/A1 unless explicitly scoped later;
- untrusted plugin loading;
- sandboxing;
- template expansion;
- geometry generation;
- model mutation;
- move-hole/local BRep surgery;
- full DFM framework;
- full GD&T/Y14.5;
- graphical PMI;
- CAD replacement behavior.

A later milestone may intentionally scope one of these items only by updating the contract or adding a more specific successor contract. Until then, these remain out of scope.

## 13. Naming guidance

Prefer:

- `Aetheris.Forge.Abstractions`
- `IForgeConcept`
- `IForgeConceptPack`
- `IForgeRegistry`
- `ConceptValidationContext`
- `FirmamentVariableSet`
- `FirmamentValue`
- `PmiObligation`
- `DfmFinding`

Avoid:

- `ScriptEngine`
- `FirmamentScript`
- `DynamicCodeExecutor`
- `CSharpScriptRunner`

These avoided names imply a macro scripting engine. Phase 2 is C# interop and concept packs, not executable Firmament scripting.

## 14. Documentation links

Primary Phase 1 context:

- [`aetheris-v2-phase1-closeout.md`](aetheris-v2-phase1-closeout.md)
- [`aetheris-v2-phase1-scope-contract.md`](aetheris-v2-phase1-scope-contract.md)
- [`firmament-v2-concept-template-syntax-reconciliation.md`](firmament-v2-concept-template-syntax-reconciliation.md)
- [`../implementation/v2-phase1-r1-validation-report.md`](../implementation/v2-phase1-r1-validation-report.md)
- [`../implementation/v2-phase1-p2-record-pmi-ap242-export.md`](../implementation/v2-phase1-p2-record-pmi-ap242-export.md)

This Phase 2 contract preserves the Phase 1 doctrine while moving implementation responsibility for concept meaning, validation logic, DFM checks, PMI obligations, report contributions, and future templates into C# through Forge.
