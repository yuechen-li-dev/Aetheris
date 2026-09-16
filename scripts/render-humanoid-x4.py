"""Render the X4 neutral joint-frame overlay; no source deformation is implied."""
import argparse
import json
import runpy
from pathlib import Path
import sys

import bpy
from mathutils import Vector

helpers = runpy.run_path(str(Path(__file__).with_name("render-humanoid-x2.py")))
setup, obj_mesh, render, material = (helpers[name] for name in ("setup", "obj_mesh", "render", "material"))


def cylinder_between(first, second, radius, mat):
    delta = second - first
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=delta.length, location=(first + second) / 2)
    obj = bpy.context.object
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(mat)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out-dir", default="artifacts/local/humanoid-x4")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    out = Path(args.out_dir)
    audit = json.loads((out / "frame-audit.json").read_text(encoding="utf-8"))
    scene = setup()
    body = obj_mesh(out / "source-neutral.obj")
    body.data.materials.clear()
    body.data.materials.append(material("Neutral Antonia", (.42, .45, .48)))
    wire = body.modifiers.new("Canonical topology wire", "WIREFRAME")
    wire.thickness = .00022
    wire.use_replace = True

    colors = {
        "poser": material("Poser deformation pivot", (.12, .9, .3)),
        "blender": material("Blender metarig pivot", (.15, .45, 1.0)),
        "aetheris": material("Aetheris seam socket proxy", (1.0, .18, .12)),
    }
    for item in audit["points"]:
        origin = Vector(tuple(item["canonicalMm"][axis] / 1000 for axis in ("x", "y", "z")))
        role = item["id"].split(".", 1)[0]
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=.018, location=origin)
        bpy.context.object.name = item["id"]
        bpy.context.object.data.materials.append(colors[role])
        if role == "blender":
            frame = audit["sourceRestFrames"]["thigh.L" if item["side"] == "Left" else "thigh.R"]
            axis_materials = [material(item["id"] + ".axis-x", (1, .2, .2)),
                              material(item["id"] + ".axis-y", (.2, 1, .2)),
                              material(item["id"] + ".axis-z", (.2, .35, 1))]
            for axis, mat in zip(("x", "y", "z"), axis_materials):
                value = frame["axesCanonical"][axis]
                endpoint = origin + Vector((value["x"], value["y"], value["z"])) * .07
                cylinder_between(origin, endpoint, .0022, mat)

    center = Vector((0, -.025, .92))
    scene.camera.data.ortho_scale = .42
    views = {
        "front": center + Vector((0, 3, .05)),
        "side": center + Vector((3, 0, .05)),
        "iso": center + Vector((2.2, 2.2, .8)),
    }
    manifest = []
    for name, eye in views.items():
        scene.camera.location = eye
        scene.camera.rotation_euler = (center - eye).to_track_quat("-Z", "Y").to_euler()
        filename = f"hip-frame-overlay.{name}.png"
        render(scene, out / filename)
        manifest.append({"file": filename, "view": name, "green": "Poser deformation pivot",
                         "blue": "Blender metarig thigh head", "red": "Aetheris seam-derived socket proxy",
                         "claim": "Neutral frame/center overlay only; not a source posed-deformation render."})
    (out / "render-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
