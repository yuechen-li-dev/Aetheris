"""Render the X1 candidate and failed poses. Blender is evidence tooling only."""
import argparse
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out-dir', default='artifacts/local/humanoid-x1')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    out = Path(args.out_dir).resolve()
    candidate = json.loads((out / 'antonia-adoption-candidate.json').read_text())
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.single_color = (.58, .63, .68)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.shading.background_type = 'WORLD'
    scene.world.color = (.035, .035, .035)
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    camera_data = bpy.data.cameras.new('Evidence camera')
    camera = bpy.data.objects.new('Evidence camera', camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = 'ORTHO'
    camera_data.clip_end = 100

    def load(name):
        for obj in list(scene.objects):
            if obj.type == 'MESH':
                bpy.data.objects.remove(obj, do_unlink=True)
        vertices, faces = [], []
        for line in (out / name).read_text().splitlines():
            fields = line.split()
            if not fields:
                continue
            if fields[0] == 'v':
                vertices.append(tuple(float(v) / 1000 for v in fields[1:4]))
            elif fields[0] == 'f':
                faces.append(tuple(int(v) - 1 for v in fields[1:]))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        for label, color in [('skin', (.58, .63, .68, 1)), ('wire', (.025, .025, .025, 1))]:
            mat = bpy.data.materials.new(label)
            mat.diffuse_color = color
            mesh.materials.append(mat)
        modifier = obj.modifiers.new('Binding triangles', 'WIREFRAME')
        modifier.thickness = .00035
        modifier.use_even_offset = False
        modifier.use_replace = False
        modifier.material_offset = 1
        modifier.show_render = False
        # Flat shading retains geometric defects instead of concealing them.
        return obj

    def render(name, eye, target=(0, 0, .875), scale=2.05, wire=False):
        camera.location = eye
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera_data.ortho_scale = scale
        for obj in scene.objects:
            if obj.type == 'MESH':
                obj.modifiers['Binding triangles'].show_render = wire
        scene.render.filepath = str(out / (name + '.png'))
        bpy.ops.render.render(write_still=True)

    load('source-pose.obj')
    render('source-front', (0, 5, .875))
    load('prepared-apose.obj')
    for name, eye in [('front', (0, 5, .875)), ('back', (0, -5, .875)),
                      ('side', (5, 0, .875)), ('iso', (3, 4, 2.6))]:
        render('neutral-' + name, eye)
    render('wireframe-front', (0, 5, .875), wire=True)
    hand_points = [p for p, v in zip(candidate['preparedPositions'], candidate['sourcePoseSurface']['vertices'])
                   if v['region'] in ('LeftHand', 'LeftThumb', 'LeftIndex', 'LeftMiddle', 'LeftRing', 'LeftLittle')]
    hand_target = tuple((min(p[k] for p in hand_points) + max(p[k] for p in hand_points)) / 2000 for k in ('x', 'y', 'z'))
    for name, target, scale in [('face', (0, .02, 1.61), .36),
                                ('shoulder', (-.25, 0, 1.39), .48),
                                ('pelvis', (0, 0, .96), .60),
                                ('hand', hand_target, .30),
                                ('feet', (0, .04, .08), .62)]:
        render('wireframe-' + name, (target[0], 5, target[2]), target, scale, True)
    for name in candidate['poseSweep']:
        if not name.endswith('-70'):
            continue
        load(name + '.obj')
        render('pose-' + name, (3, 4, 2.6))
    print('X1 visual evidence rendered; human review remains required.')


if __name__ == '__main__':
    main()
