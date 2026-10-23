# Compile-time data and choices

Firmament data exists only while the compiler runs. Every value is fixed before
geometry is built, and none of it survives into the artifact except as the
dimensions it produced and any PMI you publish explicitly.

## Static values

`Static` binds a name to an immutable value in its scope:

```firmament
Model Plate {
  units: mm
  Static width = 40mm
  Static thickness: Length = width / 8
  Box stock { size: [width, width - 10mm, thickness] }
}
```

`Static width = 40mm` infers `Length`; writing the type, as for `thickness`,
makes intent clearer and is checked. Arithmetic is dimension-checked: adding a
length to an angle is an error. Scalar Statics feed primitive and `Modify` fields
directly, and Compose intervals. Other domains take their inputs through typed
Template parameters instead.

Inside a `Feature`, use `let` for a local derived value. There is no general
`let` outside Feature bodies.

## Records and `with`

A `Record` declares a named data shape with typed fields. A Record value is
data, not a body.

```text
Record PlateSpec { width: Length; depth: Length; thickness: Length }
Static standard: PlateSpec = PlateSpec { width: 80mm depth: 50mm thickness: 8mm }
Static heavy: PlateSpec = standard with { thickness: 12mm }
```

`with` derives a new value with some fields replaced; the original is unchanged.
Overrides are type-checked, and an unknown field is an error. Records may nest,
must be finite and acyclic, and are limited to 128 levels of nesting.
[Lesson 4](../../learn/04-reuse.md) uses these in a complete file.

Record field initializers may carry a tolerance (`diameter: 8mm tol 0.1mm`); see
[requirements](requirements.md#tolerances). Replacing such a field without `tol`
replaces the requirement with an untoleranced value.

## Tables

A `Static Table` holds columns of values for a Record type, typically a range of
standard sizes. Index it to select a row:

```firmament
Record Spec { width: Length }
Static outer: Spec = Spec { width: 10mm }
Model Plate {
  units: mm
  Record Spec { width: Length thickness: Length }
  Static Table choices: Spec {
    width: [30mm, 40mm]
    thickness: [6mm, 8mm]
  }
  Static selected: Spec = choices[1]
  Box stock { size: [selected.width, 20mm, selected.thickness] }
}
```

Rows are typed exactly like literal Records. Tolerances in Table cells and other
collection elements are not supported and reject.

## Scope

Records, Statics and Tables belong to the scope that declares them. A local
declaration shadows the same name in an enclosing scope, and sibling scopes
cannot see each other's names.

Record types are nominal. In the example above, the Model's local `Spec` is a
different type from the file-level `Spec`, even though it reuses the name.
`outer` keeps its own one-field type and does not acquire `thickness`. The Table
uses the local type.

Template defaults resolve where the Template is defined; explicit arguments
resolve where it is invoked. Unknown fields, inaccessible names, duplicate
declarations and cycles reject.

## Choices: `Options` and `match`

`Options` (also spelled `Enum`) declares a set of mutually exclusive cases.
`match` selects one value by case:

```firmament
Model ConfiguredPlate {
  units: mm
  Options SizeChoice { Compact Standard }
  Static size: SizeChoice = Compact
  Static width: Length = match size {
    Compact => 40mm
    Standard => 80mm
  }
  Box stock { size: [width, 20mm, 5mm] }
}
```

The rules:

- **Exhaustive.** Every case, or both `true` and `false` for a Bool, appears
  exactly once. Missing, duplicate and unknown cases reject, even ones that would
  not be selected. There is no default arm, no fallthrough, no guard and no
  payload.
- **Typed.** All arms have the same result type. `Length` and `Angle` cannot
  share a result, and neither can two Record types that merely have identical
  fields. Cases can be qualified (`SizeChoice.Compact`); a case of another Enum
  with the same name is not interchangeable.
- **Lazy.** Only the selected arm is evaluated, so an unselected expression does
  not execute and an unselected spatial value builds no geometry. Names and
  fields in unselected arms are still checked.
- **An expression.** `match` produces a value. It does not conditionally include
  Feature, Part, Mate or Assembly declarations.

A Boolean comparison can be the scrutinee:
`match diameter > 8mm { true => 6mm; false => 4mm }`. Arms may be on separate
lines or separated by semicolons. A selected Record keeps its type, and a
selected toleranced value keeps its tolerance.

`match` is admitted in Static initializers, Record fields, Template inputs,
defaults and bodies, Feature inputs, defaults and locals, and Concept spatial
values such as `Box3` bounds and Grid point sets.

`Options` means one choice among several. It does not mean a set of enabled
flags.

### Older numeric selectors

Existing source may select among collections by count:
`Pattern ... over Match count { 4 => bolts4; 6 => bolts6; 8 => bolts8 }`. This
form is kept for compatibility, checks every arm and rejects an unlisted count.
It is not general integer matching. Use `Options` for new choices and Tables for
standard-value matrices.

## Collections: `Set<T>` and arrays

A choice picks one value; a collection keeps all of them for repetition.

```text
Static mountPoints: Set<Point2> {
  left => Point2(-12mm, 0mm)
  right => Point2(12mm, 0mm)
}
```

Each Set entry has a unique, stable name, and source order is authoritative.
Equal values are allowed. `mountPoints.left` selects one entry. An empty Set
gives zero repetitions.

Arrays (`[a, b, c]`) are ordered and anonymous. Results generated from an array
are numbered by position; results generated from a Set are named after their
keys and keep those names when entries are inserted, removed or reordered.
[Patterns](reuse.md#pattern-finite-repetition) consume both.
