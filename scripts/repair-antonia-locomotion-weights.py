"""Prepare an explicit Antonia development body for authored locomotion.

Preserves geometry, topology, rest transforms and correctives. The recipe changes
anatomical weight support only; every resulting pose still needs qualification.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path


def smoothstep(value):
    value = min(1.0, max(0.0, value))
    return value * value * (3.0 - 2.0 * value)


def point(value):
    return [value[axis] for axis in ("x", "y", "z")]


def subtract(a, b):
    return [left - right for left, right in zip(a, b)]


def length(value):
    return math.sqrt(sum(component * component for component in value))


def prepare(body, knee_width_mm):
    joints = body["skeleton"]["joints"]
    indices = {joint["kind"]: index for index, joint in enumerate(joints)}
    centers = {joint["kind"]: [joint["globalBind"][key] for key in ("m41", "m42", "m43")]
               for joint in joints}
    neck = centers["Neck"][2]
    changes = {"head": 0, "knee": 0}
    for vertex, skin in zip(body["surface"]["vertices"], body["surface"]["skinWeights"]):
        position = point(vertex["position"])
        weights = {entry["jointIndex"]: entry["weight"] for entry in skin["weights"]}
        # Facial tissue above the jaw follows the skull, not the cervical chain.
        if weights.get(indices["Neck"], 0) > 0:
            amount = weights.get(indices["Neck"], 0) * smoothstep(
                (position[2] - (neck - 60)) / 100)
            if amount > 0:
                weights[indices["Neck"]] -= amount
                weights[indices["Head"]] = weights.get(indices["Head"], 0) + amount
                changes["head"] += 1
        for side in ("Left", "Right"):
            hip = indices[side + "Hip"]
            knee = indices[side + "Knee"]
            total = weights.get(hip, 0) + weights.get(knee, 0)
            center = centers[side + "Knee"]
            relative = subtract(position, center)
            distance = length(relative)
            if total < .5 or distance > 250:
                continue
            direction = subtract(centers[side + "Ankle"], center)
            direction = [component / length(direction) for component in direction]
            along = sum(a * b for a, b in zip(relative, direction))
            desired = smoothstep((along + knee_width_mm) / (2 * knee_width_mm))
            fade = 1 - smoothstep((distance - 150) / 100)
            current = weights.get(knee, 0) / total
            fraction = current + fade * (desired - current)
            weights[knee] = total * fraction
            weights[hip] = total * (1 - fraction)
            changes["knee"] += 1
        weights = {index: value for index, value in weights.items() if value > 1e-12}
        total = sum(weights.values())
        skin["weights"] = [{"jointIndex": index, "weight": value / total}
                           for index, value in sorted(weights.items())]
    return changes


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--body", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--knee-width-mm", type=float, default=200)
    args = parser.parse_args()
    source = Path(args.body)
    output = Path(args.out)
    if source.resolve() == output.resolve():
        raise ValueError("Write a new candidate; do not overwrite the source body.")
    if not 40 <= args.knee_width_mm <= 240:
        raise ValueError("Knee transition half-width must be 40..240 mm.")
    raw = source.read_bytes()
    body = json.loads(raw)
    if body.get("schema") != "aetheris.humanoid.gameplay-body.v1" or \
            body["surface"]["topologyId"] != "aetheris.humanoid.antonia.adoption-candidate.v1" or \
            body["skeleton"]["restPoseId"] != "aetheris.humanoid.rest.apose.v1":
        raise ValueError("This recipe requires the explicit Antonia A-pose gameplay body.")
    vertices = body["surface"]["vertices"]
    skins = body["surface"]["skinWeights"]
    if len(vertices) != len(skins) or any(skin["vertexIndex"] != index for index, skin in enumerate(skins)):
        raise ValueError("Skin rows must correspond to every retained vertex in order.")
    retained = json.dumps([body["surface"][key] for key in ("vertices", "faces", "bindingTriangles")]
                          + [body["skeleton"], body["correctives"]], sort_keys=True)
    changes = prepare(body, args.knee_width_mm)
    after = json.dumps([body["surface"][key] for key in ("vertices", "faces", "bindingTriangles")]
                      + [body["skeleton"], body["correctives"]], sort_keys=True)
    if retained != after:
        raise ValueError("Weight repair changed retained geometry, topology, skeleton or correctives.")
    for skin in body["surface"]["skinWeights"]:
        values = [weight["weight"] for weight in skin["weights"]]
        if len(values) > 6 or not all(math.isfinite(value) and value > 0 for value in values):
            raise ValueError("Invalid or excessive skin influences.")
        if abs(sum(values) - 1) > 1e-9:
            raise ValueError("Skin weights must stay normalized.")
    source_hash = hashlib.sha256(raw).hexdigest().upper()
    recipe = dict(sourceSha256=source_hash, kneeTransitionHalfWidthMm=args.knee_width_mm,
                  faceSupport="neck-to-head transfer above jaw with smooth transition")
    body["id"] += ".locomotion-weights.v1"
    body["provenance"]["transformations"].append(
        "Locomotion support repair: " + json.dumps(recipe, sort_keys=True))
    body["provenance"]["generatorVersion"] = "antonia.locomotion-weights.v1"
    body["provenance"]["configHash"] = hashlib.sha256(json.dumps(recipe, sort_keys=True).encode()).hexdigest()
    body["provenance"]["admissionStatus"] = "Unqualified locomotion weight candidate; evaluate every pose separately"
    body["provenance"]["outputHash"] = hashlib.sha256(json.dumps(body["surface"]["skinWeights"]).encode()).hexdigest()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(body, separators=(",", ":")))
    print("LOCOMOTION_WEIGHTS_PREPARED", json.dumps(dict(recipe=recipe, changedVertices=changes)))


if __name__ == "__main__":
    main()
