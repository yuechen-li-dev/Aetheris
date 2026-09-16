"""Research-only Blender inspection; never imports or executes CharMorph code.

blender --background --factory-startup --disable-autoexec --python scripts/inspect-humanoid-recon-x0.py
Inputs are the pinned, locally fetched candidate packages described in the release manifest.
Outputs remain under ignored artifacts/local/humanoid-recon-x0.
"""
import collections
import hashlib
import json
import runpy
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector, kdtree

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/local/humanoid-recon-x0'
DB = OUT / 'CharMorph-db/characters'


def inspect_mesh(obj):
    mesh = obj.data
    p = np.array([v.co[:] for v in mesh.vertices], dtype=float)
    edges = collections.Counter()
    for f in mesh.polygons:
        for a, b in zip(f.vertices, list(f.vertices[1:]) + [f.vertices[0]]):
            edges[tuple(sorted((a, b)))] += 1
    tree = kdtree.KDTree(len(p))
    for i, v in enumerate(p):
        tree.insert(v, i)
    tree.balance()
    errors = [tree.find((-v[0], v[1], v[2]))[2] for v in p]
    height = float(np.ptp(p[:, 2]))
    # Scale-normalized diagnostic, not a claim about original physical stature.
    scale = 1750 / height
    return dict(name=obj.name, vertices=len(p), faces=len(mesh.polygons),
                polygon_sizes=dict(collections.Counter(len(f.vertices) for f in mesh.polygons)),
                boundary_edges=sum(n == 1 for n in edges.values()),
                nonmanifold_edges_gt2=sum(n > 2 for n in edges.values()),
                zero_area_faces=sum(f.area < 1e-12 for f in mesh.polygons),
                finite=bool(np.isfinite(p).all()), bounds=[p.min(0).tolist(), p.max(0).tolist()],
                normalized_height_mm=1750,
                reflected_nearest_vertex_rms_mm=float(np.sqrt(np.mean(np.square(errors))) * scale),
                reflected_nearest_vertex_max_mm=float(max(errors) * scale),
                uv_layers=[v.name for v in mesh.uv_layers],
                materials=[m.name if m else None for m in mesh.materials],
                vertex_groups=[g.name for g in obj.vertex_groups],
                shape_keys=[k.name for k in mesh.shape_keys.key_blocks] if mesh.shape_keys else [],
                modifiers=[dict(name=m.name, type=m.type) for m in obj.modifiers])


def render_body(obj, path):
    # Neutral gray surface, without third-party textures or shared materials.
    scene = bpy.context.scene
    for other in list(scene.objects):
        if other != obj:
            bpy.data.objects.remove(other, do_unlink=True)
    obj.data.materials.clear()
    for mod in list(obj.modifiers):
        obj.modifiers.remove(mod)
    for f in obj.data.polygons:
        f.use_smooth = True
    bounds = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
    center = sum(bounds, Vector()) / 8
    height = max(v.z for v in bounds) - min(v.z for v in bounds)
    camera = bpy.data.objects.new('ResearchCamera', bpy.data.cameras.new('ResearchCamera'))
    scene.collection.objects.link(camera)
    camera.location = center + Vector((0, -height * 2, 0))
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = height * 1.12
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (0.55, 0.58, 0.62)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def main():
    runpy.run_path(str(ROOT / 'scripts/fetch-humanoid-recon-x0.py'))['verify_inputs']()
    manifest = json.loads((ROOT / 'docs/release/HUMANOID-RECON-X0.manifest.json').read_text(encoding='utf-8'))
    result = dict(blender=bpy.app.version_string, candidates={})
    for name in ('antonia', 'reom'):
        folder = DB / name
        candidate = dict(files={}, meshes=[], rigs=[], arrays={})
        approved_inspection_paths = next(a['filesSha256'] for a in manifest['assets'] if a['id'] == name)
        for relative in sorted(approved_inspection_paths):
            path = folder / relative
            candidate['files'][relative] = hashlib.sha256(path.read_bytes()).hexdigest()
        for file in ('char.blend', 'metarig.blend', 'rigs.blend'):
            path = folder / file
            if not path.exists():
                continue
            bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False, use_scripts=False)
            for obj in list(bpy.data.objects):
                if obj.type == 'MESH' and file == 'char.blend' and obj.name == 'cm_' + name:
                    candidate['meshes'].append(inspect_mesh(obj))
                if obj.type == 'ARMATURE':
                    candidate['rigs'].append(dict(file=file, name=obj.name, bones=[
                        dict(name=b.name, parent=b.parent.name if b.parent else None,
                             head=list(b.head_local), tail=list(b.tail_local), deform=b.use_deform)
                        for b in obj.data.bones]))
            if file == 'char.blend':
                bodies = [o for o in bpy.context.scene.objects if o.type == 'MESH']
                if bodies:
                    render_body(max(bodies, key=lambda o: len(o.data.vertices)), OUT / (name + '-neutral.png'))
        for path in sorted(folder / p for p in approved_inspection_paths if Path(p).suffix in ('.npy', '.npz')):
            data = np.load(path, allow_pickle=False)
            key = path.relative_to(folder).as_posix()
            if isinstance(data, np.ndarray):
                candidate['arrays'][key] = dict(shape=list(data.shape), dtype=str(data.dtype))
            else:
                candidate['arrays'][key] = {k: dict(shape=list(data[k].shape), dtype=str(data[k].dtype)) for k in data.files}
                if 'names' in data.files:
                    candidate['arrays'][key]['names_value'] = data['names'].tolist()
                data.close()
        result['candidates'][name] = candidate
    (OUT / 'inspection.json').write_text(json.dumps(result, indent=2,
        default=lambda value: value.decode('utf-8') if isinstance(value, bytes) else str(value)), encoding='utf-8')


if __name__ == '__main__':
    main()
