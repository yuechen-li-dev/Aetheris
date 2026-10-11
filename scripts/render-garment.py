"""Render the real CLI garment OBJ artifacts; no geometry fitting or simulation in Blender.

Run Blender with --background --python scripts/render-garment.py -- --bottom shorts.
"""

import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def material(name, color, roughness):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = roughness
    return result


def load_obj(path, surface):
    vertices = []
    faces = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("v "):
            vertices.append(tuple(float(value) for value in line.split()[1:4]))
        elif line.startswith("f "):
            faces.append(tuple(int(value.split("/")[0]) - 1 for value in line.split()[1:]))
    mesh = bpy.data.meshes.new(path.stem)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    result = bpy.data.objects.new(path.parent.name + "_" + path.stem, mesh)
    bpy.context.collection.objects.link(result)
    result.data.materials.append(surface)
    for face in result.data.polygons:
        face.use_smooth = True
    return result


def load_control_cage(path):
    """Present the retained control positions; these lines do not modify the garment."""
    vertices = []
    lines = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("v "):
            vertices.append(tuple(float(value) for value in line.split()[1:4]))
        elif line.startswith("l "):
            lines.append([int(value) - 1 for value in line.split()[1:]])
    curve = bpy.data.curves.new("retained control cage", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = .0012
    curve.bevel_resolution = 2
    for indices in lines:
        spline = curve.splines.new("POLY")
        spline.points.add(len(indices) - 1)
        for point, index in zip(spline.points, indices):
            point.co = (*vertices[index], 1)
    obj = bpy.data.objects.new("retained control cage", curve)
    bpy.context.collection.objects.link(obj)
    curve.materials.append(material("control cage orange", (.95, .35, .03), .7))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--bottom", choices=("shorts", "skirt", "flared-skirt"), default="skirt")
    parser.add_argument("--garment", help="Render one garment instead of composing a tunic and bottom.")
    parser.add_argument("--input-dir", type=Path, help="Directory containing the actual garment.obj and body.obj exports.")
    parser.add_argument("--control-cage", type=Path, help="Optional retained OBJ polylines for source inspection.")
    parser.add_argument("--torso", action="store_true", help="Frame the fitted upper garment for inspection.")
    parser.add_argument("--initial", action="store_true", help="Render the exported rest mesh before simulation.")
    parser.add_argument("--samples", type=int, default=32)
    parser.add_argument("--engine", choices=("cycles", "workbench"), default="cycles",
                        help="Workbench gives a quick neutral geometry diagnostic; Cycles remains the default.")
    parser.add_argument("--frame", choices=("guided", "fitting", "midway", "wearing"), help="Render a retained solver checkpoint.")
    options = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    source = options.root / "artifacts/local/garment"
    output = source / "renders" / (options.garment or options.bottom)
    if options.frame:
        if not options.garment:
            parser.error("--frame requires --garment")
        output = output / options.frame
    if options.initial:
        output = output / "initial"
    if options.control_cage:
        output = output / "cage"
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    if options.garment:
        inputs = options.input_dir or source / options.garment
        prefix = options.frame + "-" if options.frame else ""
        body_path = inputs / (prefix + "body.obj")
        garment_path = inputs / (prefix + "garment.obj")
        if options.initial:
            if options.frame:
                parser.error("--initial and --frame select different checkpoints")
            garment_path = inputs / "initial-garment.obj"
        garment_inputs = [body_path, garment_path]
    else:
        body_path = source / options.bottom / "body.obj"
        tunic_path = source / "tunic/garment.obj"
        bottom_path = source / options.bottom / "garment.obj"
        garment_inputs = [body_path, tunic_path, bottom_path]
    load_obj(body_path, material("neutral mannequin", (.48, .47, .43), .65))
    if options.garment:
        load_obj(garment_path, material("untextured dress", (.035, .19, .18), .8))
    else:
        load_obj(tunic_path, material("untextured tunic", (.08, .24, .31), .8))
        load_obj(bottom_path, material("untextured bottom", (.11, .14, .22), .8))
    if options.control_cage:
        load_control_cage(options.control_cage)
        garment_inputs.append(options.control_cage)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.005))
    bpy.context.object.data.materials.append(material("floor", (.24, .26, .29), .9))
    scene = bpy.context.scene
    if options.engine == "workbench":
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.render_aa = "32"
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "MATERIAL"
        shading.show_shadows = False
        shading.show_cavity = False
        shading.background_type = "WORLD"
        scene.world.color = (.24, .26, .29)
    else:
        scene.render.engine = "CYCLES"
        scene.cycles.samples = options.samples
        scene.cycles.use_denoising = True
    scene.render.resolution_x = 800
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.35, .38, .44, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .35
    for name, location, power, size in (
        ("key", (2, 3, 4), 650, 3),
        ("fill", (-3, 1, 2), 300, 3),
        ("rim", (0, -3, 3), 500, 2),
    ):
        light = bpy.data.lights.new(name, "AREA")
        light.energy = power
        light.shape = "DISK"
        light.size = size
        obj = bpy.data.objects.new(name, light)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (Vector((0, 0, 1)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    camera = bpy.data.objects.new("camera", bpy.data.cameras.new("camera"))
    bpy.context.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.05
    target = Vector((0, -.035, .9))
    if options.torso:
        camera.data.ortho_scale = .68
        target = Vector((0, -.035, 1.31))
    scene.camera = camera
    for name, location in (
        ("front", (0, 4, 1.2)),
        ("side", (4, -.035, 1.2)),
        ("back", (0, -4, 1.2)),
    ):
        camera.location = location
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(output / (name + ".png"))
        bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "garment-preview.blend"))
    evidence = {
        "geometryAuthority": "Aetheris garment runtime exports; Blender only presents retained OBJ meshes",
        "inputs": {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in garment_inputs},
        "garment": options.garment or options.bottom,
        "frame": options.frame,
        "initial": options.initial,
        "controlCage": str(options.control_cage) if options.control_cage else None,
        "textures": False,
        "renderer": options.engine,
    }
    (output / "render-evidence.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
