# Multiple face holes and recess placement in Helios

The original Simple Bracket supports multiple separated Shaft, Counterbore and
Countersink features on its planar foot and upright. The fixture is
`fixtures/Regression/OrientedComposeHoles/bracket-multiple-face-holes.firmament`.
It contains three different holes on the same face; the browser regression
authors four holes through the actual graph controls and exports their source
and STEP.

The support frame is derived from the retained outer loop, so adding circular
inner loops does not shift its origin or axes. Existing openings are published
as excluded disks in that frame. The browser chooses the nearest feasible
center from exact 2D line/circle projections and boundary intersections. This
avoids local iterative projection failures in narrow feasible regions. A small
rounding allowance keeps dragged positions above the compiler clearance floor.
Numeric edits are not silently moved; invalid coordinates disable Apply.

The compiler requires 0.01 mm clearance at boundaries and existing openings.
Counterbores and countersinks reserve their outer diameters. Each completed
feature retains a construction-owned finite cylindrical envelope and its cavity
face IDs. Parallel envelopes use radial/axial separation; nonparallel envelopes
must have separated bounds. Crossing envelopes reject without a surface/surface
intersection. Diagonally separated circles pass even if their axis-aligned
bounds overlap. Unowned curved corridor boundaries use analytic circle bounds,
not just endpoint bounds; unsupported curve kinds reject.

The topology planner retains every previous inner loop and directly inserts
shaft/recess rings, cylindrical or conical walls, and an annular counterbore
shoulder in the same shell. It does not introduce internal caps or Boolean
operations. Source selectors, existing hole identities and construction
correspondence are retained. Hole reports now include this support path, so
these features also appear in the compiled model browser.

Twenty-one focused native cases pass: multiple variants on one face, both
bracket legs, recesses on the upright, exact volume and STEP reimport, fixed
support frames, safe diagonal placement, overlap/tangency/sliver refusals,
recess footprint collisions, invalid depths/angles and crossing drill axes.
The mixed three-hole fixture removes exactly `49.875*pi` mm³. The separate
crossing-axis fixture is intentionally rejected by the cavity envelope guard.

All 250 Helios unit tests pass. The real UI regression passes in both WebGPU and
WebGL: multiple holes, counterbore and countersink placement, drag collision
guards, exact-coordinate refusal, Cancel, source/STEP downloads, CLI Built
validation, Undo and Redo. Production AOT qualification is recorded under
`artifacts/local/multi-face-hole-*` alongside all raw logs.

The full solution build passed. Its parallel test attempt passed 3,500 affected
Firmament tests and all 760 CLI tests but retained five failures: two known NIST
audit snapshots in Core, plus one measurement case each in Geometry and Server,
and one EditableV8 geometry-reference mismatch. A later focused test build overlapped
the running test host and hit a Windows DLL lock; it was rerun after the test
host exited and passed. The final affected suite includes two additional native
cases beyond that initial full-solution attempt. No execution budgets or audit
baselines were changed.
The final complete affected suite passes all 3,502 tests. Geometry and Server
measurement cases also pass in a focused rerun without code changes.
The EditableV8 mismatch was independently reproduced on a clean, isolated
checkout of the original `664ebb6d3c9502f5eba6cad0122d4269f6ec29f9` baseline:
`IntakeRetainer1 positions[306]` expects approximately zero and gets
`6.549348885785214` in both trees. It predates all local changes; its reference
was not rewritten. The baseline log is `multi-face-hole-baseline-audit.log`.

All 15 production graph workflows pass against the final AOT frontend. The
native runtime is 60,350,599 bytes with SHA-256
`87882ce0d37b90f8a4c4cc949e72a7885f29061d81b619813c2bed6a9fda9997`.

Limits: convex straight outer boundaries, complete circular inner openings,
uniquely resolved source Side supports, parallel contained exit, and ThroughAll.
Recesses must have positive shoulders/cone depths and leave at least 0.01 mm of
shaft depth. Full-depth envelopes use each feature's largest radius, which is
deliberately conservative for opposite-side or crossing recesses. Noncircular
trims, coplanar planning seams, blind side holes, imported/unmapped supports and
rotated assembly occurrences remain outside this path. Rejected previews leave
source and the accepted geometry intact.
