"""Package the five native Aetheris GLBs in the supplied artifact directory."""
import argparse
import io
import json
from pathlib import Path
import struct

import numpy as np

from character_livery import ORANGE, PANEL_SIZE, body_atlas, eye


# Authoring uses mm, Z up and -X forward. The asset uses m, Y up and -Z forward.
BASIS = np.array(((0, 1, 0), (0, 0, 1), (1, 0, 0)))
ORIGIN = np.array((1235, 0, 0))
UNIT = 0.001


def load_native(path, expected_state=None):
    data = path.read_bytes()
    magic, version, length, json_length, tag = struct.unpack_from("<4sIII4s", data)
    if (magic, version, length, tag) != (b"glTF", 2, len(data), b"JSON"):
        raise ValueError(f"Invalid GLB header in {path}")
    document = json.loads(data[20:20 + json_length])
    if expected_state is not None:
        mesh, = document["meshes"]
        if mesh["extras"]["definitionIdentity"] != expected_state:
            raise ValueError(f"{path} does not match its Firmament sidecar")
    primitives = [primitive for mesh in document["meshes"] for primitive in mesh["primitives"]]
    if len(primitives) != 1:
        raise ValueError(f"Expected one native mesh primitive in {path}")
    primitive = primitives[0]

    def read(index):
        accessor = document["accessors"][index]
        view = document["bufferViews"][accessor["bufferView"]]
        width = {"VEC3": 3, "SCALAR": 1}[accessor["type"]]
        dtype = {5126: "<f4", 5125: "<u4", 5123: "<u2"}[accessor["componentType"]]
        if "byteStride" in view or view["buffer"] != 0:
            raise ValueError(f"Expected contiguous native accessors in {path}")
        offset = 28 + json_length + view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        return np.frombuffer(data, dtype, accessor["count"] * width, offset)

    attrs = primitive["attributes"]
    positions = read(attrs["POSITION"]).reshape(-1, 3)
    normals = read(attrs["NORMAL"]).reshape(-1, 3)
    indices = read(primitive["indices"]).reshape(-1, 3)
    if len(positions) != len(normals) or indices.max() >= len(positions):
        raise ValueError(f"Invalid native mesh in {path}")
    return positions, normals, indices


class CoupeGlb:
    def __init__(self):
        self.binary = bytearray()
        self.document = {
            "asset": {"version": "2.0", "generator": "Aetheris sports coupe fixture",
                      "extras": {"up": "+Y", "forward": "-Z", "linearUnit": "metre"}},
            "scene": 0, "scenes": [{"name": "Claude-mobile", "nodes": [0]}],
            "nodes": [{"name": "ClaudeMobile", "children": []}], "meshes": [],
            "materials": [], "buffers": [], "bufferViews": [], "accessors": [],
            "images": [], "textures": [], "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 33071, "wrapT": 33071}],
            "extensionsUsed": ["KHR_materials_clearcoat", "KHR_materials_unlit"],
        }

    def material(self, name, rgb, metallic, roughness, clearcoat=False, emission=None, texture=None, unlit=False):
        material = {"name": name, "pbrMetallicRoughness": {
            "baseColorFactor": list(rgb) + [1], "metallicFactor": metallic, "roughnessFactor": roughness}}
        if texture is not None:
            material["pbrMetallicRoughness"]["baseColorTexture"] = {"index": texture}
        if clearcoat:
            material["extensions"] = {"KHR_materials_clearcoat": {
                "clearcoatFactor": 0.82, "clearcoatRoughnessFactor": 0.19}}
        if unlit:
            material.setdefault("extensions", {})["KHR_materials_unlit"] = {}
        if emission is not None:
            material["emissiveFactor"] = list(emission)
        self.document["materials"].append(material)
        return len(self.document["materials"]) - 1

    def buffer_view(self, contents, target=None):
        self.binary.extend(bytes(-len(self.binary) % 4))
        view = {"buffer": 0, "byteOffset": len(self.binary), "byteLength": len(contents)}
        if target is not None:
            view["target"] = target
        self.binary.extend(contents)
        self.document["bufferViews"].append(view)
        return len(self.document["bufferViews"]) - 1

    def texture(self, name, image):
        png = io.BytesIO()
        image.save(png, format="PNG")
        images, textures = self.document["images"], self.document["textures"]
        images.append({"name": name, "mimeType": "image/png", "bufferView": self.buffer_view(png.getvalue())})
        textures.append({"name": name, "sampler": 0, "source": len(images) - 1})
        return len(textures) - 1

    def accessor(self, array, bounds=False):
        view = self.buffer_view(array.tobytes(), 34963 if array.ndim == 1 else 34962)
        accessor = {
            "bufferView": view,
            "componentType": {"<f4": 5126, "<u2": 5123, "<u4": 5125}[array.dtype.str],
            "count": len(array), "type": "SCALAR" if array.ndim == 1 else f"VEC{array.shape[1]}"}
        if bounds:
            accessor["min"] = array.min(axis=0).tolist()
            accessor["max"] = array.max(axis=0).tolist()
        self.document["accessors"].append(accessor)
        return len(self.document["accessors"]) - 1

    def mesh(self, name, native, materials, origin=None, face_uvs=None):
        positions, normals, faces = native
        local = positions if origin is None else positions - origin
        vertices = np.column_stack((local @ BASIS.T * UNIT, normals @ BASIS.T)).astype("<f4")
        if face_uvs is not None:
            # Two projected regions can share a position and normal across a UV seam.
            vertices = np.column_stack((vertices[faces.ravel()], face_uvs.reshape(-1, 2))).astype("<f4")
            faces = np.arange(len(vertices)).reshape(-1, 3)
        vertices, remap = np.unique(vertices, axis=0, return_inverse=True)
        faces = remap[faces]
        dtype = "<u2" if len(vertices) <= 65536 else "<u4"
        attrs = {"POSITION": self.accessor(vertices[:, :3], bounds=True),
                 "NORMAL": self.accessor(vertices[:, 3:6])}
        uv_attrs = dict(attrs, TEXCOORD_0=self.accessor(vertices[:, 6:])) if face_uvs is not None else attrs
        primitives = []
        for material in np.unique(materials):
            indices = faces if np.isscalar(materials) else faces[materials == material]
            textured = "baseColorTexture" in self.document["materials"][material]["pbrMetallicRoughness"]
            primitive_attrs = uv_attrs if textured else attrs
            primitives.append({"attributes": primitive_attrs, "indices": self.accessor(indices.ravel().astype(dtype)),
                               "material": int(material)})
        mesh = self.document["meshes"]
        mesh.append({"name": name, "primitives": primitives})
        return len(mesh) - 1

    def recolor(self, mesh_index, name, material):
        template, = self.document["meshes"][mesh_index]["primitives"]
        self.document["meshes"].append({"name": name, "primitives": [dict(template, material=material)]})
        return len(self.document["meshes"]) - 1

    def node(self, name, parent=0, **properties):
        nodes = self.document["nodes"]
        index = len(nodes)
        nodes.append({"name": name, **properties})
        nodes[parent].setdefault("children", []).append(index)
        return index

    def write(self, path):
        self.binary.extend(bytes(-len(self.binary) % 4))
        self.document["buffers"] = [{"byteLength": len(self.binary)}]
        metadata = json.dumps(self.document, separators=(",", ":"), allow_nan=False).encode("utf-8")
        metadata += b" " * (-len(metadata) % 4)
        header = struct.pack("<4sIII4s", b"glTF", 2, 28 + len(metadata) + len(self.binary), len(metadata), b"JSON")
        path.write_bytes(header + metadata + struct.pack("<I4s", len(self.binary), b"BIN\0") + self.binary)


def load_cage_layout(sidecar, name, native):
    faces = sidecar["cage"]["faces"]
    triangles_per_face = 2 * 4 ** sidecar["construction"]["previewLevels"]
    if len(faces) * triangles_per_face != len(native[2]):
        raise ValueError(f"{name} GLB triangles do not match the native cage face layout")
    positions = {v["id"]: v["position"] for v in sidecar["cage"]["vertices"]}
    return faces, positions, triangles_per_face


def cabin_materials(sidecar, cabin, paint, glass, screen, pillar):
    faces, cage, triangles_per_face = load_cage_layout(sidecar, "cabin", cabin)
    looks = []
    for face in faces:
        identity = face["id"]
        x = sum(cage[vertex]["x"] for vertex in face["vertices"]) / 4
        if "_Top_" in identity:
            look = paint if 1095 < x < 1770 else screen
        elif "_Left_" in identity or "_Right_" in identity:
            look = paint if x > 2170 or 860 < x < 913 else pillar if 1590 < x < 1670 else glass
        else:
            look = pillar
        looks.append(look)
    return np.repeat(looks, triangles_per_face)


def body_materials(sidecar, body, paint, dark):
    faces, controls, triangles_per_face = load_cage_layout(sidecar, "body", body)
    looks = []
    for face in faces:
        points = [controls[vertex] for vertex in face["vertices"]]
        x = sum(p["x"] for p in points) / 4
        z = sum(p["z"] for p in points) / 4
        side = "_Left_" in face["id"] or "_Right_" in face["id"]
        looks.append(dark if side and 505 < x < 1940 and z < 210 else paint)
    return np.repeat(looks, triangles_per_face)


def body_uvs(body):
    positions, normals, indices = body
    corners = positions[indices]
    x, y, z = corners.transpose(2, 0, 1)
    nx, ny, nz = normals[indices].mean(axis=1).T
    middle_x, middle_y = x.mean(axis=1), y.mean(axis=1)
    uvs = np.full((*x.shape, 2), 0.75)  # Unprinted orange atlas quadrant.

    def panel(mask, u, v, column, row):
        # Keep interpolation within each tile, including bilinear filtering at the edge.
        border = 0.5 / PANEL_SIZE
        uvs[mask, :, 0] = (column + np.clip(u[mask], border, 1 - border)) / 2
        uvs[mask, :, 1] = (row + np.clip(v[mask], border, 1 - border)) / 2

    panel((middle_x < -765) & (nx < -0.50), (y + 540) / 1080, (430 - z) / 257, 0, 0)
    panel((middle_x < 515) & (nz > 0.60) & (middle_x > -910), (y + 490) / 980, (x + 875) / 1395, 1, 0)
    along = (x - 490) / 1540
    visible_side = (middle_x > 460) & (middle_x < 2055) & (np.abs(ny) > 0.50)
    panel(visible_side, np.where(middle_y[:, None] < 0, along, 1 - along), (680 - z) / 350, 0, 1)
    return uvs


def rim_materials(rim, alloy, barrel):
    positions, normals, faces = rim
    center = positions[faces].mean(axis=1)
    x, _, z = center.T
    radius = np.hypot(x, z)
    angle = np.arctan2(z, x) + 0.23 * radius / 190
    spokes = np.cos(5 * angle) > 0.54
    face = np.abs(normals[faces, 1].mean(axis=1)) > 0.27
    return np.where(face & (radius > 65) & (radius < 199) & ~spokes, barrel, alloy)


def surface_mounts(sidecar):
    def xyz(point):
        return np.array([point[axis] for axis in ("x", "y", "z")])

    return {
        mount["name"]: (xyz(mount["positionMm"]), np.column_stack([
            xyz(mount[axis]) for axis in ("longitudinal", "crosswise", "normal")
        ]))
        for mount in sidecar["surfaceMounts"]
    }


def package(folder):
    sidecars = {name: json.loads((folder / (name + ".glb.sculpt.json")).read_text(encoding="utf-8"))
                for name in ("body", "cabin")}
    native = {name: load_native(folder / (name + ".glb"), sidecars.get(name, {}).get("stateId"))
              for name in ("body", "cabin", "tire", "rim", "trim")}
    mounts = surface_mounts(sidecars["body"])
    car = CoupeGlb()
    srgb = np.array(ORANGE) / 255
    orange = ((srgb + 0.055) / 1.055) ** 2.4
    paint = car.material("Persimmon enamel", orange, 0.16, 0.31, clearcoat=True)
    decals = car.texture("Smile, bonnet stripes and door decals", body_atlas())
    character = car.material("Claude-mobile painted body", (1, 1, 1), 0.16, 0.31, clearcoat=True, texture=decals)
    glass = car.material("Graphite side glazing", (0.017, 0.031, 0.056), 0.40, 0.18)
    screen = car.material("Smoked windscreen and backlight", (0.025, 0.038, 0.061), 0.40, 0.16)
    dark = car.material("Satin graphite trim", (0.014, 0.017, 0.019), 0.28, 0.31)
    rubber = car.material("Performance tire", (0.023, 0.025, 0.027), 0.02, 0.83)
    alloy = car.material("Satin champagne alloy", (0.54, 0.42, 0.24), 0.70, 0.29)
    barrel = car.material("Smoked alloy barrel", (0.051, 0.062, 0.068), 0.78, 0.4)
    red = car.material("Ruby tail lamps", (0.48, 0.012, 0.016), 0.18, 0.19, emission=(0.42, 0.019, 0.021))

    shell = car.mesh("Claude-mobile body", native["body"],
                     body_materials(sidecars["body"], native["body"], character, dark),
                     ORIGIN, body_uvs(native["body"]))
    cabin = car.mesh("SubD cabin", native["cabin"], cabin_materials(sidecars["cabin"], native["cabin"], paint, glass, screen, dark), ORIGIN)
    tire = car.mesh("Revolved tire", native["tire"], rubber)
    rim = car.mesh("Revolved alloy", native["rim"], rim_materials(native["rim"], alloy, barrel))
    bezel = car.mesh("Bezel and grille", native["trim"], dark)
    tail = car.recolor(bezel, "Tail lens", red)
    paintTrim = car.recolor(bezel, "Painted mirror cap", paint)
    trim_positions, _, trim_faces = native["trim"]
    eye_uvs = (0.5 + trim_positions[:, (1, 0)] * np.array((0.05, -0.05)))[trim_faces]
    eye_meshes = {}
    for side, outside in (("Left", -1), ("Right", 1)):
        texture = car.texture(f"{side} iris and lashes", eye(outside))
        material = car.material(f"{side} painted headlight eye", (1, 1, 1), 0.05, 0.50, texture=texture, unlit=True)
        eye_meshes[side] = car.mesh(f"{side} headlight eye", native["trim"], material, face_uvs=eye_uvs)

    car.node("Body", mesh=shell)
    car.node("Cabin", mesh=cabin)
    wheels = car.node("Wheels")
    lamps = car.node("Lighting")
    fascia = car.node("Fascia")
    for name, axle, track, diameter_scale, width_scale in (
        ("Front", 0, 824, 354 / 335, 1.04), ("Rear", 2470, 852, 365 / 335, 1.12)
    ):
        for side, sign in (("Left", -1), ("Right", 1)):
            center = np.array((axle, sign * track, 335 * diameter_scale))
            pose = ((center - ORIGIN) @ BASIS.T * UNIT).tolist()
            pivot = car.node(name + side, wheels, translation=pose, scale=[width_scale, diameter_scale, diameter_scale],
                             rotation=[0, 1, 0, 0] if sign < 0 else [0, 0, 0, 1], extras={"localAxle": [1, 0, 0]})
            car.node("Tire", pivot, mesh=tire)
            car.node("Rim", pivot, mesh=rim)
            car.node("CenterCap", pivot, mesh=bezel, translation=[0.100, 0, 0], scale=[1.6, 4.5, 4.5])

    def detail(name, parent, mesh, center, radii, frame=np.eye(3)):
        transform = np.eye(4)
        transform[:3, :3] = BASIS @ frame @ np.diag(np.array(radii) / 10) @ BASIS.T
        transform[:3, 3] = BASIS @ (np.array(center) - ORIGIN) * UNIT
        car.node(name, parent, mesh=mesh, matrix=transform.T.flatten().tolist())

    for side, sign in (("Left", -1), ("Right", 1)):
        name = "Headlamp" + side
        center, frame = mounts[name]
        angle = np.deg2rad(28)
        c, s = np.cos(angle), np.sin(angle)
        eye_frame = frame @ np.array(((c, 0, -s), (0, 1, 0), (s, 0, c)))
        normal = eye_frame[:, 2]
        detail(name + "Housing", lamps, paintTrim, center - normal * 14, (184, 177, 70), eye_frame)
        detail(name, lamps, eye_meshes[side], center + normal * 5, (178, 171, 69), eye_frame)

        mirror = "Mirror" + side
        center, frame = mounts[mirror]
        crosswise, normal = frame[:, 1] * sign, frame[:, 2]
        detail(mirror + "Stalk", fascia, bezel, center + crosswise * 55 + normal * 20, (39, 78, 25), frame)
        detail(mirror, fascia, paintTrim, center + crosswise * 119 + normal * 53, (89, 76, 39), frame)
        detail("BumperDuct" + side, fascia, bezel, (-872, sign * 513, 276), (47, 126, 38))
        detail("TailLamp" + side, lamps, tail, (3215, sign * 376, 469), (17, 190, 22))

    detail("FrontSplitter", fascia, bezel, (-916, 0, 203), (28, 495, 19))
    detail("RearLightPanel", fascia, bezel, (3203, 0, 465), (24, 594, 42))
    detail("TailLightLink", lamps, tail, (3224, 0, 474), (8, 360, 9))
    detail("RearDiffuser", fascia, bezel, (3199, 0, 241), (24, 481, 28))
    car.write(folder / "claude-mobile.glb")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifact_directory", type=Path)
    package(parser.parse_args().artifact_directory)
