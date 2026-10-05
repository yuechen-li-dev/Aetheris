"""Independent Blender GLB import, physical-size/instancing checks and authored-camera preview.
blender --background --python-exit-code 1 --python scripts/render-scene-x0.py -- warehouse.glb output-dir
Only presentation lights are added; imported scene geometry is unchanged.
"""
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector

source, output = [Path(p).resolve() for p in sys.argv[sys.argv.index('--') + 1:][:2]]
output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
scene = bpy.context.scene
products = [o for o in scene.objects if o.type == 'MESH']
cameras = [o for o in scene.objects if o.type == 'CAMERA']
assert products and len(cameras) == 1
points = [o.matrix_world @ Vector(c) for o in products for c in o.bound_box]
lo = [min(p[i] for p in points) for i in range(3)]
hi = [max(p[i] for p in points) for i in range(3)]
assert all(math.isfinite(v) for v in lo + hi)
assert abs((hi[0]-lo[0])-6.2) < .001
assert abs((hi[1]-lo[1])-4.2) < .001
assert abs((hi[2]-lo[2])-3.2) < .001
assert len({o.data.as_pointer() for o in products}) < len(products)
for obj in products:
    assert obj.matrix_world.determinant() > 0
    assert obj.data.polygons
report = dict(tool='Blender', version=bpy.app.version_string, meshes=len(products),
              sharedMeshData=len({o.data.as_pointer() for o in products}),
              boundsMetres=dict(min=lo, max=hi), camera=cameras[0].name)
(output/'blender-validation.json').write_text(json.dumps(report, indent=2)+'\n')
scene.camera = cameras[0]
scene.camera.data.clip_start = .01
scene.camera.data.clip_end = 100
scene.world = bpy.data.worlds.new('Warehouse presentation world')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .2
for i, point in enumerate([(1.5,1.5,2.8),(4,2.5,2.8)]):
    data = bpy.data.lights.new('Presentation area '+str(i), 'AREA')
    data.energy = 600
    data.size = 2
    light = bpy.data.objects.new(data.name, data)
    scene.collection.objects.link(light)
    light.location = point
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.cycles.use_denoising = True
scene.render.resolution_x = 1440
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(output/'warehouse-fallback.png')
scene.view_settings.view_transform = 'AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(output/'warehouse-imported.blend'))
bpy.ops.render.render(write_still=True)
print(json.dumps(report))
