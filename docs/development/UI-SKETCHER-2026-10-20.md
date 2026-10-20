# Profile workspace: first implementation and qualification

## Result

Helios now has a source-backed 2D Profile workspace. Its compiler service resolves
the existing engineering Profile, exposes driving dimensions, and prepares ordinary
structural source edits. The public SDK transports that projection; shared Telos
renders it; Helios applies edits through its existing undo/build/draft transaction.
There is no second saved sketch model, expression evaluator or constraint solver.

The motivating workflow was exercised in the live browser: create a rectangular
part, add four named corner openings with inward insets, resize the plate, drag a
dimension, then undo that gesture once. The openings follow the driving dimensions
and their identities and expressions remain intact. A duplicate opening is refused
with `sketch-opening-overlap` and no source revision change.

## Supported first slice

- Rectangle, circle, slot and rounded-rectangle starters produce ordinary source:
  shape, Profile and 6 mm Extrude.
- Existing authored Profiles expose resolved local curves and their world frame.
  Analytic geometry/provenance comes from the compiler. Browser curve sampling is
  for display only.
- Literal dimensions and directly referenced literal Static initializers can be
  edited by typing or dragging. Labels identify shared Static parameters.
- Formula, record-derived and typed-tolerance inputs remain read-only; source
  navigation exposes their authored expression rather than overwriting intent.
- A Model-level unrotated rectangular outer loop supports named circular openings
  at its center, corners and edge midpoints. The compiler names/reuses the existing
  center/size inputs and writes expressions for the requested relation.
- Overlap, containment, invalid radius and conflicting shared-value requests are
  rejected before returning an edit. Normal full build remains the solid authority;
  later failure retains the application's existing revertible draft/last model.
- Pan, zoom, Fit, keyboard dimension adjustment, source navigation, drag preview
  coalescing, revision guards and cleanup are included.
- Export's menu button is vertically centered alongside Build.

The preview contract explicitly reports `ProfileResolvedNotBuilt`. A resolved 2D
profile is not proof that an extrusion or any downstream feature can build.

## Frame decision and discovered compiler defects

Rejecting every `Concept Struct on XZ` was inherited admission debt, rather than a
useful language rule. Local 2D layouts now select a right-handed construction frame:

| Layout | Local X | Local Y | Normal |
| --- | --- | --- | --- |
| XY | world +X | world +Y | world +Z |
| XZ | world +X | world +Z | world -Y |
| YZ | world +Y | world +Z | world +X |

Default and explicit XY retain identical STEP output in the witnesses. Arbitrary
origins/orientations retain the existing traced Construction Plane workflow. This
does not imply rotating typed spatial Concept members or admitting every other
domain consumer. Unknown frames and unqualified mixed spatial layouts reject,
including unused unknown placed layouts.

Three compiler ownership gaps surfaced:

1. Construction-guide admission read unbound Static dimension expressions. Build
   now passes the same bound nominal-expression reader used by domain binding.
2. Profile frame admission scanned token sequences and imposed XY. It now reads
   the existing AST declaration/clauses and resolves a ConstructionPlane.
3. Compose retained local Profile curves but discarded their resolved frame at
   emission. Its existing placement contract now carries the inherited Base
   Profile frame. Explicit legacy Placement still transports unplaced XY inputs.
   Authored operands must share the resulting frame; a conflicting explicit
   Placement cannot override an already oriented Profile.

Compose's existing world-coordinate support adapters for Boss/Pocket/Hole/Slot/
EdgeFinish are not qualified for inherited oriented frames and now reject that
combination explicitly. Base/Add/Remove operate in one common frame. This is a
bounded capability boundary, not an implicit XY substitution.

The X4 f01 source and its explicit traced-plane reference originally matched while
both emitted the wrong XY variant. Absolute bounds tests exposed this; route parity
alone was insufficient. Before repair, XZ minimum was `[-15,-5,0]` where the simple
asymmetric witness required `[-15,-6,-5]`. After repair, both f01 spellings require
world bounds `[0,-30,0]..[60,0,40]`, enclosed volume 14250 mm³. Only f01's recorded
oracle is regenerated for this intended orientation correction; its source is
preserved. Existing XY geometry checks remain unchanged.

## Qualification

| Lane | Result |
| --- | --- |
| Release solution build | Passed |
| Focused sketch/frame tests | 22 passed |
| Fast Core lane, excluding SlowCorpus | 1,087 passed |
| Full Release solution gate, one worker | 5,728 passed in 20 suites; no failures or skips |
| Helios build and UI tests | Build passed; 104 tests passed |
| Public SDK transport tests | 17 passed |
| Shared Telos tests | 15 passed |
| Cadmata client tests | 96 passed |
| X4 authoring corpus | 32/32 passed, including chimera baseline parity |

Commands used for the .NET gate:

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_PROCESSOR_COUNT = '2'
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter 'Category!=SlowCorpus'
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

Build, corpus CLI invocations and SDK repack run serially to avoid DLL locks.
The Firmament corpus test host peaked at about 2 GB and released its memory on
exit; no claim of a memory-leak fix is made. Existing compiler/analyzer and WASM
interop warnings remain; there were no build errors. The Helios bundle retains
its existing large-chunk advisory.
During development, a React hot-refresh invalidation reused a disposed workspace
worker and required a page reload. Fresh startup and the final refreshed SDK
passed the browser checks. That development lifecycle issue is recorded here;
this pass does not claim to repair Helios hot-refresh lifecycle management.

Kernel tests cover bounds, enclosed BRep volume, topology/provenance stability,
XY STEP equality, common-frame removal, traced origins, mixed-frame rejection,
four starter families, four-opening resize, refusal and preserved expressions.
The browser witness also confirms the XZ normal `(0,-1,0)`.
After the final SDK refresh, the live WASM compiler also built the oriented Compose
witness without diagnostics. Build and Export both measured top 11 px, height
30 px, center Y 26 px in the actual toolbar. The owned test tab and Vite process
were closed after capture; no .NET/testhost processes remained.

Evidence lives under ignored `artifacts/local/`:

- `sketch-compose-before.log`: failing absolute frame witnesses before repair.
- `sketch-frame-final.log`: focused kernel witnesses after repair.
- `sketch-solution-build-final.log`, `sketch-fast-tests.log`, `sketch-full-tests.log`.
- `sketch-x4-f01.log`, `sketch-x4-full.log`.
- `sketch-helios-build-final.log`, `sketch-helios-tests-final.log`.
- `sketch-sdk-tests.log`, `sketch-telos-tests.log`, `sketch-cadmata-tests.log`.
- `sketcher/plate-workspace.png`, `sketcher/linked-opening-refusal.png`,
  `sketcher/xz-workspace.png`, `sketcher/xz-compose-built.png`: actual browser captures.

The BRep volume cross-check for the oriented nonconvex witnesses uses the existing
enclosed mass-properties evaluation. This does not extend the separate exact-volume
assertion recognizer's supported families.

## Deliberate limits

This is the first shape-and-dimension workspace, not completion of every sketching
idea from the discussion. General cursor snaps, arbitrary reference expressions,
polyline direct-distance entry, boundary-edit GUI tools and arbitrary trim/extend
remain outside this slice. No simultaneous constraint solving was introduced.
Editing included/generated recipe bodies requires an authored owner/provenance
contract and is not inferred from expanded geometry. Profile selection requires a
unique authored name. Opening clearance currently admits circular inner loops;
noncircular inner-loop clearance refuses explicitly.

The shared renderer and SDK are available to product hosts; this pass wires the
workspace into Helios. It does not claim a new Cadmata sketch command or native
desktop interaction qualification. Cadmata's existing unit suite is checked for
compatibility with the shared package.

The authoring contract and current boundaries are documented in
[the public sketcher guide](../public/firmament/sketcher.md) and
[syntax](../public/firmament/syntax.md).

## Code review and verdict

The pass adds no regex parser, whole-document dispatch route, source macro system
or diagnostic fatality allowlist. Edit preparation uses existing AST-addressed
operations and Profile admission. Unknown/unsupported frames do not fall back to
XY. Compiler-owned messages and source spans are transported to the workspace;
curve provenance may refer to lowered source, while control spans identify the
authored expression for navigation. Browser preview results are revision-guarded
and event listeners/timers are disposed with the workspace.

**Success for this bounded first slice.** The four-opening workflow improves in
the real Helios path, and both simple extrusion and Compose now preserve the
qualified Profile frame. The deferred sketch tools and domain-specific oriented
feature adapters listed above remain explicit follow-ups, not hidden support.
