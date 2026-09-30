"""Render the real USD import in Blender Cycles; no replacement product meshes.

blender --background --factory-startup --python scripts/render-industrial-atlas.py --
    artifacts/local/usd-industrial/atlas-studio.usda artifacts/local/usd-industrial/atlas-hero.png
"""
import bpy
import json
import sys
import time
from pathlib import Path

args = sys.argv[sys.argv.index("--") + 1:]
source, output = (Path(p).resolve() for p in args[:2])
preview = "--preview" in args
motion = "--motion" in args
start = time.perf_counter()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.usd_import(filepath=str(source), support_scene_instancing=True,
                      import_materials=True, import_usd_preview=True, apply_unit_conversion_scale=True)
load_seconds = time.perf_counter() - start
scene = bpy.context.scene
camera = next(o for o in scene.objects if o.type == "CAMERA" and "HeroCamera" in o.name)
scene.camera = camera
scene.render.engine = "CYCLES"
scene.cycles.samples = 24 if motion else 32 if preview else 192
scene.cycles.use_denoising = True
prefs = bpy.context.preferences.addons["cycles"].preferences
prefs.compute_device_type = "OPTIX"
prefs.get_devices()
for device in prefs.devices:
    device.use = device.type == "OPTIX"
scene.cycles.device = "GPU" if any(d.use for d in prefs.devices) else "CPU"
# USD light intensities are renderer-dependent. These are presentation settings,
# recorded here, applied only after import; every engineering mesh stays intact.
for obj in scene.objects:
    if obj.type == "LIGHT":
        # Blender 5.2 imports this mm stage's camera/meshes in metres, but leaves
        # RectLight positions and dimensions in stage units. Correct only the
        # presentation lights; this script is specifically for our .001 stage.
        transform = obj.matrix_world.copy()
        transform.translation *= .001
        obj.matrix_world = transform
        obj.data.size *= .001
        obj.data.size_y *= .001
        obj.data.energy = {"Key": 80, "Rim": 80, "Fill": 40}.get(obj.name, 8)
if scene.world is None:
    scene.world = bpy.data.worlds.new("StudioWorld")
scene.world.use_nodes = True
background = scene.world.node_tree.nodes.get("Background")
if background:
    background.inputs["Color"].default_value = (.035, .044, .06, 1)
    background.inputs["Strength"].default_value = .35
scene.render.resolution_x = 1280 if preview or motion else 2560
scene.render.resolution_y = 720 if preview or motion else 1440
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(output)
scene.frame_set(48 if scene.frame_end >= 48 else 0)
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output.with_suffix(".blend")))
render_start = time.perf_counter()
if motion:
    for frame in range(0,97,4):
        scene.frame_set(frame)
        scene.render.filepath = str(output.parent / f"motion-{frame:03d}.png")
        bpy.ops.render.render(write_still=True)
else:
    bpy.ops.render.render(write_still=True)
report = dict(tool="Blender", version=bpy.app.version_string, input=str(source),
              engine="Cycles", device=scene.cycles.device,
              devices=[d.name for d in prefs.devices if d.use],
              loadSeconds=load_seconds, renderSeconds=time.perf_counter()-render_start,
              meshObjects=sum(o.type == "MESH" for o in scene.objects),
              resolution=[scene.render.resolution_x, scene.render.resolution_y],
              samples=scene.cycles.samples, output=str(output))
output.with_suffix(".json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report))
