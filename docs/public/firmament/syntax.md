# Writing Firmament

Firmament describes finite engineering artifacts at compile time. Start with a
`Model`, describe its construction, then validate and build it. Geometry is
materialized from that description; the source has no runtime objects or
interactive program loops.

This page teaches common mechanical authoring, then points to other domains.
**Complete files** can be saved and built on their own. **Fragments** require
the stated enclosing declarations. A form being legal in one domain does not
make it legal in every other domain.

## A first part

**Complete file — a plate with a through hole:**

```firmament
Model Plate {
  units: mm

  Box stock { size: [80mm, 50mm, 8mm] }

  Modify stock {
    Hole<Shaft> mount {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: 8mm
      end: ThroughAll
    }
  }
}
```

`stock` names the body. `Modify stock` applies the hole to it. `Hole<Shaft>`
selects a built-in variant; `ThroughAll` specifies termination. This Box is
centered in X/Y and bottom-anchored in Z: its height runs from 0 to 8mm. Adding
this hole does not change the stock's placement.

Save it as `plate.firmament`, then run:

```sh
aetheris validate plate.firmament --json
aetheris build plate.firmament --output artifacts/local/plate.step
```

Normal mechanical validation performs engineering compilation in memory.
`--parse-only` reports `ParsedNotBuilt`; it does not prove the part can be built.
`--explain` adds developer information about admission and the chosen frontend.
See [diagnostics](../reference/diagnostics.md) for interpreting failures.

## How to read the language

The useful organizing analogy is `Model` as a class-like artifact owner, `Struct`
as reusable construction, `Feature` as a typed function-like recipe, and `Pattern`
as a finite loop. These analogies do not promise inheritance, runtime methods,
mutation or general-purpose computation.

| Construct | What it describes |
| --- | --- |
| `Model` | An enclosing engineering artifact and its construction. |
| `Struct` | Named construction that may be specialized and used by a consuming artifact. |
| `Concept Struct` | Non-materialized construction data: points, guides, frames and semantic members. |
| `Concept` | A semantic contract, or a domain-qualified construction value such as `Concept Path`. |
| `Profile` | Named planar boundaries consumed by an extrusion or another feature. |
| `Feature` | A reusable recipe returning one typed semantic result. |
| `Template` | Typed compile-time specialization of a declared product or bounded feature recipe. |
| `Record` / `Static` | A data shape / an immutable compile-time binding. A Record is not a body. |
| `Pattern` | Finite repetition of admitted construction over a declared source. |
| `Assembly` / `Scene` | Product occurrences and relations / spatial presentation. |

Use `Model` for an ordinary standalone part. A Struct, Template library, Profile
module or Assembly has its own entry and consumption rules; it is not simply a
Model with a different keyword.

### Declarations, fields and names

These are notation sketches, not executable files:

```text
Construct name { field: value }
Construct<Variant> name { field: value }
Feature Recipe(parameter: Type = defaultValue) -> ResultType { ... }
Struct instance = ProductFamily<parameter: value>
```

Braces delimit bodies, brackets delimit lists, parentheses delimit calls, and
`name.member` selects a published member. A colon labels a field, parameter or
declared type according to its context. Declaration bodies, typed values and
Template specializations are distinct forms, not interchangeable object literals.

Identifiers and published member names are case-sensitive. Preserve library
names, including named call arguments. `spec.Width` does not become `spec.width`
because the preferred local name is camelCase.

### Capitalization

| Role | Preferred spelling | Examples |
| --- | --- | --- |
| Constructs and types | PascalCase | `Model`, `Box`, `Feature`, `Length`, `Point2` |
| Built-in fields | camelCase | `units`, `size`, `counterboreDepth` |
| Local geometry, values and parameters | camelCase | `stock`, `outline`, `mountPoint`, `depth` |
| Linking words in their admitted positions | lowercase | `using`, `as`, `on`, `over`, `with`, `include`, `expose`, `bind` |
| Engineering symbols and enum values | Preserve domain spelling | `XY`, `+Z`, `Outer`, `ThroughAll`, `World`, `mm` |

`Profile outline using layout` selects a construction context;
`boundary.Bottom as south` assigns an output identity. `Using` and `As` remain
accepted aliases at those positions. A header clause such as
`Concept Struct layout on XY` differs from a field such as `on: +Z`.

This convention is not global case-insensitive parsing. Owners admit many
PascalCase field aliases, but some still **require** particular spellings.
Use `From`, `To`, `Reverse`, `Close` and `TraceLoop` in Profile pipelines.
Symmetry currently requires `From`, `Across`, `Source`, `About`, `Count` and
`Angle` in the forms below. Do not lowercase them merely to match the style table.
See [language style](language-style.md) for formatter behavior.

### Whitespace, comments and terminators

Use two-space indentation. Put ordinary fields on separate lines, or separate
them with semicolons in a short body. Separate list elements and call arguments
with commas. Do not rely on the parser guessing dense adjacent field boundaries.

Use `//` for comments, including before an optional `schema` header. The shared
lexer also recognizes `/* ... */`, but the schema selector currently rejects a
block comment before that header. `#` is not a general comment marker.

Some specialized forms **require** semicolons: scalar Function expressions,
point-recipe fields, PMI Note fields and Assembly root-provenance statements.
Follow the consuming guide; there is no uniform statement-termination rule yet.

### Values and units

Write dimensions explicitly in new mechanical source: `8mm`, `90deg`,
`Point2(12mm, -4mm)`. Use bare numbers for dimensionless counts and vector
components. `units: mm` belongs in an ordinary mechanical Model; it does not
make dimensions optional in every native field.

Points and lists overlap without being universal aliases. The direct Hole above
requires `Point2(...)`; a Rect2's `center` accepts `[x, y]`. A semantic Grid may
publish Point3 values in its own frame. Use the field owner's documented type
and spelling rather than replacing all point forms mechanically.

Arithmetic is available in admitted value positions, with dimensional checks.
Expression support is not uniform across every field, Template argument and
domain. `Static` and Feature-local `let` are scoped compile-time facilities;
there is no general `let`-anywhere rule. Scalar `Function` recipes are described
in [points and functions](concept-points-and-functions.md).

## Constructing from a layout

Use a `Concept Struct` for design geometry, trace its named guides into a
`Profile`, then consume that Profile. The layout itself creates no material.

**Complete file — a profile extruded from Z = 0 to 12mm:**

```firmament
Model ProfilePlate {
  units: mm

  Concept Struct layout on XY {
    Rect2 boundary { center: [0mm, 0mm]; size: [30mm, 24mm] }
  }

  Profile outline using layout {
    Loop Outer {
      boundary.Bottom as south
      |> boundary.Right as east
      |> boundary.Top as north
      |> boundary.Left as west
      |> Close
    }
  }

  Extrude stock { profile: outline; from: 0mm; to: 12mm }
}
```

`|>` traces finite compatible geometry in source order. The first stage chooses
its source direction; later stages orient themselves to the current endpoint.
A disconnected or ambiguous continuation fails. `Reverse` prefers reversed
orientation, falling back to a connectable direction after the opening stage.
`Close` verifies closure; it never supplies a missing line.

`boundary |> TraceLoop` copies a whole closed guide and normalizes winding for
the target loop. Supported families include Rect2, Square2, RoundedRect2,
Polygon2, RegularPolygon2, closed Concept Paths, Circle2 and Ellipse2. A full
circle or ellipse retains a single boundary segment; its traced identity derives
from the guide name, allowing distinct inner/outer guides.

Segment identities are Profile-wide. Loops do not silently namespace duplicate
edge names. Use `as` to disambiguate repeated leaf identities, or rename a manual
Segment. Manual `Segment` construction remains available for low-level control;
ordinary profiles can use pipelines.

**Fragment — endpoint selection inside a Profile pipeline, with the named guide
and points already declared:**

```firmament
guide From startPoint To endPoint as selectedEdge
```

After the opening stage, `guide To endPoint` uses the preceding endpoint as its
start. Endpoints must be named points on the guide. Circular spans use the same
form; `Reverse` selects the opposite traversal. `|>` is not a general map, filter,
arithmetic or function-composition operator. A Concept Path may remain open;
a material Profile must satisfy its boundary rules.

Closed boundaries should express their engineering shape. Use Rect2 for a
rectangle, Triangle2 for a triangle, or a named Polygon2 variant. Current Polygon2
variants include `Rhombus`, `Parallelogram`, `Trapezoid` and `Explicit`. Explicit
references a named point set; it does not introduce inline arbitrary vertex-array
syntax. `RegularPolygon2<N>` uses a bounded vertex count. These are built-in
shape parameters, not user-extensible generic types.

**Fragment — layout and Profile declarations inside a Model:**

```firmament
Concept Struct layout on XY {
  Polygon2<Rhombus> diamond {
    center: [0mm, 0mm]
    diagonals: [90mm, 64mm]
  }
}
Profile outline using layout { Loop Outer { diamond |> TraceLoop } }
```

The diagonals are full positive lengths, giving vertices at the south, east,
north and west half-diagonal offsets. This still needs a material consumer such
as Extrude. For a circular span and full material consumer, see the complete
[semicircular Revolve example](revolve.md#a-semicircular-meridian).

See [geometry](geometry.md), [spans](spans.md), [Concept derivation](concept-derivation.md)
and [boundary edits](profile-boundary-edits.md) for detailed construction forms.

## Adding and removing material

`Modify` targets an existing body. `Compose` creates a profile-based body with a
base material interval and admitted operations inside it. Compose does not
reopen a separately declared Box, and neither construct admits arbitrary Boolean
or feature combinations.

| Intent | Current vocabulary | Placement / termination |
| --- | --- | --- |
| Drill a hole | `Hole<Shaft>`, `Hole<Counterbore>`, `Hole<Countersink>` | `on`, `center`, `end: ThroughAll` or a bounded blind end. |
| Add a profile boss | `Boss` in profile-based Compose | Top support, a Profile and positive `height`. |
| Remove a finite-depth recess | `Pocket` in profile-based Compose | Profile and positive `depth`; must leave the admitted floor. |
| Cut through a profile-based body | Admitted `Remove` with `Role: ThroughCut` | Use the through-cut form rather than a through-depth Pocket. |
| Make a capsule slot | `Slot<Capsule>` in its admitted composition | `length`, `width`, `direction`, `extent` and `role`. |
| Finish selected edges | `EdgeFinish` with `kind: Chamfer` or `kind: Fillet` | Selector and capability depend on the target body. |

For a primitive boundary chamfer, the selector is `face: +Z` plus
`target: Boundary`; a profile-composed finish names its Profile boundary and
support instead. `EdgeFinish<Chamfer>` is not the direct primitive spelling.
A bare `Fillet corners { ... }` is not an alternative EdgeFinish declaration.

Read the [feature support and examples](geometry.md) before combining operations.
A feature's existence does not imply that every combination is admitted.
Unknown syntax, unresolved selections and unsupported operations must be
diagnosed; plausible remaining geometry is not evidence that all intent ran.

### Frames and selectors

`XY`, `XZ`, an axis such as `+Z`, a Plane, a datum frame and a published face are
different reference types. `on` appears in several contexts; it is not one
universal placement operation. A profile context, Hole support, section frame
and Assembly attachment each have their own type and admission rules.

Use only a plane or frame admitted by that consumer. Do not assume replacing
`on XY` with `on XZ` rotates every downstream feature. Follow
[Concept derivation](concept-derivation.md) and the consuming guide; unsupported
placement must reject rather than substitute XY.

Native named faces, signed primitive faces and imported `#face` references are
also distinct. Assembly occurrence identity does not by itself define a mate.
See [STEP import](step-import.md), [assembly foundations](assembly-authoring-foundation.md)
and [datum seating](datum-seating.md).

## Reusing construction

### Feature: a typed recipe

A Feature definition describes one result. Calling it materializes that result
in an admitted consuming scope. Parameters may be positional or named, with
defaults. A body may contain typed immutable `let` derivations and admitted
private construction, followed by one terminal `return`. Calls may delegate to
another Feature with the same result type; recursion is rejected.

Return kinds include Hole variants, Boss, Pocket, EdgeFinish and Profile. A
definition does not grant those results new support in an unsupported consumer.
Feature-local Profile construction is also used by Garment; WireForm, Sweep and
SectionChain reject Feature declarations.

### Pattern: finite repetition

**Complete file — two named points mapped to two holes:**

```firmament
Model MountingPlate {
  units: mm

  Feature Drill(center: Point2, diameter: Length = 6mm) -> Hole<Shaft> {
    return Hole<Shaft> {
      on: +Z
      center: center
      diameter: diameter
      end: ThroughAll
    }
  }

  Static mountPoints: Set<Point2> {
    left => Point2(-12mm, 0mm)
    right => Point2(12mm, 0mm)
  }

  Box stock { size: [50mm, 30mm, 8mm] }
  Modify stock {
    Pattern mounts over mountPoints {
      point => Drill(center: point)
    }
  }
}
```

The Pattern belongs **inside the target Modify**, not at the root of this file.
Each Set entry has a unique stable name; equal values are allowed and source
order is authoritative. `mountPoints.left` selects that value. An empty typed
Set yields zero mapped instances. Arrays provide anonymous ordered data; Sets
provide named ordered data. Pattern has a 1,024-instance safety bound.

`=>` in a Set associates a name with a value; in this Pattern it binds the element
name for finite mapping. It is not a general lambda syntax. Other owners still
have different Pattern forms:

| Pattern source | Binding form |
| --- | --- |
| Named Set mapped to a Feature | `Pattern name over values { value => Recipe(value) }` |
| Semantic PointSet / Grid | `Pattern name { Source: layout.Points ... Item ... }` |
| Record array with finite feature Template | `Pattern name over records { Recipe<Current> }` |
| Radial feature symmetry | `Radial Pattern name { Source: seed About: axis Count: n Angle: full }` |

These are context-specific forms, not interchangeable spellings of one loop.
`Item`, `Current` and an explicit element name are not global aliases. See
[Templates and finite data](templates.md) and the [functional pickup](functional-pickup.md).
A named Feature invocation may use `Feature result = Recipe(...)`; it differs
from a Feature definition and from a Pattern.

### Template: specialize a product

**Complete file — one sized member of a product family:**

```firmament
Template<width: Length, height: Length, thickness: Length>
Struct PlateFamily {
  Require positive => width > 0mm && height > 0mm && thickness > 0mm
  Box stock { size: [width, height, thickness] }
}

Struct standardPlate = PlateFamily<width: 80mm, height: 50mm, thickness: 8mm>
```

The output declaration is explicit. Product Templates specialize Struct, Model,
Concept Struct, Panel, SheetMetal, Profile or ProfileDelta. `Require` checks
specialization admissibility. Typed Record values may carry a configuration;
`Static` binds them and `with` derives checked immutable values. A separate
finite feature-Template form is used by record-array Patterns.

`<...>` therefore means several things: a built-in Hole variant, a Set element
type, or Template parameters/arguments. It does not make every construct a
general generic. See [Templates](templates.md) for constraints, finite Tables,
supported argument types and compatibility forms.

Feature, Template and Pattern describe deterministic finite construction. They
do not add mutable state, exceptions, arbitrary recursion, `for`/`while`, or
general `if`/`else` control flow. Use domain-specific forms only where admitted.

## Mirror and radial symmetry

Symmetry transforms semantic construction before materialization. It does not
mirror a finished STEP/BRep. These fragments use the **currently required**
PascalCase symmetry fields, despite the general camelCase field convention.

**Fragment — inside Modify, with a concrete `leftMount` Hole and a model-level
`centerPlane` already declared:**

```firmament
Mirrored Feature rightMount From leftMount { Across: centerPlane }
Feature largerMount = rightMount with { Diameter: 8mm }
```

`Mirrored Profile result From source { Across: plane }` derives a Profile.
Reflection transforms curves, restores loop winding and reconstructs a
right-handed frame. Construction Planes traced from Concept Planes are admitted
in the Profile form. Material, scalar magnitudes and enum tags are invariant;
`Mirrored Material` is not an admitted operation.

`with` derives a new feature with source/mirror/override provenance. A Profile
on its reflection line may remain an equivalent named derivation. A concrete
feature with an unchanged center rejects as a redundant coincident feature.

**Fragment — inside Modify, with `boltSeed` and a model-level `mainAxis`:**

```firmament
Radial Pattern boltCircle { Source: boltSeed About: mainAxis Count: 6 Angle: full }
```

The source is instance zero. `Count` is 1..1024. A full circle uses
`theta[i] = i * 2*pi/Count`, without a duplicate endpoint. A partial angle includes
both endpoints: `theta[i] = i * Angle/(Count-1)` when Count exceeds one. Angles
accept the Revolve spellings `radians`, `deg`, `quarter`, `half`, `full`.
Support is bounded to planar Point2-centered Holes about their support-normal
Axis. It does not imply Profile, Boss/Pocket, whole Struct or Assembly symmetry.

Complete sources: [mirrored hole](../../../fixtures/Canonical/Symmetry/mirrored-hole.firmament),
[mirror override](../../../fixtures/Canonical/Symmetry/mirrored-feature-with-override.firmament),
[radial bolt circle](../../../fixtures/Canonical/Symmetry/radial-bolt-circle.firmament).

## Choosing a domain and organizing files

An optional `schema Name` header selects one of these frontends. Put it on its
own line before declarations, preceded only by blank lines or `//` comments.
Do not put a trailing comment on the selector line: selection can currently be
bypassed by that formatting. Names are case-sensitive. A recognized explicit
selection is authoritative; rejection does not retry another frontend.
Revolve is an operation in Mechanical, not a schema.

| Schema | Guide |
| --- | --- |
| `Mechanical` | [Geometry](geometry.md), [Revolve](revolve.md), [gears](gears.md). |
| `WireForm` | [Formed wire](wire-form.md). |
| `Sweep` | [Circular-section sweeps](sweep.md). |
| `SectionChain` | [Framed section chains](section-chains.md). |
| `Garment` | [Panels, stitch interfaces and fitting](garments.md). |

This selector does not list every Firmament root or product domain. Assembly,
Scene, SheetMetal, structural and piping sources have their own entry forms.
Do not invent `schema Assembly` or wrap arbitrary domain declarations in a Model.

| Authoring task | Start here |
| --- | --- |
| Parts, ports, constraints and product occurrences | [Assemblies](assemblies.md), [components and sites](assembly-components-and-sites.md). |
| Spatial layout and presentation | [Scenes](scenes.md). |
| Sheet manufacturing, regions and bends | [Sheet metal](sheet-metal.md). |
| Structural / piping domains | [Structural](structural.md), [piping](piping.md). |
| Engineering annotations | [PMI](pmi.md). |
| File-backed modules and source-only projects | [Project snapshots](project-snapshots.md), [Concept derivation](concept-derivation.md). |

Assembly occurrence trees use `<Assembly>` / `<Part>` tags as well as brace
bodies. Tree nesting does not create a Mate. Inclusion is owned by the consuming
project/domain; `include` is **not** a universal mechanical preprocessor.
A bare include before an ordinary mechanical Model can currently be ignored,
even when its file is missing. Use a documented include consumer; the review
below preserves this reproduction.

## Reading older source

New ordinary parts should use the forms at the top of this page. Historical
`model`, `units mm`, `solid name: Box`, `modify` and `hole<shaft>` are bounded
compatibility forms. Bare lengths inherit millimetres only in the historical
primitive grammar that admits them. This is not a second preferred style.

Historical immutable Box derivation uses
`solid tall: stock with { size: [30, 24, 20] }`. Exposure uses
`expose { face(+Z) as top }`, with `=> top` also retained there. These forms do
not make `with`, `as` or `=>` universally interchangeable. Serialization-shaped
V1 `model:` and `firmament:` documents have an explicit compatibility path.

The formatter preserves names, published members and numeric values. It does
not migrate solid grammar into native declarations or prove a program builds.
Validate the consuming artifact after formatting.

## Current language seams

The preferred style is more consistent than the grammar enforced by every
owner. Author-visible differences remain in casing, points/lists, Pattern
binders, terminators, root/scope rules, frame/selector types, and meanings of
angle brackets and `with`. Some are legitimate domain distinctions; others are
compatibility debt or bugs.

The [October syntax review](../../development/FIRMAMENT-SYNTAX-REVIEW-2026-10.md)
records witnesses, current behavior, ownership and open decisions. It includes
the ignored mechanical include and schema-comment failures. It describes the
language as it exists, not unimplemented syntax. The [decision register](../../development/FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md)
preserves the original authoring audit and later migration dispositions.

Use [supported features](../reference/supported-features.md) for capability
boundaries and the linked domain guides for detailed syntax. Proposals and
migration reports explain design/history; they do not grant source admission.
