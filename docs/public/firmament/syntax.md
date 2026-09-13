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

Canonical fields use a colon between name and value. Braces delimit declarations, brackets delimit lists, and semicolons are optional where the owning line/block grammar is unambiguous. `Model`, `SheetMetal`, `Analysis`/import, and Assembly retain domain-appropriate target grammars; the [target reference](../reference/targets.md) records the intentional distinctions.

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

The first stage uses its source direction. Later stages match the current endpoint against the candidate span's endpoints. A unique end match reverses the traced geometry automatically; no match reports `firmament-profile-pipeline-disconnected`, and a degenerate two-way match reports `firmament-profile-pipeline-orientation-ambiguous`. `Reverse Stock.Left` is the explicit escape hatch. `Stock.Bottom As MountingEdge` changes the output segment identity while retaining source provenance. Unrenamed outer spans inherit their source leaf name; inner-loop spans are qualified by the loop name to preserve Profile-wide uniqueness.

`|> Close` validates that the authored chain already closes. It never invents a missing line. A real closed `Concept Path` may be copied as a whole with `Outline |> TraceLoop`; a rectangle, Body, Face, open path, or multi-loop container is not guessed into a loop. `TraceLoop` preserves span identities and normalizes winding for the target outer or inner loop when required.

A `Concept Path` may also compose existing named spans and remain open:

```firmament
Concept Path Guide {
    Stock.Bottom
    |> Stock.Right
    |> Stock.Top As Return
}
```

Pipeline source order is semantic order. Stages are limited to qualified geometry references, optional `Reverse`, optional `As`, `Close`, and `TraceLoop` in their admitted contexts. The operator has lower binding precedence than member access and does not admit arithmetic stages, calls, lambdas, conditionals, filtering, mutation, runtime execution, or repetition. Use Feature for a reusable semantic transformation, Pattern for bounded repetition, Template for specialization, and `|>` for finite semantic composition. Manual `Segment { Trace/From/To }` remains the explicit low-level Profile escape hatch.
