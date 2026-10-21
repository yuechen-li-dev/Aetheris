# Patterns: a layout mapped to construction

A Firmament Pattern is finite compile-time construction. Linear and circular
repetition are useful layouts, but they are not the limits of the language.
Keep three responsibilities separate:

1. **Layout:** a keyed `Set<T>` or Concept `Points<Point2>` / `Points<Point3>`.
   Points may be explicit, equally spaced, placed at named stations, or generated
   by a checked `Series` expression.
2. **Recipe:** a `Feature` for repeated construction, or a shared Part/Subassembly
   definition for repeated components. A scalar `Function` computes dimensions
   and coordinates; it does not construct a body itself.
3. **Application:** `Pattern ... over ...` maps that finite collection to the
   recipe in the owning Model, Modify, assembly, or admitted domain scope.

## A complete nonlinear hole pattern

This file builds four diameter-3mm holes at X = -30, -25, -10 and 15mm.
The square-spacing law lives in a Function; the Pattern does not contain copied
coordinates or a second implementation of the equation.

```firmament
Function siteX(n: Int, pitch: Length) -> Length = -30mm + pitch * n * n;
Model EquationPlate {
 units: mm
 Concept Struct Layout {
  Points<Point2> Sites {
   Series { index: n; first: 0; count: 4; keyPrefix: Site;
    position: Point2(siteX(n,5mm),0mm); }
  }
 }
 Feature Drill(center: Point2) -> Hole<Shaft> {
  return Hole<Shaft> { on: +Z; center: center; diameter: 3mm; end: ThroughAll; }
 }
 Box Stock { size: [100mm,50mm,8mm] }
 Modify Stock { Pattern Drills over Layout.Sites { point => Drill(center: point) } }
}
```

Arithmetic can also appear directly in `position`. Functions compose scalar
arithmetic and the admitted dimensionless `Pow` builtin. Their supported
argument/result types are Int, Float, Length and Angle. Function calls currently
use positional arguments; Feature calls may use named arguments. There is no
host-code execution, recursion, mutation, I/O or arbitrary function library.
`Sin`/`Cos` are not currently admitted by this scalar Function evaluator.
See [Concept points and Functions](concept-points-and-functions.md) for its
checked units and evaluation budgets.

## Choose a layout for its intent

| Intent | Existing source form |
| --- | --- |
| Named irregular positions or per-site Record data | `Static sites: Set<T> { key => value }` |
| Equal pitch with named members | Concept Points with `keys` and `Linear { start; step; }` |
| Unequal distances along one direction | `Linear { start; direction; Stations { key: distance; } }` |
| Coordinates governed by a scalar design law | `Series { index; first; count; keyPrefix; position; }` |
| Existing planar circular feature symmetry | `Radial Pattern ...` in its admitted owner |
| Repeated complete equipment / components | Keyed assembly Pattern producing Part or Assembly occurrences |
| Cross-sections driven by station Records | Keyed Pattern producing Section values in SectionChain |

The guitar uses the equation route for 22 frets, named stations for nine inlays,
and ordinary finite Patterns for other components. A law may affect both
placement and recipe dimensions, as fret distance also determines fret width.
Changing the law can therefore reshape a part intentionally; preserved instance
identity does not imply preserved geometry.

Point2 stays local 2D data. An assembly placement explicitly supplies the frame
and lifts the coordinates into 3D. Point3 stays 3D. The local Points binder rejects a placement/contract header on
its containing Concept Struct rather than erasing it. Geometric Concept Struct
frames remain a separate supported scaffold owner. Neither the graph nor an
exporter should guess a missing frame or silently promote dimensionality.

## Keys, count and order

Set/Concept member keys identify repeated instances. Reordering explicit keyed
stations retains each key's distance. Changing a Series law or growing its count
retains identities for existing keys. Changing `first` changes the key domain;
changing `keyPrefix` renames the generated members. Those are identity edits,
not merely display labels. Ordered arrays and older Grid expansions retain their
existing positional identity rules.

A Series contains 1–1024 points; the current point expansion total is 4096.
An empty typed Set is admitted and produces no mapped instances. A zero-count
Series is currently rejected. A Pattern does not bypass the target feature's
geometry, overlap, containment or frame capability checks.

## Checked inputs and scope

Part and assembly Series consume the same checked Static and Record values.
Template specialization may supply those inputs too. For example, add
`Static count: Int = 4;` to the model and use `count: count` in Series. Changing
the Static changes the finite layout without turning its law into coordinates.

Function parameters are lexical locals. A Static named `pitch` does not replace
the `pitch` parameter in `siteX`; the call's actual argument controls the value.
Functions remain pure: pass design values as arguments rather than capturing
Statics implicitly. This distinction also applies to the Series index.

Typed tolerances cannot be silently discarded by layout evaluation. A tolerated
value that has no admitted point-layout tolerance projection is rejected; keep
manufacturing tolerance on the owning material feature and explicit PMI.

## Helios: Layout, Recipe, Instances

Open **Patterns…** from the graph construction menu, or select a Pattern, Points,
Series, Stations or Function and open its Pattern action.

- **Layout** reopens the authored collection and exposes its expressions,
  count/spacing inputs, station distances and scalar Function laws. An authored
  keyed Set entry has a separate value control; editing it keeps its key and
  original expression ownership. Derived entries stay read-only. References
  remain references until explicitly edited. Shared consumers are listed.
- **Recipe** exposes the keyed mapping and fields of the shared construction
  recipe. A Feature can be selected when creating an application; its typed
  parameter names seed the mapping. A component selector reuses an existing
  definition/specialization; explicit source/target frames and a coordinate law
  generate ordinary Part/Assembly Placement source. The mapping remains editable.
- **Instances** shows compiler-evaluated keys, values and expansion associations.
  Rows are read-only. The gold 2D plot is a **local layout**, not a world-space
  placement. The recipe's explicit support/frame still controls construction.

**New Pattern…** can use an existing keyed collection, create an equation Series,
or create named Stations. The initial create controls make Point2 layouts;
existing Point3 layouts retain their dimensionality and can be used/edited.
Review `first`, `keyPrefix` and station/Set keys carefully: changing them changes
identities. Changing an equation or distance keeps keys stable.

**Preview** compiles the complete candidate project with its includes. It does
not change the working source. **Apply** commits that exact admitted candidate
as one undoable source transaction. Changing any control invalidates the preview;
Cancel or a refused build leaves the working source and model intact. Layout
resolution alone reports `LayoutResolvedNotBuilt`, never a successful solid.

Imported definitions remain owned by their document; open that document to edit
its law or recipe. The current panel edits one source document per transaction.
Legacy radial/linear Pattern forms keep their production owners and show an
explicit read-only explanation when this finite-collection editor has no adapter.

**Grid seed hole…** remains a separate convenience action for a field-based Hole.
Its counts include the original seed, while its keyed Pattern maps the additional
holes. It is not an equation-layout editor and does not infer a grid from existing
coordinates.
