#!/usr/bin/env python3
"""Freeze a PDF-only reconstruction, then compare it with a canonical mm STEP.
Requires pypdf and the real Aetheris CLI. No baselines are rewritten.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import sys


def run(cli, *args):
    p = subprocess.run([str(cli), *map(str, args)], capture_output=True, text=True)
    if p.returncode:
        raise RuntimeError(p.stdout + p.stderr)
    return json.loads(p.stdout)


def cylinders(step, bounds):
    # Verification-only readback of canonical emitted primitives. This is deliberately
    # independent of both the dimension planner and its geometric recognition query.
    text = step.read_text(encoding='utf-8')
    if '.MILLI.,.METRE.' not in text:
        raise ValueError('This verification adapter requires canonical millimetre STEP.')
    entities = {int(i): value for i, value in re.findall(r'(?m)^#(\d+)=(.*);\s*$', text)}
    result = []
    for value in entities.values():
        if not value.startswith('CYLINDRICAL_SURFACE('):
            continue
        placement = entities[int(re.findall(r'#(\d+)', value)[0])]
        point = entities[int(re.findall(r'#(\d+)', placement)[0])]
        coordinates = [float(n) for n in point[point.rfind('(') + 1:].rstrip(')').split(',')]
        radius = float(value[value.rfind(',') + 1:].rstrip(')'))
        result.append((coordinates[0] - bounds['min']['x'], coordinates[1] - bounds['min']['y'], radius))
    return sorted(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pdf', type=Path, required=True)
    parser.add_argument('--reference-step', type=Path, required=True)
    parser.add_argument('--out-dir', type=Path, required=True)
    parser.add_argument('--cli', type=Path, required=True)
    args = parser.parse_args()
    cli = args.cli.resolve()
    reader = Path(__file__).with_name('reconstruct_from_pdf.py')
    # The child receives only a PDF and an output directory, and compiles a fresh source.
    subprocess.run([sys.executable, str(reader), '--pdf', str(args.pdf), '--out-dir', str(args.out_dir), '--cli', str(cli)], check=True)
    frozen = args.out_dir / 'reconstructed.firmament'
    frozen_hash = hashlib.sha256(frozen.read_bytes()).hexdigest()
    # Original model data is read only AFTER the independent reconstruction is frozen.
    rebuilt = args.out_dir / 'reconstructed.step'
    a = run(cli, 'inspect', args.reference_step, '--json')['summary']
    b = run(cli, 'inspect', rebuilt, '--json')['summary']
    for field in ('bodyCount', 'shellCount', 'faceCount', 'edgeCount', 'vertexCount', 'surfaceFamilies', 'structuralAssessment'):
        assert a[field] == b[field], (field, a[field], b[field])
    for axis in 'xyz':
        extent_a = a['boundingBox']['max'][axis] - a['boundingBox']['min'][axis]
        extent_b = b['boundingBox']['max'][axis] - b['boundingBox']['min'][axis]
        assert abs(extent_a - extent_b) < 1e-8
    ca = cylinders(args.reference_step, a['boundingBox'])
    cb = cylinders(rebuilt, b['boundingBox'])
    assert len(ca) == len(cb) and all(abs(x-y) < 1e-8 for left, right in zip(ca, cb) for x, y in zip(left, right)), (ca, cb)
    ma = run(cli, 'verify', args.reference_step, '--json', '--evidence-dir', args.out_dir / 'verify-reference')['brepMassProperties']
    mb = run(cli, 'verify', rebuilt, '--json', '--evidence-dir', args.out_dir / 'verify-rebuilt')['brepMassProperties']
    va, vb = ma['absoluteVolume'], mb['absoluteVolume']
    assert abs(va-vb) <= 1e-9 * max(1, abs(va)), (va, vb)
    definition = json.loads((args.out_dir / 'pdf-definition.json').read_text(encoding='utf-8'))['definition']
    analytic_pdf = definition['width'] * definition['height'] * definition['thickness'] - sum(math.pi * h['diameter']**2 / 4 * definition['thickness'] for h in definition['holes'])
    extents = [a['boundingBox']['max'][axis] - a['boundingBox']['min'][axis] for axis in 'xyz']
    analytic_step = math.prod(extents) - sum(math.pi * radius**2 * extents[2] for _, _, radius in ca)
    assert abs(analytic_pdf - analytic_step) <= 1e-9 * max(1, abs(analytic_step))
    assert hashlib.sha256(frozen.read_bytes()).hexdigest() == frozen_hash
    evidence = {'success': True, 'reconstructionFrozenBeforeReferenceRead': True, 'reconstructedSourceSha256': frozen_hash,
                'matchingTopology': {k: a[k] for k in ('bodyCount', 'shellCount', 'faceCount', 'edgeCount', 'vertexCount')},
                'matchingCylinderAxesXYAndRadii': ca, 'referenceVolume': va, 'reconstructedVolume': vb,
                'kernelVolumeStatus': ma['status'], 'analyticNominalVolumeFromPdf': analytic_pdf,
                'analyticNominalVolumeFromReferencePrimitives': analytic_step,
                'kernelReportedMinusAnalyticVolume': va - analytic_step,
                'numericComparisonToleranceMillimetres': 1e-8, 'absoluteVolumeRelativeTolerance': 1e-9}
    (args.out_dir / 'roundtrip-qualification.json').write_text(json.dumps(evidence, indent=2), encoding='utf-8')
    print(json.dumps(evidence, indent=2))


if __name__ == '__main__':
    main()
