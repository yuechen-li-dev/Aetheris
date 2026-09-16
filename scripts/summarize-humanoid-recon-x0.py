"""Compact source-only research summaries; outputs are ignored local evidence."""
import hashlib
import json
import runpy
import struct
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/local/humanoid-recon-x0'


def names(array):
    return array.tobytes().decode('utf-8').rstrip('\0').split('\0')


def main():
    runpy.run_path(str(ROOT / 'scripts/fetch-humanoid-recon-x0.py'))['verify_inputs']()
    inspection = json.loads((OUT / 'inspection.json').read_text())
    summary = {}
    for name, data in inspection['candidates'].items():
        folder = OUT / 'CharMorph-db/characters' / name
        item = dict(mesh=data['meshes'][0], rigs=[])
        item['mesh'].pop('vertex_groups')
        item['mesh'].pop('shape_keys')
        for rig in data['rigs']:
            item['rigs'].append(dict(name=rig['name'], count=len(rig['bones']),
                roots=[b['name'] for b in rig['bones'] if b['parent'] is None],
                sample_bones=[b for b in rig['bones'] if b['name'] in
                    ('upper_arm.L', 'forearm.L', 'hand.L', 'thigh.L', 'shin.L', 'foot.L', 'toe.L', 'spine', 'spine.006')]))
        item['morphs'] = {}
        for level in ('L2', 'L3'):
            pack = folder / ('morphs/' + level + '.npz')
            if pack.exists():
                with np.load(pack, allow_pickle=False) as z:
                    n = names(z['names'])
                item['morphs'][level] = dict(count=len(n), examples=n[:12], storage='packed')
            else:
                files = sorted((folder / 'morphs' / level).glob('*.np*'))
                n = [p.stem for p in files]
                item['morphs'][level] = dict(count=len(n), examples=n[:12], storage='individual dense/sparse',
                    dense_count=sum(p.suffix == '.npy' for p in files), sparse_count=sum(p.suffix == '.npz' for p in files))
        item['groups'] = {}
        for relative in ('weights/rigify.npz', 'joints/rigify.npz'):
            with np.load(folder / relative, allow_pickle=False) as z:
                n = names(z['names'])
                sums = np.zeros(item['mesh']['vertices'])
                np.add.at(sums, z['idx'], z['weights'])
                item['groups'][relative] = dict(count=len(n), entries=len(z['idx']),
                    index_min=int(z['idx'].min()), index_max=int(z['idx'].max()),
                    weights_finite=bool(np.isfinite(z['weights']).all()),
                    weight_min=float(z['weights'].min()), weight_max=float(z['weights'].max()),
                    per_vertex_sum_min=float(sums.min()), per_vertex_sum_max=float(sums.max()),
                    unweighted_vertices=int((sums == 0).sum()), examples=n[:10])
        summary[name] = item
    (OUT / 'candidate-summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')

    samples = []
    for file in sorted((OUT / 'vrm-specification/samples').rglob('*.vrm')):
        raw = file.read_bytes()
        magic, version, length = struct.unpack_from('<4sII', raw)
        assert magic == b'glTF' and version == 2 and length == len(raw)
        size, kind = struct.unpack_from('<II', raw, 12)
        assert kind == 0x4E4F534A
        gltf = json.loads(raw[20:20 + size])
        vrm = gltf.get('extensions', {}).get('VRMC_vrm')
        if not vrm:
            continue
        springs = gltf.get('extensions', {}).get('VRMC_springBone', {})
        samples.append(dict(path=file.relative_to(OUT / 'vrm-specification').as_posix(),
            sha256=hashlib.sha256(raw).hexdigest(), generator=gltf.get('asset', {}).get('generator'),
            meta=vrm['meta'], version=vrm['specVersion'], extensions=gltf.get('extensionsUsed', []),
            human_bones=len(vrm['humanoid']['humanBones']), meshes=len(gltf.get('meshes', [])),
            primitives=sum(len(m['primitives']) for m in gltf.get('meshes', [])),
            primitive_morph_target_counts=[len(p.get('targets', [])) for m in gltf.get('meshes', []) for p in m['primitives']],
            skins=len(gltf.get('skins', [])), skin_joint_counts=[len(s['joints']) for s in gltf.get('skins', [])],
            materials=len(gltf.get('materials', [])),
            expressions={k:list(v) for k,v in vrm.get('expressions', {}).items()},
            look_at=vrm.get('lookAt'), first_person=vrm.get('firstPerson'),
            springs=len(springs.get('springs', [])), colliders=len(springs.get('colliders', [])),
            collider_groups=len(springs.get('colliderGroups', [])),
            constrained_nodes=sum('VRMC_node_constraint' in n.get('extensions', {}) for n in gltf.get('nodes', []))))
    (OUT / 'vrm-summary.json').write_text(json.dumps(samples, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
