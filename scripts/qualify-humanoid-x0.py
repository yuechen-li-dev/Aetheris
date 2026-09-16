"""Deterministic Blender qualification for HUMANOID-X0.

Run only through Blender.  The script reads the admitted Antonia mesh without
executing CharMorph code, fits Aetheris-owned vertices in a normalized study
frame, and writes ignored local evidence.  It never copies source connectivity
into the canonical artifact.
"""
import argparse, hashlib, json, math, os, statistics, time
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def canonical_region(name):
    if name in ("Head", "Face", "LeftEar", "RightEar"): return "head"
    if name in ("Neck", "Chest", "Abdomen", "Pelvis"): return "torso"
    if name.startswith("Left") and any(x in name for x in ("Shoulder", "Arm", "Elbow", "Forearm", "Wrist", "Hand", "Thumb", "Index", "Middle", "Ring", "Little")): return "left-arm"
    if name.startswith("Right") and any(x in name for x in ("Shoulder", "Arm", "Elbow", "Forearm", "Wrist", "Hand", "Thumb", "Index", "Middle", "Ring", "Little")): return "right-arm"
    if name.startswith("Left") and any(x in name for x in ("Thigh", "Knee", "Shin", "Ankle", "Foot", "Toes")): return "left-leg"
    if name.startswith("Right") and any(x in name for x in ("Thigh", "Knee", "Shin", "Ankle", "Foot", "Toes")): return "right-leg"
    return "unsupported"


def source_region(material_index, p):
    # Material scope is frozen in the sidecar.  Side identity follows X0's
    # anatomical convention: negative X is anatomical left.
    side = "left" if p.x < 0 else "right"
    if material_index == 11: return "head"
    if material_index == 10: return "torso"
    if material_index == 9 or (material_index in (5, 6) and p.z > 850): return side + "-arm"
    if material_index == 12 or material_index in (5, 6): return side + "-leg"
    return "unsupported"


def make_bvhs(vertices, polygons_by_region):
    result = {}
    for region, polygons in polygons_by_region.items():
        if polygons:
            result[region] = BVHTree.FromPolygons(vertices, polygons, all_triangles=False)
    return result


def normals(vertices, faces):
    out = [Vector((0, 0, 0)) for _ in vertices]
    for a, b, c in faces:
        n = (vertices[b] - vertices[a]).cross(vertices[c] - vertices[a])
        out[a] += n; out[b] += n; out[c] += n
    for n in out:
        if n.length_squared: n.normalize()
    return out


def percentile(values, p):
    if not values: return None
    values = sorted(values); x = (len(values) - 1) * p
    lo, hi = int(math.floor(x)), int(math.ceil(x))
    return values[lo] if lo == hi else values[lo] * (hi - x) + values[hi] * (x - lo)


def weighted_stats(samples):
    samples = [(d, w) for d, w in samples if math.isfinite(d) and w > 0]
    if not samples: return {"rmsMm": None, "p95Mm": None, "maxMm": None, "sampleCount": 0}
    total = sum(w for _, w in samples)
    rms = math.sqrt(sum(d*d*w for d, w in samples) / total)
    expanded = sorted(samples)
    target = total * .95; acc = 0; p95 = expanded[-1][0]
    for d, w in expanded:
        acc += w
        if acc >= target: p95 = d; break
    return {"rmsMm": rms, "p95Mm": p95, "maxMm": max(d for d, _ in samples), "sampleCount": len(samples)}


def triangles_and_area(vertices, faces):
    for a, b, c in faces:
        area = .5 * (vertices[b]-vertices[a]).cross(vertices[c]-vertices[a]).length
        yield a, b, c, area


def write_obj(path, vertices, faces, regions):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("# HUMANOID-X0 local registration evidence; Aetheris connectivity\n")
        for p in vertices: f.write(f"v {p.x:.9f} {p.y:.9f} {p.z:.9f}\n")
        last = None
        for i, (a,b,c) in enumerate(faces):
            region = regions[i]
            if region != last: f.write(f"g {region}\n"); last = region
            f.write(f"f {a+1} {b+1} {c+1}\n")


def material(name, color, emission=0):
    m = bpy.data.materials.new(name); m.diffuse_color = (*color, 1)
    m.use_nodes = True; bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = .75
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*color, 1)
        bsdf.inputs["Emission Strength"].default_value = emission
    return m


def mesh_object(name, vertices, faces, mat, location=(0,0,0), wire=False):
    mesh = bpy.data.meshes.new(name + ".mesh")
    mesh.from_pydata([tuple(v) for v in vertices], [], faces); mesh.update()
    obj = bpy.data.objects.new(name, mesh); bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat); obj.location = location
    if wire:
        mod = obj.modifiers.new("Canonical wireframe", "WIREFRAME"); mod.thickness = 1.2
    return obj


def render_views(out_dir, source_v, source_f, pre_v, fitted_v, faces, errors, landmark_points):
    bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete(use_global=False)
    src_mat = material("Antonia admitted outer skin", (.28,.48,.70))
    pre_mat = material("Authored template", (.78,.55,.24))
    fit_mat = material("Fitted canonical", (.24,.67,.39))
    mesh_object("Antonia source", source_v, source_f, src_mat, (-900,0,0))
    mesh_object("Canonical before fit", pre_v, faces, pre_mat, (0,0,0))
    mesh_object("Canonical fitted", fitted_v, faces, fit_mat, (900,0,0))
    world=bpy.context.scene.world or bpy.data.worlds.new("World"); bpy.context.scene.world=world
    world.color=(.035,.035,.035)
    bpy.context.scene.render.engine="BLENDER_EEVEE"
    for loc,energy,size in [((-2600,-3200,3600),2200,1800),((2600,-1800,2500),1600,1400),((0,2500,1800),1200,1200)]:
        light_data=bpy.data.lights.new("Evidence light","AREA"); light_data.energy=energy; light_data.shape="DISK"; light_data.size=size
        light=bpy.data.objects.new("Evidence light",light_data); bpy.context.collection.objects.link(light); light.location=loc
        light.rotation_euler=(Vector((0,0,900))-light.location).to_track_quat("-Z","Y").to_euler()
    bpy.context.scene.render.resolution_x=1500; bpy.context.scene.render.resolution_y=720; bpy.context.scene.render.resolution_percentage=100
    bpy.context.scene.render.image_settings.file_format="PNG"; bpy.context.scene.render.film_transparent=False
    cam_data=bpy.data.cameras.new("Camera"); cam=bpy.data.objects.new("Camera",cam_data); bpy.context.collection.objects.link(cam)
    bpy.context.scene.camera=cam; cam_data.type="ORTHO"; cam_data.ortho_scale=2250
    cam_data.clip_end=10000
    views={"front":(Vector((0,-5200,900)),Vector((0,0,900))),"back":(Vector((0,5200,900)),Vector((0,0,900))),"side":(Vector((5200,0,900)),Vector((0,0,900))),"iso":(Vector((4200,-4200,3000)),Vector((0,0,850)))}
    for name,(loc,target) in views.items():
        cam.location=loc; cam.rotation_euler=(target-loc).to_track_quat("-Z","Y").to_euler()
        bpy.context.scene.render.filepath=str(out_dir / f"matched-{name}.png"); bpy.ops.render.render(write_still=True)

    # A separate exact-position overlay makes the independent connectivity visible.
    for o in list(bpy.context.scene.objects):
        if o.type=="MESH": bpy.data.objects.remove(o,do_unlink=True)
    mesh_object("Antonia", source_v, source_f, material("source-gray",(.18,.28,.40)))
    mesh_object("Fitted wire", fitted_v, faces, material("wire",(.95,.30,.12),1), wire=True)
    cam.location=(0,-5200,900); cam.rotation_euler=(Vector((0,0,900))-cam.location).to_track_quat("-Z","Y").to_euler(); cam.data.ortho_scale=2000
    bpy.context.scene.render.filepath=str(out_dir / "wireframe-overlay-front.png"); bpy.ops.render.render(write_still=True)

    # Landmark witness uses small emissive spheres; IDs remain in JSON evidence.
    red=material("landmarks",(1,.05,.03),2)
    for i,p in enumerate(landmark_points):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=9, location=p)
        bpy.context.object.name=f"landmark-{i:03}"; bpy.context.object.data.materials.append(red)
    bpy.context.scene.render.filepath=str(out_dir / "landmark-overlay-front.png"); bpy.ops.render.render(write_still=True)

    # Face error heatmap (blue <=5, yellow <=10, red >10 mm).
    for o in list(bpy.context.scene.objects):
        if o.type=="MESH": bpy.data.objects.remove(o,do_unlink=True)
    heat=mesh_object("fit-error-heatmap", fitted_v, faces, material("error-0-5mm",(.05,.25,1),1))
    heat.data.materials.append(material("error-5-10mm",(1,.75,.03),1)); heat.data.materials.append(material("error-over-10mm",(1,.03,.02),1))
    for polygon,face in zip(heat.data.polygons,faces):
        e=sum(errors[i] for i in face)/3; polygon.material_index=0 if e<=5 else 1 if e<=10 else 2
    bpy.context.scene.render.filepath=str(out_dir / "error-heatmap-front.png"); bpy.ops.render.render(write_still=True)


def main():
    started=time.perf_counter()
    argv = os.sys.argv[os.sys.argv.index("--")+1:] if "--" in os.sys.argv else []
    ap=argparse.ArgumentParser(); ap.add_argument("--repo",required=True); ns=ap.parse_args(argv)
    repo=Path(ns.repo).resolve(); out=repo/"artifacts/local/humanoid-x0"; out.mkdir(parents=True,exist_ok=True)
    artifact=out/"canonical-adult-standard-v1.json"
    blend=repo/"artifacts/local/humanoid-recon-x0/CharMorph-db/characters/antonia/char.blend"
    sidecar_path=repo/"fixtures/Canonical/Humanoid/antonia-reference.sidecar.json"
    config_path=repo/"fixtures/Canonical/Humanoid/x0-registration-config.json"
    sidecar=json.loads(sidecar_path.read_text()); config=json.loads(config_path.read_text()); model=json.loads(artifact.read_text())
    if sha256(blend) != sidecar["sha256"]: raise RuntimeError("HUM002_ReferenceHashMismatch")
    if sidecar["fitResearchAdmission"] != "approved-by-HUMANOID-X0-mission-with-attribution": raise RuntimeError("HUM012_ReferenceNotAdmitted")
    expected_connectivity=model["surface"]["connectivityHash"]
    pre=[Vector((v["position"]["x"],v["position"]["y"],v["position"]["z"])) for v in model["surface"]["vertices"]]
    faces=[(f["a"],f["b"],f["c"]) for f in model["surface"]["faces"]]
    face_regions=[f["region"] for f in model["surface"]["faces"]]
    vertex_regions=[canonical_region(v["region"]) for v in model["surface"]["vertices"]]
    adjacency=[set() for _ in pre]
    incident=[set() for _ in pre]
    for face_index,(a,b,c) in enumerate(faces):
        adjacency[a].update((b,c)); adjacency[b].update((a,c)); adjacency[c].update((a,b))
        incident[a].add(face_index); incident[b].add(face_index); incident[c].add(face_index)

    # Open the audited blend with UI/script execution disabled by the invoking command.
    with bpy.data.libraries.load(str(blend), link=False) as (src,dst):
        if sidecar["object"] not in src.objects: raise RuntimeError("Expected cm_antonia object missing")
        dst.objects=[sidecar["object"]]
    source_obj=dst.objects[0]; bpy.context.collection.objects.link(source_obj)
    mesh=source_obj.data
    admitted=set(sidecar["unsupportedMaterialIndices"]); admitted=set(range(16))-admitted
    raw=[source_obj.matrix_world @ v.co for v in mesh.vertices]
    admitted_indices={i for p in mesh.polygons if p.material_index in admitted for i in p.vertices}
    minz=min(raw[i].z for i in admitted_indices); maxz=max(raw[i].z for i in admitted_indices); scale=1750/(maxz-minz)
    source=[Vector((p.x*scale,p.y*scale,(p.z-minz)*scale)) for p in raw]
    by_region={r:[] for r in ("head","torso","left-arm","right-arm","left-leg","right-leg")}
    admitted_faces=[]
    for p in mesh.polygons:
        if p.material_index not in admitted: continue
        inds=list(p.vertices); center=sum((source[i] for i in inds),Vector())/len(inds)
        region=source_region(p.material_index,center)
        if region=="unsupported": continue
        by_region[region].append(inds); admitted_faces.append(inds)
    bvhs=make_bvhs(source,by_region)

    # A deterministic LBS pose is the coarse stage. It uses the same frozen
    # weights, hierarchy, and transform order as HumanoidPosing.
    def col_matrix(m):
        return Matrix(((m["m11"],m["m21"],m["m31"],m["m41"]),(m["m12"],m["m22"],m["m32"],m["m42"]),(m["m13"],m["m23"],m["m33"],m["m43"]),(m["m14"],m["m24"],m["m34"],m["m44"])))
    pose_degrees={"LeftShoulder":43.0,"RightShoulder":-43.0,"LeftElbow":8.0,"RightElbow":-8.0}
    globals=[]
    for j in model["skeleton"]["joints"]:
        local=col_matrix(j["localRest"]["matrix"])
        rot=Matrix.Rotation(math.radians(pose_degrees.get(j["kind"],0)),4,"Y")
        local=local@rot
        globals.append((globals[j["parentIndex"]]@local) if j["parentIndex"] is not None else local)
    skin_by_vertex={s["vertexIndex"]:s["weights"] for s in model["surface"]["skinWeights"]}
    current=[]
    for i,p in enumerate(pre):
        q=Vector((0,0,0,0)); hp=Vector((p.x,p.y,p.z,1))
        for w in skin_by_vertex[i]:
            j=model["skeleton"]["joints"][w["jointIndex"]]
            q += (globals[w["jointIndex"]] @ col_matrix(j["inverseBind"]) @ hp) * w["weight"]
        current.append(Vector((q.x,q.y,q.z)))
    fit_start=[p.copy() for p in current]
    initial_face_normals=[(fit_start[b]-fit_start[a]).cross(fit_start[c]-fit_start[a]) for a,b,c in faces]
    iteration_evidence=[]
    for it,schedule in enumerate(config["iterations"],1):
        ns=normals(current,faces); projected=[]; accepted=0; residuals=[]; maxd=config["correspondence"]["maximumDistanceMm"]
        for i,p in enumerate(current):
            region=vertex_regions[i]; hit=bvhs.get(region).find_nearest(p) if region in bvhs else None
            if not hit or hit[0] is None or hit[3] > maxd: projected.append(p.copy()); continue
            target,n,poly,d=hit
            if n and ns[i].dot(n) < config["correspondence"]["minimumNormalDot"]: projected.append(p.copy()); continue
            delta=target-p; limit=schedule["maximumStepMm"]
            if delta.length>limit: delta.normalize(); delta*=limit
            projected.append(p+delta*schedule["projectionFraction"]); accepted+=1; residuals.append(d)
        smoothed=[]; lap=schedule["laplacianFraction"]
        for i,p in enumerate(projected):
            if vertex_regions[i]=="unsupported" or not adjacency[i]: smoothed.append(p); continue
            avg=sum((projected[j] for j in adjacency[i]),Vector())/len(adjacency[i])
            smoothed.append(p+(avg-p)*lap*config["objective"]["regularizationWeight"])
        # Explicit symmetry averages paired displacements before admission.
        done=set()
        for i,v in enumerate(model["surface"]["vertices"]):
            j=v["symmetryPartnerIndex"]
            if j==i or i in done or j<0 or j>=len(smoothed): continue
            di=smoothed[i]-current[i]; dj=smoothed[j]-current[j]
            reflected_dj=Vector((-dj.x,dj.y,dj.z)); avg=(di+reflected_dj)/2
            smoothed[i]=current[i]+avg; smoothed[j]=current[j]+Vector((-avg.x,avg.y,avg.z)); done.update((i,j))
        # Pair-local deterministic backtracking admits useful motion while
        # preventing every affected face from crossing its fit-pose normal.
        candidate=[p.copy() for p in current]; accepted_fractions=[]; processed=set()
        for i,v in enumerate(model["surface"]["vertices"]):
            if i in processed: continue
            j=v["symmetryPartnerIndex"]; group=[i] if j==i or j<0 or j>=len(current) else [i,j]
            processed.update(group); alpha=1.0; affected=set().union(*(incident[k] for k in group))
            while alpha>=1/1024:
                trial={k:current[k]+(smoothed[k]-current[k])*alpha for k in group}; bad=False
                for fi in affected:
                    a,b,c=faces[fi]; pa=trial.get(a,candidate[a]); pb=trial.get(b,candidate[b]); pc=trial.get(c,candidate[c])
                    n=(pb-pa).cross(pc-pa); baseline=initial_face_normals[fi]
                    if baseline.length>=2e-4 and (n.length<2e-4 or baseline.dot(n)<=0): bad=True; break
                if not bad: break
                alpha*=.5
            if alpha<1/1024: alpha=0
            for k in group: candidate[k]=current[k]+(smoothed[k]-current[k])*alpha
            accepted_fractions.append(alpha)
        current=candidate
        iteration_evidence.append({"iteration":it,"acceptedVertices":accepted,"preProjectionRmsMm":math.sqrt(sum(x*x for x in residuals)/len(residuals)) if residuals else None,"meanAcceptedStepFraction":sum(accepted_fractions)/len(accepted_fractions),"minimumAcceptedStepFraction":min(accepted_fractions)})

    # Canonical -> source errors and landmark residuals.
    errors=[]; covered=0
    for i,p in enumerate(current):
        hit=bvhs.get(vertex_regions[i]).find_nearest(p) if vertex_regions[i] in bvhs else None
        d=hit[3] if hit and hit[0] is not None else float("inf"); errors.append(d)
        if math.isfinite(d): covered+=1
    landmark_samples=[]; landmark_points=[]
    face_by_id={f["id"]:f for f in model["surface"]["faces"]}
    for lm in model["landmarks"]:
        if lm.get("kind")!="surface": continue
        f=face_by_id[lm["binding"]["faceId"]]; w=lm["binding"]
        p=current[f["a"]]*w["barycentricA"]+current[f["b"]]*w["barycentricB"]+current[f["c"]]*w["barycentricC"]
        region=canonical_region(f["region"]); hit=bvhs.get(region).find_nearest(p) if region in bvhs else None
        if hit and hit[0] is not None: landmark_samples.append({"id":lm["id"],"residualMm":hit[3],"region":region}); landmark_points.append(tuple(p))
    lvals=[x["residualMm"] for x in landmark_samples]

    # Bidirectional area-weighted centroid-to-surface metrics.
    fitted_bvh=BVHTree.FromPolygons(current,faces,all_triangles=True)
    forward=[]
    for face_index,(a,b,c,area) in enumerate(triangles_and_area(current,faces)):
        center=(current[a]+current[b]+current[c])/3; region=canonical_region(face_regions[face_index])
        hit=bvhs.get(region).find_nearest(center) if region in bvhs else None
        if hit and hit[0] is not None: forward.append((hit[3],area))
    reverse=[]
    for poly in admitted_faces:
        center=sum((source[i] for i in poly),Vector())/len(poly)
        area=0
        for k in range(1,len(poly)-1): area += .5*(source[poly[k]]-source[poly[0]]).cross(source[poly[k+1]]-source[poly[0]]).length
        hit=fitted_bvh.find_nearest(center)
        if hit and hit[0] is not None: reverse.append((hit[3],area))
    bidirectional=weighted_stats(forward+reverse)

    degenerate=0; inverted=0; stretches=[]; seen=set()
    for fi,(a,b,c,area) in enumerate(triangles_and_area(current,faces)):
        if area<config["validation"]["minimumTriangleAreaMm2"]: degenerate+=1
        newn=(current[b]-current[a]).cross(current[c]-current[a])
        if newn.length_squared and initial_face_normals[fi].dot(newn)<=0: inverted+=1
        for u,v in ((a,b),(b,c),(c,a)):
            e=tuple(sorted((u,v)))
            if e in seen: continue
            seen.add(e); base=(fit_start[u]-fit_start[v]).length
            if base>=1.0: stretches.append((current[u]-current[v]).length/base)
    symmetry=[]
    for i,v in enumerate(model["surface"]["vertices"]):
        j=v["symmetryPartnerIndex"]
        if j>i: symmetry.append((current[i]-Vector((-current[j].x,current[j].y,current[j].z))).length)
    regional={}
    review_map={"face":["head"],"ears":["head"],"shoulders":["left-arm","right-arm"],"armpits":["left-arm","right-arm"],"hands":["left-arm","right-arm"],"crotch":["torso","left-leg","right-leg"],"knees":["left-leg","right-leg"],"feet":["left-leg","right-leg"]}
    for name,rs in review_map.items():
        vals=[errors[i] for i,r in enumerate(vertex_regions) if r in rs and math.isfinite(errors[i])]
        regional[name]={"rmsMm":math.sqrt(sum(x*x for x in vals)/len(vals)) if vals else None,"p95Mm":percentile(vals,.95),"maxMm":max(vals) if vals else None,"status":"review-required" if vals else "unsupported"}

    output_hash=hashlib.sha256("".join(f"{p.x:.9f},{p.y:.9f},{p.z:.9f};" for p in current).encode()).hexdigest()
    source_region_stats={r:{"polygons":len(ps),"boundsMm":{"minimum":[min(source[i][axis] for p in ps for i in p) for axis in range(3)],"maximum":[max(source[i][axis] for p in ps for i in p) for axis in range(3)]}} for r,ps in by_region.items() if ps}
    result={
      "schema":"aetheris.humanoid.fit-evidence.v1","milestone":"HUMANOID-X0","reference":sidecar["assetId"],
      "researchUseAdmission":"passed","canonicalOutputAdmission":sidecar["canonicalOutputAdmission"],
      "configuration":{"id":config["id"],"sha256":sha256(config_path),"randomSeed":config["randomSeed"],"fitElapsedMilliseconds":(time.perf_counter()-started)*1000,"iterations":iteration_evidence},
      "topology":{"id":model["surface"]["topologyId"],"beforeHash":expected_connectivity,"afterHash":expected_connectivity,"unchanged":True,"vertices":len(current),"triangles":len(faces)},
      "normalization":{"sourceHeightMm":1750,"canonicalHeightMm":1750,"sourceAdmittedPolygons":len(admitted_faces),"sourceRegions":source_region_stats,"sourceConnectivityCopied":False},
      "landmarks":{"method":"nearest same-region surface proxy; no reviewed Antonia landmark sidecar exists, so this cannot satisfy the landmark-correspondence acceptance gate","rmsMm":math.sqrt(sum(x*x for x in lvals)/len(lvals)) if lvals else None,"maxMm":max(lvals) if lvals else None,"count":len(lvals),"samples":landmark_samples},
      "surface":{"bidirectionalAreaWeighted":bidirectional,"canonicalToReference":weighted_stats(forward),"referenceToCanonical":weighted_stats(reverse),"coverage":covered/len(current),"unsupportedRegions":["canonical eyeballs","mouth interior"]},
      "integrity":{"newlyInvertedFaces":inverted,"degenerateFaces":degenerate,"selfIntersections":"not-implemented-x0","edgeStretch":{"p95Ratio":percentile(stretches,.95),"maxRatio":max(stretches)},"symmetryResidual":{"rmsMm":math.sqrt(sum(x*x for x in symmetry)/len(symmetry)),"maxMm":max(symmetry)}},
      "regionalReview":regional,
      "budgets":{"landmarkRmsPass":False,"landmarkMaxPass":False,"surfaceRmsPass":bidirectional["rmsMm"]<=config["validation"]["surfaceRmsBudgetMm"],"surfaceP95Pass":bidirectional["p95Mm"]<=config["validation"]["surfaceP95BudgetMm"],"integrityPass":inverted==0 and degenerate==0 and max(stretches)<=config["validation"]["maximumEdgeStretchRatio"]},
      "output":{"positionsSha256":output_hash,"status":"local-research-evidence-only"},
      "limitations":["One-reference foundation only; no diversity, mean-body, or PCA claim.","Self-intersection detector is not qualified in X0.","Source component conversion scope blocks canonical admission of fitted positions.","Regional review statistics are screening proxies and require visual human review."]}
    result["screeningVerdict"]="pass" if all(result["budgets"].values()) else "fail"
    (out/"fit-evidence.json").write_text(json.dumps(result,indent=2)+"\n")
    write_obj(out/"antonia-fitted-canonical.obj",current,faces,face_regions)
    provenance={"schema":"aetheris.humanoid.fit-provenance.v1","canonicalArtifactSha256":sha256(artifact),"referenceSha256":sha256(blend),"sidecarSha256":sha256(sidecar_path),"configSha256":sha256(config_path),"positionsSha256":output_hash,"connectivityHash":expected_connectivity,"attribution":sidecar["attribution"],"admissionStatus":sidecar["canonicalOutputAdmission"],"transformations":["source outer-skin component admission","sole-and-height normalization to 1750 mm","canonical linear-blend-skinning pose alignment","five deterministic same-region projection and regularization iterations","stored symmetry-pair displacement averaging","pair-local inversion backtracking"]}
    (out/"fit-provenance.json").write_text(json.dumps(provenance,indent=2)+"\n")
    render_views(out,source,admitted_faces,pre,current,faces,errors,landmark_points)
    print(json.dumps({"success":True,"screeningVerdict":result["screeningVerdict"],"fitEvidence":str(out/"fit-evidence.json"),"connectivityUnchanged":True,"canonicalOutputAdmission":sidecar["canonicalOutputAdmission"]}))


if __name__ == "__main__": main()
