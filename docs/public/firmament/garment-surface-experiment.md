# Body-referenced quad garment experiment

The first surface-first specimen is a fitted skirt around the repository's
Anatolia gameplay body. It is authored in typed C# through
`GarmentSurfaceAuthoring.Skirt`, above the existing body-section and cloth
owners. This experiment does not introduce Firmament syntax yet.

```csharp
var body = HumanoidGameplayBody.Load("antonia.gameplay-body.json");
var skirt = GarmentSurfaceAuthoring.Skirt(
    new GarmentSkirtSurface("skirt", HipHeight: .96f, ChestHeight: 1.28f),
    body,
    new ClothMaterial3D
    {
        WarpCompliance = .001f,
        WeftCompliance = .004f,
        DiagonalCompliance = .002f,
        BendCompliance = .01f,
    });
File.WriteAllText("skirt.garment.json", skirt.Artifact());
```

Dimensions are metres. The declared section heights are the same as the
canonical Anatolia fitting specimen, not universal anatomical defaults.
Anatolia's pelvis joint height does not intersect one closed torso contour;
the section query rejects it. Callers may override section heights explicitly.
`HumanoidFigureFitting.Contour` exposes the ordered closed contour already
used by FigureFit, preserving open/ambiguous-section diagnostics.

The author declares waist height relative to the hip section, length, ease,
hem flare and resolution. The skirt interpolates radial envelopes of waist and
hip sections, then extends a flared envelope below the hip. This bridges the
legs rather than copying the body's surface. It uses no commercial geometry.
The waistband is an explicit ring of permanent attachments; there is no sewing
stage or hidden temporary fitting support.

## Material cells and rest conventions

`ClothDefinition3D.Surface` opts into authoritative four-corner cells:

- Each cell has its own metre material coordinates. The periodic skirt welds
  physical vertices while retaining material coordinates across the chart cut.
- A Q1 membrane uses 2x2 Gauss quadrature for lumped mass, warp/weft strain and
  shear. Compliance is inverse surface stiffness divided by quadrature area.
- Shared quad edges use objective signed rest-angle bending. There are no
  structural springs along the rendering diagonal.
- `FlatFabric` retains the flat material metric and zero rest hinge angles.
  `FittedSurface` derives a stress-free intrinsic metric and rest hinge angles
  from the supplied 3D shape. These are deliberately different physical models.
- Grain is a continuous rotation of material derivative operators. Rotating
  grain by a tiny angle does not relabel every edge as a diagonal spring.

Existing triangle/edge definitions remain available with their existing
content keys. Quad definitions append a versioned material-cell identity and
travel through `aetheris.garment.v1`; its runtime loader needs no Firmament
compiler. Invalid cells, inverted material Jacobians, inconsistent winding,
singular rest hinges and unsupported offset self-contact fail explicitly.

Triangles still lower the cells for rendering and existing sampled body
contact. Changing the display diagonal leaves quad masses, structural
constraints and contact-free simulation unchanged. Body-contact sampling and
presentation subdivision can still depend on that triangulation.

## Reproduce and inspect

```powershell
dotnet run -c Release scripts/experiment-garment-surface.cs -- `
  artifacts/local/humanoid-production/antonia.gameplay-body.json `
  artifacts/local/garment/surface-skirt

& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' `
  --background --python scripts/render-garment.py -- `
  --garment surface-skirt --input-dir artifacts/local/garment/surface-skirt `
  --engine workbench
```

The script writes the original quad OBJ, settled quad OBJ, runtime artifact,
body OBJ and numeric evidence under ignored `artifacts/local`. Blender presents
those exact exports without fitting, smoothing modifiers or simulation; render
evidence hashes its inputs.

The local 32x9 skirt has 288 vertices, 256 quads, 512 rendering triangles,
32 waistband attachments and no stitches. At frame 32 of the stationary-body
gravity experiment, maximum tensile stretch was 2.087%, maximum sampled
clearance violation was 0.040 mm, and attachment error was zero. Rest residual
was below 0.000004; eight zero-gravity frames drifted by 0.000362 mm. Both
checkpoint replay and runtime-artifact continuation were exact. The experiment
took approximately 2.3 seconds on this host; this is not a general performance
benchmark. Clearance includes cloth half-thickness plus the declared body gap;
it is a sampled diagnostic, not a global intersection proof.

The shared CPU/Vulkan patch proof exercises deformed flat and curved-rest quad
surfaces. On the local RTX 3070 the maximum CPU/GPU position differences were
0.00326 mm and 0.00405 mm respectively, with exact same-device rewind replay.
Body-surface contact remains CPU only. The GPU extension uses the existing
colored dispatch tape and source/state buffers, with transverse derivatives in
the previously reserved stencil-header word. Visual TypeScript's compute
`Atan2` intrinsic supplies the signed-angle calculation.

## What this result does and does not establish

This is a successful stationary fitted-skirt foundation and a useful
surface-authoring witness. It does not certify the earlier panel-authored dress.
It is not a recovered sewing pattern, measured textile model or production
garment. A stress-free fitted surface can have intrinsic curvature that no
single flat panel can reproduce without strain or cuts.

Animated legs, quad self-contact, inversion prevention, friction and calibrated
material response need separate qualification. Degenerate runtime hinges skip
their bending update; there is no barrier preventing collapsed/inverted cells.
The bend diagnostic is a sum of squared angular residuals, not physical energy.
The [spline tube-top follow-up](garment-spline-experiment.md) adds retained
editable supports and fit feedback before quad lowering. Explicit surface cuts,
flattening with distortion reports and animated-body validation remain future
experiments. A Firmament Surface
declaration should be designed around these proven typed contracts rather than
silently treating fitted rest as flat fabric.

## Validation

Both Release solution builds passed. The full Aetheris suite passed 4,532 tests
with serial collection settings, the full Aurelian suite passed 1,220 tests,
and Copeland TS passed 1,497 tests. Ten new Aetheris cases cover grain continuity,
affine energy across resolution, triangulation independence, stress-free curved
rest, rigid transforms, finite-difference gradients, invalid inputs, body
contours, periodic garment topology and runtime artifact continuation. The
Aurelian scene test additionally covers curved rest through axis conversion,
placement, agent stepping and projection. The actual Vulkan proof passed on the
RTX 3070; its retained evidence is in Copeland's ignored
`artifacts/local/quad-cloth-systems` directory.
