"""Render the real Scene GLB. No generated/replacement equipment or layout.

blender --background --python-exit-code 1 --python scripts/render-factory-scene-x0.py
  -- factory.glb artifacts/local/factory-scene-x0 [--preview] [--view Hero]
Renderer-only area lights derive from authored luminaire locations. Floor bump
is finish detail. Camera transforms and every product mesh remain unchanged.
"""
import argparse
import hashlib
import json
import math
import sys
import time
from pathlib import Path
import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('source', type=Path)
parser.add_argument('output', type=Path)
parser.add_argument('--preview', action='store_true')
parser.add_argument('--view', choices=['Hero', 'Overview', 'Line', 'Robot', 'Logistics'])
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
args.output.mkdir(parents=True, exist_ok=True)
started = time.perf_counter()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(args.source.resolve()))
scene = bpy.context.scene
products = [o for o in scene.objects if o.type == 'MESH']
cameras = {o.name: o for o in scene.objects if o.type == 'CAMERA'}
assert len(cameras) == 5 and len(products) > 1000
assert all(o.matrix_world.determinant() > 0 and o.data.polygons for o in products)
points = [o.matrix_world @ Vector(c) for o in products for c in o.bound_box]
lo = [min(p[i] for p in points) for i in range(3)]
hi = [max(p[i] for p in points) for i in range(3)]
assert all(math.isfinite(v) for v in lo + hi)

def geometry_hash():
    result = hashlib.sha256()
    for obj in sorted(products, key=lambda o: o.name):
        result.update(obj.name.encode())
        result.update(str(tuple(tuple(row) for row in obj.matrix_world)).encode())
        for vertex in obj.data.vertices:
            result.update(str(tuple(vertex.co)).encode())
        for polygon in obj.data.polygons:
            result.update(str(tuple(polygon.vertices)).encode())
    return result.hexdigest()

before = geometry_hash()
for material in bpy.data.materials:
    if not material.use_nodes or material.name.split('.')[0] != 'concrete':
        continue
    nodes, links = material.node_tree.nodes, material.node_tree.links
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 95
    noise.inputs['Detail'].default_value = 2
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = .1
    bump.inputs['Distance'].default_value = .00025
    links.new(noise.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

def area(name, location, target, watts, size, color=(.91, .96, 1)):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.size, data.color = watts, size, color
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

for obj in products:
    if obj.name.endswith('.diffuser'):
        p = sum((obj.matrix_world @ Vector(c) for c in obj.bound_box), Vector()) / 8
        area('Luminaire illumination ' + obj.name, p - Vector((0, 0, .02)), p - Vector((0, 0, 1)), 350, 3)
area('Front soft daylight', (18, -2, 8), (18, 14, 0), 3000, 22)
area('North soft daylight', (20, 26, 5.8), (20, 12, 1), 2500, 18)
scene.world = bpy.data.worlds.new('Neutral industrial presentation')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.55, .67, .82, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .25
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16 if args.preview else 32
scene.cycles.use_denoising = True
scene.cycles.use_adaptive_sampling = True
scene.cycles.adaptive_threshold = .08 if args.preview else .04
scene.cycles.time_limit = 60
scene.cycles.max_bounces = 6
device = 'CPU'
try:
    preferences = bpy.context.preferences.addons['cycles'].preferences
    preferences.compute_device_type = 'OPTIX'
    preferences.get_devices()
    for available in preferences.devices:
        available.use = available.type == 'OPTIX'
    if any(d.use for d in preferences.devices):
        scene.cycles.device = 'GPU'
        device = ', '.join(d.name for d in preferences.devices if d.use)
except Exception:
    pass
scene.render.resolution_x = 1200 if args.preview else 2560
scene.render.resolution_y = 675 if args.preview else 1440
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
scene.view_settings.look = 'AgX - Medium High Contrast'
timings = {}
for name in ([args.view] if args.view else list(cameras)):
    scene.camera = cameras[name]
    scene.camera.data.clip_start, scene.camera.data.clip_end = .01, 250
    scene.render.filepath = str((args.output / (name.lower() + '.png')).resolve())
    timer = time.perf_counter()
    print('Rendering', name, device, scene.cycles.samples, 'samples', flush=True)
    bpy.ops.render.render(write_still=True)
    timings[name] = time.perf_counter() - timer
assert geometry_hash() == before, 'Renderer changed compiled geometry'
scene.camera = cameras['Hero']
bpy.ops.wm.save_as_mainfile(filepath=str((args.output / 'factory.blend').resolve()))
report = dict(tool='Blender', version=bpy.app.version_string, device=device,
              sourceSha256=hashlib.sha256(args.source.read_bytes()).hexdigest(),
              meshOccurrences=len(products), sharedMeshData=len({o.data.as_pointer() for o in products}),
              geometryUnchanged=True, geometryHash=before, boundsMetres=dict(min=lo, max=hi),
              cameras=list(cameras), renderSeconds=timings, totalSeconds=time.perf_counter() - started,
              maximumSamples=scene.cycles.samples, renderTimeLimitSeconds=scene.cycles.time_limit,
              presentationLights=len([o for o in scene.objects if o.type == 'LIGHT']))
(args.output / 'blender-validation.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
