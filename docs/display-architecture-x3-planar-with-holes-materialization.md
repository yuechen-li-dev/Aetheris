# DISPLAY-ARCH-X3 — planar-with-holes materialization

## Purpose and scope

X3 makes planar multi-loop display materialization explicit and bounded. The change focuses on the display lane only: it classifies planar loops before triangulation, fails unsupported nesting deterministically, and keeps failures face-local so DisplayIR can remain partial.

## Relationship to A0/X1/X2

A0 identified ambiguous display authority. X1 introduced partial DisplayIR responses. X2 quarantined tessellation behind the explicit `BoundedMesh` lane. X3 keeps that authority split intact and narrows the previously brittle `PlanarTriangulationWithHoles` path.

## FTC-07 face 9 finding

FTC-07 imports and canonicalizes successfully; the failure was in display materialization. The bounded evidence identified face 9 on a plane during `PlanarTriangulationWithHoles`. X3 preflights planar loops through `PlanarLoopClassification` before entering the hole triangulator so the face either triangulates or reports a deterministic face-level diagnostic.

## Existing planar-with-holes problem

The old path flattened all loops, picked a primary loop separately, and passed every remaining loop to the hole triangulator. That made nested loops, degenerate loops, and invalid role selection hard to diagnose before bridge construction or ear clipping.

## Loop classification refactor

`PlanarDisplayLoopClassifier` now emits structured descriptors with loop id, source face id, 3D points, signed area, orientation, bounding box, role candidate, and diagnostics. Results include explicit roles (`Outer`, `Hole`, `Island`, `Degenerate`, `UnsupportedNested`, `Unknown`) and stable reasons such as `largest-absolute-area`, `containment-depth-odd`, `degenerate-area`, `duplicate-point-collapse`, and `unsupported-nesting`.

The revolved loop family count ladder was isolated behind `LoopPatternClassifier`, which uses named rules returning `LoopPatternClassification` with a kind, label, evidence, and diagnostics. This preserves existing user-facing labels while making the policy testable.

## JudgmentEngine decision

JudgmentEngine was not used. The loop role policy is deterministic containment-depth classification plus degenerate/self-intersection rejection, and the revolved pattern policy is an ordered compatibility rule set. A JudgmentEngine scorer would add ceremony without improving determinism or tie-breaking clarity for this milestone.

## New materialization behavior

Planar multi-loop faces now run `PlanarLoopClassification` after loop flattening. A single outer loop and depth-one holes continue to the bounded `PlanarTriangulationWithHoles` bridge/ear-clipping implementation. Degenerate, unknown, island, or unsupported nested loops produce an empty diagnostic patch for that face instead of escalating to a whole-body display failure.

## Diagnostics

New deterministic display diagnostics use stable codes:

- `Viewer.PlanarTriangulation.DegenerateLoop`
- `Viewer.PlanarTriangulation.UnsupportedNesting`
- `Viewer.PlanarTriangulation.InvalidLoop`

Existing timeout diagnostics remain available from the execution budget when other phases exceed budget.

## Tests run

- `dotnet restore Aetheris.slnx`
- `dotnet build Aetheris.slnx -f net10.0 --no-restore`
- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "PlanarWithHoles|PlanarTriangulation|LoopClassifier|LoopPattern|DisplayIR|BoundedMesh|DisplayPrepare|ViewMaterialization|FTC07|Ftc07|Tessellation" --logger "console;verbosity=minimal"`

## What did not change

X3 did not change STEP import/export semantics, AP242 importer/exporter behavior, BRep topology, DisplayIR authority, the explicit BoundedMesh tessellation lane, Firmament V2 language/lowering, AIR Region route policy, CIR authority, Firmasm, or CAD feature behavior.

## Next milestone recommendation

DISPLAY-ARCH-X4 should add a wireframe/edge-first fallback for planar materialization failures so diagnostic-only faces can still expose their loop geometry without pretending that a filled mesh succeeded.

## Follow-up: DISPLAY-ARCH-X4 wireframe fallback

DISPLAY-ARCH-X4 builds on X3 by allowing a face whose fill materialization still fails to degrade to `WireframeOnly` when BRep boundary loops/edges are available. This keeps X3 planar diagnostics intact while making partial display more visually useful. See `docs/display-architecture-x4-wireframe-fallback.md`.
