"""Bounded shoulder weight finish against the real Aetheris pose replay."""

import importlib
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion

sys.path.insert(0, str(Path(__file__).parent))
build = importlib.import_module("build-antonia-gameplay")
study = build.study
h = build.h
ROOT = build.ROOT


def main():
    out = study.output_path(ROOT / "artifacts/local/humanoid-production-shoulder")
    surface, rig, body, rest, triangles, scene, field, mapping = study.setup()
    selected = study.anatomical_weights(
        field, surface, rest, rig, .45, .16, elbow_width=.10)
    body.modifiers[0].use_deform_preserve_volume = True
    transforms = h.read(ROOT / "artifacts/local/humanoid-production/pose-transforms.json")
    cases = [case for case in transforms if case["name"] in (
        "aim-two-handed", "shoulder120", "reach", "walking-bilateral",
        "left-shoulder-flex-60", "left-shoulder-low-0",
    )]
    results = []
    for gain in (.8, .9, .95, 1.0, 1.05, 1.1, 1.2):
        edited = study.metrics.brush_gain(selected, rest, rig, "Shoulder", .16, gain)
        edited = study.symmetric_weights(edited, surface)
        h.assign(body, edited)
        reports = []
        for case in cases:
            study.rest_study.apply_semantic(rig, mapping, [])
            for rotation in case["localRotations"]:
                rig.pose.bones[rotation["joint"]].rotation_quaternion = Quaternion(
                    (rotation["w"], rotation["x"], rotation["y"], rotation["z"]))
            bpy.context.view_layer.update()
            measured = study.metrics.measure(rest, h.positions(body), triangles,
                h.normal_transport(rig, edited, triangles))
            reports.append(dict(pose=case["name"], metrics=measured))
        results.append(dict(gain=gain, cases=reports))
        print(gain, [(case["pose"], round(case["metrics"]["max"], 3),
                      case["metrics"]["reversalCount"]) for case in reports], flush=True)
    h.write(out / "trials.json", results)


if __name__ == "__main__":
    main()
