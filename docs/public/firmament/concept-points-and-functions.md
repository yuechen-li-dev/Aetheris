# Concept points and Functions

Implemented: keyed local `Points<Point2>` / `Points<Point3>` collections and pure,
expression-bodied scalar `Function` declarations. Functions always evaluate
during compilation. There is no `Comptime` keyword or runtime execution.

## Nut crossings

```firmament
Concept Struct NutLayout {
 Points<Point2> Crossings {
  Keys: [String0, String1, String2, String3, String4, String5];
  Linear {
   Start: Point2(-18mm, 0mm);
   Step: [7.2mm, 0mm];
  }
 }
}
```

The six points are local millimeter coordinates. Named references such as
`NutLayout.Crossings.String0.X` and `.Y` resolve to checked scalar values.
`NutLayout.Crossings.String0` resolves to `Point2(-18mm,0mm)`; `.Position` is
an equivalent explicit point projection. A Point2 has no Z coordinate.
Moving the nut does not change this scaffold. A physical placement must name
the frame that gives these local coordinates their spatial meaning.

Function expressions and point-recipe fields use explicit semicolon terminators
in this bounded grammar.

Manual collections use `Key: Point2(...);` or `Key: Point3(...);` members:

```firmament
Concept Struct Supports {
 Points<Point3> Seats {
  Bass: Point3(-18mm,0mm,2mm);
  Treble: Point3(18mm,0mm,2mm);
 }
}
```

## Explicit stations along a line

An alternative Linear recipe declares independent, keyed distances rather than
equal pitch. It produces the same Point2/Point3 collection:

```firmament
Concept Struct FretboardLayout {
 Points<Point2> InlayPositions {
  Linear {
   Start: Point2(0mm,0mm);
   Direction: [0,-1];
   Stations {
    Fret3: 84.2mm;
    Fret5: 143.5mm;
    Fret7: 196.4mm;
    Fret9: 243.5mm;
    Fret12: 304.7mm;
    Fret15: 356.1mm;
    Fret17: 385.8mm;
    Fret19: 412.2mm;
    Fret21: 435.7mm;
   }
  }
 }
}
```

Each station computes `Start + normalized(Direction) * Distance`. Direction is
a nonzero dimensionless vector with the same dimensionality as the collection.
Distances are finite Length expressions; negative distances and explicitly
coincident stations are permitted. Keys are unique identifiers. Declaration
order supplies ordinals; stable identities depend on keys, so reordering does
not reassign positions. Keys/Step and Direction/Stations are mutually exclusive.
The existing 1024-point per-collection and 4096-point total bounds apply.

Guitar station distances are authored at **0.1mm resolution**, following the
default physical authoring convention when no tighter tolerance is stated.
Rounding to a tenth moves an old station by at most 0.05mm. This convention
does not round compiler intermediates or change kernel numerical tolerances.

The Board template publishes its top-surface marker frame:

```firmament
Expose {
 Semantic MarkerOrigin {
  DatumFrame Frame = [0mm,658mm,H] x [1,0,0] y [0,1,0] z [0,0,1];
 }
}
```

Inside GuitarNeck, consume stations using the existing Pattern and Placement:

```firmament
FrameTransform InlayDatum { From: GuitarNeck.Rosewood.MarkerOrigin.Frame; }

Pattern PearlInlays Over FretboardLayout.InlayPositions {
 point => <Part Inlay = Panel<L:25mm,W:12mm,H:0.3mm,R:1mm>>
  Placement {
   From: BottomSeat.Frame; To: InlayDatum.Frame;
   TranslateLocal: [point.Position.X,point.Position.Y,0mm];
  }
 </Part>
}
```

Collinearity follows from the recipe, and seating/orientation follows from the
board-owned frame. Moving or rotating that board carries its inlays. This is
datum seating; footprint containment and recessed pearl pockets are separate
geometry concerns. The current inlays remain decorative panels on the board.
Inspection preserves the authored Stations recipe and keyed point coordinates;
ordinary pattern inspection supplies occurrence associations and provenance.

## Equation-driven frets

```firmament
Function FretDistance(n: Int, scale: Length) -> Length =
 scale * (1.0 - Pow(2.0, -n / 12.0));
Function FretWidth(distance: Length) -> Length =
 42mm + 13mm * ((distance + 2mm) / 540mm);

Concept Struct FretboardLayout {
 Points<Point2> Positions {
  Series {
   Index: n;
   First: 1;
   Count: 22;
   KeyPrefix: Fret;
   Position: Point2(0mm, -FretDistance(n,628mm));
  }
 }
}
```

`First` and `Count` are literal integer bounds. This produces Fret1 through
Fret22, in ascending index order; Fret12 is exactly `[0mm,-314mm]`.
The authored recipe and each point's key, ordinal, dimensionality, coordinates
and stable identity remain in inspection evidence.

The guitar consumes the series with the existing assembly Pattern syntax:

```firmament
Pattern Frets Over FretboardLayout.Positions {
 point => <Part Fret = Panel<L:FretWidth(-point.Position.Y),W:1.2mm,H:1mm,R:0.5mm>>
  Placement {
   From: BottomSeat.Frame;
   To: FretboardDatum.Frame;
   TranslateLocal: [point.Position.X, point.Position.Y, 0mm];
  }
 </Part>
}
```

In `GuitarNeck`, the placement frame is explicitly derived from the nut:

```firmament
FrameTransform FretboardDatum { From: GuitarNeck.Nut.BottomSeat.Frame; }
```

Occurrences have paths such as `GuitarX0.Neck.Frets.Fret12.Fret`. Changing
scale length moves the keyed occurrences without changing their occurrence
identities. Definition identities still reflect their actual geometry arguments.
Patterns preserve the logical collection name in expanded associations.

## Compilation contract

- Functions have unique names in the compiled source catalog, at most 16
  parameters each, and `Int`, `Float`, `Length` or `Angle` arguments/results.
  Forward calls are permitted; recursive dependency cycles are rejected.
- Expressions admit literals, parameters, parentheses, unary signs, arithmetic,
  other declared Functions, and dimensionless `Pow(base,exponent)`.
  Division is real division, including when both operands are integral.
- Lengths use `mm`, angles use `deg`. Addition/subtraction require matching
  dimensions. Multiplication admits at most one dimensional operand. Dividing
  matching dimensions produces a dimensionless result. `Pow` requires both
  arguments to be dimensionless. Int arguments/results must be integral and
  exactly representable within the checked range ±(2^53 − 1).
- Each collection has 1–1024 points; total expansion is limited to 4096 points.
  Linear keys are unique identifiers, and the step must be nonzero. Series
  indices are nonnegative with checked integer bounds.
- Function catalogs have at most 128 declarations. Evaluation limits are
  32 active Function calls, 10000 calls per scalar evaluation, 8192 characters
  per scalar expression and 128 nested scalar atoms. Non-finite results fail.
- There are no loops, mutation, recursion, I/O, reflection, host C# callbacks
  or arbitrary source evaluation. New errors use `firmament-concept-points-*`;
  assembly parsing wraps them in `assembly-pattern-invalid` diagnostics.

The shared scalar evaluator owns arithmetic and units. Checked Concept point
recipes project to the existing finite keyed Set frontend; the existing Pattern
expander remains the occurrence expansion authority. Concept collections retain
typed `ConceptIrKeyedPointSetValue` inspection data, are erased before geometry
construction, and contribute no bodies to STEP or display meshes. Point2 is
never silently promoted to Point3.

## Guitar dogfood and evidence

The directly authored `fixtures/Canonical/AssemblyInterfaces/GuitarX0/neck-assembly.firmament`
replaces 22 explicit fret tags and world positions with the declarations above.
The Python witness generator now preserves that module rather than overwriting
its fret authoring. The inlays now use the explicit station recipe and board-owned
marker datum above. Nut crossings feed the implemented [point wire routes](wire-point-routes.md).

CLI inspection, AP242 export and USD export succeed with 91 visible parts and
53 shared geometry definitions. The original fret-series milestone added 23 assembly nodes
(130 → 153 total occurrences); it adds no visible geometry. Comparison against
the previous witness preserves all mesh index arrays. Only fret coordinates
and widths change, by less than 0.000001mm, because the compiler evaluates the
formula at full precision rather than reading six-decimal Python output. These
counts/comparisons describe that earlier milestone. The inlay pattern additionally
adds 10 containment nodes (163 total display occurrences), retains nine visible
inlays and their shared geometry, and moves them only by the authored 0.1mm rounding.

Generated evidence lives under ignored `artifacts/local/concept-points/`:
`inspect.json`, `comparison.json`, `guitar.step`, `guitar.usda`, `display.json`,
focused test TRX/logs and full-suite logs. The reusable qualification fixture is
`fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/concept-points.firmament`.

Equation-driven profile curves, point-driven WireForm routing, scalar record
parameters, closures and a general collection programming language are deferred.

Final qualification: Release solution build passed; 32 focused authoring/guitar
tests passed; the fast kernel lane passed 1004 tests; the serial full solution
gate passed 4160 tests across 20 projects with tests, with 7 existing skips.
The Python generator was syntax-checked without regenerating the authored
witness. The final CLI exports and comparison use the same qualified build.
