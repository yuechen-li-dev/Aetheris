"""Blender data ownership. Source and accepted STEP live in .blend text datablocks."""
import base64
import hashlib
from pathlib import Path
import struct
import uuid

import bpy

from .bridge import BuildError, relative_path, source_hash


def find_part(context, uid=""):
    if uid:
        parts = [o for o in context.scene.objects if o.aetheris.is_part and o.aetheris.uid == uid]
        return parts[0] if len(parts) == 1 else None
    area = context.area
    if area and area.type == "TEXT_EDITOR":
        text = area.spaces.active.text
        parts = [o for o in context.scene.objects if o.aetheris.is_part and text and
                 (o.aetheris.source == text or o.aetheris.diagnostics == text or any(r.text == text for r in o.aetheris.resources))]
        if len(parts) == 1:
            return parts[0]
        return None
    obj = context.active_object
    if obj and obj.aetheris.is_part:
        return obj
    if obj and obj.parent and obj.parent.aetheris.is_part and obj.get("aetheris_owner") == obj.parent.aetheris.uid:
        return obj.parent
    return None


def body_of(root):
    bodies = [o for o in root.children if o.type == "MESH" and o.get("aetheris_owner") == root.aetheris.uid]
    if len(bodies) > 1:
        raise BuildError("This CAD part has multiple managed meshes. Use New CAD Part or Create Mesh Copy to make independent objects.")
    return bodies[0] if bodies else None


def project_files(root):
    part = root.aetheris
    if not part.source:
        raise BuildError("Select a Firmament source text first.")
    main = relative_path(part.main_file)
    if not main.lower().endswith(".firmament"):
        raise BuildError("The main document needs a .firmament file name.")
    files = {main: part.source.as_string().encode("utf-8")}
    paths = {main.casefold()}
    for resource in part.resources:
        path = relative_path(resource.relative_path)
        if path.casefold() in paths:
            raise BuildError("Duplicate project file: " + path)
        if not resource.text:
            raise BuildError("Missing packed project text: " + path)
        paths.add(path.casefold())
        try:
            files[path] = base64.b64decode(resource.text.as_string(), validate=True) if resource.binary else resource.text.as_string().encode("utf-8")
        except ValueError as error:
            raise BuildError("Invalid packed binary resource: " + path) from error
    source_hash(files, main)
    return files, main


def current_hash(root):
    files, main = project_files(root)
    return source_hash(files, main)


def text_block(name, value):
    text = bpy.data.texts.new(name)
    text.use_fake_user = True
    text.write(value)
    return text


def new_part(context, source=None, main_file="part.firmament", directory=""):
    root = bpy.data.objects.new("Aetheris Part", None)
    context.collection.objects.link(root)
    root.location = context.scene.cursor.location
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = .025 / context.scene.unit_settings.scale_length
    part = root.aetheris
    part.is_part = True
    part.uid = uuid.uuid4().hex
    part.main_file = main_file
    part.import_directory = directory
    starter = source if source is not None else (Path(__file__).parent / "starter.firmament").read_text(encoding="utf-8")
    part.source = text_block(main_file, starter)
    for obj in context.selected_objects:
        obj.select_set(False)
    root.select_set(True)
    context.view_layer.objects.active = root
    return root


def mesh_hash(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update(struct.pack("<fff", *vertex.co))
    for polygon in mesh.polygons:
        digest.update(struct.pack("<I", len(polygon.vertices)))
        digest.update(struct.pack("<" + "I" * len(polygon.vertices), *polygon.vertices))
    return digest.hexdigest()


def mesh_edited(root):
    body = body_of(root)
    return bool(body and body.get("aetheris_mesh_hash") != mesh_hash(body.data))


def diagnostic(root, message):
    part = root.aetheris
    if not part.diagnostics:
        part.diagnostics = text_block("Aetheris diagnostics " + part.uid[:8], "")
    part.diagnostics.clear()
    part.diagnostics.write(message)
    part.status = message.splitlines()[0][:200] if message else "Ready"


def install_result(root, mesh_data, step, digest, scene):
    """Stage a complete new mesh before swapping one object's data. Keep object-owned presentation."""
    if current_hash(root) != digest:
        raise BuildError("Source changed while the compiler was running. Rebuild the current source; the last accepted part is unchanged.")
    body = body_of(root)
    factor = .001 / scene.unit_settings.scale_length
    positions, indices = mesh_data["positions"], mesh_data["indices"]
    vertices = [tuple(v * factor for v in positions[i:i + 3]) for i in range(0, len(positions), 3)]
    faces = [indices[i:i + 3] for i in range(0, len(indices), 3)]
    normals = [mesh_data["normals"][i:i + 3] for i in range(0, len(positions), 3)]
    mesh = bpy.data.meshes.new(root.name + " CAD display")
    try:
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        if mesh.validate(verbose=False, clean_customdata=False):
            raise BuildError("Blender rejected the compiler display topology. No repaired or partial mesh was installed.")
        for polygon in mesh.polygons:
            polygon.use_smooth = True
        mesh.normals_split_custom_set_from_vertices(normals)
        old = body.data if body else None
        reset_faces = False
        if old:
            for material in old.materials:
                mesh.materials.append(material)
            same_topology = len(old.polygons) == len(mesh.polygons) and all(tuple(a.vertices) == tuple(b.vertices) for a, b in zip(old.polygons, mesh.polygons))
            if same_topology:
                for new_polygon, old_polygon in zip(mesh.polygons, old.polygons):
                    new_polygon.material_index = old_polygon.material_index
            else:
                reset_faces = any(p.material_index != 0 for p in old.polygons)
        if not mesh.materials:
            material = bpy.data.materials.get("Aetheris CAD")
            if material is None:
                material = bpy.data.materials.new("Aetheris CAD")
                material.diffuse_color = (.16, .38, .55, 1)
                material.use_nodes = True
                shader = material.node_tree.nodes.get("Principled BSDF")
                shader.inputs["Base Color"].default_value = material.diffuse_color
                shader.inputs["Roughness"].default_value = .35
            mesh.materials.append(material)
    except Exception:
        bpy.data.meshes.remove(mesh)
        raise
    if body is None:
        body = bpy.data.objects.new(root.name + " · CAD", mesh)
        root.users_collection[0].objects.link(body)
        body.parent = root
        body["aetheris_owner"] = root.aetheris.uid
    else:
        body.data = mesh
    body["aetheris_mesh_hash"] = mesh_hash(mesh)
    part = root.aetheris
    if not part.step:
        part.step = text_block(".Aetheris STEP " + part.uid[:8], "")
    part.step.clear()
    part.step.write(step)
    part.step_hash = hashlib.sha256(step.encode()).hexdigest()
    part.accepted_hash = digest
    part.revision += 1
    part.status = f"Built revision {part.revision} · {len(faces):,} display triangles"
    if reset_faces:
        part.status += " · topology changed: face materials reset; slots retained"
    if part.diagnostics:
        part.diagnostics.clear()
    if old and old.users == 0:
        bpy.data.meshes.remove(old)
    return body


def accepted_step(root):
    part = root.aetheris
    if not part.step or current_hash(root) != part.accepted_hash:
        raise BuildError("Build the current source successfully before exporting STEP.")
    step = part.step.as_string()
    if hashlib.sha256(step.encode()).hexdigest() != part.step_hash:
        raise BuildError("The saved STEP text changed. Rebuild before exporting.")
    return step
