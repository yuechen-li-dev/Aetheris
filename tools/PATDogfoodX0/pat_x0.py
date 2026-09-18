from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import platform
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

import matplotlib

matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

from pointsastori.infer import PointsAsTori
from pointsastori.shape_3d import TriangleMesh


SEED = 20260917
EPS = 1.0e-9


@dataclass(frozen=True)
class MeshData:
    vertices: np.ndarray
    faces: np.ndarray
    corner_normals: np.ndarray


@dataclass(frozen=True)
class Fixture:
    name: str
    truth_kind: str
    exact_sdf: Callable[[np.ndarray], np.ndarray] | None = None


def sha256(path: Path) -> str:
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest().upper()


def read_obj(path: Path) -> MeshData:
    vertices: list[list[float]] = []
    normals: list[list[float]] = []
    triangles: list[list[int]] = []
    triangle_normal_indices: list[list[int]] = []
    with path.open('r', encoding='utf-8') as stream:
        for raw in stream:
            parts = raw.strip().split()
            if not parts:
                continue
            if parts[0] == 'v':
                vertices.append([float(x) for x in parts[1:4]])
            elif parts[0] == 'vn':
                normals.append([float(x) for x in parts[1:4]])
            elif parts[0] == 'f':
                corners = parts[1:]
                for j in range(1, len(corners) - 1):
                    tri_tokens = [corners[0], corners[j], corners[j + 1]]
                    vi: list[int] = []
                    ni: list[int] = []
                    for token in tri_tokens:
                        fields = token.split('/')
                        vi.append(int(fields[0]) - 1)
                        normal_index = int(fields[2]) - 1 if len(fields) > 2 and fields[2] else -1
                        ni.append(normal_index)
                    triangles.append(vi)
                    triangle_normal_indices.append(ni)

    v = np.asarray(vertices, dtype=np.float64)
    f = np.asarray(triangles, dtype=np.int64)
    geometric = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    geometric /= np.maximum(np.linalg.norm(geometric, axis=1, keepdims=True), EPS)
    if normals and all(index >= 0 for row in triangle_normal_indices for index in row):
        source_normals = np.asarray(normals, dtype=np.float64)
        cn = source_normals[np.asarray(triangle_normal_indices, dtype=np.int64)]
    else:
        cn = np.repeat(geometric[:, None, :], 3, axis=1)
    cn /= np.maximum(np.linalg.norm(cn, axis=2, keepdims=True), EPS)
    return MeshData(v, f, cn)


def sample_surface(mesh: MeshData, count: int, seed: int, mode: str = 'area') -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed)
    tri = mesh.vertices[mesh.faces]
    twice_area = np.linalg.norm(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]), axis=1)
    if mode == 'area':
        probability = twice_area / np.sum(twice_area)
    elif mode == 'uniform-triangle':
        probability = None
    else:
        raise ValueError(mode)
    face_index = rng.choice(len(mesh.faces), size=count, replace=True, p=probability)
    uv = rng.random((count, 2))
    swap = np.sum(uv, axis=1) > 1.0
    uv[swap] = 1.0 - uv[swap]
    bary = np.column_stack((1.0 - uv[:, 0] - uv[:, 1], uv[:, 0], uv[:, 1]))
    selected_tri = tri[face_index]
    points = np.sum(selected_tri * bary[:, :, None], axis=1)
    selected_normals = mesh.corner_normals[face_index]
    out_normals = np.sum(selected_normals * bary[:, :, None], axis=1)
    out_normals /= np.maximum(np.linalg.norm(out_normals, axis=1, keepdims=True), EPS)
    return points, out_normals


def sphere_sdf(q: np.ndarray) -> np.ndarray:
    return np.linalg.norm(q, axis=1) - 7.5


def torus_sdf(q: np.ndarray) -> np.ndarray:
    # Aetheris' canonical Torus axis is +Y.
    radial = np.linalg.norm(q[:, [0, 2]], axis=1) - 12.0
    return np.sqrt(radial * radial + q[:, 1] * q[:, 1]) - 3.0


def box_sdf(size: tuple[float, float, float]) -> Callable[[np.ndarray], np.ndarray]:
    half = np.asarray(size, dtype=np.float64) * 0.5
    center = np.array([0.0, 0.0, half[2]])

    def evaluate(q: np.ndarray) -> np.ndarray:
        d = np.abs(q - center) - half
        return np.linalg.norm(np.maximum(d, 0.0), axis=1) + np.minimum(np.max(d, axis=1), 0.0)

    return evaluate


def cylinder_sdf(q: np.ndarray) -> np.ndarray:
    radial = np.linalg.norm(q[:, :2], axis=1) - 12.0
    axial = np.abs(q[:, 2] - 15.0) - 15.0
    d = np.column_stack((radial, axial))
    return np.linalg.norm(np.maximum(d, 0.0), axis=1) + np.minimum(np.max(d, axis=1), 0.0)


def frustum_sdf(q: np.ndarray) -> np.ndarray:
    """Exact SDF for the canonical capped frustum, centered for evaluation."""
    half_height = 15.0
    bottom_radius = 20.0
    top_radius = 10.0
    radial_axial = np.column_stack((np.linalg.norm(q[:, :2], axis=1), q[:, 2] - half_height))
    cap_radius = np.where(radial_axial[:, 1] < 0.0, bottom_radius, top_radius)
    cap = np.column_stack(
        (
            radial_axial[:, 0] - np.minimum(radial_axial[:, 0], cap_radius),
            np.abs(radial_axial[:, 1]) - half_height,
        )
    )
    upper = np.array([top_radius, half_height])
    slope = np.array([top_radius - bottom_radius, 2.0 * half_height])
    projection = np.clip(
        np.sum((upper - radial_axial) * slope, axis=1) / np.dot(slope, slope),
        0.0,
        1.0,
    )
    side = radial_axial - upper + projection[:, None] * slope
    sign = np.where((side[:, 0] < 0.0) & (cap[:, 1] < 0.0), -1.0, 1.0)
    return sign * np.sqrt(np.minimum(np.sum(cap * cap, axis=1), np.sum(side * side, axis=1)))


FIXTURES = [
    Fixture('sphere', 'analytic', sphere_sdf),
    Fixture('cylinder', 'analytic', cylinder_sdf),
    Fixture('torus', 'analytic', torus_sdf),
    Fixture('frustum', 'analytic', frustum_sdf),
    Fixture('slab', 'analytic', box_sdf((40.0, 30.0, 3.0))),
    Fixture('filleted_box', 'qualified-mesh'),
    Fixture('rounded_rectangle_prism', 'qualified-mesh'),
    Fixture('sharp_box', 'analytic', box_sdf((80.0, 50.0, 25.0))),
    Fixture('chamfered_box', 'qualified-mesh'),
    Fixture('through_hole', 'qualified-mesh'),
    Fixture('counterbore', 'qualified-mesh'),
    Fixture('thin_plate', 'analytic', box_sdf((40.0, 30.0, 0.5))),
    Fixture('bent_sheet', 'qualified-mesh'),
    Fixture('phone_like_chassis', 'qualified-mesh'),
]


def truth_evaluator(fixture: Fixture, mesh: MeshData) -> Callable[[np.ndarray], np.ndarray]:
    if fixture.exact_sdf is not None:
        return fixture.exact_sdf
    bound = TriangleMesh(mesh.vertices, mesh.faces)
    return lambda q: np.asarray(bound.evaluate_signed_distance(q), dtype=np.float64)


def snap_analytic_samples(
    fixture: Fixture, points: np.ndarray, normals: np.ndarray
) -> tuple[np.ndarray, np.ndarray]:
    """Lift tessellation samples back to exact admitted analytic supports."""
    p = points.copy()
    n = normals.copy()
    if fixture.name == 'sphere':
        n = p / np.maximum(np.linalg.norm(p, axis=1, keepdims=True), EPS)
        p = 7.5 * n
    elif fixture.name == 'torus':
        radial = p[:, [0, 2]]
        radial_unit = radial / np.maximum(np.linalg.norm(radial, axis=1, keepdims=True), EPS)
        centerline = np.column_stack((12.0 * radial_unit[:, 0], np.zeros(len(p)), 12.0 * radial_unit[:, 1]))
        n = p - centerline
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), EPS)
        p = centerline + 3.0 * n
    elif fixture.name == 'cylinder':
        cap = np.abs(n[:, 2]) > 0.7
        p[cap, 2] = np.where(n[cap, 2] > 0.0, 30.0, 0.0)
        n[cap] = np.column_stack((np.zeros(np.sum(cap)), np.zeros(np.sum(cap)), np.sign(n[cap, 2])))
        side = ~cap
        radial = p[side, :2]
        radial_unit = radial / np.maximum(np.linalg.norm(radial, axis=1, keepdims=True), EPS)
        p[side, :2] = 12.0 * radial_unit
        n[side] = np.column_stack((radial_unit, np.zeros(np.sum(side))))
    elif fixture.name == 'frustum':
        cap = np.abs(n[:, 2]) > 0.7
        p[cap, 2] = np.where(n[cap, 2] > 0.0, 30.0, 0.0)
        n[cap] = np.column_stack((np.zeros(np.sum(cap)), np.zeros(np.sum(cap)), np.sign(n[cap, 2])))
        side = ~cap
        radial = p[side, :2]
        radial_unit = radial / np.maximum(np.linalg.norm(radial, axis=1, keepdims=True), EPS)
        radius = 20.0 - p[side, 2] / 3.0
        p[side, :2] = radius[:, None] * radial_unit
        side_normal = np.column_stack((radial_unit, np.full(np.sum(side), 1.0 / 3.0)))
        n[side] = side_normal / np.linalg.norm(side_normal, axis=1, keepdims=True)
    return p, n


def percentile(values: np.ndarray, p: float) -> float:
    return float(np.percentile(values, p)) if len(values) else math.nan


def summarize_errors(predicted: np.ndarray, truth: np.ndarray) -> dict[str, float | int]:
    error = predicted - truth
    absolute = np.abs(error)
    signed_mask = np.abs(truth) > 1.0e-5
    sign_correct = np.signbit(predicted[signed_mask]) == np.signbit(truth[signed_mask])
    false_material = np.sum((predicted[signed_mask] < 0.0) & (truth[signed_mask] > 0.0))
    false_void = np.sum((predicted[signed_mask] > 0.0) & (truth[signed_mask] < 0.0))
    return {
        'count': int(len(truth)),
        'rms_mm': float(np.sqrt(np.mean(error * error))),
        'p50_abs_mm': percentile(absolute, 50),
        'p95_abs_mm': percentile(absolute, 95),
        'p99_abs_mm': percentile(absolute, 99),
        'max_abs_mm': float(np.max(absolute)),
        'signed_bias_mm': float(np.mean(error)),
        'sign_error_count': int(np.sum(~sign_correct)),
        'sign_error_rate': float(np.mean(~sign_correct)) if len(sign_correct) else math.nan,
        'classification_accuracy': float(np.mean(sign_correct)) if len(sign_correct) else math.nan,
        'false_material_count': int(false_material),
        'false_void_count': int(false_void),
    }


def query_corpus(
    fixture: Fixture,
    mesh: MeshData,
    surface_points: np.ndarray,
    surface_normals: np.ndarray,
    truth: Callable[[np.ndarray], np.ndarray],
) -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(SEED + 91)
    indices = rng.choice(len(surface_points), size=min(384, len(surface_points)), replace=False)
    p = surface_points[indices]
    n = surface_normals[indices]
    scale = np.linalg.norm(np.ptp(mesh.vertices, axis=0))
    near = max(scale * 0.002, 0.01)
    moderate = max(scale * 0.03, 0.1)
    queries = []
    categories = []
    for distance, category in [
        (0.0, 'on-surface'),
        (-near, 'surface-near'),
        (near, 'surface-near'),
        (-moderate, 'inside'),
        (moderate, 'outside'),
    ]:
        queries.append(p + distance * n)
        categories.extend([category] * len(p))

    low = np.min(mesh.vertices, axis=0)
    high = np.max(mesh.vertices, axis=0)
    pad = np.maximum((high - low) * 0.35, 0.5)
    random_queries = rng.uniform(low - pad, high + pad, size=(1200, 3))
    random_truth = truth(random_queries)
    queries.append(random_queries)
    categories.extend(np.where(random_truth < 0.0, 'inside', 'far-field').tolist())

    if fixture.name == 'sharp_box':
        offsets = rng.normal(0.0, 0.6, size=(400, 3))
        edge = np.tile(np.array([40.0, 25.0, 12.5]), (200, 1)) + offsets[:200]
        corner = np.tile(np.array([40.0, 25.0, 25.0]), (200, 1)) + offsets[200:]
        queries.extend([edge, corner])
        categories.extend(['sharp-edge-near'] * len(edge))
        categories.extend(['sharp-corner-near'] * len(corner))
    elif fixture.name in {'chamfered_box', 'counterbore', 'through_hole'}:
        feature_index = np.argsort(np.linalg.norm(p[:, :2], axis=1))[: min(250, len(p))]
        queries.append(p[feature_index] + rng.normal(0.0, near, size=(len(feature_index), 3)))
        categories.extend(['sharp-feature-near'] * len(feature_index))
    elif fixture.name in {'thin_plate', 'slab', 'bent_sheet'}:
        zline = np.linspace(np.min(mesh.vertices[:, 2]) - 1.0, np.max(mesh.vertices[:, 2]) + 1.0, 500)
        thin = np.column_stack((np.zeros_like(zline), np.zeros_like(zline), zline))
        queries.append(thin)
        categories.extend(['thin-wall'] * len(thin))

    return np.vstack(queries), np.asarray(categories, dtype=object)


def perturb(
    points: np.ndarray,
    normals: np.ndarray,
    scenario: str,
    level: float,
    seed: int,
) -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed)
    p = points.copy()
    n = normals.copy()
    if scenario == 'position-noise':
        p += rng.normal(0.0, level, size=p.shape)
    elif scenario == 'normal-noise':
        n += rng.normal(0.0, math.radians(level), size=n.shape)
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), EPS)
    elif scenario == 'normal-flips':
        indices = rng.choice(len(n), size=max(1, int(level * len(n))), replace=False)
        n[indices] *= -1.0
    elif scenario == 'missing-cap':
        threshold = np.quantile(p[:, 2], 1.0 - level)
        keep = p[:, 2] < threshold
        p, n = p[keep], n[keep]
    elif scenario == 'random-missing':
        keep = rng.random(len(p)) >= level
        p, n = p[keep], n[keep]
    elif scenario == 'nonuniform-density':
        weights = 0.1 + np.square((p[:, 0] - np.min(p[:, 0])) / max(np.ptp(p[:, 0]), EPS))
        indices = rng.choice(len(p), size=len(p), replace=True, p=weights / np.sum(weights))
        p, n = p[indices], n[indices]
    elif scenario == 'open-surface':
        keep = n[:, 2] > 0.9
        p, n = p[keep], n[keep]
    return p, n


def zero_offsets(
    pat: PointsAsTori,
    points: np.ndarray,
    normals: np.ndarray,
    target: float = 0.0,
) -> dict[str, float | int]:
    count = min(128, len(points))
    p = points[:count]
    n = normals[:count]
    span = max(float(np.linalg.norm(np.ptp(points, axis=0))) * 0.08, 0.2)
    lo = np.full(count, -span)
    hi = np.full(count, span)
    flo = pat.signed_distance(p + lo[:, None] * n) - target
    fhi = pat.signed_distance(p + hi[:, None] * n) - target
    valid = np.isfinite(flo) & np.isfinite(fhi) & (np.signbit(flo) != np.signbit(fhi))
    if not np.any(valid):
        return {'root_count': 0, 'unbracketed': count, 'rms_mm': math.nan, 'p95_mm': math.nan, 'max_mm': math.nan}
    pv, nv = p[valid], n[valid]
    lv, hv = lo[valid], hi[valid]
    for _ in range(28):
        mid = (lv + hv) * 0.5
        fm = pat.signed_distance(pv + mid[:, None] * nv) - target
        left_value = pat.signed_distance(pv + lv[:, None] * nv) - target
        choose_right = np.signbit(fm) == np.signbit(left_value)
        lv = np.where(choose_right, mid, lv)
        hv = np.where(choose_right, hv, mid)
    root = (lv + hv) * 0.5
    expected = target
    error = np.abs(root - expected)
    return {
        'root_count': int(len(error)),
        'unbracketed': int(count - len(error)),
        'rms_mm': float(np.sqrt(np.mean(error * error))),
        'p95_mm': percentile(error, 95),
        'max_mm': float(np.max(error)),
    }


def run_case(
    fixture: Fixture,
    mesh: MeshData,
    point_count: int,
    scenario: str,
    level: float,
    sampling: str,
) -> tuple[list[dict], dict, PointsAsTori, np.ndarray, np.ndarray]:
    base_points, base_normals = sample_surface(mesh, point_count, SEED + point_count, sampling)
    base_points, base_normals = snap_analytic_samples(fixture, base_points, base_normals)
    points, normals = perturb(base_points, base_normals, scenario, level, SEED + point_count + 7)
    truth = truth_evaluator(fixture, mesh)
    queries, categories = query_corpus(fixture, mesh, base_points, base_normals, truth)

    import psutil

    rss_before = psutil.Process().memory_info().rss
    started = time.perf_counter()
    pat = PointsAsTori(points, normals)
    precompute_seconds = time.perf_counter() - started
    rss_after = psutil.Process().memory_info().rss
    tori = pat._tdf.get_tori()
    finite_tori = np.isfinite(tori[0]).all(axis=1) & np.isfinite(tori[1]).all(axis=1)
    finite_tori &= np.isfinite(tori[2]) & np.isfinite(tori[3])

    started = time.perf_counter()
    predicted = np.asarray(pat.signed_distance(queries), dtype=np.float64)
    query_seconds = time.perf_counter() - started
    exact = truth(queries)

    nearest_started = time.perf_counter()
    from scipy.spatial import cKDTree

    nearest = cKDTree(points).query(queries, k=1)[1]
    baseline = np.sum((queries - points[nearest]) * normals[nearest], axis=1)
    baseline_seconds = time.perf_counter() - nearest_started

    repeat = np.asarray(pat.signed_distance(queries), dtype=np.float64)
    deterministic_max_delta = float(np.max(np.abs(repeat - predicted)))
    records: list[dict] = []
    for i in range(len(queries)):
        records.append(
            {
                'fixture': fixture.name,
                'point_count': int(len(points)),
                'requested_point_count': point_count,
                'sampling': sampling,
                'scenario': scenario,
                'noise_level': level,
                'query_category': str(categories[i]),
                'query_x_mm': float(queries[i, 0]),
                'query_y_mm': float(queries[i, 1]),
                'query_z_mm': float(queries[i, 2]),
                'pat_mm': float(predicted[i]),
                'truth_mm': float(exact[i]),
                'absolute_error_mm': float(abs(predicted[i] - exact[i])),
                'sign_correct': bool(abs(exact[i]) <= 1.0e-6 or np.signbit(predicted[i]) == np.signbit(exact[i])),
                'baseline_tangent_mm': float(baseline[i]),
                'baseline_absolute_error_mm': float(abs(baseline[i] - exact[i])),
            }
        )

    groups = {}
    for category in sorted(set(categories.tolist())):
        mask = categories == category
        groups[category] = summarize_errors(predicted[mask], exact[mask])
        groups[category]['baseline_rms_mm'] = summarize_errors(baseline[mask], exact[mask])['rms_mm']
    # Occupancy has no defined positive/negative answer exactly on the boundary.
    # Keep those samples in distance metrics but exclude them from aggregate
    # classification metrics.
    overall = summarize_errors(predicted, exact)
    classification_mask = categories != 'on-surface'
    classified = summarize_errors(predicted[classification_mask], exact[classification_mask])
    for key in (
        'sign_error_count',
        'sign_error_rate',
        'classification_accuracy',
        'false_material_count',
        'false_void_count',
    ):
        overall[key] = classified[key]
    overall['baseline_rms_mm'] = summarize_errors(baseline, exact)['rms_mm']
    summary = {
        'fixture': fixture.name,
        'truth_authority': fixture.truth_kind,
        'requested_point_count': point_count,
        'actual_point_count': int(len(points)),
        'sampling': sampling,
        'scenario': scenario,
        'level': level,
        'precompute_seconds': precompute_seconds,
        'rss_delta_bytes': int(rss_after - rss_before),
        'query_count': int(len(queries)),
        'query_seconds': query_seconds,
        'queries_per_second': float(len(queries) / query_seconds),
        'baseline_query_seconds': baseline_seconds,
        'finite_torus_count': int(np.sum(finite_tori)),
        'nonfinite_torus_count': int(len(finite_tori) - np.sum(finite_tori)),
        'deterministic_repeat_max_delta_mm': deterministic_max_delta,
        'overall': overall,
        'by_category': groups,
        'zero_level': zero_offsets(pat, base_points, base_normals, 0.0),
        'offset_plus_1mm': zero_offsets(pat, base_points, base_normals, 1.0),
    }
    return records, summary, pat, points, normals


def machine_info() -> dict:
    try:
        gpu = subprocess.check_output(
            ['nvidia-smi', '--query-gpu=name,memory.total,driver_version', '--format=csv,noheader'],
            text=True,
        ).strip()
    except Exception as exc:  # pragma: no cover - diagnostic only
        gpu = f'unavailable: {exc}'
    import jax

    return {
        'platform': platform.platform(),
        'processor': platform.processor(),
        'python': platform.python_version(),
        'jax_devices': [str(device) for device in jax.devices()],
        'gpu': gpu,
        'cpu_count': os.cpu_count(),
    }


def plot_slice(
    output: Path,
    name: str,
    mesh: MeshData,
    pat: PointsAsTori,
    truth: Callable[[np.ndarray], np.ndarray],
    points: np.ndarray,
    axis: int = 2,
) -> None:
    low = np.min(mesh.vertices, axis=0)
    high = np.max(mesh.vertices, axis=0)
    pad = np.maximum((high - low) * 0.12, 0.5)
    varying = [i for i in range(3) if i != axis]
    a = np.linspace(low[varying[0]] - pad[varying[0]], high[varying[0]] + pad[varying[0]], 180)
    b = np.linspace(low[varying[1]] - pad[varying[1]], high[varying[1]] + pad[varying[1]], 180)
    aa, bb = np.meshgrid(a, b)
    queries = np.zeros((aa.size, 3), dtype=np.float64)
    queries[:, varying[0]] = aa.ravel()
    queries[:, varying[1]] = bb.ravel()
    queries[:, axis] = (low[axis] + high[axis]) * 0.5
    predicted = pat.signed_distance(queries).reshape(aa.shape)
    exact = truth(queries).reshape(aa.shape)
    error = np.abs(predicted - exact)

    fig, axes = plt.subplots(1, 3, figsize=(14, 4.2), constrained_layout=True)
    mask = np.abs(points[:, axis] - queries[0, axis]) < max((high[axis] - low[axis]) * 0.08, 0.2)
    axes[0].scatter(points[mask, varying[0]], points[mask, varying[1]], s=2, alpha=0.65)
    axes[0].set_title(f'{name}: oriented source slice')
    im = axes[1].imshow(
        predicted,
        origin='lower',
        extent=[a[0], a[-1], b[0], b[-1]],
        cmap='coolwarm',
        vmin=-max(np.ptp(a), np.ptp(b)) * 0.08,
        vmax=max(np.ptp(a), np.ptp(b)) * 0.08,
    )
    axes[1].contour(aa, bb, predicted, levels=[0.0], colors=['black'], linewidths=1.5)
    axes[1].contour(aa, bb, exact, levels=[0.0], colors=['lime'], linewidths=1.0, linestyles='--')
    axes[1].set_title('PAT zero (black) / truth (green)')
    fig.colorbar(im, ax=axes[1], label='PAT phi (mm)')
    heat = axes[2].imshow(error, origin='lower', extent=[a[0], a[-1], b[0], b[-1]], cmap='magma')
    axes[2].set_title('absolute distance error')
    fig.colorbar(heat, ax=axes[2], label='mm')
    for panel in axes:
        panel.set_aspect('equal')
        panel.set_xlabel(f'axis {varying[0]} (mm)')
        panel.set_ylabel(f'axis {varying[1]} (mm)')
    fig.savefig(output / f'{name}-slice.png', dpi=160)
    plt.close(fig)


def plots(output: Path, summaries: list[dict]) -> None:
    density = [
        s for s in summaries
        if s['fixture'] == 'sphere' and s['scenario'] == 'clean' and s['sampling'] == 'area'
    ]
    density.sort(key=lambda item: item['actual_point_count'])
    noise = [s for s in summaries if s['fixture'] == 'sphere' and s['scenario'] == 'position-noise']
    noise.sort(key=lambda item: item['level'])
    normals = [s for s in summaries if s['fixture'] == 'sphere' and s['scenario'] == 'normal-noise']
    normals.sort(key=lambda item: item['level'])
    thickness = [s for s in summaries if s['fixture'] in {'thin_plate', 'slab'} and s['scenario'] == 'clean']

    fig, axes = plt.subplots(2, 3, figsize=(15, 8), constrained_layout=True)
    axes[0, 0].plot([x['actual_point_count'] for x in density], [x['overall']['rms_mm'] for x in density], 'o-')
    axes[0, 0].set_xscale('log')
    axes[0, 0].set_title('sphere error vs density')
    axes[0, 0].set_xlabel('points')
    axes[0, 0].set_ylabel('RMS (mm)')
    axes[0, 1].plot([x['actual_point_count'] for x in density], [x['precompute_seconds'] for x in density], 'o-')
    axes[0, 1].set_xscale('log')
    axes[0, 1].set_title('precompute time vs input points')
    axes[0, 1].set_xlabel('points')
    axes[0, 1].set_ylabel('seconds')
    axes[0, 2].plot([x['actual_point_count'] for x in density], [x['query_seconds'] for x in density], 'o-')
    axes[0, 2].set_xscale('log')
    axes[0, 2].set_title('query time vs input points')
    axes[0, 2].set_xlabel('points (fixed query corpus)')
    axes[0, 2].set_ylabel('seconds')
    axes[1, 0].plot([x['level'] for x in noise], [x['overall']['rms_mm'] for x in noise], 'o-', label='position')
    axes[1, 0].set_title('sphere error vs position noise')
    axes[1, 0].set_xlabel('sigma (mm)')
    axes[1, 0].set_ylabel('RMS (mm)')
    axes[1, 1].plot([x['level'] for x in normals], [x['overall']['sign_error_rate'] for x in normals], 'o-')
    axes[1, 1].set_title('sign error vs normal noise')
    axes[1, 1].set_xlabel('noise scale (degrees)')
    axes[1, 1].set_ylabel('sign error rate')
    axes[1, 2].bar([x['fixture'] for x in thickness], [x['overall']['sign_error_rate'] for x in thickness])
    axes[1, 2].set_title('sign error vs plate thickness')
    axes[1, 2].set_ylabel('sign error rate')
    fig.savefig(output / 'benchmark-plots.png', dpi=160)
    plt.close(fig)


def sphere_trace_probe(pat: PointsAsTori) -> dict[str, float | int]:
    coordinates = np.linspace(-9.0, 9.0, 31)
    xx, yy = np.meshgrid(coordinates, coordinates)
    origins = np.column_stack((xx.ravel(), yy.ravel(), np.full(xx.size, -20.0)))
    distance_along = np.zeros(len(origins))
    active = np.ones(len(origins), dtype=bool)
    hit = np.zeros(len(origins), dtype=bool)
    steps = np.zeros(len(origins), dtype=np.int32)
    for _ in range(128):
        if not np.any(active):
            break
        index = np.flatnonzero(active)
        q = origins[index].copy()
        q[:, 2] += distance_along[index]
        phi = pat.signed_distance(q)
        converged = np.abs(phi) < 1.0e-4
        hit[index[converged]] = True
        steps[index] += 1
        advance_index = index[~converged]
        distance_along[advance_index] += np.maximum(np.abs(phi[~converged]), 1.0e-4)
        escaped = distance_along > 45.0
        active = ~(hit | escaped)
    expected = xx.ravel() ** 2 + yy.ravel() ** 2 <= 7.5**2
    return {
        'ray_count': int(len(origins)),
        'expected_hit_count': int(np.sum(expected)),
        'hit_count': int(np.sum(hit)),
        'missed_intersections': int(np.sum(expected & ~hit)),
        'false_hits': int(np.sum(~expected & hit)),
        'mean_steps_for_hits': float(np.mean(steps[hit])) if np.any(hit) else math.nan,
        'max_steps': int(np.max(steps)),
    }


def demo(local: Path) -> None:
    output = local / 'demo'
    output.mkdir(parents=True, exist_ok=True)
    source = local / 'upstream' / 'data' / 'block.obj'
    mesh = TriangleMesh.read_OBJ(str(source))
    mesh.center_and_scale()
    started = time.perf_counter()
    points, normals = mesh.sample_uniform_point_cloud(2048, 0.0, 0.0, 42)
    sample_seconds = time.perf_counter() - started
    started = time.perf_counter()
    pat = PointsAsTori(points, normals)
    precompute_seconds = time.perf_counter() - started
    x = np.linspace(-1.2, 1.2, 220)
    y = np.linspace(-1.2, 1.2, 220)
    xx, yy = np.meshgrid(x, y)
    queries = np.column_stack((xx.ravel(), yy.ravel(), np.zeros(xx.size)))
    started = time.perf_counter()
    predicted = pat.signed_distance(queries)
    query_seconds = time.perf_counter() - started
    truth = mesh.evaluate_signed_distance(queries)
    record = {
        'upstream_revision': '253bf9fc5e79831a03f0486e0c42feeb67c1c546',
        'input': 'data/block.obj',
        'input_sha256': sha256(source),
        'mesh_vertices': 2132,
        'mesh_triangles': 4272,
        'point_count': 2048,
        'sampling': 'upstream area-uniform mesh sampling',
        'seed': 42,
        'position_noise': 0.0,
        'normal_flips': 0.0,
        'model_k_neighbors': 64,
        'query_neighbors': 32,
        'sample_seconds': sample_seconds,
        'precompute_seconds': precompute_seconds,
        'query_count': int(len(queries)),
        'query_seconds': query_seconds,
        'accuracy_vs_packaged_mesh': summarize_errors(predicted, truth),
        'deviation': 'Headless public Python API (32-NN analytic queries) replaces the interactive OpenGL shader; the GUI defaults to an all-point shader blend.',
        'machine': machine_info(),
    }
    (output / 'demo.json').write_text(json.dumps(record, indent=2), encoding='utf-8')

    fig, axes = plt.subplots(1, 3, figsize=(14, 4.2), constrained_layout=True)
    axes[0].scatter(points[:, 0], points[:, 1], s=2)
    axes[0].set_title('published block: 2,048 samples')
    field = predicted.reshape(xx.shape)
    exact = truth.reshape(xx.shape)
    axes[1].imshow(field, origin='lower', extent=[x[0], x[-1], y[0], y[-1]], cmap='coolwarm', vmin=-0.4, vmax=0.4)
    axes[1].contour(xx, yy, field, levels=[0.0], colors=['black'])
    axes[1].contour(xx, yy, exact, levels=[0.0], colors=['lime'], linestyles='--')
    axes[1].set_title('PAT zero / packaged mesh truth')
    heat = axes[2].imshow(np.abs(field - exact), origin='lower', extent=[x[0], x[-1], y[0], y[-1]], cmap='magma')
    axes[2].set_title('absolute error')
    fig.colorbar(heat, ax=axes[2])
    for panel in axes:
        panel.set_aspect('equal')
    fig.savefig(output / 'author-demo.png', dpi=160)
    plt.close(fig)
    print(json.dumps(record, indent=2))


def benchmark(local: Path) -> None:
    output = local / 'benchmark'
    output.mkdir(parents=True, exist_ok=True)
    mesh_root = local / 'aetheris-meshes'
    all_records: list[dict] = []
    summaries: list[dict] = []
    visual: dict[str, tuple[MeshData, PointsAsTori, np.ndarray, Callable[[np.ndarray], np.ndarray]]] = {}

    cases: list[tuple[Fixture, int, str, float, str]] = []
    for fixture in FIXTURES:
        cases.append((fixture, 2048, 'clean', 0.0, 'area'))
    sphere = FIXTURES[0]
    for count in (1024, 5000, 20000):
        cases.append((sphere, count, 'clean', 0.0, 'area'))
    cases.append((sphere, 2048, 'clean', 0.0, 'uniform-triangle'))
    for sigma in (0.01, 0.05, 0.1, 0.5):
        cases.append((sphere, 2048, 'position-noise', sigma, 'area'))
    for degrees in (2.0, 10.0, 25.0):
        cases.append((sphere, 2048, 'normal-noise', degrees, 'area'))
    cases.extend(
        [
            (sphere, 2048, 'normal-flips', 0.05, 'area'),
            (sphere, 2048, 'missing-cap', 0.10, 'area'),
            (sphere, 2048, 'missing-cap', 0.25, 'area'),
            (sphere, 2048, 'random-missing', 0.25, 'area'),
            (sphere, 2048, 'nonuniform-density', 1.0, 'area'),
            (next(fixture for fixture in FIXTURES if fixture.name == 'slab'), 2048, 'open-surface', 1.0, 'area'),
        ]
    )

    for fixture, count, scenario, level, sampling in cases:
        mesh = read_obj(mesh_root / f'{fixture.name}.obj')
        print(f'CASE {fixture.name} points={count} scenario={scenario} level={level} sampling={sampling}', flush=True)
        records, summary, pat, points, _ = run_case(fixture, mesh, count, scenario, level, sampling)
        all_records.extend(records)
        summaries.append(summary)
        key = f'{fixture.name}-{scenario}-{str(level).replace(".", "p")}'
        if (fixture.name, scenario, level) in {
            ('sphere', 'clean', 0.0),
            ('sharp_box', 'clean', 0.0),
            ('sphere', 'position-noise', 0.5),
            ('sphere', 'missing-cap', 0.25),
        }:
            visual[key] = (mesh, pat, points, truth_evaluator(fixture, mesh))

    with (output / 'query-evidence.csv').open('w', newline='', encoding='utf-8') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(all_records[0].keys()))
        writer.writeheader()
        writer.writerows(all_records)

    performance_fixture = read_obj(mesh_root / 'sphere.obj')
    perf_points, perf_normals = sample_surface(performance_fixture, 2048, SEED)
    perf_pat = PointsAsTori(perf_points, perf_normals)
    rng = np.random.default_rng(SEED + 303)
    performance = []
    for count in (1, 1000, 100000, 1000000):
        q = rng.uniform(-12.0, 12.0, size=(count, 3))
        perf_pat.signed_distance(q[: min(count, 1000)])
        started = time.perf_counter()
        perf_pat.signed_distance(q)
        elapsed = time.perf_counter() - started
        performance.append({'query_count': count, 'seconds': elapsed, 'queries_per_second': count / elapsed})

    manifest = {
        'schema': 'aetheris.pat-dogfood-x0.v1',
        'generated_utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
        'seed': SEED,
        'machine': machine_info(),
        'upstream_revision': '253bf9fc5e79831a03f0486e0c42feeb67c1c546',
        'model_sha256': 'AAA2F3882C36AC32D57361377A73AC830C47D03D6167D51249127A6C40EF7FF9',
        'model_bytes': 6366869,
        'query_performance': performance,
        'sphere_tracing': sphere_trace_probe(perf_pat),
        'storage_model': {
            'point_and_normal_bytes_per_point': 48,
            'torus_parameter_bytes_per_point': 56,
            'precompute_neighbor_index_bytes_per_point': 256,
            'query_neighbor_index_bytes_per_query': 128,
        },
        'summaries': summaries,
    }
    (output / 'summary.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    plots(output, summaries)
    for key, (mesh, pat, points, truth) in visual.items():
        axis = 1 if key.startswith('torus') else 2
        plot_slice(output, key, mesh, pat, truth, points, axis)
    print(json.dumps({'summary': str(output / 'summary.json'), 'record_count': len(all_records)}, indent=2))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['demo', 'benchmark'])
    parser.add_argument('--local-root', type=Path, required=True)
    args = parser.parse_args()
    if args.command == 'demo':
        demo(args.local_root.resolve())
    else:
        benchmark(args.local_root.resolve())


if __name__ == '__main__':
    main()
