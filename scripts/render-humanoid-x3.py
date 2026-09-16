"""Matched X3 diagnostic views. A failed candidate is never labeled as a winner."""
import argparse
import json
import math
import runpy
from pathlib import Path
import sys

import bpy
from mathutils import Vector
helpers = runpy.run_path(str(Path(__file__).with_name('render-humanoid-x2.py')))
setup, obj_mesh, render, material = (helpers[n] for n in ('setup', 'obj_mesh', 'render', 'material'))


def points(path):
    return [Vector(tuple(float(x) for x in line.split()[1:4]))
            for line in path.read_text().splitlines() if line.startswith('v ')]


def heatmap(obj, rest, posed):
    # Blue = compression; red = stretch; grey = near-preserved. Saturates at policy 4x.
    obj.data.materials.clear()
    for bucket in range(33):
        t = (bucket - 16) / 16
        color = (.65 + .3 * t, .65 * (1 - t), .65 * (1 - t)) if t >= 0 else (.65 * (1 + t), .65 * (1 + t), .65 - .3 * t)
        obj.data.materials.append(material('signed-log-edge-' + str(bucket), color))
    for face in obj.data.polygons:
        ids = list(face.vertices)
        signed = []
        for a, b in zip(ids, ids[1:] + ids[:1]):
            before = (rest[a] - rest[b]).length
            after = (posed[a] - posed[b]).length
            signed.append(math.log(max(after / before, 1e-12)) if before else 0)
        value = max(signed, key=abs) / math.log(4)
        face.material_index = round(16 + 16 * min(1, max(-1, value)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out-dir', default='artifacts/local/humanoid-x3')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    out = Path(args.out_dir)
    evidence = json.loads((out / 'evidence.json').read_text())
    rest = points(out / 'hip-flexion-0.lbs.baseline.diagnostic.obj')
    rendered = []
    for case in evidence['cases']:
        name = case['name']
        if name not in ('hip-flexion-45','hip-flexion-70','hip-flexion-90','hip-abduction-30','hip-abduction-45'):
            continue
        selected = case['winnerId'] if case['isSuccess'] else 'lbs.weights-softened'
        role = 'winner' if case['isSuccess'] else 'REJECTED-softened-candidate'
        baseline_path = out / (name + '.lbs.baseline.diagnostic.obj')
        candidate_path = out / (name + '.' + selected + '.diagnostic.obj')
        for is_heatmap in (False, True):
            scene = setup()
            a = obj_mesh(baseline_path, -.55)
            b = obj_mesh(candidate_path, .55)
            if is_heatmap:
                heatmap(a, rest, points(baseline_path))
                heatmap(b, rest, points(candidate_path))
                scene.display.shading.show_cavity = False
            scene.camera.data.ortho_scale = 2.4
            for view, eye in [('front', (0,6,1.4)), ('back', (0,-6,1.4))]:
                scene.camera.location = eye
                scene.camera.rotation_euler = (Vector((0,0,.9))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
                filename = name + '.' + role + '.' + ('heatmap-' if is_heatmap else '') + view + '.png'
                render(scene, out / filename)
                rendered.append({'file':filename,'left':'X1 LBS baseline','right':selected,'rightStatus':role,
                                 'sourceBaseline':'Unavailable; no source deformation was sampled',
                                 'heatmap':'Blue compression, red stretch, grey near-preserved; log ratio saturation at 4x' if is_heatmap else None})
    (out / 'render-manifest.json').write_text(json.dumps(rendered,indent=2))


if __name__ == '__main__':
    main()
