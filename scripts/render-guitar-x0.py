"""Render the actual Aetheris USD in Cycles. Product topology is never replaced.

blender --background --factory-startup --python scripts/render-guitar-x0.py --
  artifacts/local/guitar-x0/guitar.usda artifacts/local/guitar-x0 [--preview]
Sunburst/wood/lacquer are downstream shading, saved in the portable .blend scene.
"""
import bpy
import hashlib
import json
import math
import sys
import time
from pathlib import Path
from mathutils import Vector, Matrix

args=sys.argv[sys.argv.index('--')+1:]
source,root=map(lambda p:Path(p).resolve(),args[:2])
preview='--preview' in args
selected_view=next((arg.split('=',1)[1] for arg in args if arg.startswith('--view=')),None)
root.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
started=time.perf_counter()
bpy.ops.wm.usd_import(filepath=str(source),support_scene_instancing=False,
                      import_materials=True,import_usd_preview=True,apply_unit_conversion_scale=True)
load_seconds=time.perf_counter()-started
scene=bpy.context.scene
products=[o for o in scene.objects if o.type=='MESH']
assert len(products) in (91,99,107), len(products)

def material(name,color,metal=0,rough=.25,coat=.35):
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal
    p.inputs['Roughness'].default_value=rough
    p.inputs['Coat Weight'].default_value=coat
    p.inputs['Coat Roughness'].default_value=.12
    return m

cream=material('Warm ivory binding',(.72,.59,.36),rough=.23)
chrome=material('Polished nickel',(.65,.69,.73),1,.16)
black=material('Black pickup bobbins',(.009,.007,.006),rough=.21)
ebony=material('Black headstock veneer',(.008,.01,.013),rough=.18)
pearl=material('Pearl markers',(.74,.73,.62),.2,.22)
amber=material('Amber control knobs',(.38,.13,.013),.25,.21)
gold=material('Brass knob centers',(.48,.27,.06),.7,.24)
wood=material('Cherry mahogany',(.10,.017,.007),rough=.24)
rose=material('Dark rosewood',(.035,.012,.006),rough=.36,coat=.12)

def grain(m,base,scale,strength):
    n=m.node_tree.nodes;l=m.node_tree.links;p=n.get('Principled BSDF')
    coord=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY'
    mapping.inputs[1].default_value=scale;l.new(coord.outputs['Generated'],mapping.inputs[0])
    noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=28;noise.inputs['Detail'].default_value=3
    l.new(mapping.outputs[0],noise.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color=(*(v*.48 for v in base),1)
    ramp.color_ramp.elements[1].color=(*base,1)
    l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Base Color'])
    bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=strength;bump.inputs['Distance'].default_value=.00005
    l.new(noise.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs[0],p.inputs['Normal'])
grain(wood,(.16,.032,.012),(10,1,2),.10)
grain(rose,(.065,.026,.012),(12,1,1),.12)

burst=material('Cherry sunburst lacquer',(1,.5,.05),rough=.24,coat=.45)
n=burst.node_tree.nodes;l=burst.node_tree.links;p=n.get('Principled BSDF')
attribute=n.new('ShaderNodeAttribute');attribute.attribute_name='edge_distance'
ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.interpolation='EASE'
colors=[(0,(.009,.0015,.0008,1)),(.16,(.11,.005,.002,1)),(.38,(.40,.035,.003,1)),(.65,(.70,.22,.013,1)),(1,(.85,.48,.065,1))]
for i,(pos,col) in enumerate(colors):
    e=ramp.color_ramp.elements[i] if i<2 else ramp.color_ramp.elements.new(pos)
    e.position=pos;e.color=col
l.new(attribute.outputs['Fac'],ramp.inputs[0])
coord=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=(3,65,2)
l.new(coord.outputs['Generated'],mapping.inputs[0])
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=4;noise.inputs['Detail'].default_value=2;noise.inputs['Roughness'].default_value=.6
l.new(mapping.outputs[0],noise.inputs['Vector'])
mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.24
l.new(ramp.outputs[0],mix.inputs[1]);l.new(noise.outputs['Fac'],mix.inputs[2]);l.new(mix.outputs[0],p.inputs['Base Color'])

def signature():
    h=hashlib.sha256()
    for o in products:
        h.update(o.name.encode());h.update(str(len(o.data.vertices)).encode())
        for v in o.data.vertices:h.update(bytes(str(tuple(v.co)),'ascii'))
        for poly in o.data.polygons:h.update(bytes(str(tuple(poly.vertices)),'ascii'))
    return h.hexdigest()
original_signature=signature()

pickup_shaded=0
pearl_shaded=0
def semantic_appearance(obj):
    while obj is not None:
        if 'aetheris_appearance' in obj:
            return obj['aetheris_appearance']
        obj = obj.parent
    raise RuntimeError('USD product has no authored appearance metadata')

looks={'PolishedSteel':chrome,'PolishedNickel':chrome,'WarmIvory':cream,
       'BlackPolymer':black,'EbonyLacquer':ebony,'Pearl':pearl,'Amber':amber,
       'Brass':gold,'CherryMahogany':wood,'Rosewood':rose,'GoldenMaple':wood}

for obj in products:
    look=semantic_appearance(obj)
    if look=='Sunburst':
        m=burst
        import numpy as np
        pts=np.array([tuple(obj.matrix_world @ v.co) for v in obj.data.vertices])
        edge=pts[np.isclose(pts[:,2],pts[:,2].min(),atol=1e-6),:2]
        assert len(edge)>20
        attr=obj.data.attributes.new('edge_distance','FLOAT','POINT')
        # Color only: distance to the actual exported lower perimeter, in metres.
        for start in range(0,len(pts),256):
            distances=np.sqrt(((pts[start:start+256,None,:2]-edge[None,:,:])**2).sum(axis=2)).min(axis=1)
            for i,d in enumerate(distances,start):attr.data[i].value=min(1,float(d)/.065)
    elif look=='PickupComposite':
        # Appearance only: exact feature-built pickup geometry is imported intact.
        # Default witness axial levels are base 0..4, coils 4..9, poles 9..10 mm.
        obj.data.materials.clear()
        for look in (cream,black,chrome):obj.data.materials.append(look)
        zs=[v.co.z for v in obj.data.vertices];low,high=min(zs),max(zs)
        for poly in obj.data.polygons:
            level=(sum(obj.data.vertices[i].co.z for i in poly.vertices)/len(poly.vertices)-low)/(high-low)
            poly.material_index=2 if level>.900001 else 1 if level>.400001 else 0
        pickup_shaded+=1
        continue
    else:
        m=looks[look]
        if look=='Pearl':pearl_shaded+=1
    obj.data.materials.clear();obj.data.materials.append(m)

assert signature()==original_signature, 'Shading must not alter imported product topology'
assert len(products)==107 or pickup_shaded==2, f'Expected two feature-built pickup appearance assignments; found {pickup_shaded}'
assert len(products)==107 or pearl_shaded==9, f'Expected nine pearl inlay appearance assignments; found {pearl_shaded}'

floor=material('Studio charcoal',(.014,.018,.024),.12,.32,0)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,.3,-.004))
stage=bpy.context.object;stage.name='Presentation_StudioFloor';stage.data.materials.append(floor)

def aim(obj,target):
    forward=(Vector(target)-obj.location).normalized()
    right=forward.cross(Vector((0,1,0))).normalized()
    up=right.cross(forward)
    obj.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler()
def area(name,position,target,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='RECTANGLE';d.size=size;d.size_y=size*.3;d.color=color
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=position;aim(o,target)
    return o
area('Key softbox',(-.5,.2,1.0),(0,.25,0),30,.9,(1,.82,.63))
area('Long lacquer strip',(.5,.45,.75),(0,.25,.03),24,1.2,(.65,.79,1))
rear=area('Rear neck softbox',(-.35,.55,-.55),(0,.5,.04),22,.65,(1,.82,.63))
area('Crown rim',(-.3,1.0,.45),(0,.5,.03),18,.7,(1,.4,.18))
scene.world=bpy.data.worlds.new('Dark studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.10,.12,.16,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.35

d=bpy.data.cameras.new('HeroCamera');camera=bpy.data.objects.new('HeroCamera',d);scene.collection.objects.link(camera);scene.camera=camera
d.type='ORTHO';d.lens=55;d.clip_start=.001
scene.render.engine='CYCLES';scene.cycles.samples=24 if preview else 64;scene.cycles.use_denoising=True
scene.cycles.max_bounces=6
scene.cycles.glossy_bounces=4
scene.cycles.use_adaptive_sampling=True
scene.cycles.adaptive_threshold=.03
prefs=bpy.context.preferences.addons['cycles'].preferences
try:
    prefs.compute_device_type='OPTIX';prefs.get_devices()
    for device in prefs.devices:device.use=device.type=='OPTIX'
    scene.cycles.device='GPU' if any(d.use for d in prefs.devices) else 'CPU'
except Exception:scene.cycles.device='CPU'
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
scene.view_settings.exposure=-.8
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.render.resolution_percentage=100

views=[('hero',(.73,-.18,1.6),(0,.30,.04),1.23,1800,2200),
       ('deck-side',(.65,.015,.060),(0,.015,.053),.27,1800,1800),
       ('carve-detail',(.45,-.48,.62),(0,-.005,.047),.60,2200,1600),
       ('shaded-isometric',(.9,-.5,1.05),(0,.30,.04),1.28,1800,2200),
       ('neck-side',(.65,.48,.045),(0,.48,.045),.66,1600,2200),
       ('neck-back',(.25,.49,-.7),(0,.49,.035),.70,1600,2200),
       ('head-detail',(.15,.69,.34),(0,.722,.039),.21,1800,2000),
       ('head-join-side',(.5,.67,.050),(0,.67,.050),.11,1600,1800),
       ('head-back',(.12,.73,-.5),(0,.71,.035),.20,1800,2000)]
if selected_view:
    views=[v for v in views if v[0]==selected_view]
    assert views, f'Unknown view: {selected_view}'
renders=[]
print('GUITAR_SCENE_READY',scene.cycles.device, 'samples',scene.cycles.samples,flush=True)
for name,location,target,scale,w,h in sorted(views,key=lambda v:not v[0].startswith('neck-')):
    stage.hide_render=name.startswith(('neck-','head-'))
    rear.hide_render=not name.startswith(('neck-','head-'))
    camera.location=location;aim(camera,target);d.ortho_scale=scale
    scene.render.resolution_x=w//2 if preview else w;scene.render.resolution_y=h//2 if preview else h
    if name=='shaded-isometric':
        scene.cycles.samples=24 if preview else 64
    scene.render.filepath=str(root/(name+('-preview' if preview else '')+'.png'))
    if name=='hero':bpy.ops.wm.save_as_mainfile(filepath=str(root/'guitar-studio.blend'))
    print('GUITAR_RENDER_START',name,flush=True)
    t=time.perf_counter();bpy.ops.render.render(write_still=True)
    renders.append(dict(view=name,seconds=time.perf_counter()-t,image=scene.render.filepath,
                        resolution=[scene.render.resolution_x,scene.render.resolution_y],samples=scene.cycles.samples))
stage.hide_render=False
rear.hide_render=True
# Save the hero camera as the default for reopening the complete offline scene.
camera.location=views[0][1];aim(camera,views[0][2]);d.ortho_scale=views[0][3]
scene.render.resolution_x=1800;scene.render.resolution_y=2200
bpy.ops.wm.save_as_mainfile(filepath=str(root/'guitar-studio.blend'))
(root/('render-preview.json' if preview else 'render.json')).write_text(json.dumps(dict(
    blender=bpy.app.version_string,engine='Cycles',device=scene.cycles.device,loadSeconds=load_seconds,
    usdSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
    devices=[device.name for device in prefs.devices if device.use],
    productMeshObjects=len(products),productTopologyHash=original_signature,
    productTopologyUnchanged=signature()==original_signature,pickupAppearance='Default feature axial height bands; shading only',pickupObjectsStyled=pickup_shaded,pearlObjectsStyled=pearl_shaded,renders=renders),indent=2)+'\n')
print('GUITAR_RENDER_COMPLETE',json.dumps(renders))
