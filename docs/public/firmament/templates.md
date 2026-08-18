# Templates: typed engineering specialization

Firmament Templates do not render text, documents, web pages, or source files. They specialize typed engineering models at compile time.

The useful mental model is generic engineering programming: one finite definition accepts typed parameters, checks admissibility, and becomes an ordinary concrete Firmament declaration before geometry or another domain artifact is materialized. Firmament borrows that mental category from C++ templates, but intentionally omits arbitrary recursive metaprogramming, macros, open-ended algorithms, and C++-style instantiation machinery.

## Canonical syntax

Angle brackets are mandatory in canonical V2 source. A product-family Template declares parameters before its output declaration:

```firmament
Template<Width: Length, Height: Length, Thickness: Length>
Struct MountingPlate {
    Require Positive => Width > 0mm && Height > 0mm && Thickness > 0mm
    Box Plate { Size: [Width, Height, Thickness] }
}

Struct StandardPlate = MountingPlate<Width: 80mm, Height: 50mm, Thickness: 8mm>
```

The complete build-qualified version is [`generic-mounting-plate.firmament`](../../../fixtures/Templates/Canonical/generic-mounting-plate.firmament).

The explicit output keyword is significant: current Templates specialize `Struct`, `Model`, `Concept Struct`, `Panel`, `SheetMetal`, or `ProfileDelta` declarations. It is not inferred from the body.

A finite feature Template used by `Pattern ... Over` has the same generic visual form:

```text
Template<spec: MountSpec> MountHole {
    Hole<Shaft> Mount {
        On: +Z
        Center: spec.Center
        Diameter: spec.Diameter
        End: ThroughAll
    }
}

Pattern MountPattern Over Mounts { MountHole<Current> }
```

The older `Template MountHole(MountSpec spec)` and `MountHole(Current)` spellings remain warning-free compatibility aliases. Current docs, fixtures, and snippets use angle brackets.

`Template<>` is valid. It exists for uniform specialization and compatibility, but a declaration with no variable engineering input is usually clearer as an ordinary `Struct`.

## Parameters

Value parameters use `Name: Type`. Defaults use `Name: Type = Value`. A real semantic/type parameter is explicit:

```text
Template<type TBody satisfies PrismaticBody, Width: Length, Variant: BracketVariant = Standard>
Struct BracketFamily {
    // existing construction
}
```

`type TBody satisfies PrismaticBody` is not an ordinary string-valued choice. The binder verifies that the supplied declaration satisfies the named language `Concept`. All other current parameters are typed compile-time values.

| Parameter kind | Preview 3 | Declaration | Checking and defaults | Forge.Host |
|---|---|---|---|---|
| `Length` | Supported | `Width: Length` | `mm`; defaults supported | dimension `length`, unit `mm` |
| `Angle` | Supported | `Draft: Angle` | `deg`; defaults supported | dimension `angle`, unit `deg` |
| integer | Supported | `Count: Int` | integer literal; defaults supported; lowercase alias accepted | JSON integer |
| number | Supported | `Ratio: Float` | finite numeric literal; defaults supported; lowercase alias accepted | JSON number |
| boolean | Supported | `Enabled: Bool` | `true` / `false`; defaults supported; lowercase alias accepted | JSON boolean |
| string | Supported | `Label: String` | quoted literal; defaults supported | JSON string |
| enum choice | Supported | `Variant: BracketVariant` | declared cases; defaults supported | allowed values listed |
| `Record` | Supported | `Spec: BracketSpec` | immutable `Static` value; nested fields checked | JSON object and field schema |
| `ImportedStep` | Supported at host seam | `Source: ImportedStep` | typed host resource token | transported as a host resource |
| `ProfilePath` | Supported in the profile-delta lane | `Owner: ProfilePath` | qualified semantic path | typed string boundary |
| semantic/type parameter | Supported | `type T satisfies Concept` | language Concept conformance; no default | category and constraint exposed |
| material reference | Not a distinct Template type | use a supported semantic material identity in the consuming domain | resolution stays in Firmament/Forge/C# | no catalog query surface |
| arrays/lists | Not a direct product-Template parameter | consume finite `Static` arrays through feature Templates/Patterns | element Record checked | not exposed as a parameter |
| `Table` | Not a direct parameter | select a row into a `Static Record` first | key and row checked | selected Record can cross the host seam |
| `Force` and other quantities | Not supported by the generic binder | — | `Length` and `Angle` are the admitted dimensions | — |
| `Profile`, feature kind, or Template | Not supported as generic parameters | — | no higher-order Templates | — |

This matrix describes the compiler today. It is not a roadmap.

## Records, `with`, Static, Tables, and Patterns

`Record` defines grouped engineering-data shape. `Static` binds immutable compile-time data. `with` derives a new Record value by replacing checked fields; it is neither inheritance nor mutation. A keyed `Static Table` is finite engineering data, and a Table lookup produces a Record that can be passed to a Template. `Pattern ... Over` expands a finite static collection into admitted semantic features.

```text
Record EnclosureSpec {
    Width: Length
    Height: Length
    WallThickness: Length
}

Static Desktop: EnclosureSpec = EnclosureSpec {
    Width: 180mm
    Height: 60mm
    WallThickness: 1.5mm
}

Static Rugged = Desktop with {
    WallThickness: 2mm
}
```

The coherent flow is: Records, Static values, and Tables define finite data; `with` derives configurations; a Template specializes from them; `Require` rejects inadmissible combinations; Pattern expands finite features; the resulting ordinary declaration follows the normal compiler and materializer path.

## Concepts and Require

A language `Concept` is a compile-time structural semantic contract. `Concept Struct` is a non-materialized typed semantic value. This differs from Forge C# runtime concept descriptors, even though both carry engineering semantics.

Parameter types own structural and dimensional checking. Records own grouped data shape. Concepts own semantic conformance. `Require Name => BooleanExpression` owns specialization-specific admissibility. A failed `Require` stops specialization before geometry materialization and records the named check in specialization provenance.

`Require` is finite: comparisons and bounded boolean combinations over specialized data are supported. It is not a runtime script or a general constraint solver.

## DFM policy families

An LLM-friendly DFM family has four ordinary pieces: a named policy `Concept`, a typed policy `Record`, one immutable `Static` default plus optional `with` variants, and a product `Template<Policy: ...>` that materializes one policy `Concept Struct`. The LLM normally changes only the Static data or selects a derived policy.

Canonical policy contracts are `CncManufacturingPolicy`, `FdmManufacturingPolicy`, `AdditiveManufacturingPolicy`, and `SheetMetalManufacturingPolicy`. The CNC and additive contracts are consumed by the existing DFM enforcement paths. CNC Pocket policy precedence is explicit: a Pocket-local `MinimumFloorThickness` wins, then the canonical CNC policy's floor value, then its wall value. FDM and sheet-metal policy structs are typed portable family data for their consuming domains.

Copy-ready, CLI-validated ports of the historical policy examples are:

- [`cnc-dfm-policy.firmament`](../../../fixtures/Templates/Canonical/cnc-dfm-policy.firmament)
- [`fdm-dfm-policy.firmament`](../../../fixtures/Templates/Canonical/fdm-dfm-policy.firmament)
- [`sheet-metal-dfm-policy.firmament`](../../../fixtures/Templates/Canonical/sheet-metal-dfm-policy.firmament)

Persisted lowercase `template<CNC>`, `template<FDM>`, `template<Additive>`, and `template<SheetMetal>` sources remain compatibility inputs. When a canonical typed policy is present it is authoritative; new source should not emit the lowercase form.

## Termination and composition

Templates may not recursively specialize themselves, directly or through a cycle. Nested specialization is only useful when it is acyclic and lowers to the admitted concrete declarations; Templates are not first-class values, cannot be passed as parameters, and have no higher-order or recursive programming surface.

The practical complexity ceiling is deliberate:

- Use Firmament for finite declarative product families, typed configuration, dimensional checks, bounded semantic choice, and deterministic repetition.
- Use C# or Forge for algorithms, external I/O, material-database queries, joins/grouping, large search or optimization, and open-ended computation.

Firmament has no `Select`, `Where`, `GroupBy`, joins, query comprehensions, SQL surface, or arbitrary source generation. C# stays C#.

## Output boundary

The modern generic specializer admits `Struct`, `Model`, `Concept Struct`, `Panel`, `SheetMetal`, and `ProfileDelta` output declarations. Finite feature Templates admit the currently documented `Hole<Shaft>`, capsule/rounded-rectangle `Slot`, `Profile`, and `StandardPart` outputs. Templates can thereby influence existing geometry, Sheet Metal, semantic PMI inputs, and an Analysis contained in an admitted concrete model, but Preview 3 has no separate generic FEA or Drawing output contract and no new PMI feature kinds.

`Struct`, `Compose`, and `Modify` keep their ordinary meanings after specialization: `Struct` owns construction intent, `Compose` creates an admitted profile-based body, and `Modify` applies admitted post-construction semantic features. Template is not a geometry subsystem.

## Forge.Host

Protocol v1 keeps stable IDs such as `Standard.SheetMetal.ElectronicsEnclosure` separate from display signatures such as `ElectronicsEnclosure<Spec: EnclosureSpec>`. `ListTemplates` discovers stable IDs. `DescribeTemplate` exposes the signature, output kind, parameter category, units/dimensions, required/default state, enum cases, nested Record fields, named `Require` constraints, documentation, and artifacts. `InvokeTemplate` binds language-neutral values and returns deterministic artifacts and specialization identity; it never exposes compiler AST nodes.

Syntax cleanup therefore does not rename a public Template. See [Template engineering examples](template-examples.md) and the [Forge Host guide](../forge/interop.md).
