# Garment authoring foundation

Temporary construction guides can now be bound to named panel boundaries with
an explicit release schedule. See [Concept fitting](garment-temporary-fitting.md)
for syntax, unsupported settling checks and the Anatolia experiment.
The [armhole follow-up](garment-armhole-experiment.md) composes these guides with
declarative pose motion and retains contact-isolation diagnostics.

`schema Garment` composes resolved 2D Profiles into material panels, distributed
stitches and an initial arrangement. Firmament owns the pattern and seam truth;
the shared `Aetheris.Cloth3D` runtime owns constraints and simulation. Blender is
an optional viewer of exported meshes, not a second fitting or simulation path.

## Source workflow

Start with `garmentcode-dress-template.firmament` for a known sewing-pattern
cut and native hem customization; see [GarmentCode import](garmentcode.md).
See [mesh quality and settling controls](garment-meshing-settling-experiment.md)
for rest-mesh diagnostics, solver budgets and failed-drape convergence evidence.
Use `skirt.firmament` as a simpler solver control. The same directory retains a
flared skirt, tunic, shorts and integrated-sleeve dress as separate experiments.
Reuse `Feature (...) -> Profile`, named Profile boundary spans, `Mirrored Profile`
and finite `Pattern ... over Set` instead of copying per-instance coordinates.

```firmament
schema Garment

Rect2 cut { center: [0mm, 225mm]; size: [612.61057mm, 450mm] }
Profile skirtCut { Loop Outer { cut |> TraceLoop } }

Garment BasicSkirt {
  meshSize: 65mm;
  Fabric cotton { thickness: 2mm; arealDensity: 0.2; }
  Panel front {
    profile: skirtCut;
    origin: [0mm, -35mm, 650mm];
    u: [1, 0, 0]; v: [0, 0, 1];
    wrapRadius: 195mm;
    grain: [0, 1]; pin: [Top];
  }
  Panel back {
    profile: skirtCut;
    origin: [0mm, -35mm, 650mm];
    u: [-1, 0, 0]; v: [0, 0, 1];
    wrapRadius: 195mm;
    grain: [0, 1]; pin: [Top];
  }
  Interface<Stitch> leftSide {
    a: front.edges.Left; b: back.edges.Right; orientation: Reversed;
  }
  Interface<Stitch> rightSide {
    a: front.edges.Right; b: back.edges.Left; orientation: Reversed;
  }
  Drape { figure: Anatolia; pose: Rest; clearance: 4mm; }
}
```

`Panel.profile` resolves through the existing Profile parser and validator.
`u`/`v` are orthonormal initial-placement axes. `grain` is a material-space unit
direction; its perpendicular supplies the second cloth axis. Rest lengths and
mass come from the flat pattern, never from the arranged or settled mesh.

`wrapRadius` provides a cylindrical initial arrangement. Optional
`wrapTopRadius`, `wrapTopOrigin` and `wrapTopAngle` interpolate to another ring
at the panel's top. These are placement controls, not a change to material rest
geometry. `pin` names boundary spans retained as explicit supports, such as a
skirt waistband. The tunic deliberately has no pins and rests on the shoulders.

`Interface<Stitch>` addresses directed spans as `panel.edges.name`, including
stable Pattern identities (`panels.front.edges.Left`). `Same` or `Reversed`
selects correspondence. The compiler merges normalized arc-length samples and
emits weighted constraints; opposing boundaries need not have the same vertex
count. Each edge admits one stitch interface. Length mismatch requires an
explicit fractional `ease` tolerance. This is distributed cloth sewing, not a
rigid Assembly mate. Open boundaries remain open.

## Pattern-first dress workflow

`tshirt-dress.firmament` and `sundress.firmament` are single-garment witnesses,
each with front/back panels. They use a Rect2 seed and immutable Profile Replace
edits rather than manually declaring every point, guide and Segment.
`Feature ... -> Profile` remains a pure, finite construction recipe. It does not
run the cloth solver. Front/back calls share the recipe and vary neckline depth.
`Interpolation: Linear` and optional `SpanNames` belong to the shared Profile
editor; garment-specific sewing consumes the resulting named boundaries.

An edit name also selects its ordered descendants as one garment boundary view.
For example, `front.edges.neckline` can select multiple cubics. Selecting both a
group and one of its children for sewing fails as overlapping seam ownership.
Consumed carrier names are not aliases for arbitrary replacement children.

Use `garment preview` before building a cloth mesh:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment preview fixtures/Canonical/Garment/sundress.firmament --body artifacts/local/humanoid-production/antonia.gameplay-body.json --json
```

This writes only `patterns.svg` and `evidence.json` under the garment's `preview`
directory. SVG uses resolved line/arc/cubic paths directly; changing meshSize
does not change the preview. It shows named boundaries, matched seam colors,
orientation, lengths and grain arrows. The evidence contains both seam lengths,
normalized mismatch and allowed ease. Unknown/reused edges and excess length
mismatch fail before meshing. Neither triangulation nor draping is run.

## Figure-bound Concept guides

```firmament
Concept Struct dressLayout {
  FigureFit body {
    figure: Anatolia;
    pose: Rest;
    chestHeight: 1280mm;
    hipHeight: 960mm;
  }
  Points<Point3> arrangement {
    front: Point3(0mm, dressLayout.body.centerY, 650mm);
    back: Point3(0mm, dressLayout.body.centerY, 650mm);
  }
}
```

The host supplies `GarmentFigureBinding`, or CLI supplies `--body` and optional
`--figure` (default `Anatolia`). FigureFit publishes checked Length values before
ordinary Features and Concept point recipes expand. Published members are
`shoulderWidth`, `shoulderHeight`, `neckHeight`, `centerX`, `centerY`,
`chestHeight`, `hipHeight`, `chestCircumference`, `hipCircumference`,
`torsoCircumference`, `frontY` and `backY`. The torso circumference is the greater
of the two measured sections. Front/back Y values bound those sections.

Chest/hip measurements are closed horizontal sections of the supplied OuterSkin
mesh, selected by containing the torso joint's XY probe. Endpoints weld at
0.0001mm; open, branched, ambiguous and vertex-coincident sections reject with
diagnostics. Explicit section heights select the design's measurement protocol;
omitting them uses Chest/Pelvis joint heights. Shoulder and neck guides are
joint proxies, not newly invented surface landmarks or standard anthropometry.
These declarations do not promote the gameplay candidate to canonical status.

Ease, hem height and cut dimensions remain authored design choices. The examples
derive panel width from circumference plus explicit ease and derive initial
wrap radius from that panel width. Rest lengths and mass still come from the flat
cut, not the wrapped arrangement. Length-valued placement fields admit the same
bounded scalar arithmetic used by Features.

Fit evidence retains body ID, topology, skeleton/rest-pose identity, section
measurements, exact rest-surface hash and CLI input-artifact hash. Draping a
compiled fit on a different rest surface rejects and requires recompilation.
The measurement pose is explicit and can differ from the Drape fitting pose.
Its name and resolved pose program accompany the measurements; the rest-surface
hash continues to identify the original body, not a newly rebound skeleton.
`preview`, `inspect`, `build` and `drape` accept the body binding. Generic
`validate`/editor analysis have no body context and report
`garment-fit-body-required` for these fixtures; they do not fabricate measurements.
The fixture qualification registry records this expected missing-input stop.

## Declarative figure poses and motion

`Pose` is a finite anatomical recipe lowered through the existing humanoid
kinematic solver and dual-quaternion skinning. `Rest` preserves the loaded bind
pose, `APose` requests the canonical 35-degree shoulder abduction, and `TPose`
requests 90 degrees. Neither preset changes the original bind skeleton.

```firmament
Pose fitting {
  figure: Anatolia;
  from: TPose;
}
Pose wearing {
  figure: Anatolia;
  from: APose;
  Joint LeftElbow { flexion: 5deg; }
}
PoseTransition lowerArms {
  from: fitting;
  to: wearing;
  duration: 2s;
  easing: SmoothStep;
}
// Inside the Garment declaration:
Drape {
  figure: Anatolia;
  pose: fitting;
  transition: lowerArms;
  clearance: 4mm;
}
```

Named poses can inherit another named pose of the same figure. Forward references
are allowed; cycles, duplicate declarations and inheritance beyond 32 levels
reject. Joint `flexion`, `abduction` and `twist` use absolute degree-valued
anatomical coordinates. Omitted coordinates inherit, including the measured
original bind pose when starting from `Rest`. The existing solver owns supported
joints and limits: shoulder/hip ball joints and elbow/knee hinges. This is not
yet arbitrary finger, spine or facial posing. Invalid requests reject rather
than silently clamping into a different pose.
The current center-based anatomical observer measures flexion and abduction;
it does not independently recover axial twist. Twist requests use the existing
solver, but its reported twist residual is not a separate orientation proof.

Transition endpoints must be named poses of the same figure; `Drape.pose` must
match the transition's `from`. Duration is an explicit seconds literal between
1/60 and 10 seconds. `SmoothStep` is the default; `Linear` is also available.
The drape path sews for 30 ticks, settles for `--ticks` in the fitting pose,
samples the authored transition at each cloth substep, then settles for another
`--ticks` in the wearing pose. Without a transition, a named pose remains static.

The moving body refits the existing triangle BVH with skinned positions and
unchanged topology. The shared CPU solver accepts `IClothKinematicBodySurface3D`
and an explicit `BodyTimeSeconds`; replay supplies that same time together with
the cloth snapshot. There is no implicit animation clock or second skeleton.

Inspect the body pose independently of garment meshing:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment pose fixtures/Canonical/Garment/sundress-posed.firmament --body artifacts/local/humanoid-production/antonia.gameplay-body.json --pose fitting --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment drape fixtures/Canonical/Garment/sundress-posed.firmament --body artifacts/local/humanoid-production/antonia.gameplay-body.json --ticks 30 --out-dir artifacts/local/garment/sundress-posed/transition --json
```

The pose command also accepts a source containing only Pose declarations. It
writes `body.obj` and surface/semantic evidence without meshing or simulating
cloth. Drape exports paired body/garment OBJ checkpoints for `fitting`, `midway`
and `wearing`, retaining anatomical requests, deformation screens, strain,
contact, seam gaps and a replay check taken during the transition. Exit code 2
retains diagnostic meshes when a screen or fit budget fails.

`scripts/render-garment.py --garment sundress-posed --frame wearing --input-dir
artifacts/local/garment/sundress-posed/transition` presents those actual exports
in Blender. It does not repair or simulate them.

## Casing

Use PascalCase for constructs, schema names and enum values; camelCase for
fields, instances and authored edges. Feature names describe reusable recipes
(`TunicPanel`); instances describe occurrences (`tunicCut`, `rightFront`).
Historical PascalCase field aliases remain supported. The source spelling tool
preserves authored Record/Set field names and references exactly. The generated
Rect2 boundary names `Top`, `Left`, `Bottom`, `Right` remain their existing
case-sensitive identities; user-defined spans can use camelCase.

## Real CLI and artifacts

From the repository root, use the repository CLI DLL:

```powershell
dotnet build Aetheris.CLI -c Release -m:1
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Canonical/Garment/skirt.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment build fixtures/Canonical/Garment/skirt.firmament
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment drape fixtures/Canonical/Garment/skirt.firmament --body artifacts/local/humanoid-production/antonia.gameplay-body.json --ticks 30 --json
```

The gameplay body is an existing local humanoid artifact, not checked into these
fixtures. `Anatolia` is the source's figure binding; the loaded artifact retains
its actual Antonia model ID and topology hash. Preserve the humanoid asset's
existing attribution and provenance. No additional model or garment downloads
are required.

Outputs under `artifacts/local/garment/<source-name>`:

- `garment.json`: versioned definition, rest/initial positions, material
  coordinates, pins, stitches, source hash, panel/span identities and optional
  settled snapshot. Runtime load recompiles and checks the retained content key.
- `garment.obj` / `garment.usda`: exact arranged or settled garment mesh in
  metres, Z up. OBJ texture coordinates are material-space metres; texture atlas
  packing and texturing are later work. USD is transport, not solver authority.
- `patterns.svg`: named material boundaries, stitches and supports in mm.
  These are cut boundaries; no seam allowances are silently added.
- `body.obj` on drape: the actual loaded body for inspection.
- `evidence.json`: source/content/body hashes, seam gap, sampled penetration,
  stretch, replay, elapsed time and largest contact/stretch outliers. A failed
  drape retains geometry and returns exit code 2; it does not report success.

Supplying a body to `build` also writes `body.obj` for inspection of the initial
arrangement. Pattern preview writes neither mesh nor body geometry.

The static drape baseline uses 30 sewing ticks with decreasing stitch compliance,
then the requested number of gravity ticks. Default settlement is 60 ticks at the
shared solver's fixed timestep. Qualification requires finite state, exact
capture/restore replay, seam gap <= 10mm, sampled penetration <= 5mm and stretch
<= 15%. These are explicit authoring checks, not textile calibration or a
collision-free certificate. Inspect front, side and back views too:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python scripts/render-garment.py -- --bottom skirt
```

For a single dress, pass the actual CLI output directory explicitly:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python scripts/render-garment.py -- --garment sundress --input-dir artifacts/local/garment/sundress/settled-60
```

The viewer renders front/side/back views and retains the imported OBJ hashes.
It does not modify the mesh, rerun simulation or repair a failed fit.

## Runtime consumption and ownership

```csharp
using Aetheris.Cloth3D;

var garment = ClothGarmentArtifact3D.Load("garment.json");
using var solver = garment.CreateSolver(); // Restores a settled snapshot when supplied.
var snapshot = solver.Capture();
// Use garment.Cloth.Definition.Indices and snapshot.Positions for presentation.
```

For Aurelian, `ClothAgentDefinition3D.FromGarment(garment)` creates the reusable
scene agent and maps the artifact's Z-up frame into Aurelian's Y-up world. Its
typed state retains the baked snapshot, velocities and support targets. Ordinary
scene placements can then translate or rotate it without redefining rest lengths.

`Aetheris.Cloth3D` contains the existing Aurelian cloth foundation, extracted
rather than duplicated. The Aurelian project forwards a dependency to it;
Aurelian retains the Vulkan backend and agent/presentation integration.
Firmament and the humanoid authoring pipeline have no dependency on Aurelian.
Shared nearest-triangle geometry lives in Aetheris.Kernel.Core.

The native Vulkan regression proof covers existing cloth behavior and new
weighted stitches. Static humanoid body contact currently uses the CPU BVH
backend. The Vulkan backend explicitly rejects that unsupported contact input,
rather than silently ignoring the body.

## Current bounds

One outer Profile loop per panel; lines, circular arcs and cubic Beziers;
15..150mm mesh spacing with bounded conforming refinement. Holes and ellipse
boundaries stop with diagnostics. One fabric and one explicit figure binding
per garment, with bounded anatomical poses and an optional authored transition.
No seam allowances, darts, zippers, graded sizes, textile material
calibration, general animation-clip-driven fitting, continuous collision detection, body
friction or garment self-contact are qualified in this foundation.

Body contact queries an oriented triangle surface and includes vertex, edge and
triangle-interior probes. It is not a watertight signed distance field. Sparse
probes cannot prove absence of every triangle intersection. Tight crotch and
armhole fits need especially careful inspection; failed fits stay failed.

## Local authoring witnesses

The supplied static Antonia gameplay candidate produced these retained results
with 30 sewing ticks followed by 60 gravity ticks for the skirts. Tunic and shorts
used 30 gravity ticks. Values are sample-based and specific to this body and cut.

| Source | Maximum stretch | Sampled penetration | Maximum seam gap | Qualification |
| --- | ---: | ---: | ---: | --- |
| `skirt.firmament` | 0.276% | 0mm | <0.001mm | Pass |
| `flared-skirt.firmament` | 4.66% | <0.001mm | 0.254mm | Pass |
| `tunic.firmament` | 21.3% | 4.41mm | 3.19mm | Fail: stretch |
| `shorts.firmament` | 45.7% | 5.36mm | 6.00mm | Fail: stretch and contact |

All four runs were finite and replayed exactly. The tighter fits are diagnostic
experiments, not accepted garment baselines. Use the flared skirt as the first
clothing starter and the straight skirt as a simple solver control. Preserve the
per-run evidence JSON and inspect the actual front/side/back meshes. Combining
the tunic and skirt in a viewer does not simulate garment-to-garment contact.

### Single-garment pattern workflow qualification

The FigureFit-backed sundress uses the actual supplied Rest surface, two panels,
four matched stitch interfaces and no pins. Its resolved SVG, compiled mesh and
static drape were exercised through the repository CLI. The final cut has a
210mm neckline width and front/back neckline depths of 80/40mm.

| Run | Gravity ticks after 30 sewing ticks | Maximum stretch | Sampled penetration | Seam gap | Qualification |
| --- | ---: | ---: | ---: | ---: | --- |
| Sundress, initial 180mm neckline | 30 | 23.31% | <0.001mm | 0.259mm | Fail: stretch |
| Sundress, retained 210mm neckline | 30 | 17.31% | <0.001mm | 0.366mm | Fail: stretch |
| Sundress, rejected 230mm neckline | 30 | 23.85% | <0.001mm | 0.194mm | Fail: stretch |
| Sundress, retained cut and standard settling | 60 | 18.00% | <0.001mm | 0.329mm | Fail: stretch |

All four runs were finite and replayed exactly. The longer retained-cut run took
249.95 seconds, including compilation, export and replay checks on this host;
this is an offline authoring measurement, not a runtime performance benchmark.
The 30- and 60-gravity-tick retained runs share source SHA256
`964d17d47ac3b2fca63eb2a0f99873aa1225d0ea0fc1f15a00a4f7e7d99b41b0`.

The largest remaining strain is a front-panel diagonal constraint at material
coordinates (-78.75, 856.25) to (-91.875, 868.9063) mm, near the neckline.
Doubling settlement did not resolve it. The next fitting problem is local
neckline strain and its interaction with body contact and discretization;
seam closure and sampled penetration already meet their budgets. No specific
solver cause is proven by these measurements. Do not relax the 15% strain
budget or mark this dress as an accepted fit.

The integrated-sleeve T-shirt dress also remains an arrangement experiment.
The first flat arrangement produced a 93.1mm seam gap and 432.7% peak stretch;
the cylindrical arrangement closed seams to 5.88mm but produced severe strain
and 38.2mm sampled penetration. Inspected meshes showed sleeves above the
A-pose arms and arms outside the intended side openings. Sleeve arrangement
and arm threading need their own qualification before this cut becomes a starter.

Retained local evidence includes `sundress-fitted-run.json`,
`sundress-final-run.json` (the rejected wider-neck experiment),
`sundress-settled-60-run.json`, the `sundress/preview` analytic SVG, and the
`sundress/settled-60` mesh/artifact exports under `artifacts/local/garment`.
`renders/sundress` contains front/side/back views of the standard-settling output.
These views present the retained solver mesh without mesh repair or another
simulation in Blender.

Both Release solution builds passed. The full Aetheris suite passed 4,447 tests;
the Aurelian consumer suite passed 1,217 tests with its installed Naga executable
selected through `AURELIAN_NAGA`. The initial Aurelian run's missing-tool failure
is retained separately from the successful configured run. These counts qualify
the earlier pattern-authoring milestone; they do not add native GPU body-contact
qualification or certify the newer moving-body experiment below.

### Declarative T-pose to A-pose experiment

`sundress-posed.firmament` retains exactly the baseline cut, material and Rest-pose
measurements. Its cloth content key matches the static baseline:
`0A7A1926E2113F0D3C22C412567C1241A24A54A18A746BAF9F8241D3A3F01E71`.
It adds T-pose fitting, a half-second SmoothStep transition, then A-pose settling.

| Checkpoint | Schedule after 30 sewing ticks | Shoulder abduction | Peak stretch | Seam gap | Qualification |
| --- | --- | ---: | ---: | ---: | --- |
| Static baseline | 60 gravity ticks in Rest/A-pose | 35deg | 18.00% | 0.329mm | Fail: stretch |
| Fitting | 30 gravity ticks in T-pose | 90deg | 18.71% | 0.243mm | Fail: stretch |
| Midway | 15 transition ticks | 62.5deg | 19.31% | 0.347mm | Fail: stretch |
| Wearing | 30 transition and 30 further gravity ticks | 35deg | 18.96% | 0.402mm | Fail: stretch |

All three checkpoints were finite, passed the existing body-deformation screen,
and had sampled penetration below 0.001mm. Cloth replay was exact both during
the transition and after settling. Maximum forward body travel was 4.09mm per
substep. Checkpoint sampling does not certify every intermediate collision or
deformation; this remains discrete, one-way CPU contact without CCD or friction.

The declarative posing and moving-body workflow now works through the real CLI,
original skeleton, skinning and cloth solver. It did **not** improve this cut's
remaining fit failure. The largest final strain is a front-panel diagonal from
(105, 880) to (91.875, 868.906) mm at the neckline. Front/side/back inspection also
shows that the dress remains tent-like rather than behaving like the reference
T-shirt dress. Posing alone is insufficient; local neckline contact,
discretization and drape/material behavior need further qualification before
sleeves or a finished garment can be claimed. No specific solver cause is proven.

Retained evidence is `posed-dress-drape-run.json`, paired checkpoint OBJ exports
in `sundress-posed/transition`, and all nine views in
`renders/sundress-posed/{fitting,midway,wearing}` under `artifacts/local/garment`.
Blender only presents the retained meshes; its render evidence hashes each input.
The run took 352.65 seconds on this host while regression tests also ran, so this
is not an isolated performance benchmark. A separate CLI measurement check also
resolved FigureFit in the T-pose body: chest circumference 892.95mm versus
871.32mm in Rest, while hip circumference stayed 1004.09mm. The comparison cut
continues to use Rest measurements to avoid changing its geometry.

Both Release solution builds passed. The full Aetheris suite passed 4,462 tests;
the Aurelian consumer suite passed 1,217 with its installed Naga selected through
`AURELIAN_NAGA`. These gates verify the shared capability and regressions. The
garment still fails its unchanged 15% strain budget and is retained as a
diagnostic specimen.

## Known sewing-pattern starters

See [GarmentCode import and templates](garmentcode.md) for the MIT-credited
shirt/dress reference, editable Firmament import, native hem customization and
separate pattern-versus-drape qualification.

## Fit, resolution and presentation experiments

See [the controlled fit/resolution round](garment-fit-resolution-experiment.md)
for named checkpoint strain diagnostics, opt-in presentation bindings and typed
regional compliance. The canonical cut and acceptance limits remain unchanged;
the complete garment dressing sequence is still unqualified.

## Body-referenced quad surfaces

See [the surface-first skirt experiment](garment-surface-experiment.md) for
typed body-contour authoring, explicit flat/fitted rest conventions and the
shared CPU/Vulkan quad material path. The stationary skirt is qualified
separately from the earlier panel dress; flattening and animated-body fit remain
future experiments.

See [the body-fitted spline tube-top experiment](garment-spline-experiment.md)
for retained editable bicubic supports, exact open-sheet BRep inspection,
control-cage and clearance feedback, quad lowering and actual Aurelian/Vulkan
artifact consumption.
