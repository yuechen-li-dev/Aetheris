"""Bounded finishing of the existing qualified ATLAS/guitar Blender scenes.

No new product geometry. Bake only existing procedural base color; export static
GLB with embedded images. The .NET exporter remains the reusable display lowering.
blender --background --python scripts/export-presentation-scene.py --
  guitar artifacts/local/guitar-x0/guitar-studio.blend artifacts/local/presentation-3d-x0/guitar.glb
"""
import bpy
import hashlib
import json
import sys
import time
from pathlib import Path

kind, source, destination = sys.argv[sys.argv.index('--') + 1:][:3]
assert kind in ('atlas', 'guitar')
source, destination = Path(source).resolve(), Path(destination).resolve()
destination.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
scene = bpy.context.scene
started = time.perf_counter()
disclaimer = ('ATLAS is an independent Aetheris demo robot. Aetheris is not affiliated with, '
              'endorsed by, or associated with Boston Dynamics. The shared name is coincidental.')

def signature():
    digest = hashlib.sha256()
    for mesh in sorted(bpy.data.meshes, key=lambda m: m.name):
        digest.update(mesh.name.encode())
        for v in mesh.vertices:
            digest.update(str(tuple(v.co)).encode())
        for p in mesh.polygons:
            digest.update(str(tuple(p.vertices)).encode())
    return digest.hexdigest()

original = signature()
products = [o for o in scene.objects if o.type == 'MESH' and not o.name.startswith('Presentation_')]
def object_geometry(obj):
    return (tuple(tuple(v.co) for v in obj.data.vertices),
            tuple(tuple(p.vertices) for p in obj.data.polygons))
product_geometry = {o.name: object_geometry(o) for o in products}
if kind == 'guitar':
    assert len(products) in (99, 107), len(products)
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 1
    scene.cycles.device = 'CPU'
    # Baking color via emission preserves metallic base color too. Lighting,
    # roughness, coat and normals are not baked into the base-color image.
    baked = []
    for obj in products:
        material = obj.data.materials[0]
        shader = material.node_tree.nodes.get('Principled BSDF')
        if not shader.inputs['Base Color'].is_linked:
            continue
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True); bpy.context.view_layer.objects.active = obj
        obj.data = obj.data.copy()
        material = material.copy(); obj.data.materials[0] = material
        nodes, links = material.node_tree.nodes, material.node_tree.links
        shader = nodes.get('Principled BSDF')
        color = shader.inputs['Base Color'].links[0].from_socket
        # A UV seam may split exported vertices, but positions/triangles never change.
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(island_margin=.02)
        bpy.ops.object.mode_set(mode='OBJECT')
        image = bpy.data.images.new('Aetheris baked ' + obj.name, width=1024, height=1024)
        image.colorspace_settings.name = 'sRGB'
        target = nodes.new('ShaderNodeTexImage'); target.image = image; nodes.active = target
        emission = nodes.new('ShaderNodeEmission'); links.new(color, emission.inputs['Color'])
        output = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output)
        links.new(emission.outputs[0], output.inputs['Surface'])
        scene.render.bake.margin = 12
        bpy.ops.object.bake(type='EMIT')
        links.new(shader.outputs[0], output.inputs['Surface']); nodes.remove(emission)
        links.new(target.outputs['Color'], shader.inputs['Base Color'])
        image.pack()
        # glTF cannot carry the procedural grain bump. Leave the real shader in
        # the source scene and export base color + PBR factors explicitly.
        for link in list(shader.inputs['Normal'].links): links.remove(link)
        baked.append(obj.name)
else:
    baked = []

# Export only the product assembly tree, including its existing printed label.
# USD's root supplies the mm-to-metres conversion, retained by Blender/glTF.
bpy.ops.object.select_all(action='DESELECT')
def belongs_to_product(obj):
    current = obj
    while current:
        if current.name == 'Assembly': return True
        current = current.parent
    return False
selected = [o for o in scene.objects if belongs_to_product(o)]
assert selected, 'Missing qualified USD Assembly root'
for obj in selected:
    obj.select_set(True)
    obj['sourceProject'] = 'Aetheris'
    obj['milestone'] = 'PRESENTATION-3D-X0'
    obj['authority'] = 'BRep/STEP; GLB is a presentation asset'
root = next(o for o in selected if o.name == 'Assembly')
if kind == 'atlas': root['disclaimer'] = disclaimer
root['creator'] = 'Aetheris with OpenAI Codex assistance'
assert all(object_geometry(o) == product_geometry[o.name] for o in products), 'Baking changed product geometry'
if kind == 'atlas': assert signature() == original, 'Presentation export changed topology'
bpy.ops.export_scene.gltf(filepath=str(destination), export_format='GLB', use_selection=True,
                          export_animations=False, export_skins=False, export_morph=False,
                          export_extras=True, export_normals=True, export_materials='EXPORT',
                          export_yup=True, export_image_format='AUTO', export_attributes=False,
                          export_vertex_color='NONE')
report = dict(model=kind, tool='Blender', version=bpy.app.version_string,
              source=source.name, output=destination.name, bytes=destination.stat().st_size,
              exportSeconds=time.perf_counter()-started, bakedObjects=baked,
              selectedObjects=len(selected), originalGeometrySignature=original,
              limitations=['Procedural grain bump omitted; base color baked and scalar PBR/coat retained.'] if kind == 'guitar' else [])
if kind == 'atlas': report['disclaimer'] = disclaimer
destination.with_suffix('.export.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report), flush=True)
