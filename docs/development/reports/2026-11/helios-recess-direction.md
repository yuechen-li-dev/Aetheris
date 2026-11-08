# Recess direction, defaults and refusal messages

The reported counterbore used a 3 mm depth on the bracket's 3 mm wall. It was
correctly refused because no smaller through shaft would remain. The placement
guide nevertheless pointed along the outward support normal, making the result
look reversed. Planning receipts were also incorrectly presented as errors
alongside the actual depth refusal.

The native cavity construction is unchanged. New regression checks verify that
the larger opening lies on the selected entry face and the smaller shaft opens
on the opposite exit, with the shoulder/shaft transition inside the material.
Both recess types are checked on the foot, upright and opposite upright face.
The bodies remain closed, correctly oriented and have the expected volume after
STEP reimport.

The gold direction arrow now points inward and has an arrowhead. The form says
that the recess opens on the selected face. Palette defaults are a 4/7 mm
counterbore at 1 mm depth, and a 6/8 mm 90° countersink (1 mm derived depth), so
both fit the 3 mm bracket without correcting the defaults manually.

A face-hole refusal returns only its actual failure, rather than promoting
successful planner receipts to errors. Depth errors preserve their stable code
and source location but present the requested depth, available wall thickness,
allowed range and parameter to change. The 3 mm case reports a 2.99 mm maximum.
The 0.01 mm remaining-shaft and clearance guards are preserved.

Forty focused native/palette cases pass, including six direction builds across
both signs of the face normal, stock-depth refusals and unchanged multi-hole
guards. All 251 Helios unit tests pass. The screenshot workflow passes in both
WebGPU and WebGL: use untouched defaults on the upright, refuse a 3 mm depth
with one actionable message, recover to 1 mm, place a default countersink and
export source/STEP for native CLI Built validation.

Full solution and production qualification logs are retained as
`artifacts/local/recess-direction-*`. Owning repository changes remain local;
the deployed Site includes the complete corresponding patches.

The Release solution build passes with zero warnings and errors. All 3,508
Firmament tests pass in the full solution run. Five previously recorded failures
remain outside this change: two Core NIST audit snapshots, the Geometry rotated
occurrence measurement and Server revision measurement timing checks, and the
EditableV8 dimensional-fit geometry snapshot. The last snapshot was reproduced
on the unchanged upstream baseline during the preceding qualification; no
snapshot or tolerance was changed. The full solution gate is therefore not green.

The production AOT SDK and Helios frontend builds pass. The deployment uses
published Helios revision `c705951db04cfb9fe726c04452d92e2181c6ce79` and Aetheris
revision `007bbb4644ca8fba59bc66d5ab5a2a45a66ffb9e`, plus this change's complete
working-tree patches. The decoded native WASM is 60,351,643 bytes with SHA-256
`a70822bff349c28119cceb88bfc63f40774fb7b393fba496839dc5436324b705`.

All 16 production graph workflows pass against the optimized bundle, with the
production AOT runtime required. This includes the motivating default-depth,
inward-direction, refusal/recovery and same-face recess workflow, as well as
existing hole clearance, drag, undo, export and assembly graph checks.
