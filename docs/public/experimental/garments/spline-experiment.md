# Body-fitted spline garment experiment

> **Experimental.** Garment work is research, not part of the supported product. Its APIs, syntax and results may change or be withdrawn. See [experimental modules](../README.md).

This bounded experiment authors a tube top as two editable bicubic B-spline
midsurfaces fitted to the actual Anatolia gameplay body. The retained control
nets own the shape. Structured quads sample those supports directly, then enter
the existing cloth solver and `aetheris.garment.v1` runtime artifact path.
Blender only presents exported geometry. No commercial reference geometry is
used or shipped.

The typed API lives in Firmament's garment authoring owner. Like the preceding
surface skirt experiment, this pass does not introduce document syntax. These
proven contracts should inform a future `schema Garment` Surface declaration;
the C# guide records are not claimed to be erased Firmament Concept Structs.

```csharp
var body = HumanoidGameplayBody.Load("antonia.gameplay-body.json");
var fitting = GarmentSplineAuthoring.FitTubeTop(
    new GarmentTubeTopSurface("tube-top", UnderarmDrop: .08f), body);
var source = fitting.Source;
var garment = GarmentSplineAuthoring.Lower(source, new ClothMaterial3D
{
    WarpCompliance = .001f,
    WeftCompliance = .004f,
    DiagonalCompliance = .002f,
    BendCompliance = .01f,
});
File.WriteAllText("source.json", GarmentSplineAuthoring.SerializeSource(source));
File.WriteAllText("tube-top.garment.json", garment.Artifact());
```

## Source, fitting and inspection

Guides declare upper/lower heights, ease and underarm drop in metres. These
defaults describe this Anatolia specimen, not universal anatomical landmarks.
The fitting owner uses the existing closed torso-section query at 13 heights,
interpolates radial envelopes between sections and samples targets for a shaped
upper edge. The underarm height varies with squared side direction. It lowers
the edge at the sides while retaining front/back coverage.

`BSplineControlNetFitting` is a bounded, polynomial shared-control least-squares
fit in Aetheris.Surfacing. Kernel evaluation supplies basis weights. Seed
regularization makes the bounded linear system positive definite; control-net
second differences regularize shape in parameter space. This authoring penalty
is not a cloth stiffness model. The fitter rejects rational supports, invalid
samples, inconsistent shared seeds and exceeded budgets with named diagnostics.
It neither guesses the body geometry nor performs cloth simulation.

Two 7x4 control nets share their side control columns: 56 logical controls,
48 independent positions. This gives exact positional side continuity, not a
tangent-continuity certificate. Source edits must preserve shared boundary IDs
and positions. The tests edit a front interior control with an 8 mm displacement,
verify changed source/mesh identity and unchanged back support, and reject a
broken side boundary before lowering. Sources round-trip with identical hashes
and compiled cloth keys. Geometry, material and body provenance enter identity;
machine paths and commercial assets do not.

`SurfacePatchBrepMaterializer.MaterializeSheet` retains each support as a single
open BRep face with exact four boundary curves and pcurves. The existing binding,
pcurve and wireframe owners validate and inspect it. It is not a closed CAD
solid and no STEP solid-export claim is made.

The runner exports `front-support.svg`, `back-support.svg`, `control-cage.obj`
and `clearance.svg` alongside the source and meshes. Clearance uses 2,450
independent nearest-triangle queries on the actual spline supports. The SVG
locates samples in each parameter chart and marks clearance below 5 mm in red,
5..10 mm in amber and at least 10 mm in green. It is sampled feedback, not a
global intersection certificate. Control-cage rendering presents source
positions; the cage is not the garment mesh and is not simulated.

## Quad material path and measured result

The garment has 624 vertices, 576 welded material quads and 48 explicit
lower-band attachments. Cells have metre-valued material coordinates and
`FittedSurface` rest. Unit spline parameters are never treated as textile
metres. The existing Q1 membrane and rest-angle bending operators own mechanics;
display triangles do not become diagonal structural springs.

The motivating level-upper-edge specimen remains an intentional failure:
minimum sampled clearance was **-36.22 mm**, and frame-eight maximum stretch
was **46.30%**. Contact response pushed its back edge outward. Giving the source
an explicit 80 mm underarm drop fixed the geometrical conflict without changing
acceptance limits or projecting simulation vertices out of the body.

For the accepted specimen, seed RMS target error fell from **26.38 to 3.66 mm**
(maximum target error 17.25 mm). Independent clearance was **6.71..32.10 mm**.
All four eight-frame checkpoints in the 32-frame gravity run were finite,
with maximum tensile stretch at most **4.32%**, zero attachment error and zero
sampled body-clearance violation. Maximum frame-32 displacement was 20.61 mm.
Eight zero-gravity frames drifted by 0.00126 mm. Source reload, solver checkpoint
rewind and settled runtime-artifact continuation reproduced exactly.

Aurelian's existing `ClothAgentDefinition3D.FromGarment` imports the actual
settled artifact and converts it to Y-up world state. The extended native
`--cloth-systems --garment` probe runs this 624-vertex garment through both CPU
and Vulkan solvers for eight additional gravity steps. On the local RTX 3070,
maximum CPU/GPU position difference was **0.00289 mm**, with exact same-device
rewind. This GPU probe has no body contact; the 32-frame body-contact experiment
uses the existing CPU body surface. The agent retains its existing material
assignment API for Aurelian presentation.

## Reproduce

```powershell
dotnet run -c Release scripts/experiment-garment-spline.cs -- `
  artifacts/local/humanoid-production/antonia.gameplay-body.json `
  artifacts/local/garment/spline-tube-top

& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' `
  --background --python scripts/render-garment.py -- `
  --garment spline-tube-top --input-dir artifacts/local/garment/spline-tube-top `
  --torso --engine workbench

# Inspect the retained rest and its source cage before simulation.
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' `
  --background --python scripts/render-garment.py -- `
  --garment spline-tube-top --input-dir artifacts/local/garment/spline-tube-top `
  --initial --control-cage artifacts/local/garment/spline-tube-top/control-cage.obj `
  --torso --engine workbench
```

An optional third runner argument loads `GarmentTubeTopSurface` JSON guides.
For the invalid control specimen, use `{"Id":"level-edge","UnderarmDrop":0}`
and a separate output directory. It deliberately returns a nonzero status.
All generated output belongs under ignored `artifacts/local/`; the body input
is a local prerequisite. Renderer evidence hashes every imported OBJ.

From Copeland:

```powershell
dotnet run --project tools/Aurelian.GraphicsProof -c Release -- `
  --cloth-systems --garment `
  ../Aetheris/artifacts/local/garment/spline-tube-top/garment.json `
  --output artifacts/local/garment-spline-gpu
```

## Qualification boundaries

This establishes editable spline fitting, open BRep inspection, quad lowering,
stationary-body simulation and real runtime/GPU consumption. The cloth response
is an experimental fitted-rest model, not calibrated textile behavior. The
lower-band pins are permanent authored support; strapless friction is absent.
The paired supports are position-continuous, and side creases remain visible.
Animated bodies, quad self-contact, inversion barriers, free strapless support,
flat sewing-pattern recovery, automatic flattening and general garment fitting
are unqualified. This experiment does not certify the earlier panel dress.

Eleven new regression cases cover independent polynomial recovery, deterministic
fitting, exact shared boundaries, non-unit-domain BRep reconstruction, rational
and invalid sample rejection, garment source reload, local control edits,
broken seams, welded topology and runtime continuation. Full solution gate
results are recorded in the experiment's local build/test logs.

The Aetheris Release solution build and full configured solution suite passed
**4,543 tests**, including all eleven new cases, with serial collection settings.
The test-generated STEP specimen was restored after validation.

The Aurelian Release solution build and full 1,220-test consumer suite passed.
The browser shader lane used the already installed Naga selected through
`AURELIAN_NAGA`; an initial run without that setting failed to locate Naga.
The native cloth proof passed through the real compiled Visual TypeScript
shader on the RTX 3070.
