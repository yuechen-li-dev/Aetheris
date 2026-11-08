# Helios face-based mounting-hole placement

The Simple Bracket foot now accepts a 6 mm Shaft through its 3 mm thickness,
normal to the selected broad face. The original XZ Profile and extrusion remain
authored source. The fixture is
`fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament`.

Extrusion construction correspondence publishes a source-owned Side selector.
The plain extrusion and its later composition use the same outward-normal,
projected world-axis frame and lower local bounds origin. This is independent
of each emitter's surface UV axes. The browser receives that exact frame and
convex boundary, rather than guessing a support from tessellation or face IDs.

The feature planner proves positive circular clearance at the selected entry
and a parallel exit. A conservative finite cylinder bound rejects other face
interactions. It replaces the two host faces with trimmed faces and directly
wires one cylindrical wall into their existing shell. No internal caps or
Boolean body replacement are introduced. Construction provenance remains in
the plan and correspondence.

Helios supports face-first Add hole and tool-first click-to-place, gold ring
dragging, exact Center fields, automatic preview, cancellation and one source
transaction on Apply. Dragging projects the full disk inside the convex support
with a 0.01 mm margin. Typed out-of-bounds centers and nonpositive diameters
disable Apply. New input invalidates an admitted candidate immediately; stale
asynchronous results are disposed. Camera gestures remain available. Shift or
right drag controls the camera while the tool is active.

Three native cases qualify exact world placement, closed/oriented volume
`4020 - 27*pi` mm³, STEP reimport, and tangent/out-of-bounds refusals. The
complete Firmament suite passes 3,484 tests with DOTNET_PROCESSOR_COUNT=2.
All 247 Helios unit tests pass. The UI regression passes through real WebGPU
and WebGL: face selection, placement, constrained drag, exact edits, Cancel,
source download, STEP export, native CLI validation and Undo.
All 14 graph authoring workflows also pass against the production AOT frontend;
the runtime request assertion verifies use of the native AOT WASM resource.
The frontend and final solution build succeed. The production native WASM is
60,309,213 bytes, SHA-256
`463606e40ee623c798c5bf7dc07384f1bba9f6e7e688716a4def814e38d72cf2`.

The first full parallel solution attempt reports 14 failures: ten Core, two
Firmament, one CLI and one EditableV8. They include execution-budget failures
and the previously observed two NIST audit hash mismatches. The complete
affected suite passes in the bounded run above. Full-suite logs and any later
qualification results are under `artifacts/local/face-placement-*`.
A final bounded parallel attempt passes all 760 CLI tests but still reports
two Core NIST audit mismatches, an EditableV8 timing failure and three
measurement-test failures in Geometry/Server. All three measurement cases pass
in a subsequent focused run without code changes. This is not a claim that
the full solution gate passes.

Scope is a simple Shaft with ThroughAll on a uniquely resolved convex planar
extrusion side with straight outer boundaries and a clear finite corridor.
Other trims, an existing inner loop, ambiguous split supports, rotated assembly
occurrences, blind holes, counterbores and countersinks are not qualified for
this placement path. Other feature tools retain their existing interactions.
The conservative corridor may refuse geometry that a future exact intersection
adapter could prove safe. Refusal preserves source and the accepted model.
