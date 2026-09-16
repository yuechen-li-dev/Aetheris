"""Fetch only hash-pinned inspection inputs, never a canonical fitting dataset.

Default output is the repository's ignored artifacts/local/humanoid-recon-x0.
No recursive submodules, source code execution, or license approval is performed.
"""
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/local/humanoid-recon-x0'
MANIFEST = ROOT / 'docs/release/HUMANOID-RECON-X0.manifest.json'


def entries():
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
    for asset in manifest['assets']:
        repo = asset['source'].removeprefix('https://github.com/')
        if repo not in ('Upliner/CharMorph-db', 'vrm-c/vrm-specification'):
            raise ValueError('Unexpected research repository')
        files = asset.get('filesSha256')
        records = [(asset['path'] + '/' + path, digest) for path, digest in files.items()] if files else [(asset['path'], asset['sha256'])]
        for relative, digest in records:
            target = (OUT / repo.split('/')[-1] / relative).resolve()
            if not target.is_relative_to(OUT.resolve()):
                raise ValueError('Input path escapes research output')
            url = 'https://raw.githubusercontent.com/' + repo + '/' + asset['revision'] + '/' + relative
            yield url, target, digest


def verify_inputs():
    for _, target, digest in entries():
        if not target.is_file() or hashlib.sha256(target.read_bytes()).hexdigest() != digest:
            raise ValueError('Missing or changed research input: ' + str(target))


def main():
    count = 0
    for url, target, digest in entries():
        if target.exists():
            data = target.read_bytes()
        else:
            with urllib.request.urlopen(url, timeout=60) as response:
                data = response.read()
        if hashlib.sha256(data).hexdigest() != digest:
            raise ValueError('Checksum mismatch; input not written: ' + str(target))
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        count += 1
    print(f'Verified {count} pinned inspection files. No asset approved for canonical fitting.')


if __name__ == '__main__':
    main()
