# Reading older source

Firmament has been through two generations. Everything in these docs is
Firmament V2. You will still find older source in the repository, in tests that pin
old behaviour, and in files people send you. This page helps you
read it; do not write it.

## Firmament V1

The historical combination `model` with `units mm`, `solid name: Box`, `modify` and `hole<shaft>` are
V1 forms, still accepted for compatibility. Bare lengths inherit millimetres only in the historical
primitive grammar and domain fields that admit them. Length fields in current
mechanical authoring require explicit units. Historical Piping, Structural,
SectionChain and Set positions may accept bare millimetres; WireForm Length
requires a unit. An explicit angle is never a substitute for a length. This is not a second preferred style.

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

A lowercase `model` wrapping native V2 declarations is a case alias for `Model`;
it does not switch native source into the historical `solid` grammar. Constructs
use PascalCase, built-in fields use camelCase, and connectives such as `using`,
`as`, `on` and `with` use lowercase. Established aliases are normalized at syntax
positions, preserving user-defined names and engineering values.

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

Reusable Templates and Features are recipes: material capability and value checks
run on their concrete specializations. An unused recipe does not prove a
supported materialization. Record members and Concept Struct publications are
authored data, not a fixed built-in field vocabulary. Unknown fields in a
materialized built-in construct are errors, including fields inside WireForm
operations, SectionChain guides, assembly occurrences and published Semantic
blocks. `validate --parse-only` reports a limited validation level; normal
validation performs the owning domain admission and required engineering
preflight, which can take time for swept or spline-heavy geometry.

A WireForm part admits one wire product. Its named operations require field
bodies; unsupported placement or parameter headers reject. Use StartFrame for
the authored frame and an Assembly for separate Parts. Comments never contribute
wire operations or operation provenance.

## Admission limits

Normal validation of a mechanical part runs its engineering build. The build
also checks its materialized feature receipts against the admitted operations:
missing Modify or Compose features reject the build. Pattern instances retain
individual identities; one built instance cannot stand in for another.
`build --json` exposes those receipts as `materializedFeatures` separately from
source-derived display reports.

Field closure does not yet cover every value-object body nested in a published
Concept Struct. For example, an extra field inside an inline `Plane { ... }`
value can currently be accepted without affecting that plane. Use only the
listed fields of these value objects; validation does not yet prove that their
entire bodies were consumed. This differs from adding a legitimate new named
member to an open Concept Struct, which is supported authoring data.

Diagnostics for specialized definitions and expanded Pattern instances use the
original field or declaration when it can be mapped. Otherwise they identify
the enclosing material operation or the specialized definition and disclose
that the offsets describe expanded source.
