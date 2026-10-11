# Garment authoring foundation

`schema Garment` composes resolved 2D Profiles into material panels, distributed
stitches and an initial arrangement. Firmament owns the pattern and seam truth;
the shared `Aetheris.Cloth3D` runtime owns constraints and simulation. Blender is
an optional viewer of exported meshes, not a second fitting or simulation path.

## Source workflow

Start with the examples in `fixtures/Canonical/Garment`: a two-panel skirt, a
sleeveless tunic, a flared-skirt variation and four-panel shorts. Reuse `Feature (...) -> Profile`, named
Profile boundary spans, `Mirrored Profile` and finite `Pattern ... over Set`
instead of copying per-instance coordinates.

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

The static drape baseline uses 30 sewing ticks with decreasing stitch compliance,
then the requested number of gravity ticks. Default settlement is 60 ticks at the
shared solver's fixed timestep. Qualification requires finite state, exact
capture/restore replay, seam gap <= 10mm, sampled penetration <= 5mm and stretch
<= 15%. These are explicit authoring checks, not textile calibration or a
collision-free certificate. Inspect front, side and back views too:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python scripts/render-garment.py -- --bottom skirt
```

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
boundaries stop with diagnostics. One fabric per garment. One static Rest-pose
body binding. No seam allowances, darts, zippers, graded sizes, textile material
calibration, animated-body fitting, continuous collision detection, body
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
