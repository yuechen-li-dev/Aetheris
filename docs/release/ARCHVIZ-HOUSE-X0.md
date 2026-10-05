# ARCHVIZ-HOUSE-X0 — InteriorDesignerGPT

Qualification date: 2026-10-04. **Verdict: Accepted.**

Codex can use Firmament Scene as a pleasant programmable environment language
for this bounded warm-modern home. The actual Concept-guided source compiles,
shared furniture renders coherently, external USD/GLB validation succeeds, and
the resulting PowerPoint model rotates, saves, reopens and remains interactive.
The reusable authoring gaps were addressed in their existing semantic owners;
the remaining deliberate simplifications and justified defers are explicit.

## Design and authoring story

The brief was a compact warm modern home: Scandinavian/Japanese restraint,
warm oak, off-white plaster, forest-green cabinetry, cream upholstery, black
metal and large glass openings. The resulting 63 m² open-plan room contains a
sectional living area, an island kitchen and four-seat dining area; a 5.28 m²
entry room makes the transition legible. Clear room height is 3.1 m. Three
large windows, two pendant fixtures, floating shelves and a media bench provide
depth without a large asset collection or elaborate architectural detailing.

The human supplied the design intent and the working strategy: top-down Concept
layout, bottom-up furniture, then composition using those guides. Codex authored
and reviewed the finite Firmament source. Aetheris compiled semantic rooms,
openings and shared engineering definitions into Scene display data. The same
display representation exported OpenUSD and GLB. Blender imported the complete
GLB for Cycles lighting/shading; the existing Presentation-3D writer embedded
the presentation GLB and rendered PNG fallbacks in PowerPoint.

```text
Human warm-modern brief
  → Concept Struct room-relative layout
  → shared Firmament furniture templates/subassemblies
  → Room / Door / Window / placed Assembly / Camera
  → compiled Scene
  → OpenUSD + GLB
  → Cycles renders + interactive PowerPoint
```

Python only imports, shades, lights, renders and verifies compiled outputs. It
does not generate Firmament, house geometry, furniture placement or camera
transforms. A before/after hash of imported mesh vertices, polygons and world
transforms checks this boundary. The overview is an explicitly disclosed
presentation cutaway; the complete enclosure remains in source, USD, the
enclosed GLB and the saved Blender scene.

## Reviewed source structure

Source project: [WarmModernHouse](../../fixtures/Canonical/Scene/WarmModernHouse/house.firmament).

| File | Semantic ownership |
| --- | --- |
| `house.firmament` | `HouseLayout` guides, architecture palette, two Rooms, three Doors, three Windows, six placed furniture groups, Hero/Kitchen/Overview cameras |
| `components.firmament` | Rounded profile/extrusion Panel and cylindrical Drum templates; published `TopSeat.Frame`; palette/material identities; shared DiningChair |
| `living.firmament` | Complete Sofa, CoffeeTable and SideTable subassemblies; rug and their local composition |
| `kitchen.firmament` | Shared BaseCabinet, six cabinetry occurrences, counter, repeated shelves, sink/cooktop placeholders |
| `island.firmament` | Island body/top, supported decorative objects, two shared pendants |
| `dining.firmament` | Table, four shared chairs with keyed local orientations, centerpiece |
| `media.firmament` | Wall-facing media bench and screen |
| `entry.firmament` | Entry bench and cushion |
| `house-presentation.json` | Two slides, local GLB/preview references and presentation view parameters |

`HouseLayout` has two Room-derived planes and six furniture datum frames.
Scene placement references those names. Each furniture group owns its internal
dimensions and offsets. Repeated chairs, cabinets, cushions, feet, shelves and
pendants use existing finite keyed Sets/Patterns and shared definitions.

## Reusable capability admission and ownership

No new general construct, scripting facility or house-specific keyword was
added. The changes extend existing semantic owners:

| Motivation and syntax | Semantic owner / lowering owner | Reuse and boundedness | House use / verification |
| --- | --- | --- | --- |
| Room has different wall, floor and ceiling finishes: `floorAppearance: warmOak; ceilingAppearance: ceilingWhite;` | `SceneRoom` and Room schema / Scene panel appearance binding | Optional finite look references; omitted fields inherit Room appearance; no extra slab geometry | Both Rooms; floor binding and invalid look tests |
| An aperture should own its glazing/frame: `glazingAppearance: clearGlass; frameAppearance: charcoalMetal; frameWidth: 45mm;` with optional `glassThickness` | `SceneOpening` / `SceneWindowFinish`, Window schema / Scene window pieces using existing shared box definitions | Four inset frame pieces and one optional pane; dimensions derive from opening/wall; rejects nonpositive clear panes and glass thicker than wall | Three large windows; all four wall orientations, dimensions, material refs, invalid intent and external three-pane checks |
| Glass needs portable transparency: `Appearance clearGlass { ... opacity: .18; }` | Existing Appearance schema and `AssemblyUsdMaterial` / existing USD and GLB material exporters | Finite scalar in `[0,1]`, default one; no shader graph or physical glass solver | Three panes; USD opacity and glTF alpha/BLEND tests plus external material inspection |
| Top-down Concept guides must place Scene objects: `Plane mainFloor { from: main.floor; }`, `Placement { from: Origin; to: HouseLayout.living; }` | Existing `AssemblyDatumAuthoring` Plane/Axis/DatumFrame parser; Scene owns boundary references / existing `AssemblyFrameAuthoring.Compose` | Erased finite spatial scaffolding; no runtime state or geometry generation; existing cycle checks, unresolved guides rejected even when unused | Eight guides; translated/clocked room placement, no Concept occurrences, diagnostic and retained placement tests |
| Orbit-only presentation needs a disclosed cutaway: `scene export-glb ... --hide-boundary main.ceiling` | Scene boundary selection / `SceneExport` projection and CLI | Exact existing boundary paths; immutable projection, unchanged compiled scene; no new Firmament syntax | Six hidden boundaries in presentation GLB; mutation/unknown-boundary tests and CLI output protection |

Window finish geometry remains Scene display/environment geometry, like Room
panels. Furniture remains independently compiled engineering assembly geometry.
No parallel tessellator, datum parser, cache, naming migration or furniture
runtime was introduced. There is no strategy selection needing JudgmentEngine:
these are deterministic transformations of explicit authored intent.

Compiler-owned schema attributes expose every new field through existing
completion, hover, diagnostics and convention formatter paths. Tests cover the
schema and real house; CLI formatting of all eight source documents reports
`changed: false`. Existing Concept/Pattern/Placement vocabulary retains its
established language-service owner.

## Friction log

| Attempt / awkward existing source | Workaround possible? | Permanent fix / disposition and reuse rationale |
| --- | --- | --- |
| One Room appearance paints walls, floor and ceiling alike; separate floor overlays duplicate room geometry | Yes, overlay Parts | Added Room finish overrides. Surface finish is normal environment intent and should follow the owned boundary |
| Windows were holes only; independently placed strips/panes repeat opening width, height, sill and wall coordinates | Yes, duplicate Parts | Added bounded optional Window finish. Editing an opening keeps its pane/frame coherent on every wall orientation |
| Appearance was opaque, making authored glazing a painted panel | Renderer-only override possible | Added portable optional opacity to the existing look and exporters. Renderer refraction remains downstream |
| Scene could not consume the existing Concept Struct furniture stations; direct placement repeated room offsets | Yes, raw room/world translations | Reused the datum parser and frame composition, extending Scene reference resolution to Room-derived Concept guides |
| Furniture-on-support placement repeated cushion, countertop, tray and vase Z heights | Yes, coordinate arithmetic | Refactored complete local subassemblies around existing published `TopSeat.Frame`; no new capability needed |
| Full roof/walls hide interiors when PowerPoint only offers orbit interaction | Yes, destructive source deletion | Added explicit boundary export projection; full source/exports remain available and cutaway is disclosed |
| A template specimen using RoundedBox was rejected with `firmament-v2-phase3-edge-finish-syntax-invalid` | Analytic rounded profile/extrusion is admitted | Deliberately use RoundedRect2/Profile/Extrude for plan-rounded furniture. Full three-dimensional upholstered edge fillets are deferred; the chosen simple stylized geometry is intentional, not hidden renderer geometry |
| Architecture and independent AssemblyFile modules have separate look catalogs | Two matching warmOak/charcoal declarations | Keep the tiny architecture palette owned by Scene and the shared furniture palette in one included module. General Scene includes/palette imports are a justified defer; avoid extending module resolution for two readable palette entries |
| Retained geometry reuse still reimports exact bodies and prepares display meshes | Mutable cached bodies or renderer geometry would bypass authority | Preserve immutable STEP cache ownership. Retained display preparation remains a measured future optimization; zero geometry rebuild is not zero compile work |
| Renderer daylight initially matched `.pane` inside `.panel`, causing excessive lighting | Reduce light power blindly | Corrected the renderer to match the terminal `pane` segment; final evidence reports three window lights rather than illuminating wall panels |
| Machine-wide CLI was older than the checkout and did not expose Scene | Assume capabilities from stale CLI | Build/use `Aetheris.CLI` from this checkout for every qualification command |

## Dedicated source-quality review

The source was revisited after the first working scene. Complete sofa, coffee
table and side table compositions now have named local owners. Cushions, arms,
chaise seating, tabletop decorations and island layers reference support frames
instead of copying surface heights. Chair/cabinet repeats use keyed Patterns;
the root only composes six meaningful furniture groups. The media screen was
moved against its wall-facing local guide after visual review.

There are no authored matrices, renderer geometry names, generated source
blobs or anonymous room-sized boxes. Explicit numbers that remain describe
design dimensions or component-local spacing. Paired hallway openings are
separate owned wall apertures: X0 does not infer an inter-room adjacency
constraint, and that simplification is visible in source. Camera names are
presentation/domain names, not renderer ownership.

Constructs/types use PascalCase and fields use camelCase. Published domain
symbols (`World`, `Origin`, `TopSeat.Frame`, Site `X/Y/Z`) are preserved. Concept
layout coordinates retain the existing millimetre contract despite the Scene's
metre authoring convention; examples deliberately show units. This mixed unit
contract is discoverable and tested, but deserves continued documentation. No
general naming migration was reopened.

## Dedicated code-quality review

The Scene extension delegates Concept parsing, unit checks, basis handling and
cycle diagnostics to the existing datum owner. Scene resolves only its own
boundary/guide graph, with build-local resolution state. Window finish lowering
is one dimension-driven helper using the existing Scene box definition path.
Optional opacity follows the single existing look record to both exporters;
GLB material creation is shared between default and appearance-variant paths.
The cutaway creates a display projection without mutating source or compiled
occurrences. No component-specific compiler cases were added.

## Performance and qualification evidence

One final-process measurement on Windows, .NET SDK 10.0.401, with rendering
finished before measurement. These are single observations, not percentile or
universal speed claims. Raw source hashes and per-phase measurements are in
`artifacts/local/archviz-house-x0/timings.json`.

| Operation | Total ms | Display preparation ms | Engineering definitions reused / rebuilt |
| --- | ---: | ---: | ---: |
| Cold, new process/session | 2367.23 | 399.21 | 0 / 49 |
| Warm process, new uncached session | 2343.21 | 697.38 | 0 / 49 |
| Retained, unchanged | 964.59 | 552.53 | 49 / 0 |
| Retained, camera-only edit | 713.29 | 416.13 | 49 / 0 |
| Retained, furniture Concept-guide edit | 662.65 | 395.58 | 49 / 0 |
| Retained, Scene appearance edit | 644.08 | 387.91 | 49 / 0 |
| Retained, one Room size edit | 533.69 | 318.32 | 49 / 0 |
| Retained, one island body height edit | 586.85 | 304.14 | 48 / 1 |

Full Scene: **49 engineering definitions, 94 display definitions, 254 total
occurrences, 161 mesh occurrences**. Four chairs share their seat definition;
six cabinets share their door definition. The island top follows its body's
published support frame when the body height changes. Room panels/window pieces
are analytical Scene display definitions, outside the engineering cache counts.
Retained appearance/camera/placement tests also compare unchanged display vertex
and index arrays. Reuse reimports fresh exact bodies and currently repeats
display preparation, so camera edits remain measurable work even with zero
engineering rebuilds.

Serialization after compilation: **USD 53.04 ms; enclosed GLB 89.22 ms;
presentation cutaway GLB 37.41 ms**. The 5,115,927-byte two-slide deck compiles
through the existing writer in **131.65 ms**. This deck timing includes package
writing; serializer measurements exclude compilation and file I/O.

Cycles 5.2.2 LTS on NVIDIA RTX 3070 rendered 1920×1200, 80 samples with denoising:
Hero **340.97 s**, Kitchen **239.37 s**, Overview **15.57 s**; complete rendering
run **596.69 s**. The complete scene imported as 161 mesh objects / 96 mesh
datablocks (material variants can split imported datablocks). Mesh geometry and
world-transform hash stays unchanged through presentation finishing. There are
seven downstream lights, with three derived from the semantic panes and two
from pendant diffusers.

| Gate | Observed result / local evidence |
| --- | --- |
| Real CLI compile/validate | `success: true`, Scene domain, empty diagnostics; `source-validation.json` |
| Formatter/LX | All eight files unchanged by convention formatter; completion/hover for every new field, actionable invalid specimens; `formatter.json`, new 13-case `ArchvizSceneTests` |
| Focused Scene/Appearance/GLB tests | 37 passed; `focused.log` |
| Core fast lane | 1005 passed, zero failures/skips; `fast.log` |
| Full solution Release build | Zero errors; four warnings in unchanged guitar tests and WebAssembly SQLite varargs; `build.log` |
| Full serial solution tests | **4298 passed, zero failed, zero skipped**, 20 populated test assemblies; `full-tests.log`, `regression-summary.json`. Existing FrictionLab assembly reports no discoverable tests |
| Repository layout / whitespace | Repository guard and `git diff --check` pass; generated STEP corpus rewrite restored after tests |
| OpenUSD 25.08 | `usdchecker`: Success; no composition errors; 94 shared prototypes, 161 mesh occurrences, 12 bound looks, three authored cameras and three opacity-.18 panes; physical bounds agree with CLI to 0.01 mm; `usdchecker.log`, `external-validation.json` |
| usdview | Actual stage opens in HdStorm with `/Scene/N_Hero_0`; viewport/hierarchy captured through existing viewer API; `usdview/`. Default viewer lighting/material-display settings are not the Cycles presentation |
| Khronos glTF Validator 2.0.0-dev.3.10 | Both final GLBs: **zero errors, zero warnings**, 117 informational default-matrix/unused-base-mesh messages each; `*.validator.json` |
| Blender | Positive-handed transforms; expected physical bounds, three imported authored cameras; geometry hash unchanged; `blender-validation.json` |
| PPTX package | Two Model3D elements and two fallback pictures; embedded model bytes equal final `house.glb`; previews equal hero/overview PNGs; zero external relationships; `external-validation.json` |
| Desktop PowerPoint 16.0.20430.20118 | Compiled `house.pptx` opens without repair/missing-media warning. Both 3D controls rotate/tilt. Saved separate `house-office-roundtrip.pptx`, closed, reopened and observed further orbit interaction; two model elements survive and Office deduplicates to one exact original GLB payload |

PowerPoint initially displays the authored render preview; after first orbit it
regenerates a view of the interactive GLB. Its scalar-PBR render and viewport
framing differ from Cycles, as expected. The deterministic `house.pptx` retains
the original presentation-quality fallbacks; the Office roundtrip is evidence.

![PowerPoint overview after orbit](../../artifacts/local/archviz-house-x0/powerpoint-overview.png)

![PowerPoint after save/reopen and further orbit](../../artifacts/local/archviz-house-x0/powerpoint-reopened-rotated.png)

All raw JSON, logs, viewer captures and desktop screenshots remain in ignored
`artifacts/local/archviz-house-x0/`. No source/layout-generation Python path was
accepted, and no renderer geometry was substituted for compiled product data.

## Renders and deliverables

![Hero](../../artifacts/local/archviz-house-x0/hero.png)

![Kitchen](../../artifacts/local/archviz-house-x0/kitchen.png)

![Overview cutaway](../../artifacts/local/archviz-house-x0/overview.png)

Local generated deliverables:

- [house.usda](../../artifacts/local/archviz-house-x0/house.usda): complete enclosure, semantic hierarchy, shared prototypes and cameras.
- [house-enclosed.glb](../../artifacts/local/archviz-house-x0/house-enclosed.glb): complete portable scene.
- [house.glb](../../artifacts/local/archviz-house-x0/house.glb): explicitly projected presentation cutaway for web/orbit and PowerPoint.
- [house.pptx](../../artifacts/local/archviz-house-x0/house.pptx): two interactive views with exact rendered fallbacks.
- [house.blend](../../artifacts/local/archviz-house-x0/house.blend): imported full geometry, authored cameras and downstream Cycles finishing.

These links are local run evidence, intentionally excluded from Git. Source,
tests, reproduction scripts and this compact report are tracked candidates.

## Reproduction

Run from the repository root with its .NET 10 SDK. The installed global tool
may be older; the commands below use the built checkout.

```powershell
dotnet build Aetheris.slnx -c Release -m:1
$taskCli = 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
$taskHouse = 'fixtures/Canonical/Scene/WarmModernHouse/house.firmament'
$taskOutput = 'artifacts/local/archviz-house-x0'
dotnet $taskCli validate $taskHouse --json
dotnet $taskCli scene export-usd $taskHouse "$taskOutput/house.usda" --json
dotnet $taskCli scene export-glb $taskHouse "$taskOutput/house-enclosed.glb" --repeat 2 --json
dotnet $taskCli scene export-glb $taskHouse "$taskOutput/house.glb" --hide-boundary main.ceiling --hide-boundary main.southWall --hide-boundary main.eastWall --hide-boundary hall.ceiling --hide-boundary hall.southWall --hide-boundary hall.eastWall --json
dotnet run scripts/measure-archviz-house.cs -- $taskHouse $taskOutput
blender --background --factory-startup --python-exit-code 1 --python scripts/render-archviz-house.py -- "$taskOutput/house-enclosed.glb" $taskOutput
dotnet $taskCli presentation compile fixtures/Canonical/Scene/WarmModernHouse/house-presentation.json "$taskOutput/house.pptx" --json
node scripts/validate-presentation-glb.cjs artifacts/local/presentation-3d-x0/tools/node_modules/gltf-validator "$taskOutput/house.glb" "$taskOutput/house-enclosed.glb"
# In the existing OpenUSD Python environment, with pxr available:
python scripts/validate-archviz-house.py $taskOutput
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter 'Category!=SlowCorpus'
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

The existing validator script writes the two `*.validator.json` files consumed
by the external script. Run the existing OpenUSD `usdchecker`
against the complete USDA. The existing `capture-usd-export-x0.py` can capture
usdview with `USD_X0_FRAMES=0`, a local capture directory, and the authored Hero
camera path from `external-validation.json`. Desktop PowerPoint opening and
interaction are direct observations, not inferred from package structure.

## Known simplifications

Single storey; no staircase, roof design, curtains, mullion system, appliance
internals, tiny hardware, simulated upholstery or exterior landscape. Doors are
semantic open passages, without leaves/hinges. Sink/cooktop and decorative
objects are deliberate stylized placeholders. Glass uses portable opacity;
Cycles supplies a simple dielectric finish. Floor boards, subtle grain/fabric
bump and seven presentation lights are shader/lighting detail over compiled
geometry. GLB/PowerPoint uses authored scalar PBR rather than those procedural
textures, so it matches layout, silhouettes and palette but not Cycles lighting.
No IFC, BIM, MEP, structural analysis, general scripting or runtime mutation.

The house is intended as a scene-authoring showcase, not construction
documentation or a photorealism benchmark.
