# Helios export picker qualification

Date: 2026-10-19

## Behavior and ownership

Helios replaces **Export STEP** with a checkbox picker for STEP AP242, GLB,
USDA, STL, OBJ and PDF/SVG/PPTX drawings. The command palette opens the same picker.
One format downloads directly; several formats download as one ZIP, avoiding
browser multiple-download permission prompts. Failures return no partial archive.
The file stem comes from the compiled project root, including while a component
document is open.

`ProductArtifactExporter` is a small adapter over the existing STEP bytes,
Assembly/Scene GLB and USDA writers, SurfaceMeshIR STL/OBJ exporters and Drawing
compiler. It owns format metadata and packages files; Helios supplies no format
schema or geometry implementation. Existing `exportSTEP()` and `exportSTEPBlob()`
remain available through the same SDK admission path. The older runtime
`exportStep` operation remains for clients using that transport contract.

The Web runtime retains the accepted display packet and scene/assembly context.
Part export packets share the viewport's position/normal/index arrays. STEP is
still written lazily for assemblies; GLB/USDA-only requests do not force STEP
serialization. Source/property edits and failed rebuilds mark exports dirty.
Requests carry the build revision; the UI rebuilds changed source and discards an
export if another edit occurs while preparation runs. The last-valid viewport
remains available without becoming an accidental export of the current draft.

Drawing generation projects the retained compiled STEP through the existing
dimension planner. `GenerateFromStep` shares the same implementation as native
file generation, including unsupported-family, PMI-readback and complete nominal
geometry checks. Authored PMI names come from the compiler-owned admission
document through `AuthoredPmiNames`; the adapter cannot silently omit a requirement
that lacks an AP242 projection. STL retains the native closed-mesh validation.
Temporary drawing/STL files are invocation-owned and removed in `finally`.

## Deliberate limits

- Parts offer all eight formats, subject to the existing geometry/drawing admission
  checks. Assemblies offer STEP/GLB/USDA. Scenes offer GLB/USDA. Disabled options
  explain their product-kind boundary.
- GLB is the glTF 2.0 download. No standalone JSON glTF or DXF implementation was
  added. GLB/USDA use the authored static pose, supported appearances and Scene
  cameras; they do not serialize a currently playing animation or viewport state.
- Automatic drawings remain limited to rectangular stock and disjoint circular
  Z-through holes. Missing tolerances, material and signoffs remain unspecified.
  The picker does not infer manufacturing requirements, use reference-only fallback,
  or claim engineering approval.
- Qualification uses the local development WASM package and real Helios browser
  application. It does not constitute a published package or new production AOT
  release. No new CAD/kernel capability is claimed.

## Reproduction

Run the normal serial .NET build and both required lanes:

```powershell
$env:MSBUILDDISABLENODEREUSE='1'
$env:DOTNET_PROCESSOR_COUNT='2'
dotnet build Aetheris.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Firmament.Tests -c Release --no-build -m:1 --filter FullyQualifiedName~ProductArtifactExportTests
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 --filter 'Category!=SlowCorpus'
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
node --test Aetheris.Web.Runtime/sdk/tests/*.test.mjs
```

Install the current local SDK with Helios's existing
`node scripts/install-local-sdk.mjs`, then run its TypeScript, Vitest and Vite
builds. Start Helios's existing Vite server on port 4173. From the Aetheris root:

```powershell
node scripts/qualify-helios-exports.mts ../HeliosCAD/node_modules/playwright/index.mjs
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/DrawingGeneration/inspection-plate.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll inspect artifacts/local/helios-exports/inspection-plate.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Regression/DrawingGeneration/inspection-plate.firmament --out artifacts/local/helios-exports/native-plate.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll mesh artifacts/local/helios-exports/inspection-plate.step --format stl --output artifacts/local/helios-exports/native-plate.stl --json
python scripts/qualify-helios-export-files.py
```

The file verifier needs `pypdf`; `pypdfium2` renders the PDF pages for visual
inspection. Browser qualification uses isolated headless Edge and closes its
browser. Shut down the temporary Vite server after qualification.

## Evidence

Raw logs, downloads, ZIP contents, PDF renders and screenshots are ignored under
`artifacts/local/helios-exports/` and `artifacts/local/export-*.log`.

- Real part download: all eight files, a single ZIP and a direct STEP file.
- Assembly: STEP/GLB/USDA; Scene: GLB/USDA including three authored cameras.
- Invalid changed source prevents exporting the retained last-good model.
  An unsupported chamfered-part drawing rejects with
  `drawing-definition-incomplete`, producing no download.
- Unit coverage checks the format catalogue, archive contents, readable output
  structure, source revision changes during export, stable diagnostic codes,
  missing authored PMI rejection, and scratch cleanup.
- The inspection plate reopens through the CLI as one body, 16 faces, 42 edges,
  48 vertices, with bounds `[-50,-35,0]` to `[50,35,8]` mm. STL has 1,684 triangles.
  Downloaded STEP and STL are byte-identical to native CLI output.
- STEP SHA256: `17eccf1954168db4b3764343e0db44bef707d51e29ecb6660a6b51c55d8c1119`.
  STL SHA256: `a4942626a3c6ddd6cc87e2869f7d2daa1e006ae396500407936b7fc44a71009d`.
- Downloaded PDF text, SVG XML, GLB JSON/binary headers, USDA unit/axis metadata
  and PPTX package contents are independently reopened. The PDF visibly retains
  X0/Y0 references, diameter PMI, controlling stock dimensions and incomplete
  manufacturing-requirement notices.

## Final gates and verdict

| Gate | Result |
| --- | --- |
| Release solution build | Passed, zero errors; existing nullable/WASM native-import warnings remain |
| Focused artifact tests | 2 passed |
| Core fast lane | 1,087 passed |
| Full solution, including corpus lane | 5,665 passed across 20 test assemblies; zero failures/skips |
| Web SDK tests | 14 passed |
| Helios tests | 101 passed across 21 files |
| Helios TypeScript and Vite development build | Passed; existing bundle-size advisory remains |
| Real Helios browser and SDK exports | Passed, no uncaught page errors |
| Download reopening, native STEP/STL byte parity and PDF visual inspection | Passed |
| Whitespace checks in both repositories | Passed |

The SDK source, built distribution and Helios-installed JavaScript hashes agree.
The direct browser SDK check also exercises the retained `exportSTEP()` API and
rejects export after an unsuccessful `setSource` rebuild. The menu was visually
checked after correcting its stacking over Monaco's scrollbar. The temporary Vite
server and browser were closed; no .NET build/test/runtime hosts remained at
closeout.

**Success.** The requested exports work through the real browser/runtime paths,
with explicit product-family limits and preserved engineering geometry. Public
guides in both repositories describe the current download behavior and limits.
