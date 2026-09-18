"""Pose a saved X0 scene without Aetheris runtime or source assets.

blender --background --disable-autoexec --python-exit-code 1
  --python scripts/replay-humanoid-rerig-x0.py -- --scene <experimental.blend>
  --corpus <pose-corpus.json> --out <directory> --poses hip-flexion-70 knee-flexion-90 shoulder-abduction-90
"""
import argparse
import importlib
import sys
from pathlib import Path
import bpy

sys.path.insert(0,str(Path(__file__).parent))
h=importlib.import_module('humanoid-rerig-x0')
p=argparse.ArgumentParser()
p.add_argument('--scene',required=True)
p.add_argument('--corpus',required=True)
p.add_argument('--out',required=True)
p.add_argument('--poses',nargs='+',default=['hip-flexion-70','knee-flexion-90','shoulder-abduction-90'])
p.add_argument('--pipeline',choices=['pass-a','pass-b','pass-c-local','pass-c-volume','pass-c-corrective','mixamo','x5'],default='pass-a')
p.add_argument('--view',choices=['iso','front','side','rear'],default='iso')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
bpy.ops.wm.open_mainfile(filepath=str(Path(args.scene).resolve()),use_scripts=False)
out=Path(args.out).resolve();out.mkdir(parents=True,exist_ok=True)
root=Path(args.scene).resolve().parent
corpus={c['name']:c for c in h.read(args.corpus)}
rig=bpy.data.objects['X5_DEFORM']
obj=bpy.data.objects['Blender experiment']
mapping=None
if args.pipeline=='mixamo':
    coll=bpy.data.collections['MIXAMO_REFERENCE_LOCAL']
    rig=next(o for o in coll.objects if o.type=='ARMATURE')
    obj=next(o for o in coll.objects if o.type=='MESH')
    mapping={j['joint']:j['mixamo'] for j in h.read(root/'mixamo-local-mapping.json')['majorJoints']}
elif args.pipeline=='x5':
    obj=bpy.data.objects['X5 LBS comparison']
elif args.pipeline=='pass-b':
    h.assign(obj,h.read(root/'pass-b-weights.json'))
elif args.pipeline=='pass-c-local':
    h.assign(obj,h.read(root/'pass-c-local-weights.json'))
elif args.pipeline.startswith('pass-c'):
    field=h.read(root/'pass-a-weights.json')
    field=[{n:w/sum(v.values()) for n,w in v.items()} for v in field]
    h.assign(obj,field)
    next(m for m in obj.modifiers if m.type=='ARMATURE').use_deform_preserve_volume=True
    correction=obj.modifiers.get('Native local Corrective Smooth')
    if args.pipeline=='pass-c-corrective':
        group=obj.vertex_groups.new(name='LOCAL_TRANSITIONS')
        group.add(h.read(root/'corrective-mask.json'),1,'REPLACE')
        correction.vertex_group=group.name
        correction.show_viewport=correction.show_render=True
obj.hide_set(False);rig.hide_set(False)
scene=bpy.context.scene
if args.view!='iso':
    from mathutils import Vector
    scene.camera.location={'front':(0,5,1.12),'side':(-5,0,1.12),'rear':(0,-5,1.12)}[args.view]
    scene.camera.rotation_euler=(Vector((0,0,1.12))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
records=[]
for name in args.poses:
    h.pose(rig,corpus[name]['requested'],mapping)
    h.render(scene,obj,out/(args.pipeline+'--'+name+'.png'))
    values=h.positions(obj)
    import hashlib
    centers=[];lengths=[]
    for bone in rig.pose.bones:
        if not bone.parent:continue
        local=bone.parent.bone.matrix_local.inverted() @ bone.bone.matrix_local
        expected=rig.matrix_world @ bone.parent.matrix @ local.translation
        actual=rig.matrix_world @ bone.matrix.translation
        centers.append((actual-expected).length*1000)
        current=(actual-rig.matrix_world @ bone.parent.matrix.translation).length
        rest=(rig.matrix_world @ bone.bone.head_local-rig.matrix_world @ bone.parent.bone.head_local).length
        lengths.append(abs(current-rest)*1000)
    records.append(dict(pose=name,positionSha256=hashlib.sha256(values.tobytes()).hexdigest(),
        maximumCenterResidualMm=max(centers),maximumLinkResidualMm=max(lengths)))
h.write(out/'replay.json',dict(blender=bpy.app.version_string,pipeline=args.pipeline,poses=records))
