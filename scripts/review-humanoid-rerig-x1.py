"""Local matched-camera overview and close-up review; no reference data enters golden.blend."""
import argparse
import base64
import importlib
import json
from pathlib import Path
import sys
import zlib
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).parent))
h=importlib.import_module('humanoid-rerig-x0')
p=argparse.ArgumentParser()
p.add_argument('--out',default=str(h.ROOT/'artifacts/local/humanoid-rerig-x1'))
p.add_argument('--genesis',action='store_true')
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.out).resolve()

def read_obj(path):
    v=[];f=[]
    for line in path.read_text().splitlines():
        parts=line.split()
        if parts and parts[0]=='v':v.append([float(c) for c in parts[1:4]])
        if parts and parts[0]=='f':f.append([int(c.split('/')[0])-1 for c in parts[1:]])
    return np.array(v),f

if a.genesis:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    scene=h.setup_camera()
    root=out/'daz-reference';neutral,_=read_obj(root/'neutral.obj')
    converted=neutral[:,[0,2,1]]*np.array([-1,1,1])
    scale=1.75/np.ptp(converted[:,2]);offset=-np.array([0,0,converted[:,2].min()])*scale
    records=[]
    for name in ['neutral','hip70','hip90','abduction45','knee90','shoulder60','shoulder90','elbow90']:
        v,f=read_obj(root/(name+'.obj'));v=v[:,[0,2,1]]*np.array([-1,1,1])*scale+offset
        obj=h.mesh('Genesis 9 local rendered reference',v,f,bpy.context.scene.collection)
        h.render(scene,obj,root/(name+'.png'));bpy.data.objects.remove(obj,do_unlink=True)
        records.append(dict(pose=name,vertices=len(v),faces=len(f)))
    h.write(root/'render-notes.json',dict(scale=scale,axis='(-X,+Z,+Y), height matched in neutral only',nativeDeformation='DAZ evaluated OBJ; no weight/morph transfer',cases=records))
else:
    bpy.ops.wm.open_mainfile(filepath=str(out/'golden.blend'),use_scripts=False)
    rig=bpy.data.objects['X5_DEFORM'];obj=bpy.data.objects['ANTONIA_GOLDEN'];scene=bpy.context.scene
    corpus={c['name']:c for c in json.loads(bpy.data.texts['pose-corpus.json'].as_string())}
    metrics={c['pose']:c['metrics'] for c in h.read(out/'metrics.json')}
    surface=json.loads(zlib.decompress(base64.b64decode(bpy.data.texts['canonical-surface.json.zlib'].as_string())))
    cases=[('hip-flexion-70','LeftHip',.60),('hip-flexion-90','LeftHip',.60),('hip-abduction-45','LeftHip',.70),
           ('knee-flexion-90','LeftKnee',.40),('elbow-flexion-90','LeftElbow',.36),('elbow-flexion-120','LeftElbow',.36),
           ('shoulder-abduction-60','LeftShoulder',.48),('shoulder-abduction-90','LeftShoulder',.48)]
    for name,joint,size in cases:
        h.pose(rig,corpus[name]['requested']);target=rig.pose.bones[joint].matrix.translation
        scene.camera.data.ortho_scale=size
        for view,direction in [('front',(0,5,0)),('side',(-5,0,0)),('rear',(0,-5,0))]:
            scene.camera.location=target+Vector(direction)
            scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
            h.render(scene,obj,out/('detail--'+name+'--'+view+'.png'))
    h.pose(rig,[])
