# Blender CAD extension, 2026-10-31

## Change

Added a Windows x64 Blender 5.2 extension with a bundled self-contained CLI.
Firmament text and explicitly selected dependencies persist inside `.blend`;
each successful build retains exact STEP and installs the existing kernel
display tessellation. Blender owns placement, materials, modifiers, rendering
and independent welded mesh copies. No second parser, geometry kernel or
long-lived server was added.

The new `mesh --format display-json --step-output` CLI path admits standalone
parts through `FirmamentBuildAndExport` and reuses
`AssemblyDisplayMeshExporter.PrepareDefinition`. Existing `assembly-json`
requires an assembly root, so it could not provide this standalone seam.
Both artifacts derive from one compilation. The extension publishes only a
complete successful process result from its private snapshot directory.

Builds poll a short-lived subprocess through Blender's modal operator timer.
Failed/cancelled builds retain accepted geometry; stale source snapshots and
modified stored STEP are rejected. Direct generated-mesh edits require an
explicit restore after offering an independent copy. Rebuilds preserve object
identity, transforms, modifiers and material slots. Face assignments persist
only when connectivity matches. Source navigation starts at the first line.

## Evidence

- Full Release solution build: zero errors, 38 warnings.
- Fast core lane: 1,168 passed. Full solution lane: 6,317 passed, zero failed,
  two existing DrawingNotes tests skipped; FrictionLab reports no discovered
  tests. Includes the new real-CLI display/STEP preservation regression.
- Installed and validated the complete ZIP in isolated Blender 5.2.2 LTS
  profiles, with no developer compiler override. Qualified actual source
  builds, parameter updates, last-good geometry retention, stale STEP refusal,
  source changes during compilation, mesh-copy independence/manifold seams,
  managed-mesh edit refusal/restore, scene unit conversion and rendering.
- Saved and reopened the actual `.blend`, removed original include files,
  rebuilt both the main part and the packed-include part, and exported STEP.
  Independent bundled CLI reimport measured exactly
  `39597.87614034051 mm³` for the 100 × 50 × 8 mm plate with an 8 mm through hole;
  bounds are `[-50,-25,0]` to `[50,25,8]` mm.
- Native desktop pass: sidebar controls, Fit CAD Part, source/view split,
  Text Editor modification from 100 to 120 mm and asynchronous header rebuild
  visibly updated the body while keeping its material/placement. Undo restored
  accepted geometry and correctly marked the edited source stale. Source
  opening initially appeared blank at EOF; fixed initial cursor/scroll position
  before the final package run. Automated qualification uses real Blender
  operators in background mode; interactive cancellation/redo are not claimed
  as separately qualified.
- Repository layout guard and `git diff --check` passed.

Local evidence lives under `artifacts/local/blender-addon/`: full build/test
logs, `final-qualification/qualification.json`, `qualified.blend`, STEP
artifacts and `cad-in-blender.png`. Generated files remain ignored.

Final ZIP: `aetheris_cad-0.1.0-windows-x64.zip`, 49,649,592 bytes,
SHA-256 `a2ae3df02a25f2460ddec13b7616af1d4f60153c4c0a917ceb69526c2b021ab9`.
Its `package-info.json` records the bundled runtime version and file hashes.

## Reproduce

Run from the repository root, with Python, .NET 10 and Blender 5.2 available:

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter 'Category!=SlowCorpus'
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
python scripts/package-blender-addon.py
python scripts/qualify-blender-addon.py --blender 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --output artifacts/local/blender-addon/final-qualification
```

The qualification script installs into its own `BLENDER_USER_RESOURCES`,
checks subprocess exit status using `--python-exit-code 1`, runs create/reopen
phases, then independently measures exported STEP. Use `--runtime-dir` with
the packager only to reuse an already published current CLI after Python-only
changes. User installation and workflow are in `docs/public/blender.md`.

## Verdict and limits

Success for a first packaged source-authored CAD workflow inside Blender.
The extension supports multiple independent standalone BRep parts. Assembly
and Scene roots, interactive CAD face/edge feature editing, mesh-to-BRep
conversion and non-Windows packages are outside this version. Exact STEP is
exported in source coordinates; Blender placement/modifiers are presentation.
No manufacturing accuracy claim is made for the display mesh or its copy.
