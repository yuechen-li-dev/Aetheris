# Firmament V1 ownership map

## Format and normalized model

The V1 document is identified by structure, not extension:

```text
TOON-style:                         JSON-shaped:
firmament:                          { "firmament": {...},
model:                                "model": {...},
schema: (optional)                    "schema": {...},
ops[N]:                               "ops": [...],
pmi[N]: (optional)                    "pmi": [...] }
        \                              /
         +-- FirmamentTopLevelParser -+
                         |
                         v
             FirmamentParsedDocument
              - FirmamentParsedHeader
              - FirmamentParsedModelHeader
              - FirmamentParsedSchema?
              - FirmamentParsedOpsSection
              - FirmamentParsedPmiSection?
```

`FirmamentTopLevelParser.Parse` tries `JsonDocument.Parse` first. Valid JSON is decoded by `ParseFromRoot`; otherwise the indentation/count-header TOON parser runs and calls `ParseFromToon`. The two syntaxes are not textual aliases: JSON preserves JSON scalar/object rendering through `JsonElement.ToString()`, while TOON has its own indentation, array-count, and nested-value collapsing rules. They do intentionally converge on the same parsed model and therefore share all post-parse semantics.

## Component ownership and disposition

| Component | Current role | Syntax or semantics | Current callers | Future disposition |
|---|---|---|---|---|
| `Parsing/FirmamentTopLevelParser.cs` | TOON + JSON parser, structure checks, placement/schema/PMI decoding | mixed codec and early semantic recognition (`KnownOpKind`) | `FirmamentCompiler`, `FirmamentFormatter`, trace probe indirectly | `KEEP_AS_COMPATIBILITY_READER`; split codec from semantic admission in M2 |
| `ParsedModel/FirmamentParsed*.cs`, `FirmamentKnownOpKind`, `FirmamentOpFamily` | shared normalized V1 document | mixed; raw fields are codec data, known kinds/families are execution semantics | validators, lowerers, formatter, tests | raw/versioned DTO: `KEEP_AS_SERIALIZATION`; op classification: `LEGACY_TEST_ONLY` then `DELETE_LATER` after consumers move |
| `Validation/FirmamentSchemaValidator`, required-field validators, target/coherence/PMI validators | V1 engineering validation and selector/reference semantics | semantics | `FirmamentCompiler` | `LEGACY_TEST_ONLY`; retain compatibility validation only to the degree needed to read/migrate safely |
| `Mapping/FirmamentCompiledSchemaMapper.cs`, `CompiledModel/FirmamentCompiledSchema.cs` | V1 process/schema lowering | semantics | `FirmamentCompiler`, DFM validators | `MIGRATE_TO_V2_CONSUMER` only where a V2 concept already exists; otherwise freeze/delete later |
| `Lowering/FirmamentPrimitiveLowerer.cs`, lowering plan | converts `op` records to executable primitives/booleans/features | semantics | `FirmamentCompiler`; selected V2 primitive bridge feeds the same executor but not this V1 lowerer | `LEGACY_TEST_ONLY`; do not move into serialization |
| `Execution/FirmamentPrimitiveExecutor.cs` | primitive, Boolean, pattern, chamfer/fillet/draft execution | semantics | V1 compiler and V2 primitive bridge; direct Boolean facade consumer | shared executor portions require separate audit; V1 orchestration `MIGRATE_TO_V2_CONSUMER`/freeze, not serialization |
| `Execution/FirmamentSelectorResolver`, placement resolver/anchor semantics | selector and placement execution | semantics | V1 executor/tests | `LEGACY_TEST_ONLY`; never infer V2 semantic references from it |
| `Execution/FirmamentValidationExecutor.cs` | executes V1 `expect_*` operations | semantics/regression language | `FirmamentCompiler` | `LEGACY_TEST_ONLY`, then `DELETE_LATER` after equivalent current tests exist |
| `FirmamentFormatter` + `Formatting/*` | deterministic canonical TOON writer from normalized model | codec, but canonicalizes rather than preserves | formatter/example tests only | `KEEP_AS_COMPATIBILITY_WRITER`; name it V1 TOON explicitly |
| `FirmamentStepExporter` | compiles V1 and exports selected executed BRep | legacy execution bridge | `FirmamentBuildAndExport` fallback; tests | `KEEP_AS_COMPATIBILITY_READER/EXEC_BRIDGE` temporarily; remove from ordinary V2 routing only after an explicit legacy command/API exists |
| `FirmamentCompiler` | full V1 parse -> validate -> lower -> execute -> validation pipeline | legacy authoring/execution | V1 tests, `FirmamentStepExporter`, two FrictionLab tests, AirChamfer legacy tests | `DEPRECATE` as public authoring compiler; retain compatibility facade during M2-M5 |
| `FirmamentFrontendTraceProbe.ParseOnly` and box trace bridge | parser-backed historical fixture evidence | compatibility/test evidence | CLI trace and fixture tracing | `LEGACY_TEST_ONLY`; V2-only probe remains current |
| `Connectors/FirmamentPartLibraryConnector.cs` | standard-library part lookup used by legacy `library_part` op | shared primitive behind legacy syntax | V1 execution | connector behavior may be `KEEP_AS_SHARED_PRIMITIVE`; V1 op binding is legacy |

## Execution chain and live dependencies

The exact V1 chain is:

```text
FirmamentCompiler.Compile
  -> FirmamentTopLevelParser.Parse
  -> schema / required-field / target / coherence / PMI validators
  -> FirmamentCompiledSchemaMapper.Map
  -> FirmamentPrimitiveLowerer.Lower
  -> FirmamentPrimitiveExecutor.Execute
  -> schema DFM + enclosed-void validation
  -> FirmamentValidationExecutor.Execute (`expect_*`)
```

Production-relevant dependencies are:

| Entry | Dependency | Classification | Migration note |
|---|---|---|---|
| `FirmamentBuildAndExport.ExportSource` | if V2 returns `NotRecognized`, calls `FirmamentStepExporter.Export`, which creates `FirmamentCompiler` | live legacy build compatibility | This is the hidden V1 execution dependency. A real CLI build of `testdata/firmament/examples/box_basic.firmament` succeeded during M1. Keep until M2 names an explicit compatibility route. |
| `FirmamentPrimitiveExecutor` | invoked by both V1 compiler and `FirmamentV2BuildLowering.LowerPrimitiveBridge` | shared execution implementation | Do not delete wholesale with V1. Separate request/model ownership before moving it. |
| CLI `build`, `view`, `verify`, and consumers of `FirmamentBuildAndExport` | inherit fallback behavior | compatibility surface | CLI `validate` and `inspect` are V2-only and reject V1; behavior is already asymmetric and must be documented. |
| `ForgeHost` | calls `FirmamentBuildAndExport.CompileSource` | indirect compatibility exposure | Expanded Forge source is expected V2, but unrecognized input can reach V1 fallback; close or explicitly retain this in M2. |
| Cadmata fixture service/server build path | calls `FirmamentBuildAndExport.Run` | indirect compatibility exposure | Preserve behavior during M2, then decide whether server accepts legacy input explicitly. |
| `FirmamentFrontendTraceProbe` / CLI trace | V1 parser-backed fixture support | historical regression/educational | Keep in a named legacy fixture lane. |

`FirmamentCompiler` has no ordinary production caller other than `FirmamentStepExporter`; direct constructor calls are in tests/FrictionLab. That makes the exporter fallback the compatibility choke point.

## Syntax versus engineering semantics

Retain as serialization concerns:

- format identity and declared version;
- top-level section/shape validation;
- deterministic ordering/writing;
- raw values needed for round-trip and provenance;
- stable authored IDs as data;
- unknown-data preservation policy (currently unknown sections are rejected, not preserved);
- source-format and source-location provenance.

Freeze outside serialization:

- the supported `op` catalog and `FirmamentOpFamily`;
- required engineering fields and selector contracts;
- implicit sequential feature/reference ordering;
- placement interpretation;
- schema DFM enforcement;
- automatic PMI meaning;
- lowering, BRep execution, and `expect_*` execution.

## Round-trip audit

| Route | Status | Evidence and loss |
|---|---|---|
| V1 TOON -> model -> V1 TOON | **semantically stable, textually lossy** | `FirmamentFormatter` parses and emits deterministic TOON. It normalizes whitespace, field ordering, number formatting, nesting, and comments; comments are not modeled. |
| V1 JSON -> model -> V1 JSON | **unsupported** | No JSON writer exists. The only writer emits TOON. |
| V1 JSON -> model -> V1 TOON | **potentially semantically stable for accepted bounded documents, textually lossy** | Both parse paths converge, but JSON representation/order/type distinctions are reduced to raw strings and canonical TOON value parsing. Compatibility tests, not assumption, must define the safe subset. |

No writer should claim losslessness until raw unknown fields, comments, quoting, numeric representation, and source order have an explicit preservation contract.

## Fixtures and tests

Content search found 333 repository files with a V1 `firmament:` root across `testdata/firmament`, the older FrictionLab case corpus, and one parser-backed `fixtures/Firmament` fixture. No committed JSON-shaped V1 source fixture was found by the same structural search; JSON coverage lives mainly as inline test strings.

| Group | Classification | Future treatment |
|---|---|---|
| parser/shape, formatter, JSON acceptance tests (`FirmamentScaffoldTests`, `FirmamentFormatterTests`) | compatibility contract | retain under explicitly named V1 codec tests; add cross-codec model equivalence cases |
| validator/selector/placement/schema/PMI tests | historical regression plus legacy behavior contract | keep opt-in; promote only current V2 concepts into V2 tests |
| primitive/Boolean/lowering/execution/STEP tests | historical regression and educational examples | keep until caller migration; preserve topology lessons in Core/recipe tests |
| `expect_*` tests | legacy regression language | keep opt-in, then retire after equivalent test assertions exist outside source syntax |
| `testdata/firmament/examples` | compatibility artifacts | freeze; do not advertise as current authoring examples |
| `testdata/firmament/fixtures` | broad historical regression corpus | freeze and label V1; no new feature syntax |
| `Aetheris.Firmament.FrictionLab/Cases/*/part.firmament` | FrictionLab-only historical inputs | freeze; do not treat as product language direction |
| placement-lab `*.toon` files | candidate experiment syntax, not the V1 document grammar | FrictionLab-only; do not fold into V1 serialization identity |

The project file already excludes the broad V1 test set unless `AETHERIS_RUN_LEGACY_TESTS=1`; this is strong repository evidence that V1 is frozen compatibility/regression rather than the active language.

## Proposed format identity and ownership

- `FirmamentV1ToonDocument`: reader + deterministic compatibility writer over a versioned V1 DTO.
- `FirmamentV1JsonDocument`: reader over the same versioned V1 DTO; writer only if Preview compatibility later proves it is required.
- suggested placement for M2: `Aetheris.Kernel.Firmament.Serialization.V1` initially, avoiding an assembly move; a later package split can follow usage evidence.
- semantic migration target: an explicit result such as `LegacyFirmamentImportResult` containing source format/version, normalized legacy document, diagnostics, provenance, and optional V2-safe data. It must never manufacture Concepts, Templates, Modules, Interfaces, Mates, or semantic feature meaning.

## Unsafe migration fields

Do not infer current meaning from:

- sequential `ops` order and `from`/`to`/`with` references;
- V1 selector strings or port contracts;
- origin/selector/around-axis placement shortcuts;
- schema process fields and their historical enforcement behavior;
- `expect_*` operations (tests, not product intent);
- automatically derived PMI or legacy target strings;
- operation IDs that were stable only within one flat V1 execution document.

These may be preserved verbatim or imported as legacy evidence. A migration may produce V2 only where the old record contains enough explicit information and the mapping is exact.
