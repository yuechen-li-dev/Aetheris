from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import platform
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

import gpytoolbox as gpy
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
from scipy.spatial import cKDTree

SEED = 20260917
EPS = 1e-12
REVISION = 'fa1962cd5825cfc9bb698569714f579e1d75a4e8'


@dataclass(frozen=True)
class Mesh:
    v: np.ndarray
    f: np.ndarray


@dataclass(frozen=True)
class Fixture:
    name: str
    sdf: Callable[[np.ndarray], np.ndarray] | None


def read_obj(path: Path) -> Mesh:
    vertices: list[list[float]] = []
    triangles: list[list[int]] = []
    with path.open('r', encoding='utf-8') as stream:
        for raw in stream:
            parts = raw.strip().split()
            if not parts: continue
            if parts[0] == 'v': vertices.append([float(x) for x in parts[1:4]])
            elif parts[0] == 'f':
                polygon = [int(token.split('/')[0]) - 1 for token in parts[1:]]
                for i in range(1, len(polygon)-1): triangles.append([polygon[0], polygon[i], polygon[i+1]])
    return Mesh(np.asarray(vertices, dtype=np.float64), np.asarray(triangles, dtype=np.int64))


def write_obj(path: Path, v: np.ndarray, f: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('w', encoding='utf-8', newline='\n') as stream:
        for p in v:
            stream.write(f'v {p[0]:.17g} {p[1]:.17g} {p[2]:.17g}\n')
        for face in f:
            stream.write('f ' + ' '.join(str(int(x) + 1) for x in face) + '\n')


def box_sdf(size: tuple[float, float, float]) -> Callable[[np.ndarray], np.ndarray]:
    half = np.asarray(size) * .5
    center = np.array([0., 0., half[2]])
    def evaluate(q: np.ndarray) -> np.ndarray:
        d = np.abs(q - center) - half
        return np.linalg.norm(np.maximum(d, 0.), axis=1) + np.minimum(np.max(d, axis=1), 0.)
    return evaluate


def triangular_prism_sdf(vertices: tuple[tuple[float, float], ...], z_min: float,
                         z_max: float) -> Callable[[np.ndarray], np.ndarray]:
    """Exact SDF for a CCW triangle extruded through a closed z interval."""
    polygon = np.asarray(vertices, dtype=np.float64)
    following = np.roll(polygon, -1, axis=0)
    edges = following - polygon
    z_center = (z_min + z_max) * .5
    z_half = (z_max - z_min) * .5

    def evaluate(q: np.ndarray) -> np.ndarray:
        planar = q[:, :2]
        offsets = planar[:, None, :] - polygon[None, :, :]
        edge_length_squared = np.sum(edges * edges, axis=1)
        t = np.clip(np.sum(offsets * edges[None, :, :], axis=2) / edge_length_squared, 0., 1.)
        closest = offsets - t[:, :, None] * edges[None, :, :]
        distance_2d = np.sqrt(np.min(np.sum(closest * closest, axis=2), axis=1))
        cross = edges[None, :, 0] * offsets[:, :, 1] - edges[None, :, 1] * offsets[:, :, 0]
        signed_2d = np.where(np.all(cross >= 0., axis=1), -distance_2d, distance_2d)
        d = np.column_stack((signed_2d, np.abs(q[:, 2] - z_center) - z_half))
        return np.linalg.norm(np.maximum(d, 0.), axis=1) + np.minimum(np.max(d, axis=1), 0.)

    return evaluate


def sphere_sdf(q: np.ndarray) -> np.ndarray:
    return np.linalg.norm(q, axis=1) - 7.5


def torus_sdf(q: np.ndarray) -> np.ndarray:
    radial = np.linalg.norm(q[:, [0, 2]], axis=1) - 12.
    return np.sqrt(radial * radial + q[:, 1] * q[:, 1]) - 3.


def cylinder_sdf(q: np.ndarray) -> np.ndarray:
    d = np.column_stack((np.linalg.norm(q[:, :2], axis=1) - 12., np.abs(q[:, 2] - 15.) - 15.))
    return np.linalg.norm(np.maximum(d, 0.), axis=1) + np.minimum(np.max(d, axis=1), 0.)


FIXTURES = {
    'sphere': Fixture('sphere', sphere_sdf),
    'cylinder': Fixture('cylinder', cylinder_sdf),
    'torus': Fixture('torus', torus_sdf),
    'sharp_box': Fixture('sharp_box', box_sdf((80., 50., 25.))),
    'sharp_wedge_prism': Fixture('sharp_wedge_prism', triangular_prism_sdf(
        ((-30., -20.), (30., -20.), (-30., 20.)), 0., 20.)),
    'thin_plate': Fixture('thin_plate', box_sdf((40., 30., .5))),
    'filleted_box': Fixture('filleted_box', None),
    'rounded_rectangle_prism': Fixture('rounded_rectangle_prism', None),
    'chamfered_box': Fixture('chamfered_box', None),
    'through_hole': Fixture('through_hole', None),
    'counterbore': Fixture('counterbore', None),
    'phone_like_chassis': Fixture('phone_like_chassis', None),
    'bent_sheet': Fixture('bent_sheet', None),
}


def rotation() -> np.ndarray:
    rng = np.random.default_rng(919993)
    axis = rng.random(3); axis /= np.linalg.norm(axis)
    angle = float(rng.random() * 2 * np.pi)
    k = np.array([[0., -axis[2], axis[1]], [axis[2], 0., -axis[0]], [-axis[1], axis[0], 0.]])
    return np.eye(3) + math.sin(angle) * k + (1 - math.cos(angle)) * (k @ k)


def author_rotation() -> np.ndarray:
    # run_ours.py uses NumPy's legacy global RandomState API.
    rng = np.random.RandomState(919993)
    axis = rng.rand(3); axis /= np.linalg.norm(axis)
    angle = float(rng.rand() * 2 * np.pi)
    k = np.array([[0., -axis[2], axis[1]], [axis[2], 0., -axis[0]], [-axis[1], axis[0], 0.]])
    return np.eye(3) + math.sin(angle) * k + (1 - math.cos(angle)) * (k @ k)


def triangulate(f: np.ndarray) -> np.ndarray:
    if f.size == 0 or f.shape[1] == 3: return f.astype(np.int64)
    return np.vstack((f[:, [0, 1, 2]], f[:, [0, 2, 3]])).astype(np.int64)


def surface_sample(mesh: Mesh, count: int, seed: int) -> np.ndarray:
    tri = mesh.v[mesh.f]
    area = np.linalg.norm(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]), axis=1)
    rng = np.random.default_rng(seed)
    idx = rng.choice(len(tri), count, p=area / np.sum(area))
    uv = rng.random((count, 2)); flip = uv.sum(axis=1) > 1; uv[flip] = 1 - uv[flip]
    bary = np.column_stack((1 - uv[:, 0] - uv[:, 1], uv[:, 0], uv[:, 1]))
    return np.sum(tri[idx] * bary[:, :, None], axis=1)


def topology(v: np.ndarray, f: np.ndarray) -> dict:
    if len(f) == 0: return {'boundary_edges': 0, 'nonmanifold_edges': 0, 'components': 0, 'euler': 0}
    edges = np.sort(np.vstack((f[:, [0,1]], f[:, [1,2]], f[:, [2,0]])), axis=1)
    unique, counts = np.unique(edges, axis=0, return_counts=True)
    parent = np.arange(len(v))
    def find(x: int) -> int:
        while parent[x] != x: parent[x] = parent[parent[x]]; x = parent[x]
        return x
    for a, b in unique:
        ra, rb = find(int(a)), find(int(b))
        if ra != rb: parent[rb] = ra
    used = np.unique(f)
    return {'boundary_edges': int(np.sum(counts == 1)), 'nonmanifold_edges': int(np.sum(counts > 2)),
            'components': len({find(int(x)) for x in used}), 'euler': int(len(used) - len(unique) + len(f))}


def quality(v: np.ndarray, f: np.ndarray) -> dict:
    if len(f) == 0: return {'min_angle_deg': math.nan, 'p99_aspect': math.nan, 'sliver_rate': math.nan}
    tri = v[f]; e = np.stack((tri[:,1]-tri[:,0], tri[:,2]-tri[:,1], tri[:,0]-tri[:,2]), axis=1)
    lengths = np.linalg.norm(e, axis=2)
    area2 = np.linalg.norm(np.cross(e[:,0], -e[:,2]), axis=1)
    aspect = np.max(lengths, axis=1) ** 2 / np.maximum(area2, EPS)
    a = np.degrees(np.arccos(np.clip(np.sum((-e[:,2])*e[:,0], axis=1) / np.maximum(lengths[:,2]*lengths[:,0], EPS), -1, 1)))
    b = np.degrees(np.arccos(np.clip(np.sum((-e[:,0])*e[:,1], axis=1) / np.maximum(lengths[:,0]*lengths[:,1], EPS), -1, 1)))
    mins = np.minimum(np.minimum(a, b), 180-a-b)
    return {'min_angle_deg': float(np.min(mins)), 'p99_aspect': float(np.percentile(aspect, 99)), 'sliver_rate': float(np.mean(mins < 5.))}


def error_stats(values: np.ndarray) -> dict:
    values = np.abs(values[np.isfinite(values)])
    return {'rms_mm': float(np.sqrt(np.mean(values**2))), 'p50_mm': float(np.percentile(values,50)),
            'p95_mm': float(np.percentile(values,95)), 'p99_mm': float(np.percentile(values,99)),
            'max_mm': float(np.max(values))}


def exact_surface_error(fixture: Fixture, source: Mesh, p: np.ndarray, r: np.ndarray, center: np.ndarray) -> np.ndarray:
    local = (p - center) @ r.T + center
    if fixture.sdf is not None: return fixture.sdf(local)
    return np.asarray(gpy.signed_distance(p, source.v, source.f)[0])


def run_case(contouring, fixture: Fixture, source_local: Mesh, cells: int, method: str,
             noise_voxels: float = 0., quantize: str = 'none', outer_iters: int = 100,
             inner_iters: int = 100, rotation_matrix: np.ndarray | None = None) -> tuple[dict, Mesh, np.ndarray]:
    r = rotation() if rotation_matrix is None else rotation_matrix; center = (source_local.v.min(0) + source_local.v.max(0)) * .5
    scale_mm = float(np.max(source_local.v.max(0) - source_local.v.min(0)))
    # Match upstream run_ours.py: normalize the longest bbox dimension to 1,
    # then rotate to avoid grid bias. Fixed optimization weights live here.
    source_normalized = Mesh(((source_local.v - center) / scale_mm) @ r, source_local.f)
    source = Mesh(source_normalized.v * scale_mm + center, source_local.f)
    bounds_min, bounds_max = source_normalized.v.min(0), source_normalized.v.max(0)
    extent = float(np.max(bounds_max - bounds_min)); pad = max(extent * .08, 1e-3)
    lo = bounds_min - pad; hi = bounds_max + pad
    gv = contouring._contouring_cpp_module._grid(lo, hi, cells+1, cells+1, cells+1)
    h_normalized = float(np.max((hi-lo)/cells))
    h = h_normalized * scale_mm
    if fixture.sdf is not None:
        local_mm = (gv @ r.T) * scale_mm + center
        s = fixture.sdf(local_mm) / scale_mm
    else:
        s = np.asarray(gpy.signed_distance(gv, source_normalized.v, source_normalized.f)[0])
    if noise_voxels:
        s = s + np.random.default_rng(SEED).normal(0., noise_voxels*h_normalized, len(s))
    if quantize == 'float16': s = s.astype(np.float16).astype(np.float64)
    opts = {'method': getattr(contouring._contouring_cpp_module.ContouringMethod, method), 'verbose': False}
    if method == 'Ours':
        opts.update({'outer_iters':outer_iters, 'inner_iters':inner_iters, 'hermite_update':True,
                     'new_hermite_pos_weight':.2, 'new_face_pos_weight':.2,
                     'new_hermite_normal_weight':.2, 'mu':.1, 'dc_weight':.02,
                     'sphere_weight':1., 'svd_threshold':.01, 'batch_size':200000})
    print(f'  {method}: invoke ({len(gv)} samples, outer={outer_iters}, inner={inner_iters})', flush=True)
    start = time.perf_counter()
    v, faces = contouring.py_contouring(s, gv, cells+1, cells+1, cells+1, 0., opts, None, None)
    seconds = time.perf_counter() - start
    print(f'  {method}: returned in {seconds:.3f}s', flush=True)
    f = triangulate(np.asarray(faces)); v = np.asarray(v) * scale_mm + center
    reconstructed = Mesh(v, f)
    if len(f):
        sample_count = min(12000, max(4000, len(f)*3))
        p = surface_sample(reconstructed, sample_count, SEED)
        exact_samples = surface_sample(source, min(20000, max(8000, len(source.f)*4)), SEED+1)
        if fixture.sdf is not None:
            forward = exact_surface_error(fixture, source, p, r, center)
        else:
            # Qualified mesh fixtures use a deterministic dense-surface proxy;
            # analytic fixtures above retain exact distance authority.
            forward = cKDTree(exact_samples).query(p, workers=-1)[0]
        reconstruction_samples = surface_sample(reconstructed, min(20000, max(8000, len(f)*4)), SEED+2)
        reverse = cKDTree(reconstruction_samples).query(exact_samples, workers=-1)[0]
        metrics = error_stats(forward)
        metrics['symmetric_rms_mm'] = float(np.sqrt(np.mean(np.concatenate((np.abs(forward), reverse))**2)))
        metrics['approx_hausdorff_mm'] = float(max(np.max(np.abs(forward)), np.max(reverse)))
    else:
        metrics = {k: math.nan for k in ('rms_mm','p50_mm','p95_mm','p99_mm','max_mm','symmetric_rms_mm','approx_hausdorff_mm')}
    record = {'fixture':fixture.name, 'method':method, 'cells':cells, 'grid_nodes':int(len(gv)),
              'voxel_mm':h, 'noise_voxels':noise_voxels, 'quantize':quantize, 'seconds':seconds,
              'outer_iters':outer_iters if method == 'Ours' else 0,
              'inner_iters':inner_iters if method == 'Ours' else 0,
              'vertices':int(len(v)), 'triangles':int(len(f)), **metrics, **topology(v,f), **quality(v,f)}
    return record, reconstructed, s


def visual(path: Path, source: Mesh, meshes: list[tuple[str,Mesh]], fixture: Fixture, r: np.ndarray | None = None) -> None:
    fig, axes = plt.subplots(1, len(meshes)+1, figsize=(4*(len(meshes)+1),4), subplot_kw={'projection':'3d'})
    all_items = [('Exact Aetheris', source)] + meshes
    for ax, (title, mesh) in zip(axes, all_items):
        f = mesh.f
        if len(f) > 12000: f = f[::max(1,len(f)//12000)]
        ax.plot_trisurf(mesh.v[:,0], mesh.v[:,1], mesh.v[:,2], triangles=f, linewidth=.05, color='#75aadb', edgecolor='#29445c')
        ax.set_title(title); ax.set_axis_off(); ax.view_init(25, 35); ax.set_box_aspect((1,1,.8))
    fig.tight_layout(); fig.savefig(path, dpi=150); plt.close(fig)


def setup(local: Path):
    upstream = local/'upstream'
    sys.path.insert(0, str(upstream/'src'/'python'))
    sys.path.insert(0, str(upstream))
    import contouring
    return contouring


def demo(local: Path) -> None:
    contouring = setup(local); out = local/'demo'; out.mkdir(parents=True, exist_ok=True)
    cube = read_obj(local/'upstream'/'data'/'cube.obj')
    cube = Mesh(gpy.normalize_points(cube.v), cube.f)
    # The author path obtains samples from mesh signed distance, not an oracle.
    fixture = Fixture('author_cube', None)
    published_rotation = author_rotation()
    mc, mm, _ = run_case(contouring, fixture, cube, 32, 'MarchingCubes', rotation_matrix=published_rotation)
    ours, om, _ = run_case(contouring, fixture, cube, 32, 'Ours', rotation_matrix=published_rotation)
    write_obj(out/'author-cube-mc.obj', mm.v, mm.f); write_obj(out/'author-cube-ours.obj', om.v, om.f)
    visual(out/'author-cube.png', cube, [('Marching Cubes',mm),('DCSD',om)], fixture)
    (out/'demo.json').write_text(json.dumps({'revision':REVISION,'input':'upstream/data/cube.obj','mc':mc,'dcsd':ours},indent=2),encoding='utf-8')
    print(json.dumps({'mc_seconds':mc['seconds'],'dcsd_seconds':ours['seconds'],'output':str(out)},indent=2))


def benchmark(local: Path) -> None:
    contouring = setup(local); mesh_root = local/'aetheris-meshes'; out = local/'benchmark'; out.mkdir(parents=True,exist_ok=True)
    # The 100x100 published optimizer is deliberately expensive (~82 s even
    # for the 32^3 cube). Use it on the decision fixtures; use an explicitly
    # labeled 10x20 screen to expose broad compatibility/failure behavior.
    cases = [(n,32,0.,'none',10,20) for n in FIXTURES]
    cases += [(n,32,0.,'none',100,100) for n in ('sphere','sharp_box','thin_plate','bent_sheet')]
    cases += [('sphere',64,0.,'none',10,20),('torus',64,0.,'none',10,20),
              ('sharp_box',64,0.,'none',100,100),('thin_plate',64,0.,'none',10,20)]
    cases += [('sharp_box',32,x,'none',10,20) for x in (.05,.2)] + [('sharp_box',32,0.,'float16',10,20)]
    records=[]; visuals={}
    for name,cells,noise,quant,outer,inner in cases:
        source=read_obj(mesh_root/f'{name}.obj'); fixture=FIXTURES[name]
        methods=['MarchingCubes','Ours']
        print(f'CASE {name} {cells} noise={noise} quant={quant}',flush=True)
        for method in methods:
            rec,mesh,_=run_case(contouring,fixture,source,cells,method,noise,quant,outer,inner); records.append(rec)
            key=(name,cells,noise,quant,outer,inner); visuals.setdefault(key,{})[method]=mesh
            write_obj(out/f'{name}-{cells}-{noise}-{quant}-{outer}x{inner}-{method}.obj',mesh.v,mesh.f)
    # Determinism: exact repeat, including vertex ordering.
    src=read_obj(mesh_root/'sharp_box.obj'); a,ma,_=run_case(contouring,FIXTURES['sharp_box'],src,32,'Ours',outer_iters=10,inner_iters=20); b,mb,_=run_case(contouring,FIXTURES['sharp_box'],src,32,'Ours',outer_iters=10,inner_iters=20)
    deterministic={'shape_equal':ma.v.shape==mb.v.shape and ma.f.shape==mb.f.shape,
                   'faces_equal':bool(np.array_equal(ma.f,mb.f)),
                   'max_vertex_delta_mm':float(np.max(np.abs(ma.v-mb.v))) if ma.v.shape==mb.v.shape else math.inf}
    for key in [('sharp_box',32,0.,'none',100,100),('thin_plate',32,0.,'none',100,100),('bent_sheet',32,0.,'none',100,100),('sphere',32,0.,'none',100,100)]:
        source=read_obj(mesh_root/f'{key[0]}.obj'); visual(out/f'{key[0]}-comparison.png',source,[(m,visuals[key][m]) for m in ('MarchingCubes','Ours')],FIXTURES[key[0]])
    with (out/'metrics.csv').open('w',newline='',encoding='utf-8') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(records[0])); writer.writeheader(); writer.writerows(records)
    summary={'schema':'aetheris.dcsd-dogfood-x0.v1','revision':REVISION,'seed':SEED,
             'environment':{'platform':platform.platform(),'python':platform.python_version(),'cpu_count':os.cpu_count()},
             'published_defaults':{'outer_iters':100,'inner_iters':100,'hermite_update':True,'mu':.1,'dc_weight':.02,'sphere_weight':1.,'svd_threshold':.01,'blend_weights':[.2,.2,.2]},
             'screening_profile':{'outer_iters':10,'inner_iters':20,'purpose':'broad bounded failure-mode screen; not used as the primary quality claim'},
             'records':records,'determinism':deterministic,
             'skipped_resolutions':{'128':'bounded X0 runtime; 64 already exposes thin-feature and scaling behavior','256':'~17.2M samples and impractical published 100x100 optimization on this CPU'}}
    (out/'summary.json').write_text(json.dumps(summary,indent=2,allow_nan=True),encoding='utf-8')
    # Simple evidence plots.
    fig,axes=plt.subplots(1,3,figsize=(14,4))
    clean=[r for r in records if r['noise_voxels']==0 and r['quantize']=='none']
    for method in ('MarchingCubes','Ours'):
        x=[r['voxel_mm'] for r in clean if r['method']==method]; y=[r['rms_mm'] for r in clean if r['method']==method]
        axes[0].scatter(x,y,label=method,alpha=.75); axes[1].scatter([r['grid_nodes'] for r in clean if r['method']==method],[r['seconds'] for r in clean if r['method']==method],label=method,alpha=.75)
    noise=[r for r in records if r['fixture']=='sharp_box' and r['cells']==32 and r['quantize']=='none']
    for method in ('MarchingCubes','Ours'):
        rows=[r for r in noise if r['method']==method]; axes[2].plot([r['noise_voxels'] for r in rows],[r['rms_mm'] for r in rows],'o-',label=method)
    axes[0].set(xlabel='voxel size (mm)',ylabel='surface RMS (mm)',title='Error vs sampling'); axes[1].set(xlabel='grid nodes',ylabel='seconds',title='Runtime scaling'); axes[1].set_xscale('log'); axes[1].set_yscale('log'); axes[2].set(xlabel='SDF noise (voxels)',ylabel='surface RMS (mm)',title='Sharp box noise');
    for ax in axes: ax.grid(alpha=.25); ax.legend()
    fig.tight_layout(); fig.savefig(out/'plots.png',dpi=160); plt.close(fig)
    print(json.dumps({'cases':len(records),'determinism':deterministic,'output':str(out)},indent=2))


def wedge(local: Path) -> None:
    """Run only the new exact sharp-prism screening witness."""
    contouring = setup(local)
    source = read_obj(local/'aetheris-meshes'/'sharp_wedge_prism.obj')
    fixture = FIXTURES['sharp_wedge_prism']
    out = local/'benchmark'
    out.mkdir(parents=True, exist_ok=True)
    records = []
    meshes = []
    for method in ('MarchingCubes', 'Ours'):
        record, mesh, _ = run_case(contouring, fixture, source, 32, method,
                                   outer_iters=10, inner_iters=20)
        records.append(record)
        meshes.append((method, mesh))
        write_obj(out/f'sharp_wedge_prism-32-0.0-none-10x20-{method}.obj', mesh.v, mesh.f)
    center = (source.v.min(0) + source.v.max(0)) * .5
    rotated_source = Mesh((source.v - center) @ rotation() + center, source.f)
    visual(out/'sharp-wedge-prism-comparison.png', rotated_source, meshes, fixture)
    evidence = {
        'schema': 'aetheris.dcsd-dogfood-x0.wedge.v1',
        'source': 'fixtures/Experiments/DCSDDogfoodX0/sharp-wedge-prism.firmament',
        'field': 'exact analytic signed distance to the triangular prism',
        'profile': {'cells': 32, 'outer_iters': 10, 'inner_iters': 20},
        'records': records,
    }
    path = out/'sharp-wedge-prism-screen.json'
    path.write_text(json.dumps(evidence, indent=2), encoding='utf-8')
    print(json.dumps({'records': records, 'output': str(path)}, indent=2))


def thin_sweep(local: Path) -> None:
    contouring = setup(local); mesh_root = local/'aetheris-meshes'; out = local/'benchmark'; out.mkdir(parents=True,exist_ok=True)
    base = read_obj(mesh_root/'thin_plate.obj'); records=[]
    source_thickness = float(base.v[:,2].max() - base.v[:,2].min())
    for thickness in (10., 3., 1., .5, .25):
        vertices = base.v.copy()
        vertices[:,2] = (vertices[:,2] - vertices[:,2].min()) / source_thickness * thickness
        source = Mesh(vertices, base.f); fixture = Fixture(f'thin_{thickness:g}mm', box_sdf((40.,30.,thickness)))
        for cells in (32,64):
            for method in ('MarchingCubes','Ours'):
                rec, mesh, _ = run_case(contouring,fixture,source,cells,method,outer_iters=10,inner_iters=20)
                center=(source.v.min(0)+source.v.max(0))*.5
                local=(mesh.v-center)@rotation().T+center
                rec['requested_thickness_mm']=thickness
                rec['reconstructed_z_span_mm']=float(np.max(local[:,2])-np.min(local[:,2])) if len(local) else math.nan
                records.append(rec)
                write_obj(out/f'thin-{thickness:g}mm-{cells}-{method}.obj',mesh.v,mesh.f)
    (out/'thin-sweep.json').write_text(json.dumps({'profile':'10x20 screening','records':records},indent=2),encoding='utf-8')
    fig,axes=plt.subplots(1,2,figsize=(10,4))
    for method in ('MarchingCubes','Ours'):
        for cells,style in ((32,'o-'),(64,'s--')):
            rows=[r for r in records if r['method']==method and r['cells']==cells]
            label=f'{method} {cells}^3'
            axes[0].plot([r['requested_thickness_mm'] for r in rows],[r['rms_mm'] for r in rows],style,label=label)
            axes[1].plot([r['requested_thickness_mm'] for r in rows],[r['nonmanifold_edges'] for r in rows],style,label=label)
    axes[0].set(xscale='log',yscale='log',xlabel='plate thickness (mm)',ylabel='surface RMS (mm)',title='Thin-wall distance')
    axes[1].set(xscale='log',yscale='symlog',xlabel='plate thickness (mm)',ylabel='nonmanifold edges',title='Thin-wall topology')
    for ax in axes: ax.grid(alpha=.25); ax.legend(fontsize=7)
    fig.tight_layout(); fig.savefig(out/'thin-sweep.png',dpi=160); plt.close(fig)
    print(json.dumps({'cases':len(records),'output':str(out/'thin-sweep.json')},indent=2))


def box_feature_metrics(mesh: Mesh, voxel_mm: float) -> dict:
    r = rotation(); center = np.array([0.,0.,12.5]); local_v = (mesh.v-center) @ r.T + center
    lo=np.array([-40.,-25.,0.]); hi=np.array([40.,25.,25.])
    corners=np.array([[x,y,z] for x in (lo[0],hi[0]) for y in (lo[1],hi[1]) for z in (lo[2],hi[2])])
    corners_rot=(corners-center)@r+center
    corner_error=cKDTree(mesh.v).query(corners_rot)[0]
    tri=mesh.v[mesh.f]; normals=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]); normals/=np.maximum(np.linalg.norm(normals,axis=1,keepdims=True),EPS)
    edges=np.sort(np.vstack((mesh.f[:,[0,1]],mesh.f[:,[1,2]],mesh.f[:,[2,0]])),axis=1)
    face_ids=np.tile(np.arange(len(mesh.f)),3)
    order=np.lexsort((edges[:,1],edges[:,0])); edges=edges[order]; face_ids=face_ids[order]
    unique,starts,counts=np.unique(edges,axis=0,return_index=True,return_counts=True)
    sharp_mid=[]; angle_error=[]
    for edge,start,count in zip(unique,starts,counts):
        if count != 2: continue
        n0,n1=normals[face_ids[start]],normals[face_ids[start+1]]
        angle=math.degrees(math.acos(float(np.clip(abs(np.dot(n0,n1)),-1,1))))
        if angle > 30:
            sharp_mid.append((mesh.v[edge[0]]+mesh.v[edge[1]])*.5); angle_error.append(abs(angle-90.))
    true_segments=[]
    for i,a in enumerate(corners_rot):
        for j,b in enumerate(corners_rot):
            if j>i and np.sum(np.abs(corners[i]-corners[j])>EPS)==1: true_segments.append((a,b))
    def segment_distance(p,a,b):
        ab=b-a; t=np.clip(np.dot(p-a,ab)/np.dot(ab,ab),0,1); return np.linalg.norm(p-(a+t*ab))
    edge_error=np.array([min(segment_distance(p,a,b) for a,b in true_segments) for p in sharp_mid])
    # Face-interior vertices: nearest supporting plane, at least two voxels from all face boundaries.
    d=np.column_stack((abs(local_v[:,0]-lo[0]),abs(local_v[:,0]-hi[0]),abs(local_v[:,1]-lo[1]),abs(local_v[:,1]-hi[1]),abs(local_v[:,2]-lo[2]),abs(local_v[:,2]-hi[2])))
    nearest=np.argmin(d,axis=1); residual=np.min(d,axis=1)
    margin=np.minimum(local_v-lo,hi-local_v)
    interior=np.array([margin[i,(nearest[i]//2+1)%3] > 2*voxel_mm and margin[i,(nearest[i]//2+2)%3] > 2*voxel_mm for i in range(len(local_v))])
    plane=residual[interior]
    return {'edge_candidate_count':len(edge_error),'edge_rms_mm':float(np.sqrt(np.mean(edge_error**2))) if len(edge_error) else math.nan,
            'edge_p95_mm':float(np.percentile(edge_error,95)) if len(edge_error) else math.nan,'edge_max_mm':float(np.max(edge_error)) if len(edge_error) else math.nan,
            'corner_rms_mm':float(np.sqrt(np.mean(corner_error**2))),'corner_p95_mm':float(np.percentile(corner_error,95)),'corner_max_mm':float(np.max(corner_error)),
            'dihedral_mean_abs_error_deg':float(np.mean(angle_error)) if angle_error else math.nan,'dihedral_p95_abs_error_deg':float(np.percentile(angle_error,95)) if angle_error else math.nan,
            'plane_interior_vertex_count':int(np.sum(interior)),'plane_residual_rms_mm':float(np.sqrt(np.mean(plane**2))) if len(plane) else math.nan,'plane_residual_max_mm':float(np.max(plane)) if len(plane) else math.nan}


def analyze(local: Path) -> None:
    out=local/'benchmark'; summary=json.loads((out/'summary.json').read_text(encoding='utf-8')); result={}
    for cells in (32,64):
        for method,outer,inner in (('MarchingCubes',100,100),('Ours',100,100)):
            path=out/f'sharp_box-{cells}-0.0-none-{outer}x{inner}-{method}.obj'
            record=next(r for r in summary['records'] if r['fixture']=='sharp_box' and r['cells']==cells and r['method']==method and (method=='MarchingCubes' or r['outer_iters']==100) and r['noise_voxels']==0 and r['quantize']=='none')
            result[f'{method}-{cells}']=box_feature_metrics(read_obj(path),record['voxel_mm'])
    (out/'sharp-feature-metrics.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result,indent=2))


def open_surface(local: Path) -> None:
    contouring=setup(local); out=local/'benchmark'; out.mkdir(parents=True,exist_ok=True)
    v=np.array([[-20.,-15.,0.],[20.,-15.,0.],[20.,15.,0.],[-20.,15.,0.]])
    source=Mesh(v,np.array([[0,1,2],[0,2,3]],dtype=np.int64))
    fixture=Fixture('open_rectangle',lambda q:q[:,2])
    records=[]
    for method in ('MarchingCubes','Ours'):
        rec,mesh,_=run_case(contouring,fixture,source,32,method,outer_iters=10,inner_iters=20)
        rec['intended_footprint_mm']=[40.,30.]
        rec['reconstructed_xy_extent_mm']=(mesh.v.max(0)-mesh.v.min(0))[:2].tolist()
        records.append(rec); write_obj(out/f'open-rectangle-{method}.obj',mesh.v,mesh.f)
    (out/'open-surface.json').write_text(json.dumps({'field':'phi=z encodes an unbounded plane; finite patch boundary is absent from signed-volume data','records':records},indent=2),encoding='utf-8')
    print(json.dumps(records,indent=2))


def visual_evidence(local: Path) -> None:
    out=local/'benchmark'; exact=box_sdf((80.,50.,25.)); center=np.array([0.,0.,12.5]); r=rotation()
    mesh=read_obj(out/'sharp_box-32-0.0-none-100x100-Ours.obj')
    q=(mesh.v-center)@r.T+center; err=np.abs(exact(q))
    fig,axes=plt.subplots(1,3,figsize=(14,4))
    views=[(0,1,'top / XY',(-42,42),(-27,27)),(0,2,'front / XZ',(-42,42),(-2,27)),(1,2,'side / YZ',(-27,27),(-2,27))]
    for ax,(a,b,title,xlim,ylim) in zip(axes,views):
        sc=ax.scatter(q[:,a],q[:,b],c=err,s=3,cmap='magma',vmin=0,vmax=np.percentile(err,99)); ax.set_title(title); ax.set_xlim(*xlim); ax.set_ylim(*ylim); ax.set_aspect('equal'); ax.grid(alpha=.15)
    fig.colorbar(sc,ax=axes,label='absolute exact SDF error (mm)',shrink=.8); fig.suptitle('DCSD sharp-box vertex error, 32³ published profile'); fig.savefig(out/'sharp-error-heatmap.png',dpi=170,bbox_inches='tight'); plt.close(fig)
    corner=np.array([40.,25.,25.]); keep=np.linalg.norm(q-corner,axis=1)<8
    fig,ax=plt.subplots(figsize=(6,5)); sc=ax.scatter(q[keep,0],q[keep,2],c=err[keep],s=12,cmap='magma'); ax.scatter([40],[25],marker='x',s=100,color='cyan',label='exact corner'); ax.set(xlabel='x (mm)',ylabel='z (mm)',title='Sharp corner close-up'); ax.set_aspect('equal'); ax.grid(alpha=.2); ax.legend(); fig.colorbar(sc,ax=ax,label='error (mm)'); fig.savefig(out/'sharp-corner-closeup.png',dpi=170,bbox_inches='tight'); plt.close(fig)
    x=np.linspace(-46.4,46.4,33); y=np.linspace(-31.4,31.4,33); xx,yy=np.meshgrid(x,y); qq=np.column_stack((xx.ravel(),yy.ravel(),np.full(xx.size,12.5))); phi=exact(qq).reshape(xx.shape)
    fig,ax=plt.subplots(figsize=(7,5)); im=ax.contourf(xx,yy,phi,levels=32,cmap='coolwarm'); ax.contour(xx,yy,phi,levels=[0],colors='black',linewidths=2); ax.scatter(xx,yy,s=2,color='black',alpha=.25); ax.set_aspect('equal'); ax.set_title('Representative exact SDF grid slice (box mid-plane)'); ax.set_xlabel('x (mm)'); ax.set_ylabel('y (mm)'); fig.colorbar(im,ax=ax,label='signed distance (mm)'); fig.savefig(out/'sdf-grid-slice.png',dpi=170,bbox_inches='tight'); plt.close(fig)
    noisy=read_obj(out/'sharp_box-32-0.2-none-10x20-Ours.obj'); nq=(noisy.v-center)@r.T+center; ne=np.abs(exact(nq))
    fig,ax=plt.subplots(figsize=(7,5)); sc=ax.scatter(nq[:,0],nq[:,2],c=ne,s=4,cmap='magma',vmax=np.percentile(ne,99)); ax.set_aspect('equal'); ax.set_title('Sharp box with 0.20-voxel SDF noise'); ax.set_xlabel('x (mm)'); ax.set_ylabel('z (mm)'); fig.colorbar(sc,ax=ax,label='error (mm)'); fig.savefig(out/'sharp-noise.png',dpi=170,bbox_inches='tight'); plt.close(fig)
    print(json.dumps({'outputs':['sharp-error-heatmap.png','sharp-corner-closeup.png','sdf-grid-slice.png','sharp-noise.png']},indent=2))


def main() -> None:
    parser=argparse.ArgumentParser(); parser.add_argument('command',choices=('demo','benchmark','wedge','thin-sweep','analyze','open-surface','visual-evidence')); parser.add_argument('--local-root',type=Path,required=True); args=parser.parse_args()
    {'demo':demo,'benchmark':benchmark,'wedge':wedge,'thin-sweep':thin_sweep,'analyze':analyze,'open-surface':open_surface,'visual-evidence':visual_evidence}[args.command](args.local_root.resolve())


if __name__=='__main__': main()
