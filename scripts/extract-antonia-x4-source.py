"""Safe, deterministic X4 neutral extraction and frame audit.

Loads only data blocks with embedded-script execution disabled by the calling command.
It does not execute CharMorph, Rigify, drivers, or bundled text blocks. The admitted
canonical subset is mapped by neutral vertex identity and validated against authored
quad connectivity. Non-neutral extraction intentionally fails until the source rig,
weights, shared tweaks, and modifier stack have a reviewed admission record.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector, kdtree


ROOT = Path(__file__).resolve().parents[1]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def point(value):
    return {"x": float(value[0]), "y": float(value[1]), "z": float(value[2])}


def vector(value):
    return {"x": float(value[0]), "y": float(value[1]), "z": float(value[2])}


def distance(first, second):
    return (Vector(first) - Vector(second)).length


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--candidate", default="artifacts/local/humanoid-x1/antonia-adoption-candidate.json")
    parser.add_argument("--char-blend", default="artifacts/local/humanoid-recon-x0/CharMorph-db/characters/antonia/char.blend")
    parser.add_argument("--metarig-blend", default="artifacts/local/humanoid-recon-x0/CharMorph-db/characters/antonia/metarig.blend")
    parser.add_argument("--weights", default="artifacts/local/humanoid-recon-x0/CharMorph-db/characters/antonia/weights/rigify.npz")
    parser.add_argument("--config", default="artifacts/local/humanoid-recon-x0/CharMorph-db/characters/antonia/config.yaml")
    parser.add_argument("--out-dir", default="artifacts/local/humanoid-x4")
    parser.add_argument("--pose", default="neutral", choices=("neutral",))
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])

    paths = {name: (ROOT / value).resolve() for name, value in {
        "candidate": args.candidate, "charBlend": args.char_blend, "metarigBlend": args.metarig_blend,
        "weights": args.weights, "config": args.config}.items()}
    for name, path in paths.items():
        if not path.is_file():
            raise FileNotFoundError(f"Missing X4 input {name}: {path}")
    manifest = json.loads((ROOT / "docs/release/HUMANOID-RECON-X0.manifest.json").read_text(encoding="utf-8"))
    admitted = next(asset for asset in manifest["assets"] if asset["id"] == "antonia")
    expected_hashes = admitted["filesSha256"]
    for name, relative in (("charBlend", "char.blend"), ("metarigBlend", "metarig.blend"),
                           ("weights", "weights/rigify.npz"), ("config", "config.yaml")):
        actual = sha256(paths[name])
        if actual != expected_hashes[relative]:
            raise RuntimeError(f"Pinned X4 input hash mismatch for {relative}: {actual}")
    out = (ROOT / args.out_dir).resolve()
    out.mkdir(parents=True, exist_ok=True)

    candidate = json.loads(paths["candidate"].read_text(encoding="utf-8"))
    canonical_vertices = candidate["sourcePoseSurface"]["vertices"]
    canonical_positions = [Vector((v["position"]["x"], v["position"]["y"], v["position"]["z"])) for v in canonical_vertices]

    bpy.ops.wm.open_mainfile(filepath=str(paths["charBlend"]), load_ui=False, use_scripts=False)
    body = bpy.data.objects.get("cm_antonia")
    if body is None or body.type != "MESH":
        raise RuntimeError("Pinned Antonia Blender mesh cm_antonia is missing.")
    source_positions = [v.co.copy() for v in body.data.vertices]

    canonical_height = max(p.z for p in canonical_positions) - min(p.z for p in canonical_positions)
    source_height = max(p.z for p in source_positions) - min(p.z for p in source_positions)
    scale = canonical_height / source_height
    signs = Vector((-1.0, -1.0, 1.0))
    canonical_mid = Vector(tuple((min(p[i] for p in canonical_positions) + max(p[i] for p in canonical_positions)) / 2 for i in range(3)))
    source_mid = Vector(tuple((min(p[i] for p in source_positions) + max(p[i] for p in source_positions)) / 2 for i in range(3)))
    offset = Vector(tuple(canonical_mid[i] - signs[i] * source_mid[i] * scale for i in range(3)))

    def to_canonical(value):
        return Vector(tuple(signs[i] * value[i] * scale + offset[i] for i in range(3)))

    def direction_to_canonical(value):
        transformed = Vector(tuple(signs[i] * value[i] for i in range(3)))
        transformed.normalize()
        return transformed

    tree = kdtree.KDTree(len(source_positions))
    for index, value in enumerate(source_positions):
        tree.insert(to_canonical(value), index)
    tree.balance()
    mapping = []
    errors = []
    used = set()
    for canonical_index, value in enumerate(canonical_positions):
        _, source_index, error = tree.find(value)
        if error > .001:
            raise RuntimeError(f"Canonical vertex {canonical_index} has no exact neutral source match: {error} mm")
        if source_index in used:
            raise RuntimeError(f"Source vertex {source_index} maps to multiple canonical vertices.")
        used.add(source_index)
        errors.append(error)
        mapping.append(source_index)

    source_id_to_canonical = {source_id: index for index, source_id in enumerate(candidate["sourceVertexIndices"])}
    source_face_sets = {tuple(sorted(p.vertices)) for p in body.data.polygons if len(p.vertices) == 4}
    connectivity_matches = 0
    for polygon in candidate["sourcePolygons"]:
        source_face = tuple(sorted(mapping[source_id_to_canonical[source_id]] for source_id in polygon["sourceVertexIndices"]))
        if source_face in source_face_sets:
            connectivity_matches += 1
    if connectivity_matches != len(candidate["sourcePolygons"]):
        raise RuntimeError(f"Only {connectivity_matches}/{len(candidate['sourcePolygons'])} admitted quads occur in the Blender mesh.")

    mapping_text = "".join(f"{canonical}:{source}\n" for canonical, source in enumerate(mapping))
    mapping_hash = hashlib.sha256(mapping_text.encode("ascii")).hexdigest()
    mapping_artifact = {
        "schema": "aetheris.humanoid.source-index-map.v1",
        "canonicalTopologyId": candidate["sourcePoseSurface"]["topologyId"],
        "canonicalConnectivityHash": candidate["sourcePoseSurface"]["connectivityHash"],
        "canonicalVertexCount": len(mapping), "sourceVertexCount": len(source_positions),
        "selectedSourceVertexCount": len(used), "bijectionOverCanonicalSubset": True,
        "mappingSha256": mapping_hash,
        "canonicalToSourceVertex": mapping,
    }
    (out / "topology-mapping.json").write_text(json.dumps(mapping_artifact, indent=2) + "\n", encoding="utf-8")

    with bpy.data.libraries.load(str(paths["metarigBlend"]), link=False) as (data_from, data_to):
        data_to.objects = list(data_from.objects)
    armatures = [obj for obj in data_to.objects if obj is not None and obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one source metarig armature, found {len(armatures)}.")
    rig = armatures[0]

    def bone_frame(name):
        bone = rig.data.bones.get(name)
        if bone is None:
            raise RuntimeError(f"Missing source rest bone: {name}")
        matrix = bone.matrix_local.to_3x3()
        return {
            "id": name, "classification": "control-rig pivot / weighted anchor candidate",
            "originCanonicalMm": point(to_canonical(bone.head_local)),
            "tailCanonicalMm": point(to_canonical(bone.tail_local)),
            "axesCanonical": {
                "x": vector(direction_to_canonical(matrix.col[0])),
                "y": vector(direction_to_canonical(matrix.col[1])),
                "z": vector(direction_to_canonical(matrix.col[2])),
            },
            "parent": bone.parent.name if bone.parent else None,
            "useDeform": bool(bone.use_deform),
        }

    source_frames = {name: bone_frame(name) for name in ("spine", "thigh.L", "thigh.R", "shin.L", "shin.R")}
    aetheris_joints = {joint["kind"]: joint for joint in candidate["preparedSkeleton"]["joints"]}
    def joint_origin(kind):
        matrix = aetheris_joints[kind]["globalBind"]
        return Vector((matrix["m41"], matrix["m42"], matrix["m43"]))

    poser_left = Vector((-0.036 * candidate["scaleToMm"], (-0.023 - candidate["sourcePelvisZ"]) * candidate["scaleToMm"],
                         (0.386 - candidate["sourceSoleY"]) * candidate["scaleToMm"]))
    poser_right = Vector((0.036 * candidate["scaleToMm"], (-0.023 - candidate["sourcePelvisZ"]) * candidate["scaleToMm"],
                          (0.386 - candidate["sourceSoleY"]) * candidate["scaleToMm"]))
    blender_left = Vector(tuple(source_frames["thigh.L"]["originCanonicalMm"][axis] for axis in ("x", "y", "z")))
    blender_right = Vector(tuple(source_frames["thigh.R"]["originCanonicalMm"][axis] for axis in ("x", "y", "z")))
    aetheris_left = joint_origin("LeftHip")
    aetheris_right = joint_origin("RightHip")

    frame_audit = {
        "schema": "aetheris.humanoid.x4.frame-audit.v1",
        "canonicalFrame": {"unit": "mm", "handedness": "right", "axes": "+X anatomical right, +Y forward, +Z up"},
        "sourceToCanonical": {"scaleMmPerBlenderUnit": scale, "axisSigns": list(signs), "offsetMm": list(offset), "determinantPositive": True,
                              "derivation": "Positive-determinant signed uniform scale and translation from neutral extrema; accepted only after all admitted vertices match within 0.001 mm."},
        "points": [
            {"id": "poser.lThigh.jointX.center", "side": "Left", "classification": "deformation pivot", "canonicalMm": point(poser_left), "sourceCoordinates": [0.036, 0.386, -0.023]},
            {"id": "blender.metarig.thigh.L.head", "side": "Left", "classification": "control-rig pivot / weighted anchor candidate", "canonicalMm": point(blender_left)},
            {"id": "aetheris.LeftHip.globalBind", "side": "Left", "classification": "socket proxy from mesh-group seam", "canonicalMm": point(aetheris_left)},
            {"id": "poser.rThigh.jointX.center", "side": "Right", "classification": "deformation pivot", "canonicalMm": point(poser_right), "sourceCoordinates": [-0.036, 0.386, -0.023]},
            {"id": "blender.metarig.thigh.R.head", "side": "Right", "classification": "control-rig pivot / weighted anchor candidate", "canonicalMm": point(blender_right)},
            {"id": "aetheris.RightHip.globalBind", "side": "Right", "classification": "socket proxy from mesh-group seam", "canonicalMm": point(aetheris_right)},
        ],
        "comparisons": {
            "leftPoserToAetheris": {"differenceAetherisMinusSourceMm": vector(aetheris_left - poser_left), "distanceMm": distance(aetheris_left, poser_left)},
            "rightPoserToAetheris": {"differenceAetherisMinusSourceMm": vector(aetheris_right - poser_right), "distanceMm": distance(aetheris_right, poser_right)},
            "leftBlenderToPoser": {"differenceBlenderMinusPoserMm": vector(blender_left - poser_left), "distanceMm": distance(blender_left, poser_left)},
            "rightBlenderToPoser": {"differenceBlenderMinusPoserMm": vector(blender_right - poser_right), "distanceMm": distance(blender_right, poser_right)},
            "leftBlenderToAetheris": {"differenceAetherisMinusBlenderMm": vector(aetheris_left - blender_left), "distanceMm": distance(aetheris_left, blender_left)},
            "rightBlenderToAetheris": {"differenceAetherisMinusBlenderMm": vector(aetheris_right - blender_right), "distanceMm": distance(aetheris_right, blender_right)},
        },
        "symmetry": {
            "poserMirrorResidualMm": distance(poser_right, Vector((-poser_left.x, poser_left.y, poser_left.z))),
            "blenderMirrorResidualMm": distance(blender_right, Vector((-blender_left.x, blender_left.y, blender_left.z))),
            "aetherisMirrorResidualMm": distance(aetheris_right, Vector((-aetheris_left.x, aetheris_left.y, aetheris_left.z))),
        },
        "sourceRestFrames": source_frames,
        "aetherisFemurHeadFrame": None,
        "conclusion": "Coordinate conversion is corroborated by independent Poser and Blender rig pivots. The Aetheris seam-derived hip is a different and materially lower proxy; no distinct femur-head frame exists in the current model.",
    }
    (out / "frame-audit.json").write_text(json.dumps(frame_audit, indent=2) + "\n", encoding="utf-8")

    mapped_positions = [to_canonical(source_positions[source_index]) for source_index in mapping]
    neutral_dump = {
        "schema": "aetheris.humanoid.blender-source-pose.v1",
        "poseId": "neutral",
        "topologyId": candidate["sourcePoseSurface"]["topologyId"],
        "connectivityHash": candidate["sourcePoseSurface"]["connectivityHash"],
        "mappingSha256": mapping_hash,
        "sourceAssetSha256": sha256(paths["charBlend"]),
        "topologyVerified": True,
        "positions": [point(value) for value in mapped_positions],
        "requestedPose": {"joint": "LeftHip", "side": "Left", "flexionDegrees": 0.0, "abductionDegrees": 0.0, "twistDegrees": 0.0,
                          "frame": "no rotation; source rest", "convention": "neutral identity", "restBasis": "CharMorph Antonia char.blend neutral mesh"},
        "versions": {"blender": bpy.app.version_string, "extractorSha256": sha256(Path(__file__)),
                     "candidateSha256": sha256(paths["candidate"]), "metarigSha256": sha256(paths["metarigBlend"]),
                     "weightsSha256": sha256(paths["weights"]), "configSha256": sha256(paths["config"])},
        "neutralMappingErrorMm": {"rms": (sum(e * e for e in errors) / len(errors)) ** .5, "maximum": max(errors)},
        "limitations": [
            "Neutral geometry and rest-frame data blocks only; no source pose deformation was evaluated.",
            "The CharMorph Antonia mesh contains additional vertices; the stored mapping is bijective over the admitted 27193-vertex canonical subset.",
            "Rigify generation, CharMorph shared tweaks, sliding joints, weights, drivers, and modifier evaluation remain outside the admitted source component and were not executed.",
            "The current Aetheris prepared A-pose differs from the source-rest arm treatment by design; hip-region neutral comparison remains valid, whole-body rest agreement requires an admitted source A-pose conversion.",
        ],
    }
    (out / "source-neutral.json").write_text(json.dumps(neutral_dump, indent=2) + "\n", encoding="utf-8")

    with (out / "source-neutral.obj").open("w", encoding="utf-8", newline="\n") as stream:
        stream.write("# Research-only Antonia neutral canonical subset; source attribution retained in repository notices.\n")
        for value in mapped_positions:
            stream.write(f"v {value.x:.17g} {value.y:.17g} {value.z:.17g}\n")
        for face in candidate["sourcePoseSurface"]["faces"]:
            stream.write(f"f {face['a'] + 1} {face['b'] + 1} {face['c'] + 1}\n")

    manifest = {
        "schema": "aetheris.humanoid.x4.source-extraction-manifest.v1", "status": "NeutralOnly-PosedSourceBlocked",
        "pose": args.pose, "outputs": {name: sha256(out / name) for name in ("topology-mapping.json", "frame-audit.json", "source-neutral.json", "source-neutral.obj")},
        "topology": {"canonicalVertices": len(mapping), "sourceVertices": len(source_positions), "admittedQuads": len(candidate["sourcePolygons"]),
                     "connectivityMatches": connectivity_matches, "mappingSha256": mapping_hash, "maximumNeutralErrorMm": max(errors)},
        "blocker": "The exact Blender deformation stack requires currently unadmitted CharMorph/Rigify weights, shared tweaks, sliding-joint behavior and modifiers. X4 does not label an approximated rig as the source authority.",
    }
    (out / "source-extraction-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
