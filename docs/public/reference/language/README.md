# Firmament language reference

This reference describes Firmament as the compiler accepts it today. It assumes
you have worked through the [lessons](../../learn/README.md), or at least read a
few complete examples. For a domain such as sheet metal or piping, the
[guides](../../README.md#guides) are the authority; they build on what is here.

| Page | Covers |
|---|---|
| This page | constructs at a glance, names and casing, values and units, layout |
| [Compile-time data and choices](data-and-choices.md) | `Record`, `Static`, `Table`, `with`, scope, `Options` and `match`, `Set` |
| [Requirements and interfaces](requirements.md) | `tol`, `Concept`, `Require`, `Semantic`, `Path` |
| [Profiles and 2D construction](profiles.md) | layouts, `Profile`, loops and `|>`, 2D shape families, frames and selectors |
| [Solids and features](solids-and-features.md) | primitives, `Modify`, `Compose`, holes, edge finishes, imported-geometry regions |
| [Reuse](reuse.md) | `Feature`, `Function`, `Pattern`, `Template`, mirror and radial symmetry |
| [Domains and modules](domains-and-modules.md) | `schema`, domain entry points, `BodyState`, `MeshState`, `Inventory`, `include` |
| [Assemblies and motion](assemblies-and-motion.md) | `Interface`, `Mate`, shared datums, joints, `Motion` |
| [Checks and measurement](checks.md) | `Assert`, `measure`, tolerance stack-ups, what `validate` and `build` check |
| [Reading older source](compatibility.md) | compatibility spellings and known inconsistencies |

## The rule that matters most

Firmament describes a finished artifact, and the compiler either produces what
you described or tells you why it cannot. An unknown field, an unresolved name,
an unsupported combination or a requirement that fails always rejects, with a
diagnostic pointing at the source. A successful build never means "built
something close". If you find a case where source is silently ignored, it is a
bug.

## Constructs at a glance

If you program, think of `Model` as a class that owns one artifact, `Struct` as
reusable construction, `Feature` as a typed function, and `Pattern` as a finite
loop. The analogies stop there: there is no inheritance, no runtime method, no
mutation and no general-purpose computation.

| Construct | What it describes |
| --- | --- |
| `Model` | A standalone part and its construction. |
| `Struct` | Named construction that a consuming artifact specializes or uses, such as an assembly part definition. |
| `Concept Struct` | Construction data that never becomes material: points, guides, frames, semantic members. |
| `Concept` | A compile-time requirement: a structural contract, a Boolean predicate, or a labelled bundle of predicates. |
| `Profile` | Named closed planar boundaries consumed by an extrusion or another feature. |
| `Feature` | A reusable recipe returning one typed result. |
| `Template` | Typed compile-time specialization of a product or bounded recipe. |
| `Record` / `Static` | A data shape / an immutable compile-time value. A Record is not a body. |
| `Options` (or `Enum`), `match` | A finite choice type, and exhaustive selection of one value. |
| `Set<T>` | Named entries of type `T`, available together for repetition. |
| `Semantic` | A named engineering interface: published datums, axes, points or dimensions. |
| `Path` | An ordered construction chain, open or closed. It creates no material. |
| `Pattern` | Finite repetition of construction over a declared source. |
| `Assembly` / `Scene` | Product occurrences and their relations / spatial composition and presentation. |

Use `Model` for an ordinary standalone part. A Struct, a Template library, a
Profile module and an Assembly each have their own entry and consumption rules;
they are not a Model with a different keyword.

## Declarations, fields and names

These are notation sketches, not complete files:

```text
Construct name { field: value }
Construct<Variant> name { field: value }
Feature Recipe(parameter: Type = defaultValue) -> ResultType { ... }
Struct instance = ProductFamily<parameter: value>
```

Braces delimit bodies, brackets delimit lists, parentheses delimit calls, and
`name.member` selects a published member. A colon labels a field, a parameter or
a declared type, depending on context. Declaration bodies, typed values and
Template specializations are distinct forms, not interchangeable object literals.

`<...>` means different things in different places: a built-in variant
(`Hole<Shaft>`), a collection element type (`Set<Point2>`), or Template
parameters and arguments. It does not make every construct generic.

Identifiers and published member names are case-sensitive. Preserve library
names exactly, including named call arguments: `spec.Width` does not become
`spec.width` because your local style is camelCase.

## Casing

| Role | Spelling | Examples |
| --- | --- | --- |
| Constructs and types | PascalCase | `Model`, `Box`, `Feature`, `Length`, `Point2` |
| Built-in fields | camelCase | `units`, `size`, `counterboreDepth` |
| Your geometry, values and parameters | camelCase | `stock`, `outline`, `mountPoint`, `depth` |
| Linking words | lowercase | `using`, `as`, `from`, `to`, `on`, `over`, `with`, `include`, `expose`, `bind`, `match` |
| Engineering symbols and enum values | as the domain spells them | `XY`, `+Z`, `Outer`, `ThroughAll`, `World`, `mm` |
| Geometry operations in pipelines | PascalCase | `Reverse`, `Close`, `TraceLoop` |

Most built-in fields also accept a PascalCase spelling (`Diameter` for
`diameter`), and older source uses it widely. New source should use camelCase.
For acronym fields write `pmi` and `bom`. This is not case-insensitive parsing:
the alias rule applies to built-in field names and linking words in their
admitted positions, never to your own names.

Some words appear both as a header clause and as a field.
`Concept Struct layout on XY` selects the plane of a layout; `on: +Z` inside a
Hole selects its support face. They are different positions with different
types. [Language style](../../firmament/language-style.md) describes what the
formatter changes, and the [cookbook](../../../../fixtures/Canonical/README.md)
shows the conventions applied across every canonical example.

## Values and units

Write units on every dimension in new source: `8mm`, `90deg`,
`Point2(12mm, -4mm)`. Bare numbers are for dimensionless counts and vector
components. `units: mm` belongs in an ordinary mechanical Model; it does not make
units optional in every field.

| Quantity | Units |
| --- | --- |
| `Length` | `mm` |
| `Angle` | `deg`; `rad` where a field documents it |
| `Time` | `s`, `ms` (motion only) |
| Volume (assertions) | `mm^3` |

Mixed degree/radian arithmetic needs an explicit conversion; the compiler will
not add the raw numbers. `Sin` and `Cos` take radians as a bare number, or an
angle with explicit units, and return a dimensionless value.

Planar points such as a Hole's or Rect2's `center` accept both `Point2(x, y)` and
`[x, y]`, with length coordinates. Arithmetic with dimensional checking is
available in admitted value positions, but not in every field of every domain;
the consuming guide says where.

For a hole on the top face of its host, `+Z`, `face(+Z)`, `Top` and `host.Top`
name the same support. The name of an unrelated body is not an alias for it.

## Layout, comments and terminators

Use two-space indentation. Put fields on separate lines, or separate them with
semicolons in a short body. Separate list elements and call arguments with
commas. Do not rely on the parser to split dense fields that run together.

Comments are `//` and `/* ... */`. `#` is not a comment marker.

A few specialized forms **require** semicolons: scalar `Function` expressions,
point-recipe fields, PMI `Note` fields and assembly root-provenance statements.
Their guides show them. There is no single statement-termination rule yet.
