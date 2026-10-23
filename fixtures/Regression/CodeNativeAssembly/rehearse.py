"""Rehearse the two-component showcase through the real CLI; no hosted actions.

Build Aetheris.CLI in Release first. Python 3 alone runs qualification;
--preview additionally uses numpy and matplotlib to render the exported GLB.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def near(actual, expected):
    return len(actual) == len(expected) and all(abs(a - b) < 1e-8 for a, b in zip(actual, expected))


def preview(glb, output):
    # Read the artifact, including its actual component and unit transforms.
    # Exploding the right-hand view is a display operation only.
    import numpy as np
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection

    data = glb.read_bytes()
    magic, version, size = struct.unpack_from("<4sII", data)
    require((magic, version, size) == (b"glTF", 2, len(data)), "Invalid GLB header")
    chunks, offset = {}, 12
    while offset < len(data):
        length, kind = struct.unpack_from("<II", data, offset)
        chunks[kind] = data[offset + 8:offset + 8 + length]
        offset += length + 8
    document = json.loads(chunks[0x4E4F534A])
    binary = chunks[0x004E4942]

    def accessor(index):
        a = document["accessors"][index]
        v = document["bufferViews"][a["bufferView"]]
        dtype = {5126: "<f4", 5125: "<u4", 5123: "<u2", 5121: "u1"}[a["componentType"]]
        count = {"VEC3": 3, "SCALAR": 1}[a["type"]]
        element = np.dtype(dtype).itemsize
        return np.ndarray((a["count"], count), dtype=dtype, buffer=binary,
                          offset=v.get("byteOffset", 0) + a.get("byteOffset", 0),
                          strides=(v.get("byteStride", element * count), element)).copy()

    components = {}

    def visit(index, parent):
        node = document["nodes"][index]
        require("rotation" not in node and "scale" not in node and "translation" not in node,
                "Preview expects the exporter's explicit node matrices")
        world = parent @ np.array(node.get("matrix", np.eye(4).flatten()), dtype=float).reshape((4, 4), order="F")
        if "mesh" in node:
            faces = []
            for primitive in document["meshes"][node["mesh"]]["primitives"]:
                require(primitive.get("mode", 4) == 4, "Preview requires triangle primitives")
                positions = accessor(primitive["attributes"]["POSITION"])
                transformed = (world @ np.column_stack([positions, np.ones(len(positions))]).T).T[:, :3]
                mm = transformed[:, [0, 2, 1]] * [1000, -1000, 1000]
                indices = accessor(primitive["indices"]).reshape((-1, 3))
                faces.append(mm[indices])
            components[node["name"].split(".")[-1]] = np.concatenate(faces)
        for child in node.get("children", []):
            visit(child, world)

    for root in document["scenes"][document.get("scene", 0)]["nodes"]:
        visit(root, np.eye(4))
    require(set(components) == {"Bracket", "Fastener"}, "GLB lost separate component names")
    for name, low, high in [("Bracket", [0, 0, 0], [96, 60, 12]),
                            ("Fastener", [70, 4, -4], [82, 16, 12])]:
        vertices = components[name].reshape((-1, 3))
        require(np.allclose(vertices.min(0), low, atol=1e-4, rtol=0) and
                np.allclose(vertices.max(0), high, atol=1e-4, rtol=0), "Rendered bounds disagree: " + name)

    fig = plt.figure(figsize=(15, 6), facecolor="white", layout="constrained")
    def front_faces(triangles, elevation, azimuth):
        elevation, azimuth = np.radians([elevation, azimuth])
        view = [np.cos(elevation) * np.cos(azimuth), np.cos(elevation) * np.sin(azimuth), np.sin(elevation)]
        normals = np.cross(triangles[:, 1] - triangles[:, 0], triangles[:, 2] - triangles[:, 0])
        return triangles[normals @ view > 0]

    for index, exploded in [(1, False), (2, True)]:
        ax = fig.add_subplot(1, 3, index, projection="3d")
        elevation, azimuth = (27, -65) if exploded else (90, -90)
        all_faces, colors = [], []
        for name, triangles in components.items():
            displayed = triangles.copy()
            if exploded and name == "Fastener":
                displayed[:, :, 2] += 24
            displayed = front_faces(displayed, elevation, azimuth)
            all_faces.extend(displayed)
            colors.extend(["#cf9e49" if name == "Fastener" else "#7c98aa"] * len(displayed))
        # One collection sorts triangles across both components, so a seated head
        # isn't incorrectly hidden by a whole-object painter's ordering.
        ax.add_collection3d(Poly3DCollection(all_faces, facecolors=colors, edgecolor="none", linewidth=0, antialiased=False, shade=True,
                                            lightsource=matplotlib.colors.LightSource(140, 40)))
        ax.set(xlim=(-3, 100), ylim=(-3, 64), zlim=(-6, 40), title="Exploded view · display only" if exploded else "Seated · top view · head flush")
        ax.set_box_aspect((103, 67, 46))
        ax.view_init(elevation, azimuth)
        ax.set_axis_off()
    ax = fig.add_subplot(1, 3, 3, projection="3d")
    triangles = components["Fastener"] - [76, 10, -4]
    triangles = front_faces(triangles, 8, -78)
    ax.add_collection3d(Poly3DCollection(triangles, facecolors="#cf9e49", edgecolor="none", linewidth=0, antialiased=False, shade=True,
                                        lightsource=matplotlib.colors.LightSource(140, 40)))
    ax.set(xlim=(-6.5, 6.5), ylim=(-6.5, 6.5), zlim=(-1, 17), title="Threadless countersunk fastener")
    ax.set_box_aspect((13, 13, 18))
    ax.view_init(8, -78)
    ax.set_axis_off()
    ax.text2D(.08, .08, "Tip: 0.5 mm × 45° chamfer\nShaft Ø5.7 · head Ø12 × 90° · length 16 mm", transform=ax.transAxes, fontsize=10)
    fig.suptitle("Local assembly rehearsal · nominal geometry preview, not a manufacturing drawing", fontsize=14)
    fig.savefig(output, dpi=160)
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=Path("artifacts/local/code-native-assembly/primitive-rehearsal"))
    parser.add_argument("--preview", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    source = Path(__file__).resolve().parent
    output = (root / args.out).resolve()
    output.mkdir(parents=True, exist_ok=True)
    (output / "qualification.json").unlink(missing_ok=True)
    cli = root / "Aetheris.CLI/bin/Release/net10.0/aetheris.dll"
    require(cli.is_file(), "Build Aetheris.CLI in Release first")

    def run(name, *arguments, accepted=True):
        result = subprocess.run(["dotnet", str(cli), *map(str, arguments), "--json"],
                                cwd=root, capture_output=True, text=True, encoding="utf-8")
        (output / (name + ".json")).write_text(result.stdout, encoding="utf-8")
        if result.stderr:
            (output / (name + ".stderr.txt")).write_text(result.stderr, encoding="utf-8")
        require((result.returncode == 0) == accepted, name + " unexpected exit: " + result.stdout[:1500] + result.stderr[:500])
        return json.loads(result.stdout)

    validation = run("fastener-validation", "validate", source / "fastener.firmament")
    require(validation["firmamentV2Validation"]["validationLevel"] == "Built", "Fastener wasn't admitted through build")
    require(validation["firmamentV2Validation"]["parserRoute"] == "UnifiedMechanical", "Primitive Compose left unified mechanical admission")
    run("fastener-build", "build", source / "fastener.firmament", "--out", output / "fastener.step")
    run("fastener-verification", "verify", output / "fastener.step", "--evidence-dir", output / "verification")
    run("solid-revolve-validation", "validate", source / "solid-fastener-revolve.firmament")
    run("solid-revolve-build", "build", source / "solid-fastener-revolve.firmament", "--out", output / "solid-fastener-revolve.step")
    unsupported = output / "unsupported-partial-revolve.firmament"
    unsupported.write_text((source / "solid-fastener-revolve.firmament").read_text().replace("angle: full", "angle: half"), encoding="utf-8")
    rejected = run("partial-solid-revolve-rejection", "validate", unsupported, accepted=False)
    require("firmament-revolve-axis-touch-materialization-not-supported" in json.dumps(rejected), "Partial Revolve limitation changed; review fixture")

    inspections = []
    for name, x in [("assembly", 76), ("relocated", 60)]:
        inspection = run(name + "-inspection", "asm", "inspect", source / (name + ".firmasm"))
        require(inspection["success"], name + " was rejected")
        geometry = inspection["geometryArtifact"]
        require(len(geometry["definitions"]) == 2, "Definitions were merged")
        require(all(r["passed"] for r in geometry["mateResiduals"]) and len(geometry["mateResiduals"]) == 1, "Mate was not satisfied")
        fastener = next(i for i in inspection["assemblyIr"]["instances"] if i["path"]["segments"][-1] == "Fastener")
        require(near(fastener["resolvedTransform"]["matrix"][12:15], [x, 10, -4]), "Semantic placement did not follow the hole")
        run(name + "-step-export", "asm", "export-ap242", source / (name + ".firmasm"), "--out", output / (name + ".step"))
        run(name + "-glb-export", "asm", "export-glb", source / (name + ".firmasm"), output / (name + ".glb"))
        analysis = run(name + "-analysis", "analyze", "assembly", output / (name + ".step"))
        require(analysis["importStatus"] == "Qualified" and analysis["definitionCount"] == analysis["occurrenceCount"] == analysis["bodyCount"] == 2,
                "STEP assembly failed product-structure qualification")
        require(near(analysis["assemblyBoundingBox"]["minimum"], [0, 0, -4]) and
                near(analysis["assemblyBoundingBox"]["maximum"], [96, 60, 12]), "Unexpected assembly bounds")
        imported = run(name + "-import", "asm", "import-step", output / (name + ".step"), "--out", output / (name + "-roundtrip"))
        run(name + "-roundtrip-export", "asm", "export-ap242", imported["firmasmPath"], "--out", output / (name + "-roundtrip.step"))
        roundtrip = run(name + "-roundtrip-analysis", "analyze", "assembly", output / (name + "-roundtrip.step"))
        require(roundtrip["definitionCount"] == roundtrip["occurrenceCount"] == roundtrip["bodyCount"] == 2 and
                roundtrip["importStatus"] == "Qualified", "STEP roundtrip lost components")
        # Imported occurrence identifiers are interchange-owned; compare transforms and geometry, not spelling.
        before = sorted(tuple(o["worldTransform"]) for o in analysis["occurrences"])
        after = sorted(tuple(o["worldTransform"]) for o in roundtrip["occurrences"])
        require(all(near(a, b) for a, b in zip(before, after)), "Roundtrip placement changed")
        require(roundtrip["assemblyBoundingBox"] == analysis["assemblyBoundingBox"], "Roundtrip bounds changed")
        replay = run(name + "-roundtrip-inspection", "asm", "inspect", imported["firmasmPath"])
        def metrics(artifact):
            return sorted(json.dumps(d["metrics"], sort_keys=True) for d in artifact["geometryArtifact"]["definitions"])
        require(metrics(replay) == metrics(inspection), "Roundtrip changed component topology or local bounds")
        inspections.append(inspection)
    hashes = [next(d["stepSha256"] for d in i["geometryArtifact"]["definitions"] if d["definitionIdentity"].startswith("Fastener<")) for i in inspections]
    require(hashes[0] == hashes[1], "Hole relocation changed the fastener definition")
    if args.preview:
        preview(output / "assembly.glb", output / "preview.png")
    summary = {"status": "passed", "sourceHead": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
               "workingTreeChanges": subprocess.check_output(["git", "status", "--porcelain"], cwd=root, text=True).splitlines(),
               "sourceHashes": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in source.glob("*.firma*")},
               "fastenerDefinitionSha256": hashes[0], "components": 2, "mateResidualMm": 0,
               "tipChamferMm": .5, "tipChamferDegrees": 45,
               "diametralClearanceMm": {"nominal": .3, "minimumWithIndependentPlusMinus0_1mmSizes": .1, "maximum": .5},
               "limits": ["Nominal seat checked; angular/location tolerance stacks, retention and strength unqualified.",
                          "Part PMI stays part-owned by design. Assembly PMI requires an explicit assembly declaration; no assembly manufacturing drawing generated.",
                          "Solid axis-touching Revolve admits full turns of flat-ended monotone line stacks; other axis-touching shapes remain unsupported."]}
    (output / "qualification.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
