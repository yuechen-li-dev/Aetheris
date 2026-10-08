"""Neutral before/after and retained local Genesis reference renders.

The reference is rendered separately and is never saved into the open character asset.
Its fixed neutral scale is reused for every pose; posed silhouettes are not rescaled.
"""

import importlib
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils import Quaternion

sys.path.insert(0, str(Path(__file__).parent))
build = importlib.import_module("build-antonia-gameplay")
h = build.h
ROOT = build.ROOT


def read_reference(path):
    vertices = []
    faces = []
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith("v "):
            x, y, z = map(float, line.split()[1:4])
            vertices.append((-x, z, y))
        elif line.startswith("f "):
            faces.append(tuple(int(value.split("/")[0]) - 1 for value in line.split()[1:]))
    return np.asarray(vertices), faces


def main():
    out = build.study.output_path(ROOT / "artifacts/local/humanoid-production/comparison")
    surface, rig, body, rest, triangles, scene, field, mapping = build.study.setup()
    h.assign(body, field)
    transforms = {case["name"]: case for case in h.read(out.parent / "pose-transforms.json")}
    poses = ("hip90", "knee90", "elbow120", "shoulder120")
    for pose in poses:
        build.rest_study.apply_semantic(rig, mapping, [])
        for rotation in transforms[pose]["localRotations"]:
            rig.pose.bones[rotation["joint"]].rotation_quaternion = Quaternion(
                (rotation["w"], rotation["x"], rotation["y"], rotation["z"]))
        bpy.context.view_layer.update()
        h.render(scene, body, out / f"before--{pose}.png")
    reference_dir = ROOT / "artifacts/local/humanoid-rest-x2/genesis-local"
    neutral, neutral_faces = read_reference(reference_dir / "canonical-apose.obj")
    scale = 1.75 / np.ptp(neutral[:, 2])
    offset = np.array(((neutral[:, 0].min() + neutral[:, 0].max()) / 2,
                       (neutral[:, 1].min() + neutral[:, 1].max()) / 2,
                       neutral[:, 2].min()))
    for pose in poses:
        points, faces = read_reference(reference_dir / f"{pose}.obj")
        points = (points - offset) * scale
        reference = h.mesh("Local Genesis reference " + pose, points, faces,
                           h.collection("LOCAL_REFERENCE_ONLY"))
        h.render(scene, reference, out / f"genesis--{pose}.png")
        bpy.data.objects.remove(reference, do_unlink=True)
    h.write(out / "reference-evidence.json", dict(
        role="Retained local Genesis 9 visual reference; no geometry, weights or deltas transferred",
        sourceNeutralSha256=h.sha(reference_dir / "canonical-apose.obj"),
        referencePoses={pose: h.sha(reference_dir / f"{pose}.obj") for pose in poses},
        fixedRestScaleToMetres=scale,
        hip90Exception="Retained Genesis request reaches 87.519 degrees; this column is not numerical pose parity",
        proprietaryPayloadEmbedded=False,
    ))


if __name__ == "__main__":
    main()
