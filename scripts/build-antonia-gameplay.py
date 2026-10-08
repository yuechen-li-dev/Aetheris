"""Build the open Antonia gameplay candidate and independently replay Aetheris poses.

Uses the existing admitted Antonia geometry, never Genesis geometry or deltas.
Generated meshes, binary parity samples and render evidence stay in artifacts/local.
"""

import argparse
import importlib
import json
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils import Quaternion, Vector

sys.path.insert(0, str(Path(__file__).parent))
study = importlib.import_module("humanoid-production-study")
rest_study = study.rest_study
h = study.helpers
ROOT = study.ROOT


def selected_weights(surface, rest, rig, field):
    edited = study.anatomical_weights(field, surface, rest, rig, .45, .16, elbow_width=.10)
    edited = study.metrics.brush_gain(edited, rest, rig, "Shoulder", .16, 1.2)
    return study.symmetric_weights(edited, surface)


def corrective_shapes(rest, rig):
    result = []
    for side in ("Left", "Right"):
        sign = -1 if side == "Left" else 1
        center = np.array((sign * .09, .065, rig.data.bones[side + "Hip"].head_local.z + .035))
        radius = np.array((.09, .075, .085)) * 1.25
        distance = (rest - center) / radius
        influence = np.exp(-np.sum(distance ** 2, axis=1) * 2)
        delta = influence[:, None] * np.array((0, -.01, -.017))
        delta[np.linalg.norm(delta, axis=1) < 1e-6] = 0
        result.append((side, delta))
    return result


def pose_corpus():
    result = study.corpus()
    for side in ("Left", "Right"):
        for angle in (-20, 20):
            result.append(dict(name=f"{side.lower()}-hip-stride-{angle}", requested=[
                dict(joint=side + "Hip", flexionDegrees=angle),
            ]))
        for angle in (0, 15, 45):
            result.append(dict(name=f"{side.lower()}-shoulder-low-{angle}", requested=[
                dict(joint=side + "Shoulder", abductionDegrees=angle),
            ]))
        for angle in (-25, 30, 60):
            result.append(dict(name=f"{side.lower()}-shoulder-flex-{angle}", requested=[
                dict(joint=side + "Shoulder", flexionDegrees=angle, abductionDegrees=35),
            ]))
        for angle in (65, 105):
            result.append(dict(name=f"{side.lower()}-elbow-{angle}", requested=[
                dict(joint=side + "Shoulder", abductionDegrees=35),
                dict(joint=side + "Elbow", flexionDegrees=angle),
            ]))
        for angle in (75, 110):
            result.append(dict(name=f"{side.lower()}-shoulder-{angle}", requested=[
                dict(joint=side + "Shoulder", abductionDegrees=angle),
            ]))
    result.append(dict(name="walking-bilateral", requested=[
        dict(joint="LeftHip", flexionDegrees=30),
        dict(joint="RightHip", flexionDegrees=-20),
        dict(joint="LeftKnee", flexionDegrees=35),
        dict(joint="RightKnee", flexionDegrees=5),
        dict(joint="LeftShoulder", flexionDegrees=-20, abductionDegrees=20),
        dict(joint="RightShoulder", flexionDegrees=20, abductionDegrees=20),
        dict(joint="LeftElbow", flexionDegrees=20),
        dict(joint="RightElbow", flexionDegrees=20),
    ]))
    result.append(dict(name="aim-two-handed", requested=[
        dict(joint="LeftShoulder", flexionDegrees=65, abductionDegrees=15),
        dict(joint="RightShoulder", flexionDegrees=65, abductionDegrees=15),
        dict(joint="LeftElbow", flexionDegrees=60),
        dict(joint="RightElbow", flexionDegrees=60),
    ]))
    for case in result:
        case["role"] = "qualification"
        if any(request["joint"].endswith("Hip") and request.get("flexionDegrees", 0) > 90
               for request in case["requested"]):
            case["role"] = "stress"
    return result


def inputs(out):
    surface, rig, body, rest, triangles, scene, field, mapping = study.setup()
    edited = selected_weights(surface, rest, rig, field)
    baseline = h.read(ROOT / "artifacts/local/humanoid-rest-x2/selected-weights.json")
    portable = dict(baseline)
    portable["skinning"] = "dual-quaternion"
    portable["maxInfluences"] = max(map(len, edited))
    portable["provenance"] = dict(
        source="Original open Antonia base geometry; corrected A-pose weights and authored hip shapes",
        steps=["symmetry-pair averaging", "central pelvis retention strength 0.45",
               "knee smooth transition half-width 160 mm", "elbow smooth transition half-width 100 mm",
               "shoulder support gain 1.2 radius 160 mm with bilateral averaging",
               "dual-quaternion skinning", "bilateral pre-skin hip corrective at 30..90 degrees"],
        scriptSha256=h.sha(__file__), weightScriptSha256=h.sha(study.__file__),
        blender=bpy.app.version_string, proprietaryGeometryOrDeltasUsed=False,
    )
    portable["vertices"] = [dict(vertexId=vertex["id"], weights=[
        dict(boneId=name, weight=value) for name, value in sorted(row.items())
    ]) for vertex, row in zip(surface["vertices"], edited)]
    h.write(out / "weights.json", portable)
    correctives = []
    for side, delta in corrective_shapes(rest, rig):
        correctives.append(dict(
            id=f"hip-flexion-volume.{side.lower()}", joint=side + "Hip",
            startFlexionDegrees=30, fullFlexionDegrees=90,
            vertices=[dict(vertexIndex=index, deltaMm=dict(
                x=float(point[0] * 1000), y=float(point[1] * 1000), z=float(point[2] * 1000)))
                for index, point in enumerate(delta) if np.linalg.norm(point) > 0],
        ))
    h.write(out / "correctives.json", correctives)
    h.write(out / "pose-corpus.json", pose_corpus())
    h.write(out / "recipe.json", dict(
        schema="antonia.gameplay-recipe.v1", restPoseId=baseline["restPoseId"],
        sourceTopologyId=surface["topologyId"], connectivityHash=surface["connectivityHash"],
        originalNoticeSha256=h.sha(ROOT / "fixtures/Canonical/Humanoid/antonia-original-LICENSE.txt"),
        sourceCandidateSha256=h.sha(ROOT / "artifacts/local/humanoid-x1/antonia-adoption-candidate.json"),
        baselineWeightsSha256=h.sha(ROOT / "artifacts/local/humanoid-rest-x2/selected-weights.json"),
        weightRecipe=portable["provenance"],
        correctiveRecipe=dict(centerXmm=90, centerYmm=65, centerAboveHipMm=35,
            gaussianRadiiMm=[112.5, 93.75, 106.25], deltaMm=[0, -10, -17],
            sparseCutoffMm=.001, activation="smoothstep((absolute flexion - 30) / 60)"),
        intendedEnvelope="Hip flexion -20..90; knee 0..110; elbow 0..120; shoulder abduction 0..120; tested flexion and combined poses",
        canonicalPromotion=False, exportWarning="Ordinary glTF export does not preserve dual-quaternion skinning",
    ))
    print("ANTONIA_GAMEPLAY_INPUTS_READY", out, flush=True)


def add_shape_keys(body, rest, shapes):
    body.shape_key_add(name="Basis")
    for side, delta in shapes:
        key = body.shape_key_add(name=f"hip-flexion-volume.{side.lower()}")
        key.data.foreach_set("co", (rest + delta).reshape(-1))


def restore_quads(body, candidate):
    index_map = {source: target for target, source in enumerate(candidate["sourceVertexIndices"])}
    faces = [tuple(index_map[index] for index in polygon["sourceVertexIndices"])
             for polygon in candidate["sourcePolygons"]]
    mesh = bpy.data.meshes.new("Antonia original quad topology")
    mesh.from_pydata([tuple(vertex.co) for vertex in body.data.vertices], [], faces)
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    preview = body.copy()
    preview.data = mesh
    preview.name = "ANTONIA_EDITABLE_QUADS"
    bpy.context.scene.collection.objects.link(preview)
    preview.hide_render = False
    preview.hide_set(False)
    return preview


def materials(body, candidate):
    material_specs = [
        ("Skin", (.42, .23, .15, 1), .48),
        ("Lips", (.34, .095, .07, 1), .4),
        ("Nails", (.48, .31, .25, 1), .3),
        ("Sclera", (.8, .83, .76, 1), .22),
        ("Iris", (.035, .11, .09, 1), .25),
        ("Pupil", (.002, .002, .002, 1), .2),
        ("Cornea", (.8, .85, .9, 1), .08),
    ]
    for name, color, roughness in material_specs:
        material = bpy.data.materials.new("Antonia." + name)
        material.diffuse_color = color
        material.use_nodes = True
        shader = material.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = color
        shader.inputs["Roughness"].default_value = roughness
        if name == "Cornea":
            shader.inputs["Transmission Weight"].default_value = 1
            shader.inputs["IOR"].default_value = 1.376
        if name == "Skin":
            shader.inputs["Subsurface Weight"].default_value = .08
        body.data.materials.append(material)
    for polygon, source in zip(body.data.polygons, candidate["sourcePolygons"]):
        name = source["material"]
        index = 0
        if name == "lips":
            index = 1
        elif name.startswith("nails"):
            index = 2
        elif name.startswith("sclera"):
            index = 3
        elif name.startswith("iris"):
            index = 4
        elif name.startswith("pupil"):
            index = 5
        elif name.startswith("cornea"):
            index = 6
        polygon.material_index = index


def restore_source_uvs(body, candidate):
    path = ROOT / "artifacts/local/humanoid-x1/source/Antonia-1.2.obj"
    admission = h.read(ROOT / "fixtures/Canonical/Humanoid/antonia-original.sidecar.json")
    expected = next(item["sha256"] for item in admission["files"] if item["localName"] == path.name)
    if h.sha(path) != expected:
        raise RuntimeError("Original Antonia geometry hash mismatch before UV restoration.")
    uvs = []
    faces = []
    for line in path.read_text().splitlines():
        if line.startswith("vt "):
            uvs.append(tuple(float(value) for value in line.split()[1:3]))
        elif line.startswith("f "):
            faces.append([tuple(int(value) - 1 for value in token.split("/")[:2])
                          for token in line.split()[1:]])
    layer = body.data.uv_layers.new(name="Antonia original UV")
    for polygon, source in zip(body.data.polygons, candidate["sourcePolygons"]):
        face = faces[source["sourceFaceIndex"]]
        if [corner[0] for corner in face] != source["sourceVertexIndices"]:
            raise RuntimeError("Original UV face identity mismatch.")
        for loop_index, corner in zip(polygon.loop_indices, face):
            layer.data[loop_index].uv = uvs[corner[1]]


def replay(out):
    surface, rig, body, rest, triangles, scene, field, mapping = study.setup()
    edited = selected_weights(surface, rest, rig, field)
    h.assign(body, edited)
    body.modifiers[0].use_deform_preserve_volume = True
    shapes = corrective_shapes(rest, rig)
    add_shape_keys(body, rest, shapes)
    exact = {case["name"]: case for case in h.read(out / "pose-transforms.json")}
    output = out / "blender"
    output.mkdir(exist_ok=True)
    renders = out / "renders"
    renders.mkdir(exist_ok=True)
    records = []
    for case in pose_corpus():
        rest_study.apply_semantic(rig, mapping, [])
        transform = exact[case["name"]]
        for rotation in transform["localRotations"]:
            rig.pose.bones[rotation["joint"]].rotation_quaternion = Quaternion(
                (rotation["w"], rotation["x"], rotation["y"], rotation["z"]))
        for name, activation in transform["correctives"].items():
            body.data.shape_keys.key_blocks[name].value = activation
        bpy.context.view_layer.update()
        posed = h.positions(body)
        (posed * 1000).astype("<f4").tofile(output / f"{case['name']}.positions.f32")
        measured = study.metrics.measure(rest, posed, triangles,
            h.normal_transport(rig, edited, triangles))
        records.append(dict(pose=case["name"], role=case["role"], metrics=measured))
        if case["name"] in ("canonical-apose", "shoulder120", "elbow120", "hip90", "knee90", "seated", "squat", "reach"):
            h.render(scene, body, renders / f"candidate--{case['name']}.png")
        print(case["name"], round(measured["max"], 3), measured["reversalCount"], flush=True)
    rest_study.apply_semantic(rig, mapping, [])
    for key in body.data.shape_keys.key_blocks:
        key.value = 0
    body.hide_set(True)
    body.hide_render = True
    body.name = "ANTONIA_RUNTIME_BINDING_TRIANGLES"
    candidate = h.read(ROOT / "artifacts/local/humanoid-x1/antonia-adoption-candidate.json")
    preview = restore_quads(body, candidate)
    h.assign(preview, edited)
    add_shape_keys(preview, rest, shapes)
    materials(preview, candidate)
    restore_source_uvs(preview, candidate)
    subdivision = preview.modifiers.new("Optional Catmull-Clark finish", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    rig["asset_id"] = "antonia.gameplay-candidate.v1"
    rig["skinning"] = "dual-quaternion"
    rig["qualification"] = "runtime-evidence.json; hip100 remains an explicit stress specimen"
    for side in ("left", "right"):
        rig[f"hip_corrective_{side}"] = 0.0
        name = "hip-flexion-volume." + side
        for mesh in (preview, body):
            driver = mesh.data.shape_keys.key_blocks[name].driver_add("value").driver
            driver.type = "SCRIPTED"
            variable = driver.variables.new()
            variable.name = "amount"
            variable.type = "SINGLE_PROP"
            variable.targets[0].id = rig
            variable.targets[0].data_path = f'["hip_corrective_{side}"]'
            driver.expression = "amount"
    pose_index = []
    for index, case in enumerate(pose_corpus()):
        frame = 1 + index * 10
        transform = exact[case["name"]]
        rest_study.apply_semantic(rig, mapping, [])
        for rotation in transform["localRotations"]:
            rig.pose.bones[rotation["joint"]].rotation_quaternion = Quaternion(
                (rotation["w"], rotation["x"], rotation["y"], rotation["z"]))
        for bone in rig.pose.bones:
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        for side in ("left", "right"):
            rig[f"hip_corrective_{side}"] = float(transform["correctives"]["hip-flexion-volume." + side])
            rig.keyframe_insert(data_path=f'["hip_corrective_{side}"]', frame=frame)
        scene.timeline_markers.new(case["name"], frame=frame)
        pose_index.append(dict(frame=frame, pose=case["name"], role=case["role"]))
    scene.frame_end = pose_index[-1]["frame"]
    scene.frame_set(1)
    h.write(out / "pose-library.json", pose_index)
    subdivision.show_viewport = False
    library_parity = []
    for name in ("canonical-apose", "hip90", "seated", "elbow120", "squat"):
        entry = next(item for item in pose_index if item["pose"] == name)
        scene.frame_set(entry["frame"])
        bpy.context.view_layer.update()
        positions = h.positions(preview) * 1000
        expected = np.fromfile(output / f"{name}.positions.f32", dtype="<f4").reshape(-1, 3)
        maximum = float(np.max(np.linalg.norm(positions - expected, axis=1)))
        if maximum > .015:
            raise RuntimeError(f"Editable quad pose library does not reproduce runtime pose {name}: {maximum} mm")
        library_parity.append(dict(pose=name, maximumMm=maximum))
    subdivision.show_viewport = True
    scene.frame_set(1)
    scene.display.shading.color_type = "MATERIAL"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 1250
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.12, .14, .18, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .35
    for name, location, energy, size in (
        ("Soft key", (2.5, 3, 3.5), 450, 3),
        ("Soft fill", (-2, 1.5, 2.5), 250, 3),
        ("Rim", (0, -2.5, 3), 350, 2),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector((0, 0, 1.1)) - light.location).to_track_quat("-Z", "Y").to_euler()
    for pose in ("canonical-apose", "seated", "elbow120"):
        entry = next(item for item in pose_index if item["pose"] == pose)
        scene.frame_set(entry["frame"])
        h.render(scene, preview, renders / f"editable--{pose}.png")
    scene.frame_set(1)
    scene["authoring_notes"] = "Explicit corrective activations are supplied by the typed pose API; no embedded Python auto-execution. Do not export glTF and silently fall back to LBS."
    notice = bpy.data.texts.new("ANTONIA-LICENSE.txt")
    notice.write((ROOT / "fixtures/Canonical/Humanoid/antonia-original-LICENSE.txt").read_text())
    recipe = bpy.data.texts.new("GAMEPLAY-RECIPE.json")
    recipe.write((out / "recipe.json").read_text())
    h.active(preview)
    rig.select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out / "antonia-gameplay.blend"))
    h.write(out / "blender-evidence.json", dict(
        schema="antonia.gameplay-blender-replay.v1", cases=records,
        editableQuads=len(preview.data.polygons), runtimeTriangles=len(triangles),
        editablePoseLibraryParity=library_parity,
        uvLoops=len(preview.data.uv_layers.active.data),
        proprietaryPayloadsEmbedded=False,
    ))
    print("ANTONIA_GAMEPLAY_BLENDER_REPLAY_COMPLETE", out, flush=True)


def verify_saved(out):
    path = out / "antonia-gameplay.blend"
    bpy.ops.wm.open_mainfile(filepath=str(path), use_scripts=False)
    body = bpy.data.objects["ANTONIA_EDITABLE_QUADS"]
    subdivision = next(modifier for modifier in body.modifiers if modifier.type == "SUBSURF")
    subdivision.show_viewport = False
    entries = h.read(out / "pose-library.json")
    reports = []
    for name in ("canonical-apose", "hip90", "seated", "elbow120", "squat",
                 "left-hip-holdout-80", "walking-bilateral", "aim-two-handed"):
        entry = next(item for item in entries if item["pose"] == name)
        bpy.context.scene.frame_set(entry["frame"])
        bpy.context.view_layer.update()
        positions = h.positions(body) * 1000
        expected = np.fromfile(out / "blender" / f"{name}.positions.f32", dtype="<f4").reshape(-1, 3)
        maximum = float(np.max(np.linalg.norm(positions - expected, axis=1)))
        if maximum > .015:
            raise RuntimeError(f"Saved Blender pose does not reproduce {name}: {maximum} mm")
        reports.append(dict(pose=name, maximumMm=maximum))
    h.write(out / "saved-blend-evidence.json", dict(
        accepted=True, blendSha256=h.sha(path), cases=reports,
        embeddedScriptExecution=False, vertices=len(body.data.vertices),
        quads=len(body.data.polygons), uvLoops=len(body.data.uv_layers.active.data),
    ))
    print("ANTONIA_SAVED_BLEND_VERIFIED", len(reports), "poses", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=str(ROOT / "artifacts/local/humanoid-production"))
    parser.add_argument("--phase", choices=("inputs", "replay", "verify"), required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    directory = study.output_path(args.out)
    if args.phase == "inputs":
        inputs(directory)
    elif args.phase == "replay":
        replay(directory)
    else:
        verify_saved(directory)
