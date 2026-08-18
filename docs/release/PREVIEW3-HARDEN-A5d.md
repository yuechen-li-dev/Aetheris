# PREVIEW3-HARDEN-A5d — Firmament Template metaprogramming reconciliation

## Executive verdict

Yes. Firmament Templates now present as bounded typed generic engineering specialization: canonical declarations and specializations use angle brackets, types and engineering values are checked before materialization, named `Require` constraints reject inadmissible specializations, and Forge exposes a language-neutral generic contract without exposing compiler AST.

This remains deliberately smaller than C++ template metaprogramming. There are no text macros, arbitrary source generation, recursive compile-time programming, higher-order Templates, LINQ/query syntax, or general database access.

## Canonical syntax

Current source uses `Template<...>`. Product-family Templates retain an explicit output declaration, for example `Template<Spec: PlateSpec>` followed by `Struct PlateFamily { ... }`; the output kind is semantic, not inferred. Bounded feature Templates use `Template<spec: MountSpec> MountHole { ... }` and specialize as `MountHole<Current>`.

The former feature-helper spelling `Template MountHole(MountSpec spec)` remains a warning-free compatibility alias on the same finite expansion path. Historical lowercase process templates remain compatibility inputs, now adapted to the canonical DFM representation; new CNC, FDM, and SheetMetal authoring uses typed policy families. `Template<>` remains valid and cycle-tested, though an ordinary `Struct` is clearer when no specialization input exists.

## Verified capability inventory

The authoritative modern path is `FirmamentV2TemplateExpansion`: immutable declaration/parameter/application IR, typed binding, Record flattening, finite enum Match selection, `Require` evaluation, recursion/cycle detection, deterministic identity, source expansion, then the ordinary domain parser and materializer. `FirmamentTemplateHostBridge` projects that IR to stable Forge metadata. `CanonicalStaticAuthoring` owns the smaller finite feature Template/Static array/Pattern path. The older process metadata parser and Pocket minimum-floor compatibility lookup are separate bounded legacy owners, not modern generic specialization.

| Parameter kind | Status | Syntax/checking | Defaults | Forge.Host |
|---|---|---|---|---|
| `Length` / `Angle` | Supported | `Name: Length` (`mm`), `Name: Angle` (`deg`) | yes | dimension and canonical unit |
| `Int` / `Float` / `Bool` / `String` | Supported | typed literal; lowercase scalar aliases remain accepted | yes | JSON integer/number/boolean/string |
| declared enum | Supported | `Variant: EnumName`; case checked | yes | allowed values |
| Record | Supported | `Spec: RecordName`; immutable Static Record, nested fields checked | yes | JSON object with field schema |
| semantic/type parameter | Supported | `type T satisfies Concept`; actual Concept conformance | no | category `type` and constraint |
| `Version`, `Date` | Supported binder values | canonical literal forms | yes | string transport |
| `ImportedStep` | Supported bounded host seam | typed resource token | yes | host resource transport |
| `ProfilePath` | Supported profile-delta seam | qualified path | yes | typed string boundary |
| material identity | Not a distinct generic type | consumed through the existing domain/Forge material route | — | no catalog query API |
| Force/other dimensions | Unsupported | no binder type | — | — |
| array/list or Table | Unsupported as direct product parameters | use finite Static/Pattern data or select a Table row into a Record | — | — |
| Profile, feature kind, or Template | Unsupported | no higher-order parameter surface | — | — |

The complete public matrix and notes are in [Templates: typed engineering specialization](../public/firmament/templates.md).

## Record, Concept, Require, `with`, Static, Table, and Pattern doctrine

- Parameter types own literal, structural, and dimensional admission.
- `Record` owns grouped immutable engineering-data shape.
- A language `Concept` owns structural semantic contracts; `Concept Struct` owns non-materialized semantic values. Forge C# runtime concept descriptors remain a separate extension boundary.
- `Require Name => BooleanExpression` owns specialization-specific admissibility and runs before geometry materialization.
- `with` derives a checked immutable Record; it is not inheritance, mutation, or a macro.
- `Static` binds finite compile-time data; `Table` holds finite checked rows and keyed/index lookup; `Pattern ... Over` performs bounded admitted feature expansion.
- `Struct`, `Compose`, and `Modify` keep construction, profile-body creation, and post-construction semantic-operation ownership after specialization.

Recursive and mutually recursive specialization cycles are rejected. Templates are not first-class values. Acyclic nested specialization may only produce the existing admitted concrete declaration families. Rich algorithms, I/O, database queries, joins, optimization, and open-ended computation remain C#/Forge work.

## DFM and manufacturing reconciliation

The historical `.firmfixture` forms `template<CNC>`, `template<FDM>`, and `template<SheetMetal>` are preserved as migration evidence. Their canonical ports are buildable policy families under `fixtures/Templates/Canonical/`: each uses a typed Record, immutable Static defaults, `with` overrides, a policy-parameterized product Template, named `Require` checks, and a typed policy Concept Struct. `Int`, `Float`, and `Bool` are now accepted in canonical Record schemas alongside their lowercase compatibility aliases.

Canonical CNC and Additive policy Concept Structs now feed the real DFM enforcement paths; the lowercase templates are adapted only when no canonical policy exists. Boss/Pocket keep their existing A3b DFM ownership. Pocket's local `MinimumFloorThickness` remains the final explicit feature policy, followed by canonical CNC floor policy, canonical wall policy, then the historical compatibility lookup. Sheet Metal product-family policy continues to use ordinary `Template<Spec: ...>` Records, enum relief policy, and `Require`. Material catalog querying stays in C#/Forge; specialization consumes semantic identity or resolved data through existing paths.

## Output and domain boundary

Modern product specialization currently admits `Struct`, `Model`, `Concept Struct`, `Panel`, `SheetMetal`, and `ProfileDelta`. Finite feature Templates admit the implemented Shaft Hole, capsule/rounded-rectangle Slot, Profile, and StandardPart outputs. Existing semantic values can affect existing PMI and Analysis contained in a concrete output, but Preview 3 has no separate generic Drawing/Analysis output family, no new PMI kinds, no new physics, and no new geometry.

## Forge.Host Protocol v1

Stable IDs are unchanged: for example `Standard.SheetMetal.ElectronicsEnclosure`. `DescribeTemplate` now adds the human-readable `ElectronicsEnclosure<Spec: EnclosureSpec>` signature, output kind, parameter category, language Concept constraint, and named `Require` constraints to the existing units/dimensions, required/default state, enum cases, nested Record fields, documentation, and artifact list. This is additive Protocol v1 data; invocation identity does not depend on display spelling.

`list`, `describe`, and Python invocation were exercised against the production host. Python produced formed STEP, flat STEP, and SVG for the stable enclosure ID with specialization `template:94546352d5d67afa`; the artifact hashes were `51aa1f...3223`, `832775...cff`, and `1657e3...519` respectively.

## Flagship examples

The new build-qualified [`generic-mounting-plate.firmament`](../../fixtures/Templates/Canonical/generic-mounting-plate.firmament) specializes four typed dimensions, checks them, resolves a compile-time mounting point, lowers a semantic shaft Hole, exports STEP, and reimports it. The CNC, FDM, and SheetMetal canonical DFM fixtures port the historical process data into copy-ready typed families; the CNC fixture is enforcement-tested against a real semantic Hole. The existing current corpus supplies Record/`with`/Table profile specialization, finite patterned holes and slots, ProfileDelta specialization, Assembly and Sheet Metal product families. The public [engineering product-family showcase](../public/firmament/template-examples.md) organizes mounting, process/DFM, enclosure, table, pattern, semantic-type, and Forge examples by engineering purpose rather than parser tricks.

## Fresh-agent mental-model test

The first clean-room agent saw only `docs/public/` and current canonical fixtures. It immediately classified Template as generic compile-time engineering specialization and never inferred text/web templating. It found two real usability gaps: no single product Template plus semantic-Hole fixture, and no inline Python Protocol v1 request envelope. Both were repaired. A final restricted re-probe then answered all four authoring tasks without implementation access: generic mounting plate 0.97 confidence, Record/`with` configurations 0.94, explicit DFM policy 0.84, and exact Python Forge invocation 0.99. Its only caution was that public docs intentionally promise bounded comparisons/booleans rather than arbitrary arithmetic in `Require`.

## Diagnostics

Missing, unknown, wrong-type, Concept-constraint, non-boolean `Require`, and failed-`Require` diagnostics now include the expected generic Template signature while preserving their stable diagnostic prefixes. Existing diagnostics cover Record type/field/membership errors, `with` base/field errors, Table schema/key/index errors, missing Templates, defaults/default cycles, and recursive/application cycles. Successful builds retain concise specialization identity, Record arguments, selected finite Match arms, named `Require` results, and generated declaration paths; no unbounded instantiation trace is emitted.

## Validation

- Release build: pass, 0 warnings, 0 errors.
- Focused Template/DFM/canonical tests pass, including five canonical-policy/legacy-compatibility tests and canonical Pocket precedence; Forge.Host: 24/24 pass.
- Full active .NET suite: 3,032/3,032 tests pass after the policy ports.
- Canonical Template CLI corpus: the migrated fixtures and new mounting Template validate; CNC, FDM, and SheetMetal policy families each build STEP with resolved typed policy Concept IR, specialization provenance, semantic Hole evidence, and STEP reimport success.
- Forge `list`/`describe`/`invoke`: pass; Python foreign-language invocation: pass.
- VS Code: TSPack format/check, typecheck, 13 tests, build, and VSIX package pass. Known acknowledged dependency-version and blocked lifecycle-script notices remain.
- Cadmata: TSPack check, typecheck, 81 tests, build, and lint pass with the same acknowledged notices.
- NuGet CLI pack, NativeAOT Forge publish, Windows x64 ZIP, extracted packaged CLI/Forge/fixture smoke: pass.
- Checksums: 3/3 recomputed; two complete release runs produced byte-identical hashes.
- `git diff --check`: pass.

## Release artifacts

Generated outputs remain ignored under `artifacts/local/a5d/release/`.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| Windows x64 ZIP | 107,820,280 | `00886c8c646e3118760b02bd5f3cb9439e9c013f75a7b92f762d17d4a8d10eef` |
| Firmament VSIX | 12,480 | `05b58a6a06a7d77637166f0fba3c5ec1bbfe274297e83789d338ada3d5494a7f` |
| CLI NuGet package | 33,416,699 | `4cbe40706d2d9827e0df1c8ac52d7ceff293c40267559976d364cd44f4005f30` |

Repeated release packaging exposed and fixed one convergence issue: a pre-existing VSIX in `dist/` could be selected as input while `vsce` overwrote it. The release script now removes that exact generated file before packaging; consecutive full releases are byte-identical.

## Remaining limitations

Direct array/Table parameters, Force and arbitrary quantity parameters, material catalog queries, Template parameters, higher-order specialization, recursion, arbitrary source generation, and general query/algorithm syntax remain unsupported. Feature Templates are a smaller bounded output family than product Templates. FEA and PMI can consume specialized existing intent but have no new standalone Template output contract. Historical lowercase manufacturing syntax remains compatibility-only; canonical typed policy structs are authoritative.

## Feature freeze

Preview 3 feature freeze remains intact. A5d reconciles syntax, generic metadata, diagnostics, documentation, fixtures, tooling, and release reproducibility around existing capability. It adds no geometry, Sheet Metal operation, hole family, FEA physics, PMI kind, Forge verb, database query language, or general-purpose programming construct.
