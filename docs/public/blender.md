# Aetheris CAD for Blender

Aetheris CAD adds exact, code-authored parts to Blender. Edit Firmament in
Blender's Text Editor, build a BRep with the bundled local Aetheris compiler,
and use Blender for placement, materials, mesh modeling and rendering. The
source and accepted exact STEP body travel with the `.blend` file.

## Install and build a first part

The extension ZIP targets **Blender 5.2 on Windows x64**. In Preferences,
open **Get Extensions**, use its menu to **Install from Disk**, select the
complete `aetheris_cad-0.1.0-windows-x64.zip`, and enable Aetheris CAD.
The ZIP includes the compiler runtime; no .NET SDK, account or server is needed.

1. In the 3D View, choose **Add → Aetheris CAD Part**. This creates a CAD
   container and opens its Firmament source in a Text Editor beside the view.
2. Click **Build CAD** in the Text Editor header. The first successful build
   frames the part in the viewport. The **Aetheris** tab in the sidebar
   (toggle the sidebar with `N`) contains the same build and export controls.
3. Change a dimension in the source and click **Build CAD** again. Use this
   command for Firmament; Blender's **Run Script** command runs Python.
4. Save the `.blend` normally. Reopen it with the extension enabled to continue
   editing, rebuilding or exporting the exact body.

The starter is the complete [plate lesson](../../fixtures/Canonical/Learn/01-plate.firmament):

```firmament
Model Plate {
  units: mm
  Box stock { size: [80mm, 50mm, 8mm] }
  Modify stock {
    Hole<Shaft> mount {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: 8mm
      end: ThroughAll
    }
  }
}
```

## Editing and mesh modeling

The Firmament source owns the exact part. A successful build updates the
managed display mesh in place, preserving its Blender object, placement,
modifiers and material slots. Per-face material assignments survive when
triangle connectivity is unchanged; otherwise faces return to material slot
zero and the build status reports the reset.

Use **Create Editable Mesh Copy** for Blender vertex editing, sculpting or
mesh booleans. This creates an independent mesh with welded seams. Changes
to it do not change the Firmament source or exact STEP body. If you edit the
managed mesh directly, the next build refuses to overwrite those edits.
Create a mesh copy to keep them, then choose **Restore CAD Mesh** explicitly.

**Fit CAD Part** frames the selected CAD part. Selecting either its container,
its managed mesh or its associated source in the Text Editor identifies the
part for build/export commands. A scene may contain multiple independent CAD
parts.

## Existing source and dependencies

Use **Open Firmament Part** in the Aetheris sidebar to copy a `.firmament`
document into the scene. Use **Add Project File** for each include, STEP
asset or other dependency. Files below the original source folder keep their
relative paths; files selected elsewhere use their filename. The project file
list lets you change the relative path to match the source reference.

The extension packs only explicitly selected files, never a whole directory.
Paths must be relative, unique ignoring case, and must not contain `..`.
A selected dependency may be at most 32 MiB; a build snapshot may total at
most 64 MiB. Source, text dependencies and encoded binary dependencies are
saved as Blender Text datablocks. **Unlink Project File** removes the project
reference but retains its Text datablock. Reopening and rebuilding does not
require the original files on disk.

## Builds and exact export

Builds use a short-lived local compiler process and keep the UI responsive.
Press `Esc` to cancel. An invalid source, missing dependency or failed build
keeps the last accepted geometry; **Open Diagnostics** shows the error.
Changing the source during a build prevents that older result from replacing
the part.

**Export Exact STEP** writes the accepted BRep in **Firmament source
coordinates**. Blender placement, scale, modifiers, material changes and
editable mesh copies are presentation changes and are not baked into STEP.
Export is refused when the source/dependencies have changed since the last
successful build or the stored STEP has been altered.

Firmament millimetres are converted using the scene's unit scale at build
time. Rebuild after changing the scene unit scale. Source dimensions and
STEP dimensions retain their physical units.

## Limits

Each CAD part builds one standalone BRep model through the existing compiler.
Assembly and Scene roots and mesh-only model results are not supported by this
extension. Geometry support is the same as the Aetheris compiler; incomplete
display tessellation is rejected. There is no mesh-to-BRep conversion,
interactive face/edge CAD feature editing, or Firmament completion/highlighting
in Blender's Text Editor.

The compiler override in extension preferences is for developers. Leave it
empty to use the runtime included in the package.

See [Learn Firmament](learn/README.md), [geometry and features](firmament/geometry.md)
and [STEP import](firmament/step-import.md) for source modeling and supported
operations.
