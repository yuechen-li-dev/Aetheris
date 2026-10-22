#!/usr/bin/env python3
"""Rebuild the authored bracket from PDF text only, then optionally compare STEP.

Bounded drawing convention: five controlling dimensions in printed view order
(width, height, leg width, leg height, thickness), origin O, and H1's two tables.
No source, Drawing IR or reference STEP is read until reconstruction is frozen.
Requires pypdf and the real Aetheris CLI. Does not update any baseline.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
from pypdf import PdfReader

NUMBER = r"\d+(?:\.\d+)?"


def definition(text):
    sizes = re.findall(rf"(?m)^({NUMBER}) ±({NUMBER})", text)
    if len(sizes) != 5 or any(float(t) <= 0 for _, t in sizes):
        raise ValueError("Five controlling dimensions with positive limits are required.")
    required = ["O (X0, Y0)", "O: lower-left TOP vertex. +X right, +Y up.",
                "Six straight edges.", "Uniform thickness; hole axis normal to A.",
                "CSK enters DATUM A (TOP); shaft is THRU.", "ALL DIMENSIONS mm."]
    if any(value not in text for value in required):
        raise ValueError("Missing origin, boundary, units, entry or axis/extent definition.")
    center = re.search(rf"H1 CENTER FROM O / mm\s+ID X ±({NUMBER}) Y ±({NUMBER})\s+H1 ({NUMBER}) ({NUMBER})", text)
    hole = re.search(rf"H1 COUNTERSUNK THROUGH HOLE\s+THRU Ø ±({NUMBER}) CSK Ø ±({NUMBER}) Angle ±({NUMBER})°\s+({NUMBER}) ({NUMBER}) ({NUMBER})°", text)
    angles = re.search(rf"Unspecified angles 90° ±({NUMBER})°", text)
    if not angles or float(angles[1]) <= 0:
        raise ValueError("The six-edge outline has no right-angle definition/limits.")
    if not center or not hole:
        raise ValueError("Missing hole location, size, countersink or limits.")
    if any(float(v) <= 0 for v in (*center.groups()[:2], *hole.groups()[:3])):
        raise ValueError("Coordinate, diameter and angle limits must be positive.")
    w, h, lw, lh, t = [float(v) for v, _ in sizes]
    x, y = map(float, center.groups()[2:])
    d, entry, angle = map(float, hole.groups()[3:])
    if not (0 < lw < w and 0 < lh < h and t > 0 and 0 < d < entry and 0 < angle < 180):
        raise ValueError("Invalid nominal bracket dimensions.")
    depth = (entry - d) / (2 * math.tan(math.radians(angle / 2)))
    if depth >= t:
        raise ValueError("No positive cylindrical shaft remains.")
    return dict(width=w, height=h, legWidth=lw, legHeight=lh, thickness=t, holeX=x, holeY=y,
                shaftDiameter=d, entryDiameter=entry, includedAngle=angle,
                linearLimits=[float(limit) for _, limit in sizes],
                centerLimits=list(map(float, center.groups()[:2])), holeLimits=list(map(float, hole.groups()[:3])),
                generalAngle=90, generalAngleTolerance=float(angles[1]))


def reconstruct(p):
    # An independent Path construction, not the original pair of Rect2 guides.
    w, h, lw, lh, t, x, y, d, e, a = (p[k] for k in (
        'width', 'height', 'legWidth', 'legHeight', 'thickness', 'holeX', 'holeY', 'shaftDiameter', 'entryDiameter', 'includedAngle'))
    return f"""// Independently reconstructed from the visible PDF only.
Model ReconstructedBracket {{
  units: mm
  Path Boundary {{
    start: Point2(0mm, 0mm); heading: 0deg
    Line South {{ length: {w}mm }}
    Line East {{ turn: 90deg; length: {lh}mm }}
    Line Inner {{ turn: 90deg; length: {w-lw}mm }}
    Line Upright {{ turn: -90deg; length: {h-lh}mm }}
    Line North {{ turn: 90deg; length: {lw}mm }}
    Close West
  }}
  Profile Outline from Boundary
  Struct Part {{ Compose Body {{ Base Stock {{ profile: Outline; from: 0mm; to: {t}mm }} }} }}
  Modify Body {{ Hole<Countersink> H1 {{
    on: +Z; center: Point2({x}mm, {y}mm); diameter: {d}mm
    countersinkDiameter: {e}mm; countersinkAngle: {a}deg; end: ThroughAll
  }} }}
}}
"""


def geometry(path):
    """Independent canonical STEP witness: topology vertices and analytic supports."""
    entities = dict((int(i), v) for i, v in re.findall(r"(?m)^#(\d+)=(.*);\s*$", path.read_text(encoding='utf-8')))
    def ref(value): return [int(v) for v in re.findall(r'#(\d+)', value)]
    def vector(value): return [float(v) for v in value[value.rfind('(') + 1:].rstrip(')').split(',')]
    def placement(value):
        p, z, x = ref(entities[ref(value)[0]])
        return vector(entities[p]), vector(entities[z]), vector(entities[x])
    vertices = sorted(vector(entities[ref(v)[0]]) for v in entities.values() if v.startswith('VERTEX_POINT('))
    supports = []
    for v in entities.values():
        kind = v.partition('(')[0]
        if kind not in ('CYLINDRICAL_SURFACE', 'CONICAL_SURFACE'):
            continue
        station, axis, _ = placement(v)
        measures = [float(n) for n in v.rstrip(')').split(',')[2:]]
        supports.append([kind, station, axis, measures])
    return dict(vertices=vertices, supports=sorted(supports))


def close(a, b):
    if isinstance(a, (list, tuple)):
        return len(a) == len(b) and all(close(x, y) for x, y in zip(a, b))
    if isinstance(a, dict): return a.keys() == b.keys() and all(close(a[k], b[k]) for k in a)
    return abs(a-b) <= 1e-8 if isinstance(a, (float, int)) else a == b


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pdf', type=Path, required=True)
    parser.add_argument('--out-dir', type=Path, required=True)
    parser.add_argument('--cli', type=Path, required=True)
    parser.add_argument('--reference-step', type=Path)
    args = parser.parse_args()
    text = '\n'.join(page.extract_text() for page in PdfReader(args.pdf).pages)
    p = definition(text)
    mutants = {
        'missing_size': re.sub(rf'(?m)^{NUMBER} ±{NUMBER}', '', text, count=1),
        'missing_origin': text.replace('O (X0, Y0)', ''),
        'missing_coordinate_limits': text.replace('X ±0.1', 'X'),
        'missing_hole_row': re.sub(rf'(?m)^H1 {NUMBER} {NUMBER}', '', text),
        'missing_countersink_angle': text.replace('Angle ±0.5°', 'Angle'),
        'missing_through_extent': text.replace('shaft is THRU.', 'shaft.'),
        'missing_axis_orientation': text.replace('hole axis normal to A.', 'hole.'),
        'missing_outline_angles': text.replace('Unspecified angles 90° ±0.5°.', ''),
        'missing_cone_angle_value': re.sub(rf'(?m)^({NUMBER} {NUMBER}) 90°', r'\1', text)
    }
    for name, mutant in mutants.items():
        try: definition(mutant)
        except ValueError: continue
        raise AssertionError('Incomplete PDF was accepted: ' + name)
    args.out_dir.mkdir(parents=True, exist_ok=True)
    source = args.out_dir / 'reconstructed.firmament'
    source.write_text(reconstruct(p), encoding='utf-8', newline='')
    frozen_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    result = {'pdfOnlyDefinition': p, 'negativeCasesRejected': list(mutants), 'reconstructionSha256': frozen_hash}
    (args.out_dir / 'pdf-definition.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    cli = ['dotnet', str(args.cli.resolve())] if args.cli.suffix == '.dll' else [str(args.cli.resolve())]
    def run(*arguments):
        process = subprocess.run(cli + list(map(str, arguments)), capture_output=True, text=True, encoding='utf-8')
        if process.returncode: raise RuntimeError(process.stdout + process.stderr)
        return json.loads(process.stdout)
    rebuilt = args.out_dir / 'reconstructed.step'
    run('build', source, '--out', rebuilt, '--json')
    if args.reference_step:
        # Original geometry is intentionally read only after the PDF source was frozen.
        expected = run('inspect', args.reference_step, '--json')['summary']
        actual = run('inspect', rebuilt, '--json')['summary']
        for key in ('bodyCount', 'shellCount', 'faceCount', 'edgeCount', 'vertexCount', 'surfaceFamilies', 'boundingBox'):
            assert close(expected[key], actual[key]), (key, expected[key], actual[key])
        assert close(geometry(args.reference_step), geometry(rebuilt)), 'STEP vertices or analytic cylinder/cone supports differ.'
        ma = run('verify', args.reference_step, '--json', '--evidence-dir', args.out_dir / 'reference-verification')['brepMassProperties']
        mb = run('verify', rebuilt, '--json', '--evidence-dir', args.out_dir / 'rebuilt-verification')['brepMassProperties']
        assert abs(ma['absoluteVolume'] - mb['absoluteVolume']) <= 1e-8
        assert hashlib.sha256(source.read_bytes()).hexdigest() == frozen_hash
        result.update(referenceReadAfterFreeze=True, matchingGeometry=True, referenceVolume=ma['absoluteVolume'], rebuiltVolume=mb['absoluteVolume'])
    (args.out_dir / 'pdf-qualification.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
