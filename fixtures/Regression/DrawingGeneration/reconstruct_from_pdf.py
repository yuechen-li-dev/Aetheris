#!/usr/bin/env python3
"""Independent reader for the visible dimensioned-plate PDF convention.

Requires pypdf. Input is ONLY the PDF, never the source part or Drawing IR.
Reads width/height/thickness size lines in their visible view sequence and the
hole coordinate/type schedule; emits and optionally builds a fresh Firmament part.
This is a bounded reconstruction witness, not a general drawing OCR system.
"""
from __future__ import annotations
import argparse
from decimal import Decimal
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
from pypdf import PdfReader

NUMBER = r"(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][+-]?\d+)?"


def read_definition(text: str) -> dict:
    sizes = re.findall(rf"(?m)^({NUMBER})\s+±({NUMBER})\s*$", text)
    if len(sizes) != 3:
        raise ValueError('Expected three controlling stock size/limit dimensions; reference envelopes do not qualify.')
    width, height, thickness = [float(pair[0]) for pair in sizes]
    if min(width, height, thickness) <= 0:
        raise ValueError('Stock dimensions must be positive.')
    if 'X FROM LEFT FINISHED EDGE; Y FROM BOTTOM FINISHED EDGE.' not in text:
        raise ValueError('The coordinate reference edges are missing.')
    if 'X/Y AT TOP FACE; HOLE AXES NOMINALLY NORMAL; THRU.' not in text:
        raise ValueError('Hole entry plane, orientation or extent is missing.')
    groups = {}
    for identity, count, diameter, plus, minus in re.findall(rf"(?m)^(D\d+): (\d+)X Ø({NUMBER}) \+({NUMBER})/-({NUMBER}) THRU\s*$", text):
        groups[identity] = {'count': int(count), 'diameter': float(diameter), 'plus': float(plus), 'minus': float(minus), 'extent': 'THRU'}
    if not groups:
        raise ValueError('No complete through-hole size/limit specification was found.')
    holes = []
    for identity, x, y, group in re.findall(rf"(?m)^([A-Za-z_]\w*) ({NUMBER}) ({NUMBER}) (D\d+)\s*$", text):
        if group not in groups:
            raise ValueError('A hole refers to an undefined size/extent group.')
        holes.append({'identity': identity, 'x': float(x), 'y': float(y), 'group': group, **groups[group]})
    if len({h['identity'] for h in holes}) != len(holes):
        raise ValueError('Duplicate hole identities.')
    for identity, specification in groups.items():
        if sum(h['group'] == identity for h in holes) != specification['count']:
            raise ValueError('The hole table does not account for the specified quantity.')
    coordinates = re.search(rf'STOCK SIZES AND X/Y COORDINATES: ±({NUMBER}) mm\.', text)
    angles = re.search(rf'UNLESS SPECIFIED: ANGLES ±({NUMBER})°\.', text)
    if not coordinates or not angles:
        raise ValueError('General dimensional acceptance requirements are missing.')
    values = [width, height, thickness] + [float(v) for pair in sizes for v in pair] + [h[k] for h in holes for k in ('x', 'y', 'diameter', 'plus', 'minus')]
    if not all(math.isfinite(v) for v in values):
        raise ValueError('Nonfinite dimension.')
    return {'width': width, 'height': height, 'thickness': thickness, 'stockTolerances': [float(pair[1]) for pair in sizes],
            'coordinateTolerance': float(coordinates[1]), 'angularToleranceDegrees': float(angles[1]), 'groups': groups, 'holes': holes}


def reconstruct(definition: dict) -> str:
    def number(n):
        return format(Decimal(str(n)), 'f')
    width, height, thickness = (definition[k] for k in ('width', 'height', 'thickness'))
    lines = ['// Reconstructed from visible PDF dimensions and hole schedule only.', 'Model ReconstructedPlate {', '  units: mm',
             f'  Box Stock {{ size: [{number(width)}mm, {number(height)}mm, {number(thickness)}mm] }}', '  Modify Stock {']
    for hole in definition['holes']:
        lines += [f"    Hole<Shaft> {hole['identity']} {{", '      on: +Z',
                  f"      center: Point2({number(hole['x'] - width / 2)}mm, {number(hole['y'] - height / 2)}mm)",
                  f"      diameter: {number(hole['diameter'])}mm", '      end: ThroughAll', '    }']
    return '\n'.join(lines + ['  }', '}']) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pdf', type=Path, required=True)
    parser.add_argument('--out-dir', type=Path, required=True)
    parser.add_argument('--cli', type=Path, help='Optional real aetheris executable; builds the independently reconstructed part.')
    args = parser.parse_args()
    text = '\n'.join(page.extract_text() for page in PdfReader(args.pdf).pages)
    definition = read_definition(text)
    # Missing stock sizes, centers, extent and location tolerance must fail reconstruction.
    mutations = {
        'missing_stock_size': re.sub(rf'(?m)^{NUMBER}\s+±{NUMBER}\s*$', '', text, count=1),
        'missing_hole_row': re.sub(rf'(?m)^[A-Za-z_]\w* {NUMBER} {NUMBER} D\d+\s*$', '', text, count=1),
        'missing_extent': text.replace(' THRU', ''),
        'missing_coordinate_tolerance': text.replace('STOCK SIZES AND X/Y COORDINATES:', 'REFERENCE COORDINATES:'),
    }
    for name, mutant in mutations.items():
        try:
            read_definition(mutant)
        except ValueError:
            continue
        raise AssertionError(f'Incomplete drawing was admitted: {name}')
    args.out_dir.mkdir(parents=True, exist_ok=True)
    source = args.out_dir / 'reconstructed.firmament'
    source.write_text(reconstruct(definition), encoding='utf-8')
    evidence = {'sourcePdfSha256': hashlib.sha256(args.pdf.read_bytes()).hexdigest(), 'inputAuthority': 'visible PDF text only',
                'definition': definition, 'negativeCasesRejected': list(mutations),
                'reconstructionSha256': hashlib.sha256(source.read_bytes()).hexdigest()}
    # Persist the reconstruction BEFORE any optional comparison outside this reader.
    (args.out_dir / 'pdf-definition.json').write_text(json.dumps(evidence, indent=2), encoding='utf-8')
    if args.cli:
        result = subprocess.run([str(args.cli.resolve()), 'build', str(source.resolve()), '--output', str((args.out_dir / 'reconstructed.step').resolve())], capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
    print(json.dumps({'success': True, 'holes': len(definition['holes']), 'negativeCasesRejected': len(mutations), 'source': str(source)}))


if __name__ == '__main__':
    main()
