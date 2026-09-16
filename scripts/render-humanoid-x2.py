"""Local X2 diagnostic renders and read-only FBX reference inspection; Blender is not runtime authority."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def setup():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.world.color = (.035, .035, .035)
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    data = bpy.data.cameras.new('Diagnostic camera')
    camera = bpy.data.objects.new('Diagnostic camera', data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.type = 'ORTHO'
    data.ortho_scale = 2.05
    data.clip_end = 10000
    camera.location = (3, 5, 2.3)
    camera.rotation_euler = (Vector((0, 0, .9)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    return scene


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    return mat


def obj_mesh(path, shift=0):
    vertices, faces = [], []
    for line in path.read_text().splitlines():
        f = line.split()
        if f and f[0] == 'v':
            vertices.append((float(f[1]) / 1000 + shift, float(f[2]) / 1000, float(f[3]) / 1000))
        elif f and f[0] == 'f':
            faces.append(tuple(int(v) - 1 for v in f[1:]))
    mesh = bpy.data.meshes.new(path.stem)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(path.stem, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(material('Surface', (.58, .63, .68)))
    return obj


def mechanism(path):
    data = json.loads(path.read_text())
    joints = data['joints']
    positions = [Vector(tuple(j['position'][k] / 1000 for k in ('x', 'y', 'z'))) for j in joints]
    mat = material('Links', (.95, .45, .08))
    for i, joint in enumerate(joints):
        parent = joint['parentIndex']
        if parent is None:
            continue
        a, b = positions[parent], positions[i]
        length = (b - a).length
        if length < .00001:
            continue
        bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=.006, depth=length, location=(a + b) / 2)
        link = bpy.context.object
        link.rotation_euler = (b - a).to_track_quat('Z', 'Y').to_euler()
        link.data.materials.append(mat)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=.012, location=b)
        bpy.context.object.data.materials.append(mat)


def render(scene, path):
    scene.render.filepath = str(path.resolve())
    bpy.ops.render.render(write_still=True)


def inspect_fbx(path, out):
    raw = path.read_bytes()
    if raw.startswith(b'; FBX'):
        # Declaration inventory only. Do not interpret FBX transform conventions with a homegrown importer.
        text = raw.decode('utf-8')
        models = re.findall(r'^\s*Model:\s*(\d+),\s*"Model::([^"]+)",\s*"([^"]+)"', text, re.M)
        names = {identifier: name for identifier, name, _ in models}
        parents = {child: parent for child, parent in re.findall(r'^\s*C:\s*"OO",\s*(\d+),\s*(\d+)\s*$', text, re.M)
                   if child in names and parent in names}
        evidence = {'file': path.name, 'sha256': hashlib.sha256(raw).hexdigest(),
                    'format': 'ASCII FBX 7.7', 'status': 'DeclarationInventoryOnly',
                    'limitation': 'Installed Blender FBX importer rejects ASCII. No pose, weight or deformation interpretation claimed.',
                    'modelDeclarations': [{'name': name, 'type': kind, 'parent': names.get(parents.get(identifier))}
                                          for identifier, name, kind in models],
                    'geometryDeclarations': len(re.findall(r'^\s*Geometry:\s*\d+,', text, re.M)),
                    'clusterDeclarations': len(re.findall(r'^\s*Deformer:\s*\d+,\s*"[^"]*",\s*"Cluster"', text, re.M)),
                    'animationCurveDeclarations': len(re.findall(r'^\s*AnimationCurve:\s*\d+,', text, re.M))}
        (out / (path.stem + '.reference.json')).write_text(json.dumps(evidence, indent=2))
        return {k: v for k, v in evidence.items() if k != 'modelDeclarations'} | {'limbNodes': sum(kind == 'LimbNode' for _, _, kind in models)}
    scene = setup()
    bpy.ops.import_scene.fbx(filepath=str(path.resolve()), use_anim=True)
    armatures = [o for o in scene.objects if o.type == 'ARMATURE']
    meshes = [o for o in scene.objects if o.type == 'MESH']
    evidence = {'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                'armatures': [], 'meshes': [], 'frameStart': scene.frame_start, 'frameEnd': scene.frame_end}
    for arm in armatures:
        evidence['armatures'].append({'name': arm.name, 'bones': [
            {'name': b.name, 'parent': b.parent.name if b.parent else None,
             'headLocal': list(b.head_local), 'tailLocal': list(b.tail_local), 'lengthLocal': b.length}
            for b in arm.data.bones]})
    for mesh in meshes:
        evidence['meshes'].append({'name': mesh.name, 'vertices': len(mesh.data.vertices),
                                  'polygons': len(mesh.data.polygons), 'weightGroups': [g.name for g in mesh.vertex_groups],
                                  'unweightedVertices': sum(not v.groups for v in mesh.data.vertices)})
    evidence['samples'] = []
    for frame in sorted(set([scene.frame_start, (scene.frame_start + scene.frame_end) // 2, scene.frame_end])):
        scene.frame_set(frame)
        evidence['samples'].append({'frame': frame, 'armatures': [
            {'name': arm.name, 'bones': [{'name': b.name, 'headWorld': list(arm.matrix_world @ b.head),
                                        'tailWorld': list(arm.matrix_world @ b.tail)} for b in arm.pose.bones]}
            for arm in armatures]})
    (out / (path.stem + '.reference.json')).write_text(json.dumps(evidence, indent=2))
    if meshes:
        scene.frame_set(scene.frame_start)
        points = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
        hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
        center = (lo + hi) / 2
        span = max(hi - lo)
        scene.camera.data.ortho_scale = span * 1.3
        scene.camera.location = center + Vector((span * 2, -span * 3, span * .7))
        scene.camera.rotation_euler = (center - scene.camera.location).to_track_quat('-Z', 'Y').to_euler()
        render(scene, out / (path.stem + '.reference.png'))
    return {'file': path.name, 'sha256': evidence['sha256'], 'armatures': len(armatures),
            'bones': sum(len(a.data.bones) for a in armatures), 'meshes': len(meshes),
            'vertices': sum(len(o.data.vertices) for o in meshes), 'frameStart': scene.frame_start, 'frameEnd': scene.frame_end}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out-dir', default='artifacts/local/humanoid-x2')
    parser.add_argument('--x1-dir', default='artifacts/local/humanoid-x1')
    parser.add_argument('--fbx', action='append', default=[])
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    out = Path(args.out_dir)
    evidence = json.loads((out / 'evidence.json').read_text())
    for case in evidence['cases']:
        if not case.get('isSolved') or case['name'] not in ('neutral', 'hip-flexion-45', 'hip-flexion-70', 'hip-flexion-90', 'hip-abduction-45', 'elbow-flexion-90', 'knee-flexion-90', 'hip-knee-90'):
            continue
        scene = setup()
        obj_mesh(out / case['diagnosticObj'])
        render(scene, out / (case['name'] + '.png'))
        if case['name'] in ('hip-flexion-90', 'hip-knee-90'):
            scene = setup()
            mechanism(out / (case['name'] + '.mechanism.json'))
            render(scene, out / (case['name'] + '.mechanism.png'))
    # Same angle / unchanged shape and skin: a causal witness, not a claimed improvement.
    scene = setup()
    obj_mesh(Path(args.x1_dir) / 'hip-flexion-70.obj', -.55)
    obj_mesh(out / 'hip-flexion-70.failed-diagnostic.obj', .55)
    scene.camera.location = (0, 6, 1.4)
    scene.camera.rotation_euler = (Vector((0, 0, .9)) - scene.camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera.data.ortho_scale = 2.4
    render(scene, out / 'x1-left-x2-right-hip70.png')
    references = [inspect_fbx(Path(p), out) for p in args.fbx]
    (out / 'fbx-summary.json').write_text(json.dumps(references, indent=2))


if __name__ == '__main__':
    main()
