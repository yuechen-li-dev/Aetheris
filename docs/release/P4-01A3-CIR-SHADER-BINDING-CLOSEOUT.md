# P4-01A3 — CIR shader artifact binding closeout

**Verdict: Accepted for Preview 4 scope, with experimental CSG qualification deferred.**
The `shader-artifact-not-bound` connector is
repaired in both real products. Normal authored primitives, a locating pin,
mixed Assembly and Scene now submit compiler-generated WGSL through FieldPass.
The remaining previously qualified simple CSG specimen uses historical V1 input;
canonical in-memory compilation refuses it with `firmament-v2-source-required`.
Its retained root lowers and binds successfully in compiler tests. No V1 browser
firewall was weakened and no new CIR coverage was invented.

**P4-01A rerun: Accepted for Preview 4 scope, with experimental CSG qualification deferred.**
Automatic primitive display is no longer blocked by shader binding. Experimental
CSG product qualification remains open outside Preview 4 acceptance and no longer
blocks P4-01B or distribution work. Those steps have not been completed here.

## Exact seam and ownership

The initial trace reached `FirmamentStepExportResult.Cir`, native Assembly
`DefinitionCir`, browser definition `cir`, and Cadmata's canonical `display`
packet. All contained qualification, structural identity, typed field source and
bounds. None supplied an executable artifact to `fromDisplayMesh`; products made
visual meshes and never submitted a field. This was the precise missing connector.

| Stage | Current owner/data |
|---|---|
| Authored source | Normal `Basics/cylinder.firmament`, `BareCylinder.Base`, radius 12 mm, height 30 mm |
| Existing CIR creation | Primitive executor/native geometry mirror retains its evaluable root beside BRep |
| A1 retention | `FirmamentCirRetention`: stable definition and structural identity, compiler-lowered typed field, conservative bounds; runtime AST omitted from JSON |
| Compiled definition | Native Part `Cir`, Assembly `DefinitionCir`, Scene's shared definitions |
| Display projection | `AssemblyDisplayMeshExporter`, `DisplayProjection`, Web Part mesh builder and native Part service resolve the same provider |
| Artifact generation | `CirShaderArtifactProvider` invokes existing managed `WgslGraphicsBackend.Compile`: Copeland frontend → VD-MIR → direct WGSL |
| Transport | Normal native JSON and development WASM SDK/Worker definition `shader` contains artifact, entrypoints, bindings, source/compiler identity and state |
| Product definition | Cadmata canonical `display`; Helios `ModelSession.mesh`; no product registry |
| Field construction | Shared `fromDisplayMesh` checks qualified CIR, matching artifact source identity and retained bounds; carries resolved material/transform/engineering identity |
| GPU ownership | Telos validates modules/pipelines, owns bind groups, attachments, resource cache and lifetime |
| Actual FieldPass | Compiler pipeline `draw(6)` and field submission counts; no visual mesh surface for accepted field definitions; invisible BRep picking proxy retained |

The production typed field source is
`Aetheris.Kernel.Firmament/Display/telos-field.v.ts`. The earlier standalone
fixture was moved to this owner and its qualification script consumes that file.
There is no handwritten primitive WGSL, copied shader compiler, frontend CIR
interpreter, witness import, debug registration or per-product artifact provider.

## Generation, contract and caching

Generation is **lazy at display projection**, not STEP compilation or rendering.
The existing retained compiler payload is sufficient; artifacts cross transport
as deterministic WGSL plus ordinary metadata. Native and browser use the same
managed backend. Browser execution has no DXC, Naga, subprocess or native shader
compiler requirement.

The artifact schema is `aetheris/display-shader/1`. Identity combines the retained
structural identity, managed backend/compiler version, shared typed template
contract and compiled semantic program hash. It does not depend on occurrence,
filename, pointer identity or emitted WGSL formatting alone. A synchronized FIFO
cache holds at most 256 generation results, including failures. Scene projection
reuses an existing binding instead of resolving it twice. Equivalent definitions
and occurrences reuse one artifact; appearance/placement changes do not specialize
the program.

The managed dependency uses exact package version
`0.1.0-preview.1-a3.7329cf8ff04d504a`, with reviewed producer source hash
`7329cf8ff04d504a062e8b5f62adcb9e12a1bbd13159c9c0af42b94576ba38d9`.
`scripts/prepare-managed-wgsl.ps1 -CopelandRoot <explicit checkout>` verifies that
source graph and packs the existing projects into an ignored local feed. This is
a build prerequisite for a fresh checkout, not a production sibling-project
reference, vendored source tree or new package service. Copeland source was not
edited. Production AOT/distribution remains a separate lane.

GPU cache keys retain semantic artifact identity, attachment formats, sample
count, entrypoints and drawing/transparency state. Module identity collisions are
rejected. Geometry changes release old draw buffers and drop obsolete field
module/pipeline references; those GPU objects have no explicit destroy API.
Material and transform changes retain program state.

## Auto, material and failure behavior

Qualified CIR plus matching artifact produces a field automatically. Unsupported
geometry, generation failure or missing/mismatched transport produces the owned
mesh. Product fields keep that visual mesh while asynchronous GPU validation is
pending. Module/pipeline rejection emits `shader-artifact-gpu-failed` and preserves
mesh visibility and engineering picking; it does not install a fatal viewport
overlay. Non-rigid product occurrences retain mesh fallback.

The field receives the same resolved base colour, roughness, metallic and opacity
as the mesh. `telos-field-rays/2` adds metallic at vertex location 6 (76-byte
vertices), retaining the canonical 32-byte tint/opacity/roughness uniform. The
prior rays/1 ABI is still supported. The typed shader now uses roughness and
metallic in bounded lighting. Its diffuse/specular and square-root display encoding
are approximations; pixel-identical mesh lighting, PBR, emission realization and
advanced/intersecting transparency are not claimed. Current house panes remain
on the already-supported mesh blending path. TAA was not revised.

Developer inspection exposes definition/binding state, shader identity, actual
field and visual mesh counts, separate picking proxies, submitted field draws
and GPU cache counts through `inspectDisplay()` and canvas data attributes. It
distinguishes compiler qualification/binding, generation failure, missing
transport, pending GPU state and GPU rejection without dumping WGSL.

Two adjacent issues were isolated and corrected during real product qualification:

- Cadmata's existing Refresh Display Data action was disabled for normal source
  startup. It now recompiles the owned source path and preserves valid occurrence
  selection, while keeping the existing body refresh path.
- Shared Assembly/Scene face ranges used a definition ID as a semantic entity ID.
  They now retain BRep face IDs with a null feature entity where no such binding
  exists. The SDK's existing occurrence resolution selects the actual engineering
  occurrence. Runtime-only faces do not acquire fabricated source selectors.

The larger managed dependency also exposed an Edge private-context asset fetch
failure: `Microsoft.CodeAnalysis.CSharp` (6,687,001 bytes) returned
`net::ERR_CACHE_WRITE_FAILURE`, followed by a .NET boot fetch rejection. The shared
development Vite middleware now sends `no-store` and explicit content lengths.
Actual product reruns have no page errors. Production fingerprint caching remains
unchanged; no WASM loader redesign was performed.

## Real Edge product evidence

Every model came from normal Cadmata source startup/API or Helios file input and
Build. Browser instrumentation observed actual draws using the compiler pipeline;
it never supplied field packets or registered shaders. Negative cases deliberately
rejected a real compiler-produced GPU module, leaving compiler/transport data intact.

| Case, both products | Actual fields / visual meshes | Evidence |
|---|---:|---|
| Normal Cylinder | 1 / 0 | One compiler shader module, actual field draws, one invisible proxy; orbit/zoom/pick remain functional |
| Sphere, Cone, Torus | 1 / 0 each | Normal Auto dispatch and real compiler pipeline draws |
| Locating pin, Ø6 × 20 mm | 1 / 0 | Clean nominal mechanical specimen; no threaded-showcase dependency or manufacturing-release claim |
| Mixed Assembly | 2 / 1 | Repeated cylinder definition shares one artifact/module/pipeline; annular plate uses mesh; BRep edges and face identity retained |
| Mixed Scene | 2 / 1 | Same shared binding, authored cameras, stable engineering face/occurrence picking |
| Rejected GPU module | 0 / 1 | Named GPU failure, visible mesh, selection functional, viewport alive in Cadmata and Helios |
| House Scene | 0 / 161 | 94 definitions / 254 total hierarchy occurrences, authored environment/furniture/materials, three cameras and translucent panes preserved |

Mixed-depth qualification uses two ordinary authored Scene cameras. In both
products the front centre pixel is blue mesh `[40,99,171]`, with the Mesh occurrence
picked; the back pixel is copper CIR `[174,102,63]`, with CylinderA picked. Selection
overlays are cleared through a normal background click before sampling. This
proves both occlusion directions as well as agreement with engineering proxies.

Cadmata's normal refresh remains CIR and does not recreate its shader module.
Helios edits the Cylinder radius **12 → 14 mm** in Monaco and clicks Build:
the artifact identity changes, the field remains active, the occurrence identity
survives and obsolete program references leave the cache. Helios also edits the
Copper colour `.55 → .15` and the repeated occurrence translation **50 → 70 mm**.
Both reuse the original artifact/module; the resolved colour updates and pipeline
counts remain unchanged for placement-only rebuilding. Native Scene tests verify
the same reuse across appearance/placement updates.

## Performance observations

A fresh native diagnostic process measured the first Box artifact at 129.31 ms,
then Cylinder 8.82 ms, Sphere 6.29 ms, Cone 6.44 ms, Torus 5.87 ms and locating pin
5.59 ms. Every immediate repeat was a cache hit with zero regeneration time.
These are backend generation timings, not total product load times.

The final Helios product audit measured Cylinder/Sphere/Cone/locating-pin loads
at 0.86–0.87 s, Torus 1.88 s, mixed Assembly 2.46 s and mixed Scene 1.89 s. The
development/non-AOT house load was **53.485 s**, compared with the previous A2
52.6 s observation. These are single observations under qualification load,
not a performance claim. No retained-compilation or whole-house optimization was
attempted. The bounded reuse win is avoiding duplicate semantic generation and
unnecessary program creation on material/placement edits.

## Validation and cleanup

- Release solution build: zero errors; existing build advisories retained.
- Fast .NET lane: 1,005 passed.
- Full serial .NET lane: **4,323 passed across 20 projects**, zero failed/skipped.
- Focused display projection: 11 passed, including primitives, serialization,
  identity/corruption, repeated occurrences, Scene edits and the CSG route boundary.
- Telos: 12 tests passed; Cadmata: 86 across 18 files; Helios: 29 across 7 files.
- Both frontend builds and development SDK build/install passed.
- Real Edge substrate/direct-WGSL qualification: PASS, zero page/console errors.
- Real product qualification: 16 main witnesses plus the separate Helios negative
  case passed. Normal house/guitar/factory/robot regression captures were refreshed.
- Repository CLI independently inspects Cylinder, locating pin and mixed Scene;
  native compiler audit records shader identity, generation/reuse and exact CSG refusal.
- Ownership/cache/import/lifetime cleanup completed. Source formatting, repository
  layout and whitespace checks passed. Prior viewport/A1/A2 work was preserved.

Raw reports/captures/logs are ignored under `artifacts/local/cir-artifact-binding/`:
`products/`, `products-negative/`, `regressions/`, `native/`, and the named gate logs.
The release document records compact conclusions rather than promoting run dumps.

## Reproduction

```powershell
./scripts/prepare-managed-wgsl.ps1 -CopelandRoot <explicit matching Copeland checkout>
dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
npm --prefix Aetheris.Web.Runtime/telos test
npm --prefix aetheris.client test
npm --prefix aetheris.client run build
dotnet build Aetheris.Server -c Release -m:1
# In the explicitly scoped HeliosCAD checkout: npm run sdk:install; npm test;
# npm run build:fast; npm run dev -- --host 127.0.0.1
node scripts/audit-cir-artifact-products.mts <Playwright module path>
node scripts/audit-display-projection-products.mts <Playwright module path> artifacts/local/cir-artifact-binding/regressions
./scripts/audit-display-projection-closeout.ps1 -OutputDirectory artifacts/local/cir-artifact-binding/native
```

The default current strict product audit also includes the Helios GPU-negative
case; `--failure-only` can isolate it with a separate output directory. Substrate
regeneration uses `prepare-three-telos-cir.ps1`, `build:witness` and
`qualify-three-telos-browser.mts`; it is independent corroboration, not product
acceptance authority.

## Deferred experimental CSG qualification

`fixtures/Compatibility/LegacyV1/Examples/w2_cylinder_root_blind_bore_semantic.firmament`
compiles through its compatibility owner, retains a qualified evaluable CSG root,
and binds through this provider in tests. The same document passed to normal
`CompileSource` fails at `FirmamentV2.CompatibilityFirewall`, including
`firmament-v2-source-required`. Future experimental qualification would need to
exercise that existing CSG through a canonical V2 authored/retained execution route, preserving
BRep authority; allowing historical V1 input in the browser would bypass the
existing language boundary. This is isolated source/retention qualification work,
not another shader registry or renderer patch.

P4-01A is **Accepted for Preview 4 scope, with experimental CSG qualification deferred.**
Threaded wire-only faces,
final visual release qualification, distribution and production AOT were left
outside this milestone.
