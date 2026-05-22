# CIR-F5 second bounded CIR→BRep materializer checkpoint

## Chosen pattern

CIR-F5 adds exactly one new pattern: `subtract(box,box)` (box minus box), while preserving CIR-F4 support for `subtract(box,cylinder)`.

## Supported shape

Supported:
- root `CirSubtractNode`
- lhs: translated/untranslated `CirBoxNode`
- rhs: translated/untranslated `CirBoxNode` or `CirCylinderNode`
- pure-translation `CirTransformNode` wrappers only

## Explicitly unsupported

Unsupported (clear diagnostic path):
- non-subtract roots
- lhs non-box
- rhs non-box/non-cylinder
- non-translation transforms
- nested/general CIR trees
- union/intersect materialization
- assemblies and generated topology naming

## Materialization strategy

No new geometry kernel logic was added.

`subtract(box,box)` rematerialization is implemented by reusing existing bounded BRep primitives and booleans:
- `BrepPrimitives.CreateBox(...)` for lhs
- `BrepPrimitives.CreateBox(...)` for rhs
- `BrepBoolean.Subtract(...)` for final exact BRep

`subtract(box,cylinder)` remains on the existing path:
- `BrepPrimitives.CreateBox(...)`
- `BrepPrimitives.CreateCylinder(...)`
- `BrepBoolean.Subtract(...)`

## Runtime rematerialization and export

`NativeGeometryRematerializer.TryRematerialize(...)` behavior is unchanged:
- accepts `CirOnly` state,
- lowers CIR from replay/lowering plan,
- invokes bounded `CirBrepMaterializer`,
- on success transitions to `BRepActive` + `BRepAuthoritative` with transition event.

`FirmamentStepExporter` behavior is unchanged in policy:
- for `CirOnly`, it attempts bounded rematerialization first,
- exports through existing STEP242 path on success,
- fails clearly when no bounded materializer match exists.

## F6 recommendation

Add one additional bounded subtract family with explicit recognizer naming and strict diagnostics (for example a constrained translated prism tool) while keeping the matcher list explicit and non-generic.
