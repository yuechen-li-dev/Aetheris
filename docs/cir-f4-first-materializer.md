# CIR-F4 first bounded CIR→BRep materializer checkpoint

## Chosen pattern

CIR-F4 materializes exactly one pattern: `subtract(box, cylinder)` (box minus cylinder).

## Why this pattern

It reuses existing trusted BRep primitive constructors and bounded boolean subtract behavior, avoiding tangent/contact sensitivity.

## Boundaries

Supported:
- root `CirSubtractNode`
- lhs box, rhs cylinder
- optional pure-translation `CirTransformNode` wrappers

Unsupported:
- non-translation transforms
- non box/cylinder operands
- arbitrary CIR trees and all other boolean/tool families

## State transition behavior

`NativeGeometryRematerializer.TryRematerialize` accepts a `CirOnly` state plus lowering plan, lowers CIR, invokes the bounded materializer, and if successful transitions to:
- `ExecutionMode = BRepActive`
- `MaterializationAuthority = BRepAuthoritative`
- appended `CirOnly -> BRepActive` transition event

## Export behavior

`FirmamentStepExporter` still fails clearly for unsupported `CirOnly` states.
For this single supported pattern, exporter attempts internal rematerialization first; successful rematerialization exports through existing STEP242 path.

## Next F5 recommendation

Add one additional bounded family (for example `subtract(box, translated box)` notch) with the same strict recognizer-and-diagnostic shape, and keep rematerialization opt-in/internal.
