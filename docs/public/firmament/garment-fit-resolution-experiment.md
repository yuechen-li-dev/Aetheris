# Garment fit, resolution and presentation experiments

Date: 2026-10-11. This continues the [armhole experiment](garment-armhole-experiment.md)
with the same MIT-credited GarmentCode cut, Anatolia body, fabric and sewing/motion
schedule. These are bounded experiments, not an accepted production garment.
Detailed run data and meshes remain under ignored
`artifacts/local/garment/fit-resolution-round/`.

## Controlled fit and regional trials

All complete runs use 30 sewing frames, 60 fitting frames, a half-second T-to-A
transition, 60 wearing frames, eight substeps and 24 structural iterations.
Temporary supports release after 20 fitting frames. There are no permanent pins.
The acceptance limits remain 15% tensile strain, 10mm seam gap and 5mm sampled
body-clearance violation. Qualified checkpoints do not certify every intervening
surface or motion.

| Trial | Fitting strain | Midpoint strain | Wearing strain | Complete sequence |
| --- | ---: | ---: | ---: | --- |
| Retained control, 60mm mesh request | 22.309% | 22.372% | 9.228% | Fails |
| Collar plane lowered from shoulder +45mm to +15mm | 64.887% | 77.871% | 53.519% | Fails |
| Support sewn shoulder edges at the original height | 21.789% | 22.511% | 9.227% | Fails |
| Collar material region, original support | 22.596% | 22.960% | 9.201% | Fails |
| 30mm mesh request, original support | 19.558% | 19.666% | 20.450% | Fails |

The first row is the retained, source-bound control from the previous round. New
support trials change only the erased Concept guide or its named edge selection.
The shared rest/arrangement content key for the 60mm trials remains
`42C204F0BE495216B6CB3C2C3087EC8C786135D3EE71FE2D465BCFB4D468BA8B`.
Source hashes differ because guides, pose declarations and comments are separate
authoring context. Body artifact SHA-256 remains
`0a0ad03e86447fc56448940c7aa56b54ea6683e8a874f833e01fd56a65a2d0e5`.

The collar region selects a 48mm material-space band around the named collar
curves: 56 vertices and 121 modified structural stencils. Stretch compliance is
scaled by 0.25 and bending compliance by 4. Rest lengths, masses, seam compliance
and cross-region stencils remain unchanged. The complete regional run has exact
static/midpoint replay and releases all temporary guides. It is still rejected:
right-back armhole link 331/332, rest length 10.770mm, dominates fitting and motion.
No regional settings or support changes become defaults.

Denser geometry moves the dominant failure. At 30mm, link 54/55 on the left-back
armhole adjacent to the shoulder seam has rest length 19.736mm and reaches
20.450% wearing strain. Seam gap improves to 0.353mm, while sampled clearance
violation remains below 0.001mm. Improved sewing accuracy does not establish an
accepted material fit. More density is not a substitute for resolving that
local strain failure. Its concentration beside the shoulder seam suggests a
contact/structural conflict, but these trials do not isolate that causal mechanism.

`GarmentMeshInspection.StretchOutliers` now owns the shared structural diagnostic.
CLI evidence records named-panel/material-coordinate outliers at fitting,
midpoint and wearing checkpoints, rather than reporting only the final frame.

## Mesh quality and bounded startup cost

The mesher's interior lattice pitch is 80% of `meshSize`. Requests of 60, 30 and
15mm therefore produce median edges near 48, 24 and 12mm. The same analytic cut
is resampled, not resized; small rest-area differences come from curved boundary
chord approximation.

| Request | Vertices / triangles | Median edge | Minimum angle | Triangles below 10 degrees | Lumped area ratio | Five startup frames |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 60mm | 611 / 992 | 48mm | 10.33 degrees | 0 | 26.32 | 9.17s |
| 30mm | 1,944 / 3,486 | 24mm | 19.02 degrees | 0 | 15.32 | 20.55s |
| 15mm | 7,224 / 13,672 | 12mm | 19.01 degrees | 0 | 11.74 | 61.40s |

All have zero unused vertices. Rest areas are 0.804074, 0.803821 and 0.803607m².
The prefix probe uses the real T-pose body, temporary guide, solver, contact
queries and first five sewing compliance values. It is not a completed drape or
convergence test. Position/velocity replay agrees exactly. Fifth-frame strain is
51.54%, 93.70% and 98.28% respectively; these early sewing transients are not
qualification checkpoints.

Timing is a local observation including diagnostic samples, not a hardware or
continuum-material benchmark. Link compliance is an uncalibrated discrete
authoring control; keeping it fixed across resolutions does not establish equal
physical fabric stiffness. The complete 30mm run took about 641 seconds and
overlapped the independent regional trial. The 15mm complete sequence is not run:
its prefix costs roughly 6.7 times the coarse prefix, and the completed medium
trial worsens final strain. A longer, denser repetition has no established benefit.

## Optional presentation binding

`Aetheris.Cloth3D.ClothPresentationBinding3D.Compile(plan, levels)` constructs
deterministic sparse Loop interior stencils for zero, one or two refinement
levels. Material boundaries are fixed; inserted boundary vertices remain exact
linear samples. This includes sewn panel boundaries: the operation does not
weld them or hide measured seam gaps. Compiled weights retain original simulation
vertex correspondence and material coordinates. Budgets stop before exceeding
65,536 display vertices or 262,144 display triangles.

Evaluation accepts only a snapshot belonging to that compiled cloth key. It
produces a finer surface without changing physics, stored vertices, rest lengths,
constraints, contact geometry or qualification. In Aurelian:

```csharp
var artifact = ClothGarmentArtifact3D.Load("dress.garment.json");
var agent = ClothAgentDefinition3D.FromGarment(artifact).WithPresentation(2);
```

Compilation occurs in the explicit authoring call. Scene projection evaluates the
binding and normals without stepping the agent. It has a distinct presentation
identity, so display topology does not alias the original mesh. The current
presentation evaluator is CPU-based.

| Display levels | Vertices / triangles | Maximum sampled clearance violation | Sampled body penetration |
| --- | ---: | ---: | ---: |
| 0 | 611 / 992 | below 0.001mm | 0 |
| 1 | 2,210 / 3,968 | 1.648mm | 0 |
| 2 | 8,384 / 15,872 | 2.041mm | 0 |

Here clearance includes the authored body margin plus half cloth thickness;
body penetration uses half thickness only. The refined surface spends some of
that margin but does not enter the sampled body. This is a sampled diagnostic,
not an exhaustive intersection proof. On all three displays, simulation strain
remains 9.228% and seam gap remains 1.1325mm.

Front/side/back renders show a modest smoothing benefit. They still show the
projecting neckline, stiff silhouette and visible center seam. Refinement cannot
invent textile construction or fix the remaining physical fit. Blender presents
the engine-generated meshes with no additional fitting, subdivision or simulation.

## Regional material capability

`ClothDefinition3D.MaterialRegions` supplies disjoint named vertex sets with
finite stretch/bend compliance scales in `(0,1000]`. A structural constraint is
modified only when its whole stencil belongs to one region. Stitches retain
their own authored compliance. Empty, invalid, duplicate or overlapping regions
stop before simulation. Regional data enters the content key and garment
transport; definitions without regions preserve their established keys.

This is a typed C# capability. `schema Garment` still authors one shared Fabric;
no speculative regional Firmament syntax is introduced. The existing Vulkan
storage ABI receives the compiled compliance values without another shader path.

## Reproduction

Use the repository CLI and actual gameplay body. `control-30.firmament` is the
canonical guided source with only `meshSize: 60mm` replaced by `meshSize: 30mm`.
The lower collar trial replaces `shoulderHeight + 45mm` with `+15mm`. The shoulder
trial selects `left_ftorso.edges.edge3`, `right_ftorso.edges.edge2`,
`left_btorso.edges.edge2`, and `right_btorso.edges.edge3` instead of collar edges.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment drape `
  artifacts/local/garment/fit-resolution-round/control-30.firmament `
  --body artifacts/local/humanoid-production/antonia.gameplay-body.json `
  --ticks 60 --out-dir artifacts/local/garment/fit-resolution-round/control-30 --json

dotnet run -c Release scripts/experiment-garment-fit.cs -- prefix `
  fixtures/Canonical/Garment/garmentcode-dress-guided.firmament `
  artifacts/local/humanoid-production/antonia.gameplay-body.json `
  artifacts/local/garment/fit-resolution-round/prefix

dotnet run -c Release scripts/experiment-garment-fit.cs -- regions `
  fixtures/Canonical/Garment/garmentcode-dress-guided.firmament `
  artifacts/local/humanoid-production/antonia.gameplay-body.json `
  artifacts/local/garment/fit-resolution-round/regions-60
```

Presentation mode takes a matching source/body/output directory and reads
`input-garment.json` in that directory. It rejects a source-hash mismatch instead
of accepting a superficially similar source. The retained control uses its
original `armhole-round/anatolia-guided-transition.firmament` source, not the
later canonical source with different comments.

The tested scene projection remains distinct from native GPU body draping.
Fitting guides and triangle-body contacts remain CPU-only; no friction, CCD,
garment self-contact or physical fabric calibration is newly qualified.

## Validation

Both Release solution builds succeed. The complete Aetheris solution test run
passes 4,522 tests with test collections serialized; the complete Aurelian
solution passes 1,219 tests, including 28 cloth tests. Presentation tests exercise
actual scene compilation, mounting and projection without advancing simulation.
Regional tests cover transport round-trip, content identity, preserved rest
metrics and masses, seam exclusion, invalid selections and scalar GPU packing.

The first Aetheris run encountered the existing five-second CAD display deadline
in `MissingDisplayPatch_IsSeparateFromEngineering_WithSourceFaceIdentity`.
That test passes alone and in the serialized complete rerun. No CAD geometry,
test budgets or qualification limits were changed. The test-generated tracked
STEP witness was restored after validation.

The actual `Aurelian.GraphicsProof --cloth-systems` path compiles
`Cloth3D.v.ts` and runs Vulkan compute on NVIDIA GeForce RTX 3070. Its added
25-vertex regional case runs eight steps with a deformed initial surface;
maximum CPU/GPU position disagreement is 0.004554mm. Same-device position and
velocity replay agrees exactly. The full proof emits
`AURELIAN_CLOTH_SYSTEMS_PASSED`; detailed evidence remains in Copeland's ignored
`artifacts/local/garment-fit-round-vulkan/`.

This GPU result qualifies regional constraint execution, not Anatolia triangle-body
draping or GPU evaluation of the new presentation stencils. The complete garment
experiments all remain rejected by their unchanged fit gates. No commercial
geometry, textures, morphs or extracted assets are inputs to these experiments.
