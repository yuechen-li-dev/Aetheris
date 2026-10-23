# Reading older source

Firmament has been through two generations. Everything in these docs is
Firmament V2. You will still find older source in the repository, in tests that pin
old behaviour, and in files people send you. This page helps you
read it; do not write it.

## Firmament V1

Lowercase `model`, `units mm`, `solid name: Box`, `modify` and `hole<shaft>` are
V1 forms, still accepted for compatibility. Bare lengths inherit millimetres only in the historical
primitive grammar that admits them. This is not a second preferred style.

Historical immutable Box derivation uses
`solid tall: stock with { size: [30, 24, 20] }`. Exposure uses
`expose { face(+Z) as top }`, with `=> top` also retained there. These forms do
not make `with`, `as` or `=>` universally interchangeable. Serialization-shaped
V1 `model:` and `firmament:` documents (TOON- or JSON-shaped) are a separate legacy
input with their own compatibility path. Historical JSON-shaped `.firmasm` assembly
files are also legacy; current `.firmasm` uses the V2 assembly syntax.

The formatter preserves names, published members and numeric values. It does
not migrate solid grammar into native declarations or prove a program builds.
Validate the consuming artifact after formatting.

## Known inconsistencies

Firmament's preferred style is more consistent than what every domain enforces.
Some differences are real domain distinctions; others are compatibility debt or
bugs that are being worked down. You will still meet differences in:

- field casing (older domain examples and some formatters use PascalCase
  aliases; the compiler accepts camelCase everywhere a built-in field is read);
- points written as `Point2(...)` or as lists;
- Pattern element binders (`point =>`, `Item`, `Current`; see [reuse](reuse.md#pattern-finite-repetition));
- statement terminators (some forms require semicolons);
- which declarations may appear at file, Model or Struct scope;
- frame and selector types (`on` means different things in different places);
- what `<...>` and `with` mean in a given position;
- two distinct SectionChain forms, Drawing policy Concepts and Structural table
  column conventions.

Structural, Sculpting, PlasticShell, Analysis, Drawing and Review share the
mechanical parser. Each still owns its statements and fields, and rejects unknown
fields, duplicate aliases, unknown statements and misplaced headers. Structural,
Sculpting and PlasticShell accept predicate and structural `Concept`
declarations and `Require` checks at their product scopes.

Two rules hold regardless:

- **Your names are data.** Record members, Drawing metadata fields such as
  `PartNumber`, Table column names and published ports keep the spelling you
  gave them. The formatter never renames them, and neither should you when
  restyling.
- **Formatting is not migration.** `aetheris format` changes vocabulary spelling
  and layout only. It does not move source to newer forms and does not prove the
  result builds. Validate after formatting.

Lookup is lexical everywhere: Features, product Templates, Records, Statics and
Tables resolve in their declaring scope, callers bind arguments in their own
scope, duplicates in one scope reject, and sibling-private data is invisible.
Source maps through `include` and Template expansion are incomplete;
`validate --json --explain` shows the scopes, bound arguments and
definition/use links the compiler has.

If you find source that is silently ignored, or a documented form that does not
behave as described, that is a bug. Capability limits are listed in
[supported features](../supported-features.md).
