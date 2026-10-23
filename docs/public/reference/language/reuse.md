# Reuse

`Feature`, `Function`, `Pattern`, `Template` and the symmetry operations let you
write construction once and use it many times. All of them are expanded by the
compiler into ordinary, finite construction. None of them adds mutable state,
exceptions, unbounded recursion, `for`/`while` loops or `if`/`else` statements.

## Feature: a typed recipe

A Feature definition describes one result; calling it produces that result where
it is called.

```text
Feature Drill(center: Point2, diameter: Length = 6mm) -> Hole<Shaft> {
  let depth: Length = diameter * 2
  return Hole<Shaft> { on: +Z; center: center; diameter: diameter; end: Blind depth }
}
```

- Parameters are typed, can be passed by position or by name, and can have
  defaults. Defaults may use other parameters, including dimensional
  arithmetic; cyclic defaults are errors.
- The body holds immutable `let` values and private construction, then exactly
  one `return`.
- A Feature may call another Feature with the same result type. Recursion is
  rejected.
- Result kinds include the Hole variants, `Boss`, `Pocket`, `EdgeFinish` and
  `Profile`. Wrapping a feature in a Feature does not make it supported anywhere
  it was not already.
- WireForm, Sweep and SectionChain do not accept Feature declarations.

**Scope.** Feature names resolve lexically. A Feature in a Model shadows an outer
one with the same name; a Feature inside a Struct is invisible outside it; two
definitions in one scope are an error. Calls inside a Feature resolve where the
Feature was defined. A Feature and a Profile may share a name.

**Limits.** Expansion is bounded: 64 levels of nested calls or default
dependencies, 4,096 invocations, 65,536 expanded declarations and 4 MiB of
generated construction per expansion phase. Exceeding a limit rejects the whole
program; it never yields a partly generated part.

### Naming applications

Name a call when you will refer to its result, edit it or attach anything to it:

```firmament
Modify stock {
  Feature leftBore = MountHole(center: Layout.leftMount)
  Feature rightBore = MountHole(center: Layout.rightMount)
}
```

`MountHole` is the recipe, `leftBore` is this application's identity, and
`Layout.leftMount` supplies its location. Moving the datum moves the bore
without renaming it. Reordering named applications keeps their identities,
though order still decides construction dependencies. The name gives access to
what the result kind already publishes, for example `face(leftBore.Wall)` for a
hole wall. It does not add capabilities, and it does not promise that every face
survives a topology change.

Anonymous calls get positional names (`__0`, `__1`, ...). Do not use them as
durable references; name the call first, then update any selectors that used the
generated name. Expanded fields in reports are read-only: edit the call's
arguments or the datum it uses.

## Function: a pure expression

A `Function` returns a scalar, `Point2`, `Point3` or a declared Record from its
arguments. Functions have no access to outer names except through their
parameters; a Static with the same name as a parameter does not replace it.

`Curve2` and `Curve3` bind a point-valued expression to a finite parameter
range. Profiles and other consumers turn curves into material.

- `representation: Exact` requires an exact lowering.
  `representation: Approximate` requires a positive `maxDeviation` and reports
  the geometric error bound. This budget is unrelated to `tol` and PMI.
- `Layout.Curve.At(t)` evaluates the original equation within its domain. The
  parameter is not arc length.
- `Curve2` carries an explicit plane; `Curve3` uses its definition's world frame.

See [equation-driven curves](../../firmament/equation-curves.md) for complete
examples.

## Pattern: finite repetition

A Pattern maps a finite **source** to a **recipe**:

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

- Put a Pattern inside its target's `Modify`, or directly in a Model or Struct
  that has exactly one body.
- Sources can be named Sets, arrays, equal-pitch or irregular stations, or a
  Function-driven series, so not only linear and circular arrangements. See
  [Patterns](../../firmament/patterns.md) for a non-linear example.
- At most 1,024 instances.
- `=>` here binds the element name for the recipe. It is not a general lambda.
- The element name substitutes values only. It never renames fields,
  declarations, strings or similarly spelled identifiers.

**Identity.** Results from a Set are named by Pattern and key
(`mounts.left`, `mounts.right`) and keep those names when entries are inserted,
removed or reordered. Results from arrays and generated grids are positional.
`build --json` reports the generated instances under `patterns`.

Different owners use different Pattern forms. They are separate forms, not
alternative spellings of one loop, and `Item`, `Current` and an explicit element
name are not interchangeable:

| Source | Form |
| --- | --- |
| Named Set to a Feature | `Pattern name over values { value => Recipe(value) }` |
| Concept points from Linear, Stations or Series | `Pattern name over Layout.Points { point => Recipe(point) }` |
| Keyed assembly sites | `Pattern name over sites { site => <Part Member = Definition> ... </Part> }` |
| Semantic PointSet or Grid | `Pattern name { Source: layout.Points ... Item ... }` |
| Record array with a feature Template | `Pattern name over records { Recipe<Current> }` |
| Radial symmetry | `Radial Pattern name { source: seed; about: axis; count: n; angle: full }` |

In Helios, **Patterns…** edits the source layout, law and recipe, previews the
whole result and applies it as one undoable change.

## Template: specialize a product

```firmament
Template<width: Length, height: Length, thickness: Length>
Struct PlateFamily {
  Require positive => width > 0mm && height > 0mm && thickness > 0mm
  Box stock { size: [width, height, thickness] }
}

Struct standardPlate = PlateFamily<width: 80mm, height: 50mm, thickness: 8mm>
```

- A Template declares typed parameters and the kind of thing it produces:
  `Struct`, `Model`, `Concept Struct`, `Panel`, `SheetMetal`, `Profile` or
  `ProfileDelta`.
- `Require` decides whether a specialization is admitted.
- Parameters can be scalars or Records; a Record carries a whole configuration
  (see [lesson 4](../../learn/04-reuse.md)).
- Template names are lexical. A Model-local Template can shadow a file-level one;
  a sibling Model's Template is invisible; duplicates in one scope reject.
  Templates nested in a Template resolve from its definition, not from the
  caller.
- Forge Host and other tools discover file-level product Templates.
- A specialization header cannot silently drop placement or other clauses.

A separate, smaller feature-Template form is used by record-array Patterns. See
[Templates](../../firmament/templates.md) for argument types and Tables.

## Mirror and radial symmetry

Symmetry transforms construction before it is built. It never mirrors a finished
BRep or STEP file.

```firmament
Mirrored Feature rightMount from leftMount { across: centerPlane }
Feature largerMount = rightMount with { diameter: 8mm }
```

(Inside a `Modify`, with a `leftMount` Hole and a `centerPlane` declared.)

- `Mirrored Feature` and `Mirrored Profile result from source { across: plane }`
  reflect curves, restore loop winding and rebuild a right-handed frame.
  Construction planes traced from Concept planes are accepted in the Profile
  form.
- Materials, scalar values and enum values are unchanged by reflection.
  `Mirrored Material` is not an operation.
- `with` derives a feature with overrides and keeps its source and mirror
  provenance.
- A mirrored feature that would land exactly on its source is rejected as a
  redundant duplicate. A Profile lying on the mirror plane may remain as a named
  derivation.

```firmament
Radial Pattern boltCircle { source: boltSeed; about: mainAxis; count: 6; angle: full }
```

- The source is instance zero. `count` is 1 to 1,024.
- A full circle spaces instances at `i × 360°/count`, without a duplicate at the
  end. A partial `angle` includes both ends: `i × angle/(count − 1)`.
- Angles accept `deg`, `radians`, `quarter`, `half` and `full`.
- Supported for planar holes with a `Point2` centre, about their support's
  normal axis. Profiles, Boss, Pocket, whole Structs and assemblies are not
  supported.

Complete examples: [mirrored hole](../../../../fixtures/Canonical/Symmetry/mirrored-hole.firmament),
[mirror with override](../../../../fixtures/Canonical/Symmetry/mirrored-feature-with-override.firmament),
[radial bolt circle](../../../../fixtures/Canonical/Symmetry/radial-bolt-circle.firmament).
