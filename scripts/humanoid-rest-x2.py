"""HUMANOID-REST-X2 local benchmark.

Runs the open A-pose weight ablation and, when supplied, a local Mixamo oracle.
All generated data stays under ignored artifacts/local; proprietary meshes/weights are
never embedded in the promotable .blend or portable weight artifact.
"""
import argparse
import copy
import importlib
import json
import math
from pathlib import Path
import sys
import time

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, str(Path(__file__).parent))
h = importlib.import_module('humanoid-rerig-x0')
x1 = importlib.import_module('humanoid-rerig-x1')
ROOT = h.ROOT


def normalize(field):
    return [{n: value / sum(row.values()) for n, value in sorted(row.items()) if value > 1e-8}
            for row in field]


def portable_field(path):
    value = h.read(path)
    if isinstance(value, list):
        return normalize(value)
    return normalize([{w['boneId']: w['weight'] for w in vertex['weights']} for vertex in value['vertices']])


def normalized_inputs():
    candidate = h.read(ROOT/'artifacts/local/humanoid-x1/antonia-adoption-candidate.json')
    reference = h.read(ROOT/'fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json')
    evidence = h.read(ROOT/'artifacts/local/humanoid-rest-x1/evidence.json')
    by_kind = {j['kind']: j for j in evidence['normalizedJoints']}
    artifact = copy.deepcopy(reference)
    artifact['restPoseId'] = evidence['identities']['restPoseId']
    for joint in artifact['canonicalJoints']:
        joint['globalRestCanonical'] = by_kind[joint['canonicalJoint']]['globalBind']
    surface = candidate['sourcePoseSurface']
    vertices = []
    for line in (ROOT/'artifacts/local/humanoid-rest-x1/canonical-apose.obj').read_text().splitlines():
        if line.startswith('v '):
            vertices.append(tuple(float(v)/1000 for v in line.split()[1:4]))
    if len(vertices) != len(surface['vertices']):
        raise RuntimeError('REST-X1 canonical A-pose vertex evidence does not match Antonia topology.')
    return candidate, surface, artifact, evidence, vertices


def canonical_corpus():
    r = lambda joint, flex=0, abd=0, twist=0: dict(joint=joint, flexionDegrees=flex,
        abductionDegrees=abd, twistDegrees=twist)
    return [
        dict(name='canonical-apose', requested=[]),
        *[dict(name=f'shoulder{a}', requested=[r('LeftShoulder', abd=a)]) for a in (60, 90, 120)],
        *[dict(name=f'elbow{a}', requested=[r('LeftShoulder', abd=35), r('LeftElbow', flex=a)]) for a in (45, 90, 120)],
        *[dict(name=f'hip{a}', requested=[r('LeftHip', flex=a)]) for a in (45, 70, 90)],
        *[dict(name=f'abduction{a}', requested=[r('LeftHip', abd=a)]) for a in (30, 45)],
        *[dict(name=f'knee{a}', requested=[r('LeftKnee', flex=a)]) for a in (45, 90)],
        dict(name='walking', requested=[r('LeftHip', 30), r('LeftKnee', 35), r('RightShoulder', abd=20)]),
        dict(name='balance-kick', requested=[r('LeftHip', 70), r('LeftKnee', 35), r('RightShoulder', abd=55)]),
        dict(name='reach', requested=[r('LeftShoulder', 35, 105), r('LeftElbow', 25), r('RightShoulder', abd=35)]),
    ]


def canonical_neutralized(requests):
    defaults={
        'LeftShoulder':dict(joint='LeftShoulder',flexionDegrees=0,abductionDegrees=35,twistDegrees=0),
        'RightShoulder':dict(joint='RightShoulder',flexionDegrees=0,abductionDegrees=35,twistDegrees=0),
        'LeftHip':dict(joint='LeftHip',flexionDegrees=0,abductionDegrees=0,twistDegrees=0),
        'RightHip':dict(joint='RightHip',flexionDegrees=0,abductionDegrees=0,twistDegrees=0),
        'LeftElbow':dict(joint='LeftElbow',flexionDegrees=0,abductionDegrees=0,twistDegrees=0),
        'RightElbow':dict(joint='RightElbow',flexionDegrees=0,abductionDegrees=0,twistDegrees=0),
        'LeftKnee':dict(joint='LeftKnee',flexionDegrees=0,abductionDegrees=0,twistDegrees=0),
        'RightKnee':dict(joint='RightKnee',flexionDegrees=0,abductionDegrees=0,twistDegrees=0)}
    for request in requests:defaults[request['joint']]=request
    return list(defaults.values())


def role_map(rig, mixamo=False):
    result = {name: name for name in ('Pelvis','SpineLower','SpineMid','Chest','Neck','Head')}
    for side in ('Left','Right'):
        for kind in ('Clavicle','Shoulder','Elbow','Wrist','Hip','Knee','Ankle','ToeBase'):
            result[side+kind] = side+kind
    if mixamo:
        result = {'Pelvis':'Hips','SpineLower':'Spine','SpineMid':'Spine1','Chest':'Spine2','Neck':'Neck','Head':'Head',
            **{side+kind: side+native for side in ('Left','Right') for kind,native in
               dict(Clavicle='Shoulder',Shoulder='Arm',Elbow='ForeArm',Wrist='Hand',Hip='UpLeg',Knee='Leg',Ankle='Foot',ToeBase='ToeBase').items()}}
        result = {kind: 'mixamorig:'+name for kind,name in result.items()}
    missing = [f'{kind}->{name}' for kind,name in result.items() if name not in rig.data.bones]
    if missing:
        raise RuntimeError('Hierarchy-seeded role mapping failed: ' + ', '.join(missing))
    # Names seed the map; parent-chain evidence verifies every benchmark limb.
    for side in ('Left','Right'):
        for parent,child in (('Shoulder','Elbow'),('Elbow','Wrist'),('Hip','Knee'),('Knee','Ankle')):
            bone = rig.data.bones[result[side+child]]
            if bone.parent is None or bone.parent.name != result[side+parent]:
                raise RuntimeError(f'Hierarchy verification failed for {side+parent}->{side+child}.')
    return result


def center(rig, mapping, kind):
    return rig.matrix_world @ rig.pose.bones[mapping[kind]].head


def direction(rig, mapping, a, b):
    return (center(rig,mapping,b)-center(rig,mapping,a)).normalized()


def semantic_measure(rig, mapping, kind):
    side = -1 if kind.startswith('Left') else 1
    if kind.endswith('Shoulder'):
        d = direction(rig,mapping,kind,kind.replace('Shoulder','Elbow'))
        flex=math.degrees(math.asin(max(-1,min(1,d.y))))
        abd=0 if abs(abs(d.y)-1)<1e-6 else math.degrees(math.atan2(side*d.x,-d.z))
        return dict(flexionDegrees=flex,abductionDegrees=abd,twistDegrees=0)
    if kind.endswith('Hip'):
        d = direction(rig,mapping,kind,kind.replace('Hip','Knee'))
        flex=math.degrees(math.asin(max(-1,min(1,d.y))))
        abd=0 if abs(abs(d.y)-1)<1e-6 else math.degrees(math.atan2(side*d.x,-d.z))
        return dict(flexionDegrees=flex,abductionDegrees=abd,twistDegrees=0)
    if kind.endswith('Elbow'):
        proximal=direction(rig,mapping,kind.replace('Elbow','Shoulder'),kind)
        distal=direction(rig,mapping,kind,kind.replace('Elbow','Wrist'))
    else:
        proximal=direction(rig,mapping,kind.replace('Knee','Hip'),kind)
        distal=direction(rig,mapping,kind,kind.replace('Knee','Ankle'))
    return dict(flexionDegrees=math.degrees(math.acos(max(-1,min(1,proximal.dot(distal))))),abductionDegrees=0,twistDegrees=0)


def rotate_world(rig, pose_bone, axis, angle):
    if abs(angle) < 1e-10 or axis.length < 1e-10:
        return
    local_axis = pose_bone.matrix.to_3x3().normalized().inverted() @ axis.normalized()
    pose_bone.rotation_mode='QUATERNION'
    pose_bone.rotation_quaternion = pose_bone.rotation_quaternion @ Quaternion(local_axis, angle)
    bpy.context.view_layer.update()


def apply_semantic(rig, mapping, requests):
    for bone in rig.pose.bones:
        bone.rotation_mode='QUATERNION'; bone.rotation_quaternion=Quaternion(); bone.location=(0,0,0); bone.scale=(1,1,1)
    bpy.context.view_layer.update()
    residuals=[]
    # Parent ball joints first, then hinges. Two correction iterations remove numeric/frame noise.
    ordered=sorted(requests,key=lambda q: 1 if q['joint'].endswith(('Elbow','Knee')) else 0)
    for request in ordered:
        kind=request['joint']; pb=rig.pose.bones[mapping[kind]]
        for _ in range(4):
            if kind.endswith(('Shoulder','Hip')):
                side=-1 if kind.startswith('Left') else 1
                f=math.radians(request.get('flexionDegrees',0)); a=math.radians(request.get('abductionDegrees',0))
                target=Vector((side*math.cos(f)*math.sin(a),math.sin(f),-math.cos(f)*math.cos(a))).normalized()
                child=kind.replace('Shoulder','Elbow').replace('Hip','Knee')
                current=direction(rig,mapping,kind,child)
                q=current.rotation_difference(target)
                rotate_world(rig,pb,q.axis,q.angle)
            else:
                if kind.endswith('Elbow'):
                    proximal=direction(rig,mapping,kind.replace('Elbow','Shoulder'),kind)
                    distal=direction(rig,mapping,kind,kind.replace('Elbow','Wrist'))
                    axis=proximal.cross(Vector((0,1,0)))
                else:
                    proximal=direction(rig,mapping,kind.replace('Knee','Hip'),kind)
                    distal=direction(rig,mapping,kind,kind.replace('Knee','Ankle'))
                    axis=-proximal.cross(Vector((0,1,0)))
                target=Quaternion(axis.normalized(),math.radians(request.get('flexionDegrees',0)))@proximal
                q=distal.rotation_difference(target)
                rotate_world(rig,pb,q.axis,q.angle)
        actual=semantic_measure(rig,mapping,kind)
        requested={k:request.get(k,0) for k in ('flexionDegrees','abductionDegrees','twistDegrees')}
        residuals.append(dict(joint=kind,requested=requested,actual=actual,
            maximumAbsoluteDegrees=max(abs(actual[k]-requested[k]) for k in requested)))
    return residuals


def make_open_scene(surface, artifact, vertices):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    faces=[tuple(f[k] for k in ('a','b','c')) for f in surface['faces']]
    rig=h.make_rig(artifact,h.collection('CANONICAL_A_POSE_RIG'))
    obj=h.mesh('ANTONIA_A_POSE',vertices,faces,h.collection('OPEN_DEFORMATION'))
    mod=obj.modifiers.new('Portable linear blend skinning','ARMATURE');mod.object=rig
    return rig,obj,np.asarray(vertices),np.asarray(faces),h.setup_camera()


def run_field(name, field, rig, obj, mapping, rest, triangles, corpus, out, scene, render):
    h.assign(obj,field)
    cases=[]
    started=time.perf_counter()
    for case in corpus:
        residuals=apply_semantic(rig,mapping,case['requested'])
        posed=h.positions(obj)
        metrics=x1.measure(rest,posed,triangles,h.normal_transport(rig,field,triangles))
        cases.append(dict(pose=case['name'],requested=case['requested'],semanticResiduals=residuals,
            maximumSemanticResidualDegrees=max([r['maximumAbsoluteDegrees'] for r in residuals] or [0]),metrics=metrics))
        if render:
            h.render(scene,obj,out/f'{name}--{case["name"]}.png')
    apply_semantic(rig,mapping,[])
    return dict(pipeline=name,seconds=time.perf_counter()-started,cases=cases)


def export_portable(out, surface, field, evidence, recipe):
    value=dict(schema='aetheris.humanoid.experimental-weights.v1',topologyId=surface['topologyId'],
        connectivityHash=surface['connectivityHash'],referenceRigSha256=evidence['referenceRigSha256'],
        restPoseId=evidence['identities']['restPoseId'],maxInfluences=max(map(len,field)),skinning='linear-blend-skinning',
        deformationRigExtensions=[],provenance=dict(source='Fresh canonical A-pose Blender bone heat plus unchanged RERIG-X1 cleanup',steps=recipe,
            manualPaintStrokes=0,topologyEdits=0,jointEdits=0,blender=bpy.app.version_string,scriptSha256=h.sha(__file__)),
        vertices=[dict(vertexId=v['id'],weights=[dict(boneId=n,weight=w) for n,w in sorted(row.items())]) for v,row in zip(surface['vertices'],field)])
    h.write(out/'selected-weights.json',value)


def mixamo_oracle(path, target, corpus, out, scene, render):
    before=set(bpy.data.objects); started=time.perf_counter()
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    imported=set(bpy.data.objects)-before
    rig=next(o for o in imported if o.type=='ARMATURE'); obj=next(o for o in imported if o.type=='MESH')
    conversion=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
    raw=np.array([tuple(conversion@Vector(v)) for v in h.positions(obj)])
    scale=np.ptp(target[:,2])/np.ptp(raw[:,2]); offset=(target.min(0)+target.max(0))/2-scale*(raw.min(0)+raw.max(0))/2
    alignment=Matrix.Translation(Vector(offset))@Matrix.Scale(float(scale),4)@conversion
    original={o:o.matrix_world.copy() for o in imported}
    def depth(o): return 0 if o.parent is None else 1+depth(o.parent)
    for o in sorted(imported,key=depth): o.matrix_world=alignment@original[o]
    bpy.context.view_layer.update(); mapping=role_map(rig,True)
    native=[]
    for kind,name in sorted(mapping.items()):
        bone=rig.data.bones[name]; world=rig.matrix_world@bone.matrix_local
        native.append(dict(role=kind,sourceBone=name,parent=bone.parent.name if bone.parent else None,
            jointCenterMm=list(world.translation*1000),majorAxis=list(world.to_3x3().normalized()@Vector((0,1,0))),
            localRestTransform=[list(row) for row in bone.matrix_local]))
    cases=[]
    obj.data.calc_loop_triangles();oracle_faces=[tuple(face.vertices) for face in obj.data.loop_triangles]
    for case in corpus:
        effective=canonical_neutralized(case['requested']);residuals=apply_semantic(rig,mapping,effective)
        cases.append(dict(pose=case['name'],requested=case['requested'],semanticResiduals=residuals,
            maximumSemanticResidualDegrees=max([r['maximumAbsoluteDegrees'] for r in residuals] or [0])))
        if render:
            p=h.positions(obj);low=p.min(0);high=p.max(0);s=1.75/max(high[2]-low[2],1e-9);p=(p-(low+high)/2)*s;p[:,2]-=p[:,2].min()
            display=h.mesh('Mixamo normalized display',p,oracle_faces,h.collection('MIXAMO_NORMALIZED_DISPLAY'))
            h.render(scene,display,out/f'mixamo--{case["name"]}.png');bpy.data.objects.remove(display,do_unlink=True)
    apply_semantic(rig,mapping,[])
    evidence=dict(status='available',sourceSha256=h.sha(path),nativeRest='source FBX bind pose; normalized through measured rest directions',
        handedness='proper FBX-to-canonical rotation; determinant positive',unitScaleToMeters=scale,alignment=[list(row) for row in alignment],
        hierarchyVerified=True,joints=native,cases=cases,adapterSeconds=time.perf_counter()-started)
    h.write(out/'mixamo-adapter.json',evidence)
    for o in imported: bpy.data.objects.remove(o,do_unlink=True)
    return evidence


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--out',default=str(ROOT/'artifacts/local/humanoid-rest-x2'))
    parser.add_argument('--mixamo')
    parser.add_argument('--no-render',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    out=Path(args.out).resolve(); out.mkdir(parents=True,exist_ok=True)
    if not out.is_relative_to((ROOT/'artifacts/local').resolve()): raise ValueError('Ignored local output required.')
    candidate,surface,artifact,evidence,vertices=normalized_inputs()
    rig,obj,rest,triangles,scene=make_open_scene(surface,artifact,vertices); mapping=role_map(rig)
    corpus=canonical_corpus(); h.write(out/'pose-corpus.json',corpus)
    # Import the local oracle before Blender's auto-parent operator mutates scene parenting state.
    if args.mixamo and Path(args.mixamo).is_file(): mixamo=mixamo_oracle(Path(args.mixamo).resolve(),rest,corpus,out,scene,not args.no_render)
    else:
        mixamo=dict(status='skipped',diagnostic='No --mixamo local FBX supplied; open qualification remains valid.')
        h.write(out/'mixamo-adapter.json',mixamo)
    old_auto=portable_field(ROOT/'artifacts/local/humanoid-rerig-x1/auto-weights.json')
    old_clean=portable_field(ROOT/'artifacts/local/humanoid-rerig-x1/golden-weights.json')
    auto_started=time.perf_counter(); fresh_auto=x1.automatic_weights(rig,obj); auto_seconds=time.perf_counter()-auto_started
    cleanup_started=time.perf_counter(); fresh_clean=x1.golden_cleanup(obj,rig,rest,fresh_auto); fresh_clean=normalize(fresh_clean); cleanup_seconds=time.perf_counter()-cleanup_started
    h.write(out/'apose-auto-weights.json',fresh_auto); h.write(out/'apose-cleaned-weights.json',fresh_clean)
    variants={'tpose-auto-rebound':old_auto,'tpose-cleaned-rebound':old_clean,'apose-auto':fresh_auto,'apose-cleaned':fresh_clean}
    results=[run_field(name,field,rig,obj,mapping,rest,triangles,corpus,out,scene,not args.no_render) for name,field in variants.items()]
    h.write(out/'weight-ablation.json',dict(schema='aetheris.humanoid.rest-x2.ablation.v1',autoWeightSeconds=auto_seconds,
        cleanupSeconds=cleanup_seconds,cleanupRecipe='unchanged RERIG-X1 golden_cleanup',variants=results))
    # Selection is metric-led and complexity-aware: cleaned wins only when it reduces reversals, then p99, across required poses.
    required={'shoulder60','shoulder90','shoulder120','hip70','hip90','abduction45','elbow90','knee90'}
    def score(result):
        cases=[c for c in result['cases'] if c['pose'] in required]
        return (sum(c['metrics']['reversalCount'] for c in cases),sum(c['metrics']['p99'] for c in cases),sum(c['metrics']['max'] for c in cases))
    open_candidates=[r for r in results if r['pipeline'].startswith('apose-')]
    selected=min(open_candidates,key=score); selected_field=variants[selected['pipeline']]
    selection=dict(selected=selected['pipeline'],scores={r['pipeline']:score(r) for r in results},
        rule='minimum aggregate reversal count, then p99, then max over required ablation poses; only fresh A-pose candidates admissible')
    h.write(out/'selection.json',selection)
    recipe=['native bone heat at temporary data scale 10; restore canonical A-pose frames','normalize']
    if selected['pipeline']=='apose-cleaned': recipe += ['bilateral hip soft gain 0.85 radius 220 mm','native Smooth 0.5 once at knees 110 mm and elbows 100 mm','bilateral shoulder contrast exponent 0.5 radius 180 mm','normalize']
    export_portable(out,surface,selected_field,evidence,recipe)
    h.assign(obj,selected_field)
    exact_path=out/'aetheris/pose-transforms.json'
    exact={case['pose']:case for case in h.read(exact_path)} if exact_path.is_file() else None
    for case in corpus:
        if case['name'] in ('shoulder90','hip70','hip90','knee90'):
            if exact:
                apply_semantic(rig,mapping,[])
                for rotation in exact[case['name']]['localRotations']:
                    rig.pose.bones[rotation['joint']].rotation_quaternion=Quaternion((rotation['w'],rotation['x'],rotation['y'],rotation['z']))
                bpy.context.view_layer.update()
            else:
                apply_semantic(rig,mapping,case['requested'])
            h.write(out/f'blender-selected--{case["name"]}.positions.json',(h.positions(obj)*1000).tolist())
    apply_semantic(rig,mapping,[])
    for block in list(bpy.data.collections):
        if block.name not in ('Collection','CANONICAL_A_POSE_RIG','OPEN_DEFORMATION'): bpy.data.collections.remove(block)
    bpy.context.scene['study']='HUMANOID-REST-X2 open canonical A-pose golden candidate; no proprietary payloads'
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'golden-apose.blend'))
    h.write(out/'benchmark-summary.json',dict(openSelection=selection,mixamoStatus=mixamo['status'],genesisStatus='separate local DAZ process required',
        canonicalRestPoseId=evidence['identities']['restPoseId'],topologyUnchanged=True,proprietaryPayloadsEmbedded=False))
    print(json.dumps(dict(success=True,output=str(out),selection=selection,mixamo=mixamo['status'],autoWeightSeconds=auto_seconds),indent=2))


if __name__=='__main__': main()
