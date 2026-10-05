# PRESENTATION-3D-X0

Status on 2026-10-04: **Meaningful progression; desktop qualification pending.**

The C# GLB exporter and the existing C# slide writer now produce a two-slide PPTX
containing physically embedded ATLAS and guitar GLBs. Independent glTF validation
and Blender import succeed. Acceptance remains conditional on observing rotation,
tilt, save, close, reopen, and further interaction in desktop PowerPoint without
repair or missing-media warnings. Schema validation alone cannot prove this.

## Architecture and authority

`AssemblyGlbExporter` lowers `AssemblyDisplayMeshDocument`, the same qualified
display representation used by USD. The assembly compiler and pose evaluator
remain the owners of geometry and occurrence frames. Definitions become shared
indexed triangle meshes with existing normals. Occurrences become named glTF
nodes, using world-to-parent local transforms. Aetheris row-vector storage is the
column-major array of its transpose, so GLB requires no extra array transpose.

One root boundary maps `(x,y,z)` to `.001 × (x,z,-y)`, converting right-handed
Z-up millimetres to right-handed Y-up metres. Sharp face boundaries remain split
in the display mesh. No second tessellator, BRep serialization, glTF importer,
animation, compression, or game-engine integration was introduced. STEP/BRep
retains engineering authority.

## Appearance

The reusable .NET exporter maps existing USD preview RGB, metallic and roughness
values into glTF PBR factors. Explicit definition UVs and PNG/JPEG texture bytes
are supported and embedded in the binary buffer. Paths never enter GLB metadata.

The final polished witnesses use their established Aetheris USD-derived Blender
scenes. `export-presentation-scene.py` preserves product positions and polygons,
exports the existing ATLAS label texture, and bakes guitar sunburst/wood
base-color surfaces. This is a bounded finishing step, not a general material
graph translator. Procedural wood-grain bump is omitted; scalar PBR and supported
coat survive. No guitar geometry work was reopened. Studio floors, lights and
the chrome target are excluded from the exported assets.

The direct .NET exports are retained as `atlas-display.glb` and
`guitar-display.glb`; the polished deliverables are `atlas.glb` and `guitar.glb`.

## C# slide compiler and OOXML oracle

`DrawingPptxWriter.WriteModel3DDeck` extends the existing slide builder and OPC
package writer. `Model3D` provides source, PNG preview, millimetre placement,
semantic name and explicit view parameters. The JSON CLI is:

```powershell
dotnet run --project Aetheris.CLI -c Release -- presentation compile `
  fixtures/Canonical/Presentation3D/interactive-3d-demo.json `
  artifacts/local/presentation-3d-x0/interactive-3d-demo.pptx --json
```

The user authored `powerpoint-reference.pptx` in desktop PowerPoint
**16.0.20430.20118**, inserting both local GLBs with Insert → 3D Models → This
Device and saving the package. Its original 43-part package remains local. Slide
2 contains both models. A compact first-object XML extract is retained under
`fixtures/Canonical/Presentation3D/office-model3d-oracle.xml`.

The generated package follows the inspected oracle:

- Content type `model/gltf.binary` for GLB and `image/png` for fallback.
- Internal model relationship
  `http://schemas.microsoft.com/office/2017/06/relationships/model3d`.
- `mc:AlternateContent`, with an `am3d` choice containing `p:graphicFrame` and
  `a:graphicData` for `http://schemas.microsoft.com/office/drawing/2017/model3d`.
- `am3d:model3d r:embed`, shape properties, deliberate camera, centering/scale/
  rotation, raster image relationship, viewport, ambient and point lights.
- A `p:pic` fallback referencing the same embedded PNG.

The compiler embeds the exact input GLB bytes; it does not invoke Office to
generate the deck. It adds no Open XML dependency to production: the writer
already emits OOXML directly. Tests use the existing Open XML SDK 3.3.0 and
Office 2019 validation. Current part locations are `ppt/media/model3d-1-1.glb`
and `ppt/media/model3d-2-1.glb`. All relationships are internal. Package byte
equality against the source assets and XML structure against the manual oracle
are checked, without demanding byte identity with Office's rewritten package.

## External validation and fallback

Khronos glTF Validator **2.0.0-dev.3.10** reports zero errors and zero warnings on
both direct display GLBs and both polished GLBs. ATLAS has one informational
message for the existing 1024×192 label texture; direct display GLBs have
informational default-matrix messages. Guitar has no validator issues.

Blender **5.2.2 LTS** imports the polished assets independently. ATLAS has 61
visible meshes using 25 mesh datablocks, including its printed label. The current
guitar has 99 visible mesh objects and six embedded 1024×1024 images. Imported normals
are unit length and transforms are positive-handed. Measured physical bounds in
Blender's Z-up metres are approximately:

| Model | X | Y | Z |
|---|---:|---:|---:|
| ATLAS | 0.295001 | 0.130000 | 0.340774 |
| Guitar | 0.337550 | 1.039273 | 0.066340 |

`atlas-fallback.png` and `guitar-fallback.png` render the independently imported
GLBs. Visual inspection confirms the industrial robot, maker mark, polished
metal parts, and guitar sunburst. View parameters in the deck are deliberate
presentation normalization and Euler rotation; the embedded GLBs retain physical
scale. Fallback images are useful static views, not a substitute for interaction.

## Artifacts, timing and regression

Generated files, raw validators, import reports, build/test logs and the manual
oracle live under ignored `artifacts/local/presentation-3d-x0/`.

| Artifact | Bytes |
|---|---:|
| ATLAS polished GLB | 546,680 |
| Guitar polished GLB, refreshed 2026-10-04 | 6,606,296 |
| Two-slide PPTX before models | 6,526 |
| Refreshed two-slide PPTX, 2026-10-04 | 5,870,858 |

Original October 2 timings: direct .NET GLB lowering took about 60 ms for ATLAS and 96 ms for guitar after
display preparation. Compilation and tessellation took about 4.29 s and 8.90 s
respectively. Polished finishing took approximately 0.70 s and 2.09 s. C# slide
compilation took approximately 116 ms. Desktop PowerPoint load time is unmeasured.
The complete reproduction script ran successfully, including independent import,
renders, all four validator reports and both deck variants.

Focused GLB tests cover real display geometry, world placement, shared meshes,
normal bytes, deterministic output, textures/PBR, malformed inputs and both
witnesses. CLI tests cover valid export and preservation of output after invalid
options. Three existing/new PPTX tests pass, including Office 2019 validation,
exact embedding, multiple model relationships, fallback, deterministic packages,
oracle structure and existing drawing functionality. Release build succeeds;
the fast core lane passes 1,005 tests. The October 2 complete serial solution lane passes
4,042 tests with zero failures (`full-tests-closeout.log`), including 1,138 core,
1,736 Firmament and 455 CLI tests.

The first full run is retained in `full-tests.log`: its documentation check ran
before this report existed, and an existing USD export test exceeded the bounded
five-second tessellation budget (5,916 ms). Both passed in a focused diagnostic
run without changing production USD behavior. After generation completed, the
full serial closeout run passed. The initial failures are retained rather than
erased from the evidence.

Reproduce the generated assets with:

```powershell
pwsh -File scripts/qualify-presentation-3d-x0.ps1
```

This requires the existing qualified ATLAS `.blend` scene. If absent, run the
industrial ATLAS qualification script with `-Render` first. The qualifier rebuilds
the guitar USD from current Firmament code and invokes the existing sunburst
renderer to recreate `guitar-source/guitar-studio.blend` on every run. It builds
both direct GLBs, finishes both polished scenes, imports/renders them independently,
runs pinned Khronos validation, and calls the C# deck compiler.
It does not claim desktop acceptance.

## Current-source refresh, 2026-10-04

The original presentation guitar used a September 30 cached studio scene. It
predated the source-owned semicircular tailpiece/string rail and the repaired
headstock/neck connection. The presentation qualifier now recompiles and reshades
the guitar before GLB finishing, so cached geometry cannot conceal those fixes.
The material binding for the widened headstock root was also corrected to its
current full definition identity. Guitar geometry source was not changed.

The current source has 99 visible part occurrences and 56 shared display
definitions, rather than the old scene's 107 visible parts. Refresh evidence is
retained in `guitar-source/` and `refresh-2026-10-04.log` beneath local artifacts.
The refreshed polished GLB imports as 99 mesh objects and 145,728 triangles;
Khronos validation reports zero errors and zero warnings. Its embedded copy in
`interactive-3d-demo-2026-10-04.pptx` matches the current GLB byte-for-byte. The
dated filename avoids overwriting the original deck while it is open in Office.
Use `-DeckName interactive-3d-demo-2026-10-04.pptx` with the qualifier to choose
this output name. Blender invocations now use `--python-exit-code 1`, ensuring
failed finishing cannot silently pass by retaining an old output file.
The refresh log records the original filename's Office lock; the separate
`deck-compile-2026-10-04.json` records successful dated compilation (136 ms).
The dated deck's media are embedded and its relationships are all internal.

`guitar-bridge-detail.png` and `guitar-head-join-detail.png` are closeups rendered
from an independent import of the refreshed GLB. They show the authored 5 mm
semicircular string rail and the solid scarf/neck-to-headstock transition. The
existing engineering witness tests verify the rail diameter sits at z=60 mm
and the string crown at z=65 mm; no presentation-only repair geometry was added.

October 4 validation: the solution Release build succeeds (zero errors, nine
warnings), and the fast core lane passes all 1,005 tests. The full serial lane
records 4,265 passes and one failure in
`ProfileBoundaryAuthoringTests.OverlapIsRejectedAndDisjointEditOrderDoesNotChangeGeometry`
at line 73. It reproduces in isolation using inline rectangular-panel source,
without referencing guitar files, appearances or presentation artifacts. No C#
compiler code was modified in this refresh. Both the full failure and focused
diagnostic are retained as `refresh-full-tests.log` and
`refresh-profile-boundary-diagnostic.log`; the full suite is not claimed green.

## ATLAS disclaimer

ATLAS is an independent Aetheris demo robot. Aetheris is not affiliated with,
endorsed by, or associated with Boston Dynamics. The shared name is coincidental.

The deck caption, asset metadata, fixture README and public documentation carry
this disclaimer. Renaming is possible later, but changing source identities,
labels and historical witness names would complicate provenance; X0 retains the
existing name with explicit non-affiliation.

## Remaining acceptance gate

Open the generated PPTX in desktop PowerPoint. Select each 3D model and use its
central 3D control to rotate and tilt. Save under another local folder, close and
reopen, then rotate both again. Record model appearance, textures, warnings and
repair behavior. These manual results are still required for Accepted. Desktop
PowerPoint is installed and the manual oracle is available; the remaining gate
is qualification of the compiler-generated deck, not installation or package
schema discovery. No successful interaction result is claimed yet.
