"""Record real CLI admission and emitted bytes; compare against a frozen baseline."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import time


def run(args):
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    sources = sorted(p for p in (root / 'fixtures/Canonical').rglob('*')
                     if p.suffix in ('.firmament', '.firmasm', '.firmscene'))
    rows = []
    for i, source in enumerate(sources):
        relative = source.relative_to(root).as_posix()
        directory = output / source.relative_to(root / 'fixtures/Canonical')
        directory.mkdir(parents=True)
        row = {'source': relative}
        for verb in ('validate', 'build'):
            command = ['dotnet', str(args.cli.resolve()), verb, str(source), '--json']
            if verb == 'build':
                command += ['--out', str(directory / 'model.step')]
            start = time.monotonic()
            try:
                p = subprocess.run(command, capture_output=True, text=True, encoding='utf-8', timeout=120)
                (directory / (verb + '.json')).write_text(p.stdout, encoding='utf-8')
                (directory / (verb + '.stderr.txt')).write_text(p.stderr, encoding='utf-8')
                row[verb] = {'exit': p.returncode, 'seconds': round(time.monotonic() - start, 3)}
            except subprocess.TimeoutExpired:
                row[verb] = {'exit': 'timeout', 'seconds': 120}
        artifact = directory / 'model.step'
        row['sha256'] = hashlib.sha256(artifact.read_bytes()).hexdigest() if artifact.is_file() else None
        rows.append(row)
        (output / 'summary.json').write_text(json.dumps(rows, indent=2) + '\n', encoding='utf-8')
        print(f'{i+1}/{len(sources)} {relative}: {row["validate"]["exit"]}/{row["build"]["exit"]}', flush=True)
    if args.baseline:
        before = {r['source']: r for r in json.loads((args.baseline / 'summary.json').read_text())}
        differences = [r for r in rows if r['source'] not in before or
                       any(r[k]['exit'] != before[r['source']][k]['exit'] for k in ('validate', 'build')) or
                       r['sha256'] != before[r['source']]['sha256']]
        (output / 'differences.json').write_text(json.dumps(differences, indent=2) + '\n')
        print(f'{len(differences)} differences require explicit classification.')
    if any(r[v]['exit'] == 'timeout' for r in rows for v in ('validate', 'build')):
        raise SystemExit('Corpus timed out: qualification incomplete.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--cli', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--baseline', type=Path)
    run(p.parse_args())
