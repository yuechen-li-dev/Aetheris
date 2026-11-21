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

## Spline-guided front wheel arches

An original sports-coupe front clip uses a longitudinal G1 SectionChain,
with the lower hood between raised wings as a styling reference from
[Porsche's 911 design notes](https://newsroom.porsche.com/en/products/porsche-911-design-production-anniversary-13742.html).
One XZ cubic profile sets both arches. The bounded Cutout remaps existing
lower side and sole rows into an underside-open well, keeps the quad
connectivity, and publishes two named lip regions. Its companion exact
tire envelope is a 664 mm diameter, 245 mm wide cylinder.

- The encoded shell is watertight with 34,496 triangles. All 112 sampled
  nose-rim positions retained their input coordinates. The absolute
  nose-rim fit stayed at 8.393 mm against a 12 mm budget.
- The nominal tire center was inside the seed. The exported arch shell
  clears the tire with a 32.290 mm lower bound over the continuous
  ±32° steering and 0–65 mm upward bump domain. The lowest of 27 pose
  bounds was 61.856 mm at -32° and 65 mm. The maximum distance
  from the nearest tested pose contributes a 29.566 mm allowance.
  Repeating the sweep with the placed cylinder on the opposite side
  produced the same 32.290 mm lower bound.
- The 3 mm Fair budget on the two lip regions gave 1.900 mm peak sampled
  displacement. The RMS turn between coarse normals dropped from
  20.58° to 18.86°. The CLI regression confirms that raising the guide
  rebuilds both arches, and checks sidecar replay and the wheel sweep.
- The Release build, 33 focused CLI tests and 1,259 fast kernel tests
  passed. The full parallel suite reported eight display and assembly
  test failures. All eight passed in isolated reruns covering 22 cases.

The native GLBs, STEP cylinder, compare SVG and JSON, and the shaded
render of the exported geometry are under `artifacts/local/subd-car/front-clip/`.

## Full sports coupe silhouette

The [sports coupe sources](../../../../fixtures/Canonical/Sculpting/sports-coupe/body.firmament)
produce an original 911-inspired exterior. The full lower body carries the
low hood, raised front wings and broader rear haunches. The separate cabin
SectionChain carries the raked windscreen, rounded roof and sloping rear
glass. The packed GLB contains the two SubD shells, four instanced revolved
wheel assemblies, and lamps and trim made from the shared SubD blank.
The fixture's NumPy packer places instances, assigns materials, and
deduplicates matching render vertices. It does not generate the surface
geometry. Complete reproduction commands are in the public sculpting guide.

- The shell and cabin validated and exported as watertight meshes. The
  file uses metres, +Y up and -Z forward. Four independently located
  wheel nodes expose a local +X axle. The 2,470 mm wheelbase and the
  tire dimensions are styling choices for this example.
- The packed asset has 99,248 rendered triangles with instances counted.
  It is about 1.25 MB. Khronos glTF Validator reported zero errors and
  warnings, and a repeated package produced identical bytes.
- The exact cylindrical bounds for the four dressed tires all cleared the
  exported shell at nominal ride height. The front and rear lower bounds
  were 77.86 mm and 79.09 mm respectively. These clearances use this
  silhouette's nominal wheel poses.
- Separate XZ arch sketches initially reused unqualified guide names.
  Inspect showed the front operation resolving the rear guide coordinates.
  Unique front and rear guide names in the fixture resolved the issue.
  Compare reports from the front and rear wheel placements confirm the
  independent openings in the packaged asset.
- The Release build and fast kernel lane passed. The full parallel run
  reported eight existing display and USD test failures. All eight passed
  in isolated reruns covering 23 cases.

The whole-scene GLB, replayable native parts, exact tire and rim STEP,
preview and validation evidence are under
`artifacts/local/subd-car/sports-coupe/`.

## Arcade proportions and native SubD surface mounts

The next styling pass shortens and rounds both ends, pulls the sills in,
and gives the rear haunches and larger wheels more visual weight. The
round lens treatment draws from [Porsche's 964 reference photos](https://newsroom.porsche.com/en/history/porsche-history-white-giants-991-turbo-964-turbo-3-6-993-turbo-s-13863.html).
The original dimensions are a styling starting point for the new arcade
proportions.

- The previous packer ray-cast GLB triangles to find the headlight
  attitude. `SurfaceMount` now takes a named interior section, span,
  and dimensionless parameter. It emits a world-frame position and
  right-handed orientation from the evaluated limit samples. It follows
  later cage operations and persists by source incidence in replay.
  The coupe uses this path for the paired headlights and mirror mounts.
- Nominal lower clearance bounds are 41.547 mm at both front wheels
  and 46.17 mm at the rears. Previous bounds were 77.86 and 79.09 mm.
  The nominal cylinders enclose the dressed tires. The export stays a
  single-resolution exterior with 111,248 triangles counting instances.
  Khronos glTF Validator reports zero errors and warnings. Packaging is
  byte-stable across repeated runs.
- The native mixed-assembly follow-up has a reduced witness under
  `artifacts/local/subd-car/sports-coupe-arcade/mixed-probe.firmasm`.
  A typed Firmament Part referencing the sculpting body beside an exact
  STEP wheel rejects during definition materialization with
  `firmament-symmetry-type-unsupported:Cutout`. The mechanical assembly
  lowerer is receiving the MeshState program. A native visual scene needs
  explicit MeshState artifact selection, access to its SurfaceMount frames,
  and named-region appearance bindings. The scoped packer consumes the
  native outputs and no longer reconstructs the mount frames.
- The car also exposes the need for a spline on the SubD surface with an
  authored width or gap. That construction would make door and hood
  shutlines editable without identifying individual cage edges.
- Release build, 34 focused CLI tests, and 1,259 fast kernel tests passed.
  The full parallel suite reported 14 display, STEP, and assembly failures.
  All 14 passed on isolated reruns covering 27 cases.

The revised GLB, reference comparison, sidecars, mixed-assembly probe,
pose reports, and test evidence are under
`artifacts/local/subd-car/sports-coupe-arcade/`.
