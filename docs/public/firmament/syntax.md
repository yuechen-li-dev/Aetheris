# Firmament V2 syntax

## Definitions, values and construction

Firmament describes a finite engineering artifact at compile time. `Model` is the
class-like owner of an artifact, `Struct` groups reusable construction, `Feature`
is a typed function-like recipe, and `Pattern` performs bounded repetition.
These analogies explain organization; they do not introduce runtime objects,
inheritance, mutable state or arbitrary loops.

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

A declaration has a PascalCase construct or type, an optional `<Variant>`, a
case-sensitive name and a brace-delimited body. Fields use `camelCase: value`.
Lists use brackets; calls use parentheses. Separate adjacent fields with a newline
or semicolon; dense one-line bodies may also use the owning grammar's established
field boundaries. Required terminators in specialized grammars are documented in
their guides.

| Source role | Preferred spelling |
| --- | --- |
| Constructs and types | `Model`, `Struct`, `Box`, `Profile`, `Feature`, `Length` |
| Built-in fields | `units`, `size`, `center`, `counterboreDepth` |
| Linking words | `using`, `as`, `on`, `over`, `with`, `include`, `expose`, `bind` |
| Local values and parameters | `stock`, `outline`, `mountPoint`, `depth` |
| Engineering symbols and enum values | `XY`, `+Z`, `Outer`, `ThroughAll`, `World`, `mm` |

`Profile outline using layout` selects its construction frame/context;
`guide.Bottom as south` gives traced geometry an explicit identity. `Using` and
`As` remain admitted aliases at those grammar positions. A field such as
`on: +Z` is distinct from a header clause such as `Concept Struct layout on XY`.
See [language style](language-style.md) for formatting and compatibility rules.

Built-in PascalCase field aliases remain admitted by their owners. User names,
named recipe arguments and published members preserve their authored spelling;
`Spec.Width` is not automatically renamed to `spec.width`. Unknown statements or
fields are errors in migrated AST scopes, regardless of whether the remaining
geometry could build.

## Frontend schema and validation

An optional `schema <Name>` header selects a specialized frontend. Put it before
`Model`, after any leading comments. Explicit selection is authoritative: rejection
does not retry another frontend. `Revolve` belongs to `Mechanical`; it is not a
schema name.

| Schema | Authoring guide |
| --- | --- |
| `Mechanical` | Ordinary parts, profiles and semantic features; the example above. |
| `WireForm` | [Formed wire](wire-form.md), bends and coils. |
| `Sweep` | Circular-section Concept Path sweeps. |
| `SectionChain` | [Section chains](section-chains.md) and ruled/G1 bodies. |
| `Garment` | [Garments](garments.md), panels, stitch interfaces and fitting poses. |

Use `aetheris validate file.firmament --json` before building. Normal mechanical
validation performs engineering compilation in memory. `--parse-only` reports
`ParsedNotBuilt`, rather than claiming geometry construction has succeeded.
`--explain` exposes the chosen parser route as developer metadata. See
[diagnostics](../reference/diagnostics.md) for the structured diagnostic contract.

Normal mechanical construction, Revolve, Gear, Panel, Plateau, SectionChain and
Loft bind their admitted inputs from the shared recursive-descent AST. Lowercase
primitive solids, derived Box records, exposure aliases, Hole modifications and region/cut operations now
use that AST too; their remaining legacy semantic extensions have separate owners.
See [migration status](../../release/FIRMAMENT-LOWERCASE-AST-MIGRATION.md) for the
qualified boundary. Specialized frontend coverage is not a promise of a single
whole-language AST binder yet.

## Compatibility spelling

New source uses `Model`, `units: mm` and direct named primitives, as above.
Historical `model`, `units mm`, `solid name: Box`, `modify` and `hole<shaft>` are
bounded compatibility spellings. Inside that historical primitive grammar only,
bare length values inherit the model's millimetres. Native fields retain explicit
dimensional values: write `size: [30mm, 24mm, 12mm]`.

`solid tall: stock with { size: [30, 24, 20] }` retains immutable Box derivation:
the original `stock` is unchanged. `expose { face(+Z) as top }` names a face;
the historical `face(+Z) => top` association remains compatible. Neither form
requests an extra geometric operation.

These aliases preserve legitimate existing intent; they do not establish a second
preferred language. The convention formatter does not mechanically convert old
solid grammar into native declarations. Serialization-shaped V1 `model:` and
`firmament:` documents have an explicit compatibility boundary and are not native
V2 examples. Unknown casing is never a request for arbitrary case-insensitive
interpretation.

## Reusable semantic construction with Feature

`Feature` is Firmament's function-like abstraction for reusable semantic construction. It accepts typed inputs and returns exactly one typed semantic output. Feature evaluation is pure, deterministic, file-local, and expanded before Feature AIR; a definition does not materialize geometry until it is invoked.

```firmament
Feature M8Counterbore(center: Point2, depth: Length = 4mm) -> Hole<Counterbore> {
    let counterboreDiameter: Length = 8.5mm + 5.5mm
    return Hole<Counterbore> {
        on: +Z
        center: center
        diameter: 8.5mm
        counterboreDiameter: counterboreDiameter
        counterboreDepth: depth
        end: ThroughAll
    }
}

Modify Plate {
    M8Counterbore(center: Point2(-20mm, 0mm))
    M8Counterbore(center: Point2(20mm, 0mm), depth: 5mm)
}
```

Parameters are strongly typed and calls may use positional or named arguments. Defaults are optional. A body contains immutable typed `let` derivations and optional private `Point2`, `Line2`, `CubicBezier2`, `Rect2`, `RoundedRect2`, `Circle2`, and `Profile` construction declarations followed by one terminal `return`. Local construction names are qualified separately for each invocation. Product Templates specialize before Feature argument binding; Feature results then lower before patterns and Feature AIR. A Feature may return `Hole<Shaft>`, `Hole<Counterbore>`, `Hole<Countersink>`, `Boss`, `Pocket`, `EdgeFinish`, or `Profile`, and may call another Feature with the same return type. Direct and indirect recursion are rejected. Feature names occupy the callable namespace, separate from typed construction declarations such as `Profile`. Named results use `Feature Name = Function(...)`; the name becomes the returned feature identity and appears in invocation inspection. See [functional pickup construction](functional-pickup.md) for a qualified part example and [garment authoring](garments.md) for reusable Profile recipes.

Firmament intentionally does not provide general runtime control flow. `if`/`else`, conditional expressions, `match` inside Feature, mutable variables, exceptions, and `for`/`while` loops are rejected. A Firmament program describes one deterministic engineering construction, not a runtime family of topologically unrelated outputs.

Use each abstraction for its single role:

| Construct | Role |
| --- | --- |
| `Feature` | Function-like reusable semantic construction. |
| `Pattern` | Bounded finite geometric repetition; it is the loop substitute. |
| `Template` | Compile-time product/configuration specialization; it is the structural branch substitute. |
| `|>` | Finite semantic composition of compatible Profile/Path geometry. |
| `Concept` | Semantic contract or capability. |
| `Record` / `Struct` | Typed data and semantic structure. |
| `Set<T>` | Finite immutable source-ordered data whose entries have stable names. |

Feature composes with an existing bounded Pattern without exposing its internals:

```firmament
Feature MountHole(center: Point3, diameter: Length = 6mm) -> Hole<Shaft> {
    return Hole<Shaft> {
        on: Base.Top
        center: center
        diameter: diameter
        end: ThroughAll
    }
}

Concept Struct Design {
    Bounds: Box3 { Size: [60mm, 40mm, 10mm] }
    Points: Grid {
        Within: Bounds.Face(+Z).Inset(8mm)
        Columns: 2
        Rows: 2
    }
}

Pattern Mounts {
    Source: Design.Points
    MountHole(center: Item)
}
```

The two-column, two-row Grid is the explicit four-hole rectangular distribution. This semantic PointSet Pattern uses `Item`. The older `Pattern Name over Records { Template<Current> }` form remains the record-array composition syntax for finite feature Templates; `Current` is the selected record. Feature functions do not replace that compatibility form.

A Feature may return a call to another Feature with the same declared result type:

```firmament
Feature CoreCounterbore(center: Point2, depth: Length) -> Hole<Counterbore> {
    return Hole<Counterbore> {
        on: +Z
        center: center
        diameter: 8.5mm
        counterboreDiameter: 14mm
        counterboreDepth: depth
        end: ThroughAll
    }
}

Feature M8Counterbore(center: Point2) -> Hole<Counterbore> {
    return CoreCounterbore(center: center, depth: 4mm)
}
```

Structurally different edge finishes should be separate Features —for example, `StandardChamfer` and `StandardFillet`. A caller may instead select a specialized product with Template where that owning Template grammar admits the returned construction. Feature itself cannot branch between them.

Feature is supported by the `Mechanical` frontend. `WireForm`, `Sweep`, and `SectionChain` reject Feature declarations explicitly; shared cross-schema Feature IR and module/library packaging are deferred.


## Finite named sets and mapping

`Set<T>` is a finite immutable collection of named typed values. Use it when entries have semantic identity and should be addressable by name; use `T[]` for an anonymous ordered sequence.

```firmament
Static MountPoints: Set<Point2> {
    LowerLeft  => Point2(-16mm, -9mm)
    LowerRight => Point2(16mm, -9mm)
    UpperLeft  => Point2(-16mm, 9mm)
    UpperRight => Point2(16mm, 9mm)
}

Pattern Mounts over MountPoints {
    point => M8Counterbore(center: point)
}
```

`MountPoints.LowerLeft` has type `Point2`; Record-valued entries compose naturally, for example `MountSpecs.Left.Center`. Set entry names are unique, values may be equal, and authored order is authoritative. An empty typed Set is valid and a Pattern over it produces zero instances. Inspection exposes the element type, cardinality, entries, source order, provenance, and generated associations.

Inside a Set, `Name => Value` is a named association. Inside Pattern, `value => construction` maps every member of that finite Set to one semantic construction. Neither spelling is conditional control flow. Set has no mutation, indexing, filtering, sorting, `Map`, `Fold`, arbitrary keys, or runtime collection API, and Pattern retains the existing 1,024-instance safety bound.

Choose among the closed data forms by meaning: a `Record` is one structured value; an `Enum` is one-of-N alternatives; `T[]` is anonymous ordered data; `Set<T>` is a dataset in which all N named entries exist simultaneously. Pattern performs finite construction mapping, Feature supplies a reusable semantic transformation, and `|>` composes finite compatible geometry. Match is unchanged.

## Mirror and radial symmetry

Mirror and radial Pattern are resolved before geometry materialization. They transform semantic construction intent, not finished BRep topology.

An explicitly named destination derives from an existing Profile or concrete feature and an existing Plane:

```firmament
Plane CenterPlane { origin: [0mm,0mm,0mm]; normal: [1,0,0]; up: [0,0,1] }
Hole<Shaft> LeftMount { on: +Z; center: Point2(24mm,0mm); diameter: 6mm; end: ThroughAll }
Mirrored Feature RightMount From LeftMount { Across: CenterPlane }
```

Profiles may use either that direct Plane spelling or a `Construction Plane` traced from a `Concept Struct` Plane. Reflection analytically transforms points and curves, reverses arc traversal, then restores the Profile's canonical outer/inner winding. Frames use reflected origin/X/Z followed by canonical right-handed Y reconstruction; negative-determinant frames never reach a consumer. Mirroring an already-materialized STEP/BRep is not safe authoring and is not provided by this syntax.

Keep the source Profile semantic too. For example, an arbitrary triangular source should be authored as `Triangle2<Explicit> LeftBoundary { A: [...] B: [...] C: [...] }` and consumed with `LeftBoundary |> TraceLoop`; an axis-aligned four-sided section should use `Rect2`. Use `Polygon2<Explicit>` only when the linear boundary is genuinely an otherwise-unconstrained polygon. Closed boundaries lower to ordinary named Profile guides before Mirror resolves them.

A radial feature Pattern uses an existing Axis and includes the source construction as instance zero:

```firmament
Axis MainAxis { origin: [0mm,0mm,0mm]; direction: [0,0,1] }
Hole<Shaft> BoltSeed { on: +Z; center: Point2(26mm,0mm); diameter: 6mm; end: ThroughAll }
Radial Pattern BoltCircle { Source: BoltSeed About: MainAxis Count: 6 Angle: full }
```

`Count` is in `1..1024`; one is a valid trivial Pattern. `full` uses `theta[i] = i * 2*pi/Count` and omits the full-angle duplicate endpoint. Partial angles use inclusive endpoints, `theta[i] = i * Angle/(Count-1)`. Angles reuse Revolve spellings (`radians`, `deg`, `quarter`, `half`, `full`). Current radial feature lowering is deliberately bounded to planar Point2-centered Hole semantics about the support-normal Axis. Profile radial Pattern, whole Model/Struct derivation, arbitrary selector rebinding, Boss/Pocket radial derivation, and assembly occurrence symmetry remain explicit future qualifications.

Intentional asymmetry uses immutable `with`, not a mirror-exception mini-language:

```firmament
Mirrored Feature RightMount From LeftMount { Across: CenterPlane }
Feature RightMountCustom = RightMount with { Diameter: 8mm }
```

The derived feature retains the source/mirror/override provenance chain in `inspect --json`. Material, scalar magnitudes, and enum tags are invariant values; meaningless declarations such as `Mirrored Material` fail typed.

A Profile exactly on its reflection line may be retained as an equivalent named semantic derivation because it has not yet created material. A concrete feature whose center is unchanged is rejected as `firmament-symmetry-redundant-feature-mirror` so a Compose/Modify cannot silently create coincident duplicate material.

### Built-in closed polygons

`Polygon2<Rhombus>` is the first bounded closed-polygon authoring form. A rhombus is specified by its center and full horizontal/vertical diagonal lengths:

```firmament
Concept Struct Layout on XY {
    Polygon2<Rhombus> MountBoundaryShape {
        center: [0mm, 0mm]
        diagonals: [90mm, 64mm]
    }
}

Profile MountBoundary using Layout {
    Loop Outer { MountBoundaryShape |> TraceLoop }
}
```

This produces vertices at south, east, north, and west half-diagonal offsets and a counter-clockwise closed boundary. It lowers before pipeline validation to the ordinary typed `Point2`, `Line2`, and Profile path, so closure, orientation, Span containment, materialization, and STEP export keep their existing owners.

The angle-bracket argument is a closed built-in shape variant, not a general generic or user-defined type. The admitted variant is `Rhombus`; both diagonals must be finite positive lengths. It intentionally does not add arbitrary vertex arrays, `Connect`, a scene graph, or a second polygon materializer. Use `Rect2` when the four sides are axis-aligned; unequal horizontal and vertical rhombus diagonals do not describe that rectangle.

## Finite Profile and Path composition

Named single-loop Profiles can be placed through Concept Curve2 values before
being traced into a new Profile. See [Concept derivation](concept-derivation.md)
for From/On/Translate/Rotate and bounded uniform Scale/Pivot syntax.

`|>` composes compatible semantic geometry in a finite authoring pipeline. In a Profile loop, each source span is traced in authored order and automatically oriented to continue from the preceding endpoint:

```firmament
Profile BaseProfile using Layout {
    Loop Outer {
        Stock.Bottom
        |> Stock.Right
        |> Stock.Top
        |> Stock.Left
        |> Close
    }
}
```

The first stage uses its source direction. Later stages match the current endpoint against the candidate span's endpoints. A unique end match reverses the traced geometry automatically; no match reports `firmament-profile-pipeline-disconnected`, and a degenerate two-way match reports `firmament-profile-pipeline-orientation-ambiguous`. `Reverse Stock.Left` prefers reversed orientation first and then falls back to the normal connectable orientation; on the opening stage it selects the initial direction. `Stock.Bottom as MountingEdge` changes the output segment identity while retaining source provenance.

To trace only part of a guide, use `Guide To Point`. The current pipeline endpoint is the implicit start. On the opening stage, use `Guide From Start To End`; `Guide To End` may omit `From` when the guide's natural start is intended. Endpoints must be named points on the guide. This is bounded selection, not a general slicing expression:

```firmament
Profile Bracket using Layout {
    Horizontal.Bottom as South
        |> Horizontal.Right as East
        |> Horizontal.Top To Notch as Inner
        |> Vertical.Right To Vertical.TopRight as Upright
        |> Vertical.Top as North
        |> Vertical.Left To Horizontal.BottomLeft as West
        |> Close
}
```

The same form traces partial circular arcs. Endpoint order chooses the normal counter-clockwise circle span; `Reverse` selects the clockwise span. No pipeline `Sweep` keyword is needed.

A half-circle revolve meridian uses the same syntax through the ordinary Mechanical schema:

```firmament
schema Mechanical

Model RevolvedSphere {
    units: mm
    Axis MainAxis { origin: [0mm, 0mm, 0mm]; direction: [0, 1, 0] }
    Point2 South { position: [0mm, -20mm] }
    Point2 North { position: [0mm, 20mm] }
    Point2 Center { position: [0mm, 0mm] }
    Concept Circle2 Meridian { center: Center; radius: 20mm }
    Line2 Diameter { from: North; to: South }
    Profile SphereSection {
        Meridian From South To North as Arc
            |> Diameter as AxisClosure
            |> Close
    }
    Revolve Ball { profile: SphereSection; about: MainAxis; angle: full }
}
```

`|> Close` validates that the authored chain already closes. It never invents a missing line. Any admitted closed ordered guide family may be copied with `Outline |> TraceLoop`, including `Rect2`, `Square2`, `RoundedRect2`, `Polygon2`, `RegularPolygon2`, a closed `Concept Path`, `Circle2`, and `Ellipse2`. The resulting identities are the family's ordinary named edges; full circles and ellipses retain the single `Boundary` identity. `TraceLoop` normalizes winding for the target outer or inner loop when required.

Recognized guide declarations may appear at the start of an implicit-loop Profile body, followed by exactly one pipeline expression:

```firmament
Profile SideProfile using PositiveXWorkplane {
    Rect2 Outline { center: [0mm, 0mm]; size: [20mm, 10mm] }
    Outline |> TraceLoop
}
```

Segment names are Profile-wide identities. Manual `Segment` and pipeline stages both preserve their authored or inherited leaf names; loops do not silently qualify them. A traced full `Circle2` or `Ellipse2` derives its segment identity from the guide name, so `Outside |> TraceLoop` and `Inside |> TraceLoop` have distinct identities. If two loops would introduce the same name, binding reports `firmament-profile-segment-identity-collision`. Resolve the collision explicitly with `as` on the pipeline stages (for example, `Cutout.Bottom as CutoutBottom`) or by renaming a manual Segment.

A `Concept Path` may also compose existing named spans and remain open:

```firmament
Concept Path Guide {
    Stock.Bottom
    |> Stock.Right
    |> Stock.Top as Return
}
```

Pipeline source order is semantic order. Stages are limited to geometry references (qualified or unqualified), bounded `From`/`To` named endpoints, optional `Reverse`, optional `as`, `Close`, and `TraceLoop` in their admitted contexts. The operator has lower binding precedence than member access and does not admit arithmetic stages, calls, lambdas, conditionals, filtering, mutation, runtime execution, or repetition. Use Feature for a reusable semantic transformation, Pattern for bounded repetition, Template for specialization, and `|>` for finite semantic composition. Manual `Segment { Trace/From/To }` remains supported for compatibility and deliberate low-level parity tests; ordinary canonical Profile authoring does not require it.
