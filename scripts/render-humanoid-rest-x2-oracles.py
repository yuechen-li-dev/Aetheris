"""Render evaluated Aetheris/Genesis OBJ columns with the REST-X2 Blender camera."""
import argparse
import importlib
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).parent))
h=importlib.import_module('humanoid-rerig-x0')
x2=importlib.import_module('humanoid-rest-x2')

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',required=True);p.add_argument('--aetheris',required=True);p.add_argument('--genesis',required=True)
    a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.out)
    if not out.is_absolute():out=h.ROOT/out
    scene=h.setup_camera()
    def absolute(value):
        path=Path(value);return path if path.is_absolute() else h.ROOT/path
    def load_obj(path,source):
        vertices=[];faces=[]
        for line in path.read_text(encoding='utf-8-sig').splitlines():
            if line.startswith('v '):
                x,y,z=map(float,line.split()[1:4]);vertices.append((-x,z,y) if source=='genesis' else (x,y,z))
            elif line.startswith('f '):faces.append(tuple(int(v.split('/')[0])-1 for v in line.split()[1:]))
        p=np.asarray(vertices);low=p.min(0);high=p.max(0);scale=1.75/max(high[2]-low[2],1e-9);p=(p-(low+high)/2)*scale;p[:,2]-=p[:,2].min()
        return h.mesh(source+' '+path.stem,p,faces,h.collection(source+' display'))
    for source,directory in [('aetheris',absolute(a.aetheris)),('genesis',absolute(a.genesis))]:
        for path in sorted(directory.glob('*.obj')):
            if path.stem not in {x['name'] for x in x2.canonical_corpus()}:continue
            obj=load_obj(path,source);h.render(scene,obj,out/f'{source}--{path.stem}.png')
            bpy.data.objects.remove(obj,do_unlink=True)

if __name__=='__main__':main()
