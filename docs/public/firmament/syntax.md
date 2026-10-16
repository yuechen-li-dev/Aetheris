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
| `Concept` | A compile-time requirement: a structural contract, a Boolean predicate, or a labeled bundle of predicates. |
| `Profile` | Named planar boundaries consumed by an extrusion or another feature. |
| `Feature` | A reusable recipe returning one typed semantic result. |
| `Template` | Typed compile-time specialization of a declared product or bounded feature recipe. |
| `Record` / `Static` | A data shape / an immutable compile-time binding. A Record is not a body. |
| `Enum` / `Match` | A finite choice type / compile-time selection of one result. |
| `Set<T>` | All named entries of type T, available together for finite repetition. |
| `Semantic` | A named engineering interface containing published datums, axes, points or dimensions. |
| `Path` | An ordered construction chain, open or closed; it creates no material. `Concept Path` is a compatibility spelling. |
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
| Linking words in their admitted positions | lowercase | `using`, `as`, `from`, `to`, `on`, `over`, `with`, `include`, `expose`, `bind` |
| Engineering symbols and enum values | Preserve domain spelling | `XY`, `+Z`, `Outer`, `ThroughAll`, `World`, `mm` |

`Profile outline using layout` selects a construction context;
`boundary.Bottom as south` assigns an output identity. `Using` and `As` remain
accepted aliases at those positions. A header clause such as
`Concept Struct layout on XY` differs from a field such as `on: +Z`.

This convention is not global case-insensitive parsing. Profile endpoint links
use `from`/`to`; `From`/`To` remain accepted aliases. Mirror headers use `from`.
Symmetry fields use `across`, `source`, `about`, `count` and `angle`, with their
PascalCase aliases retained. These positions bind through the shared AST, which
keeps authored names and members unchanged. Geometry operations `Reverse`,
`Close` and `TraceLoop` remain PascalCase, like other constructs. See
[language style](language-style.md) for formatter behavior and domain scope.

### Whitespace, comments and terminators

Use two-space indentation. Put ordinary fields on separate lines, or separate
them with semicolons in a short body. Separate list elements and call arguments
with commas. Do not rely on the parser guessing dense adjacent field boundaries.

Use `//` or `/* ... */` for comments, including before an optional `schema`
header or after its name. Comments do not disable schema selection. `#` is not
a general comment marker.

Some specialized forms **require** semicolons: scalar Function expressions,
point-recipe fields, PMI Note fields and Assembly root-provenance statements.
Follow the consuming guide; there is no uniform statement-termination rule yet.

### Values and units

Write dimensions explicitly in new mechanical source: `8mm`, `90deg`,
`Point2(12mm, -4mm)`. Use bare numbers for dimensionless counts and vector
components. `units: mm` belongs in an ordinary mechanical Model; it does not
make dimensions optional in every native field. Existing Panel arc boundary
angles also admit `rad`; this does not make radians a universal alias in every
Angle field. Mixed degree/radian arithmetic requires an explicit owning-domain
conversion rather than silently adding the raw numbers.

Planar point fields such as a Hole's or Rect2's `center` accept both
`Point2(x, y)` and `[x, y]`, with Length coordinates. `Set<Point2>` entries
accept both forms and typed scalar arithmetic. A semantic Grid publishes
Point3 values in its own frame; these retain their spatial placement checks.
For a direct Hole on its host's top plane, `+Z`, `face(+Z)`, `Top` and
`host.Top` identify the same support. An unrelated body name is not an alias.

Arithmetic is available in admitted value positions, with dimensional checks.
Expression support is not uniform across every field, Template argument and
domain. `Static` and Feature-local `let` are scoped compile-time facilities;
there is no general `let`-anywhere rule. Scalar `Function` recipes are described
in [points and functions](concept-points-and-functions.md).

### Typed tolerances and PMI

A tolerance is a compile-time requirement attached to a dimensional value:

```firmament
Model TolerancedPlate {
  units: mm
  Static drillDiameter: Length = 8mm tol +0.10mm -0.05mm
  Box Body { size: [40mm, 30mm, 8mm] }
  Modify Body {
    Hole<Shaft> Mount {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: drillDiameter
      end: ThroughAll
    }
  }
  Pmi {
    HoleDiameter MountSize { target: Mount dimension: drillDiameter }
  }
}
```

`tol 0.1mm` means ±0.1 mm. `tol +0.10mm -0.05mm` means upper and lower
allowance magnitudes of 0.10 and 0.05 mm. Both must be finite, nonnegative and
have the value's dimensional unit. `Length` uses mm and `Angle` uses deg in this
scalar lane; tolerances on Int, Float, Bool or String reject. `tol` applies to
the whole initializer, after arithmetic. Zero allowances are permitted.

Geometry consumes the nominal value. `Pmi` explicitly projects the requirement
into AP242; declaring a tolerance alone emits no manufacturing annotation and
does not loosen kernel geometry tolerances. A direct Static alias preserves
the allowance. Arithmetic produces a nominal value and warns if it drops an
operand's allowance; supply a new explicit `tol` when the result is a new
requirement. There is no inferred interval propagation or tolerance-stack
certification in Model scalar arithmetic.

Typed requirements also travel through Record data, Template parameters and
Feature parameters or local aliases. This complete example uses the same
requirement for construction and explicit inspection:

```firmament
Record BoreSpec { diameter: Length }
Static production: BoreSpec { diameter: 8mm tol +0.10mm -0.05mm }

Template<spec: BoreSpec> Model PlateFamily {
  units: mm
  Feature Drill(spec: BoreSpec) -> Hole<Shaft> {
    let inspection: Length = spec.diameter
    return Hole<Shaft> {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: inspection
      end: ThroughAll
    }
  }
  Box Body { size: [40mm, 30mm, 8mm] }
  Modify Body { Feature Mount = Drill(spec: spec) }
  Pmi { HoleDiameter MountSize { target: Mount dimension: spec.diameter } }
}
Model ProductionPlate = PlateFamily<spec: production>
```

Record field initializers may declare `tol`; Record field types remain `Length`
or `Angle`. Passing either a scalar field or the whole typed Record preserves
its requirement. Nested Records, checked `with` overrides, scalar Template
defaults, nested specialization and Feature-local aliases follow the same
rule. Feature locals stay local; the enclosing model selects its visible
Static or Record requirement for PMI. Record data must be finite and acyclic;
the current nesting limit is 128. A replacement field without `tol` replaces
the requirement with an
untoleranced value. Template `Require` conditions check nominal values; they
do not certify a tolerance stack. Validation reports expose requirement origins
and binding steps; internal transport identifiers are compiler details.

Scalar Static values belong directly to the Model or its source module and
feed direct primitive and Modify fields. Direct Record member references are
also admitted in these fields and `Pmi dimension:`. The existing shorthand
`Static width = 40mm` infers Length; an annotation makes engineering intent
clearer. Profile/Compose recipes and
other domain frontends retain their typed Template inputs; they do not acquire
general scalar environments from this feature. Unsupported scalar consumers
in the qualified construction, Sweep and wire lanes reject; this pass does not
qualify arbitrary scalar environments in other domains. Inline geometry-field
annotations such as `diameter: 8mm tol 0.1mm` reject; give
the requirement a named Static or Record field. Toleranced Table cells and
collection elements are not qualified by the Record/Template/Feature lane.
Angle allowances can be bound and inspected, but native
Model angular PMI export is not yet admitted. See [PMI](pmi.md) for projection
targets and export limits.

### Contracts, semantic interfaces and construction geometry

`Concept` names a requirement. A structural Concept describes the members a
construction must provide. A predicate Concept checks compile-time values and
returns Bool. A record-shaped requirement bundle names several Boolean checks;
every field must pass. A bundle is not a data Record and does not store geometry.

**Complete file — reusable requirements applied before geometry:**

```firmament
Concept Positive(value: Length) => value > 0mm

Concept PlateRequirements(width: Length, height: Length) {
  positiveWidth: Positive(width)
  positiveHeight: Positive(height)
  aspect: width >= height
}

Template<width: Length, height: Length> Model Plate {
  units: mm
  Require dimensions => PlateRequirements(width, height)
  Box Body { size: [width, height, 5mm] }
}
Model Product = Plate<width: 40mm, height: 20mm>
```

`Require` applies a requirement. It also works directly in a mechanical Model
with literal or checked scalar Static inputs. Defining a Concept alone imposes
no check: `Concept AlwaysFalse => false` is a legal definition, but a Require
that applies it fails. Definitions are type-checked even when unapplied.
Predicate parameters are scalar Length, Angle, Time, Int, Float, Bool or String;
positional and named calls are admitted. Pass Record scalar members as arguments.
Concepts do not introduce runtime functions, implicit unit conversion, Record
parameters, arbitrary geometry queries or a general constraint solver. Toleranced
inputs are checked at their nominal values; this is not an interval proof.

Bundles can call other bundles. Their labels identify a failed check, avoiding
long chains of `&&`. `&&`, `||`, `!`, comparisons and dimensional arithmetic
remain available; all operands must resolve and type-check, including an operand
whose Boolean result would not affect the answer. Bare zero-argument predicates
can be referenced by name or called with `()`.

**Fragment — structural contracts, used by an existing conformance consumer:**

```firmament
Concept Bounded { Bounds: Box3 }
Concept Planar { TopPlane: Plane }
Concept PlateContract : Bounded && Planar { }
```

The composed contract requires both typed members. Use its name in
`type T satisfies PlateContract` or `Concept Struct Design: PlateContract`.
Conformance checks typed members; a structural Concept is not a Boolean value.
Composition permits identical inherited requirements and rejects conflicting
member types, duplicate own members, missing contracts and cycles. Claims on
Concept Struct retain the existing closed published-member signature: additional
members require a larger composed contract. Built-in type constraints check the
required capabilities of that type. This pass does not introduce private members
or general trait/type inheritance.

Contract member names are user-owned symbols: `Bounds` is not silently renamed to
`bounds`. Current part compilation resolves Concept names within the expanded
part; this does not introduce namespaces or nested class-style type scopes.

`Concept Struct` supplies non-materialized design geometry or an implementation
of a structural contract. Its historical `Concept` prefix does not apply a
Boolean predicate. Inside a planar layout, prefer plain `Rect2`, `Circle2` and
the other geometry declarations. Compatibility forms such as
`Concept Circle2 circle { ... }` and `Concept Slot2 slot { ... }` bind through
the same shape owner, with the same fields, placement restrictions and geometry.
They are construction values, not declarations of new requirement types.

`Semantic Name { ... }`, usually published through `Expose` or attached to an
Assembly occurrence, groups engineering references that consumers can name.
For example, a mount interface can publish a `DatumFrame` and a toleranced
`Dimension`. It is not a second body, a free-form metadata bag, or a command to
recognize arbitrary BRep faces. Published values retain their types and source
identity. [Assembly authoring](assembly-authoring-foundation.md) explains the
interface and placement rules. PMI selects requirements for export; Semantic
alone does not export every member as PMI.

Use `Path` for ordered guides and `Profile` for a closed planar material
boundary. Both `Path Name { ... }` and the older `Concept Path Name { ... }`
resolve through the same owner, with identical endpoints, segment identities
and geometry. A path can remain open for a Sweep; a Profile made from it must
satisfy the Profile's closure rules. Path does not imply a solid or a spatial
volume.

`Region` is not a general replacement for Struct, Profile or Semantic. Current
mechanical compatibility syntax has the bounded lowercase
`region sideHole on face(+X) { cut Cylinder { ... } }` operation inside
`modify`; it is not a general native `Region` declaration. Prefer `Hole<Shaft>`
for new hole construction. `Recognize` names a verified region of existing
geometry. Sheet Metal's named planar regions and FEA's `Region:` selection
fields are domain-owned uses of the word, with separate types and admission
rules. See [Sheet Metal](sheet-metal.md) and [FEA](fea.md).

### Choices versus collections

`Enum` and `Match` remain useful; `Set<T>` does not supersede them:

| Intent | Form | Compile-time meaning |
| --- | --- | --- |
| Choose Compact or Standard | `Enum SizeChoice { Compact Standard }` | Define alternatives; a typed parameter holds one case. |
| Compute a value for the selected case | `Match choice { Compact => 1mm Standard => 2mm }` | Select one arm in an admitted Template/static expression position. |
| Construct every mount | `Static mounts: Set<Point2> { ... }` and `Pattern` | Bind all entries; expand the recipe for each named entry. |

Enum cases are tags, not coordinates or a collection of feature instances.
Match is not a runtime branch or a general statement executor. Pattern repeats
construction; it does not choose an enum case. Match support is bounded by its
consumer: the Template binder supports typed choices, while Model scalar Static
initializers currently admit literals, aliases and arithmetic. Do not infer
general Match support from its presence in the syntax tree. See
[Templates](templates.md) and the Pattern examples below.

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
Polygon2, RegularPolygon2, closed Paths, Circle2 and Ellipse2. A full
circle or ellipse retains a single boundary segment; its traced identity derives
from the guide name, allowing distinct inner/outer guides.

Segment identities are Profile-wide. Loops do not silently namespace duplicate
edge names. Use `as` to disambiguate repeated leaf identities, or rename a manual
Segment. Manual `Segment` construction remains available for low-level control;
ordinary profiles can use pipelines.

**Fragment — endpoint selection inside a Profile pipeline, with the named guide
and points already declared:**

```firmament
guide from startPoint to endPoint as selectedEdge
```

After the opening stage, `guide to endPoint` uses the preceding endpoint as its
start. Endpoints must be named points on the guide. Circular spans use the same
form; `Reverse` selects the opposite traversal. `|>` is not a general map, filter,
arithmetic or function-composition operator. A Path may remain open;
a material Profile must satisfy its boundary rules.

Closed boundaries should express their engineering shape. Use Rect2 for a
rectangle, Triangle2 for a triangle, or a named Polygon2 variant. Current Polygon2
variants include `Rhombus`, `Parallelogram`, `Trapezoid` and `Explicit`. Explicit
references a named point set with `vertices: points`. The inline equivalent is
`Polygon2 outline { points: [[0mm, 0mm], [40mm, 0mm], [0mm, 20mm]] }`.
Each coordinate can use typed Static arithmetic. Both forms check winding,
duplicate points, self-intersection and nonzero area; neither accepts a nonzero
`rotation`. Segment endpoints still reference named Point2 declarations.
`RegularPolygon2<N>` uses a bounded vertex count. These are built-in
shape parameters, not user-extensible generic types.

| 2D construction family | Main fields / meaning |
| --- | --- |
| `Point2`, `Line2`, `CubicBezier2` | `position`; named `from`/`to`; named endpoints plus `control1`/`control2`. Open guides create no material. |
| `Rect2`, `Square2` | `center`, `size`; rectangle size is two full lengths, square size is one length. |
| `Circle2` | `center` and exactly one of `radius` or `diameter`. |
| `Ellipse2` | `center`, `axisLengths` (two full axis lengths); `axes` is an admitted alias. |
| `Slot2` | `center`, `length`, `width`; length includes both semicircular ends and must be at least width. |
| `RoundedRect2` | `center`, `size`, `radius`. |
| `SmoothRoundedRect2` | `center`, `size`, `cornerExtent`; polynomial corners, not circular fillets. |
| `Triangle2<...>` | `Equilateral`, `Isosceles`, `Right` or `Explicit`, each with its own dimension fields. |
| `Polygon2<...>`, `RegularPolygon2<N>` | Named polygon variants or a bounded regular polygon. |

The closed families admit the compatibility `Concept` prefix. Parametric variants
admit `rotation` in degrees; a rotation is redundant for a circle. Explicit
Triangle2 and Polygon2 variants use authored points directly and reject nonzero
rotation. Rotate those points or place a derived Profile with Curve2 instead.
These remain planar guides: trace a Profile
and give it a material consumer to obtain a solid. Layout `on XY` is admitted;
an unsupported `on XZ` does not silently change the plane. These forms do not
promise arbitrary 2D Boolean operations, implicit sketch solving, or first-class
shape arguments to predicate Concepts. See [boundary details](boundaries.md)
for variant fields and exact curve families.

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

The current engineering exporter admits one material body per Model. A second
Box, Cylinder or other material body is rejected at its declaration, even if
the bodies have no Modify operations. It is never silently discarded. Use
separate Parts in an Assembly for separate objects, or the admitted Compose
operations for material belonging to one body. Palette body entries marked
`requiresEmptyModel` start an empty Model; feature entries operate on its body.

In a primitive Model, `Modify Base {}` is a valid no-op once `Base` resolves to
an existing body. Deleting its last feature leaves the same stock geometry;
an unresolved target still rejects. An empty Modify does not create a feature.

Hole fields depend on the declared variant. `Hole<Shaft>` uses the common
`on`, `center`, `diameter` and `end` fields. `Hole<Counterbore>` additionally
uses `counterboreDiameter` and `counterboreDepth`; `Hole<Countersink>` uses
`countersinkDiameter` and `countersinkAngle`. Relief fields on a Shaft hole
are errors. Compiler completion and bound-field inspection project the same
variant applicability from the shared schema.

| Intent | Current vocabulary | Placement / termination |
| --- | --- | --- |
| Drill a hole | `Hole<Shaft>`, `Hole<Counterbore>`, `Hole<Countersink>` | `on`, `center`, `end: ThroughAll` or a bounded blind end. |
| Add a profile boss | `Boss` in profile-based Compose | Top support, a Profile and positive `height`. |
| Remove a finite-depth recess | `Pocket` in profile-based Compose | Profile and positive `depth`; must leave the admitted floor. |
| Cut through a profile-based body | `Pocket` with `end: ThroughAll`, or `Remove` with `role: ThroughCut` | Through-Pocket selects its top support and lowers to the same through-cut operation; omit `depth` and floor policy. |
| Make a capsule slot | `Slot<Capsule>` in its admitted composition | `length`, `width`, `direction`, `extent` and `role`. |
| Finish selected edges | `EdgeFinish<Chamfer>` or `EdgeFinish<Fillet>` | `kind: Chamfer` / `kind: Fillet` remains an alias; if both are supplied they must agree. Selector and capability depend on the target body. |

For a Box top-boundary chamfer or fillet, the selector is `face: +Z` plus
`target: Boundary`. A fillet uses `radius`; a chamfer uses `distance`.
One such finish can coexist with disjoint top `ThroughAll` shaft, counterbore
or countersink holes in the same Modify. The hole entry circles must stay strictly inside the
inset top face. Fillets retain sharp vertical stock corners and join their four
cylindrical blends on exact elliptical seams; this is not a whole-box rounding.
A profile-composed finish names its Profile boundary and support instead.
The generic variant has the same capability checks as `kind`.
A bare `Fillet corners { ... }` is not an alternative EdgeFinish declaration.
For a combined countersink, its conical relief must end above a positive shaft
span. Blind or side-entry holes sharing a boundary finish remain unsupported;
their diagnostic points to the Hole declaration.

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

Feature names resolve lexically. A Feature declared inside a Model shadows an
enclosing Feature with the same name; a Feature inside another Struct is not
visible outside that Struct. Two definitions in the same scope are an error.
Calls delegated by a Feature resolve in its definition's scope. A Feature and
a construction Profile may share a name because calls and construction references
have different roles.

Defaults may use other typed parameters, including dimensional arithmetic;
cyclic defaults are errors. Expansion is finite: at most 64 nested calls or
default dependencies, 4,096 invocations, 65,536 expanded declarations and 4 MiB
of generated construction per Feature expansion phase. Exceeding a limit rejects
the program; it never yields a partially generated part.

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

A Pattern may be inside its target `Modify`, or directly inside a Model/Struct
with exactly one material body. The latter form targets that body; ambiguous
material ownership requires an explicit `Modify`. Scalar `Static` values may
be declared in the module or Model scope and used in Compose intervals as well
as primitive dimensions. Use `Static` for Model-local values; `let` remains a
Feature-local derivation.
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
| Radial feature symmetry | `Radial Pattern name { source: seed; about: axis; count: n; angle: full }` |

These are context-specific forms, not interchangeable spellings of one loop.
`Item`, `Current` and an explicit element name are not global aliases. See
[Templates and finite data](templates.md) and the [functional pickup](functional-pickup.md).
A named Feature invocation may use `Feature result = Recipe(...)`; it differs
from a Feature definition and from a Pattern.

In the admitted Set and semantic point Patterns, an iteration symbol substitutes
value references. It does not rename field labels, declaration names, string
contents or a similarly spelled identifier. Generated instance identities still
come from the Pattern and its keys or ordered elements.

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

Product Template names resolve in their lexical scope. A Model-local Template
can shadow a document Template; a sibling Model's private Template is invisible.
Nested applications resolve from the Template definition, so a caller-local
Template does not capture them. Duplicate Template names in the same scope
reject. Host discovery and invocation expose document-level product Templates;
existing single-Model module containers also export their immediate Templates.
Application headers cannot silently discard placement or other clauses.

`<...>` therefore means several things: a built-in Hole variant, a Set element
type, or Template parameters/arguments. It does not make every construct a
general generic. See [Templates](templates.md) for constraints, finite Tables,
supported argument types and compatibility forms.

Feature, Template and Pattern describe deterministic finite construction. They
do not add mutable state, exceptions, arbitrary recursion, `for`/`while`, or
general `if`/`else` control flow. Use domain-specific forms only where admitted.

### Compile-time data scope

Record schemas, Static values and Static Tables belong to their lexical scope.
A local declaration shadows the same name in an enclosing scope; sibling scopes
remain private. Record types are nominal: declaring another `Record Spec` does
not make values of the outer Spec instances of the local Spec. Both field names
and user symbols retain their authored casing.

```firmament
Record Spec { width: Length }
Static outer: Spec = Spec { width: 10mm }
Model Plate {
  units: mm
  Record Spec { width: Length thickness: Length }
  Static Table choices: Spec {
    width: [30mm]
    thickness: [6mm]
  }
  Static selected: Spec = choices[0]
  Box stock { size: [selected.width, 20mm, selected.thickness] }
}
```

The local Table uses the local schema. The unused outer value keeps its own
schema and does not acquire a `thickness` requirement. Table rows, checked `with`
derivations and Record aliases use the same typed data binding as literal
Records. Product Template defaults resolve where the Template is defined;
explicit arguments resolve at the invocation. Specialized data is admitted before
Feature arguments. Unknown fields, inaccessible names, duplicate declarations,
cycles and unsupported data-header clauses reject.

Typed tolerances on Record/scalar values retain their requirement provenance
through specialization, geometry and explicit Pmi. Table/collection tolerance
propagation remains unsupported and rejects; a Table is not a way to bypass that
boundary. See [the scoped Record witness](../../../fixtures/Regression/FrontendX2/record-shadowing.firmament)
and [explicit scoped PMI](../../../fixtures/Regression/FrontendX2/scoped-record-pmi.firmament).

## Mirror and radial symmetry

Symmetry transforms semantic construction before materialization. It does not
mirror a finished STEP/BRep. Its headers and fields follow the same lowercase
link / camelCase field conventions as ordinary mechanical construction.

**Fragment — inside Modify, with a concrete `leftMount` Hole and a model-level
`centerPlane` already declared:**

```firmament
Mirrored Feature rightMount from leftMount { across: centerPlane }
Feature largerMount = rightMount with { diameter: 8mm }
```

`Mirrored Profile result from source { across: plane }` derives a Profile.
Reflection transforms curves, restores loop winding and reconstructs a
right-handed frame. Construction Planes traced from Concept Planes are admitted
in the Profile form. Material, scalar magnitudes and enum tags are invariant;
`Mirrored Material` is not an admitted operation.

`with` derives a new feature with source/mirror/override provenance. A Profile
on its reflection line may remain an equivalent named derivation. A concrete
feature with an unchanged center rejects as a redundant coincident feature.

**Fragment — inside Modify, with `boltSeed` and a model-level `mainAxis`:**

```firmament
Radial Pattern boltCircle { source: boltSeed; about: mainAxis; count: 6; angle: full }
```

The source is instance zero. `count` is 1..1024. A full circle uses
`theta[i] = i * 2*pi/Count`, without a duplicate endpoint. A partial angle includes
both endpoints: `theta[i] = i * Angle/(Count-1)` when Count exceeds one. Angles
accept the Revolve spellings `radians`, `deg`, `quarter`, `half`, `full`.
Support is bounded to planar Point2-centered Holes about their support-normal
Axis. It does not imply Profile, Boss/Pocket, whole Struct or Assembly symmetry.

Complete sources: [mirrored hole](../../../fixtures/Canonical/Symmetry/mirrored-hole.firmament),
[mirror override](../../../fixtures/Canonical/Symmetry/mirrored-feature-with-override.firmament),
[radial bolt circle](../../../fixtures/Canonical/Symmetry/radial-bolt-circle.firmament).

## Choosing a domain and organizing files

An optional `schema Name` header selects one of these frontends. Declare it once,
before other directives or declarations. Prefer a separate line for readability;
inline declarations and leading/trailing comments also retain schema selection.
Names are case-sensitive. Unknown names, duplicate or nested/late headers and
mismatched domains reject; rejection does not retry another frontend. Revolve
is an operation in Mechanical, not a schema. Template specialization must also
remain within the selected domain; it cannot change the schema after admission.

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

### Inventory: organize module dependencies

Use a short `include "parts/bolt";` when shared declarations are convenient. For
larger compositions, give Part, Subassembly, Assembly and Scene dependencies typed
names in an Inventory Record:

```firmament
Inventory Record Components {
  bracket: Part = Firmament("parts/bracket").Bracket
  drive: Subassembly = Firmament("assemblies/drive").Drive
  bearing: Part = Step("vendor/bearing")
}
```

This fragment requires the named resources. Locators normally omit the extension;
ambiguous matches reject. Place `Components.bracket` through a Part occurrence,
`Components.drive` through an Assembly occurrence, or a Scene entry through a
Scene occurrence. The catalog makes definitions available; occurrences determine
quantity and placement. Part and Subassembly entries may reference typed Template exports; supply their
named arguments at the occurrence. Definition-private names remain in the defining
module. Assembly and Scene entries currently import concrete exports.

Inventory follows lexical scope and retains imported file identity. Existing
`include` remains the concise, unqualified composition form. See
[Inventory Records](inventory.md) for complete projects, source snapshots, STEP
boundaries and extension resolution.

Assembly occurrence trees use `<Assembly>` / `<Part>` tags as well as brace
bodies. Tree nesting does not create a Mate. Inclusion is owned by the consuming
project/domain; `include` is **not** a universal mechanical preprocessor.

Standalone part compilation rejects an unresolved include explicitly, even if
the named file exists. Use a documented Assembly/project loader to resolve file
dependencies before part compilation. The shipped `Use Profile.Modifications`
import retains its existing library owner. Other unconsumed directives reject.

For relationships, declare a reusable typed contract and apply it with a named
Mate. The family owns its capability requirements and ordinary lowering:

```firmament
Interface<Fixed> Installation { }
// Inside the consuming Assembly:
Mate InstallBracket: Installation {
    A: Machine.Frame.Mount;
    B: Machine.Bracket.Mount;
}
```

`Semantic Mount` publishes the mating port on each occurrence; it is not another
material body. Here `Fixed` expects datum-frame-capable ports. `A` and `B` are
role symbols, not camelCase built-in field names. Named Mate assignments use the
declared role spelling. Compact inline typed applications retain their historical
`a`/`b` aliases for the predefined `A`/`B` slots. A Mate applies
the contract at compile time; it is not a runtime function call. Inline
`Interface<Fixed> Name { A: ...; B: ...; }` remains admitted shorthand. Old custom
`Role ... requires ...` and `Lower ...` clauses remain bounded compatibility,
parsed into the same AST and relationship IR. New ordinary relationships should
use the typed families; use `Interface<Custom>` only when explicit atomic
relations are necessary. Unknown clauses, duplicate assignments and unresolved
roles reject rather than disappearing. See [Assemblies](assemblies.md).

## Shared datums and articulated motion

An assembly `Concept Struct` can publish derived three-dimensional frames. Use
`from`, `translateLocal` and `rotateLocal` rather than authoring trigonometric
basis components:

```firmament
Concept Struct Layout {
  DatumFrame Shaft { from: World; translateLocal: [3mm,4mm,5mm] }
  DatumFrame Bank { from: Shaft; rotateLocal: { axis: Z; angle: -45deg } }
  DatumFrame Cam { from: Bank; translateLocal: [0mm,100mm,20mm] }
}
```

A local name such as `Bank` resolves within this Concept Struct. A qualified
name can reference another declared datum. Translation is expressed in the
referenced frame. A sibling `rotateLocal` changes orientation without rotating
that translation; derive an intermediate frame when translation should follow
its rotated basis. Required lengths and angles are dimension checked. Unknown
fields, unresolved sources and datum cycles reject. The existing `DatumFrame`
form with `on`, `at` and `X` on a Plane remains supported.

Reusable Subassemblies retain their local Fixed, Revolute and Prismatic joint
graphs. Occurrences share geometry and own independent coordinates. Exposed
geometric ports follow their actual child occurrence when it moves. At the
compiler API boundary, a nested realized Mate is qualified by occurrence, for
example `Articulated.First.Elbow`; a root Mate keeps its declared name. Names in
motion source address those realized Mates, not the shared Interface type.
Reusable definitions publish their motion coordinates explicitly, for example
`expose { Mate Bend = Elbow; }`. A parent Track uses
`Articulated.First.Bend.angle`; the private local `Elbow` name is not a parent
Motion target. A wrapper can re-export a nested public channel with
`Mate Bend = Unit.Bend;`. These aliases retain the realized Mate's stable ID,
so two public aliases cannot become independent writers for one coordinate.

Declare `Motion` and reusable `Keyframes` in the root Assembly or its module:

```firmament
Motion Cycle {
  duration: 2s
  repeat: Loop
  Track Rotation {
    target: Machine.Crank.angle
    interpolation: Linear
    Keyframe Start { at: 0s; value: 0deg }
    Keyframe Finish { at: 2000ms; value: 720deg }
  }
}
```

This fragment requires an admitted Revolute Mate named `Crank` in `Machine`.
`Time` is a compile-time scalar with `s` and `ms`, normalized to seconds; it can
flow through checked Record fields. Time is distinct from Length and Angle.
A Revolute track writes `angle` in degrees; a Prismatic track writes
`translation` in millimetres. Angles remain unwrapped: 720deg means two turns.
Inline keys use Time, start at zero and end at the declared duration.

Reusable curves declare their domain and value dimensions explicitly:

```firmament
Keyframes<Angle,Length> Lift {
  period: 720deg
  interpolation: Cosine
  Keyframe Rest { at: 0deg; value: 0mm }
  Keyframe Open { at: 360deg; value: 0mm }
  Keyframe Peak { at: 450deg; value: 9mm }
  Keyframe Close { at: 540deg; value: 0mm }
  Keyframe End { at: 720deg; value: 0mm }
}
```

A Track can use `values: Lift` with `input: Machine.Crank.angle`. Input
expressions use the existing typed arithmetic owner; for example, negate the
input for a clockwise crank coordinate or divide an angular coordinate by two.
Reusable Time curves use clip time when input is omitted. Curve domains are
Time or Angle; values are Angle or Length. Interpolation is
explicitly Linear, Hold or Cosine. A periodic curve includes zero and period
keys with equal endpoint values. Without `period`, a curve holds its endpoint
values outside its key domain.

Keys must have unique names and strictly increasing finite domain values.
`repeat: Once` clamps clip time; `repeat: Loop` wraps it, including negative
scrub times, and requires matching physical poses at the clip seam. Compiler
sampling can preserve the final endpoint explicitly. Each independent Mate has
one Track writer per clip. Unknown targets, Fixed targets, conflicting writers,
incorrect dimensions and driver cycles reject with source diagnostics.
Sampling is stateless and retains shared geometry; it does not accumulate the
last frame's transforms.

The complete executable witness is
[`articulated-motion.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/articulated-motion.firmament).
Inspect or sample it with `aetheris asm inspect` and
`aetheris asm motion <file> --clip Cycle --time 1.25 --json`.

These tracks prescribe admitted rigid joints. They do not yet admit SliderCrank
closure, deform a WireForm spring, export an open Interface contract from a
Subassembly, or create Motion-scoped Patterns. The editable V8 still uses its
existing qualified mechanism adapter for those relationships. A static STEP
export is the compiled engineering configuration; sampling a clip does not
silently replace it with a different posed artifact.

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
records the original witnesses and their subsequent repair status, ownership
and open decisions. The include, schema-header and symmetry casing repairs are
described in the [implementation report](../../release/FIRMAMENT-DIRECTIVES-AND-CASING.md).
The [decision register](../../development/FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md)
preserves the original authoring audit and later migration dispositions.

Use [supported features](../reference/supported-features.md) for capability
boundaries and the linked domain guides for detailed syntax. Proposals and
migration reports explain design/history; they do not grant source admission.

Feature, product Template, Record, Static and Table names use lexical lookup.
Same-named Records in different scopes are distinct nominal types, even when their
fields match. Values resolve in their declaration scope; callers bind explicit
arguments in the caller scope. Duplicate names in one namespace/scope reject,
and sibling-private data cannot be referenced. Complete include/Template
expansion source maps remain unfinished. `validate --json --explain` exposes
the implemented Feature and Template owners, lexical scopes, bound arguments and
available definition/use receipts, plus compile-time data symbol scopes and
identities, with these limits stated.


### Modular mechanism example

The [editable V8](editable-mechanisms.md) is a complete Inventory/Part/Subassembly/
Assembly/Scene project. Its construction recipes and product structure are authored
Firmament; typed Records carry dimensions and scalar rest inputs. One `Template<config:
CylinderConfig> Subassembly CylinderUnit` is instantiated eight times by keyed
`Pattern` entries derived with immutable `with`. Pattern mapping blocks admit
occurrences with local Concept Struct, Interface and Mate declarations; each key
owns their scope. No per-Part pose tables are required. Assembly
placements bind scalar Static/Record fields through the same checked value owner.
Assembly-owned Semantic datums bind in the owning occurrence; concrete WireForm
winding ports remain available across module boundaries. See the guide for static
Scene versus prescribed-motion limits and the intentional hierarchical identity
change from the old generated flat source.
