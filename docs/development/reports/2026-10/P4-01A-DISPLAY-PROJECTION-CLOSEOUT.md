# P4-01A — Display projection closeout audit

## Current rerun after P4-01A3

**Verdict: Accepted for Preview 4 scope, with experimental CSG qualification deferred.**

Normal authored Parts, mixed Assemblies and Scenes now automatically reach
FieldPass for qualified primitives in both Cadmata and Helios, alongside owned
mesh fallback. One compiler-owned managed WGSL provider supplies versioned
artifacts; no witness injection is required. Actual Edge draws distinguish fields
from visual meshes and invisible engineering proxies. Repeated occurrences share
programs; material/placement edits reuse them; radius edits change artifact identity.
Both products preserve selection and mesh visibility on bounded GPU rejection.
Authoritative Assembly BRep edges now travel with shared definitions, and ordinary
occurrence/face picking agrees with mixed-depth surface pixels in both directions.
The current house, guitar, factory and robot regressions remain functional.

The deferred experimental qualification boundary is the previously qualified simple
CSG specimen's source route: its compatibility-owned retained root lowers and
binds, but normal in-memory compilation rejects the V1 document at
`FirmamentV2.CompatibilityFirewall` with `firmament-v2-source-required`. Canonical
V2 CSG retention/product qualification remains optional future experimentation
outside Preview 4 acceptance; accepting V1 browser input would bypass existing
ownership policy. Runtime-only shared faces
remain runtime-only rather than receiving invented source selectors. Existing
emission/PBR and advanced transparency limitations remain explicit.

See [P4-01A3](P4-01A3-CIR-SHADER-BINDING-CLOSEOUT.md) for the full trace, current
gates, failure cases, cache/material/edit evidence and performance observations.
Experimental CSG no longer blocks P4-01B or distribution work. Those steps and
final visual release qualification have not been completed by this rerun.

## Historical rerun after P4-01A1 and P4-01A2

**A2 verdict: Meaningful progression.** The following records that earlier state;
the shader-artifact and Assembly-edge blockers described here are superseded above.

[P4-01A1](P4-01A1-CIR-AUTHORITY-RETENTION.md) succeeds: normal V2 primitive
compilation retains the existing evaluable CIR beside native BRep, with stable
definition identity and transport-safe admission metadata. Assembly and Scene
definitions carry the retained metadata without duplicating it per occurrence.
Engineering topology remains BRep-owned.

[P4-01A2](P4-01A2-DISPLAY-TRANSPORT-X0.md) succeeds: the shared projector carries
owned Scene environment, resolved materials, cameras, boundaries and bounds
through native/product and SDK/Worker transports. Cadmata and Helios now render
the house from normal source input, rather than refusing it or retaining the
previous cylinder. Cadmata also renders authored guitar and factory materials.

The next exact blocker is **`shader-artifact-not-bound`**. The normal cylinder
packet contains `cir-qualified` metadata, typed field source and structural
identity, but production adapters still draw it through `telos-mesh/1`. Aetheris
has not adopted the existing managed WGSL backend as a production dependency or
bound its artifacts to the existing Telos field ABI. This is no longer a lost
compiler-representation or Scene/material transport problem.

The smallest next repair is a bounded engine-owned artifact provider/cache over
retained CIR, with ordinary versioned backend dependency adoption and stable
definition/structural identity. Automatic field dispatch must then qualify its
BRep picking proxy, mixed-depth, material and selection behavior in the products.
Complete Assembly BRep-edge transport and CIR face(+Z) selection remain open.
Emissive values are transported but not applied by the current mesh shader.
P4-01B threaded faces were not changed.

The rerun preserves the original source witnesses and product workflows. The
robot is an additional regression witness; it has no authored appearance bindings.
Raw packets/UI evidence and visually inspected screenshots are in ignored
`artifacts/local/display-projection-closeout/products-a2/`:

| Witness | Current normal product result |
|---|---|
| Cadmata cylinder | HTTP 200, 1 definition / 1 occurrence; one retained qualified field, explicit mesh fallback |
| Cadmata guitar | HTTP 200, 56 / 171; 99 resolved material bindings render |
| Cadmata house | HTTP 200, 94 / 254; 161 materials, 3 translucent bindings, 3 cameras; Hero interior renders |
| Cadmata factory | HTTP 200, 124 / 2,562; 1,492 materials, 2 translucent bindings, 5 cameras; Hero view renders |
| Cadmata robot | HTTP 200, 24 / 64; existing Assembly rendering preserved |
| Helios cylinder | Worker compile succeeds, 1 / 1, zero diagnostics, mesh pipeline |
| Helios house | Explicit multi-file Worker compile succeeds, 94 / 254, zero diagnostics; authored palette and Hero interior render |

The final development/non-AOT Helios house build took 52.6 seconds (48.8 seconds
reported compile, with 9.4 seconds of display preparation included in that phase).
Release performance is not qualified. Simple pane opacity uses bounded mesh
blending, not OIT/refraction research. Native tests prove Room-finish, placement
and camera edits retain engineering definition revisions; product GPU reuse for
those edits is not measured here. Shared WebGPU controls still qualify mixed
field/mesh depth, AA transitions, resources and disposal; they do not substitute
for automatic product CIR evidence.

Consolidating display exposed imported OCCT regression cases. The shared exporter
now honors existing external-STEP provenance and bounded mesh behavior, including
explicit diagnostics for unmeshed imported faces. Authored patch validation stays
strict; no filled replacement geometry or source-shape inference was added.

### Rerun reproduction

```powershell
pwsh -File scripts/audit-display-projection-closeout.ps1 -OutputDirectory artifacts/local/display-projection-closeout/compiler-a2
# In HeliosCAD: npm run sdk:install; npm run dev -- --host 127.0.0.1
node scripts/audit-display-projection-products.mts <playwright-module-path> artifacts/local/display-projection-closeout/products-a2
dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
npm --prefix Aetheris.Web.Runtime/telos test
npm --prefix Aetheris.Web.Runtime/telos run build:witness
node scripts/qualify-three-telos-browser.mts <playwright-module-path>
npm --prefix aetheris.client test
# In HeliosCAD: npm test; npm run build:fast
```

### Rerun validation

- Release solution build: passed, zero errors; existing nullable and WASM SQLite warnings remain.
- Fast kernel lane: 1,005 passed, zero failed/skipped.
- Full serial solution lane: **4,320 passed across 20 projects**, zero failed/skipped.
- Eight added compiler/transport regression cases pass; existing imported OCCT
  server cases now also check canonical transport and explicit unmeshed-face diagnostics.
- Telos build/typecheck and tests: 11 passed. Shared Edge/WebGPU qualification:
  `PASS`, zero GPU/page errors; mixed depth, AA transitions, resource reuse and disposal checked.
- Cadmata: production build passed, 86 tests across 18 files passed.
- Helios: refreshed local SDK and development build passed, 29 tests across 7 files passed.
  Queued source revisions are checked with their matching project resource snapshots.
- Native contract audit and repository CLI house/factory `scene inspect --repeat 2 --json`: passed.
- Final real-product audit completed with the results above; captures were inspected.
- Repository layout guard and diff whitespace checks passed. Test-generated STEP
  line-ending metadata was restored only after confirming no substantive diff.
- Temporary product hosts were stopped and the Helios audit port is closed.

Logs use the `a1a2-` prefix under ignored
`artifacts/local/display-projection-closeout/`; compiler contracts are under
`compiler-a2/`, product captures under `products-a2/`, and shared GPU qualification
is `artifacts/local/three-telos/browser-report.json`. The Telos test log is
`a2-telos-tests.log`. Earlier viewport polish edits were preserved. No commits,
background services or startup hooks were created.

The original audit follows as historical evidence; its absent contracts and
failed product views describe the state before A1/A2, not current behavior.

## Initial audit (before A1/A2)

**Verdict: Honest stop. Preview 4 display projection is not accepted.**

Normal Cadmata and Helios workflows do not yet automatically project Parts,
Assemblies and Scenes into one canonical Telos scene with qualified CIR and
authored materials. The audit isolates missing retained compiler data and
duplicated projection ownership below the product adapters. Adding a frontend
primitive detector or an empty canonical packet would conceal those boundaries.
The polished mesh path remains intact. This change adds reproducible diagnostics
and corrects documentation; it does not claim a production projection repair.

## Current ownership and normal paths

| Input | Existing authority / normal path | Where projection stops |
|---|---|---|
| Authored V2 Part | `FirmamentBuildAndExport.CompileSource` → `FirmamentStepExportResult` → Web runtime BRep/STEP reimport → `WebMeshBuilder` → SDK mesh → Helios Telos adapter | Result has no retained CIR root/program/admission contract. Cadmata startup classifies `.firmament` as Assembly, including ordinary Parts. |
| BRep-backed Part | Compiler `RuntimeBody` and `RuntimeCorrespondence`, when supplied | Box retains both; cylinder supplies neither and uses STEP reimport. Engineering correspondence must survive independently of display dispatch. |
| Analytic CIR | Legacy `FirmamentCompilationArtifact.PrimitiveLoweringPlan` → `FirmamentCirLowerer`; existing `SdfNode` → `CirVisualTsLowerer` → direct WGSL backend → Telos field | Legacy compiler rejects the current V2 Box and Cylinder sources. Standalone SDF roots are diagnostic controls, not normal authored compilation. |
| Assembly | `AssemblyM1Pipeline` / `FirmamentAssemblyDocumentCompiler` retain engineering definitions, occurrences, transforms and resolved appearances | `AssemblyExecutedGeometry` exposes artifact and BRep dictionaries, without retained authored CIR or V2 correspondence. Shared mesh exporter and Cadmata face-patch service produce different packets. Neither carries complete material/field/topology data. |
| Scene | `FirmamentSceneSession` → `CompiledScene` | Neither product invokes this owner for Scene source. SDK has Part/Assembly dispatch and no Scene/project-resource input. |
| Room / walls / floor / ceiling | Scene owner produces environment definitions and boundary identities | Geometry exists upstream; product display cannot carry it. Do not reproduce panels in React. |
| Door / Window | Scene compiler cuts openings and produces frame/pane finish geometry | Product Scene dispatch absent; pane opacity/frame and glazing appearance absent from transport. |
| Scene display geometry | `CompiledScene.Display`, `Nodes`, `Boundaries`, world bounds | USD/GLB exporter projection consumes it, including cutaway filtering; no shared Telos projection exposes these semantics. |
| Appearance / occurrence override | Compiler-resolved `Appearance` and `Preview` values; Scene `Appearances` | Guitar has 99 authored bindings, including `with-appearance-override`; Cadmata display packet omits them. Theme material is used instead. |
| Imported STEP | Existing BRep import, tessellation, topology and semantic/PMI service | Mesh remains the safe path. No imported analytic-surface guess constitutes CIR admission. |
| Recovered/spline geometry | Existing bounded surfacing/mesh pipelines | Remains mesh fallback; generic implicit recovery is outside this milestone. |
| PMI / topology / selection | BRep edge provenance, semantic correspondence and existing pick services | Web Part mesh retains engineering ranges/edges where available; assembly face-patch transport lacks equivalent edge projection. CIR proxies/PMI anchors cannot be manufactured from display triangles. |

The Web runtime's `FirmamentFieldProjector` projects editable Box/Hole **source
properties** and parser spans. It is not an implicit field/CIR provider.

Relevant implementation seams are `Aetheris.Web.Runtime/Program.cs`,
`Aetheris.Web.Runtime/sdk/src/index.d.ts`,
`Aetheris.Kernel.Firmament/FirmamentStepExportResult.cs`,
`Aetheris.Kernel.Firmament/Assembly/AssemblyM1Execution.cs`,
`Aetheris.Kernel.Firmament/Assembly/AssemblyDisplayMeshExporter.cs`,
`Aetheris.Kernel.Firmament/Scene/SceneCompilation.cs`,
`Aetheris.Kernel.Firmament/Scene/SceneExport.cs`,
`Aetheris.Server/Api/AssemblyDisplayService.cs`, and
`aetheris.client/src/viewer/cadmataTelos.ts`.

## Exact missing contract

The normal V2 export result contains STEP and feature inspection data, with
optional BRep and correspondence. It does not retain a compiler-admitted field
definition bound to the exported engineering definition. The assembly
materializer retains BRep/semantics/export artifact but does not retain that
field representation or V2 topology correspondence in `AssemblyExecutedGeometry`.
Consequently, adding fields to SDK JSON alone cannot supply their authority.

The native audit proves this on `fixtures/Canonical/Basics/cylinder.firmament`:
normal compilation succeeds, but `RuntimeBody` and `RuntimeCorrespondence` are
absent. Both current Box and Cylinder sources fail in the legacy CIR-capable
compiler with:

```text
[FIRM-PARSE-0001] Firmament source must be valid canonical TOON-style text or JSON with an object root.
```

The existing cylinder SDF control lowers successfully and produces structural
identity `da4151e90e22f2e4f112260e722a0ac155dc15a4abd51c1c352857c80a5c07cd`.
That establishes a usable lowering seam, not a safe link from this authored
document to that field. Re-parsing source in a product, inferring primitives
from tessellation, or routing V2 through a legacy compatibility reader would
create a competing geometry interpretation.

Direct managed WGSL emission already exists; a native compiler subprocess is
not the fundamental blocker. `scripts/qualify-three-telos-cir.cs` demonstrates
the existing SDF/v.ts/direct-WGSL chain. The sphere, cylinder, cone, torus and
bounded CSG witnesses remain standalone qualification evidence. Production
adapters still return `fields: []`; no automatic CIR, mixed product Assembly,
shader artifact reuse or CIR face-selection acceptance is claimed here.

## Scene and material evidence

The native Scene compiler already retains the required spatial semantics.
These measurements are from a fresh session followed by an unchanged compile
in the same session; they are CPU compilation measurements, not GPU frame times.

| Native compiled witness | Definitions / occurrences | Environment or window-finish occurrences | Resolved looks / translucent looks | Cameras | Retained engineering definitions |
|---|---:|---:|---:|---:|---:|
| WarmModernHouse | 94 / 254 | 57 | 161 / 3 | 3 | 49 reused, 0 rebuilt |
| FactoryX0 | 124 / 2,562 | 38 | 1,492 / 2 | 5 | 92 reused, 0 rebuilt |
| Industrial Atlas Assembly | 24 / 64 | — | 0 / 0 | — | Not measured here |
| GuitarX0 Assembly | 56 / 171 | — | 99 authored bindings | — | Not measured here |

House first compile: 603 ms; display preparation 143 ms initially / 137 ms
retained. Factory first compile: 2,089 ms; display preparation 293 ms initially /
234 ms retained. Existing engineering definition reuse works; display preparation
still runs. This does not prove bounded GPU invalidation after appearance,
placement or geometry edits.

House bounds are `[-100,-100,-100]` to `[11700,7100,3200]` mm;
factory bounds are `[-180,-180,-180]` to `[42180,28180,7180]` mm.
Authored camera positions, targets and FOV are retained in the audit JSON.
`SceneExport` applies validated hidden-boundary projection and resolved looks
for USD/GLB. Those exporters are not evidence that either product displays them.

The house references six sibling Assembly files. The normal SDK compile input
has no project-resource snapshot; native `FirmamentSceneSession` resolves files
through its existing filesystem path. Browser integration needs an explicit
resource input bound to the existing project authority, rather than hardcoded
house component loading. Existing `FirmamentProjectSnapshot` is a reuse seam;
Scene does not currently accept it.

Industrial Atlas has no authored appearance bindings in this fixture. Its
uniform product material is therefore not proof that a robot palette was lost.
The guitar's physical defaults and Sunburst occurrence override provide the
actual material-loss witness. Mesh/CIR appearance parity and normal glass
presentation remain unqualified.

## Real product checks

The diagnostic opened repository sources through actual Cadmata startup/API
and Helios file input in Edge with WebGPU, at 1600×1000 CSS pixels and DPR 2.
It injected pipeline-label instrumentation only, never model/render packets.

| Product / source | Observed result |
|---|---|
| Cadmata cylinder | HTTP 400: `assembly-parse-missing-root: Expected 'Assembly Name { ... }'.` No model surface pipeline. |
| Cadmata guitar | HTTP 200, 56 definitions / 171 occurrences. Packet definition has `stableId`, `definitionIdentity`, `facePatches`; no appearance or field artifact. Actual view is uniformly themed mesh. |
| Cadmata house / factory | Same Assembly-parser refusal as cylinder. No Scene surface display. |
| Helios cylinder | Normal worker compile succeeds, 1 definition / 1 occurrence, zero diagnostics; `telos-mesh/1` pipeline, no field pipeline. |
| Helios house | Build fails with two diagnostics and `MODEL OUT OF DATE`; previous cylinder remains visible at display revision 1 while source advances to 2. This is failure-retention evidence, not a house view. |

Screenshots and packet/UI evidence are under ignored
`artifacts/local/display-projection-closeout/products/`:
`cadmata-cylinder.png`, `cadmata-guitar.png`, `cadmata-house.png`,
`cadmata-factory.png`, `helios-cylinder.png`, `helios-house.png`, and `audit.json`.
No accepted house, factory, automatic CIR mechanical, robot material or mixed
Assembly screenshot set exists. Prior robot/mechanical evidence in
`VIEWPORT-FINISH-X0.md` remains historical and does not close these criteria.

## Friction log and smallest permanent repair

| Old path / gap | Correct owner and smallest repair | Safe fallback / deferred boundary |
|---|---|---|
| V2 export discards admitted field structure | Retain optional CIR eligibility/root plus definition identity at V2 definition construction; carry it through assembly materialization. Reuse existing CIR lowering and direct managed WGSL emission. | Keep existing BRep mesh. Do not infer fields from imports. |
| Several mesh/export/witness packet builders | One engine-owned, product-neutral projection over retained compiled definitions/occurrences, with adapters only at transport/backend boundaries. | Preserve existing working packets until the shared replacement qualifies. |
| Part/Scene treated as Assembly or Part-only SDK input | Dispatch on existing compiled document kind and provide project resources to the existing compiler owner. | Actionable refusal and last valid display; no Scene reconstruction. |
| Resolved appearances omitted | Carry compiler-resolved defaults/occurrence overrides through the shared projector; use the existing material conversion and one fallback policy. | Current theme/default mesh material; parity remains open. |
| Scene cameras, boundaries, opacity omitted | Project `CompiledScene` data, including validated cutaway visibility, environment identity, bounds and camera metadata. Telos retains camera/render authority. | No product Scene acceptance until normal source loading works. |
| Engineering selection not bound to field admission | Retain BRep/correspondence alongside CIR; automatically project invisible picking proxy and semantic edge overlays under the same identities. | Existing mesh picking/PMI; no display-triangle authority. |
| Retained compilation does not imply retained projection | Key projection/shader artifacts by existing definition/revision and structural identities; occurrence changes update occurrence state. | No new giant cache; appearance/transform edit qualification deferred. |

The proposed definition contract should retain engineering identity, optional
admitted CIR representation, BRep/topology correspondence, mesh fallback, bounds
and resolved default appearance. Occurrences retain identity, parent/world
transform and resolved override. A shared projector then emits deterministic
field/mesh admission diagnostics, shader identities and artifacts, topology and
selection proxies, Scene environment and cameras without duplicating payloads.
These are proposed repairs, not new APIs shipped by this audit.

No new renderer, IR, language, implicit recovery, TAA, transparency research,
distribution work or P4-01B threaded-face repair was introduced. No witness-only
provider was promoted into production. The cleanup pass identifies duplicate
owners and corrects stale documentation; deleting working paths before their
replacement qualifies would regress the products.

## Reproduction and validation

```powershell
pwsh -File scripts/audit-display-projection-closeout.ps1
# Start the actual Helios development app on 127.0.0.1:4173 first.
node scripts/audit-display-projection-products.mts <playwright-module-path>
dotnet build Aetheris.slnx -c Release -m:1 --nologo -v:q
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
npm --prefix Aetheris.Web.Runtime/telos test
npm --prefix aetheris.client test
# In the explicitly scoped HeliosCAD checkout:
npm test
```

The repository CLI independently passes `scene inspect --repeat 2 --json` for
both house and factory; results are `house-cli.json` and `factory-cli.json`.
The compiler diagnostic emits `compiled-contracts.json` into ignored
`artifacts/local/display-projection-closeout/`. Both scripts are standalone
diagnostics without production callers. Product audit records observed failures;
a successful diagnostic exit does not mean milestone acceptance.

Validation completed:

- Release solution build passed: zero errors, two existing WASM SQLite warnings.
- Fast kernel lane: 1,005 passed, zero failed/skipped.
- Full serial solution lane: **4,312 passed across 20 projects**, zero failed/skipped.
- Telos build/typecheck and tests: 10 passed.
- Cadmata frontend tests: 86 passed across 18 files.
- Helios frontend tests: 29 passed across 7 files; existing CSS parser advisories remain.
- Native compiler audit and both repository CLI Scene inspections passed.
- Actual product audit completed; the recorded refusals and mesh-only behavior
  are expected diagnostic findings, not acceptance passes.
- Repository layout guard and diff whitespace checks passed. Test-generated
  STEP line-ending metadata was restored after confirming no substantive diff.
- Temporary product hosts were stopped; the Helios audit port is closed.

Logs remain under `artifacts/local/display-projection-closeout/`. There are no
new production code edits for P4-01A; earlier VIEWPORT-FINISH-X0 changes in both
checkouts were preserved. No commits, background services or startup hooks were
created.

**Executive answer: No.** The retained compiler/transport prerequisites above
remain blockers. The next repair must retain authored field and engineering
correspondence at their compiler owner and feed one shared projector, before
Preview 4 can move on to P4-01B and final visual requalification.
