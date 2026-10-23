# Firmament V2 language style

Firmament uses mixed casing to make artifact definitions stand out from their
descriptions. Preferred casing does not rename authored identifiers; admitted
built-in spellings and aliases are defined by their compiler owners, without
global case folding. The [design proposal](../../development/proposals/casing-and-layout.md) records the reasoning.

| Role | Convention | Examples |
| --- | --- | --- |
| Artifact, declaration, construction form | PascalCase | `Model`, `Assembly`, `Scene`, `Room`, `Door`, `Window`, `Camera`, `Concept Struct`, `Profile`, `Feature`, `Placement`, `Mate` |
| Type and reusable recipe | PascalCase | `Length`, `PickupSpec`, `Humbucker`, `ControlKnob` |
| Concrete value, local geometry, occurrence, parameter, port | camelCase | `layout`, `outline`, `neckPickup`, `bottomSeat`, `width` |
| Built-in property label | camelCase | `units`, `size`, `from`, `translateLocal`, `minimumBendRadius` |
| Linking and scope word | lowercase | `using`, `as`, `from`, `to`, `on`, `over`, `with`, `return`, `include`, `expose`, `bind` |
| Scalar function | camelCase | `fretDistance`, `halfWidth` |
| Engineering symbol, enum, external identity | Preserve domain spelling | `XY`, `+Z`, `Outer`, `World`, `Origin`, `Fixed`, `Periodic`, `Stock`, `mm`, `m`, `304_Annealed` |

The root product may keep its definition name: `Assembly GuitarX0` and
`<Assembly GuitarX0>`. Feature constructors such as `Coil` remain PascalCase.
Use two-space indentation, spaces after list commas, blank lines between major
definitions, and separate closing tags for multiline occurrences. Preserve
numbers and units; formatting must not round derived surface controls.

User-defined identifiers and published members remain case-sensitive.
`beam`, `Beam`, `mainDeck`, and `MAIN_DECK` remain valid wherever an identifier
is admitted. Case alone does not assign a semantic type. Material designations,
standards, imported identities and part numbers retain their source spelling.
Existing PascalCase properties and supported historical aliases remain accepted,
without style warnings. Older examples need not be rewritten wholesale.

The table describes preferred style, not universal case folding. Profile endpoint
and mirror-header links prefer `from`/`to`, with `From`/`To` retained as aliases.
Mirror/radial fields prefer `across`, `source`, `about`, `count`, `angle`; their
PascalCase aliases bind to the same AST fields. Unknown and duplicate fields
reject. `Reverse`, `Close` and `TraceLoop` remain PascalCase geometry operations.
The formatter recognizes these linking/field positions structurally and preserves
authored identifiers. It does not impose this grammar on unrelated domain owners;
the [syntax review](../../development/reports/2026-10/FIRMAMENT-SYNTAX-REVIEW-2026-10.md) tracks the
remaining differences. Formatting is still not proof of successful compilation.

```firmament
Feature Coil(center: Point2, width: Length, depth: Length,
             height: Length, support: Plane) -> Boss {
  RoundedRect2 outline { center: center; size: [width, depth]; radius: 3mm }
  Profile section { Loop Outer { outline |> TraceLoop } }
  return Boss { on: support; profile: section; height: height }
}
```

## Safe formatting and bounded compatibility

`aetheris format <file.firmament|file.firmasm>...` prints preferred vocabulary
spellings; add `--write` to update the explicitly named files, or `--json` for a
change summary. This conservative pass preserves layout, comments, strings,
numeric literals, user names and occurrence identities. It prepares every input
before writing and refuses a rewrite that changes normalized source text.
It is **not validation**: validate/build the consuming assembly after a sweep.

The same pass is available as
`FirmamentLanguageAnalysisService.FormatConventions`. The existing standalone
V2 `Format` API also prefers these spellings while arranging layout, and checks
both token equivalence and parser admission. Imported modules use the conservative
pass and their consuming project's compiler; they are not standalone parts.

Compatibility is scoped to schema-owned properties, AST-position Profile links
and symmetry fields, plus the bounded frame, assembly, point and wire
vocabularies used by the guitar. The parser maps
these aliases back to existing semantic identities. Unknown blocks, Record and
Static data, explicit station keys, named template/Feature arguments and dotted
members are not guessed or renamed by the formatter. Property completion accepts
either prefix spelling; generated entry snippets prefer the new spelling, while
schema/field identities in inspection APIs remain stable.

Historical lowercase `model`/`solid` scopes are compatibility syntax, not the
preferred style. Their primitive solids, immutable Box derivations, exposure
aliases and Hole modifications now bind through the shared AST and existing
engineering owners. The spelling pass leaves this grammar untouched: converting
`solid stock: Box` to `Box stock` also requires making inherited length units
explicit. Remaining PMI, lattice and manufacturing scopes retain their semantic
owners; see the [migration boundary](../../development/reports/2026-10/FIRMAMENT-LOWERCASE-AST-MIGRATION.md).

Use lowercase `using` and `as` in Profile headers and pipelines. `Using` and `As`
remain grammar-position aliases, and convention formatting prefers lowercase.
This does not rename a user-defined member or identifier named `As`.

Authored names require a manual, binding-aware edit. For example, `.Frame`,
`.BottomSeat`, `Spec`, `R` and `H` can be authored public contracts; their casing
must change together with every declaration and use. The guitar sweep deliberately
retains its established public ports, template argument names, occurrence paths
and generated span keys. Its private pickup geometry, record fields and scalar
functions demonstrate the quieter convention. Remaining casing stragglers are
acceptable. An exhaustive migration or symbol rename service is outside this pass.

Current assembly definition identities can retain whitespace in specialization
arguments. Preserve those argument spellings when preserving identities; this
formatter does so. General whitespace canonicalization of definition keys is a
separate compiler concern.

Semicolons are optional wherever the owning grammar already admits newline/block structure as a field delimiter.
The bounded `Function` expressions and `Points` recipes currently require explicit
semicolon terminators; see [Concept points and Functions](concept-points-and-functions.md).
Root assembly `provenance` selections and `Pmi Note` fields also require explicit
semicolons; see [PMI](pmi.md). Use semicolons when they improve readability in dense one-line Records, Tables, or `ProfileDelta` members; their presence must not select a different language path where optional.

Built-in aliases exist only where the owning grammar admits them.
A field uses `name: value`; braces delimit
declarations, brackets delimit lists, and semicolons are optional where the
owning grammar is unambiguous.

Different target grammars are intentional. Native Model geometry uses axis faces and semantic selectors; Sheet Metal uses named planar regions and paths; native and imported Analysis use body-qualified faces; Assembly uses typed roles, ports, interfaces, and DatumFrame references. Similar engineering ideas do not make these value types interchangeable.

Firmament V1 is compatibility history, not canonical V2 authoring. Speculative or expected-failure `.firmfixture` bodies do not define the public language.

## Preview 3 migration note

Preview 3 originally documented PascalCase vocabulary and fields. Its supported
lowercase and `solid name: Primitive` inputs remain accepted where documented.
New examples prefer mixed casing and direct named primitives with `units: mm`.
