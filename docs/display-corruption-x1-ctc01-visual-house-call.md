# DISPLAY-CORRUPTION-X1 — CTC-01 visual house call

## 1. Purpose and scope

Diagnose the severe local Aetheris Cadmata display corruption reported for the CTC-01 AP242 fixture, prove the failing layer, and land the smallest safe fix that makes the imported model visually recognizable again without changing STEP import/export semantics, BRep topology, or server authority.

## 2. User-observed symptom

User report for CTC-01:

- Aetheris displayed the model as exploded / fragmented.
- Many cylindrical strip-like pieces were visible.
- A large dark incorrect face mass dominated the body.
- The result was not recognizable as the expected bracket.
- SolidWorks displayed the same STEP file as a coherent bracket / plate with holes, slots, rounded ends, and a center block.

## 3. Exact CTC-01 file tested

- `testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp`

## 4. Commands run

```powershell
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1

dotnet run --project Aetheris.CLI -f net10.0 -- analyze testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --json
dotnet run --project Aetheris.CLI -f net10.0 -- canon testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --out artifacts/display-corruption-x1/nist_ctc_01_canonical.step --json

node scripts/display-corruption-x1-playwright.mjs

cd aetheris.client
npm run build
npm test -- --run App.test.tsx displayRenderables.test.ts AetherisViewport.test.tsx displaySceneBuilder.test.ts displaySceneBounds.test.ts

dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|ViewMaterialization|Tessellation|CTC01|Ctc01|Ctc" --logger "console;verbosity=minimal"
dotnet test Aetheris.Server.Tests/Aetheris.Server.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|KernelApi|ViewMaterialization|Tessellate|Tessellation|StepIo|CTC01|Ctc01|Ctc" --logger "console;verbosity=minimal"

dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "Step242Ftc06SameSenseRegressionTests|FTC06|Ftc06|same_sense" --logger "console;verbosity=minimal"
dotnet run --project Aetheris.CLI -f net10.0 -- analyze testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp --json
```

## 5. Backend import/export result

Artifacts:

- [ctc01-analyze.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-analyze.json)
- [ctc01-canon.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-canon.json)
- [nist_ctc_01_canonical.step](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/nist_ctc_01_canonical.step)

Observed:

- `analyze` succeeded.
- `canon` succeeded.
- Topology counts were plausible for a single enclosed body:
  - `bodyCount=1`
  - `shellCount=1`
  - `faceCount=117`
  - `edgeCount=318`
  - `vertexCount=206`
  - bounds `[-400,-225,-100]` to `[400,225,50]`
- Surface family mix was plausible:
  - `plane=56`
  - `cylinder=57`
  - `cone=4`

Conclusion:

- No evidence that the failure originates in STEP/AP242 import.
- No evidence that canonical export is failing for this house call.
- The imported body is structurally plausible and bounded.

## 6. DisplayIR server result

Artifacts:

- [ctc01-import.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-import.json)
- [ctc01-display-prepare.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-display-prepare.json)
- [ctc01-display-summary.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-display-summary.json)

Observed:

- `/import/step` succeeded.
- `/display/prepare` succeeded with `status=Complete`.
- `displayAuthority=DisplayIR`, `sourceAuthority=BRep`.
- Lane summary:
  - `AnalyticPatch`: `99` faces, `Complete`
  - `BoundedMesh`: `18` faces, `Complete`
- Per-face records were finite and diagnostic-free for this case.
- The fallback tessellation payload was more complete than the face-level lane summary implied:
  - `tessellationFallback.facePatches = 117`
  - `tessellationFallback.edgePolylines = 318`

Important evidence:

- The server already emitted an accurate bounded fallback mesh for every face.
- Several analytic plane faces had inner loops:
  - face ids `3, 15, 48, 50, 55, 56, 98`
- Those are exactly the kinds of faces the existing frontend analytic preview cannot render faithfully because it triangulates only the outer boundary.

Conclusion:

- The server did not fail to prepare a display packet.
- The server already provided enough fallback geometry for the frontend to avoid visible corruption.

## 7. Frontend DisplayScene result

Relevant files:

- [displayRenderables.ts](/C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/viewer/displayRenderables.ts)
- [analyticMapper.ts](/C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/viewer/analyticMapper.ts)

Pre-fix problem:

- The client always preferred typed analytic faces when present.
- For planes, `analyticMapper.ts` triangulated only `planeGeometry.outerBoundary`, so any inner holes were filled.
- For cylinders and cones, the analytic preview used only axial `domainHint` and a full `0..2π` revolution preview, which is a lossy representation for trimmed surfaces.
- The client ignored the already-available per-face bounded fallback mesh in `tessellationFallback` for those faces.

Effect:

- CTC-01 displayed large incorrect planar fills and misleading cylindrical strips even though `/display/prepare` had already provided correct fallback meshes for the same faces.

Fix:

- Keep the typed DisplayIR path authoritative.
- When a face is emitted as `AnalyticPatch` but the frontend analytic preview is known to be lossy and a same-face bounded mesh exists in `tessellationFallback`, render the fallback mesh for that face instead.
- Current fallback-selection policy:
  - planar analytic face with `loopCount > 1`
  - any analytic `Cylinder`
  - any analytic `Cone`
  - any analytic face whose preview mesh cannot be built

This is a targeted render-selection fix, not a return to the legacy mesh-scene primary path.

## 8. UI / Playwright screenshots and findings

Artifacts:

- [ctc01-aetheris-default.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-aetheris-default.png)
- [ctc01-aetheris-status.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-aetheris-status.png)
- [ctc01-aetheris-angle-1.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-aetheris-angle-1.png)
- [ctc01-aetheris-angle-2.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-aetheris-angle-2.png)
- [ctc01-playwright-summary.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-playwright-summary.json)
- [ctc01-playwright-console.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-playwright-console.json)
- [ctc01-playwright-pageerrors.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-playwright-pageerrors.json)
- [ctc01-playwright-requestfailures.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-corruption-x1/ctc01-playwright-requestfailures.json)

Observed after the fix:

- The imported shape is recognizable as the expected CTC-01 bracket / plate.
- Two upright rounded ends are visible.
- Through-holes and slots are visible.
- The center protruding block is visible.
- The severe exploded-strip failure is no longer present.
- No console or network errors were required to explain the original corruption.

Inspector summary from the fixed UI capture:

- display lane: `mixed-fallback`
- display status: `Complete`
- render path: `mixed-fallback`
- analytic faces: `99`
- fallback faces: `18`

Note:

- The current inspector “Face count” reflects rendered mesh-face count, not total backend face count. In the fixed capture it reads `70` even though backend topology is still `117` faces.

## 9. Root cause

Classification:

- `AnalyticPatchMappingError`

Exact failing layer:

- frontend DisplayScene / analytic preview selection

Why this layer, not import or server:

- import and canonical export both succeeded with plausible topology
- `/display/prepare` succeeded and returned a complete typed packet
- the same packet also contained full fallback tessellation for every face
- the frontend chose lossy analytic previews over accurate same-face fallback meshes for trimmed planes / cylinders / cones

## 10. Fix applied or blocker

Fix applied.

Files changed:

- [displayRenderables.ts](/C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/viewer/displayRenderables.ts)
- [displayRenderables.test.ts](/C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/__tests__/displayRenderables.test.ts)
- [display-corruption-x1-playwright.mjs](/C:/Users/yuech/source/repos/Aetheris/scripts/display-corruption-x1-playwright.mjs)

Fix summary:

- Added a targeted client-side compatibility rule to consume per-face bounded fallback meshes only when the analytic preview path is known to be visually lossy for the current DTO contract.
- Did not change STEP import/export behavior.
- Did not change BRep topology.
- Did not change DisplayIR server authority.
- Did not rewrite tessellator algorithms.
- Did not remove legacy compatibility fields.

## 11. Tests added

Added focused regression coverage in [displayRenderables.test.ts](/C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/__tests__/displayRenderables.test.ts):

- `PrefersFallbackMeshForAnalyticPlanesWithInnerLoops`
- `PrefersFallbackMeshForAnalyticCylindersWhenAvailable`

## 12. Remaining issues

- The underlying analytic DTO contract is still lossy for trimmed curved surfaces because it does not carry angular trim information for cylinders / cones.
- The analytic planar DTO still exposes only `outerBoundary`, not explicit hole loops.
- The current fix avoids user-visible corruption by selecting bounded fallback meshes where needed, but it does not expand the analytic patch contract itself.
- Inspector face-count labeling could be clarified in a later polish pass.

## 13. Recommended next milestone

Recommended next milestone:

- Extend the typed analytic patch contract so trimmed analytic surfaces can be represented faithfully without needing per-face fallback mesh substitution.

Suggested bounded follow-up:

- add explicit trimmed-loop data for planar faces
- add angular trim intervals or edge-loop-derived trim descriptors for cylinder / cone analytic patches
- keep the current fallback-selection safeguard until the richer analytic contract is proven

## 14. Non-goals

This house call did not:

- change STEP import semantics
- change STEP canonical export semantics
- change AP242 importer/exporter behavior
- change BRep topology construction
- change DisplayIR server authority
- rewrite the tessellator
- weaken FTC-06 / FTC-07 regression coverage
- change CAD feature behavior
