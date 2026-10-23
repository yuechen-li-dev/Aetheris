# CIR-DISPLAY-X0

Status: **Meaningful progression**, 2026-10-05. The three identified shader-stack
blockers are resolved and sphere, cylinder, cone, torus and a CSG cut render on a
real browser GPU through the requested stack. Full Cadmata/Helios X0 acceptance
is not claimed. This release-report location follows the mission's explicit path.

## What CIR mathematically guarantees today

The audited display candidate is `Aetheris.Continuum/Backends/Sdf`, not every
implementation of the broader CIR occupancy interfaces. `SdfNodes.cs` implements
centered box, finite capped cylinder, finite capped cone/frustum, sphere and torus;
union is min, intersection is max and subtraction is max(left, -right).
`SdfTapeLowerer` flattens these into primitive evaluations and min/max/negation,
with accumulated inverse transforms in the existing row-vector convention.

For valid dimensions, the five primitive formulas are Euclidean signed distances
(ring/horn torus admission requires major radius >= minor radius). They are
1-Lipschitz. Rigid transforms preserve this bound. Min/max/negation preserve the
1-Lipschitz bound and occupancy sign, but generally not exact distance throughout
CSG interiors. These are sufficient for conservative outside distance stepping
in exact arithmetic, not a promise of exact engineering measurements from CSG
field values. The broader `IImplicitFieldCapability` does not provide a universal
distance or Lipschitz promise.

`SdfCapabilityAnalyzer` advertises sign, intervals, exact primitive distance and
gradient capability. It removes exact-distance capability for CSG and non-rigid
transforms. It does not expose a quantitative Lipschitz bound. Non-rigid
`SdfTransformNode` evaluates its child at the inverse point without distance
rescaling; blindly advancing a world ray by that value is unsafe. Display
admission therefore rejects non-rigid fields with `cir-display-transform-not-rigid`.

`SdfContinuumRegion.TryGradient` uses six central finite differences with h=1e-6
and returns a normalized direction. It is not automatic differentiation and is
not an unnormalized derivative suitable for unrestricted Newton refinement.
Gradients can fail at singularities and CSG seams. Bounds come from the existing
nodes; intersection currently uses the union box, which is conservative but loose.
No Fidget dependency, Fidget import or Fidget AD implementation was found in the
current C# tree. Historical lineage is not evidence for additional guarantees.

## What the shader pipeline actually supports today

The practical frontend is implemented: Copeland TS syntax/parser and
`GpuGraphicsBinder` emit frontend-neutral VD-MIR. `VdMirGraphicsHlslEmitter` and
`VdMirGraphicsBackend` produce HLSL and invoke DXC; `spirv-val` validates the
result. The old Aurelian SDSL-V AST lane also exists, but this work uses `*.v.ts`.
No Oct frontend port is needed. Current Oct pixel-output validation rejects
builtins, so fragment depth is a new documented Visual TypeScript extension.

Before this work, the graphics binder allowed locals, returns and restricted
branches, but no traversal loops, local mutation or numeric comparisons. Pixel
outputs were color targets only. DXC emitted Vulkan 1.3 SPIR-V, with no WGSL
conversion. DXC emits DXIL/SPIR-V rather than browser WGSL; browser shader modules
require WGSL. Sources: [DXC](https://github.com/microsoft/DirectXShaderCompiler),
[browser shader-module API](https://gpuweb.github.io/types/interfaces/GPUShaderModuleDescriptor.html).

Implemented now:

```text
CIR nodes -> existing CIR tape -> specialized Field() *.v.ts source
  -> Copeland TS parser/binder -> VD-MIR graphics.m4
  -> existing HLSL emitter -> DXC (Vulkan 1.1 SPIR-V for browser artifacts)
  -> spirv-val -> Naga 27.0.0 -> WGSL -> WGSL validation -> browser WebGPU
```

M4 adds bounded C-style for, typed mutation, scoped branches, numeric comparisons,
boolean logic, negation, break, pixel discard and `@builtin(frag_depth)`.
Depth survives compiled metadata separately from color targets. Loop counters
are readonly inside their bodies; literal bounds permit at most 4096 iterations
per loop. Range/step/descend syntax is deferred because the existing TS parser
already expresses the required traversal. Naga is an external translator, not a
replacement compiler or a handwritten WGSL renderer. The browser bridge preserves
clip coordinates with `--keep-coordinate-space`.

## CIR lowering, ownership and minimum remaining glue

`CirVisualTsLowerer` owns the one CIR-to-Visual-TS operation mapping in Aetheris.
It specializes tape instructions into straight-line typed source and preserves
node bounds. Sphere, cylinder, cone, torus, box, rigid transforms and min/max CSG
are admitted. Non-finite/invalid dimensions, spindle toruses, non-rigid fields and
unsupported nodes receive explicit fallback reasons. The X0 dimension range is
1e-5 to 1e6 in model units; this is a bounded qualification range, not support for
arbitrary CAD scales. Production source does not reference a Copeland checkout.
The qualification script takes the checkout and translator paths explicitly.

Copeland owns frontend syntax/binding and VD-MIR. Aurelian.Shaders owns HLSL,
DXC and the WGSL artifact bridge. Aetheris owns CIR mapping and future hybrid
admission. Three/Cadmata owns scene cameras, occurrence transforms, surface passes,
mesh fallback, overlay and selection projection. BRep remains engineering/topology
authority; STEP remains interchange authority.

The next isolated integration boundary is the display packet: SDK `index.d.ts`
exposes only `DisplayMeshDefinition` and `DisplayMeshOccurrence`; Web.Runtime
`Program.cs` tessellates BReps; Cadmata `AetherisViewport.tsx` uses React Three
Fiber Canvas; the Helios demo explicitly constructs `THREE.WebGLRenderer`.
Neither packet exposes admitted CIR programs nor does either viewport own a
WebGPU surface pass. Completing this requires a shared definition-level CIR
descriptor (bounds, program identity, appearance and stable engineering identity),
occurrence-only transforms and a shared depth attachment/pass lifecycle. It must
retain existing mesh picking proxies and BRep edge overlays. No surface meshes
are hidden or replaced before this contract and depth ownership are implemented.

The structural hash includes the specialized field and a compatibility version.
Equivalent definitions have equal identities; different dimensions do not alias.
There is no production GPU program cache yet. This is a cache key and sharing
seam, not a claim of measured occurrence cache hits. The witness compiles five
definitions once each; it does not exercise assembly instance reuse.

## Traversal and numerical boundary

The witness uses orthographic local rays, a 3x3x3 box enclosing its rotated
definitions, 256 steps maximum, absolute hit epsilon 1e-5, and 0.9*abs(field)
steps. Ray work is limited to this fixture box; it is not an unbounded world
post-effect. Correct depth for this orthographic witness is t/3. The GPU writes
actual hit depth through `SV_Depth`/`frag_depth`; no BRep modeling tolerance is
reused. Normals use central field differences with h=1e-4 and a 1e-8 denominator
floor. No tessellation normals are used.

The margin is an X0 numerical precaution, not a floating-point proof. Maximum
iterations can miss grazing rays; fixed epsilon is not screen-space termination.
There is no Newton/bisection refinement, perspective camera proof, arbitrary
world normal-transform proof, or extreme-zoom acceptance yet. General rendering
must intersect authoritative per-object bounds and transform rays/normals using
occurrence matrices; the small witness is not that renderer implementation.

## Evidence and reproduction

```powershell
# In Copeland, requires Cargo plus the existing DXC/SPIR-V toolchain:
./scripts/install-webgpu-compiler.ps1
dotnet test tests/Aurelian/Aurelian.Shaders.Tests -c Release -m:1

# In Aetheris, pass the actual installed paths explicitly:
./scripts/qualify-cir-display-x0.ps1 -CopelandRoot <Copeland-checkout> -NagaExecutable <naga-executable>
python -m http.server 8765 --bind 127.0.0.1 --directory artifacts/local/cir-display-x0
# Open http://127.0.0.1:8765 in a WebGPU-capable browser.
```

Generated source, HLSL, WGSL, metrics, browser readback report and screenshot live
under ignored `artifacts/local/cir-display-x0/`. The browser verifies shader
compilation, pipelines with a depth attachment, draw validation and nonempty
surface pixel readback for each primitive and the CSG cut. Edge 153, Windows,
NVIDIA Ampere adapter, DXC 1.9.2602.24 (d355aa836), Naga 27.0.0 qualified locally.
Browser privacy output did not expose the exact GPU model.

Initial normal-view readbacks: sphere 22,864 pixels; cylinder 25,868; cone 21,566;
torus 16,416. Generation was 0.66–18.44 ms, two-stage DXC 78.57–113.11 ms and
Naga conversion/revalidation 90.42–126.98 ms for these four definitions. First-run
browser pipeline+draw+readback was 34.1–56.4 ms. These include startup/compilation
and readback overhead and are not frame time or GPU timestamp measurements.
No mesh quality/performance comparison or universal benchmark claim is made.

Final validation: Aetheris Release solution build completed with zero errors;
Core fast lane passed 1,005 tests and the serial full solution lane passed 4,312
tests across 20 projects, with zero failures/skips. Copeland TS passed 1,375
tests; Aurelian's full solution lane passed 815 tests across 22 projects; the
final shader suite passed 151 tests after the additional boolean-literal test.
The final cleanup-focused lane passed 12 implicit graphics tests. CIR lowering
tests passed inside the Aetheris full lane. Raw commands/logs remain under
`artifacts/local/cir-display-x0-*.log`. Both repository diff-whitespace checks
passed. Test-generated tracked STEP line-ending changes were restored; pre-existing
factory-scene work was preserved.

## Friction log and cleanup review

| Boundary | Permanent owner | Resolution | Remaining qualification |
| --- | --- | --- | --- |
| Traversal loop, comparison and mutable locals absent | Copeland TS/VD-MIR | Graphics M4 implemented using existing parser | Optional Oct-style range grammar deferred |
| Fragment depth absent in TS and current Oct pixel output | Copeland binding, Aurelian emitter/contracts | Typed frag_depth + SV_Depth + separate metadata implemented | Mixed mesh/CIR depth orders in CAD viewport |
| DXC output not browser shader source | Aurelian.Shaders | DXC SPIR-V -> pinned Naga -> validated WGSL | Broader SPIR-V capability compatibility |
| Vulkan 1.3 discard is unsupported by Naga 27 | Aurelian DXC browser target | Browser-only Vulkan 1.1 selection; Vulkan default retained | Native/broader shader corpus |
| General CIR field lacks global distance promise | Aetheris CIR display admission | Valid primitive/rigid/CSG whitelist; affine rejection | Quantitative affine Lipschitz support deferred |
| SDK carries only mesh, renderers own WebGL surfaces | Aetheris display packet + shared renderer | Boundary isolated; existing display preserved | CIR descriptors, WebGPU pass and hybrid dispatch |
| Selection and Helios topology edges are mesh-based | Existing selection/overlay projection | No changes to picking or topology authority | Keep proxies and qualify overlay alignment |

Dedicated code review removed flattened lexical scopes, preserved immutable
material diagnostics, separated color/depth metadata, versioned new VD-MIR
features, bounded translator processes and cleanup, and kept all CIR primitive
mapping out of the browser. There is no shader VM, duplicated compiler, generated
tracked shader output, production sibling-path assumption or shader cache claim.

## Executive verdict

**Meaningful progression.** CIR -> practical Visual TS -> VD-MIR -> HLSL -> DXC
-> translated WGSL -> real browser GPU now works for all four target primitives.
The motivating CAD display has not yet changed: shared viewport consumption,
hybrid fallback, occurrence program cache, mixed depth, selection/topology,
side-by-side mesh comparison, close-up, real-part and assembly witnesses remain
unqualified. These are explicit acceptance gaps rather than compiler blockers.
