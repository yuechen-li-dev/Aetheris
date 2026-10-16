"""Recover the V8 numeric golden only from meshes matching the existing hashes."""

import argparse
import hashlib
import json
from pathlib import Path
import struct
import zipfile


def freeze(source: Path, output: Path) -> None:
    root = Path(__file__).resolve().parent.parent
    reference = json.loads((root / "fixtures/Canonical/EditableV8/geometry-reference.expected.json").read_text())
    expected = {row["meshSha256"] for row in reference["bindings"]}
    entries = {}
    for definition in json.loads(source.read_text())["definitions"]:
        data = bytearray()
        for channel in ("positions", "normals"):
            values = definition[channel]
            data.extend(struct.pack("<i", len(values)))
            for value in values:
                data.extend(struct.pack("<d", value if value else 0))
        indices = definition["indices"]
        data.extend(struct.pack("<i", len(indices)))
        data.extend(struct.pack(f"<{len(indices)}i", *indices))
        fingerprint = hashlib.sha256(data).hexdigest().upper()
        if fingerprint not in expected:
            raise ValueError(f"Mesh does not match the original baseline: {fingerprint}")
        entries[fingerprint] = data
    if set(entries) != expected:
        raise ValueError("Missing original baseline definitions")
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w") as archive:
        for fingerprint, data in sorted(entries.items()):
            entry = zipfile.ZipInfo(fingerprint + ".bin", date_time=(2026, 10, 16, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, data, compresslevel=9)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mesh", type=Path)
    parser.add_argument("--output", type=Path, default=Path("artifacts/local/v8-ci/geometry-reference.meshes.zip"))
    args = parser.parse_args()
    freeze(args.mesh, args.output)
