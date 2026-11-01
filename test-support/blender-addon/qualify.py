"""Run with Blender's Python against an installed extension and the real local compiler."""
import argparse
import hashlib
import importlib
import json
from pathlib import Path
import sys

import addon_utils
import bpy
import bmesh
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument("--module", default="bl_ext.aetheris_test.aetheris_cad")
parser.add_argument("--source-addon", type=Path)
parser.add_argument("--compiler", type=Path)
parser.add_argument("--output", required=True, type=Path)
parser.add_argument("--repo", required=True, type=Path)
parser.add_argument("--phase", choices=("create", "reopen"), default="create")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
out = args.output.resolve()
out.mkdir(parents=True, exist_ok=True)
if args.source_addon:
    sys.path.insert(0, str(args.source_addon.resolve()))
addon_utils.enable(args.module, default_set=True, persistent=True)
addon = importlib.import_module(args.module)
if args.compiler:
    bpy.context.preferences.addons[args.module].preferences.executable_path = str(args.compiler.resolve())
state = addon.state


def select(root):
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    root.select_set(True)
    bpy.context.view_layer.objects.active = root


def build(root, **kwargs):
    select(root)
    result = bpy.ops.aetheris.build(**kwargs)
    assert result == {"FINISHED"}, root.aetheris.diagnostics.as_string() if root.aetheris.diagnostics else root.aetheris.status
    return state.body_of(root)


def dimensions(root):
    body = state.body_of(root)
    xs = [v.co.x for v in body.data.vertices]
    ys = [v.co.y for v in body.data.vertices]
    zs = [v.co.z for v in body.data.vertices]
    return [max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)]


def expect_dimensions(root, values):
    for actual, expected in zip(dimensions(root), values):
        # Blender stores float32 mesh positions; compare physical metres, including unit scale.
        assert abs(actual - expected) * bpy.context.scene.unit_settings.scale_length < 1e-7, (actual, expected)


def replace(text, source):
    text.clear()
    text.write(source)


def export(root, filename):
    select(root)
    path = out / filename
    assert bpy.ops.aetheris.export_step(filepath=str(path)) == {"FINISHED"}
    return path


if args.phase == "reopen":
    root = bpy.data.objects["CAD Plate"]
    select(root)
    accepted = root.aetheris.accepted_hash
    assert state.current_hash(root) == accepted
    assert state.accepted_step(root).startswith("ISO-10303-21;")
    expect_dimensions(root, [.1, .05, .008])
    material = state.body_of(root).data.materials[0]
    build(root)
    assert state.body_of(root).data.materials[0] == material
    assert abs(root.location.x - .025) < 1e-8
    assert abs(state.body_of(root).location.x - .003) < 1e-8
    packed = bpy.data.objects["Packed Plate"]
    assert len(packed.aetheris.resources) == 1
    build(packed)
    expect_dimensions(packed, [.04, .03, .012])
    export(root, "reopened.step")
    export(packed, "reopened-packed.step")
    (out / "reopen.json").write_text(json.dumps({"success": True, "saved_source": True, "saved_dependencies": True,
        "compiler_override": bool(args.compiler), "dimensions": dimensions(root)}, indent=2))
    print("AETHERIS_BLENDER_REOPEN_OK")
else:
    # Only this factory-startup test scene is modified.
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    assert bpy.ops.aetheris.new_part() == {"FINISHED"}
    root = bpy.context.active_object
    root.name = "CAD Plate"
    body = build(root)
    expect_dimensions(root, [.08, .05, .008])
    identity = body.as_pointer()
    source = root.aetheris.source.as_string()
    material = bpy.data.materials.new("Qualification brass")
    material.diffuse_color = (.55, .28, .07, 1)
    material.use_nodes = True
    shader = material.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = material.diffuse_color
    shader.inputs["Metallic"].default_value = .7
    shader.inputs["Roughness"].default_value = .3
    body.data.materials.clear()
    body.data.materials.append(material)
    root.location = (.025, -.025, .005)
    root.rotation_euler.z = .15
    body.location.x = .003
    transform = body.matrix_basis.copy()
    replace(root.aetheris.source, source.replace("80mm", "100mm"))
    body = build(root)
    assert body.as_pointer() == identity
    assert body.matrix_basis == transform
    assert body.data.materials[0] == material
    expect_dimensions(root, [.1, .05, .008])
    good = root.aetheris.source.as_string()
    accepted_mesh = state.mesh_hash(body.data)
    accepted_step = state.accepted_step(root)
    replace(root.aetheris.source, good.replace("diameter: 8mm", "diameter: -8mm"))
    assert bpy.ops.aetheris.build() == {"CANCELLED"}
    assert state.mesh_hash(body.data) == accepted_mesh
    assert root.aetheris.step.as_string() == accepted_step
    assert root.aetheris.diagnostics.as_string()
    try:
        state.accepted_step(root)
        raise AssertionError("A stale source exported STEP")
    except addon.BuildError:
        pass
    replace(root.aetheris.source, good)
    build(root)
    # Native mesh editing remains independent of exact CAD.
    assert bpy.ops.aetheris.mesh_copy() == {"FINISHED"}
    copy = bpy.context.active_object
    assert copy.data != body.data and copy.parent is None
    editable = bmesh.new()
    try:
        editable.from_mesh(copy.data)
        assert all(edge.is_manifold for edge in editable.edges), "Mesh copy has open display seams"
        volume_mm3 = abs(editable.calc_volume()) * 1e9
        import math
        assert abs(volume_mm3 - (100 * 50 * 8 - math.pi * 4 * 4 * 8)) < 5, volume_mm3
    finally:
        editable.free()
    copy.data.vertices[0].co.x += .001
    copy.hide_render = True
    copy.hide_set(True)
    select(root)
    body.data.vertices[0].co.x += .001
    assert bpy.ops.aetheris.build() == {"CANCELLED"}
    assert "vertex edits" in root.aetheris.status
    build(root, replace_edited=True)
    assert not state.mesh_edited(root)
    assert copy.data != body.data
    assert state.body_of(root).data.materials[0] == material
    export(root, "plate.step")
    # Build from packed dependencies, then take the original files away before reopening.
    external = out / "original-project"
    external.mkdir(exist_ok=True)
    fixture_dir = args.repo / "fixtures/Regression/BlenderAddon"
    for name in ("packed-plate.firmament", "parameters.firmament"):
        (external / name).write_bytes((fixture_dir / name).read_bytes())
    assert bpy.ops.aetheris.import_source(filepath=str(external / "packed-plate.firmament")) == {"FINISHED"}
    packed = bpy.context.active_object
    packed.name = "Packed Plate"
    assert bpy.ops.aetheris.build() == {"CANCELLED"}
    assert state.body_of(packed) is None
    assert bpy.ops.aetheris.add_resource(filepath=str(external / "parameters.firmament")) == {"FINISHED"}
    build(packed)
    expect_dimensions(packed, [.04, .03, .008])
    replace(packed.aetheris.resources[0].text, "Static Depth: Length = 12mm\n")
    build(packed)
    expect_dimensions(packed, [.04, .03, .012])
    packed.location = (-.055, .035, .005)
    # Source edits during an asynchronous compiler job cannot replace accepted geometry.
    select(root)
    files, main = state.project_files(root)
    job = addon.BuildJob(addon.executable(bpy.context), files, main)
    replace(root.aetheris.source, good.replace("100mm", "110mm"))
    try:
        import time
        while job.poll() is None:
            time.sleep(.02)
        mesh, step = job.result()
        try:
            state.install_result(root, mesh, step, job.digest, bpy.context.scene)
            raise AssertionError("A stale asynchronous result replaced the accepted part")
        except addon.BuildError as error:
            assert "Source changed" in str(error)
    finally:
        job.close()
    replace(root.aetheris.source, good)
    expect_dimensions(root, [.1, .05, .008])
    # Different Blender unit scales still represent the same millimetre geometry.
    bpy.context.scene.unit_settings.scale_length = .001
    body = build(root)
    expect_dimensions(root, [100, 50, 8])
    bpy.context.scene.unit_settings.scale_length = 1
    body = build(root)
    expect_dimensions(root, [.1, .05, .008])
    export(root, "plate.step")
    # A real Blender render of the compiler's geometry, alongside a native Blender floor.
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new("CAD studio")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.14, .17, .22, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = .4
    floor_mesh = bpy.data.meshes.new("Blender floor")
    floor_mesh.from_pydata([(-1,-1,0), (1,-1,0), (1,1,0), (-1,1,0)], [], [(0,1,2,3)])
    floor = bpy.data.objects.new("Native Blender floor", floor_mesh)
    scene.collection.objects.link(floor)
    floor_material = bpy.data.materials.new("Studio floor")
    floor_material.diffuse_color = (.07, .09, .12, 1)
    floor_material.use_nodes = True
    floor_material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = floor_material.diffuse_color
    floor_mesh.materials.append(floor_material)
    camera_data = bpy.data.cameras.new("CAD camera")
    camera = bpy.data.objects.new("CAD camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (.19, -.23, .21)
    camera.rotation_euler = (Vector((0,0,.005)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 50
    camera_data.clip_start = .001
    scene.camera = camera
    for name, location, energy, size in [("Key", (.1,-.1,.2), 2, .15), ("Fill", (-.12,.05,.15), 1, .12)]:
        light = bpy.data.lights.new(name, "AREA")
        light.energy, light.shape, light.size = energy, "DISK", size
        obj = bpy.data.objects.new(name, light)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (-obj.location).to_track_quat("-Z", "Y").to_euler()
    select(root)
    bpy.context.view_layer.update()
    scene.render.filepath = str(out / "cad-in-blender.png")
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out / "qualified.blend"))
    for path in external.iterdir():
        if path.suffix != ".unavailable":
            path.replace(path.with_suffix(path.suffix + ".unavailable"))
    (out / "create.json").write_text(json.dumps({"success": True, "module": args.module, "compiler_override": bool(args.compiler),
        "revision": root.aetheris.revision, "dimensions": dimensions(root), "triangles": len(body.data.polygons),
        "identity_preserved": True, "material_preserved": True, "placement_preserved": True, "failed_build_retained": True,
        "stale_result_refused": True, "mesh_copy_independent": True, "unit_scale_qualified": True,
        "packed_dependency": True, "original_dependency_removed": True,
        "step_sha256": hashlib.sha256((out / "plate.step").read_bytes()).hexdigest()}, indent=2))
    print("AETHERIS_BLENDER_CREATE_OK")
