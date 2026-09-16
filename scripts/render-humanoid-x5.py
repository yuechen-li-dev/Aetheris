"""Render X5 source/canonical rest-frame overlays and the required pose-review corpus."""
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
    if delta.length < 1e-8:
        return
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=delta.length, location=(first + second) / 2)
    obj = bpy.context.object
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(mat)


def point(matrix):
    return Vector((matrix["m41"] / 1000, matrix["m42"] / 1000, matrix["m43"] / 1000))


def axis(matrix, row):
    return Vector((matrix[f"m{row}1"], matrix[f"m{row}2"], matrix[f"m{row}3"]))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out-dir", default="artifacts/local/humanoid-x5")
    parser.add_argument("--rig", default="fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    out, rig_path = Path(args.out_dir), Path(args.rig)
    rig = json.loads(rig_path.read_text(encoding="utf-8"))
    source = {joint["sourceJointId"]: joint for joint in rig["sourceJoints"]}
    majors = {"LeftHip", "RightHip", "LeftKnee", "RightKnee", "LeftAnkle", "RightAnkle",
              "LeftShoulder", "RightShoulder", "LeftElbow", "RightElbow", "LeftWrist", "RightWrist"}

    scene = setup()
    body = obj_mesh(out / "neutral.screened.obj")
    body.data.materials.clear()
    body.data.materials.append(material("Neutral Antonia", (.36, .39, .43)))
    blue = material("Source metarig frames", (.10, .42, 1.0))
    green = material("Canonical mapped centers", (.15, 1.0, .28))
    axes = [material("Canonical X", (1, .15, .15)), material("Canonical Y", (.15, 1, .15)), material("Canonical Z", (.18, .35, 1))]
    maximum_center_delta = 0.0
    for mapping in rig["canonicalJoints"]:
        if mapping["canonicalJoint"] not in majors:
            continue
        imported = source[mapping["sourceJointId"]]
        source_origin, canonical_origin = point(imported["globalRestCanonical"]), point(mapping["globalRestCanonical"])
        maximum_center_delta = max(maximum_center_delta, (source_origin - canonical_origin).length * 1000)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=.014, location=source_origin)
        bpy.context.object.data.materials.append(blue)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=.009, location=canonical_origin)
        bpy.context.object.data.materials.append(green)
        for row, mat in zip((1, 2, 3), axes):
            direction = axis(mapping["globalRestCanonical"], row).normalized()
            cylinder_between(canonical_origin, canonical_origin + direction * .055, .0018, mat)

    center = Vector((0, 0, .9))
    scene.camera.data.ortho_scale = 2.0
    scene.camera.location = (0, 4, 1.05)
    scene.camera.rotation_euler = (center - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
    render(scene, out / "reference-frame-overlay.front.png")
    scene.camera.location = (3, 0, 1.05)
    scene.camera.rotation_euler = (center - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
    render(scene, out / "reference-frame-overlay.side.png")

    review = ["neutral", "hip-flexion-45", "hip-flexion-70", "hip-flexion-90", "hip-abduction-30",
              "hip-abduction-45", "knee-flexion-90", "shoulder-abduction-60", "shoulder-abduction-90", "elbow-flexion-90"]
    manifest = []
    for name in review:
        choices = list(out.glob(name + ".*.obj")) if not (out / (name + ".screened.obj")).exists() else [out / (name + ".screened.obj")]
        choices = [path for path in choices if ".diagnostic." not in path.name]
        if not choices:
            choices = [out / (name + ".failed-diagnostic.obj")]
        scene = setup()
        obj_mesh(choices[0])
        filename = name + ".png"
        render(scene, out / filename)
        manifest.append({"pose": name, "mesh": choices[0].name, "render": filename})
    (out / "visual-review-manifest.json").write_text(json.dumps({
        "sourceCanonicalMaximumCenterDeltaMm": maximum_center_delta,
        "overlayLegend": {"blue": "source metarig center", "green": "canonical mapped center", "rgbAxes": "canonical rest basis"},
        "poses": manifest,
        "reviewStatus": "Pending human classification"
    }, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
