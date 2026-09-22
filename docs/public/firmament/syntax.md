# Firmament V2 syntax

## Frontend schema

`schema` is an optional file-level frontend selector, not a namespace or data-validation schema. Put it before `Model` (comments and whitespace may precede it). Schema-free files retain compatibility routing; new specialized sources should state their owner explicitly.

| Schema | Use it for |
| --- | --- |
| `Mechanical` | Ordinary V2 mechanical parts and features. |
| `WireForm` | Formed wire, bends, coils, and knots. |
| `Sweep` | Circular-section Concept Path sweeps. |
| `Revolve` | Bounded Profile revolution about an explicit coplanar Axis. |
| `SectionChain` | Standalone ruled/G1 section-chain bodies. |

For example, begin a formed-wire program with `schema WireForm`. An explicit schema is authoritative: if its frontend rejects the program, Firmament does not retry another frontend. Historical V1 `schema:` mappings remain compatibility metadata; they are distinct from this `schema <Name>` header.

A canonical native part begins with `Model`, declares `Units`, creates named solids, and applies semantic features in `Modify` or `Compose`:

The bounded material-offset subset is `AddOffset<Prism>` and `RemoveOffset<Prism>` inside an active `Compose`; see [bounded material offsets](material-offsets.md). `Cylinder` and `Sphere` offset families are reserved but not qualified.

```firmament
Model Plate {
    Units: mm
    Box Body { Size: [80mm, 50mm, 8mm] }
    Modify Body {
        Hole<Shaft> Mount { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll }
    }
}
```

Firmament-owned vocabulary is canonically PascalCase: declarations, semantic features, built-in values, and field names such as `Size`, `Body`, and `Region`. User-defined identifiers are case-preserving and stylistically unrestricted. External engineering identifiers such as `Standard.Materials.Aluminum.5052_H32`, standards, imported identities, and part numbers preserve their source spelling. New Firmament-owned V2 vocabulary does not use snake_case.

Accepted lowercase or historical spellings remain compatibility inputs where they are cheap and unambiguous. They select the same semantic route and produce no style warning. Canonical documentation, snippets, and fixtures use only the preferred spelling; casing is not a type-system distinction.

Canonical fields use a colon between name and value. Braces delimit declarations, brackets delimit lists, and semicolons are optional where the owning line/block grammar is unambiguous. `Model`, `SheetMetal`, `Analysis`/import, and Assembly retain domain-appropriate target grammars; the [target reference](../reference/targets.md) records the intentional distinctions. Assembly definitions may use compile-time `Include`, first-class `Subassembly`, typed `Interface<T>`, atomic `Mate`, and explicit `Expose`; see [typed interfaces and reusable subassemblies](assemblies.md).

Firmament V1 is compatibility history rather than canonical V2 authoring. Bodies embedded in `.firmfixture` entries marked future, not implemented, or invalid are corpus evidence and do not define the public language.

An untransformed `Box` stock is centered in X/Y with `Bottom` at Z=0 and `Top` at Z=`Height`. Its local feature coordinates use that same stock frame: adding holes or edge finishes does not translate the stock.

Firmament admits declarations through typed semantic namespaces. Construction `Profile` names may coincide with `Boss` or `Pocket` feature names because feature `Profile:` references are explicitly typed; these identities remain distinct in structured inspection. Names may not otherwise shadow another declaration in the same semantic namespace.

Use `aetheris validate file.firmament --json` before building. Diagnostics are codes intended for automation plus short corrective messages. See [diagnostics](../reference/diagnostics.md).

## Reusable semantic construction with Feature

`Feature` is Firmament's function-like abstraction for reusable semantic construction. It accepts typed inputs and returns exactly one typed semantic output. Feature evaluation is pure, deterministic, file-local in X1, and expanded before Feature AIR; a definition does not materialize geometry until it is invoked.

```firmament
Feature M8Counterbore(Center: Point2, Depth: Length = 4mm) -> Hole<Counterbore> {
    let CounterboreDiameter: Length = 8.5mm + 5.5mm
    return Hole<Counterbore> {
        On: +Z
        Center: Center
        Diameter: 8.5mm
        CounterboreDiameter: CounterboreDiameter
        CounterboreDepth: Depth
        End: ThroughAll
    }
}

Modify Plate {
    M8Counterbore(Center: Point2(-20mm, 0mm))
    M8Counterbore(Center: Point2(20mm, 0mm), Depth: 5mm)
}
```

Parameters are strongly typed and calls may use positional or named arguments. Defaults are optional. A body contains immutable typed `let` derivations followed by one terminal `return`. A Feature may return `Hole<Shaft>`, `Hole<Counterbore>`, `Hole<Countersink>`, `Boss`, `Pocket`, or `EdgeFinish`, and may call another Feature with the same return type. Direct and indirect recursion are rejected. Feature names occupy the callable namespace, separate from typed construction declarations such as `Profile`.

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
Feature MountHole(Center: Point3, Diameter: Length = 6mm) -> Hole<Shaft> {
    return Hole<Shaft> {
        On: Base.Top
        Center: Center
        Diameter: Diameter
        End: ThroughAll
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
    MountHole(Center: Item)
}
```

The two-column, two-row Grid is the explicit four-hole rectangular distribution. This semantic PointSet Pattern uses `Item`. The older `Pattern Name Over Records { Template<Current> }` form remains the record-array composition syntax for finite feature Templates; `Current` is the selected record. Feature functions do not replace that compatibility form.

## Finite named sets and mapping

`Set<T>` is a finite immutable collection of named typed values. Use it when entries have semantic identity and should be addressable by name; use `T[]` for an anonymous ordered sequence.

```firmament
Static MountPoints: Set<Point2> {
    LowerLeft  => Point2(-16mm, -9mm)
    LowerRight => Point2(16mm, -9mm)
    UpperLeft  => Point2(-16mm, 9mm)
    UpperRight => Point2(16mm, 9mm)
}

Pattern Mounts Over MountPoints {
    point => M8Counterbore(Center: point)
}
```

`MountPoints.LowerLeft` has type `Point2`; Record-valued entries compose naturally, for example `MountSpecs.Left.Center`. Set entry names are unique, values may be equal, and authored order is authoritative. An empty typed Set is valid and a Pattern over it produces zero instances. Inspection exposes the element type, cardinality, entries, source order, provenance, and generated associations.

Inside a Set, `Name => Value` is a named association. Inside Pattern, `value => construction` maps every member of that finite Set to one semantic construction. Neither spelling is conditional control flow. Set has no mutation, indexing, filtering, sorting, `Map`, `Fold`, arbitrary keys, or runtime collection API, and Pattern retains the existing 1,024-instance safety bound.

Choose among the closed data forms by meaning: a `Record` is one structured value; an `Enum` is one-of-N alternatives; `T[]` is anonymous ordered data; `Set<T>` is a dataset in which all N named entries exist simultaneously. Pattern performs finite construction mapping, Feature supplies a reusable semantic transformation, and `|>` composes finite compatible geometry. Match is unchanged.

## Mirror and radial symmetry

Mirror and radial Pattern are resolved before geometry materialization. They transform semantic construction intent, not finished BRep topology.

An explicitly named destination derives from an existing Profile or concrete feature and an existing Plane:

```firmament
Plane CenterPlane { Origin: [0mm,0mm,0mm]; Normal: [1,0,0]; Up: [0,0,1] }
Hole<Shaft> LeftMount { On: +Z; Center: Point2(24mm,0mm); Diameter: 6mm; End: ThroughAll }
Mirrored Feature RightMount From LeftMount { Across: CenterPlane }
```

Profiles may use either that direct Plane spelling or a `Construction Plane` traced from a `Concept Struct` Plane. Reflection analytically transforms points and curves, reverses arc traversal, then restores the Profile's canonical outer/inner winding. Frames use reflected origin/X/Z followed by canonical right-handed Y reconstruction; negative-determinant frames never reach a consumer. Mirroring an already-materialized STEP/BRep is not safe authoring and is not provided by this syntax.

Keep the source Profile semantic too. For example, an arbitrary triangular source should be authored as `Triangle2<Explicit> LeftBoundary { A: [...] B: [...] C: [...] }` and consumed with `LeftBoundary |> TraceLoop`; an axis-aligned four-sided section should use `Rect2`. Use `Polygon2<Explicit>` only when the linear boundary is genuinely an otherwise-unconstrained polygon. Closed boundaries lower to ordinary named Profile guides before Mirror resolves them.

A radial feature Pattern uses an existing Axis and includes the source construction as instance zero:

```firmament
Axis MainAxis { Origin: [0mm,0mm,0mm]; Direction: [0,0,1] }
Hole<Shaft> BoltSeed { On: +Z; Center: Point2(26mm,0mm); Diameter: 6mm; End: ThroughAll }
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
Concept Struct Layout On XY {
    Polygon2<Rhombus> MountBoundaryShape {
        Center: [0mm, 0mm]
        Diagonals: [90mm, 64mm]
    }
}

Profile MountBoundary Using Layout {
    Loop Outer { MountBoundaryShape |> TraceLoop }
}
```

This produces vertices at south, east, north, and west half-diagonal offsets and a counter-clockwise closed boundary. It lowers before pipeline validation to the ordinary typed `Point2`, `Line2`, and Profile path, so closure, orientation, Span containment, materialization, and STEP export keep their existing owners.

The angle-bracket argument is a closed built-in shape variant, not a general generic or user-defined type. X1 admits only `Rhombus`; both diagonals must be finite positive lengths. It intentionally does not add arbitrary vertex arrays, `Connect`, a scene graph, or a second polygon materializer. Use `Rect2` when the four sides are axis-aligned; unequal horizontal and vertical rhombus diagonals do not describe that rectangle.

A Feature may return a call to another Feature with the same declared result type:

```firmament
Feature CoreCounterbore(Center: Point2, Depth: Length) -> Hole<Counterbore> {
    return Hole<Counterbore> {
        On: +Z
        Center: Center
        Diameter: 8.5mm
        CounterboreDiameter: 14mm
        CounterboreDepth: Depth
        End: ThroughAll
    }
}

Feature M8Counterbore(Center: Point2) -> Hole<Counterbore> {
    return CoreCounterbore(Center: Center, Depth: 4mm)
}
```

Structurally different edge finishes should be separate Features in X1—for example, `StandardChamfer` and `StandardFillet`. A caller may instead select a specialized product with Template where that owning Template grammar admits the returned construction. Feature itself cannot branch between them.

For X1, Feature is supported by the `Mechanical` frontend. `WireForm`, `Sweep`, and `SectionChain` reject Feature declarations explicitly; shared cross-schema Feature IR and module/library packaging are deferred.

## Finite Profile and Path composition

`|>` composes compatible semantic geometry in a finite authoring pipeline. In a Profile loop, each source span is traced in authored order and automatically oriented to continue from the preceding endpoint:

```firmament
Profile BaseProfile Using Layout {
    Loop Outer {
        Stock.Bottom
        |> Stock.Right
        |> Stock.Top
        |> Stock.Left
        |> Close
    }
}
```

The first stage uses its source direction. Later stages match the current endpoint against the candidate span's endpoints. A unique end match reverses the traced geometry automatically; no match reports `firmament-profile-pipeline-disconnected`, and a degenerate two-way match reports `firmament-profile-pipeline-orientation-ambiguous`. `Reverse Stock.Left` prefers reversed orientation first and then falls back to the normal connectable orientation; on the opening stage it selects the initial direction. `Stock.Bottom As MountingEdge` changes the output segment identity while retaining source provenance.

To trace only part of a guide, use `Guide To Point`. The current pipeline endpoint is the implicit start. On the opening stage, use `Guide From Start To End`; `Guide To End` may omit `From` when the guide's natural start is intended. Endpoints must be named points on the guide. This is bounded selection, not a general slicing expression:

```firmament
Profile Bracket Using Layout {
    Horizontal.Bottom As South
        |> Horizontal.Right As East
        |> Horizontal.Top To Notch As Inner
        |> Vertical.Right To Vertical.TopRight As Upright
        |> Vertical.Top As North
        |> Vertical.Left To Horizontal.BottomLeft As West
        |> Close
}
```

The same form traces partial circular arcs. Endpoint order chooses the normal counter-clockwise circle span; `Reverse` selects the clockwise span. No pipeline `Sweep` keyword is needed.

A half-circle revolve meridian uses the same syntax through the ordinary Mechanical schema:

```firmament
schema Mechanical

Model RevolvedSphere {
    Units: mm
    Axis MainAxis { Origin: [0mm, 0mm, 0mm]; Direction: [0, 1, 0] }
    Point2 South { Position: [0mm, -20mm] }
    Point2 North { Position: [0mm, 20mm] }
    Point2 Center { Position: [0mm, 0mm] }
    Concept Circle2 Meridian { Center: Center; Radius: 20mm }
    Line2 Diameter { From: North; To: South }
    Profile SphereSection {
        Meridian From South To North As Arc
            |> Diameter As AxisClosure
            |> Close
    }
    Revolve Ball { Profile: SphereSection; About: MainAxis; Angle: full }
}
```

`|> Close` validates that the authored chain already closes. It never invents a missing line. Any admitted closed ordered guide family may be copied with `Outline |> TraceLoop`, including `Rect2`, `Square2`, `RoundedRect2`, `Polygon2`, `RegularPolygon2`, a closed `Concept Path`, `Circle2`, and `Ellipse2`. The resulting identities are the family's ordinary named edges; full circles and ellipses retain the single `Boundary` identity. `TraceLoop` normalizes winding for the target outer or inner loop when required.

Recognized guide declarations may appear at the start of an implicit-loop Profile body, followed by exactly one pipeline expression:

```firmament
Profile SideProfile Using PositiveXWorkplane {
    Rect2 Outline { Center: [0mm, 0mm]; Size: [20mm, 10mm] }
    Outline |> TraceLoop
}
```

Segment names are Profile-wide identities. Manual `Segment` and pipeline stages both preserve their authored or inherited leaf names; loops do not silently qualify them. If two loops would introduce the same name, binding reports `firmament-profile-segment-identity-collision`. Resolve the collision explicitly with `As` on the pipeline stages (for example, `Cutout.Bottom As CutoutBottom`) or by renaming a manual Segment.

A `Concept Path` may also compose existing named spans and remain open:

```firmament
Concept Path Guide {
    Stock.Bottom
    |> Stock.Right
    |> Stock.Top As Return
}
```

Pipeline source order is semantic order. Stages are limited to geometry references (qualified or unqualified), bounded `From`/`To` named endpoints, optional `Reverse`, optional `As`, `Close`, and `TraceLoop` in their admitted contexts. The operator has lower binding precedence than member access and does not admit arithmetic stages, calls, lambdas, conditionals, filtering, mutation, runtime execution, or repetition. Use Feature for a reusable semantic transformation, Pattern for bounded repetition, Template for specialization, and `|>` for finite semantic composition. Manual `Segment { Trace/From/To }` remains supported for compatibility and deliberate low-level parity tests; ordinary canonical Profile authoring does not require it.
