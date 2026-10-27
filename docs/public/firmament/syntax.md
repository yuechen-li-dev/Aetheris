# Firmament syntax

The material that used to live on this page is now split up:

- **New to Firmament?** Work through the [lessons](../learn/README.md). Six short
  lessons, each with a complete example that the test suite builds.
- **Looking something up?** Use the [language reference](../reference/language/README.md).

## The language in one screen

```firmament
Model Plate {
  units: mm
  Profile outline = Rect2 { center: [0mm, 0mm]; size: [80mm, 50mm] }
  Extrude stock { profile: outline; from: 0mm; to: 8mm }
  Modify stock {
    Hole<Shaft> mount { on: +Z; center: [0mm, 0mm]; diameter: 8mm; end: ThroughAll }
  }
}
```

- A `Model` is one part with one material body.
- Constructs and types are PascalCase (`Profile`, `Rect2`, `Hole<Shaft>`); fields,
  your names and linking words are camelCase or lowercase.
- Every length carries its unit.
- Construction geometry (a `Rect2`, a layout) is not material until something
  such as `Extrude` consumes it.
- Anything the compiler cannot build exactly as written is rejected with a
  diagnostic. Nothing is silently skipped.

| Topic | Reference |
| --- | --- |
| Constructs, names, casing, units | [Overview](../reference/language/README.md) |
| Records, Statics, Tables, `match`, Sets | [Compile-time data and choices](../reference/language/data-and-choices.md) |
| Tolerances, `Concept`, `Require`, `Semantic` | [Requirements and interfaces](../reference/language/requirements.md) |
| Layouts, Profiles, 2D shapes, frames | [Profiles and 2D construction](../reference/language/profiles.md) |
| Primitives, `Modify`, `Compose`, holes, edge finishes | [Solids and features](../reference/language/solids-and-features.md) |
| `Feature`, `Function`, `Pattern`, `Template`, symmetry | [Reuse](../reference/language/reuse.md) |
| `schema`, `BodyState`, `MeshState`, `Inventory` | [Domains and modules](../reference/language/domains-and-modules.md) |
| Interfaces, mates, datums, motion | [Assemblies and motion](../reference/language/assemblies-and-motion.md) |
| `Assert`, `measure`, stack-ups | [Checks and measurement](../reference/language/checks.md) |
| Explicit GD&T, datum precedence, metric PMI passthrough | [Semantic PMI](pmi.md) |
| Older V1 forms | [Reading older source](../reference/language/compatibility.md) |
