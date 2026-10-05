"""Independent Blender import and useful GLB fallback render, no geometry replacement.
blender --background --python scripts/inspect-presentation-glb.py -- <model.glb> <evidence-dir>
"""
import bpy
import json
import math
import sys
import time
from pathlib import Path
from mathutils import Vector

source, output = map(lambda p: Path(p).resolve(), sys.argv[sys.argv.index('--') + 1:][:2])
output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
started = time.perf_counter()
bpy.ops.import_scene.gltf(filepath=str(source))
load_seconds = time.perf_counter() - started
scene = bpy.context.scene
products = [o for o in scene.objects if o.type == 'MESH']
assert products
points = [o.matrix_world @ Vector(corner) for o in products for corner in o.bound_box]
lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
size, center = hi-lo, (hi+lo)/2
assert all(math.isfinite(v) for p in points for v in p)
assert .05 < max(size) < 2, 'Expected mm engineering models converted to physical metres'
for obj in products:
    assert obj.data.polygons
    assert all(abs(v.normal.length-1) < 1e-3 for v in obj.data.vertices)
    assert obj.matrix_world.determinant() > 0
images = [i for i in bpy.data.images if i.type == 'IMAGE']
for image in images:
    assert image.size[0] > 0 and image.size[1] > 0, 'Missing texture'
materials = []
for m in bpy.data.materials:
    if not m.use_nodes: continue
    p = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if p:
        materials.append(dict(name=m.name, metallic=p.inputs['Metallic'].default_value,
                              roughness=p.inputs['Roughness'].default_value,
                              textured=p.inputs['Base Color'].is_linked))
report = dict(tool='Blender', version=bpy.app.version_string, file=source.name,
              loadSeconds=load_seconds, meshObjects=len(products), sharedMeshData=len({o.data.name for o in products}),
              triangles=sum(len(o.data.loop_triangles) for o in products),
              boundsMetres=dict(min=list(lo), max=list(hi), size=list(size)),
              materials=materials, images=[dict(name=i.name, size=list(i.size)) for i in images],
              hierarchy=[dict(name=o.name, parent=o.parent.name if o.parent else None) for o in scene.objects],
              normalAndTransformChecks='passed')
output.joinpath(source.stem+'.blender.json').write_text(json.dumps(report, indent=2)+'\n')
camera_data = bpy.data.cameras.new('Aetheris Presentation Camera')
camera = bpy.data.objects.new('Aetheris Presentation Camera', camera_data); scene.collection.objects.link(camera)
camera.location = center + Vector((.85,-1.5,1.0) if source.stem.startswith('atlas') else (.9,-.8,1.3)) * max(size)
camera.rotation_euler = (center-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO'; camera_data.ortho_scale=max(size)*1.45; scene.camera=camera
scene.world=bpy.data.worlds.new('Studio'); scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.28,.32,.4,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
for name, direction, power in [('Key',(-1,-1,2),55),('Fill',(1,-.3,1),35),('Rim',(0,1,1),60)]:
    light_data=bpy.data.lights.new(name,'AREA'); light_data.energy=power*max(size)**2; light_data.size=max(size)
    light=bpy.data.objects.new(name,light_data); scene.collection.objects.link(light)
    light.location=center+Vector(direction)*max(size)
    light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES'; scene.cycles.samples=24; scene.cycles.use_denoising=True
scene.render.resolution_x=1200; scene.render.resolution_y=1200; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=True
scene.render.filepath=str(output/(source.stem+'-fallback.png'))
scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(output/(source.stem+'-imported.blend')))
bpy.ops.render.render(write_still=True)
if source.stem == 'guitar':
    # Evidence from the imported deliverable, not from a cached source scene.
    for name, position, target, scale in [
        ('bridge-detail', (.30, -.28, .22), (0, -.055, .060), .21),
        ('head-join-detail', (.5, .67, .050), (0, .67, .050), .11),
    ]:
        camera.location = position
        camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
        camera_data.ortho_scale = scale
        scene.render.filepath = str(output/(source.stem+'-'+name+'.png'))
        bpy.ops.render.render(write_still=True)
print(json.dumps({k:v for k,v in report.items() if k not in ('materials','hierarchy')}), flush=True)
