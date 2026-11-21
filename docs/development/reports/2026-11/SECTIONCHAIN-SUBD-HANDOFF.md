# SectionChain to SubD handoff

The compact mouse fixture uses an unbranded exterior based on the supplied M170
reference views. Logitech lists the external envelope as 97.3 × 61.9 × 35.2 mm.
The authored profiles estimate the silhouette and parting station from the views.
The fixture covers the exterior shape and leaves wheel, button and internal
geometry for a separate pass.
[Logitech technical specifications](https://support.logi.com/hc/en-gb/articles/360023301394-M170-M171-B170-Technical-Specifications)

## Construction

- The normal SectionChain materializer builds the exact body and provides face
  correspondence to the cage converter. The converter samples exact supports
  and fits their corresponding Catmull–Clark limit samples. The rim is a sharp
  loop with a separate sampled error budget.
- The cage owns authoring handles. Station and transition regions expand to
  concrete cage vertex IDs in replayable operations. GLB and STL sample the
  limit surface independently of the authored cage density.
- The CLI selects the exact state or an edited sibling with `build --state`.
  `inspect --json` reports source identities, cage geometry and fit evidence.

## Qualification

Run the checkout's Release CLI from the repository root.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Sculpting/compact-mouse.firmament --state Exact --out artifacts/local/subd-mouse/Exact.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll analyze artifacts/local/subd-mouse/Exact.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Sculpting/compact-mouse.firmament --state PalmComfort --out artifacts/local/subd-mouse/PalmComfort.glb --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Sculpting/compact-mouse.firmament --state LowFront --out artifacts/local/subd-mouse/LowFront.glb --json
```

- STEP reimport qualifies one enclosed shell with 22 faces and 88 pcurves.
- Across 2,592 corresponding samples, fitting reduced the worst transition
  deviation from 1.028 mm to 0.448 mm. The maximum difference at the 256 rim
  samples was 0.1235 mm in both branches, under the authored 0.15 mm budget.
- Each GLB contains 45,056 triangles. CLI qualification checks watertightness,
  replays both sidecars, verifies distinct upper geometry and compares every
  sampled position at and below the parting plane exactly.
- The raised-palm GLB moves the evaluated surface by up to 1.212 mm compared
  with the seed. The lowered-nose GLB moves it down by up to 0.400 mm. GLB
  positions at and below the parting plane match the seed in both branches.
- The invalid rim budget and removal of its crease fail before file creation.
- Matched oblique and side previews show the two edits and a small residual
  crown plateau from the planar cap.

## Result

The Release solution build and all 23 focused sculpting CLI tests passed. The
full parallel suite reported seven display-related failures in Kernel Core and
CLI. All seven passed in a 20-case targeted rerun with one test process per
project. The same 20 cases also passed on the unchanged `397d7bee` baseline.
The full parallel gate remains red on this host.

The transition and rim figures are sampled positional evidence. General
retopology, global mesh self-intersection proof, and STEP export of edited SubD
remain outside this lane.

## Polynomial crown refinement

The original cap met the loft with a sampled maximum normal mismatch of
22.99 degrees. The fixture now uses a smaller 4 mm × 4 mm terminal outline and
`endContinuity: G2`. The final loft transition is quartic longitudinally. Its
last three Bézier control rows lie in the cap plane, and its start derivative
continues the preceding transition.

The Release CLI reports G2 at all four crown edges with 33 samples per edge.
Comparing the exported STEP control nets gave a maximum normal mismatch of
zero degrees at those samples. STEP reimport qualified the 22-face shell and
export reported 20 non-rational B-spline surfaces and zero rational surfaces.
The two SubD variants stayed watertight with the same 0.1235 mm sampled rim
deviation. Matched renders from both exports show the former circular highlight
removed. The small planar terminal face remains in the exact topology.

Crown follow-up artifacts are under `artifacts/local/subd-mouse/crown-g2/`.
The Release build, 26 focused CLI tests and 1,259 fast kernel tests passed.
The full parallel gate failed on ten Kernel Core display cases and one CLI
assembly display case. All eleven passed in an isolated rerun of 25 cases.

## Named displacement brush

The mouse's default output now comes from
`Feature NoseDip = LowerNose(anchor: Seed.Nose, drop: 0.6mm)`. The normal
Feature expander specializes the brush recipe. The seed publishes `Nose`
at the start of section `Shoulder` span `FrontRight`. The brush evaluates a
smooth 18 mm footprint using edge distances between limit samples. The
qualified rim and preserved regions block propagation. The mesh recipe
replays the semantic anchor and brush operation.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll inspect fixtures/Canonical/Sculpting/compact-mouse.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Sculpting/compact-mouse.firmament --state Seed --out artifacts/local/subd-mouse/brush/Seed.glb --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll mesh fixtures/Canonical/Sculpting/compact-mouse.firmament --format glb --output artifacts/local/subd-mouse/brush/LowFront.glb --json
```

- The nose anchor moved down 0.530778 mm. The maximum across 22,530 matched
  limit samples was 0.531223 mm. The brush updated 56 cage controls.
- The 128 exported rim samples moved 0 mm. The separate fit to the exact
  rim stayed at 0.123499 mm across 256 samples against a 0.15 mm budget.
- The two GLBs have identical topology and identical encoded positions at
  and below the rim. The brush GLB is watertight.
- The Release build, 30 focused CLI tests and 1,259 fast kernel tests passed.
  The targeted checks cover named Feature expansion, anchor/region rejection,
  dimensional arguments, sample movement and sidecar replay.
- The full parallel lane reported seven display-related failures. All seven
  passed in a 20-case isolated rerun.

The footprint uses a cage-edge approximation to surface distance. The maximum
displacement is reported at the export sampling density.

## Wheel recess, constrained fairing and headless comparison

`WheelOpening` uses `Recess WheelLip` to replace 20 upper cage quads
with a blind pocket. Its single boundary contains 24 edges. `WheelFaired`
uses `Fair SoftenLip` with that named region, a 0.3 mm movement budget,
and three iterations. The compact mouse retains `LowFront` as its default
output. Select a wheel state explicitly on export.

- The peak displacement over 23,682 corresponding limit samples was
  0.193258 mm, with 0.021113 mm RMS. The 128 matched rim samples stayed
  fixed. The absolute sampled rim fit stayed at 0.123499 mm against
  its 0.15 mm allowance.
- Fairing moved 48 controls. RMS normal turning across neighboring
  coarse quads formed from limit positions went from 40.778° to 40.195°.
- The separate STEP wheel is a placed 15 mm diameter, 7.5 mm wide
  exact cylinder. For both shell states, the exact wheel and all encoded
  preview triangles had a separation of 1.380531–1.405531 mm. This
  clears the 1 mm qualification target. A 2 mm target was rejected.
- The shell GLB is watertight. Clearance is for that triangulation,
  with a blind pocket in the closed exterior blockout. The comparison
  preview includes matched oblique views, the displacement heatmap,
  and the Y = 0 mm section.
- The Release build, 31 focused CLI tests and 1,259 fast kernel tests passed.
  The full parallel suite reported four failures in existing display and
  STEP tests. All four passed in an isolated rerun covering 17 cases.

The reproducible commands are in the public sculpting guide. The build
JSON, replay sidecars, comparison SVG and JSON, and assembled preview are
under `artifacts/local/subd-mouse/wheel/`.

## Mirrored grip stroke

The mouse now has a `SideGrips` state after `WheelFaired`. `RightGrip`
routes across nine cage controls between two shoulder and palm anchors.
`Mirrored Stroke LeftGrip from RightGrip` uses the same plane resolution
as the existing Firmament symmetry authoring. The cage must have
reciprocal reflected vertices, quads and creases. Both passes use one
predecessor, preserve the rim and compose into one replay operation.
The mouse's default output still selects `LowFront`.

- The 13.5 mm radius stroke moved 86 cage controls. Peak movement over
  23,682 matched limit samples was 1.413195 mm, with 0.201376 mm RMS.
  The worst reflected position error was 2.85 × 10⁻¹⁴ mm.
- None of the 128 matched rim samples moved. Exported GLB positions
  at and below the rim match the predecessor. The shell stayed
  watertight, and exact-wheel clearance against each exported mesh
  stayed within the same 1.380531–1.405531 mm interval.
- The functional `Feature` form gives identical displacement when it
  supplies the source `Stroke`. The CLI regression replays both grips
  from the GLB sidecar, rejects
  an unmatched plane, and checks that the centerline overlap of a
  0.2 mm stroke stays within its single-pass displacement.
- The Release build, 32 focused CLI tests and 1,259 fast kernel tests
  passed. The full parallel suite reported 12 display and STEP test
  failures. All 12 passed in an isolated rerun covering 28 cases.

The build JSON, GLBs and replay sidecars, comparison JSON and SVG, and
the rendered top-view heatmap are under `artifacts/local/subd-mouse/stroke/`.
