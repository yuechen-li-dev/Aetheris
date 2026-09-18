"""Bounded X1 weight cleanup study, using Blender native deformation and operators."""
import argparse
import base64
import importlib
import math
from pathlib import Path
import sys
import time
import zlib

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).parent))
h = importlib.import_module('humanoid-rerig-x0')
ROOT = h.ROOT


def normalize(field):
    return [{n: w/sum(row.values()) for n,w in sorted(row.items()) if w>0} for row in field]


def setup():
    candidate = h.read(ROOT/'artifacts/local/humanoid-x1/antonia-adoption-candidate.json')
    surface = candidate['sourcePoseSurface']
    artifact = h.read(ROOT/'fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json')
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    verts = [tuple(v['position'][k]/1000 for k in 'xyz') for v in surface['vertices']]
    faces = [tuple(f[k] for k in ('a','b','c')) for f in surface['faces']]
    original = h.mesh('ANTONIA_CANONICAL',verts,faces,h.collection('ANTONIA_CANONICAL'))
    original.hide_set(True);original.hide_render=True
    rig = h.make_rig(artifact,h.collection('X5_DEFORM_RIG'))
    obj = h.mesh('ANTONIA_GOLDEN',verts,faces,h.collection('GOLDEN_DEFORMATION'))
    mod = obj.modifiers.new('Portable linear blend skinning','ARMATURE');mod.object=rig
    scene=h.setup_camera()
    return surface,rig,obj,np.array(verts),np.array(faces),scene


def automatic_weights(rig,obj):
    saved=[(b.name,b.matrix_local.copy(),b.length) for b in rig.data.bones]
    coords=[v.co.copy() for v in obj.data.vertices]
    h.pose(rig,[])
    obj.vertex_groups.clear()
    for v in obj.data.vertices:v.co*=10
    h.active(rig);bpy.ops.object.mode_set(mode='EDIT')
    for b in rig.data.edit_bones:b.head*=10;b.tail*=10
    bpy.ops.object.mode_set(mode='OBJECT')
    h.active(obj);rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    raw=h.weights(obj)
    for v,co in zip(obj.data.vertices,coords):v.co=co
    h.active(rig);bpy.ops.object.mode_set(mode='EDIT')
    for name,matrix,length in saved:
        rig.data.edit_bones[name].matrix=matrix;rig.data.edit_bones[name].length=length
    bpy.ops.object.mode_set(mode='OBJECT')
    if any(not row for row in raw):raise RuntimeError('Incomplete native heat weights')
    return normalize(raw)


def golden_cleanup(obj,rig,rest,field):
    result=brush_gain(field,rest,rig,'Hip',.22,.85)
    for kind,radius in [('Knee',.11),('Elbow',.10)]:
        centers=[np.array(rig.data.bones[side+kind].head_local) for side in ('Left','Right')]
        mask=np.minimum(*[np.linalg.norm(rest-c,axis=1) for c in centers])<radius
        result=local_smooth(obj,result,mask,1)
    return local_contrast(result,rest,rig,'Shoulder',.18,.5)


def build(out,surface,rig,obj,rest,tris,scene,corpus,no_render=False):
    started=time.monotonic()
    automatic=automatic_weights(rig,obj)
    field=golden_cleanup(obj,rig,rest,automatic)
    h.assign(obj,field)
    # Read back the actual float field Blender evaluates, then normalize portable doubles.
    field=normalize(h.weights(obj));h.assign(obj,field)
    h.write(out/'auto-weights.json',automatic)
    h.write(out/'cleaned-weights.json',field)
    rigpath=ROOT/'fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json'
    import json
    artifact=json.loads(bpy.data.texts['x5-reference.json'].as_string()) if bpy.data.texts.get('x5-reference.json') else h.read(rigpath)
    reference_hash=rig.get('referenceRigSha256') or h.sha(rigpath)
    rig['referenceRigSha256']=reference_hash
    h.write(out/'golden-skeleton.json',dict(canonicalReference=artifact,deformationRigExtensions=[]))
    portable=dict(schema='aetheris.humanoid.experimental-weights.v1',topologyId=surface['topologyId'],
        connectivityHash=surface['connectivityHash'],referenceRigSha256=reference_hash,
        maxInfluences=max(len(w) for w in field),skinning='linear-blend-skinning',deformationRigExtensions=[],
        provenance=dict(blender=bpy.app.version_string,scriptSha256=h.sha(__file__),source='Blender native automatic weights plus recorded local cleanup; no oracle transfer',
            steps=['native bone heat at temporary data scale 10; restore X5 frames',
                   'normalize','bilateral hip soft gain 0.85, radius 220 mm, quartic radial falloff',
                   'native Smooth factor 0.5 once within knee 110 mm and elbow 100 mm spheres',
                   'bilateral shoulder contrast exponent 0.5, radius 180 mm, quartic radial falloff','normalize'],
            manualPaintStrokes=0,topologyEdits=0,jointEdits=0),
        vertices=[dict(vertexId=v['id'],weights=[dict(boneId=n,weight=w) for n,w in sorted(row.items())]) for v,row in zip(surface['vertices'],field)])
    h.write(out/'golden-weights.json',portable)
    h.write(out/'pose-corpus.json',corpus)
    results=[]
    for case in corpus:
        h.pose(rig,case['requested'])
        posed=h.positions(obj)
        transport=h.normal_transport(rig,field,tris)
        m=measure(rest,posed,tris,transport,True)
        m['regions']={region:measure(rest,posed,tris[mask],transport[mask]) for region in sorted({f['region'] for f in surface['faces']})
            for mask in [np.array([f['region']==region for f in surface['faces']])]}
        results.append(dict(pose=case['name'],metrics=m))
        h.write(out/(case['name']+'.positions.json'),(posed*1000).tolist())
        if not no_render:
            h.render(scene,obj,out/('cleaned--'+case['name']+'.png'))
        print('GOLDEN',case['name'],round(m['max'],3),m['reversalCount'],flush=True)
    h.write(out/'metrics.json',results)
    h.pose(rig,[])
    bind_error=float(np.linalg.norm(h.positions(obj)-rest,axis=1).max()*1000)
    topology=[tuple(p.vertices) for p in obj.data.polygons]
    if topology!=[tuple(t) for t in tris] or bind_error>.001:raise RuntimeError('Topology/bind invariant failed')
    h.write(out/'integrity.json',dict(vertexCount=len(rest),faceCount=len(tris),connectivityHash=surface['connectivityHash'],
        exactOrderedTriangleIndices=True,stableVertexIds=[v['id'] for v in surface['vertices']],neutralReconstructionMaximumMm=bind_error,
        maxNormalizationError=max(abs(sum(row.values())-1) for row in field),finiteTransforms=all(math.isfinite(x) for b in rig.pose.bones for row in b.matrix for x in row),
        x5FrameMaximumElementDelta=max(abs(rig.data.bones[j['canonicalJoint']].matrix_local[r][c]-h.matrix(j['globalRestCanonical'])[r][c]) for j in artifact['canonicalJoints'] for r in range(4) for c in range(4))))
    h.write(out/'weight-inspection.json',h.weight_stats(obj,field,[v['region'] for v in surface['vertices']]))
    # These embedded inputs make a fresh replay independent of the repo's implementation/data.
    import json
    for name,value in [('canonical-surface.json.zlib',{k:surface[k] for k in ('vertices','faces','topologyId','connectivityHash')}),('x5-reference.json',artifact),('pose-corpus.json',corpus),('cleanup-history.json',portable['provenance'])]:
        text=bpy.data.texts.get(name) or bpy.data.texts.new(name);text.clear()
        encoded=json.dumps(value,sort_keys=True,indent=2)
        if name.endswith('.zlib'):
            encoded=base64.b64encode(zlib.compress(encoded.encode(),9)).decode()
            encoded='\n'.join(encoded[i:i+76] for i in range(0,len(encoded),76))
        text.write(encoded)
    scene['study']='HUMANOID-RERIG-X1; experimental, no proprietary references; portable LBS'
    h.active(obj)
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'golden.blend'))
    h.write(out/'effort.json',dict(buildSeconds=time.monotonic()-started,manualPaintStrokes=0))


def measure(rest, posed, tris, transported, details=False):
    edges=np.unique(np.sort(np.concatenate([tris[:,[0,1]],tris[:,[1,2]],tris[:,[2,0]]]),axis=1),axis=0)
    ratio=np.linalg.norm(posed[edges[:,0]]-posed[edges[:,1]],axis=1)/np.maximum(np.linalg.norm(rest[edges[:,0]]-rest[edges[:,1]],axis=1),1e-12)
    distortion=np.maximum(ratio,1/np.maximum(ratio,1e-12))
    n0=np.cross(rest[tris[:,1]]-rest[tris[:,0]],rest[tris[:,2]]-rest[tris[:,0]])
    n1=np.cross(posed[tris[:,1]]-posed[tris[:,0]],posed[tris[:,2]]-posed[tris[:,0]])
    expected=np.einsum('nij,nj->ni',transported,n0)
    cos=np.sum(n1*expected,axis=1)/np.maximum(np.linalg.norm(n1,axis=1)*np.linalg.norm(expected,axis=1),1e-20)
    area=np.linalg.norm(n1,axis=1)/np.maximum(np.linalg.norm(n0,axis=1),1e-20)
    result=dict(p50=float(np.quantile(distortion,.5)),p95=float(np.quantile(distortion,.95)),p99=float(np.quantile(distortion,.99)),
        max=float(distortion.max()),minCompression=float(ratio.min()),maxStretch=float(ratio.max()),
        p95Stretch=float(np.quantile(ratio,.95)),p99Stretch=float(np.quantile(ratio,.99)),
        reversalCount=int((cos<0).sum()),degenerateTriangles=int((area<1e-6).sum()),minArea=float(area.min()),maxArea=float(area.max()))
    if details:
        result.update(reversedFaces=np.where(cos<0)[0].tolist(),reversedCosines=cos[cos<0].tolist(),
                      worstEdges=[dict(vertices=edges[i].tolist(),ratio=float(ratio[i])) for i in np.argsort(distortion)[-10:][::-1]])
    return result


def evaluate(rig,obj,field,rest,tris,corpus):
    h.assign(obj,field)
    results=[]
    for case in corpus:
        h.pose(rig,case['requested'])
        m=measure(rest,h.positions(obj),tris,h.normal_transport(rig,field,tris),True)
        results.append(dict(pose=case['name'],metrics=m))
    h.pose(rig,[])
    return results


def local_smooth(obj,field,mask,repeat,factor=.5):
    h.assign(obj,field);h.active(obj)
    for v,selected in zip(obj.data.vertices,mask):v.select=bool(selected)
    obj.data.use_paint_mask_vertex=True
    bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
    bpy.ops.object.vertex_group_smooth(group_select_mode='ALL',factor=factor,repeat=repeat)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.data.use_paint_mask_vertex=False
    return normalize(h.weights(obj))


def brush_gain(field,rest,rig,kind,radius,gain):
    edited=[dict(row) for row in field]
    for side in ('Left','Right'):
        name=side+kind
        center=np.array(rig.data.bones[name].head_local)
        distance=np.linalg.norm(rest-center,axis=1)/radius
        strength=np.maximum(0,1-distance**2)**2
        for i,row in enumerate(edited):
            if name in row:row[name]*=1+(gain-1)*strength[i]
    return normalize(edited)


def hip_brush(field,rest,rig,inner_gain):
    """Two mirrored soft Levels strokes, defined relative to the hip frames in metres."""
    edited=[dict(row) for row in field]
    for side in ('Left','Right'):
        name=side+'Hip';hip=np.array(rig.data.bones[name].head_local)
        for center,radii,gain in [(hip+np.array([0,.09,.05]),np.array([.08,.07,.10]),.80),
                                  (hip*np.array([.55,1,1])+np.array([0,.075,-.07]),np.array([.075,.09,.14]),inner_gain)]:
            q=np.sum(((rest-center)/radii)**2,axis=1)
            strength=np.maximum(0,1-q)**2
            for i,row in enumerate(edited):
                if name in row:row[name]*=1+(gain-1)*strength[i]
    return normalize(edited)


def local_contrast(field,rest,rig,kind,radius,power):
    centers=[np.array(rig.data.bones[side+kind].head_local) for side in ('Left','Right')]
    distance=np.minimum(*[np.linalg.norm(rest-c,axis=1) for c in centers])/radius
    strength=np.maximum(0,1-distance**2)**2
    result=[]
    for row,s in zip(field,strength):
        exponent=1+(power-1)*s
        result.append({n:w**exponent for n,w in sorted(row.items())})
    return normalize(result)


def cleanup_trials(out,surface,rig,obj,rest,tris,field,corpus):
    records={}
    for kind,radius,needle in [('Hip',.22,'hip-'),('Knee',.11,'knee-'),('Elbow',.10,'elbow-'),('Shoulder',.15,'shoulder-')]:
        subset=[c for c in corpus if needle in c['name']]
        centers=[np.array(rig.data.bones[side+kind].head_local) for side in ('Left','Right')]
        mask=np.minimum(*[np.linalg.norm(rest-c,axis=1) for c in centers])<radius
        for repeat in (1,3,6):
            name=f'{kind.lower()}-smooth-{repeat}'
            edited=local_smooth(obj,field,mask,repeat)
            records[name]=evaluate(rig,obj,edited,rest,tris,subset)
            h.write(out/(name+'-weights.json'),edited)
            print(name,[(x['pose'],round(x['metrics']['max'],3),x['metrics']['reversalCount']) for x in records[name]],flush=True)
        for gain in (.65,.85,1.2,1.5):
            name=f'{kind.lower()}-gain-{gain}'
            edited=brush_gain(field,rest,rig,kind,radius,gain)
            records[name]=evaluate(rig,obj,edited,rest,tris,subset)
            h.write(out/(name+'-weights.json'),edited)
            print(name,[(x['pose'],round(x['metrics']['max'],3),x['metrics']['reversalCount']) for x in records[name]],flush=True)
        h.write(out/'cleanup-trials.json',records)


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--out',default=str(ROOT/'artifacts/local/humanoid-rerig-x1'))
    p.add_argument('--phase',choices=['diagnose','cleanup','hip','contrast','build'],default='diagnose')
    p.add_argument('--scene')
    p.add_argument('--no-render',action='store_true')
    args=p.parse_args(sys.argv[sys.argv.index('--')+1:])
    out=Path(args.out).resolve();out.mkdir(parents=True,exist_ok=True)
    if not out.is_relative_to(ROOT/'artifacts/local'):raise ValueError('Local ignored output required')
    if args.scene:
        import json
        bpy.ops.wm.open_mainfile(filepath=str(Path(args.scene).resolve()),use_scripts=False)
        surface=json.loads(zlib.decompress(base64.b64decode(bpy.data.texts['canonical-surface.json.zlib'].as_string())))
        corpus=json.loads(bpy.data.texts['pose-corpus.json'].as_string())
        rig=bpy.data.objects['X5_DEFORM'];obj=bpy.data.objects['ANTONIA_GOLDEN'];scene=bpy.context.scene
        rest=np.array([tuple(v['position'][k]/1000 for k in 'xyz') for v in surface['vertices']])
        tris=np.array([tuple(f[k] for k in ('a','b','c')) for f in surface['faces']])
    else:
        surface,rig,obj,rest,tris,scene=setup()
        corpus=h.read(ROOT/'artifacts/local/humanoid-rerig-x0/pose-corpus.json')
    if args.phase=='build':
        build(out,surface,rig,obj,rest,tris,scene,corpus,args.no_render)
        return
    field=normalize(h.read(ROOT/'artifacts/local/humanoid-rerig-x0/pass-a-weights.json'))
    if args.phase=='cleanup':
        cleanup_trials(out,surface,rig,obj,rest,tris,field,corpus)
        return
    if args.phase=='hip':
        sweep={}
        for gain in (.5,1,1.5,2,3):
            name=f'hip-local-{gain}'
            edited=hip_brush(field,rest,rig,gain)
            sweep[name]=evaluate(rig,obj,edited,rest,tris,[c for c in corpus if 'hip-' in c['name']])
            h.write(out/(name+'-weights.json'),edited)
            print(name,[(x['pose'],round(x['metrics']['max'],3),x['metrics']['reversalCount']) for x in sweep[name]],flush=True)
        h.write(out/'hip-local-trials.json',sweep)
        return
    if args.phase=='contrast':
        sweep={}
        for kind,radius,needle in [('Hip',.28,'hip-'),('Knee',.14,'knee-'),('Elbow',.12,'elbow-'),('Shoulder',.18,'shoulder-')]:
            for power in (.5,.75,1.5,2):
                name=f'{kind.lower()}-contrast-{power}'
                edited=local_contrast(field,rest,rig,kind,radius,power)
                sweep[name]=evaluate(rig,obj,edited,rest,tris,[c for c in corpus if needle in c['name']])
                h.write(out/(name+'-weights.json'),edited)
                print(name,[(x['pose'],round(x['metrics']['max'],3),x['metrics']['reversalCount']) for x in sweep[name]],flush=True)
        h.write(out/'contrast-trials.json',sweep)
        return
    baseline=evaluate(rig,obj,field,rest,tris,corpus)
    h.write(out/'baseline-metrics.json',baseline)
    hip70=next(c for c in baseline if c['pose']=='hip-flexion-70')['metrics']
    findings=[]
    for fi,cosine in zip(hip70['reversedFaces'],hip70['reversedCosines']):
        indices=tris[fi].tolist()
        adjacent=sorted(set(tris[np.any(np.isin(tris,indices),axis=1)].ravel().tolist()))
        findings.append(dict(faceIndex=fi,face=surface['faces'][fi],cosine=cosine,
            vertices=[dict(index=i,record=surface['vertices'][i],weights=field[i]) for i in indices],
            adjacentVertices=[dict(index=i,position=rest[i].tolist(),weights=field[i]) for i in adjacent]))
    h.write(out/'hip70-reversal-diagnosis.json',findings)
    sweep={'normalized-auto':baseline}
    for limit in (2,4,8):
        h.assign(obj,field);h.active(obj)
        bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=limit)
        bpy.ops.object.vertex_group_normalize_all(lock_active=False)
        limited=normalize(h.weights(obj))
        h.write(out/f'limit-{limit}-weights.json',limited)
        sweep[f'limit-{limit}']=evaluate(rig,obj,limited,rest,tris,corpus)
        print('LIMIT',limit,[(x['pose'],round(x['metrics']['max'],3),x['metrics']['reversalCount']) for x in sweep[f'limit-{limit}']],flush=True)
    h.write(out/'influence-sweep.json',sweep)


if __name__=='__main__':main()
