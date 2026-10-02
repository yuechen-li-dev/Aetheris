"""Reproduce the source-owned guitar through Aetheris.CLI; never rewrite fixtures.

Run after a Release CLI build. Optional --out selects an ignored artifact folder.
Rendering remains the independent render-guitar-x0.py downstream step.
"""
from pathlib import Path
import argparse
import hashlib
import subprocess

REPO = Path(__file__).resolve().parents[1]
ROOT = REPO / 'fixtures/Canonical/AssemblyInterfaces/GuitarX0'

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, default=REPO / 'artifacts/local/guitar-x0')
    args = parser.parse_args()
    output = args.out.resolve()
    allowed = (REPO / 'artifacts/local').resolve()
    if output != allowed and allowed not in output.parents:
        parser.error('--out must be inside artifacts/local')
    output.mkdir(parents=True, exist_ok=True)
    cli = REPO / 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'
    if not cli.exists():
        subprocess.run(['dotnet', 'build', 'Aetheris.CLI', '-c', 'Release', '-m:1'], cwd=REPO, check=True)
    sources = sorted(ROOT.glob('*.firm*'))
    before = {p: hashlib.sha256(p.read_bytes()).digest() for p in sources}
    def run(*command):
        subprocess.run(['dotnet', str(cli), *map(str, command)], cwd=REPO, check=True)
    root = ROOT / 'guitar.firmasm'
    run('inspect', root, '--json', '--profile', '--repeat', '2', '--out', output / 'inspect.json')
    run('build', root, '--output', output / 'guitar.step', '--json')
    run('asm', 'export-usd', root, output / 'guitar.usda', '--evidence', output / 'display.json', '--json')
    assert all(hashlib.sha256(p.read_bytes()).digest() == before[p] for p in sources), 'Source changed during reproduction'
    print(f'Guitar reproduced from immutable authored sources: {output}')

if __name__ == '__main__':
    main()
