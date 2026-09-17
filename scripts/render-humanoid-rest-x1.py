"""Render canonical REST-X1 meshes and a joint-center/axis overlay with one matched camera."""
import argparse
import html
import json
import runpy
from pathlib import Path
import sys

import bpy
from mathutils import Vector

helpers = runpy.run_path(str(Path(__file__).with_name("render-humanoid-x2.py")))
setup, obj_mesh, render, material = (helpers[name] for name in ("setup", "obj_mesh", "render", "material"))


def point(matrix):
    return Vector((matrix["m41"] / 1000, matrix["m42"] / 1000, matrix["m43"] / 1000))


def axis(matrix, row):
    return Vector((matrix[f"m{row}1"], matrix[f"m{row}2"], matrix[f"m{row}3"]))


def cylinder(first, second, radius, mat):
    delta = second - first
    if delta.length < 1e-8:
        return
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=delta.length, location=(first + second) / 2)
    obj = bpy.context.object
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(mat)


def matched_scene(mesh_path):
    scene = setup()
    body = obj_mesh(mesh_path)
    body.data.materials.clear()
    body.data.materials.append(material("REST-X1 body", (.42, .45, .50)))
    scene.camera.data.ortho_scale = 2.05
    scene.camera.location = (0, 4, 1.02)
    center = Vector((0, 0, .9))
    scene.camera.rotation_euler = (center - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
    return scene


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out-dir", default="artifacts/local/humanoid-rest-x1")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    out = Path(args.out_dir)
    evidence = json.loads((out / "evidence.json").read_text(encoding="utf-8"))
    renders = []
    for mesh in [out / "source-native-rest.obj", out / "canonical-apose.obj"] + [out / case["obj"] for case in evidence["cases"]]:
        scene = matched_scene(mesh)
        image = out / (mesh.stem + ".png")
        render(scene, image)
        renders.append((mesh.stem, image.name))

    scene = matched_scene(out / "canonical-apose.obj")
    joints = evidence["normalizedJoints"]
    bone_mat = material("Canonical skeleton", (.08, .20, .25))
    axes = [material("Anatomical X", (1, .12, .12)), material("Forward Y", (.12, 1, .20)), material("Up Z", (.15, .35, 1))]
    major = {"LeftShoulder", "RightShoulder", "LeftElbow", "RightElbow", "LeftHip", "RightHip", "LeftKnee", "RightKnee"}
    for joint in joints:
        origin = point(joint["globalBind"])
        if joint["parentIndex"] is not None:
            cylinder(origin, point(joints[joint["parentIndex"]]["globalBind"]), .003, bone_mat)
        if joint["kind"] in major:
            bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=.011, location=origin)
            bpy.context.object.data.materials.append(bone_mat)
            for row, mat in zip((1, 2, 3), axes):
                cylinder(origin, origin + axis(joint["globalBind"], row).normalized() * .055, .0018, mat)
    render(scene, out / "canonical-apose-skeleton-overlay.png")

    rows = "\n".join(f"<tr><th>{html.escape(name)}</th><td><img src='{html.escape(image)}'></td></tr>" for name, image in renders)
    (out / "gallery.html").write_text("""<!doctype html><meta charset='utf-8'><title>HUMANOID-REST-X1</title>
<style>body{font:15px system-ui;background:#171a1f;color:#eef;margin:24px}table{border-collapse:collapse}th{vertical-align:top;text-align:left;padding:12px}td{padding:8px}img{width:420px;border:1px solid #49515c}</style>
<h1>HUMANOID-REST-X1 — semantic A-pose evidence</h1><p>Matched camera, scale, ground, and orientation. Labels name absolute anatomical targets.</p><table>""" + rows + "</table>", encoding="utf-8")


if __name__ == "__main__":
    main()
