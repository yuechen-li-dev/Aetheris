"""Reproducible Antonia deformation experiments. No proprietary source transfer."""

import argparse
import importlib
import json
from pathlib import Path
import sys
import time

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).parent))
rest_study = importlib.import_module("humanoid-rest-x2")
helpers = rest_study.h
metrics = rest_study.x1
ROOT = helpers.ROOT


def output_path(value):
    path = Path(value).resolve()
    if not path.is_relative_to((ROOT / "artifacts/local").resolve()):
        raise ValueError("Generated humanoid output must stay in artifacts/local.")
    path.mkdir(parents=True, exist_ok=True)
    return path


def setup():
    candidate, surface, artifact, evidence, vertices = rest_study.normalized_inputs()
    rig, body, rest, triangles, scene = rest_study.make_open_scene(
        surface, artifact, vertices
    )
    field = rest_study.portable_field(
        ROOT / "artifacts/local/humanoid-rest-x2/selected-weights.json"
    )
    helpers.assign(body, field)
    mapping = rest_study.role_map(rig)
    return surface, rig, body, rest, triangles, scene, field, mapping


def corpus():
    cases = rest_study.canonical_corpus()
    for side in ("Left", "Right"):
        for angle in (55, 80, 100):
            cases.append(dict(
                name=f"{side.lower()}-hip-holdout-{angle}",
                requested=[dict(joint=side + "Hip", flexionDegrees=angle)],
            ))
        for angle in (65, 110):
            cases.append(dict(
                name=f"{side.lower()}-knee-holdout-{angle}",
                requested=[dict(joint=side + "Knee", flexionDegrees=angle)],
            ))
    cases.append(dict(name="seated", requested=[
        dict(joint="LeftHip", flexionDegrees=90),
        dict(joint="RightHip", flexionDegrees=90),
        dict(joint="LeftKnee", flexionDegrees=90),
        dict(joint="RightKnee", flexionDegrees=90),
    ]))
    cases.append(dict(name="squat", requested=[
        dict(joint="LeftHip", flexionDegrees=80, abductionDegrees=15),
        dict(joint="RightHip", flexionDegrees=80, abductionDegrees=15),
        dict(joint="LeftKnee", flexionDegrees=110),
        dict(joint="RightKnee", flexionDegrees=110),
    ]))
    return cases


def smoothstep(value):
    t = max(0.0, min(1.0, value))
    return t * t * (3 - 2 * t)


def symmetric_weights(field, surface):
    result = [dict(row) for row in field]
    def mirror(name):
        if name.startswith("Left"):
            return "Right" + name[4:]
        if name.startswith("Right"):
            return "Left" + name[5:]
        return name
    for index, vertex in enumerate(surface["vertices"]):
        partner = vertex.get("symmetryPartnerIndex")
        if partner is None or partner < index:
            continue
        reflected = {mirror(name): weight for name, weight in field[partner].items()}
        names = sorted(set(field[index]) | set(reflected))
        averaged = {name: .5 * (field[index].get(name, 0) + reflected.get(name, 0))
                    for name in names}
        result[index] = averaged
        result[partner] = {mirror(name): weight for name, weight in averaged.items()}
    return rest_study.normalize(result)


def anatomical_weights(field, surface, rest, rig, hip_strength, hinge_width,
                       anterior_strength=0, anterior_width=.12, elbow_width=0):
    result = symmetric_weights(field, surface)
    hip_height = rig.data.bones["LeftHip"].head_local.z
    for index, (point, row) in enumerate(zip(rest, result)):
        center_factor = 1 - smoothstep(abs(point[0]) / .095)
        height_factor = np.exp(-((point[2] - (hip_height - .065)) / .12) ** 4)
        anterior = anterior_strength * smoothstep((point[1] + .045) / .10)
        anterior *= smoothstep((point[2] - (hip_height - .025)) / anterior_width)
        anchor = 1 - (1 - hip_strength * center_factor * height_factor) * (1 - anterior)
        transferred = 0.0
        for name in ("LeftHip", "RightHip"):
            amount = row.get(name, 0) * anchor
            row[name] = row.get(name, 0) - amount
            transferred += amount
        row["Pelvis"] = row.get("Pelvis", 0) + transferred
        joints = [(side + "Knee", side + "Hip", side + "Ankle", hinge_width)
                  for side in ("Left", "Right")]
        if elbow_width > 0:
            joints.extend((side + "Elbow", side + "Shoulder", side + "Wrist", elbow_width)
                          for side in ("Left", "Right"))
        for joint, parent, distal_joint, width in joints:
            center = np.array(rig.data.bones[joint].head_local)
            distal = np.array(rig.data.bones[distal_joint].head_local)
            direction = (distal - center) / np.linalg.norm(distal - center)
            longitudinal = float(np.dot(point - center, direction))
            pair = row.get(joint, 0) + row.get(parent, 0)
            distance = np.linalg.norm(point - center)
            if pair < .5 or distance > .25:
                continue
            replacement = smoothstep((longitudinal + width) / (2 * width))
            fade = 1 - smoothstep((distance - .15) / .10)
            current = row.get(joint, 0) / pair
            child = current + fade * (replacement - current)
            row[joint] = pair * child
            row[parent] = pair * (1 - child)
        result[index] = row
    return rest_study.normalize(result)


def smooth_modifier(body, rig, rest, iterations, factor, scale):
    centers = [np.array(rig.data.bones[side + kind].head_local)
               for side in ("Left", "Right")
               for kind in ("Hip", "Knee", "Shoulder", "Elbow")]
    radii = [.22, .13, .18, .12] * 2
    influence = np.zeros(len(rest))
    for center, radius in zip(centers, radii):
        distance = np.linalg.norm(rest - center, axis=1) / radius
        influence = np.maximum(influence, np.clip(1 - distance ** 2, 0, 1) ** 2)
    group = body.vertex_groups.new(name="REST_AWARE_JOINT_FINISH")
    for index, value in enumerate(influence):
        if value > 1e-6:
            group.add([index], float(value), "REPLACE")
    modifier = body.modifiers.new("Rest-aware joint correction", "CORRECTIVE_SMOOTH")
    modifier.vertex_group = group.name
    modifier.factor = factor
    modifier.iterations = iterations
    modifier.scale = scale
    modifier.smooth_type = "LENGTH_WEIGHTED"
    modifier.use_pin_boundary = True
    modifier.rest_source = "ORCO"
    return modifier


def run(args):
    out = output_path(args.out)
    surface, rig, body, rest, triangles, scene, field, mapping = setup()
    cases = corpus()
    armature = next(mod for mod in body.modifiers if mod.type == "ARMATURE")
    corrective_specs = {}
    trials = [("baseline-lbs", False, None, field), ("volume-dqs", True, None, field)]
    if args.phase == "selected":
        edited = anatomical_weights(field, surface, rest, rig, .5, .16)
        trials.append(("selected-anatomical-dqs", True, None, edited))
    elif args.phase == "elbow":
        for width in (.06, .08, .10, .12, .14):
            edited = anatomical_weights(field, surface, rest, rig, .45, .16, elbow_width=width)
            trials.append((f"dqs-elbow-{width}", True, None, edited))
        cases = [case for case in cases if case["name"] in (
            "elbow45", "elbow90", "elbow120", "reach", "shoulder120"
        )]
    elif args.phase in ("corrective", "corrective2", "corrective3", "corrective4"):
        edited = anatomical_weights(field, surface, rest, rig, .45, .16)
        trials.append(("anatomical-reference", True, None, edited))
        fronts = (-.02, -.01, .01, .02, .03)
        ups = (-.01, 0, .01)
        if args.phase == "corrective2":
            fronts = (-.03, -.02, -.01, 0, .01)
            ups = (-.015, -.02, -.03)
        if args.phase == "corrective3":
            fronts = (-.005, -.01, -.015)
            ups = (-.017, -.019, -.021)
        if args.phase == "corrective4":
            fronts = (-.01, -.02)
            ups = (-.02, -.025)
        for front in fronts:
            for up in ups:
                radii = (1.0,)
                if args.phase == "corrective3":
                    radii = (1.0, 1.25)
                if args.phase == "corrective4":
                    radii = (1.5, 1.75)
                for radius in radii:
                    name = f"hip-corrective-{front}-{up}-{radius}"
                    trials.append((name, True, None, edited))
                    corrective_specs[name] = (front, up, radius)
        cases = [case for case in cases if case["name"] in (
            "hip70", "hip90", "seated", "squat", "left-hip-holdout-100", "abduction45"
        )]
    elif args.phase == "hip-smooth":
        edited = anatomical_weights(field, surface, rest, rig, .45, .16)
        centers = [np.array(rig.data.bones[side + "Hip"].head_local)
                   for side in ("Left", "Right")]
        mask = np.minimum(*[np.linalg.norm(rest - center, axis=1)
                            for center in centers]) < .22
        for iterations in (1, 3, 6, 12, 24):
            smoothed = metrics.local_smooth(body, edited, mask, iterations, .5)
            smoothed = symmetric_weights(smoothed, surface)
            trials.append((f"dqs-hip-field-smooth-{iterations}", True, None, smoothed))
        cases = [case for case in cases if case["name"] in (
            "hip70", "hip90", "seated", "squat", "left-hip-holdout-100", "abduction45"
        )]
    elif args.phase == "fascia":
        for strength in (.35, .65, 1.0):
            for width in (.08, .12, .16):
                edited = anatomical_weights(field, surface, rest, rig, .45, .16, strength, width)
                trials.append((f"dqs-fascia-{strength}-{width}", True, None, edited))
        cases = [case for case in cases if case["name"] in (
            "hip70", "hip90", "seated", "squat", "left-hip-holdout-100", "abduction45"
        )]
    elif args.phase == "hip":
        for strength in (.15, .25, .35, .45, .55, .65):
            edited = anatomical_weights(field, surface, rest, rig, strength, .16)
            trials.append((f"dqs-pelvis-{strength}", True, None, edited))
        cases = [case for case in cases if case["name"] in (
            "hip70", "hip90", "seated", "squat", "left-hip-holdout-100", "abduction45"
        )]
    elif args.phase == "smoothing":
        for volume in (False, True):
            for iterations in (5, 15, 30):
                name = f"{'dqs' if volume else 'lbs'}-rest-smooth-{iterations}"
                trials.append((name, volume, (iterations, 1.0, 1.0), field))
    else:
        for strength in (.5, 1.0):
            for width in (.08, .12, .16):
                edited = anatomical_weights(field, surface, rest, rig, strength, width)
                for volume in (False, True):
                    name = f"{'dqs' if volume else 'lbs'}-anatomy-{strength}-{width}"
                    trials.append((name, volume, None, edited))
        cases = [case for case in cases if case["name"] in (
            "canonical-apose", "hip70", "hip90", "knee90", "seated", "squat",
            "left-hip-holdout-100", "left-knee-holdout-110", "abduction45",
        )]
    records = []
    for name, volume, smoothing, trial_field in trials:
        started = time.perf_counter()
        armature.use_deform_preserve_volume = volume
        helpers.assign(body, trial_field)
        modifier = None
        if smoothing:
            modifier = smooth_modifier(body, rig, rest, *smoothing)
        result = []
        for case in cases:
            residuals = rest_study.apply_semantic(rig, mapping, case["requested"])
            sculpted = rest.copy()
            if name in corrective_specs:
                front, up, radius = corrective_specs[name]
                for side in ("Left", "Right"):
                    request = next((r for r in case["requested"]
                                    if r["joint"] == side + "Hip"), {})
                    activation = smoothstep((request.get("flexionDegrees", 0) - 30) / 60)
                    sign = -1 if side == "Left" else 1
                    center = np.array((sign * .09, .065,
                                       rig.data.bones[side + "Hip"].head_local.z + .035))
                    distance = (rest - center) / (np.array((.09, .075, .085)) * radius)
                    influence = np.exp(-np.sum(distance ** 2, axis=1) * 2)
                    sculpted[:, 1] += front * activation * influence
                    sculpted[:, 2] += up * activation * influence
            body.data.vertices.foreach_set("co", sculpted.reshape(-1))
            body.data.update()
            bpy.context.view_layer.update()
            posed = helpers.positions(body)
            measured = metrics.measure(
                rest, posed, triangles,
                helpers.normal_transport(rig, trial_field, triangles), True,
            )
            result.append(dict(
                pose=case["name"], requested=case["requested"], metrics=measured,
                maximumSemanticResidualDegrees=max(
                    [r["maximumAbsoluteDegrees"] for r in residuals] or [0]
                ),
            ))
            if args.render and case["name"] in (
                "canonical-apose", "hip90", "knee90", "shoulder120", "seated", "squat"
            ):
                helpers.render(scene, body, out / f"{name}--{case['name']}.png")
        rest_study.apply_semantic(rig, mapping, [])
        body.data.vertices.foreach_set("co", rest.reshape(-1))
        body.data.update()
        record = dict(name=name, cases=result, seconds=time.perf_counter() - started)
        records.append(record)
        helpers.write(out / "trials.json", dict(
            schema="aetheris.humanoid.production-study.v1",
            blender=bpy.app.version_string,
            baselineWeightsSha256=helpers.sha(
                ROOT / "artifacts/local/humanoid-rest-x2/selected-weights.json"
            ),
            trials=records,
        ))
        print(name, [(case["pose"], round(case["metrics"]["max"], 3),
                      case["metrics"]["reversalCount"])
                     for case in result if case["pose"] in ("hip90", "knee90", "seated", "squat")],
              flush=True)
        if modifier:
            group = body.vertex_groups.get(modifier.vertex_group)
            body.modifiers.remove(modifier)
            body.vertex_groups.remove(group)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=str(ROOT / "artifacts/local/humanoid-production-study"))
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--phase", choices=("smoothing", "weights", "selected", "hip", "fascia", "hip-smooth", "corrective", "corrective2", "corrective3", "corrective4", "elbow"), default="smoothing")
    arguments = sys.argv[sys.argv.index("--") + 1:]
    run(parser.parse_args(arguments))
