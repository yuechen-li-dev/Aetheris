# FACTORY-SCENE-X0

**Verdict: Success for the static Scene and presentation witness.** Simulation,
process fidelity and robotics execution remain outside the qualified claim.

## Scope and artifacts

An original static automated factory authored through the real Firmament Scene
compiler. The 42 × 28 × 7 metre hall contains three 16-metre production lines,
three packout stations, two robot islands, five storage racks, seven wheeled
carts, open totes, pallet buffers, inspection benches, cabinets, columns,
overhead luminaires and pedestrian lanes. Dimensions are visual design choices,
not an industrial engineering specification.

The canonical [Scene](../../../fixtures/Canonical/Scene/FactoryX0/factory.firmament)
owns the hall, Concept layout, equipment placements, markings and five cameras.
The [kit](../../../fixtures/Canonical/Scene/FactoryX0/kit.firmament) owns shared
parts and subassemblies. Production, packaging, robotics, storage and structure
have independent Assembly documents. Each subsystem moves as one component.

Generated output defaults to ignored `artifacts/local/factory-scene-x0/`:

| File | Purpose |
| --- | --- |
| `factory.usda` | Complete enclosure, hierarchy, shared instances, materials, five cameras |
| `factory-enclosed.glb` | Complete static GLB |
| `factory.glb` | Presentation cutaway: ceiling, south wall and east wall omitted |
| `hero.png`, `overview.png`, `line.png`, `robot.png`, `logistics.png` | Five 2560 × 1440 presentation stills |
| `factory.blend` | Imported compiled geometry, authored cameras and presentation lighting |
| `factory.pptx` | Two slides, each embedding the cutaway GLB and a PNG fallback |
| `timings.json`, `external-validation.json`, `blender-validation.json` | Measured qualification evidence and source/artifact hashes |

## Capabilities and authoring friction

Scene previously rejected the existing `Linear Sites` and `Mirrored Sites`
declarations. It now admits the shared producer without introducing another
pattern engine. Named keys, derivation metadata, positive-handed placements,
finite mm vectors and existing bounds/rejection rules remain intact. Scene
recipes do not enter engineering definition cache inputs.

Pattern offsets such as `site.Y - 600mm` previously failed as a nonliteral
length. Scene now normalizes metre literals and delegates dimension checking
to the existing scalar evaluator. Assembly translations use that evaluator
with their existing mm contract. This keeps shelf offsets and fixture
suspensions local to their definitions. Unitless additions, length products
and division by zero fail instead of producing arbitrary coordinates.

Appearance now admits `emissive: [r,g,b]`, finite linear RGB in `[0,1]` with
black as the default. GLB emits `emissiveFactor`; USD emits PreviewSurface
`emissiveColor`. Luminaires, monitor faces and status indicators use it. It is
portable appearance state rather than photometric light intensity.

The final source uses PascalCase construct/type names and camelCase field labels.
The CLI formatter confirms preferred spelling without changing any of the eight
Firmament documents. Shared component patterns replace copied part lists.
The rendering script adds area illumination at compiled diffuser locations and
two presentation fill lights. Concrete micro-bump is shader detail. It creates
no equipment and verifies that imported geometry and transforms remain unchanged.

## Reproduction

From the repository root with .NET 10, Blender, the repository's OpenUSD tools
and the Khronos validator installed:

```powershell
dotnet build Aetheris.slnx -c Release -m:1
./scripts/qualify-factory-scene-x0.ps1
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
# Optional installed-PowerPoint native qualification:
./scripts/check-factory-powerpoint.ps1
```

The qualification script compiles the reviewed source, measures retained edits,
exports both GLB variants and USD, runs external validators, renders all views
and embeds actual GLB and PNG bytes via Aetheris's Model3D presentation compiler.
It fails on missing dependencies or failed commands. Build/test qualification
is a separate gate, not inferred from successful rendering.

## Evidence

The scene expands to 2,562 display nodes, 1,492 full-scene mesh occurrences,
124 display definitions and 92 engineering definitions across the independent
compiler sessions. The cutaway imports in Blender as 1,481 meshes sharing 114
mesh datablocks. Independent compilation sessions qualify reuse separately;
these counts do not claim a global cache across all subsystem documents.

Release solution build passed with four existing WASM native-varargs warnings
and no errors. The focused Scene lane passed 42 tests. The required fast Core
lane passed 1,005 tests. The final serial full solution lane passed **4,309 tests**
with zero failures and zero skips, including the slow interchange corpus. The
FrictionLab test assembly reports no discoverable tests. An earlier run caught
a link to this report before the file existed; the completed documentation and
final source passed the complete rerun. Test-generated STEP line-ending churn
was restored; it is not part of this change.

One measured Release retained-session run took 6.15 seconds cold, 1.12 seconds
unchanged, 0.92 seconds after a camera edit, 0.72 seconds after a row-pitch edit
and 0.75 seconds after a Scene appearance edit. Every retained build reused all
92 engineering definitions with zero rebuilds. The edit probes compare position
and index arrays against the original geometry. These are individual local
timings with JIT/cache warm-up effects, not a statistical benchmark. Display
preparation still runs; retained STEP reuse does not imply zero tessellation work.

Both complete and cutaway GLBs pass Khronos glTF Validator 2.0.0-dev.3.10 with
zero errors and warnings. Informational messages report identity matrices and
unused default/cutaway material meshes. OpenUSD 0.25.8 opens the complete Scene
with zero composition errors, 1,492 mesh occurrences, 124 shared prototypes and
five cameras. `usdchecker` passes. The final external validator additionally
checks finite vertices, index ranges, material bindings, emission and exact
agreement between independent USD bounds and CLI inspection.

The renderer uses Cycles, OptiX, adaptive sampling, denoising and at most 32
samples with a 60-second tracing budget per view. Presentation shader/lighting
choices are not photometric evidence. Each final image is inspected visually;
the Blender evidence compares a geometry/transform hash before and after
rendering and records actual render time and the source GLB hash.
The final five renders took 77.6, 17.8, 10.2, 11.7 and 15.1 seconds respectively
for Hero, Overview, Line, Robot and Logistics on an RTX 3070. Total import,
preparation, rendering and evidence generation took 138.2 seconds. The 60-second
budget bounds tracing, not import/BVH preparation, denoising or image writing.

The PowerPoint package passed exact embedded-model and preview byte checks with
two Model3D elements, two fallback pictures and zero external relationships.
Microsoft PowerPoint 16.0 opened both models natively through COM, changed native
rendered output after rotation, saved a separate copy, closed/reopened it,
preserved rotations and rotated both again. Native slide exports were visually
inspected for layout and real 3D output. The pre-rotation poster uses the authored
PNG; rotation refreshes the native model view. This is native automation evidence,
not a mouse/UI acceptance test. The separate roundtrip package is qualification
output; the delivered `factory.pptx` retains its authored views.
The saved Office package also retains both Model3D elements and the exact source
GLB bytes. Native PowerPoint lighting differs from the Cycles stills; the PNG
fallbacks preserve the rendered presentation finish.

## Honest boundary and shot plan

This is a programmable spatial/presentation witness. It demonstrates authored
geometry, shared components, spatial composition, retained compilation and
portable static assets. It does not qualify manufacturing process accuracy,
collision avoidance, safety guarding, logistics flow, physics, robot controls,
IK or production simulation. Robot arms are original simplified connected
silhouettes, not certified mechanisms. Machine windows are display panels, not
transparent views of modeled internals. Materials have nominal presentation
identities and no certified physical properties.

The PPTX package contains embedded static models and fallbacks. Native COM
rotation/save/reopen is qualified above; manual mouse interaction is untested.
The check uses the documented [Office Model3D API](https://learn.microsoft.com/en-us/office/vba/api/powerpoint.model3dformat).
OpenUSD parser/checker validation does not imply usdview interaction; usdview
was not opened during this qualification.

For a promo edit, use Overview (establish scale), Hero (operational floor), Line
(repeated modules), Robot (automation detail) and Logistics (material staging).
These are saved cameras and stills. No motion clip or simulated process execution
is claimed.
