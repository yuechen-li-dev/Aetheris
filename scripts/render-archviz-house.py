"""Render the compiled house GLB; no layout, furniture, or replacement meshes.

blender --background --factory-startup --python-exit-code 1 --python
  scripts/render-archviz-house.py -- house.glb output-directory [--preview]
Presentation owns shader detail, daylight and a disclosed overview cutaway.
All camera transforms, materials and physical geometry come from Firmament.
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
parser.add_argument('--view', choices=['Hero', 'Kitchen', 'Overview'])
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
args.output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
start = time.perf_counter()
bpy.ops.import_scene.gltf(filepath=str(args.source.resolve()))
scene = bpy.context.scene
products = [o for o in scene.objects if o.type == 'MESH']
cameras = {o.name: o for o in scene.objects if o.type == 'CAMERA'}
assert all(name in cameras for name in ('Hero', 'Kitchen', 'Overview'))
points = [o.matrix_world @ Vector(c) for o in products for c in o.bound_box]
lo = Vector([min(p[i] for p in points) for i in range(3)])
hi = Vector([max(p[i] for p in points) for i in range(3)])
assert all(o.matrix_world.determinant() > 0 for o in products)

def geometry_hash():
    result = hashlib.sha256()
    for obj in sorted(products, key=lambda o: o.name):
        result.update(obj.name.encode())
        for vertex in obj.data.vertices:
            result.update(str(tuple(vertex.co)).encode())
        for polygon in obj.data.polygons:
            result.update(str(tuple(polygon.vertices)).encode())
        result.update(str(tuple(tuple(row) for row in obj.matrix_world)).encode())
    return result.hexdigest()

before = geometry_hash()
for obj in products:
    if '.floor.panel' not in obj.name:
        continue
    for slot in obj.material_slots:
        if slot.material and slot.material.name.startswith('warmOak'):
            slot.material = slot.material.copy()
            slot.material.name = 'warmOak.floor'
for material in bpy.data.materials:
    if not material.use_nodes:
        continue
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None:
        continue
    name = material.name.split('.')[0]
    if name == 'clearGlass':
        # Portable authored opacity becomes an ordinary dielectric downstream.
        bsdf.inputs['Alpha'].default_value = 1
        bsdf.inputs['Transmission Weight'].default_value = 1
        bsdf.inputs['IOR'].default_value = 1.12
        bsdf.inputs['Base Color'].default_value = (.94, .98, .97, 1)
    if name in ('warmOak', 'creamFabric', 'linenRug'):
        tex = nodes.new('ShaderNodeTexNoise')
        mapping = nodes.new('ShaderNodeVectorMath')
        mapping.operation = 'MULTIPLY'
        coordinates = nodes.new('ShaderNodeTexCoord')
        mapping.inputs[1].default_value = (2, 85, 4) if name == 'warmOak' else (180, 180, 180)
        links.new(coordinates.outputs['Generated'], mapping.inputs[0])
        links.new(mapping.outputs[0], tex.inputs['Vector'])
        tex.inputs['Scale'].default_value = 2
        tex.inputs['Detail'].default_value = 2
        bump = nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = .12 if name == 'warmOak' else .18
        bump.inputs['Distance'].default_value = .001 if name == 'warmOak' else .0006
        links.new(tex.outputs['Fac'], bump.inputs['Height'])
        links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
        if name == 'warmOak':
            ramp = nodes.new('ShaderNodeValToRGB')
            base = tuple(bsdf.inputs['Base Color'].default_value)
            ramp.color_ramp.elements[0].color = tuple(v * .72 for v in base[:3]) + (1,)
            ramp.color_ramp.elements[1].color = tuple(min(1, v * 1.17) for v in base[:3]) + (1,)
            links.new(tex.outputs['Fac'], ramp.inputs[0])
            links.new(ramp.outputs[0], bsdf.inputs['Base Color'])
        if material.name.startswith('warmOak.floor'):
            # A restrained board pattern is shader detail over the room slab.
            geometry = nodes.new('ShaderNodeNewGeometry')
            brick = nodes.new('ShaderNodeTexBrick')
            links.new(geometry.outputs['Position'], brick.inputs['Vector'])
            brick.inputs['Scale'].default_value = 1
            brick.inputs['Brick Width'].default_value = 1.6
            brick.inputs['Row Height'].default_value = .18
            brick.inputs['Mortar Size'].default_value = .002
            brick.inputs['Mortar Smooth'].default_value = .001
            brick.inputs['Color1'].default_value = (.41, .27, .135, 1)
            brick.inputs['Color2'].default_value = (.61, .42, .23, 1)
            brick.inputs['Mortar'].default_value = (.26, .17, .095, 1)
            links.new(brick.outputs['Color'], bsdf.inputs['Base Color'])

# Light fixtures are authored geometry; renderer lights derive from their diffusers.
def area(name, position, target, energy, size, color=(1, .87, .68)):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.size, data.color = energy, size, color
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    return obj

centre = (lo + hi) / 2
for obj in products:
    if '.diffuser' in obj.name:
        p = sum((obj.matrix_world @ Vector(c) for c in obj.bound_box), Vector()) / 8
        area('Pendant illumination ' + obj.name, p - Vector((0, 0, .02)), p - Vector((0, 0, 1)), 55, .23)
    if obj.name.rsplit('.', 1)[-1] == 'pane':
        p = sum((obj.matrix_world @ Vector(c) for c in obj.bound_box), Vector()) / 8
        # Place daylight just inside each compiled aperture, aimed into its room.
        inward = Vector((centre.x - p.x, centre.y - p.y, 0)).normalized()
        area('Window daylight ' + obj.name, p + inward * .06, p + inward,
             650, 3.5, (1, .94, .82))
area('Soft ceiling bounce', (centre.x, centre.y, hi.z - .18), (centre.x, centre.y, 0), 160, 5)
scene.world = bpy.data.worlds.new('Warm daylight')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.78, .84, .87, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45
sun_data = bpy.data.lights.new('Late afternoon', 'SUN')
sun_data.energy, sun_data.angle = .8, math.radians(12)
sun_data.color = (1, .87, .66)
sun = bpy.data.objects.new(sun_data.name, sun_data)
scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(32), math.radians(-24), math.radians(-35))

scene.render.engine = 'CYCLES'
scene.cycles.samples = 16 if args.preview else 80
scene.cycles.use_denoising = True
device = 'CPU'
try:
    preferences = bpy.context.preferences.addons['cycles'].preferences
    preferences.compute_device_type = 'OPTIX'
    preferences.get_devices()
    for available in preferences.devices:
        available.use = available.type == preferences.compute_device_type
    if any(d.use for d in preferences.devices):
        scene.cycles.device = 'GPU'
        device = ', '.join(d.name for d in preferences.devices if d.use)
except Exception:
    pass
scene.render.resolution_x = 960 if args.preview else 1920
scene.render.resolution_y = 600 if args.preview else 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
scene.view_settings.look = 'AgX - Medium High Contrast'
scene.view_settings.exposure = 0
timings = {}
hidden = []
for name in ([args.view] if args.view else ['Hero', 'Kitchen', 'Overview']):
    for obj in products:
        # Disclosed presentation cutaway. Full authored enclosure stays in USD/GLB.
        obj.hide_render = name == 'Overview' and any('.' + b + '.' in obj.name for b in ('ceiling', 'southWall', 'eastWall'))
    if name == 'Overview':
        hidden = [o.name for o in products if o.hide_render]
    scene.camera = cameras[name]
    scene.camera.data.clip_start = .01
    scene.camera.data.clip_end = 200
    scene.render.filepath = str((args.output / (name.lower() + '.png')).resolve())
    timer = time.perf_counter()
    bpy.ops.render.render(write_still=True)
    timings[name] = time.perf_counter() - timer
for obj in products:
    obj.hide_render = False
scene.camera = cameras['Hero']
assert geometry_hash() == before, 'Renderer changed compiled geometry'
bpy.ops.wm.save_as_mainfile(filepath=str((args.output / 'house.blend').resolve()))
report = dict(tool='Blender', version=bpy.app.version_string, device=device,
              sourceSha256=hashlib.sha256(args.source.read_bytes()).hexdigest(),
              geometryHash=before, geometryUnchanged=True,
              meshOccurrences=len(products), sharedMeshData=len({o.data.as_pointer() for o in products}),
              presentationLights=len([o for o in scene.objects if o.type == 'LIGHT']),
              boundsMetres=dict(min=list(lo), max=list(hi)), cameras=list(cameras),
              overviewHiddenBoundaries=hidden, renderSeconds=timings, totalSeconds=time.perf_counter() - start)
(args.output / 'blender-validation.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
