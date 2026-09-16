"""Extract the pinned Antonia Blender metarig into Aetheris canonical rest frames.

This reads armature data blocks only. It never generates Rigify controls, imports source
weights, evaluates drivers, or executes embedded scripts. The output is self-contained
runtime data with explicit source mapping evidence.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector, kdtree


ROOT = Path(__file__).resolve().parents[1]


SOURCE_NAMES = {
    "Root": None,
    "Pelvis": "spine", "SpineLower": "spine.001", "SpineMid": "spine.002",
    "Chest": "spine.003", "Neck": "spine.005", "Head": "spine.006",
    "LeftClavicle": "shoulder.L", "LeftShoulder": "upper_arm.L", "LeftElbow": "forearm.L", "LeftWrist": "hand.L",
    "LeftHip": "thigh.L", "LeftKnee": "shin.L", "LeftAnkle": "foot.L", "LeftToeBase": "toe.L",
    "RightClavicle": "shoulder.R", "RightShoulder": "upper_arm.R", "RightElbow": "forearm.R", "RightWrist": "hand.R",
    "RightHip": "thigh.R", "RightKnee": "shin.R", "RightAnkle": "foot.R", "RightToeBase": "toe.R",
    "LeftEye": "eye.L", "RightEye": "eye.R",
}
for side, suffix in (("Left", "L"), ("Right", "R")):
    SOURCE_NAMES.update({
        side + "ThumbMetacarpal": f"thumb.01.{suffix}", side + "ThumbProximal": f"thumb.02.{suffix}", side + "ThumbDistal": f"thumb.03.{suffix}",
        side + "IndexProximal": f"f_index.01.{suffix}", side + "IndexIntermediate": f"f_index.02.{suffix}", side + "IndexDistal": f"f_index.03.{suffix}",
        side + "MiddleProximal": f"f_middle.01.{suffix}", side + "MiddleIntermediate": f"f_middle.02.{suffix}", side + "MiddleDistal": f"f_middle.03.{suffix}",
        side + "RingProximal": f"f_ring.01.{suffix}", side + "RingIntermediate": f"f_ring.02.{suffix}", side + "RingDistal": f"f_ring.03.{suffix}",
        side + "LittleProximal": f"f_pinky.01.{suffix}", side + "LittleIntermediate": f"f_pinky.02.{suffix}", side + "LittleDistal": f"f_pinky.03.{suffix}",
    })


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def point(value):
    return {"x": float(value.x), "y": float(value.y), "z": float(value.z)}


def matrix_json(value):
    # Blender uses column vectors. System.Numerics uses row vectors, so transpose
    # the rotation/basis and place translation in row four.
    return {f"m{row + 1}{column + 1}": float(value[column][row]) for row in range(4) for column in range(4)}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--candidate", default="artifacts/local/humanoid-x1/antonia-adoption-candidate.json")
    parser.add_argument("--char-blend", default="artifacts/local/humanoid-recon-x0/CharMorph-db/characters/antonia/char.blend")
    parser.add_argument("--metarig-blend", default="artifacts/local/humanoid-recon-x0/CharMorph-db/characters/antonia/metarig.blend")
    parser.add_argument("--output", default="fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    candidate_path = (ROOT / args.candidate).resolve()
    char_path = (ROOT / args.char_blend).resolve()
    metarig_path = (ROOT / args.metarig_blend).resolve()
    output = (ROOT / args.output).resolve()
    manifest = json.loads((ROOT / "docs/release/HUMANOID-RECON-X0.manifest.json").read_text(encoding="utf-8"))
    antonia = next(asset for asset in manifest["assets"] if asset["id"] == "antonia")
    for path, key in ((char_path, "char.blend"), (metarig_path, "metarig.blend")):
        if not path.is_file() or sha256(path) != antonia["filesSha256"][key]:
            raise RuntimeError(f"Missing or changed pinned Antonia input: {key}")
    candidate = json.loads(candidate_path.read_text(encoding="utf-8"))

    bpy.ops.wm.open_mainfile(filepath=str(char_path), load_ui=False, use_scripts=False)
    body = bpy.data.objects["cm_antonia"]
    canonical_positions = [Vector((item["position"]["x"], item["position"]["y"], item["position"]["z"]))
                           for item in candidate["sourcePoseSurface"]["vertices"]]
    source_positions = [vertex.co.copy() for vertex in body.data.vertices]
    scale = (max(p.z for p in canonical_positions) - min(p.z for p in canonical_positions)) / (max(p.z for p in source_positions) - min(p.z for p in source_positions))
    signs = Vector((-1.0, -1.0, 1.0))
    canonical_mid = Vector(tuple((min(p[i] for p in canonical_positions) + max(p[i] for p in canonical_positions)) / 2 for i in range(3)))
    source_mid = Vector(tuple((min(p[i] for p in source_positions) + max(p[i] for p in source_positions)) / 2 for i in range(3)))
    offset = Vector(tuple(canonical_mid[i] - signs[i] * source_mid[i] * scale for i in range(3)))

    def canonical_point(value):
        return Vector(tuple(signs[i] * value[i] * scale + offset[i] for i in range(3)))

    tree = kdtree.KDTree(len(source_positions))
    for index, value in enumerate(source_positions):
        tree.insert(canonical_point(value), index)
    tree.balance()
    errors, used = [], set()
    for value in canonical_positions:
        _, index, error = tree.find(value)
        errors.append(error); used.add(index)
    if len(used) != len(canonical_positions) or max(errors) > .001:
        raise RuntimeError("Reference-rig coordinate conversion did not reproduce the admitted topology.")

    with bpy.data.libraries.load(str(metarig_path), link=False) as (data_from, data_to):
        data_to.objects = list(data_from.objects)
    rigs = [obj for obj in data_to.objects if obj is not None and obj.type == "ARMATURE"]
    if len(rigs) != 1:
        raise RuntimeError(f"Expected exactly one Antonia metarig, found {len(rigs)}")
    rig = rigs[0]
    bone_by_name = {bone.name: bone for bone in rig.data.bones}
    missing = sorted(set(name for name in SOURCE_NAMES.values() if name) - set(bone_by_name))
    if missing:
        raise RuntimeError("Missing mapped source bones: " + ", ".join(missing))

    axis_conversion = Matrix.Diagonal(Vector((-1.0, -1.0, 1.0, 1.0)))
    source_joints = []
    source_global = {}
    for bone in sorted(rig.data.bones, key=lambda item: item.name):
        rotation = axis_conversion @ bone.matrix_local.to_4x4()
        rotation.translation = canonical_point(bone.head_local)
        source_global[bone.name] = rotation
        source_joints.append({
            "sourceJointId": bone.name,
            "parentSourceJointId": bone.parent.name if bone.parent else None,
            "headCanonicalMm": point(canonical_point(bone.head_local)),
            "tailCanonicalMm": point(canonical_point(bone.tail_local)),
            "globalRestCanonical": matrix_json(rotation),
            "sourceDeform": bool(bone.use_deform),
            "sourceSemanticName": bone.name,
        })
    extracted_hash = hashlib.sha256(json.dumps(source_joints, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()

    old_joints = candidate["sourcePoseSkeleton"]["joints"]
    old_kinds = [joint["kind"] for joint in old_joints]
    if len(old_kinds) != 55 or set(old_kinds) != set(SOURCE_NAMES):
        raise RuntimeError("X5 source mapping does not exactly cover the existing 55-joint canonical skeleton.")
    canonical_joints = []
    for index, old in enumerate(old_joints):
        kind = old["kind"]
        parent_kind = old_joints[old["parentIndex"]]["kind"] if old["parentIndex"] is not None else None
        source_name = SOURCE_NAMES[kind]
        collapsed = []
        if source_name is not None and parent_kind is not None and SOURCE_NAMES[parent_kind] is not None:
            cursor = bone_by_name[source_name].parent
            expected = SOURCE_NAMES[parent_kind]
            while cursor is not None and cursor.name != expected:
                collapsed.append(cursor.name)
                cursor = cursor.parent
            if cursor is None:
                raise RuntimeError(f"Source hierarchy for {kind} does not reach canonical parent {parent_kind}")
        if source_name is None:
            global_rest = Matrix.Identity(4)
            confidence = "SyntheticRoot"
            role = "canonical origin; no source anatomy claim"
        else:
            global_rest = source_global[source_name]
            confidence = "High" if not collapsed else "HighWithCollapsedHelpers"
            role = "source-derived canonical rest frame"
        canonical_joints.append({
            "canonicalJoint": kind, "canonicalParent": parent_kind, "sourceJointId": source_name,
            "mappingConfidence": confidence, "semanticRole": role,
            "collapsedSourceHelpers": collapsed, "globalRestCanonical": matrix_json(global_rest),
            "sourceCenterDeltaMm": 0.0, "sourceOrientationDeltaDegrees": 0.0, "parentRelativeDeltaMm": 0.0,
        })

    mapped = set(name for name in SOURCE_NAMES.values() if name)
    ignored = sorted(set(bone_by_name) - mapped)
    artifact = {
        "schema": "aetheris.humanoid.reference-rig.v1",
        "topologyId": candidate["sourcePoseSurface"]["topologyId"],
        "skeletonId": "aetheris.humanoid.antonia.reference-rig.v1",
        "restPoseId": "antonia.charmorph-metarig.rest.v1",
        "sourceRigType": "CharMorph Antonia Blender metarig; derived from the Antonia conversion package, not the original Poser CR2 evaluator",
        "sourceAssetSha256": sha256(char_path), "sourceMetarigSha256": sha256(metarig_path),
        "extractorSha256": sha256(Path(__file__)), "extractedFrameSha256": extracted_hash,
        "coordinateConversion": f"row-vector canonical = Blender via axis diag(-1,-1,+1), uniform {scale:.17g} mm/unit, translation ({offset.x:.17g},{offset.y:.17g},{offset.z:.17g}) mm; determinant positive",
        "sourceJoints": source_joints, "canonicalJoints": canonical_joints,
        "ignoredSourceJoints": ignored,
        "limitations": [
            "Rest-frame adoption only; Rigify generation, source weights, drivers, sliding joints and modifiers are not runtime dependencies.",
            "Canonical helper-free hierarchy intentionally collapses source palm/face/trunk helper paths recorded per mapping.",
            "The canonical Root is an Aetheris frame origin, not a copied anatomical joint.",
            "Facial rig qualification beyond the retained eye joints remains outside X5.",
        ],
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(artifact, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "sha256": sha256(output), "sourceBones": len(source_joints),
                      "mappedCanonicalJoints": len(canonical_joints), "ignoredSourceBones": len(ignored),
                      "maximumNeutralMappingErrorMm": max(errors)}, indent=2))


if __name__ == "__main__":
    main()
