# Firmament V2 active grammar inventory

This A5c inventory records parser and lowering ownership. Canonical forms use PascalCase; compatibility aliases are deliberately bounded rather than produced by global case folding.

| Construct / token | Owning parser/domain | Accepted forms | Canonical form | Compatibility policy | Lowering/runtime owner |
|---|---|---|---|---|---|
| Model, Units | `FirmamentV2Parser` | `Model`/`Units: mm`; legacy lowercase document route; lowercase field alias in a canonical root | `Model`, `Units: mm` | retain audited lowercase inputs; no warning | normalized `FirmamentV2Document` |
| Box, Cylinder, Cone, Sphere, Torus, RoundedBox, Frustum | canonical primitive binder plus legacy solid adapter | direct named declarations; legacy `solid name: Type` | direct named declaration | retain `solid` compatibility; both form the same primitive record | `FirmamentV2BuildLowering` and production primitive executor |
| Concept, Struct, Profile, Loop, Segment | canonical advanced parser with profile adapters | qualified PascalCase forms | PascalCase forms in canonical fixtures | no new aliases | Concept IR, `ProfileAuthoringParser`, profile materializers |
| Record, Static, Table, Template, Require, `with`, Pattern/Over | Template/static frontends | PascalCase constructs; lowercase `with` operator | shown qualified forms | `with` is an operator, not snake_case vocabulary | `FirmamentV2TemplateExpansion`, `CanonicalStaticAuthoring`; erased before material lowering |
| Struct, Compose, Base, Add, Remove, Boss, Pocket | prismatic composition parser | qualified Compose placements; legacy low-level Add/Remove | `Boss`/`Pocket` for their first-class contracts; Base/features inside Compose | Add/Remove remain bounded lower-level compatibility | `PrismaticProfileCompositionParser` and section-stack materializer |
| Modify, Hole family, Slot, EdgeFinish | canonical primitive or profile semantic binders | qualified top-level/body-adjacent Modify; Compose-owned holes/slots where admitted | post-construction operations in `Modify`; construction-owned operations inside active `Compose` where the host parser requires it | multiple placements are retained only where runtime ownership differs | semantic hole/edge finish lowering and profile composition |
| Pmi, Datum, HoleDiameter | Model PMI binder | PascalCase canonical records; bounded lowercase compatibility records | `Pmi`, `Datum`, `HoleDiameter`, PascalCase fields | retained aliases must bind to identical semantic PMI | Firmament semantic values and STEP AP242 exporter |
| SheetMetal, Base, Flange, Hole | `AuthoredSheetMetalCompiler` | specialized PascalCase grammar | domain form shown in Sheet Metal guide | Model `Hole<T>` is rejected, not aliased | Sheet Metal IR, flattener, manufacturing artifacts |
| Analysis, Fixed, Force | `FirmamentAnalysisCompiler` | case-insensitive audited keywords/fields; ordinary Model Box or bounded inline STEP | PascalCase vocabulary over ordinary Model geometry | lowercase forms bind the same Analysis IR | Continuum region producer and linear-elastic solver |
| inlineSTEP / InlineStep | FEA expression reader / canonical import declaration | bounded `inlineSTEP("path")`; `InlineStep Name { Path: ... }` | declaration form in a Model; expression spelling retained in FEA | domain-specific compatibility | STEP importer and recognized-region bridge |
| Assembly | `FirmamentAssemblyDocumentProfile`, `AssemblyM0Parser` | V2 `Assembly Name { ... }`; deprecated JSON-shaped `.firmasm` | V2 Assembly source profile | JSON is legacy compatibility only | Assembly compiler/executor/AP242 interop |
| Drawing | `FirmamentDrawingCompiler` | qualified Drawing declarations | PascalCase qualified fixture form | separate domain grammar | Drawing IR and SVG/PDF/PPTX writers |

## Semantic ownership matrix

| Construct | Meaning and placement | Geometry effect | Owner |
|---|---|---|---|
| Template | typed compile-time specialization over values/Records | none directly; emits ordinary admitted source | Template expansion |
| Record / Static / Table / `with` | immutable finite engineering data and derivation | none; erased after binding | static authoring and Template expansion |
| Pattern / Over | finite repetition from static data | repeats an admitted feature | static authoring into the owning material grammar |
| Struct | named construction intent or concept materialization | contains the selected construction route | concept/profile frontend |
| Compose / Base | constructive body and its initial material interval | creates the prismatic host | profile composition |
| Add / Remove | bounded low-level section-stack operations | adds/removes finite profile intervals | profile composition compatibility surface |
| Boss / Pocket | first-class connected additive / finite non-through removal contracts | adds a Boss or removes a Pocket while preserving its engineering rules | profile composition semantic features |
| Modify | post-construction operations against an existing admitted body | applies semantic holes/finishes or other qualified mutations | canonical primitive/profile binders |
| Hole / Slot | typed opening feature; placement depends on owning host | removes admitted material | semantic hole/slot lowering |
| EdgeFinish | qualified semantic finishing operation | admitted chamfer/fillet routes only | EdgeFinish binders/materializers |
| Pmi | engineering requirements over the semantic model | no geometry; emits semantic AP242 | PMI binder/exporter |

## Through and extent types

`End: ThroughAll` is the canonical Model Hole termination condition. Slot `Extent: ThroughAll` is a slot interval. Legacy `through: face(-X)` is an exit-face selector in the old Region/Cut route, while `through: true` belongs to recognition evidence or speculative/legacy inputs depending on owner. Sheet Metal openings terminate through their planar sheet region by domain semantics. These values are not unified because their AST types differ.

## Intentional domain differences

Model and Sheet Metal Hole syntax, Model faces and Sheet Metal planar regions, native and imported STEP face identity, Assembly typed relationship selectors, and external material/standard identifiers are `IntentionalDomainDifference`. V1 and JSON `.firmasm` inputs are compatibility history; future/not-implemented `.firmfixture` bodies are speculative evidence.
