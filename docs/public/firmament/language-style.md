# Firmament V2 language style

Firmament-owned vocabulary is canonically PascalCase. This includes document and declaration constructs, semantic features, built-in primitive and value names, and built-in fields. Canonical source therefore uses `Model`, `Units`, `Sphere`, `Modify`, `Analysis`, `Body`, `Region`, and `ThroughAll`.

User-defined identifiers are case-preserving and stylistically free. `beam`, `Beam`, `mainDeck`, and `MAIN_DECK` may all be names when the owning construct admits an identifier. External identifiers preserve their engineering or source spelling, including material designations, standards, imported identities, and part numbers such as `5052_H32`.

Lowercase and historical aliases may remain accepted for compatibility when unambiguous. They receive no style warning and must not select different semantics. Documentation, snippets, generated examples, and `fixtures/Canonical/` use the canonical spelling.

Semicolons are optional wherever newline/block structure already delimits a field. Use them when they improve readability in dense one-line Records, Tables, or `ProfileDelta` members; their presence must not select a different language path.

Casing is not a type-system distinction unless a domain explicitly says otherwise. A field uses `Name: Value`; braces delimit declarations, brackets delimit lists, and semicolons are optional where the owning grammar is unambiguous.

Different target grammars are intentional. Native Model geometry uses axis faces and semantic selectors; Sheet Metal uses named planar regions and paths; native and imported Analysis use body-qualified faces; Assembly uses typed roles, ports, interfaces, and DatumFrame references. Similar engineering ideas do not make these value types interchangeable.

Firmament V1 is compatibility history, not canonical V2 authoring. Speculative or expected-failure `.firmfixture` bodies do not define the public language.

## Preview 3 migration note

Preview 3 documents one visual dialect. Existing supported lowercase and `solid name: Primitive` inputs remain accepted where documented, without casing warnings. New source should use PascalCase Firmament vocabulary, direct named primitive declarations, and `Units: mm`.
