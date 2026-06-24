# Aetheris V2.0 Phase 1 closeout report

Milestone: **V2-PHASE1-CLOSEOUT-A0**

This closeout summarizes what Aetheris V2.0 Phase 1 now supports, what remains intentionally deferred, what fixture proves the finish line, and how Phase 2 should begin. It is a report only; it does not define new compiler, language, geometry, or export behavior.

## 1. Executive summary

Aetheris V2.0 Phase 1 is now a STEP/AP242 manufacturing-intent workbench over existing models.

Phase 1 deliberately closes on a narrow, sellable workflow rather than broad CAD replacement:

- It is **not** full CAD modeling from scratch.
- It is **not** arbitrary decompilation or feature-history recovery.
- It is **not** graphical PMI layout or drawing replacement.
- It **does** support typed Firmament overlays, Forge concept-family applications, record-shaped PMI validation, validation/build reporting, and AP242 datum/diameter export with evidence.

The Phase 1 scope contract defines the product wedge as adding, inspecting, validating, and transporting manufacturing intent on existing STEP/AP242 models. The concept/template reconciliation note preserves the language doctrine: Firmament remains immutable manufacturing data; concepts annotate and constrain existing geometry; templates and generation remain deferred.

## 2. Phase 1 product workflow

The completed Phase 1 workflow is:

```text
Input:
  existing STEP/AP242 file

Firmament:
  InlineStep overlay
  let values/records
  arithmetic
  tolerances
  Forge concept applications
  pmi block

Aetheris:
  validate/report
  export supported PMI to AP242
  emit evidence

Output:
  enriched AP242
  validation/build report
  editable Firmament source
```

In concrete terms, a user starts with an existing STEP/AP242 file, attaches an editable Firmament V2 overlay, records typed and toleranced manufacturing values, applies Forge concepts to named regions/features, authors record-shaped PMI, validates the result, and exports the supported subset of semantic PMI back into AP242 with machine-checkable evidence.

## 3. Completed milestone timeline

| Milestone | What it added | Why it matters |
| --- | --- | --- |
| L0: phase scope contract | Defined Phase 1 as a conservative STEP/AP242 manufacturing-intent workbench and documented explicit non-goals. | Prevents scope drift into full CAD modeling, arbitrary decompilation, drawing replacement, or Turing-complete scripting. |
| L1: primitive `let` declarations | Added immutable typed scalar declarations such as `let name: type = literal` for `int`, `float`, `length`, `angle`, `string`, and `bool`. | Establishes typed manufacturing data as the foundation for later tolerances, PMI, and report rows. |
| L2: let records + dotted references | Added one-level grouped `let` records and exact dotted references to record fields. | Lets related manufacturing facts travel together while remaining explicit and auditable. |
| L3: arithmetic expression graph + strict type rules | Added side-effect-free arithmetic, dependency tracking, cycle rejection, and strict dimensional type rules. | Allows derived manufacturing values without hidden state, mutation, or unsafe unit coercion. |
| L4: tolerance syntax | Added bilateral and asymmetric tolerance syntax on dimensional scalar lets and record fields. | Makes tolerance evidence first-class data that PMI validation/export can inspect. |
| F1: Forge concept-family application syntax | Added generic `family<Concept>` parsing/binding and built-in descriptors for narrow process/hole concepts. | Provides the Phase 1 bridge from Firmament data into manufacturing semantic constraints without hardcoding concept grammar branches. |
| P1: record-shaped `pmi` block | Added authoring-oriented `datum`, `diameter`, `distance`, `flatness`, `parallel`, `perpendicular`, and `coplanar` PMI records. | Gives users an editable, source-level PMI representation before AP242 lowering. |
| R1: validation/report integration | Added `aetheris validate ... --json` reporting for lets, tolerances, concepts, PMI, export support, and diagnostics. | Makes the manufacturing-intent overlay inspectable by humans, LLMs, demos, and CI. |
| P2: AP242 export for record-shaped datum/diameter PMI | Wired record-shaped `datum` and `diameter` PMI over resolved InlineStep targets into the semantic AP242 export path and evidence reporting. | Completes the Phase 1 proof that editable Firmament PMI can become enriched AP242, not just parser state. |
| Syntax reconciliation / fixture metadata cleanup | Reconciled concept-first syntax doctrine and fixture usage around immutable data, concept applications, and deferred templates. | Keeps authoring examples and implementation direction aligned with Phase 1 boundaries. |
| CLI help hygiene | Kept CLI validation/build/help surfaces discoverable for the workflow. | Makes the closeout path repeatable from the command line instead of relying on private knowledge. |

Primary context documents: `docs/v2/aetheris-v2-phase1-scope-contract.md`, `docs/v2/firmament-v2-concept-template-syntax-reconciliation.md`, and the L1/L2/L3/L4/F1/P1/R1/P2 implementation notes under `docs/implementation/`.

## 4. Current Firmament V2 language capabilities

Current Firmament V2 Phase 1 language features are:

```text
let name: type = literal
let Record { field: type = value }
dotted references
+ - * / arithmetic
acyclic dependency graph
strict int/float/length/angle type rules
tolerance syntax:
  tol 0.05mm
  tol +0.10mm -0.05mm
concept-family application:
  process<CNC>
  hole<Countersink>
record-shaped pmi:
  datum
  diameter
  distance
  flatness
  parallel
  perpendicular
  coplanar
```

The language boundaries remain intentional:

- no loops;
- no conditionals;
- no functions;
- no mutation;
- no hidden state;
- no Turing completeness.

Firmament V2 remains typed manufacturing-intent data, not an executable CAD scripting language.

## 5. Forge / concept status

Implemented in Phase 1:

```text
parser/model/binder support for concept-family applications
built-in descriptors for process<CNC>, hole<Shaft>, hole<Counterbore>, hole<Countersink>
required field/type validation
```

Deferred beyond Phase 1:

```text
real DFM execution
external C# concept packs
generated PMI obligations
template/generation behavior
```

Doctrine:

- Concepts are semantic constraints over existing geometry and manufacturing intent.
- Templates are future generation or suggestion mechanisms.
- Phase 1 is concept-first, not template-generation-first.

This matches the syntax reconciliation note: parser grammar owns the stable generic application shape, while Forge descriptors own concept catalog semantics and future validation/report behavior.

## 6. PMI / AP242 export status

| PMI record | Parse/bind | Validate | AP242 export | Notes |
| --- | --- | --- | --- | --- |
| `datum` | yes | yes | yes | Semantic AP242 evidence emitted when the target resolves. |
| `diameter` | yes | yes | yes | Toleranced dimension evidence emitted for supported InlineStep targets. |
| `distance` | yes | yes | deferred | No AP242 lowering yet. |
| `flatness` | yes | yes | deferred | No AP242 lowering yet. |
| `parallel` | yes | yes | deferred | No AP242 lowering yet. |
| `perpendicular` | yes | yes | deferred | No AP242 lowering yet. |
| `coplanar` | yes | yes | deferred | Semantic relation, no AP242 lowering yet. |

Important boundaries:

- Graphical PMI is not implemented.
- Drawing views are not implemented.
- Full GD&T/Y14.5 is not implemented.
- Unsupported or deferred PMI records are explicit in reports/diagnostics; they are not silently dropped.

For build/export, P2 rejects export-deferred PMI records instead of emitting an incomplete AP242 file that appears fully supported.

## 7. Validation/report status

The validation entry point is:

```bash
aetheris validate <file.firmament|file.firmfixture> --json
```

When `aetheris` is not installed on `PATH`, the repository path is:

```bash
dotnet run --project Aetheris.CLI -- validate <file.firmament|file.firmfixture> --json
```

The JSON report includes:

- status;
- scalar lets and record-field lets;
- tolerances;
- Forge concepts;
- PMI records;
- export support or deferred-export state;
- diagnostics.

Top-level status values are:

- `valid`;
- `valid-with-deferred-export`;
- `invalid`.

Per-item rows expose whether a concept or PMI record is valid, invalid, export-supported, or export-deferred. Concept rows currently report DFM execution as not run because real DFM execution is deferred.

## 8. Phase 1 proof fixture / demo proof

The key P2 proof fixture is:

```text
fixtures/FirmamentV2/InlineStep/valid/inline-step-v2-record-pmi-datum-diameter-step-verified.valid.firmfixture
```

It proves the Phase 1 finish line because it exercises:

- an existing InlineStep STEP/AP242 model;
- recognized datum/hole regions;
- a toleranced `let` record;
- record-shaped datum and diameter PMI;
- AP242 export through the supported semantic PMI path;
- diameter tolerance evidence;
- reimport/analyze verification;
- build JSON `pmiExportEvidence` rows confirming supported export evidence.

The P2 implementation note records the key AP242 strings checked for datum, diameter, diameter tolerance, `tolerance_plus`, and `tolerance_minus` evidence.

The FTC-11-style demo packet is relevant as a product narrative where available: public NIST STEP in, editable Firmament overlay, enriched AP242 out, and report evidence. The closeout claim remains limited to supported Phase 1 semantics and does not imply full feature recognition, complete GD&T, or drawing replacement.

## 9. Known limitations

Phase 1 intentionally does not include:

```text
no DFM execution yet
no external C# concept packs yet
no full PMI/GD&T export yet
no distance/flatness/relation AP242 lowering yet
no graphical PMI/drawing views
no full modeling from scratch
no feature-history recovery
no move-hole/local BRep surgery
no automatic feature recognition as a product promise
no automatic tolerance propagation
```

Additional practical boundaries:

- Tolerance propagation through arithmetic is not inferred; arithmetic is nominal-only unless an explicit tolerance is declared.
- Built-in concept descriptors are narrow and descriptor-oriented.
- AP242 export support is limited to the supported semantic datum/diameter path.
- Deferred export records are authoring-valid but not AP242-lowered in Phase 1.

## 10. Phase 2 handoff

Phase 2 direction:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

Phase 2 should focus on:

- C# concept-pack abstractions;
- C# concept validators;
- DFM checks;
- PMI obligations generated by concepts;
- report integration;
- later external/trusted concept-pack loading.

Do not start Phase 2 implementation as part of this closeout. The recommended order is:

```text
V2-PHASE2-L0: Phase 2 C# interop scope contract
FORGE-CS-A0: audit current Forge abstractions/descriptors
FORGE-CS-A1: C# concept-pack abstractions and registry
FORGE-CS-A2: port built-in process/hole concepts to C# descriptors
FORGE-CS-A3: run concept validators through aetheris validate
FORGE-CS-A4: concept-generated PMI obligations/report rows
FORGE-CS-A5: optional trusted external assembly loading
```

Templates/generation come later. Move-hole and local editing remain out of immediate scope.

## 11. Commercial positioning

Phase 1 now supports the product wedge:

```text
add, inspect, validate, and export manufacturing intent on existing STEP/AP242 models
```

This is useful and sellable without claiming to replace SolidWorks, FreeCAD, Onshape, drawings, feature-history systems, or full CAD modeling.

## 12. Validation

Closeout validation should use the repository's real .NET path and CLI ground truth:

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --filter "FirmamentV2|PMI|Pmi|Forge|Concept|Let|Tolerance|Validation"
dotnet run --project Aetheris.CLI -- validate fixtures/FirmamentV2/InlineStep/valid/inline-step-v2-record-pmi-datum-diameter-step-verified.valid.firmfixture --json
dotnet run --project Aetheris.CLI -- --help
git diff --check
git status --short
```

If the broad test filter selects no tests or misses relevant names, use focused equivalent Firmament V2, PMI, Forge, let, tolerance, and validation tests and record the exact command used.
