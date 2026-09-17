"""Local, experimental Blender study. No canonical files or Mixamo assets are written to Git.

Run with Blender --background --factory-startup --disable-autoexec --python-exit-code 1
--python scripts/humanoid-rerig-x0.py -- --mixamo <local.fbx> [--no-render].
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import time

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2, allow_nan=False) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def matrix(value):
    m = Matrix([[value[f'm{c+1}{r+1}'] for c in range(4)] for r in range(4)])
    m.translation /= 1000
    return m


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def mesh(name, positions, faces, coll):
    data = bpy.data.meshes.new(name)
    data.from_pydata(positions, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    coll.objects.link(obj)
    for p in data.polygons:
        p.use_smooth = True
    return obj


def active(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def make_rig(artifact, coll):
    data = bpy.data.armatures.new('X5 exact rest frames')
    obj = bpy.data.objects.new('X5_DEFORM', data)
    coll.objects.link(obj)
    active(obj)
    bpy.ops.object.mode_set(mode='EDIT')
    sources = {s['sourceJointId']: s for s in artifact['sourceJoints']}
    for j in artifact['canonicalJoints']:
        b = data.edit_bones.new(j['canonicalJoint'])
        b.head = (0, 0, 0)
        b.tail = (0, .05, 0)
        b.matrix = matrix(j['globalRestCanonical'])
        if j['sourceJointId']:
            s = sources[j['sourceJointId']]
            b.length = (Vector(tuple(s['tailCanonicalMm'][k] for k in 'xyz')) -
                        Vector(tuple(s['headCanonicalMm'][k] for k in 'xyz'))).length / 1000
        b.use_deform = j['canonicalJoint'] != 'Root'
    for j in artifact['canonicalJoints']:
        if j['canonicalParent']:
            data.edit_bones[j['canonicalJoint']].parent = data.edit_bones[j['canonicalParent']]
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.show_in_front = True
    return obj


def weights(obj):
    names = [g.name for g in obj.vertex_groups]
    return [{names[g.group]: float(g.weight) for g in v.groups if g.weight > 1e-8}
            for v in obj.data.vertices]


def assign(obj, field):
    obj.vertex_groups.clear()
    groups = {name: obj.vertex_groups.new(name=name) for name in sorted({n for w in field for n in w})}
    for i, w in enumerate(field):
        for n, value in sorted(w.items()):
            groups[n].add([i], value, 'REPLACE')


def positions(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    return np.array([tuple(evaluated.matrix_world @ v.co) for v in evaluated.data.vertices])


def pose(rig, requests, mapping=None):
    for b in rig.pose.bones:
        b.matrix_basis = Matrix.Identity(4)
    mapping = mapping or {b.name: b.name for b in rig.data.bones}
    for r in requests:
        kind = r['joint']
        b = rig.pose.bones[mapping[kind]]
        rest = (rig.matrix_world @ b.bone.matrix_local).to_3x3().normalized()
        sign = 1 if kind.startswith('Left') else -1
        if kind.endswith(('Knee', 'Elbow')):
            end = kind.replace('Knee', 'Ankle').replace('Elbow', 'Wrist')
            direction = (rig.matrix_world @ rig.data.bones[mapping[end]].head_local -
                         rig.matrix_world @ b.bone.head_local)
            axis = direction.cross(Vector((0, 1, 0))).normalized()
            if kind.endswith('Knee'):
                axis.negate()
            q = Quaternion(rest.inverted() @ axis, math.radians(r['flexionDegrees']))
        else:
            swing = Vector((r['flexionDegrees'], sign*r['abductionDegrees'], 0))
            q = Quaternion(rest.inverted() @ swing.normalized(), math.radians(swing.length)) if swing.length else Quaternion()
            q = q @ Quaternion(rest.inverted() @ Vector((0, 0, -1)), math.radians(sign*r.get('twistDegrees', 0)))
        b.rotation_mode = 'QUATERNION'
        b.rotation_quaternion = q
    bpy.context.view_layer.update()


def metric(rest, posed, triangles, region_ids=None, transported=None):
    t = np.asarray(triangles, dtype=int)
    edges = np.unique(np.sort(np.concatenate((t[:, [0, 1]], t[:, [1, 2]], t[:, [2, 0]])), axis=1), axis=0)
    before = np.linalg.norm(rest[edges[:, 0]]-rest[edges[:, 1]], axis=1)
    after = np.linalg.norm(posed[edges[:, 0]]-posed[edges[:, 1]], axis=1)
    ratio = after / np.maximum(before, 1e-12)
    distortion = np.maximum(ratio, 1 / np.maximum(ratio, 1e-12))
    n0 = np.cross(rest[t[:, 1]]-rest[t[:, 0]], rest[t[:, 2]]-rest[t[:, 0]])
    n1 = np.cross(posed[t[:, 1]]-posed[t[:, 0]], posed[t[:, 2]]-posed[t[:, 0]])
    area = np.linalg.norm(n1, axis=1)/np.maximum(np.linalg.norm(n0, axis=1), 1e-12)
    expected = n0 if transported is None else np.einsum('nij,nj->ni',transported,n0)
    reversal = np.sum(expected*n1, axis=1) < 0
    face_ratios = []
    for a, b in ((0, 1), (1, 2), (2, 0)):
        q = np.linalg.norm(posed[t[:, a]]-posed[t[:, b]], axis=1)/np.maximum(np.linalg.norm(rest[t[:, a]]-rest[t[:, b]], axis=1), 1e-12)
        face_ratios.append(np.maximum(q, 1/np.maximum(q, 1e-12)))
    flagged = (np.max(face_ratios, axis=0)>4) | (area<1e-6) | reversal
    result = dict(minEdgeRatio=float(ratio.min()), maxEdgeRatio=float(ratio.max()),
                  p95EdgeDistortion=float(np.quantile(distortion, .95)), maxEdgeDistortion=float(distortion.max()),
                  minAreaRatio=float(area.min()), maxAreaRatio=float(area.max()),
                  normalReversalProxy=int(reversal.sum()), flaggedTriangles=int(flagged.sum()),
                  collapsedTriangles=int((area<1e-6).sum()))
    if region_ids is not None:
        result['regions'] = {name: metric(rest, posed, t[np.asarray(region_ids)==name], transported=None if transported is None else transported[np.asarray(region_ids)==name])
                             for name in sorted(set(region_ids))}
    return result


def normal_transport(rig, field, triangles):
    rotations = {b.name: np.array((rig.matrix_world @ b.matrix @ b.bone.matrix_local.inverted() @ rig.matrix_world.inverted()).to_3x3()) for b in rig.pose.bones}
    result=[]
    for tri in triangles:
        sums={}
        for i in tri:
            for name,value in field[i].items():
                sums[name]=sums.get(name,0)+value
        dominant=max(sorted(sums),key=sums.get)
        result.append(rotations[dominant])
    return np.array(result)


def weight_stats(obj, field, regions=None):
    names = sorted({n for w in field for n in w})
    dense = np.array([[w.get(n, 0) for n in names] for w in field])
    edges = np.array([tuple(e.vertices) for e in obj.data.edges])
    gradient = np.sum(np.abs(dense[edges[:, 0]]-dense[edges[:, 1]]), axis=1)
    counts = (dense>1e-4).sum(axis=1)
    result = dict(vertices=len(field), maxInfluences=int(counts.max()), meanInfluences=float(counts.mean()),
                  unweighted=int((dense.sum(axis=1)<1e-6).sum()),
                  maxNormalizationError=float(np.max(np.abs(dense.sum(axis=1)-1))),
                  p95EdgeWeightL1=float(np.quantile(gradient,.95)), maxEdgeWeightL1=float(gradient.max()))
    if regions:
        result['regions'] = {}
        for region in sorted(set(regions)):
            mask = np.array(regions)==region
            emask = mask[edges[:,0]] | mask[edges[:,1]]
            mean = dense[mask].mean(axis=0)
            result['regions'][region] = dict(meanInfluences=float(counts[mask].mean()),
                p95EdgeWeightL1=float(np.quantile(gradient[emask], .95)),
                meanWeights={n:float(v) for n,v in zip(names,mean) if v>.001})
            opposite='Right' if region.startswith('Left') else 'Left' if region.startswith('Right') else None
            if opposite:
                cross=dense[mask][:,[i for i,n in enumerate(names) if opposite in n]].sum(axis=1)
                result['regions'][region]['oppositeSideWeightMax']=float(cross.max())
                result['regions'][region]['verticesAboveOnePercentOppositeWeight']=int((cross>.01).sum())
    return result


def setup_camera():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (.58,.63,.68)
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.render.resolution_x = 800
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    data = bpy.data.cameras.new('Comparison orthographic')
    camera = bpy.data.objects.new('Comparison orthographic', data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.type = 'ORTHO'
    data.ortho_scale = 2.65
    camera.location = (2.8, 5, 1.8)
    camera.rotation_euler = (Vector((0,0,1.12))-camera.location).to_track_quat('-Z','Y').to_euler()
    return scene


def render(scene, obj, path):
    for o in scene.objects:
        if o.type == 'MESH':
            o.hide_render = o != obj
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--mixamo', required=True)
    parser.add_argument('--out', default=str(ROOT/'artifacts/local/humanoid-rerig-x0'))
    parser.add_argument('--no-render', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    out = Path(args.out).resolve()
    if not out.is_relative_to((ROOT/'artifacts/local').resolve()):
        raise ValueError('This local-only study must write beneath ignored artifacts/local/.')
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    candidate_path = ROOT/'artifacts/local/humanoid-x1/antonia-adoption-candidate.json'
    rig_path = ROOT/'fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json'
    candidate, artifact = read(candidate_path), read(rig_path)
    baseline_evidence=read(ROOT/'artifacts/local/humanoid-x5/evidence.json')
    if baseline_evidence['sourceCandidateSha256']!=sha(candidate_path) or baseline_evidence['referenceRigSha256']!=sha(rig_path):
        raise RuntimeError('X5 evidence does not match the candidate/reference rig; regenerate X5 before comparison.')
    surface = candidate['sourcePoseSurface']
    verts = [tuple(v['position'][k]/1000 for k in 'xyz') for v in surface['vertices']]
    faces = [tuple(f[k] for k in ('a','b','c')) for f in surface['faces']]
    regions = [v['region'] for v in surface['vertices']]
    face_regions = [f['region'] for f in surface['faces']]
    canonical = mesh('ANTONIA_CANONICAL', verts, faces, collection('ANTONIA_CANONICAL'))
    rig = make_rig(artifact, collection('EXPERIMENTAL_RIG'))
    reference = rig.copy()
    reference.data = rig.data.copy()
    reference.name = 'X5_REFERENCE_RIG'
    collection('X5_REFERENCE_RIG').objects.link(reference)
    reference.hide_set(True)
    experiment = mesh('Blender experiment', verts, faces, rig.users_collection[0])
    active(experiment)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    started = time.time()
    result = bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    elapsed = time.time()-started
    automatic = weights(experiment)
    write(out/'initial.json', dict(blender=bpy.app.version_string, automaticSeconds=elapsed,
        operatorResult=list(result), stats=weight_stats(experiment, automatic, regions)))
    if any(not w for w in automatic):
        # Native heat is scale-sensitive for this mesh's tiny eye/finger structures.
        # Scale temporary data tenfold, solve, then restore the EXACT input frames/vertices.
        for v in experiment.data.vertices: v.co *= 10
        active(rig)
        bpy.ops.object.mode_set(mode='EDIT')
        for b in rig.data.edit_bones: b.head *= 10; b.tail *= 10
        bpy.ops.object.mode_set(mode='OBJECT')
        experiment.vertex_groups.clear()
        active(experiment); rig.select_set(True); bpy.context.view_layer.objects.active=rig
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        automatic=weights(experiment)
        for v,co in zip(experiment.data.vertices,verts): v.co=co
        active(rig); bpy.ops.object.mode_set(mode='EDIT')
        for b in rig.data.edit_bones:
            original=reference.data.bones[b.name]
            b.matrix=original.matrix_local; b.length=original.length
        bpy.ops.object.mode_set(mode='OBJECT')
        write(out/'scale-retry.json', dict(scale=10,stats=weight_stats(experiment,automatic,regions)))
    if any(not w for w in automatic):
        raise RuntimeError('Native automatic weights remain incomplete; no usable rig claimed.')
    write(out/'pass-a-weights.json', automatic)
    # Inspect the local oracle without animation and retain the imported bind pose.
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(Path(args.mixamo).resolve()), use_anim=False)
    imported = set(bpy.data.objects)-before
    coll = collection('MIXAMO_REFERENCE_LOCAL')
    for obj in imported:
        for c in list(obj.users_collection):
            c.objects.unlink(obj)
        coll.objects.link(obj)
    inventory = []
    for o in sorted(imported, key=lambda o:o.name):
        item = dict(name=o.name, type=o.type, matrix=[list(row) for row in o.matrix_world])
        if o.type=='ARMATURE':
            item['bones'] = [dict(name=b.name,parent=b.parent.name if b.parent else None,
                head=list(o.matrix_world@b.head_local),tail=list(o.matrix_world@b.tail_local),
                restMatrix=[list(row) for row in b.matrix_local],inverseBind=[list(row) for row in (o.matrix_world@b.matrix_local).inverted()]) for b in o.data.bones]
        if o.type=='MESH':
            item.update(vertices=len(o.data.vertices), polygons=len(o.data.polygons),stats=weight_stats(o,weights(o)))
            write(out/(o.name.replace(':','_')+'-mixamo-local-weights.json'),weights(o))
        inventory.append(item)
    write(out/'mixamo-local-inventory.json', dict(sha256=sha(args.mixamo),objects=inventory))
    write(out/'inputs.json',dict(candidateSha256=sha(candidate_path),rigSha256=sha(rig_path),mixamoSha256=sha(args.mixamo),blender=bpy.app.version_string))
    oracle_rig=next(o for o in imported if o.type=='ARMATURE')
    oracle=next(o for o in imported if o.type=='MESH')
    pose(oracle_rig, [])
    # FBX imported -Y-up, +Z-front. Proper rotation maps it to X5 +Z-up,+Y-front.
    conversion=Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
    raw=np.array([tuple(conversion @ Vector(v)) for v in positions(oracle)])
    target=np.array(verts)
    scale=np.ptp(target[:,2])/np.ptp(raw[:,2])
    offset=(target.min(axis=0)+target.max(axis=0))/2-scale*(raw.min(axis=0)+raw.max(axis=0))/2
    alignment=Matrix.Translation(Vector(offset)) @ Matrix.Scale(float(scale),4) @ conversion
    original_world={o:o.matrix_world.copy() for o in imported}
    # Assign parent first, then each object's desired global matrix.
    def depth(o):
        return 0 if o.parent is None else 1+depth(o.parent)
    for o in sorted(imported,key=depth):
        o.matrix_world=alignment @ original_world[o]
    bpy.context.view_layer.update()
    mapping={'Pelvis':'Hips','SpineLower':'Spine','SpineMid':'Spine1','Chest':'Spine2','Neck':'Neck','Head':'Head'}
    for side in ('Left','Right'):
        mapping.update({side+k:side+v for k,v in dict(Clavicle='Shoulder',Shoulder='Arm',Elbow='ForeArm',Wrist='Hand',Hip='UpLeg',Knee='Leg',Ankle='Foot',ToeBase='ToeBase').items()})
    mapping={k:'mixamorig:'+v for k,v in mapping.items()}
    differences=[]
    for kind,name in mapping.items():
        x5=rig.matrix_world @ rig.data.bones[kind].matrix_local
        mx=oracle_rig.matrix_world @ oracle_rig.data.bones[name].matrix_local
        angle=math.degrees(x5.to_quaternion().rotation_difference(mx.to_quaternion()).angle)
        differences.append(dict(joint=kind,mixamo=name,positionDifferenceMm=list((mx.translation-x5.translation)*1000),
            centerDistanceMm=(mx.translation-x5.translation).length*1000,
            frameAngleDegrees=min(angle,360-angle)))
    oracle_rest=positions(oracle)
    from mathutils.kdtree import KDTree
    tree=KDTree(len(target))
    for i,v in enumerate(target):tree.insert(Vector(v),i)
    tree.balance()
    matches=[tree.find(Vector(v)) for v in oracle_rest]
    nearest=[m[1] for m in matches]
    oracle_regions=[regions[i] for i in nearest]
    write(out/'mixamo-local-mapping.json',dict(alignment=[list(row) for row in alignment],
        nearestSurfaceMaxMm=max(m[2] for m in matches)*1000,nearestUnique=len(set(nearest)),
        indexwiseMaxMm=float(np.linalg.norm(oracle_rest-target,axis=1).max()*1000) if len(oracle_rest)==len(target) else None,
        majorJoints=differences))
    oracle.data.calc_loop_triangles()
    oracle_faces=[tuple(t.vertices) for t in oracle.data.loop_triangles]
    oracle_face_regions=[oracle_regions[t[0]] for t in oracle_faces]
    oracle_field=weights(oracle)
    old_names=[j['kind'] for j in candidate['sourcePoseSkeleton']['joints']]
    old_field=[{old_names[w['jointIndex']]:w['weight'] for w in v['weights']} for v in surface['skinWeights']]
    baseline=mesh('X5 LBS comparison',verts,faces,collection('X5_CURRENT_DEFORMATION'))
    assign(baseline,old_field)
    mod=baseline.modifiers.new('X5 LBS','ARMATURE');mod.object=rig
    active(experiment)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
    bpy.ops.object.vertex_group_smooth(group_select_mode='ALL',factor=.5,repeat=3)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    smoothed=weights(experiment)
    write(out/'pass-b-weights.json',smoothed)
    normalized=[{n:w/sum(v.values()) for n,w in v.items()} for v in automatic]
    # Final bounded cleanup: exact semantic mirror averaging, then native smoothing
    # only within 80 mm of elbow/knee centers. No vertex positions or pivots change.
    symmetric=[]
    for i,v in enumerate(surface['vertices']):
        partner=v['symmetryPartnerIndex']
        other=normalized[partner] if partner is not None else normalized[i]
        mirrored={n.replace('Left','TEMP').replace('Right','Left').replace('TEMP','Right'):w for n,w in other.items()}
        symmetric.append({n:(normalized[i].get(n,0)+mirrored.get(n,0))/2 for n in sorted(set(normalized[i])|set(mirrored))})
    assign(experiment,symmetric)
    centers=[rig.data.bones[s+j].head_local for s in ('Left','Right') for j in ('Knee','Elbow')]
    for v in experiment.data.vertices:v.select=min((v.co-c).length for c in centers)<.08
    experiment.data.use_paint_mask_vertex=True
    active(experiment);bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
    bpy.ops.object.vertex_group_smooth(group_select_mode='ALL',factor=.25,repeat=1)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    experiment.data.use_paint_mask_vertex=False
    cleanup=weights(experiment)
    write(out/'pass-c-local-weights.json',cleanup)
    fields={'pass-a':automatic,'pass-b':smoothed,'pass-c-volume':normalized,'pass-c-corrective':normalized,
            'pass-c-local':cleanup}
    stats={'x5':weight_stats(baseline,old_field,regions),'mixamo':weight_stats(oracle,oracle_field,oracle_regions)}
    for name,field in fields.items():
        stats[name]=weight_stats(experiment,field,regions)
        # Canonical identity provides exact symmetry partners; no nearest-neighbor paint transfer.
        errors=[]
        for i,v in enumerate(surface['vertices']):
            other=field[v['symmetryPartnerIndex']] if v['symmetryPartnerIndex'] is not None else field[i]
            mirrored={n.replace('Left','TEMP').replace('Right','Left').replace('TEMP','Right'):w for n,w in other.items()}
            errors.append(sum(abs(field[i].get(n,0)-mirrored.get(n,0)) for n in set(field[i])|set(mirrored)))
        stats[name]['symmetryWeightL1Max']=max(errors)
        stats[name]['symmetryWeightL1P95']=float(np.quantile(errors,.95))
    write(out/'weight-inspection.json',stats)
    evidence=baseline_evidence
    corpus=[dict(name=c['name'],requested=c['requested']) for c in evidence['cases']]
    def request(j,f=0,a=0):return dict(joint=j,flexionDegrees=f,abductionDegrees=a,twistDegrees=0)
    corpus += [dict(name='combined-stride',requested=[request('LeftHip',30),request('RightHip',-15),request('LeftKnee',45),request('RightElbow',45)]),
               dict(name='combined-raised-arm',requested=[request('LeftShoulder',a=60),request('RightShoulder',a=60),request('LeftElbow',90)]),
               dict(name='combined-balance',requested=[request('LeftHip',70),request('LeftKnee',90),request('RightShoulder',a=30)])]
    write(out/'pose-corpus.json',corpus)
    scene=setup_camera()
    armature=next(m for m in experiment.modifiers if m.type=='ARMATURE')
    correction=experiment.modifiers.new('Native local Corrective Smooth','CORRECTIVE_SMOOTH')
    correction.factor=.5
    correction.iterations=5
    correction.smooth_type='LENGTH_WEIGHTED'
    correction.show_viewport=False;correction.show_render=False
    records=[]
    for variant,field in fields.items():
        assign(experiment,field)
        armature.use_deform_preserve_volume=variant in ('pass-c-volume','pass-c-corrective')
        correction.show_viewport=correction.show_render=variant=='pass-c-corrective'
        if variant=='pass-c-corrective':
            group=experiment.vertex_groups.new(name='LOCAL_TRANSITIONS')
            for i,w in enumerate(field):
                if regions[i] not in ('Head','LeftEye','RightEye','Neck') and max(w.values())<.98:
                    group.add([i],1,'REPLACE')
            correction.vertex_group=group.name
            write(out/'corrective-mask.json',[v.index for v in experiment.data.vertices if any(g.group==group.index for g in v.groups)])
        for case in corpus:
            pose(rig,case['requested'])
            p=positions(experiment)
            record=dict(pipeline=variant,pose=case['name'],metrics=metric(target,p,faces,face_regions,normal_transport(rig,field,faces)))
            records.append(record)
            print(variant,case['name'],record['metrics']['maxEdgeDistortion'],flush=True)
            if not args.no_render:render(scene,experiment,out/(variant+'--'+case['name']+'.png'))
        write(out/'metrics-progress.json',records)
    for case in corpus:
        pose(rig,case['requested']);pose(oracle_rig,case['requested'],mapping)
        x5=next((c for c in evidence['cases'] if c['name']==case['name']),None)
        if x5:
            lines=(ROOT/'artifacts/local/humanoid-x5'/x5['obj']).read_text().splitlines()
            p=np.array([[float(x)/1000 for x in line.split()[1:]] for line in lines if line.startswith('v ')])
            # Compare native LBS emulation to the real X5 output; hip JDM winners may differ.
            residual=float(np.linalg.norm(p-positions(baseline),axis=1).max()*1000)
            display=mesh('X5 actual '+case['name'],p,faces,baseline.users_collection[0])
        else:
            p=positions(baseline);residual=None;display=baseline
        records.append(dict(pipeline='x5',pose=case['name'],nativeLbsResidualMm=residual,
            origin='X5 real exported mesh' if x5 else 'Blender LBS replay of X5 weights/frames',
            metrics=metric(target,p,faces,face_regions,normal_transport(rig,old_field,faces))))
        if not args.no_render:render(scene,display,out/('x5--'+case['name']+'.png'))
        if display!=baseline:bpy.data.objects.remove(display,do_unlink=True)
        p=positions(oracle)
        records.append(dict(pipeline='mixamo',pose=case['name'],metrics=metric(oracle_rest,p,oracle_faces,oracle_face_regions,normal_transport(oracle_rig,oracle_field,oracle_faces))))
        if not args.no_render:render(scene,oracle,out/('mixamo--'+case['name']+'.png'))
    write(out/'metrics.json',records)
    pose(rig,[]);pose(oracle_rig,[],mapping)
    assign(experiment,automatic)
    armature.use_deform_preserve_volume=False
    correction.show_viewport=correction.show_render=False
    for o in scene.objects:
        if o.type=='MESH':o.hide_render=o!=experiment;o.hide_set(o!=experiment)
    reference.hide_set(True);oracle_rig.hide_set(True)
    topology=[tuple(p.vertices) for p in experiment.data.polygons]
    frame_error=max(max(abs(rig.data.bones[j['canonicalJoint']].matrix_local[r][c]-matrix(j['globalRestCanonical'])[r][c]) for r in range(4) for c in range(4)) for j in artifact['canonicalJoints'])
    write(out/'integrity.json',dict(vertices=len(verts),faces=len(faces),topologyExact=topology==faces,
        restPositionMaxMm=float(np.linalg.norm(positions(experiment)-target,axis=1).max()*1000),maxRestMatrixElementError=frame_error,
        connectivityHash=surface['connectivityHash'],manualPaintEdits=0))
    write(out/'x5-skeleton.json',artifact)
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'experimental.blend'))


if __name__ == '__main__':
    main()
