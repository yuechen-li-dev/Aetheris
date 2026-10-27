"""Mutate every real opening brace in admitted canonical seeds through the real CLI.

This is a qualification mutator, not a language validator. Strings and comments
are skipped; the compiler owns all admission decisions and route classification.
"""
import argparse
import collections
import json
from pathlib import Path
import re
import shutil
import subprocess


def run(args):
    root = Path(__file__).resolve().parents[1]
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=args.resume)
    sources = output / 'sources' / 'Canonical'
    shutil.copytree(root / 'fixtures' / 'Canonical', sources, dirs_exist_ok=args.resume)
    baseline = json.loads(args.seeds.read_text(encoding='utf-8'))
    seeds = [r['source'] for r in baseline if r['validate']['exit'] == 0]
    previous = json.loads((output / 'summary.json').read_text(encoding='utf-8')) if args.resume else {}
    rows = previous.get('probes', [])
    controls = previous.get('controls', [])
    completed = {r['source'] for r in controls if r['exit'] == 0}
    controls = [r for r in controls if r['source'] in completed]
    # Mirror referenced repository files without copying an entire STEP corpus.
    # Paths outside the repository or the qualification directory are never copied.
    for relative in seeds:
        original = root / relative
        destination = sources / Path(relative).relative_to('fixtures/Canonical')
        for literal in re.findall(r'"([^"\n]+)"', original.read_text(encoding='utf-8')):
            requested = Path(literal)
            if requested.is_absolute():
                continue
            dependency = (original.parent / requested).resolve()
            target = (destination.parent / requested).resolve()
            if dependency.is_relative_to(root) and target.is_relative_to(output) and dependency.is_file() and not target.exists():
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(dependency, target)
    # Token-shaped mutation boundaries only; never insert into strings/comments.
    token = re.compile(r'//[^\r\n]*|/\*[\s\S]*?\*/|"(?:\\.|[^"\\])*"|\{')

    def validate(path):
        try:
            p = subprocess.run(['dotnet', str(args.cli.resolve()), 'validate', str(path), '--json'],
                               capture_output=True, text=True, encoding='utf-8', timeout=60)
            try:
                payload = json.loads(p.stdout)
            except json.JSONDecodeError:
                payload = {'stderr': p.stderr, 'stdout': p.stdout}
            report = payload.get('firmamentV2Validation', payload)
            return p.returncode, report.get('parserRoute', report.get('domain', 'unreported')), payload
        except subprocess.TimeoutExpired:
            return 'timeout', 'timeout', {}

    for index, relative in enumerate(seeds):
        if relative in completed:
            continue
        source = (root / relative).read_text(encoding='utf-8')
        path = sources / Path(relative).relative_to('fixtures/Canonical')
        exit_code, route, payload = validate(path)
        controls.append({'source': relative, 'exit': exit_code, 'route': route})
        if exit_code != 0:
            (path.parent / (path.name + '.control.json')).write_text(json.dumps(payload, indent=2), encoding='utf-8')
            continue  # Missing external resources must not masquerade as field rejection.
        try:
            for match in token.finditer(source):
                if match.group() != '{':
                    continue
                position = match.end()
                path.write_text(source[:position] + ' zzUnknownField: 3mm; ' + source[position:], encoding='utf-8')
                code, selected, evidence = validate(path)
                row = {'source': relative, 'offset': position, 'exit': code, 'route': selected}
                rows.append(row)
                if code == 0 or code == 'timeout':
                    evidence_path = output / ('accepted' if code == 0 else 'timeouts') / (str(len(rows)) + '.json')
                    evidence_path.parent.mkdir(exist_ok=True)
                    evidence_path.write_text(json.dumps(evidence, indent=2), encoding='utf-8')
        finally:
            path.write_text(source, encoding='utf-8')
        summary = {'seeds': len(seeds), 'controls': controls, 'probes': rows,
                   'acceptedByRoute': dict(collections.Counter(r['route'] for r in rows if r['exit'] == 0))}
        (output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
        print(f'{index+1}/{len(seeds)} {relative}: {len(rows)} probes', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cli', type=Path, required=True)
    parser.add_argument('--seeds', type=Path, required=True, help='Frozen corpus summary.json')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--resume', action='store_true', help='Resume completed seeds and retry unavailable controls')
    run(parser.parse_args())
