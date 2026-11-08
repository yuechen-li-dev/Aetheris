# Bracket edge finish robustness

## Changes

Longitudinal fillets/chamfers now combine on different original polygon corners
and with separated source-side mounting shafts, counterbores and countersinks
on both bracket legs. The same exact profile plan owns the finished host and is
adapted to the existing planar side-hole planner. General Compose histories
are not flattened or approximated.

Original construction-face origins travel with topology correspondence. A corner
trim changes the remaining support boundary but does not move existing hole
coordinates. The adapter carries the Base axial interval explicitly, avoiding
the profile emitter's centered compatibility default. Hole metadata uses one
tree reporting path, with separate engineering metadata for each finish.

The WebGL fallback now ranks visible edge candidates by screen distance within
an eight-pixel picking radius instead of taking the first depth-sorted line in
a broad world-space tolerance. Picking updates camera/occurrence matrices before
the next render. Placement gestures retain their pointer-down support instead
of consulting a possibly stale previous-face guide at pointer-up. A regression
test holds the old guide throughout a new-face gesture and verifies both points
remain on the newly picked face. Upright recess placement passed three repeated
WebGL runs during diagnosis.

The authoring panel also retains a first face/point pick until compiler-projected
diameter fields are ready. A click during asynchronous outline loading no longer
tries to fit a NaN diameter and loses the intended center. A second regression
test delays the outline response and proves the original picked point is placed
with the compiler's 8 mm recess envelope when it arrives.

Admission requires finish size >=0.01 mm, >=0.01 mm on adjacent legs and between
new curves and all other boundaries/finishes. Analytic line/arc and arc/arc
distance checks include crossings and tangencies. A repeated original corner
is refused. Side-hole admission checks the full recess/shaft envelope, remaining
planar entry/exit supports, other cavity envelopes and a conservative finite
corridor. Corridor circle bounds now honor their actual arc trims. Failed
previews preserve accepted geometry/source and keep Apply disabled.

## Evidence

- 67 focused native cases passed, including 23 new robustness cases. All six
  original bracket corners can be rounded independently. Mixed inside/outside
  fillets and chamfers work; adjacent finishes consuming the 3 mm leg fail.
- Shaft/recess clearances are checked at 0.01 mm, tangency and overlap. Existing
  mounting cylinder origins/axes remain unchanged after a bend. Both native
  bodies and STEP reimports pass closed/oriented checks. The mixed-recess fixture
  also matches an independently calculated analytic volume.
- All 3,554 Firmament tests passed after fixing the integration regressions.
- The real UI sequence passed in development: add a mounting hole, refuse a
  colliding bend without source changes, apply a 2 mm inside fillet and a 0.5 mm
  outside chamfer, add another hole in the preserved face frame, export STEP,
  and undo/redo. The final production workflow also exercises an adjacent
  outside fillet and a shared-edge collapse refusal.
- Real CLI validate/build/analyze passed for
  `fixtures/Regression/OrientedComposeHoles/bracket-finished-mounts.firmament`:
  one enclosed-manifold body/shell, 16 faces, 45 edges, 32 vertices, unchanged
  [0,-20,0] to [30,0,40] bounds. The fixture combines counterbore and countersink
  mounting holes on both legs with three mixed corner finishes.

The full solution gate was executed. Its two bracket integration regressions
were corrected and the full Firmament project rerun passed. Geometry occurrence
and CLI slider-export failures passed on focused rerun. Three persistent failures
also reproduce in an isolated unmodified worktree at published Aetheris
575e7ec22ff716ebaf503e16f6b53589b9a5f29f: NIST FTC08/STC06 audit SHA snapshots and
EditableV8 `PhysicalEngineUsesInterfacePlacementAndDimensionalFits` (IntakeRetainer1
display position reference). No fixes or snapshot rewrites were made outside
this bracket task. The full solution is not claimed green.

Production AOT built successfully: decoded native WASM is 60,464,558 bytes,
SHA-256 `321c1868f10cb12aceda40de9d334c59322ec938741e6b00062200d2949d151e`.
All 760 CLI tests, 253 Helios unit tests and six SDK selection tests passed.
TypeScript, frontend production build and both owning repositories'
`git diff --check` passed.

All 25 production graph workflows passed their final checks. The full suite
passed 23; a new guard assertion expected a different diagnostic phrase, while
the product correctly refused the collapsing radius. After accepting the
actual radius-too-large diagnostic, that complete workflow passed. The added
counterbore/countersink-after-finishing workflow also passed. The final mixed
workflow covers three finishes, two shafts, refusal recovery, exact coordinate
tuning, STEP analysis, undo and redo. All six focused final production WebGL
workflows passed together after the picking/placement fixes. Hosted evidence
is recorded with the final deployment.

## Limits

One polygonal profile extrusion, multiple original straight-sided corners and
separated Side through-hole variants. No original curved profile corners or
inner loops, general Compose Base/Add/Remove histories, imported-body finishing,
mixed cap/longitudinal finishes or full-edge cap fillets. Cap finishes are not
advertised on the combined side-hole host. Conservative corridor proof may
refuse interactions needing a general surface intersection planner. This is
not a general-purpose rolling-ball fillet engine or manufacturing certification.

Owning repositories retain the accumulated uncommitted changes; deployment
publishes complete source patches against the existing published baselines.
